using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;

using MasterOfPuppets.Extensions.Dalamud;
using MasterOfPuppets.Util;

namespace MasterOfPuppets;

internal abstract class SeasonalEventRunner : IDisposable {
    private readonly List<Func<bool>> _steps = [];
    private CancellationTokenSource? _cts;
    private bool _running;

    public bool IsRunning => _running;

    protected Plugin Plugin { get; private set; } = null!;

    protected abstract HashSet<ushort> ValidTerritories { get; }

    /// <summary>Adds steps executed once before the activity loop (travel, interaction, etc.).</summary>
    protected abstract void AddJoinSteps();

    /// <summary>Main repeating activity loop. Should run until complete or <paramref name="ct"/> is cancelled.</summary>
    protected abstract Task RunActivityLoop(CancellationToken ct);

    /// <summary>Called after <see cref="RunActivityLoop"/> finishes. Add any post-activity steps here.</summary>
    protected virtual void AddLeaveSteps() { }

    /// <summary>Called at the start of <see cref="Start"/> before any steps are queued.</summary>
    protected virtual void OnStart() { }

    /// <summary>Called when <see cref="Stop"/> is invoked.</summary>
    protected virtual void OnStop() { }

    public void Start(Plugin plugin) {
        Stop();
        _running = true;
        Plugin = plugin;
        _cts = new CancellationTokenSource();
        OnStart();
        AddJoinSteps();
        // Task.Run ensures RunLoopThenLeave runs off the framework thread so that
        // RunOnFrameworkThread inside RunActivityLoop is truly async and doesn't
        // complete synchronously inside stopWhen (which would cause RunNextStep
        // to be called before the callback removes the current step).
        _steps.Add(() => { _ = Task.Run(() => RunLoopThenLeave(_cts.Token)); return true; });
        RunNextStep();
    }

    public void Stop() {
        _running = false;
        _cts?.Cancel();
        _steps.Clear();
        OnStop();
    }

    public void Dispose() => Stop();

    private async Task RunLoopThenLeave(CancellationToken ct) {
        await RunActivityLoop(ct);
        if (!ct.IsCancellationRequested) {
            // Dispatch back to framework thread so _steps is only touched from there.
            await DalamudApi.Framework.RunOnFrameworkThread(() => {
                AddLeaveSteps();
                RunNextStep();
            });
        }
    }

    private void RunNextStep() {
        if (_steps.Count == 0) {
            _running = false; // all steps completed — runner is idle
            return;
        }
        if (_cts?.IsCancellationRequested == true) return;
        var step = _steps[0];
        Coroutine.StartRunOnFramework(
            runFunction: () => { },
            stopWhen: step,
            callback: () => {
                // Guard: Stop() may have cleared the list before this callback fired.
                if (_cts?.IsCancellationRequested == true) return;
                if (_steps.Count > 0) _steps.RemoveAt(0);
                RunNextStep();
            },
            cancellationToken: _cts!.Token);
    }

    protected void AddStep(Func<bool> step) => _steps.Add(step);

    protected void AddStepWaitNotBetweenAreas() =>
        AddStep(() => !DalamudApi.Condition[ConditionFlag.BetweenAreas]);

    protected void AddStepWaitInValidTerritory() =>
        AddStep(() => ValidTerritories.Contains(DalamudApi.ClientState.TerritoryType));

    protected void AddStepInteractWithNpc(uint npcBaseId) => AddStep(() => {
        if (DalamudApi.Condition[ConditionFlag.BetweenAreas]) return false;
        var npc = DalamudApi.ObjectTable.FirstOrDefault(
            o => o.ObjectKind == ObjectKind.EventNpc && o.IsTargetable && o.BaseId == npcBaseId);
        if (npc == null) return false;
        npc.Interact();
        return true;
    });

    protected void AddStepSelectString(int index = 0) => AddStep(() =>
        GameDialogManager.IsAddonVisible(GameDialogManager.AddonName.SelectString) &&
        GameDialogManager.SelectStringAtIndex(index));
}
