using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ImGuiNotification;

namespace MasterOfPuppets.Debug;

public sealed class ActionsDebugWidget : Widget {
    public override string Title => "Actions";

    public ActionsDebugWidget(WidgetContext ctx) : base(ctx) {
    }

    public override void Draw() {
        if (ImGui.Button("ExecuteHotbarAction (sit anywhere 96)")) {
            HotbarManager.ExecuteHotbarEmoteAction(96);
        }

        if (ImGui.Button("ExecuteHotbarAction (sleep anywhere pose 99)")) {
            HotbarManager.ExecuteHotbarEmoteAction(99);
        }
        if (ImGui.Button("ExecuteHotbarAction (sleep / wake anywhere)")) {
            HotbarManager.ExecuteHotbarEmoteAction(88);
        }

        if (ImGui.Button("ExecuteActionCommand Umbrella Dance")) {
            Context.Plugin.IpcProvider.ExecuteActionCommand(30868);
            DalamudApi.ShowNotification($"ExecuteActionCommand", NotificationType.Info, 5000);
        }

        if (ImGui.Button("ExecuteHotbarActionBySlotIndex(1, 5)")) {
            HotbarManager.ExecuteHotbarActionByIndex(1, 5);
            DalamudApi.ShowNotification($"ExecuteHotbarActionBySlotIndex", NotificationType.Info, 5000);
        }

        if (ImGui.Button("ExecutePetHotbarActionBySlotIndex(0)")) {
            HotbarManager.ExecutePetHotbarActionByIndex(0);
            DalamudApi.ShowNotification($"ExecutePetHotbarActionBySlotIndex", NotificationType.Info, 5000);
        }

        if (ImGui.Button("ExecutePetHotbarActionBySlotIndex(1)")) {
            HotbarManager.ExecutePetHotbarActionByIndex(1);
            DalamudApi.ShowNotification($"ExecuteHotbarActionBySlotIndex", NotificationType.Info, 5000);
        }

        if (ImGui.Button("UseItem(Heavenscracker)")) {
            GameActionManager.UseItem("Heavenscracker");
            DalamudApi.ShowNotification($"UseItem", NotificationType.Info, 5000);
        }

        if (ImGui.Button("UseAction(\"Peloton\")")) {
            GameActionManager.UseAction("Peloton");
            DalamudApi.ShowNotification($"UseAction", NotificationType.Info, 5000);
        }

        if (ImGui.Button("UseGeneralAction(23) unmount")) {
            GameActionManager.UseGeneralAction(23);
            DalamudApi.ShowNotification($"UseGeneralAction", NotificationType.Info, 5000);
        }

        if (ImGui.Button("Broadcast ExecuteItemCommand")) {
            Context.Plugin.IpcProvider.ExecuteItemCommand(5893);
            DalamudApi.ShowNotification($"ExecuteItemCommand", NotificationType.Info, 5000);
        }

        if (ImGui.Button("UseItem(Lominsan Sparkler)")) {
            GameActionManager.UseItem("Lominsan Sparkler");
            DalamudApi.ShowNotification($"UseItem", NotificationType.Info, 5000);
        }

        if (ImGui.Button("UseItem(5893)")) {
            uint lominsanSparklere = 5893;
            GameActionManager.UseItem(lominsanSparklere);

            DalamudApi.ShowNotification($"UseItem", NotificationType.Info, 5000);
        }
        if (ImGui.Button("Broadcast UseItem(5893)")) {
            uint lominsanSparklere = 5893;
            Context.Plugin.IpcProvider.ExecuteItemCommand(lominsanSparklere);
            DalamudApi.ShowNotification($"Broadcast UseItem(5893)", NotificationType.Info, 5000);
        }

    }
}
