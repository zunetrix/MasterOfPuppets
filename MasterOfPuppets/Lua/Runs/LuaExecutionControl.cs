using System;
using System.Threading;
using System.Threading.Tasks;

using MasterOfPuppets.LuaScripting.Runtime;

namespace MasterOfPuppets.LuaScripting.Runs;

/// <summary>
/// Run-local cooperative control reached by VM instruction hooks and awaitable
/// host calls. It never touches a Lua state from another thread.
/// </summary>
public sealed class LuaExecutionControl {
    private readonly object _lock = new();
    private TaskCompletionSource<bool>? _resumeSignal;
    private long _instructions;

    public bool IsPaused {
        get { lock (_lock) return _resumeSignal != null; }
    }

    public long Instructions => Interlocked.Read(ref _instructions);

    public bool Pause() {
        lock (_lock) {
            if (_resumeSignal != null)
                return false;
            _resumeSignal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            return true;
        }
    }

    public bool Resume() {
        TaskCompletionSource<bool>? signal;
        lock (_lock) {
            signal = _resumeSignal;
            _resumeSignal = null;
        }
        return signal?.TrySetResult(true) == true;
    }

    public async ValueTask CheckpointAsync(
        int executedInstructions,
        int callStackFrames,
        LuaRuntimeLimits limits,
        CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        var total = Interlocked.Add(ref _instructions, executedInstructions);
        if (total > limits.MaximumInstructions)
            throw new LuaQuotaExceededException(
                "instructions",
                $"Lua instruction budget exceeded ({limits.MaximumInstructions:N0}).");
        if (callStackFrames > limits.MaximumCallStackFrames)
            throw new LuaQuotaExceededException(
                "call-stack",
                $"Lua call stack exceeds {limits.MaximumCallStackFrames} frames.");

        Task? wait;
        lock (_lock)
            wait = _resumeSignal?.Task;
        if (wait != null)
            await wait.WaitAsync(cancellationToken);
        else
            await Task.Yield();
    }

    public async ValueTask WaitIfPausedAsync(CancellationToken cancellationToken) {
        Task? wait;
        lock (_lock)
            wait = _resumeSignal?.Task;
        if (wait != null)
            await wait.WaitAsync(cancellationToken);
    }
}
