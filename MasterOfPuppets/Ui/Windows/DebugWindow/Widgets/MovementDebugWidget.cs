using System.Collections.Generic;
using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Utility;

using MasterOfPuppets.Resources;
using MasterOfPuppets.Util.ImGuiExt;

namespace MasterOfPuppets.Debug;

public sealed class MovementDebugWidget : Widget {
    public override string Title => "Movement";

    private float _xInput = 0;
    private float _yInput = 0;
    private float _zInput = 0;
    private int _angleInput = 180;
    private string _targetNameMoveTo = string.Empty;
    private string _targetNameMoveToRelative = string.Empty;
    private readonly List<Vector3> _path = new();
    private List<Vector3> _pathOffset = new();

    public MovementDebugWidget(WidgetContext ctx) : base(ctx) {
    }

    public override void Draw() {
        if (ImGui.Button("Stop Move")) {
            Context.Plugin.MovementManager.StopMove();
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.Text("Move to");
        ImGui.BeginGroup();
        {
            ImGui.Text("X");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(180);
            ImGui.InputFloat("X (+Left | -Right)##xInput", ref _xInput, 1, 10, "%.10f", flags: ImGuiInputTextFlags.AutoSelectAll);

            ImGui.Spacing();
            ImGui.Text("Y");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(180);
            ImGui.InputFloat("Y (+Fly Up | -Fly Down)##yInput", ref _yInput, 1, 10, "%.10f", flags: ImGuiInputTextFlags.AutoSelectAll);

            ImGui.Spacing();
            ImGui.Text("Z");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(180);
            ImGui.InputFloat("Z (+Forward | -Back)##zInput", ref _zInput, 1, 10, "%.10f", flags: ImGuiInputTextFlags.AutoSelectAll);

            ImGui.Spacing();
            if (ImGui.Button("Set Target Position to Input##SetTargetPositionToInput")) {
                if (DalamudApi.ObjectTable.LocalPlayer.TargetObject == null) return;
                var target = DalamudApi.ObjectTable.LocalPlayer.TargetObject;

                _xInput = target.Position.X;
                _yInput = target.Position.Y;
                _zInput = target.Position.Z;
            }

            ImGui.Spacing();
            ImGui.Spacing();
            if (ImGui.Button("Move By Offset")) {
                var offsetXYZ = new Vector3(_xInput, _yInput, _zInput);

                if (!_targetNameMoveToRelative.IsNullOrEmpty()) {
                    Context.Plugin.MovementManager.MoveToPositionRelative(offsetXYZ, _targetNameMoveToRelative);
                    return;
                }

                Context.Plugin.MovementManager.MoveToPosition(offsetXYZ);
            }

            ImGui.SameLine();
            if (ImGui.Button("Move To Coord")) {
                var offsetXYZ = new Vector3(_xInput, _yInput, _zInput);

                Context.Plugin.MovementManager.MoveToCoord(offsetXYZ);
            }

            ImGui.SameLine();
            if (ImGui.Button("Reset")) {
                _xInput = 0;
                _yInput = 0;
                _zInput = 0;
                _targetNameMoveToRelative = string.Empty;
            }

            ImGui.Spacing();
            ImGui.Text("If informed will be used as origin point");
            ImGui.InputTextWithHint("##MoveToCharacterNameRelativeInput", "Reference character name", ref _targetNameMoveToRelative, 255, ImGuiInputTextFlags.AutoSelectAll);
            ImGui.SameLine();
            if (ImGui.Button("Get Target Name##GetReferenceTargetName")) {
                _targetNameMoveToRelative = DalamudApi.ObjectTable.LocalPlayer.TargetObject?.Name.TextValue ?? string.Empty;
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            if (ImGui.Button($"MoveToObject (Target)")) {
                var targetObjectId = GameTargetManager.GetTargetObjectId();
                if (targetObjectId == null) return;

                Context.Plugin.MovementManager.MoveToObject(targetObjectId.Value);
            }

            if (ImGui.Button($"MoveToTargetPosition")) {
                Context.Plugin.MovementManager.MoveToTargetPosition();
            }

            ImGui.Spacing();
            ImGui.Spacing();
            ImGui.Text($"Player Position X:{DalamudApi.ObjectTable.LocalPlayer.Position.X}, Y:{DalamudApi.ObjectTable.LocalPlayer.Position.Y}, Z:{DalamudApi.ObjectTable.LocalPlayer.Position.Z}");
            ImGui.SameLine();
            if (ImGui.Button("Copy##CopyPlayerPositionToClipboard")) {
                if (DalamudApi.ObjectTable.LocalPlayer == null) return;
                var player = DalamudApi.ObjectTable.LocalPlayer;
                ImGui.SetClipboardText($"{player.Position.X}, {player.Position.Y}, {player.Position.Z}");
            }

            ImGui.Text($"Target: {GameTargetManager.GetTargetName()} ({GameTargetManager.GetTargetObjectId()})");
            ImGui.Text($"Target Position X:{DalamudApi.ObjectTable.LocalPlayer.TargetObject?.Position.X}, Y:{DalamudApi.ObjectTable.LocalPlayer.TargetObject?.Position.Y}, Z:{DalamudApi.ObjectTable.LocalPlayer.TargetObject?.Position.Z}");
            ImGui.SameLine();
            if (ImGui.Button("Copy##CopyTargetPositionToClipboard")) {
                if (DalamudApi.ObjectTable.LocalPlayer.TargetObject == null) return;
                var target = DalamudApi.ObjectTable.LocalPlayer.TargetObject;
                ImGui.SetClipboardText($"{target.Position.X}, {target.Position.Y}, {target.Position.Z}");
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            ImGui.Text("Move To Character");
            ImGui.InputTextWithHint("##MoveToCharacterNameInput", "Target name", ref _targetNameMoveTo, 255, ImGuiInputTextFlags.AutoSelectAll);

            ImGui.SameLine();
            if (ImGui.Button("Move to Character")) {
                Context.Plugin.MovementManager.MoveToObject(_targetNameMoveTo);
            }
            ImGui.SameLine();
            if (ImGui.Button("Get Target Name##GetMoveToTargetName")) {
                _targetNameMoveTo = GameTargetManager.GetTargetName();
            }
        }
        ImGui.EndGroup();

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        //     //                    +Z
        //     //                     |
        //     //                     |
        //     //                     |
        //     //       +X -----------+----------- -X
        //     //                     |
        //     //                     |
        //     //                     |
        //     //                    -Z
        //     //                    /
        //     //                   /
        //     //                  /
        //     //              +Y / (fly up)

        //     //               -Y (fly down)

        if (ImGui.Button("Enable Walk")) {
            Context.Plugin.MovementManager.SetWalking(true);
        }

        ImGui.SameLine();
        if (ImGui.Button("Disable Walk")) {
            Context.Plugin.MovementManager.SetWalking(false);
        }

        ImGui.SameLine();
        if (ImGui.Button("Toggle Walk")) {
            Context.Plugin.MovementManager.ToggleWalking();
        }

        ImGui.Text("Rotate Character");
        ImGui.InputInt("Angle##RotateCharacterInput", ref _angleInput, 1, 10, flags: ImGuiInputTextFlags.AutoSelectAll);

        ImGui.SameLine();
        if (ImGui.Button("Rotate")) {
            Context.Plugin.MovementManager.Rotate(_angleInput);
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.Text("Move Path");

        ImGui.BeginChild("##PathListGroup", ImGuiHelpers.ScaledVector2(300, 250));
        ImGui.Text("Path Points");
        if (ImGui.BeginListBox($"##PathList", ImGuiHelpers.ScaledVector2(300, 220))) {
            for (int i = 0; i < _path.Count; i++) {
                if (ImGui.Selectable($"[{i + 1:00}] {_path[i].ToString()}##PathList_{i}", false, ImGuiSelectableFlags.None)) {
                    if (ImGui.GetIO().KeyCtrl) {
                        _path.RemoveAt(i);
                    }

                }
                ImGuiUtil.ToolTip(Language.DeleteInstructionTooltip);
            }

            ImGui.EndListBox();
        }
        ImGui.EndChild();

        ImGui.SameLine();

        ImGui.BeginChild("##PathListOffsetGroup", ImGuiHelpers.ScaledVector2(300, 250));
        ImGui.Text("Path Points Offset");
        if (ImGui.BeginListBox($"##PathListOffset", ImGuiHelpers.ScaledVector2(300, 220))) {
            for (int i = 0; i < _pathOffset.Count; i++) {
                if (ImGui.Selectable($"[{i + 1:00}] {_pathOffset[i].ToString()}##PathListOffset_{i}", false, ImGuiSelectableFlags.None)) {
                    if (ImGui.GetIO().KeyCtrl) {
                        _pathOffset.RemoveAt(i);
                    }

                }
                ImGuiUtil.ToolTip(Language.DeleteInstructionTooltip);
            }

            ImGui.EndListBox();
        }
        ImGui.EndChild();


        if (ImGui.Button("Add Current Location Point")) {
            var point = DalamudApi.ObjectTable.LocalPlayer.Position;
            _path.Add(point);
            _pathOffset = CalculatePathOffsets(_path);
        }

        if (ImGui.Button("Move to Path")) {
            Context.Plugin.MovementManager.MoveByOffsetPathCommand(_pathOffset);
        }
    }

    public static List<Vector3> CalculatePathOffsets1(List<Vector3> path) {
        var offsets = new List<Vector3>();

        if (path == null || path.Count < 2)
            return offsets;

        for (int i = 1; i < path.Count; i++) {
            offsets.Add(path[i] - path[i - 1]);
        }

        return offsets;
    }

    public static List<Vector3> CalculatePathOffsets(List<Vector3> path) {
        var offsets = new List<Vector3>();

        if (path == null || path.Count < 2)
            return offsets;

        for (int i = 1; i < path.Count; i++) {
            offsets.Add(new Vector3(
                path[i].X - path[i - 1].X,
                0f,
                path[i].Z - path[i - 1].Z
            ));
        }

        return offsets;
    }
}

