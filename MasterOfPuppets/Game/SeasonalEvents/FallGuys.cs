using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;

namespace MasterOfPuppets;

internal class FallGuys : SeasonalEventRunner {

    private const uint RegistratorBaseId = 0xFF7A8;

    //alternative: chat type 2222 - You gain the effect of Spectator.
    private static readonly Regex MgfRegex =
        new(@"^You obtain \d+ MGF\.$", RegexOptions.IgnoreCase | RegexOptions.Compiled);


    protected override HashSet<ushort> ValidTerritories { get; } = [1165];

    private volatile bool _mgfObtained;

    protected override void OnStart() {
        _mgfObtained = false;
        DalamudApi.ChatGui.ChatMessage += OnChatMessage;
    }

    protected override void OnStop() {
        DalamudApi.ChatGui.ChatMessage -= OnChatMessage;
    }

    protected override void AddJoinSteps() {
        AddStepWaitNotBetweenAreas();
        AddStepInteractWithNpc(RegistratorBaseId);

        // Wait for FGSEnterDialog and click the enter button (skip if already in duty)
        AddStep(() =>
            DalamudApi.Condition[ConditionFlag.BoundByDuty] ||
            (GameDialogManager.IsAddonReady(GameDialogManager.AddonName.FGSEnterDialog) &&
             GameDialogManager.ClickFallGuysEnterDialog()));

        // Wait for ContentsFinderConfirm and click Commence (skip if already in duty)
        AddStep(() =>
            DalamudApi.Condition[ConditionFlag.BoundByDuty] ||
            (GameDialogManager.IsAddonReady(GameDialogManager.AddonName.ContentsFinderConfirm) &&
             GameDialogManager.ClickContentsFinderConfirm()));

        // Wait until fully inside the duty
        AddStep(() => DalamudApi.Condition[ConditionFlag.BoundByDuty]);
    }

    protected override void AddLeaveSteps() {
        // Fire the leave command
        AddStep(() => {
            if (!DalamudApi.Condition[ConditionFlag.BoundByDuty]) return true;
            DalamudApi.PluginLog.Debug("[FallGuys] Abandoning duty...");
            GameFunctions.AbandonDuty();
            return true;
        });

        // If a leave cutscene/confirm appears, fire again
        AddStep(() => {
            if (!DalamudApi.Condition[ConditionFlag.BoundByDuty]) return true;
            if (!DalamudApi.Condition[ConditionFlag.OccupiedInCutSceneEvent]) return false;
            DalamudApi.PluginLog.Debug("[FallGuys] Confirming duty leave...");
            GameFunctions.AbandonDuty();
            return true;
        });

        AddStepWaitNotBetweenAreas();
    }

    protected override async Task RunActivityLoop(CancellationToken ct) {
        DalamudApi.PluginLog.Debug("[FallGuys] Waiting for MGF reward...");
        try {
            while (!ct.IsCancellationRequested && !_mgfObtained)
                await Task.Delay(3000, ct);

            if (_mgfObtained)
                DalamudApi.PluginLog.Debug("[FallGuys] MGF obtained - proceeding to leave duty.");
        } catch (OperationCanceledException) {
            // normal stop
        }
    }

    private void OnChatMessage(XivChatType type, int timestamp, ref SeString sender, ref SeString message, ref bool isHandled) {

        uint mgfChatType = 2110;

        if ((uint)type != mgfChatType) {
            return;
        }
        if (MgfRegex.IsMatch(message.TextValue))
            _mgfObtained = true;
    }
}
