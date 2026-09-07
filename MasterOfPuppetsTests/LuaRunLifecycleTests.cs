using MasterOfPuppets.LuaScripting;
using MasterOfPuppets.LuaScripting.Runs;
using MasterOfPuppets.LuaScripting.Runtime;

using Xunit;

public class LuaRunLifecycleTests {
    [Fact]
    public void RunLogBuffer_IsBoundedOrderedAndTruncatesMessages() {
        var logs = new LuaRunLogBuffer(capacity: 2);
        logs.Append("INFO", "one", DateTimeOffset.FromUnixTimeSeconds(1));
        logs.Append("warning", "two", DateTimeOffset.FromUnixTimeSeconds(2));
        logs.Append("error", new string('x', LuaRunLogBuffer.MaximumMessageLength + 10), DateTimeOffset.FromUnixTimeSeconds(3));

        var snapshot = logs.Snapshot();
        Assert.Equal(2, snapshot.Count);
        Assert.Equal("two", snapshot[0].Message);
        Assert.Equal("error", snapshot[1].Level);
        Assert.Equal(LuaRunLogBuffer.MaximumMessageLength, snapshot[1].Message.Length);
        Assert.True(snapshot[0].Sequence < snapshot[1].Sequence);
    }

    [Fact]
    public void RunInstance_Tracks_Pause_Resume_Stop_And_Terminal_Snapshot() {
        using var run = new LuaRunInstance(
            "run-1", "Show", new string('a', 64), 2, 8, 123, TimeSpan.FromMinutes(1));

        run.MarkWaiting("synchronizing");
        Assert.True(run.Pause());
        Assert.Equal(LuaRunState.Paused, run.Snapshot.State);
        Assert.True(run.Resume());
        Assert.Equal(LuaRunState.Waiting, run.Snapshot.State);
        run.MarkRunning("performing");
        Assert.True(run.RequestStop("operator stop"));
        Assert.True(run.CancellationToken.IsCancellationRequested);
        run.CompleteCancellation();

        var snapshot = run.Snapshot;
        Assert.Equal(LuaRunState.Stopped, snapshot.State);
        Assert.Equal("operator stop", snapshot.StopReason);
        Assert.NotNull(snapshot.StartedAt);
        Assert.NotNull(snapshot.EndedAt);
        Assert.Contains("run-1", snapshot.ToStatusText());
    }

    [Fact]
    public async Task RunInstance_Distinguishes_Maximum_Duration_Timeout() {
        using var run = new LuaRunInstance(
            "run-timeout", "Timed", new string('b', 64), 0, 1, 1, TimeSpan.FromMilliseconds(40));
        run.MarkRunning();

        await WaitUntilAsync(() => run.CancellationToken.IsCancellationRequested, TimeSpan.FromSeconds(2));
        run.CompleteCancellation();

        Assert.Equal(LuaRunState.TimedOut, run.Snapshot.State);
        Assert.Contains("maximum run time", run.Snapshot.StopReason);
    }

    [Fact]
    public void RunInstance_Captures_Structured_Quota_Error() {
        using var run = new LuaRunInstance(
            "run-failed", "Failed", new string('c', 64), 0, 1, 1, TimeSpan.FromMinutes(1));
        run.MarkRunning();

        run.Fail(new LuaQuotaExceededException("instructions", "budget exceeded"));

        Assert.Equal(LuaRunState.Failed, run.Snapshot.State);
        Assert.Equal("quota:instructions", run.Snapshot.Error?.Category);
        Assert.Equal("budget exceeded", run.Snapshot.Error?.Message);
        Assert.NotEmpty(run.Snapshot.Error?.StackTrace ?? string.Empty);
    }

