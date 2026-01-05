using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ImGuiNotification;

using FFXIVClientStructs.FFXIV.Client.UI.Misc;

using MasterOfPuppets.Util;

namespace MasterOfPuppets.Debug;

public sealed class GeneralDebugWidget : Widget {
    public override string Title => "General";

    private static int _macroIdx = 0;

    public GeneralDebugWidget(WidgetContext ctx) : base(ctx) {
    }

    public override void Draw() {
        ImGui.Text("Macros");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.InputInt("##MacroIndexInput", ref _macroIdx);
        ImGui.SameLine();
        if (ImGui.Button("Print Macro")) {
            DalamudApi.PluginLog.Warning($"{Context.Plugin.Config.Macros[_macroIdx].JsonSerialize()}");
        }

        if (ImGui.Button("Get Object Quantity")) {
            GameSettingsManager.GetDisplayObjectLimit();
        }

        ImGui.SameLine();
        if (ImGui.Button("Set Object Quantity Minimum")) {
            GameSettingsManager.SetDisplayObjectLimit(SettingsDisplayObjectLimitType.Minimum);
        }

        if (ImGui.Button("Print Game Chat Error")) {
            DalamudApi.ChatGui.PrintError($"Test error message");
        }

        if (ImGui.Button("Chat SendChatRunMacro(2)")) {
            Chat.SendMessage($"/p moprun 2");
        }

        if (ImGui.Button("Chat SendChatRunMacro(Parasol action 1)")) {
            Chat.SendMessage($"\"Parasol action 1\"");
        }

        if (ImGui.Button("Chat SendChatStopMacroExecution")) {
            Context.Plugin.ChatWatcher.SendChatStopMacroExecution();
        }

        ImGui.Button("Resset all Config data (double click)");
        if (ImGui.IsItemHovered()) {
            if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left)) {
                Context.Plugin.Config.ResetData();
                Context.Plugin.IpcProvider.SyncConfiguration();
            }
        }

        if (ImGui.Button("Abandon Duty")) {
            GameFunctions.AbandonDuty();
        }
        // unsafe
        // {
        //     ImGui.Text($"{ActionManager.Instance()->QueuedActionId}");
        // }

        if (ImGui.Button("Use Invalid Item name")) {
            var item = ItemHelper.GetExecutableAction("Lominsan Sparkler Flare");
            DalamudApi.PluginLog.Warning($"item: {item?.ActionName}");

            GameActionManager.UseItem("Lominsan Sparkler Flare");
            DalamudApi.ShowNotification($"UseItem", NotificationType.Info, 5000);
        }

        if (ImGui.Button("Print Hotbar")) {
            PrintHotbar();
        }

        if (ImGui.Button("Reset Macros Color To White")) {
            for (var i = 0; i < Context.Plugin.Config.Macros.Count; i++) {
                Context.Plugin.Config.Macros[i].Color = new Vector4(1f, 1f, 1f, 1f);
            }

            Context.Plugin.Config.Save();
            Context.Plugin.IpcProvider.SyncConfiguration();
        }
    }

    private unsafe void PrintHotbar() {
        var hotbars = RaptureHotbarModule.Instance()->Hotbars;
        for (var hotbarIndex = 0; hotbarIndex < hotbars.Length; hotbarIndex++) {
            var hotbar = hotbars[hotbarIndex];

            for (var slotIndex = 0; slotIndex < hotbar.Slots.Length; slotIndex++) {
                var slot = hotbar.Slots[slotIndex];
                DalamudApi.PluginLog.Debug($" bar[{hotbarIndex},{slotIndex}] {slot.CommandType} - ({slot.ApparentSlotType}) - {slot.CommandId} - ({slot.ApparentActionId})");
            }
        }
    }
}

