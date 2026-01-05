using Dalamud.Bindings.ImGui;

namespace MasterOfPuppets.Debug;

public sealed class TargetDebugWidget : Widget {
    public override string Title => "Target";

    private static string _targetName = string.Empty;

    public TargetDebugWidget(WidgetContext ctx) : base(ctx) {
    }

    public override void Draw() {
        ImGui.InputTextWithHint("##TargetNameDebugInput", "Target name", ref _targetName, 255, ImGuiInputTextFlags.AutoSelectAll);
        ImGui.SameLine();
        if (ImGui.Button("Target")) {
            GameTargetManager.TargetObject(_targetName);
        }

        ImGui.SameLine();
        if (ImGui.Button("Target Clear")) {
            GameTargetManager.TargetClear();
        }

        if (ImGui.Button("Target Clear Broadcast")) {
            Context.Plugin.IpcProvider.ExecuteTargetClear();
        }

        if (ImGui.Button("Target My Target")) {
            Context.Plugin.IpcProvider.ExecuteTargetMyTarget();
        }

        if (ImGui.Button("Target My Minion")) {
            GameTargetManager.TargetMyMinion();
        }
    }
}