    [Fact]
    public async Task VmInstructionHook_Stops_A_Tight_Loop_At_The_Budget() {
        var limits = LuaRuntimeLimits.Default with {
            InstructionHookInterval = 500,
            MaximumInstructions = 2_000,
        };
        using var runner = new LuaScriptRunner(new LuaScriptContext(
            0, 1, 1, _ => { }, Limits: limits));

        var exception = await Assert.ThrowsAnyAsync<Exception>(() =>
            runner.RunAsync("while true do end", CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(2)));

        Assert.Contains("instruction budget", exception.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task VmInstructionHook_Pauses_And_Resumes_A_Tight_Loop() {
        var control = new LuaExecutionControl();
        var limits = LuaRuntimeLimits.Default with {
            InstructionHookInterval = 500,
            MaximumInstructions = 20_000_000,
        };
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        using var runner = new LuaScriptRunner(new LuaScriptContext(
            0, 1, 1, _ => { }, Limits: limits, ExecutionControl: control));
        var task = runner.RunAsync("while true do end", cts.Token);

        await WaitUntilAsync(() => control.Instructions >= 2_000, TimeSpan.FromSeconds(1));
        Assert.True(control.Pause());
        await Task.Delay(50);
        var pausedAt = control.Instructions;
        await Task.Delay(100);
        Assert.InRange(control.Instructions - pausedAt, 0, limits.InstructionHookInterval);
        Assert.False(task.IsCompleted);

        Assert.True(control.Resume());
        await WaitUntilAsync(
            () => control.Instructions > pausedAt + limits.InstructionHookInterval,
            TimeSpan.FromSeconds(1));
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
    }

    [Fact]
    public async Task RuntimeStop_Requests_Host_Control_With_Reason() {
        string? reason = null;
        using var runner = new LuaScriptRunner(new LuaScriptContext(
            0, 1, 1, _ => { }, RequestStop: value => reason = value));

        await runner.RunAsync("mop.runtime.stop('show complete')", CancellationToken.None);

        Assert.Equal("show complete", reason);
    }

    [Fact]
    public async Task RuntimeGlobalStopAndEmoteResync_UseTypedHostCallbacks() {
        string? stopReason = null;
        (uint Id, bool Persistent, ulong Target)? emote = null;
        using var runner = new LuaScriptRunner(new LuaScriptContext(
            0, 1, 1, _ => { },
            RequestGlobalStop: (reason, _) => {
                stopReason = reason;
                return Task.FromResult(true);
            },
            RequestEmoteResync: (id, persistent, target, _) => {
                emote = (id, persistent, target);
                return Task.FromResult(true);
            }));

        await runner.RunAsync("""
            assert(mop.runtime.global_stop("target lost"))
            assert(mop.runtime.broadcast_emote_resync(16, true, "123456789"))
            """, CancellationToken.None);

        Assert.Equal("target lost", stopReason);
        Assert.Equal((16u, true, 123456789ul), emote);
    }

    [Fact]
    public void RunId_Is_Deterministic_Across_Participants_And_Changes_With_Identity() {
        const string hash = "6338b6ed83e7cb7db084db1caa3891f194e3e40f8e3a85d9d3cbc15d709774ff";
        var first = LuaScriptManager.CreateRunId(639233833003323930, 793223713, hash);

        Assert.Equal(first, LuaScriptManager.CreateRunId(639233833003323930, 793223713, hash));
        Assert.NotEqual(first, LuaScriptManager.CreateRunId(639233833003323931, 793223713, hash));
        Assert.NotEqual(first, LuaScriptManager.CreateRunId(639233833003323930, 793223714, hash));
    }

    [Fact]
    public void ResourceLeases_Allow_Disjoint_Runs_And_Reject_Conflicts() {
        var manager = new LuaResourceLeaseManager();

        Assert.True(manager.TryAcquire(
            "movement-run",
            LuaResourceKind.Movement,
            out var movement,
            out var firstConflict));
        Assert.Null(firstConflict);
        Assert.True(manager.TryAcquire(
            "chat-run",
            LuaResourceKind.ChatActionBudget,
            out var chat,
            out var secondConflict));
        Assert.Null(secondConflict);
        Assert.False(manager.TryAcquire(
            "other-movement",
            LuaResourceKind.Movement | LuaResourceKind.GameActions,
            out var rejected,
            out var conflict));
        Assert.Null(rejected);
        Assert.Equal(LuaResourceKind.Movement, conflict?.Resource);
        Assert.Equal("movement-run", conflict?.OwnerRunId);

        movement!.Dispose();
        Assert.True(manager.TryAcquire(
            "other-movement",
            LuaResourceKind.Movement,
            out var acquiredAfterRelease,
            out _));
        acquiredAfterRelease!.Dispose();
        chat!.Dispose();
        Assert.Empty(manager.SnapshotOwners());
    }

    [Fact]
    public void ScriptResourceDeclarations_Preserve_Legacy_Exclusivity_And_Allow_Explicit_None() {
        var legacy = new LuaScriptDefinition { Name = "Legacy", Source = "return" };
        var observationOnly = new LuaScriptDefinition {
            Name = "Observer",
            Source = "return",
            RequiredResources = LuaResourceKind.None,
            DeclaredCapabilities = ["mop.runtime"],
        };

        legacy.Validate();
        observationOnly.Validate();

        Assert.Equal(LuaResourceKinds.LegacyExclusive, legacy.ResolveRequiredResources());
        Assert.Equal(LuaResourceKind.None, observationOnly.ResolveRequiredResources());
        Assert.Equal(["mop.runtime"], observationOnly.DeclaredCapabilities);
    }

    private static async Task WaitUntilAsync(Func<bool> predicate, TimeSpan timeout) {
        var started = DateTime.UtcNow;
        while (!predicate()) {
            if (DateTime.UtcNow - started > timeout)
                throw new TimeoutException("Condition was not reached within the test timeout.");
            await Task.Delay(5);
        }
    }
}
