// using System;
// using System.Collections.Generic;
// using System.Linq;
// using System.Numerics;
// using System.Text;

// using Dalamud.Bindings.ImGui;
// using Dalamud.Game.ClientState.Keys;
// using Dalamud.Interface.Utility;
// using Dalamud.Interface.Windowing;

// namespace MasterOfPuppets;

// public class OverlayWindow : Window {
//     private Plugin Plugin { get; }

//     private Vector2 _lastSize = Vector2.Zero;
//     private Vector2 _newPosition = Vector2.Zero;

//     public OverlayWindow(Plugin plugin) : base($"{Plugin.Name} OverlayMop###OverlayWindow", ImGuiWindowFlags.AlwaysAutoResize
//           | ImGuiWindowFlags.NoTitleBar
//           | ImGuiWindowFlags.NoFocusOnAppearing
//           | ImGuiWindowFlags.NoNavFocus
//           | ImGuiWindowFlags.NoScrollbar) {
//         Plugin = plugin;

//         RespectCloseHotkey = false;
//         Namespace = "MasterOfPuppets";
//         Size = ImGuiHelpers.ScaledVector2(450, 400);
//         SizeCondition = ImGuiCond.FirstUseEver;
//         SizeConstraints = new WindowSizeConstraints {
//             MinimumSize = Vector2.Zero,
//             MaximumSize = Vector2.One * 10000,
//         };
//     }


//     private void DrawItem(IGatherable item, ILocation loc, TimeInterval time, uint quantity) {
//         if (GatherBuddy.Config.ShowGatherWindowOnlyAvailable && time.Start > GatherBuddy.Time.ServerTime)
//             return;

//         var inventoryCount = item.GetTotalCount();

//         if (quantity > 0 && inventoryCount >= quantity && GatherBuddy.Config.HideGatherWindowCompletedItems)
//             return;

//         var hasPredatorIssue = HasPredatorTimerIssue(item);

//         if (ImGui.TableNextColumn()) {
//             using var style = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, ImGui.GetStyle().ItemSpacing / 2);
//             if (Icons.DefaultStorage.TryLoadIcon(item.ItemData.Icon, out var icon))
//                 ImGuiUtil.HoverIcon(icon.Handle, icon.Size, new Vector2(ImGui.GetTextLineHeight()));
//             else
//                 ImGui.Dummy(new Vector2(ImGui.GetTextLineHeight()));
//             ImGui.SameLine();

//             var colorId = time == TimeInterval.Always ? ColorId.GatherWindowText :
//                 time.Start > GatherBuddy.Time.ServerTime ? ColorId.GatherWindowUpcoming : ColorId.GatherWindowAvailable;

//             if (quantity > 0 && inventoryCount >= quantity)
//                 colorId = ColorId.DisabledText;
//             using var color = ImRaii.PushColor(ImGuiCol.Text, colorId.Value());
//             var quantityText = quantity > 0 ? $" ({inventoryCount}/{quantity})" : "";
//             if (ImGui.Selectable($"{item.Name[GatherBuddy.Language]}{quantityText}", false)) {
//                 if (_plugin.Executor.LastItem != item)
//                     _plugin.Executor.GatherItem(item);
//                 else if (item is Gatherable)
//                     _plugin.Executor.GatherItemByName("next");
//                 else
//                     _plugin.Executor.GatherFishByName("next");
//             }

//             var clicked = ImGui.IsItemClicked(ImGuiMouseButton.Right);
//             color.Pop();
//             CreateTooltip(item, loc, time);

//             if (clicked && Dalamud.Keys[VirtualKey.MENU]) {
//                 if (quantity > 0)
//                     foreach (var list in _plugin.AutoGatherListsManager.Lists) {
//                         if (!list.Enabled)
//                             continue;

//                         var idx = list.Items.IndexOf(item);
//                         if (idx < 0)
//                             continue;

//                         _plugin.AutoGatherListsManager.ChangeEnabled(list, item, false);
//                         break;
//                     }
//             } else if (clicked && Functions.CheckModifier(GatherBuddy.Config.GatherWindowDeleteModifier, false))
//                 if (quantity > 0)
//                     foreach (var list in _plugin.AutoGatherListsManager.Lists) {
//                         if (!list.Enabled)
//                             continue;

//                         var idx = list.Items.IndexOf(item);
//                         if (idx < 0)
//                             continue;

//                         _deleteListObj = list;
//                         _deleteItemIdx = idx;
//                         _deleteAutoGather = true;
//                         break;
//                     }
//                 else
//                     for (var i = 0; i < _plugin.GatherWindowManager.Presets.Count; ++i) {
//                         var preset = _plugin.GatherWindowManager.Presets[i];
//                         if (!preset.Enabled)
//                             continue;

//                         var idx = preset.Items.IndexOf(item);
//                         if (idx < 0)
//                             continue;

//                         _deleteSet = i;
//                         _deleteItemIdx = idx;
//                         _deleteAutoGather = false;
//                         break;
//                     }
//             else
//                 Interface.CreateGatherWindowContextMenu(item, clicked);
//         }

//         DrawTime(loc, time, hasPredatorIssue);
//     }

//     public override void PreDraw() {
//         ImGui.PushStyleColor(ImGuiCol.WindowBg, ColorId.GatherWindowBackground.Value());
//         ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.One * 2 * ImGuiHelpers.GlobalScale);
//         if (GatherBuddy.Config.LockGatherWindow)
//             Flags |= ImGuiWindowFlags.NoMove;
//         else
//             Flags &= ~
//                 ImGuiWindowFlags.NoMove;

//         if (_newPosition.Y != 0) {
//             ImGui.SetNextWindowPos(_newPosition);
//             _newPosition = Vector2.Zero;
//         }
//     }

//     public override void PostDraw() {
//         DeleteItem();
//         ImGui.PopStyleVar();
//         ImGui.PopStyleColor();
//     }

//     private void CheckAnchorPosition() {
//         if (!GatherBuddy.Config.GatherWindowBottomAnchor)
//             return;

//         // Can not use Y size since a single text row is smaller than the minimal window size
//         // for some reason. 50 is arbitrary. Default window size was 32,32 for me.
//         if (_lastSize.X < 50 * ImGuiHelpers.GlobalScale)
//             _lastSize = ImGui.GetWindowSize();

//         var size = ImGui.GetWindowSize();
//         if (_lastSize == size)
//             return;

//         _newPosition = ImGui.GetWindowPos();
//         _newPosition.Y += _lastSize.Y - size.Y;
//         _lastSize = size;
//     }

//     public override void Draw() {
//         var colorId = GatherBuddy.AutoGather.Enabled ? ColorId.GatherWindowAvailable.Value() : ColorId.GatherWindowText.Value();
//         using var color = ImRaii.PushColor(ImGuiCol.Text, colorId);
//         if (ImGui.Selectable($"Auto-Gather: {GatherBuddy.AutoGather.AutoStatus}###toggle-button")) {
//             GatherBuddy.AutoGather.Enabled = !GatherBuddy.AutoGather.Enabled;
//         }
//         if (ImGui.IsItemClicked(ImGuiMouseButton.Right)) {
//             _plugin.Interface.Toggle();
//         }
//         color.Pop();
//         ImGuiUtil.HoverTooltip("Click to enable/disable auto-gather. Right click to toggle interface");
//         using var table = ImRaii.Table("##table", GatherBuddy.Config.ShowGatherWindowTimers ? 2 : 1);
//         if (!table)
//             return;

//         foreach (var (item, loc, time, quantity) in _data)
//             DrawItem(item, loc, time, quantity);

//         CheckAnchorPosition();
//     }
// }
