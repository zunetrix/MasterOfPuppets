using System;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;

using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Bindings.ImGui;

using MasterOfPuppets.Resources;

namespace MasterOfPuppets;

internal class MainWindow : Window
{
    private PluginUi Ui { get; }
    private Plugin Plugin { get; }
    public bool IsVisible { get; private set; }
    // private static readonly Version Version = typeof(MainWindow).Assembly.GetName().Version;
    // private static readonly string VersionString = Version?.ToString();
    private string MacroSearchString = "";
    private readonly List<int> MacroListSearchedIndexs = new();

    internal MainWindow(Plugin plugin, PluginUi ui) : base(Plugin.Name)
    {
        Ui = ui;
        Plugin = plugin;

        Size = new Vector2(290, 195);
        SizeCondition = ImGuiCond.FirstUseEver;
        UpdateWindowConfig();
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
        DrawMacroHeader();
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

    private void DrawMacroHeader()
    {
        ImGui.TextUnformatted(Language.MacroListTitle);

        float spacing = ImGui.GetStyle().ItemSpacing.X;
        float buttonWidth = ImGui.GetFrameHeight();
        int buttonCount = 4;
        float marginRight = 10f;
        float totalButtonsWidth = (buttonWidth * buttonCount) + (spacing * (buttonCount - 1)) + marginRight;

        ImGui.SameLine(ImGui.GetWindowContentRegionMax().X - totalButtonsWidth);
        if (ImGuiUtil.IconButton(FontAwesomeIcon.Smile, $"##ShowEmotesBtn", Language.ShowEmotesBtn))
        {
            Ui.EmotesWindow.Toggle();
        }

        ImGui.SameLine();
        if (ImGuiUtil.IconButton(FontAwesomeIcon.Umbrella, $"##ShowFashionAccessoriesBtn", Language.ShowFashionAccessoriesBtn))
        {
            Ui.FashionAccessoriesWindow.Toggle();
        }
        ImGui.SameLine();
        if (ImGuiUtil.IconButton(FontAwesomeIcon.Glasses, $"##ShowFacewearBtn", Language.ShowFacewearBtn))
        {
            Ui.FacewearWindow.Toggle();
        }

        ImGui.SameLine();
        if (ImGuiUtil.IconButton(FontAwesomeIcon.Horse, $"##ShowMountBtn", Language.ShowMountBtn))
        {
            Ui.MountWindow.Toggle();
        }

        // ImGui.Text(Language.MacroSearchInputLabel);
        if (ImGui.InputTextWithHint("##MacroSearchInput", Language.MacroSearchInputLabel, ref MacroSearchString, 255, ImGuiInputTextFlags.AutoSelectAll))
        {
            SearchMacro();
        }
        ImGuiUtil.HelpMarker("""
        Commands:
            /mop run number
            /mop run name
            /mop run "name with spaces"

        Drag to reorder macro list
        """);

        int buttonMacroCount = 4;
        float totalButtonsMacroWidth = (buttonWidth * buttonMacroCount) + (spacing * (buttonMacroCount - 1)) + marginRight;

        ImGui.SameLine(ImGui.GetWindowContentRegionMax().X - totalButtonsMacroWidth);
        if (ImGuiUtil.IconButton(FontAwesomeIcon.Plus, $"##AddMacroBtn", Language.AddMacroBtn))
        {
            Ui.MacroEditorWindow.AddNewMacro();
        }

        ImGui.SameLine();
        if (ImGuiUtil.IconButton(FontAwesomeIcon.Stop, $"##StopMacroExecutionBtn", Language.StopMacroExecutionBtn))
        {
            Plugin.IpcProvider.StopMacroExecution();
            DalamudApi.ShowNotification($"Macro execution queue stoped", NotificationType.Info, 3000);
        }

        ImGui.SameLine();
        if (ImGuiUtil.IconButton(FontAwesomeIcon.Users, $"##ShowCharactersBtn", Language.ShowCharactersBtn))
        {
            Ui.CharactersWindow.Toggle();
        }

        ImGui.SameLine();
        if (ImGuiUtil.IconButton(FontAwesomeIcon.List, $"##ShowMacroExecutionQueueBtn", Language.ShowMacroExecutionQueueBtn))
        {
            Ui.MacroExecutionQueueWindow.Toggle();
        }

        var isFiltered = !string.IsNullOrEmpty(MacroSearchString);
        var noSearchResults = MacroListSearchedIndexs.Count == 0;
        if (isFiltered && noSearchResults)
        {
            ImGuiUtil.DrawColoredBanner(Style.Colors.Red, "Nothing found");
        }

        ImGui.Spacing();
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        ImGui.Spacing();
    }

    private void SearchMacro()
    {
        MacroListSearchedIndexs.Clear();

        MacroListSearchedIndexs.AddRange(
            Plugin.Config.Macros
            .Select((item, index) => new { item, index })
            .Where(x => x.item.Name.Contains(MacroSearchString, StringComparison.OrdinalIgnoreCase))
            .Select(x => x.index)
            .ToList()
        );
    }

    private void DrawMacroEntry(int macroIdx)
    {
        var macro = Plugin.Config.Macros[macroIdx];
        ImGui.PushID(macroIdx);
        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        ImGui.TextUnformatted($"{macroIdx + 1:000}");

        ImGui.TableNextColumn();
        ImGui.Selectable($"{macro.Name}");

        if (ImGui.BeginDragDropSource())
        {
            unsafe
            {
                ImGui.SetDragDropPayload("DND_MACROS_TABLE", new ReadOnlySpan<byte>(&macroIdx, sizeof(int)), ImGuiCond.None);
                ImGui.Button($"({macroIdx + 1}) {macro.Name}");
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

                    int offset = macroIdx - originalIndex;
                    if (offset != 0 && originalIndex + offset >= 0)
                    {
                        int targetIndex = originalIndex + offset;
                        // PluginLog.Warning($"Drag end [{i}]: [{originalIndex}, {targetIndex}] {offset}");
                        Plugin.Config.MoveMacroToIndex(originalIndex, targetIndex);
                        Plugin.IpcProvider.SyncConfiguration();
                    }
                }
            }
            ImGui.EndDragDropTarget();
        }
        ImGui.PopStyleColor();

        ImGui.TableNextColumn();
        ImGuiUtil.IconButton(FontAwesomeIcon.Trash, $"##DeleteMacro_{macroIdx}", Language.DeleteMacroBtn);
        if (ImGui.IsItemHovered())
        {
            if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
            {
                Plugin.Config.RemoveMacroItem(macroIdx);
                Plugin.IpcProvider.SyncConfiguration();
            }
        }

        ImGui.SameLine();
        if (ImGuiUtil.IconButton(FontAwesomeIcon.Copy, $"##CloneMacro_{macroIdx}", Language.CloneMacroBtn))
        {
            Plugin.Config.CloneMacroItem(macroIdx);
        }

        ImGui.SameLine();
        if (ImGuiUtil.IconButton(FontAwesomeIcon.Edit, $"##EditMacro_{macroIdx}", Language.EditMacroBtn))
        {
            Ui.MacroEditorWindow.EditMacro(macroIdx);
        }

        ImGui.SameLine();

        if (ImGuiUtil.IconButton(FontAwesomeIcon.Play, $"##RunMacro_{macroIdx}", Language.RunMacroBtn))
        {
            Plugin.IpcProvider.RunMacro(macroIdx);
        }
        ImGui.PopID();
    }

