using System;

using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;

using MasterOfPuppets.Resources;

namespace MasterOfPuppets;

internal class MainWindow : Window
{
    private PluginUi Ui { get; }
    private Plugin Plugin { get; }

    public bool IsVisible { get; private set; }

    private static void HelpMarker(string text)
    {
        ImGui.TextDisabled("(?)");
        if (!ImGui.IsItemHovered())
        {
            return;
        }

        ImGui.BeginTooltip();
        ImGui.PushTextWrapPos(ImGui.GetFontSize() * 20f);
        ImGui.TextUnformatted(text);
        ImGui.PopTextWrapPos();
        ImGui.EndTooltip();
    }

    internal MainWindow(Plugin plugin, PluginUi ui) : base(Plugin.Name)
    {
        Ui = ui;
        Plugin = plugin;

        Size = new Vector2(290, 195);
        SizeCondition = ImGuiCond.FirstUseEver;
        UpdateConfig();
    }

    public override void Update()
    {
        IsVisible = false;
        base.Update();
    }

    public override void PreDraw()
    {
        Flags = ImGuiWindowFlags.None;
        if (!Plugin.Config.AllowMovement)
        {
            Flags |= ImGuiWindowFlags.NoMove;
        }

        if (!Plugin.Config.AllowResize)
        {
            Flags |= ImGuiWindowFlags.NoResize;
        }

        base.PreDraw();
    }

    public override void Draw()
    {
        IsVisible = true;

        // prevent change macro index while editing
        ImGui.BeginDisabled(Ui.MacroEditorWindow.IsOpen);
        DrawMacrosTable();
        ImGui.EndDisabled();
    }

    public override bool DrawConditions()
    {
        // var inCombat = DalamudApi.Condition[ConditionFlag.InCombat];
        // var inInstance = DalamudApi.Condition[ConditionFlag.BoundByDuty]
        //                  || DalamudApi.Condition[ConditionFlag.BoundByDuty56]
        //                  || DalamudApi.Condition[ConditionFlag.BoundByDuty95];
        // var inCutscene = DalamudApi.Condition[ConditionFlag.WatchingCutscene]
        //                  || DalamudApi.Condition[ConditionFlag.WatchingCutscene78]
        //                  || DalamudApi.Condition[ConditionFlag.OccupiedInCutSceneEvent];

        // if (inCombat && !Plugin.Config.ShowInCombat) return false;
        // if (inInstance && !Plugin.Config.ShowInInstance) return false;
        // if (inCutscene && !Plugin.Config.ShowInCutscenes) return false;

        return true;
    }

