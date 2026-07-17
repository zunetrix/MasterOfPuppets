using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

using MasterOfPuppets.Util.ImGuiExt;

namespace MasterOfPuppets;

public partial class GameSettingsWindow {

    private void DrawDebugTab() {
        bool isEnabled = GameSettingsManager.IsDebugEnabled;

        if (isEnabled) {
            if (ImGuiUtil.ButtonStyled("Disable Debug##GscDbgDisable",
                    ImGuiUtil.ButtonStyle.Danger)) {
                GameSettingsManager.DisableDebug();
            }
            ImGui.SameLine();
            ImGui.TextColored(Style.Colors.Green, "Debug: ENABLED");
        } else {
            if (ImGuiUtil.ButtonStyled("Enable Debug##GscDbgEnable",
                    ImGuiUtil.ButtonStyle.Success)) {
                GameSettingsManager.EnableDebug();
            }
            ImGui.SameLine();
            ImGui.TextColored(Style.Colors.Yellow, "Debug: Disabled");
        }

        ImGui.SameLine();
        using (ImRaii.PushColor(ImGuiCol.Button, Style.Components.ButtonDangerNormal)
                    .Push(ImGuiCol.ButtonHovered, Style.Components.ButtonDangerHovered)
                    .Push(ImGuiCol.ButtonActive, Style.Components.ButtonDangerActive)) {
            if (ImGui.Button("Clear Log##GscDbgClear")) {
                GameSettingsManager.ClearDebugLog();
            }
        }
        ImGuiUtil.HelpMarker("While debug is enabled, mapped game setting change is recorded below");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        var log = GameSettingsManager.DebugLog;
        float tableH = ImGui.GetContentRegionAvail().Y;

        if (!ImGui.BeginTable("##GscDbgTbl", 4,
                ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerV |
                ImGuiTableFlags.ScrollY | ImGuiTableFlags.NoSavedSettings |
                ImGuiTableFlags.SizingStretchProp,
                new Vector2(-1, tableH)))
            return;

        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableSetupColumn("#", ImGuiTableColumnFlags.WidthFixed, 34f);
        ImGui.TableSetupColumn("Source", ImGuiTableColumnFlags.WidthFixed, 100f * ImGuiHelpers.GlobalScale);
        ImGui.TableSetupColumn("Key Name", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("New Value", ImGuiTableColumnFlags.WidthFixed, 90f * ImGuiHelpers.GlobalScale);
        ImGui.TableHeadersRow();

        // Draw newest entries first
        for (int i = log.Count - 1; i >= 0; i--) {
            var entry = log[i];
            ImGui.TableNextRow();
            ImGui.TableNextColumn(); ImGui.TextDisabled($"{entry.Number}");
            ImGui.TableNextColumn(); ImGui.TextUnformatted(entry.Source);
            ImGui.TableNextColumn(); ImGui.TextUnformatted(entry.KeyName);
            ImGui.TableNextColumn(); ImGui.TextUnformatted(entry.NewValue);
        }

        if (log.Count == 0) {
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.TableNextColumn();
            ImGui.TextDisabled("No entries — enable debug and change a game setting.");
        }

        ImGui.EndTable();
    }
}