    private void DrawMacrosTable()
    {
        var tableFlags = ImGuiTableFlags.RowBg | ImGuiTableFlags.PadOuterX |
                ImGuiTableFlags.NoSavedSettings | ImGuiTableFlags.BordersInnerV;
        var tableColumnCount = 3;
        var macros = Plugin.Config.Macros;
        var isFiltered = !string.IsNullOrEmpty(MacroSearchString);
        var itemCount = isFiltered ? MacroListSearchedIndexs.Count : macros.Count;

        if (ImGui.BeginTable("##MacrosTable", tableColumnCount, tableFlags))
        {
            ImGui.TableSetupColumn("#", ImGuiTableColumnFlags.WidthFixed);
            ImGui.TableSetupColumn("Macro", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Options", ImGuiTableColumnFlags.WidthFixed);



            ImGuiListClipperPtr clipper;
            unsafe
            {
                clipper = new ImGuiListClipperPtr(ImGuiNative.ImGuiListClipper());
            }

            clipper.Begin(itemCount);

            while (clipper.Step())
            {
                for (int i = clipper.DisplayStart; i < clipper.DisplayEnd; i++)
                {
                    if (i >= itemCount) break;
                    int realIndex = isFiltered ? MacroListSearchedIndexs[i] : i;
                    if (realIndex >= macros.Count) continue;

                    DrawMacroEntry(realIndex);
                }
            }

            // for (int i = 0; i < macros.Count; i++)
            // {
            //     DrawMacroEntry(i, macros[i]);
            // }
            clipper.End();
            ImGui.EndTable();
        }
    }

    internal void UpdateWindowConfig()
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

#if DEBUG
            TitleBarButtons.Add(new TitleBarButton()
            {
                AvailableClickthrough = false,
                Click = _ => Ui.DebugWindow.Toggle(),
                Icon = FontAwesomeIcon.Bug
            });
#endif
        }
    }
}