    internal void DrawMacrosTable()
    {
        ImGui.TextUnformatted(Language.MacroListTitle);
        ImGuiUtil.HelpMarker("""
        Commands:
        /mop run number

        Special Actions:
        /wait time
        /wait 3

        Drag to reorder macro list
        """);

        ImGui.SameLine(ImGuiUtil.GetWindowContentRegionWidth() - ImGui.GetFrameHeightWithSpacing());

        if (ImGuiUtil.IconButton(FontAwesomeIcon.Plus, $"##AddMacroBtn", Language.AddMacroBtn))
        {
            Ui.MacroEditorWindow.AddNewMacro();
        }

        ImGui.Separator();
        ImGui.Spacing();

        var tableFlags = ImGuiTableFlags.RowBg | ImGuiTableFlags.PadOuterX |
                ImGuiTableFlags.NoSavedSettings | ImGuiTableFlags.BordersInnerV;
        var tableColumnCount = 3;
        if (ImGui.BeginTable("##MacrosTable", tableColumnCount, tableFlags))
        {
            ImGui.TableSetupColumn("#", ImGuiTableColumnFlags.WidthFixed);
            ImGui.TableSetupColumn("Macro", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Options", ImGuiTableColumnFlags.WidthFixed);
            var macros = Plugin.Config.Macros;

            for (int i = 0; i < macros.Count; i++)
            {
                ImGui.PushID(i);
                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0);
                ImGui.TextUnformatted($"{i + 1:000}");

                ImGui.TableNextColumn();
                ImGui.Selectable($"{macros[i].Name}");

                if (ImGui.BeginDragDropSource())
                {
                    unsafe
                    {
                        ImGui.SetDragDropPayload("DND_MACROS_TABLE", new ReadOnlySpan<byte>(&i, sizeof(int)), ImGuiCond.None);
                        ImGui.Button($"({i + 1}) {macros[i].Name}");
                    }

                    // PluginLog.Warning($"Drag start [{i}]");
                    ImGui.EndDragDropSource();
                }

                ImGui.PushStyleColor(ImGuiCol.DragDropTarget, Style.Components.DragDropTarget);
                if (ImGui.BeginDragDropTarget())
                {
                    ImGuiPayloadPtr dragDropPayload = ImGui.AcceptDragDropPayload("DND_MACROS_TABLE");

                    bool isDropping = false;
                    unsafe
                    {
                        isDropping = !dragDropPayload.IsNull;
                    }

                    if (isDropping && dragDropPayload.IsDelivery())
                    {
                        unsafe
                        {
                            int originalIndex = *(int*)dragDropPayload.Data;

                            int offset = i - originalIndex;
                            if (offset != 0 && originalIndex + offset >= 0)
                            {
                                int targetIndex = originalIndex + offset;
                                // PluginLog.Warning($"Drag end [{i}]: [{originalIndex}, {targetIndex}] {offset}");
                                Plugin.Config.MoveMacroToIndex(originalIndex, targetIndex);
                                Plugin.Config.Save();
                                Plugin.IpcProvider.SyncConfiguration();
                            }
                        }
                    }
                    ImGui.EndDragDropTarget();
                }
                ImGui.PopStyleColor();

                ImGui.TableNextColumn();
                ImGuiUtil.IconButton(FontAwesomeIcon.TrashAlt, $" X ##DeleteMacro_{i}", Language.DeleteMacroBtn);
                if (ImGui.IsItemHovered())
                {
                    if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                    {
                        Plugin.Config.RemoveMacroItem(i);
                        Plugin.Config.Save();
                        Plugin.IpcProvider.SyncConfiguration();
                    }
                }

                ImGui.SameLine();
                if (ImGuiUtil.IconButton(FontAwesomeIcon.Copy, $" X ##DuplicateMacro_{i}", Language.DuplicateMacroBtn))
                {
                    Plugin.Config.DuplicateMacroItem(i);
                }

                ImGui.SameLine();
                if (ImGuiUtil.IconButton(FontAwesomeIcon.Edit, $" X ##EditMacro_{i}", Language.EditMacroBtn))
                {
                    Ui.MacroEditorWindow.EditMacro(i);
                }

                ImGui.SameLine();

                if (ImGuiUtil.IconButton(FontAwesomeIcon.Play, $" X ##RunMacro_{i}", Language.RunMacroBtn))
                {
                    Plugin.IpcProvider.RunMacro(i);
                }
                ImGui.PopID();
            }
            ImGui.EndTable();
            ImGui.Unindent();
        }
    }

    internal void UpdateConfig()
    {
        RespectCloseHotkey = Plugin.Config.AllowCloseWithEscape;

        TitleBarButtons.Clear();
        if (Plugin.Config.ShowSettingsButton)
        {
            TitleBarButtons.Add(new TitleBarButton()
            {
                AvailableClickthrough = false,
                Click = _ => Ui.SettingsWindow.Toggle(),
                Icon = FontAwesomeIcon.Cog
            });

            TitleBarButtons.Add(new TitleBarButton()
            {
                AvailableClickthrough = false,
                // Click = _ => Ui.SettingsWindow.Toggle(),
                Icon = FontAwesomeIcon.Heart
            });
        }
    }
}
