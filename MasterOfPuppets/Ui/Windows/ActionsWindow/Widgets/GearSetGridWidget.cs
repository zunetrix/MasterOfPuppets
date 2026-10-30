using System;
using System.Collections.Generic;
using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Utility;

using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;

using Lumina.Excel.Sheets;
using Lumina.Text.ReadOnly;

using MasterOfPuppets.Extensions.Dalamud;
using MasterOfPuppets.Util;
using MasterOfPuppets.Util.ImGuiExt;

using GearsetEntry = FFXIVClientStructs.FFXIV.Client.UI.Misc.RaptureGearsetModule.GearsetEntry;
using GearsetFlag = FFXIVClientStructs.FFXIV.Client.UI.Misc.RaptureGearsetModule.GearsetFlag;
using GearsetItem = FFXIVClientStructs.FFXIV.Client.UI.Misc.RaptureGearsetModule.GearsetItem;
using GearsetItemIndex = FFXIVClientStructs.FFXIV.Client.UI.Misc.RaptureGearsetModule.GearsetItemIndex;

namespace MasterOfPuppets.Actions;

// https://github.com/Haselnussbomber/HaselTweaks/blob/main/HaselTweaks/Windows/GearSetGridWindow.cs
// https://github.com/Critical-Impact/InventoryTools
public unsafe partial class GearSetGridWidget : Widget {
    public override string Title => "Gear Set Grid";
    public override FontAwesomeIcon Icon => FontAwesomeIcon.Briefcase;

    // Layout constants
    private static readonly Vector2 IconSize = new(34);
    private static readonly Vector2 IconInset = IconSize * 0.08333f;

    // Dye dot size rendered on the bottom-right corner of an item icon
    private const float DyeDotRadius = 4f;

    // Scroll / reset
    private bool _resetScrollPosition;

    // Search / filter
    private string _searchText = string.Empty;
    private bool _searchByGearset = true;
    private bool _searchByItem = true;
    private bool _searchByDye = true;

    // Try-On on hover
    private bool _tryOnOnHover = false;
    // When true, each new Try-On keeps the previously previewed items visible
    // (sets AgentTryon.SaveDeleteOutfit = true before the TryOn call).
    private bool _tryOnKeepOutfit = true;
    private uint _lastTryOnItemId = 0;
    private int _lastTryOnGsIdx = -1;

    // Deferred action (avoids modifying game state inside table draw)
    private System.Action? _pendingAction = null;

    //  Move mode configuration
    /// <summary>How to resolve the target slot when moving a gearset item → Armoury.</summary>
    private enum ArmouryMoveMode {
        /// <summary>Move to the first empty armoury slot for this item type.</summary>
        FirstEmpty = 0,
        /// <summary>Move to the armoury slot matching the gearset order index (GS 1 → slot 0).</summary>
        MatchGearsetIndex = 1,
        /// <summary>Swap with the slot occupied by a second chosen gearset in the armoury.</summary>
        SwapWithGearset = 2,
    }

    /// <summary>How to resolve the destination when moving an armoury item → Inventory.</summary>
    private enum InventoryMoveMode {
        /// <summary>Move to the first empty inventory bag slot.</summary>
        FirstEmpty = 0,
        /// <summary>Swap with the inventory copy of this item if one exists; otherwise first empty.</summary>
        SwapWithArmoury = 1,
    }

    private ArmouryMoveMode _armouryMoveMode = ArmouryMoveMode.FirstEmpty;
    private InventoryMoveMode _inventoryMoveMode = InventoryMoveMode.FirstEmpty;
    // Index (0-based) of the "other" gearset used in SwapWithGearset mode.
    private int _swapTargetGearsetIdx = 0;
    private bool _showMoveSettings = false;

    // Filter slots when moving gearset items
    private bool _moveIgnoreWeapons = true;
    private bool _moveIgnoreAccessories = true;
    private bool _moveIgnoreSoulCrystal = true;

    public GearSetGridWidget(WidgetContext ctx) : base(ctx) { }

    public override void OnShow() {
        _resetScrollPosition = true;
        base.OnShow();
    }

    //  Main Draw

    public override void Draw() {
        DrawHeader();
        DrawTable();

        // Execute deferred actions after all ImGui draw calls so we don't
        // corrupt the table / popup stack.
        _pendingAction?.Invoke();
        _pendingAction = null;
    }

    //  Header

    private void DrawHeader() {
        ImGui.SetNextItemWidth(220 * ImGuiHelpers.GlobalScale);
        ImGui.InputTextWithHint("##GearGridSearch", "Search...", ref _searchText, 128, ImGuiInputTextFlags.AutoSelectAll);

        // Try-On on hover toggle
        ImGui.SameLine();
        if (ImGui.Checkbox("Try On Hover", ref _tryOnOnHover)) {
            _lastTryOnItemId = 0;
            _lastTryOnGsIdx = -1;
        }
        ImGuiUtil.ToolTip("When enabled, hovering over an item icon will preview it (Try On)");

        // Keep outfit: controls AgentTryon.SaveDeleteOutfit so each new
        // Try-On doesn't replace the previous previewed pieces.
        if (_tryOnOnHover) {
            ImGui.SameLine();
            ImGui.Checkbox("Keep Outfit", ref _tryOnKeepOutfit);
            ImGuiUtil.ToolTip("When enabled, trying on a new item keeps previously previewed items visible");
        }

        ImGui.SameLine();
        ImGui.Spacing();
        ImGui.SameLine();

        // Move settings toggle button
        using (ImRaii.PushColor(ImGuiCol.Button,
            _showMoveSettings ? new Vector4(0.3f, 0.5f, 0.8f, 1f) : default,
            _showMoveSettings)) {
            if (ImGuiUtil.IconButton(FontAwesomeIcon.Cog, "##MoveSettingsBtn", "Move Settings")) {
                _showMoveSettings = !_showMoveSettings;
            }
        }

        ImGui.TextDisabled("in:");

        ImGui.SameLine();
        ImGui.Checkbox("Gearset", ref _searchByGearset);
        ImGui.SameLine();
        ImGui.Checkbox("Item", ref _searchByItem);
        ImGui.SameLine();
        ImGui.Checkbox("Dye", ref _searchByDye);

        ImGui.SameLine();
        ImGui.Spacing();

        ImGui.Separator();

        if (_showMoveSettings)
            DrawMoveSettingsPanel();
    }

    //  Move Settings Panel

    private void DrawMoveSettingsPanel() {
        using var bg = ImRaii.PushColor(ImGuiCol.ChildBg, new Vector4(0.10f, 0.10f, 0.16f, 1f));
        using var child = ImRaii.Child("##MoveSettings", new Vector2(-1, 104 * ImGuiHelpers.GlobalScale), true);
        if (!child) return;

        // Inventory → Armoury
        ImGui.TextColored(new Vector4(0.7f, 0.85f, 1f, 1f), "Inventory → Armoury:");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(180 * ImGuiHelpers.GlobalScale);
        var armLabel = _armouryMoveMode switch {
            ArmouryMoveMode.FirstEmpty => "First Empty Slot",
            ArmouryMoveMode.MatchGearsetIndex => "Match Gearset Index",
            ArmouryMoveMode.SwapWithGearset => "Swap with Gearset...",
            _ => "?"
        };
        using (var combo = ImRaii.Combo("##ArmouryMode", armLabel)) {
            if (combo) {
                if (ImGui.Selectable("First Empty Slot", _armouryMoveMode == ArmouryMoveMode.FirstEmpty))
                    _armouryMoveMode = ArmouryMoveMode.FirstEmpty;
                ImGuiUtil.ToolTip("Moves the item to the first empty slot in the correct armoury section.");

                if (ImGui.Selectable("Match Gearset Index", _armouryMoveMode == ArmouryMoveMode.MatchGearsetIndex))
                    _armouryMoveMode = ArmouryMoveMode.MatchGearsetIndex;
                ImGuiUtil.ToolTip("Moves to armoury slot N where N = gearset number - 1.\nKeeps each gearset in its own numbered armoury slot.");

                if (ImGui.Selectable("Swap with Gearset...", _armouryMoveMode == ArmouryMoveMode.SwapWithGearset))
                    _armouryMoveMode = ArmouryMoveMode.SwapWithGearset;
                ImGuiUtil.ToolTip("Moves to the armoury slot used by a second chosen gearset,\npushing that gearset's item to the inventory.");
            }
        }

        // Sub-picker for swap target gearset
        if (_armouryMoveMode == ArmouryMoveMode.SwapWithGearset) {
            ImGui.SameLine();
            ImGui.TextDisabled("↔");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(170 * ImGuiHelpers.GlobalScale);
            var rapture = RaptureGearsetModule.Instance();
            var gearsetCount = InventoryManager.Instance()->GetPermittedGearsetCount();
            var swapGs = rapture->IsValidGearset(_swapTargetGearsetIdx)
                ? rapture->GetGearset(_swapTargetGearsetIdx) : null;
            var swapLabel = swapGs != null
                ? $"[{_swapTargetGearsetIdx + 1}] {swapGs->NameString}" : "(pick)";
            using (var combo2 = ImRaii.Combo("##SwapGs", swapLabel)) {
                if (combo2) {
                    for (int i = 0; i < gearsetCount; i++) {
                        var gs = rapture->GetGearset(i);
                        if (!gs->Flags.HasFlag(GearsetFlag.Exists)) continue;
                        if (ImGui.Selectable($"[{i + 1}] {gs->NameString}", _swapTargetGearsetIdx == i))
                            _swapTargetGearsetIdx = i;
                    }
                }
            }
        }

        ImGui.Spacing();

        // Armoury → Inventory
        ImGui.TextColored(new Vector4(0.7f, 1f, 0.8f, 1f), "Armoury → Inventory:");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(220 * ImGuiHelpers.GlobalScale);
        var invLabel = _inventoryMoveMode switch {
            InventoryMoveMode.FirstEmpty => "First Empty Slot",
            InventoryMoveMode.SwapWithArmoury => "Swap (inv copy → armoury)",
            _ => "?"
        };
        using (var combo3 = ImRaii.Combo("##InventoryMode", invLabel)) {
            if (combo3) {
                if (ImGui.Selectable("First Empty Slot", _inventoryMoveMode == InventoryMoveMode.FirstEmpty))
                    _inventoryMoveMode = InventoryMoveMode.FirstEmpty;
                ImGuiUtil.ToolTip("Moves the armoury item to the first empty slot in your inventory bags.");

                if (ImGui.Selectable("Swap (inv copy → armoury)", _inventoryMoveMode == InventoryMoveMode.SwapWithArmoury))
                    _inventoryMoveMode = InventoryMoveMode.SwapWithArmoury;
                ImGuiUtil.ToolTip("If the same item is already in your inventory, moves it to the armoury\nand the armoury copy to the inventory (swap). Falls back to first empty.");
            }
        }

        ImGui.Spacing();

        // Ignore Slots Checkboxes
        ImGui.TextColored(new Vector4(0.9f, 0.9f, 0.7f, 1f), "Ignore Slots:");
        ImGui.SameLine();
        ImGui.Checkbox("Weapons (Main/Off)", ref _moveIgnoreWeapons);
        ImGuiUtil.ToolTip("When moving whole gearsets, skip Main Hand and Off Hand items.");

        ImGui.SameLine();
        ImGui.Checkbox("Accessories", ref _moveIgnoreAccessories);
        ImGuiUtil.ToolTip("When moving whole gearsets, skip Ears, Neck, Wrists, and Rings.");

        ImGui.SameLine();
        ImGui.Checkbox("Soul Crystal", ref _moveIgnoreSoulCrystal);
        ImGuiUtil.ToolTip("When moving whole gearsets, skip the Soul Crystal.");
    }

    // Table
    private void DrawTable() {
        var slotIndices = Enum.GetValues<GearsetItemIndex>();

        using var padding = ImRaii.PushStyle(ImGuiStyleVar.CellPadding, Vector2.Zero)
                                  .Push(ImGuiStyleVar.FramePadding, Vector2.Zero);

        // Columns: [num] [icon] [name] [slot x N] [actions]
        using var table = ImRaii.Table(
            "##TableGearsets",
            3 + slotIndices.Length,
            ImGuiTableFlags.ScrollY | ImGuiTableFlags.NoSavedSettings);

        if (!table) return;

        if (_resetScrollPosition) {
            ImGui.SetScrollY(0);
            _resetScrollPosition = false;
        }

        // Col 0: gearset number (narrow)
        float numColW = ImGui.CalcTextSize("99").X + ImGui.GetStyle().ItemInnerSpacing.X * 2f;
        ImGui.TableSetupColumn("##Num", ImGuiTableColumnFlags.WidthFixed, numColW * ImGuiHelpers.GlobalScale);

        // Col 1: class icon
        float iconColW = 28 + ImGui.GetStyle().ItemInnerSpacing.X * 2f;
        ImGui.TableSetupColumn("##Icon", ImGuiTableColumnFlags.WidthFixed, iconColW * ImGuiHelpers.GlobalScale);

        // Col 2: gearset name
        ImGui.TableSetupColumn("##Name", ImGuiTableColumnFlags.WidthFixed, 120 * ImGuiHelpers.GlobalScale);

        // Item slot columns
        foreach (var slot in slotIndices) {
            if (slot == GearsetItemIndex.Belt) continue;
            ImGui.TableSetupColumn($"##Slot{(int)slot}", ImGuiTableColumnFlags.WidthFixed, IconSize.X * ImGuiHelpers.GlobalScale);
        }

        // Last col: action buttons
        float actionColW = 160f * ImGuiHelpers.GlobalScale;
        ImGui.TableSetupColumn("##Actions", ImGuiTableColumnFlags.WidthFixed, actionColW);

        var raptureGearsetModule = RaptureGearsetModule.Instance();
        var gearsetCount = InventoryManager.Instance()->GetPermittedGearsetCount();

        for (var gearsetIndex = 0; gearsetIndex < gearsetCount; gearsetIndex++) {
            var gearset = raptureGearsetModule->GetGearset(gearsetIndex);
            if (!gearset->Flags.HasFlag(GearsetFlag.Exists))
                continue;

            if (!GearsetPassesFilter(gearset))
                continue;

            using var rowId = ImRaii.PushId($"GS_{gearsetIndex}");

            ImGui.TableNextRow();

            // Col 0: gearset number
            ImGui.TableNextColumn();
            DrawGearsetNumberColumn(gearset, gearsetIndex, slotIndices);

            // Col 1: class icon (clickable to equip)
            ImGui.TableNextColumn();
            DrawGearsetIconColumn(gearset, gearsetIndex, slotIndices);

            // Col 2: gearset name
            ImGui.TableNextColumn();
            DrawGearsetNameColumn(gearset, gearsetIndex);

            // Item slot columns
            foreach (var slotIndex in slotIndices) {
                if (slotIndex == GearsetItemIndex.Belt) continue;

                using var slotId = ImRaii.PushId($"Slot{slotIndex}");

                var slotItem = gearset->GetItem(slotIndex);
                ImGui.TableNextColumn();

                var itemId = ItemUtil.GetBaseId(slotItem.ItemId).ItemId;
                if (itemId == 0) {
                    DrawEmptySlotPlaceholder(slotIndex);
                    continue;
                }

                if (!ExcelService.TryGetRow<Item>(itemId, out var item))
                    continue;

                ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 2f * ImGuiHelpers.GlobalScale);
                DrawItemIcon(gearset, gearsetIndex, slotIndex, slotItem, item);

                if (slotIndex == GearsetItemIndex.SoulStone) continue;

                var itemLevelText = $"{item.LevelItem.RowId}";
                ImGui.SetCursorPosX(
                    ImGui.GetCursorPosX()
                    + IconSize.X * ImGuiHelpers.GlobalScale / 2f
                    - ImGui.CalcTextSize(itemLevelText).X / 2f);
                ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 2f * ImGuiHelpers.GlobalScale);
            }

            // Last col: action buttons
            ImGui.TableNextColumn();
            DrawGearsetActionButtons(gearset, gearsetIndex, slotIndices);
        }
    }

    // Gearset Number Column (col 0) — plain text number + inventory warning dot
    private void DrawGearsetNumberColumn(GearsetEntry* gearset, int gearsetIndex, GearsetItemIndex[] slotIndices) {
        var startPos = ImGui.GetCursorPos();
        var region = ImGui.GetContentRegionAvail();
        var rowHeight = IconSize.Y * ImGuiHelpers.GlobalScale + ImGui.GetFrameHeight();

        // Draw gearset number centred (plain text, no selectable)
        var text = $"{gearsetIndex + 1}";
        var textSz = ImGui.CalcTextSize(text);
        ImGui.SetCursorPos(startPos + new Vector2(
            MathF.Max(0, (region.X - textSz.X) * 0.5f),
            (rowHeight - textSz.Y) * 0.5f));
        ImGui.TextUnformatted(text);
    }

    // Gearset Class Icon Column (col 1) — clicking equips gearset, shows tooltip on hover
    private void DrawGearsetIconColumn(GearsetEntry* gearset, int gearsetIndex, GearsetItemIndex[] slotIndices) {
        var startPos = ImGui.GetCursorPos();
        var region = ImGui.GetContentRegionAvail();
        var rowHeight = IconSize.Y * ImGuiHelpers.GlobalScale + ImGui.GetFrameHeight();
        var iconSize = 28 * ImGuiHelpers.GlobalScale;

        // Selectable covering the icon cell — clicking equips the gearset (as it was before)
        bool isEquipped = gearsetIndex == RaptureGearsetModule.Instance()->CurrentGearsetIndex;
        if (ImGui.Selectable(
                $"##Equip_{gearsetIndex}",
                isEquipped,
                ImGuiSelectableFlags.None,
                new Vector2(region.X, rowHeight))) {
            UIGlobals.PlaySoundEffect(8);
            RaptureGearsetModule.Instance()->EquipGearset(gearsetIndex);
        }

        // Gearset tooltip on hover over the class icon
        if (ImGui.IsItemHovered()) {
            DrawGearsetTooltip(gearset, slotIndices);
        }

        // Class icon drawn centered inside the selectable
        var itemStartPos = startPos + new Vector2(
            MathF.Max(0, (region.X - iconSize) * 0.5f),
            (rowHeight - iconSize) * 0.5f);

        ImGui.SetCursorPos(itemStartPos);
        DalamudApi.TextureProvider.DrawIcon(62100 + gearset->ClassJob, new Vector2(iconSize));

        // Indicator dot on the class icon if any item is in inventory or missing
        var (_, invCount, missingCount) = GetGearsetItemStatus(gearset, slotIndices);
        if (invCount > 0 || missingCount > 0) {
            var screenPos = ImGui.GetCursorScreenPos();
            var cursorPos = ImGui.GetCursorPos();
            var dotCenter = new Vector2(
                screenPos.X - cursorPos.X + itemStartPos.X + iconSize - 2f * ImGuiHelpers.GlobalScale,
                screenPos.Y - cursorPos.Y + itemStartPos.Y + 2f * ImGuiHelpers.GlobalScale);
            uint dotColor = missingCount > 0 ? 0xFF2222FFu : 0xFF0088FFu; // Red if missing, Amber if in inventory
            var drawList = ImGui.GetWindowDrawList();
            drawList.AddCircleFilled(dotCenter, 4f * ImGuiHelpers.GlobalScale, dotColor);
            drawList.AddCircle(dotCenter, 4f * ImGuiHelpers.GlobalScale, 0xAA000000u);
        }
    }

    private static void DrawGearsetTooltip(GearsetEntry* gearset, GearsetItemIndex[] slotIndices) {
        using var tooltip = ImRaii.Tooltip();
        ImGui.Text($"[{gearset->Id + 1}] {gearset->NameString}");

        if (gearset->GlamourSetLink != 0)
            ImGui.Text($"{TextService.GetAddonText(3185)}: {gearset->GlamourSetLink}");

        var (_, inInventory, missing) = GetGearsetItemStatus(gearset, slotIndices);
        if (inInventory > 0 || missing > 0) {
            ImGui.Separator();
            if (inInventory > 0)
                ImGui.TextColored(new Vector4(1f, 0.65f, 0f, 1f),
                    $"{inInventory} item(s) in Inventory (not Armoury)");
            if (missing > 0)
                ImGui.TextColored(new Vector4(1f, 0.3f, 0.3f, 1f),
                    $"{missing} item(s) not found anywhere");
        } else {
            ImGui.TextColored(new Vector4(0.4f, 0.9f, 0.4f, 1f), "All items in Armoury / Equipped");
        }
    }

    // Gearset Name Column (col 2)
    private static void DrawGearsetNameColumn(GearsetEntry* gearset, int gearsetIndex) {
        var name = gearset->NameString;
        var availWidth = ImGui.GetContentRegionAvail().X;
        var rowHeight = IconSize.Y * ImGuiHelpers.GlobalScale + ImGui.GetFrameHeight();

        // Centre vertically
        var cursorY = ImGui.GetCursorPosY() + (rowHeight - ImGui.GetTextLineHeight()) * 0.5f;
        ImGui.SetCursorPosY(cursorY);

        // Truncate with ellipsis if too wide
        string displayName = name;
        var textSize = ImGui.CalcTextSize(name);
        if (textSize.X > availWidth && availWidth > 0) {
            const string ellipsis = "...";
            float ellipsisW = ImGui.CalcTextSize(ellipsis).X;
            int lo = 0, hi = name.Length;
            while (lo < hi) {
                int mid = (lo + hi + 1) / 2;
                if (ImGui.CalcTextSize(name[..mid]).X + ellipsisW <= availWidth) lo = mid;
                else hi = mid - 1;
            }
            displayName = lo > 0 ? name[..lo] + ellipsis : ellipsis;
        }

        ImGui.TextUnformatted(displayName);

        if (ImGui.IsItemHovered() && displayName != name) {
            using var tt = ImRaii.Tooltip();
            ImGui.TextUnformatted($"[{gearsetIndex + 1}] {name}");
        }
    }

    // Gearset Action Buttons (last col)
    private void DrawGearsetActionButtons(GearsetEntry* gearset, int gearsetIndex, GearsetItemIndex[] slotIndices) {
        var rowHeight = IconSize.Y * ImGuiHelpers.GlobalScale + ImGui.GetFrameHeight();

        // Small space between item icons columns and action buttons
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 10f * ImGuiHelpers.GlobalScale);

        // Restore standard FramePadding so action buttons have default size (table had pushed FramePadding to zero)
        using var fp = ImRaii.PushStyle(ImGuiStyleVar.FramePadding, ImGuiHelpers.ScaledVector2(5, 4));

        var btnHeight = ImGui.GetFontSize() + ImGuiHelpers.GlobalScale * 8f;
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + (rowHeight - btnHeight) * 0.5f);

        var gsId = gearset->Id;
        var gsName = gearset->NameString;
        var gsIdx = gearsetIndex;
        var slots = slotIndices;

        // Equip
        if (ImGuiUtil.IconButton(FontAwesomeIcon.Play, $"##BtnEquip_{gsIdx}", $"Equip Gearset [{gsId + 1}] {gsName}")) {
            _pendingAction = () => {
                UIGlobals.PlaySoundEffect(8);
                RaptureGearsetModule.Instance()->EquipGearset(gsIdx);
            };
        }

        ImGui.SameLine(0, 4f * ImGuiHelpers.GlobalScale);

        // Try On all items in this gearset
        if (ImGuiUtil.IconButton(FontAwesomeIcon.Tshirt, $"##BtnTryOn_{gsIdx}", $"Try On entire gearset [{gsId + 1}] {gsName}")) {
            var keep = _tryOnKeepOutfit;
            _pendingAction = () => TryOnGearset(gearset, slots, keep);
        }

        ImGui.SameLine(0, 4f * ImGuiHelpers.GlobalScale);

        // Move Inventory → Armoury
        if (ImGuiUtil.IconButton(FontAwesomeIcon.BoxOpen, $"##BtnInv2Arm_{gsIdx}", $"Move Inventory items → Armoury [{gsId + 1}] {gsName}")) {
            _pendingAction = () => MoveGearsetInventoryItemsToArmoury(gearset, slots);
        }

        ImGui.SameLine(0, 4f * ImGuiHelpers.GlobalScale);

        // Move Armoury → Inventory
        if (ImGuiUtil.IconButton(FontAwesomeIcon.ShoppingBag, $"##BtnArm2Inv_{gsIdx}", $"Move Armoury items → Inventory [{gsId + 1}] {gsName}")) {
            _pendingAction = () => MoveGearsetArmouryItemsToInventory(gearset, slots);
        }
    }

    // Item Icon Cell
    public void DrawItemIcon(GearsetEntry* gearset, int gearsetIndex, GearsetItemIndex slotIndex, GearsetItem slotItem, ItemHandle item) {
        var startPos = ImGui.GetCursorPos();
        var screenBase = ImGui.GetCursorScreenPos();

        // Item icon
        ImGui.SetCursorPos(startPos + IconInset * ImGuiHelpers.GlobalScale);
        DalamudApi.TextureProvider.DrawIcon(
            new GameIconLookup(GetItemIcon(item), ItemUtil.IsHighQuality(slotItem.ItemId)),
            (IconSize - IconInset * 2f) * ImGuiHelpers.GlobalScale);

        // Determine item location — equipped counts as "accessible" (no warning border)
        bool inArmoury = slotItem.IsInArmoury();
        bool isEquipped = !inArmoury && IsItemEquipped(slotItem);
        bool inInventory = !inArmoury && !isEquipped && slotItem.IsInInventory();
        bool isMissing = slotItem.ItemId != 0 && !inArmoury && !isEquipped && !inInventory;

        if (inInventory) {
            // Orange border — item is in inventory bag, not armoury
            var drawList = ImGui.GetWindowDrawList();
            drawList.AddRect(
                screenBase,
                screenBase + IconSize * ImGuiHelpers.GlobalScale,
                0xFF0088FFu, 2f, ImDrawFlags.None, 2f * ImGuiHelpers.GlobalScale);
        } else if (isMissing) {
            // Red border — item not found anywhere
            var drawList = ImGui.GetWindowDrawList();
            drawList.AddRect(
                screenBase,
                screenBase + IconSize * ImGuiHelpers.GlobalScale,
                0xFF2222FFu, 2f, ImDrawFlags.None, 2f * ImGuiHelpers.GlobalScale);
        }

        // Dye color dots
        DrawDyeDots(screenBase, slotItem.Stain0Id, slotItem.Stain1Id);

        // Reset cursor to bottom of icon cell
        ImGui.SetCursorPos(startPos + new Vector2(0, (IconSize.Y - 3) * ImGuiHelpers.GlobalScale));

        // Try-On on hover (ignore Soul Crystal)
        if (_tryOnOnHover && slotIndex != GearsetItemIndex.SoulStone && ImGui.IsItemHovered()) {
            TryOnItemThrottled(gearsetIndex, slotItem.ItemId, slotItem.Stain0Id, slotItem.Stain1Id, slotItem.GlamourId);
        }

        // Right-click context menu
        DrawItemContextMenu(gearset, gearsetIndex, slotIndex, slotItem, item, inArmoury, inInventory);

        // Tooltip
        if (!ImGui.IsItemHovered() || !ExcelService.TryGetRow<Item>(item.ItemId, out var itemRow))
            return;

        DrawItemTooltip(gearset, slotIndex, slotItem, item, itemRow, inArmoury, isEquipped, inInventory);
    }

    /// <summary>
    /// Returns true when <paramref name="slotItem"/> is currently equipped by the local player
    /// (present in the EquippedItems container). Equipped = accessible, no missing border.
    /// </summary>
    private static bool IsItemEquipped(in GearsetItem slotItem) {
        var container = InventoryManager.Instance()->GetInventoryContainer(InventoryType.EquippedItems);
        if (container == null) return false;

        var targetId = slotItem.ItemId;
        for (int i = 0; i < container->Size; i++) {
            var slot = container->GetInventorySlot(i);
            if (slot == null) continue;
            if (slot->GetItemId() != targetId) continue;
            if (slot->GlamourId != slotItem.GlamourId) continue;
            if (slot->Stains[0] != slotItem.Stain0Id || slot->Stains[1] != slotItem.Stain1Id) continue;
            return true;
        }
        return false;
    }

    // Dye Dots
    private void DrawDyeDots(Vector2 screenBase, byte stain0Id, byte stain1Id) {
        if (stain0Id == 0 && stain1Id == 0) return;

        var drawList = ImGui.GetWindowDrawList();
        var iconPx = IconSize * ImGuiHelpers.GlobalScale;
        var dotR = DyeDotRadius * ImGuiHelpers.GlobalScale;
        var margin = 2f * ImGuiHelpers.GlobalScale;
        float dotY = screenBase.Y + iconPx.Y - margin - dotR;

        if (stain0Id != 0 && ExcelService.TryGetRow<Stain>(stain0Id, out var stain0)) {
            var color = StainColorToImGui(stain0.Color);
            float dot0X = screenBase.X + iconPx.X - margin - dotR
                          - (stain1Id != 0 ? dotR * 2 + margin : 0f);
            drawList.AddCircleFilled(new Vector2(dot0X, dotY), dotR, color);
            drawList.AddCircle(new Vector2(dot0X, dotY), dotR, 0xAA000000u);
        }

        if (stain1Id != 0 && ExcelService.TryGetRow<Stain>(stain1Id, out var stain1)) {
            var color = StainColorToImGui(stain1.Color);
            float dot1X = screenBase.X + iconPx.X - margin - dotR;
            drawList.AddCircleFilled(new Vector2(dot1X, dotY), dotR, color);
            drawList.AddCircle(new Vector2(dot1X, dotY), dotR, 0xAA000000u);
        }
    }

    // Lumina Stain.Color is packed as 0x00RRGGBB (uint: alpha=0 at MSB, R=bits16, G=bits8, B=bits0).
    // Reference: HaselCommon StainExtensions: ReverseEndianness(row.Color)>>8 extracts R from bits16-23, G from bits8-15, B from bits0-7.
    // ImGui DrawList/PushColor expect ABGR uint (A=MSB bits24-31, R=LSB bits0-7).
    private static uint StainColorToImGui(uint stainColor) {
        byte r = (byte)((stainColor >> 16) & 0xFF);
        byte g = (byte)((stainColor >> 8) & 0xFF);
        byte b = (byte)((stainColor) & 0xFF);
        return (uint)((0xFF << 24) | (b << 16) | (g << 8) | r); // ABGR
    }

    // Item Context Menu
    private void DrawItemContextMenu(GearsetEntry* gearset, int gearsetIndex, GearsetItemIndex slotIndex, GearsetItem slotItem, ItemHandle item, bool inArmoury, bool inInventory) {
        using var popup = ImRaii.ContextPopupItem($"##ItemCtx_{gearsetIndex}_{(int)slotIndex}");
        if (!popup) return;

        // Copy values before lambdas capture them (avoids ref/in capture CS1628)
        var gsIdx = gearsetIndex;
        var itemId = item.ItemId;
        var s0 = slotItem.Stain0Id;
        var s1 = slotItem.Stain1Id;
        var glamId = slotItem.GlamourId;
        var itemName = GetItemName(item).ToString();
        var gsId = gearset->Id;
        var gsName = gearset->NameString;
        var capturedSlotItem = slotItem;

        // Equip entire gearset
        // if (ImGui.MenuItem($"Equip [{gsId + 1}] {gsName}")) {
        //     _pendingAction = () => {
        //         UIGlobals.PlaySoundEffect(8);
        //         RaptureGearsetModule.Instance()->EquipGearset(gsIdx);
        //     };
        // }

        // ImGui.Separator();

        // Try On this item (Soul Crystal cannot be previewed)
        using (ImRaii.Disabled(slotIndex == GearsetItemIndex.SoulStone)) {
            if (ImGui.MenuItem("Try On")) {
                var keep = _tryOnKeepOutfit;
                _pendingAction = () => TryOnViaAgent(itemId, s0, s1, glamId, keep);
            }
            ImGui.SameLine();
            ImGui.TextDisabled(_tryOnKeepOutfit ? "(Keep Outfit ON)" : "(Keep Outfit OFF)");
        }

        ImGui.Separator();

        // Move to Armoury (only when item is in inventory bag)
        using (ImRaii.Disabled(!inInventory)) {
            if (ImGui.MenuItem("Move to Armoury Chest")) {
                _pendingAction = () => MoveSingleItemToArmoury(capturedSlotItem);
            }
        }
        if (!inInventory) ImGuiUtil.ToolTip("Item is not in your Inventory.");

        // Move to Inventory (only when item is in armoury)
        using (ImRaii.Disabled(!inArmoury)) {
            if (ImGui.MenuItem("Move to Inventory")) {
                _pendingAction = () => MoveSingleItemToInventory(capturedSlotItem);
            }
        }
        if (!inArmoury) ImGuiUtil.ToolTip("Item is not in the Armoury Chest.");

        ImGui.Separator();

        // Copy item name
        if (ImGui.MenuItem("Copy Name")) {
            ImGui.SetClipboardText(itemName);
        }
    }

    // Item Tooltip
    private void DrawItemTooltip(
        GearsetEntry* gearset,
        GearsetItemIndex slotIndex,
        GearsetItem slotItem,
        ItemHandle item,
        Item itemRow,
        bool inArmoury,
        bool isEquipped,
        bool inInventory) {

        using var _ = ImRaii.Tooltip();

        // Item name
        ImGui.TextColored(GetItemRarityColor(item), GetItemName(item).ToString());

        if (itemRow.ItemUICategory.IsValid) {
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() - ImGui.GetStyle().ItemSpacing.Y);
            ImGui.Text(itemRow.ItemUICategory.Value.Name.ToString() ?? string.Empty);
        }

        // Location indicator
        ImGui.Separator();
        if (inArmoury) {
            ImGui.TextColored(new Vector4(0.4f, 0.9f, 0.4f, 1f), "In Armoury Chest");
        } else if (isEquipped) {
            ImGui.TextColored(new Vector4(0.4f, 0.9f, 0.4f, 1f), "Currently Equipped");
        } else if (inInventory) {
            ImGui.TextColored(new Vector4(1f, 0.65f, 0f, 1f), "In Inventory (not in Armoury Chest)");
            ImGui.TextDisabled("Right-click → Move to Armoury Chest");
        } else {
            ImGui.TextColored(new Vector4(1f, 0.3f, 0.3f, 1f), "Item not found in Inventory or Armoury");
        }

        // Glamour + dye info
        var glamourItem = new ItemHandle(slotItem.GlamourId);
        bool hasGlamourOrDye = !glamourItem.IsEmpty || slotItem.Stain0Id != 0 || slotItem.Stain1Id != 0;
        if (hasGlamourOrDye) ImGui.Separator();

        if (!glamourItem.IsEmpty) {
            ImGui.Text("Glamour:");
            ImGui.SameLine();
            ImGui.TextColored(GetItemRarityColor(glamourItem), GetItemName(glamourItem).ToString());
        }

        DrawDyeTooltipLine("Dye 1:", slotItem.Stain0Id);
        DrawDyeTooltipLine("Dye 2:", slotItem.Stain1Id);

        // "Also used in" gearset list
        var usedInGearsets = GetItemInGearsetsList(slotItem.ItemId, slotIndex);
        if (usedInGearsets.Count > 1) {
            ImGui.Separator();
            DrawSharedGearsetsList(gearset->Id, usedInGearsets);
        }
    }

    // Helper: one dye line in the tooltip
    private static void DrawDyeTooltipLine(string label, byte stainId) {
        if (stainId == 0) return;
        if (!ExcelService.TryGetRow<Stain>(stainId, out var stain)) return;

        ImGui.Text(label);
        ImGui.SameLine();
        using (ImRaii.PushColor(ImGuiCol.Text, StainColorToImGui(stain.Color)))
            ImGui.Bullet();
        ImGui.SameLine(0, 0);
        ImGui.Text(TextService.GetStainName(stainId));
    }

    // Also-used-in gearsets list (compact 3-column layout)
    private static void DrawSharedGearsetsList(byte currentGearsetId, List<ItemInGearsetEntry> usedInGearsets) {
        var others = usedInGearsets.FindAll(e => e.Id != currentGearsetId);
        if (others.Count == 0) return;

        ImGui.Text($"Also used in {others.Count} gearset(s):");

        using var tbl = ImRaii.Table("##UsedInGS", 3, ImGuiTableFlags.None);
        if (!tbl) {
            foreach (var e in others)
                ImGui.Text($"  [{e.Id + 1}] {e.Name}");
            return;
        }

        // Calculate dynamic column width based on the longest gearset name, min 140 scaled
        float maxTextW = 120f;
        foreach (var e in others) {
            var tw = ImGui.CalcTextSize($"[{e.Id + 1}] {e.Name}").X;
            if (tw > maxTextW) maxTextW = tw;
        }
        float colW = MathF.Max(140f * ImGuiHelpers.GlobalScale, maxTextW + 16f * ImGuiHelpers.GlobalScale);

        ImGui.TableSetupColumn("##Col1", ImGuiTableColumnFlags.WidthFixed, colW);
        ImGui.TableSetupColumn("##Col2", ImGuiTableColumnFlags.WidthFixed, colW);
        ImGui.TableSetupColumn("##Col3", ImGuiTableColumnFlags.WidthFixed, colW);

        for (int i = 0; i < others.Count; i++) {
            if (i % 3 == 0) ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.Text($"[{others[i].Id + 1}] {others[i].Name}");
        }
    }

    // Search / Filter
    private bool GearsetPassesFilter(GearsetEntry* gearset) {
        var text = _searchText.Trim();
        if (string.IsNullOrEmpty(text)) return true;
        if (!_searchByGearset && !_searchByItem && !_searchByDye) return true;

        if (_searchByGearset && gearset->NameString.Contains(text, StringComparison.OrdinalIgnoreCase))
            return true;

        if (!_searchByItem && !_searchByDye) return false;

        foreach (var slotIndex in Enum.GetValues<GearsetItemIndex>()) {
            if (slotIndex == GearsetItemIndex.Belt) continue;

            var slotItem = gearset->GetItem(slotIndex);
            var itemId = ItemUtil.GetBaseId(slotItem.ItemId).ItemId;
            if (itemId == 0) continue;

            if (_searchByItem && ExcelService.TryGetRow<Item>(itemId, out var itemRow)) {
                if (itemRow.Name.ToString().Contains(text, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            if (_searchByDye) {
                if (slotItem.Stain0Id != 0) {
                    var dyeName = TextService.GetStainName(slotItem.Stain0Id);
                    if (!string.IsNullOrEmpty(dyeName) && dyeName.Contains(text, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                if (slotItem.Stain1Id != 0) {
                    var dyeName = TextService.GetStainName(slotItem.Stain1Id);
                    if (!string.IsNullOrEmpty(dyeName) && dyeName.Contains(text, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
        }

        return false;
    }

    // Try-On
    private void TryOnItemThrottled(int gearsetIndex, uint rawItemId, byte stain0, byte stain1, uint glamId) {
        var baseId = ItemUtil.GetBaseId(rawItemId).ItemId;
        if (baseId == 0) return;

        var previewId = glamId != 0 ? glamId : baseId;
        if (ExcelService.TryGetRow<Item>(previewId, out var itemRow)) {
            if (itemRow.EquipSlotCategory.RowId is 0 or 6 or 17) return;
        }

        if (previewId == _lastTryOnItemId && gearsetIndex == _lastTryOnGsIdx) return;
        if (!EzThrottler.Throttle("GearGridTryOn", 400)) return;

        _lastTryOnItemId = previewId;
        _lastTryOnGsIdx = gearsetIndex;

        var iid = previewId;
        var s0 = stain0;
        var s1 = stain1;
        var gl = glamId;
        var keep = _tryOnKeepOutfit;
        _pendingAction = () => TryOnViaAgent(iid, s0, s1, gl, keep);
    }

    /// <summary>
    /// Calls AgentTryon.TryOn, optionally setting SaveDeleteOutfit so the game keeps
    /// previously previewed pieces visible when trying on a new item.
    /// Prioritizes glamourItemId if present so the previewed appearance is accurate.
    /// </summary>
    private static void TryOnViaAgent(uint itemId, byte stain0, byte stain1, uint glamourId, bool keepOutfit) {
        var agent = AgentTryon.Instance();
        if (agent != null)
            agent->SaveDeleteOutfit = keepOutfit;

        var previewId = glamourId != 0 ? glamourId : itemId;
        AgentTryon.TryOn(0, previewId, stain0, stain1, glamourId);
    }

    /// <summary>
    /// Previews the entire gearset in the Try-On window, slot by slot.
    /// Uses keepOutfit=true for all slots except the first so previously added items stay.
    /// Skips Belt and SoulStone (which cannot be previewed in Fitting Room).
    /// Prioritizes glamour item ID so appearance is accurate.
    /// </summary>
    private static void TryOnGearset(GearsetEntry* gearset, GearsetItemIndex[] slotIndices, bool keepOutfit) {
        var agent = AgentTryon.Instance();
        bool first = true;
        foreach (var slotIndex in slotIndices) {
            if (slotIndex == GearsetItemIndex.Belt || slotIndex == GearsetItemIndex.SoulStone) continue;

            var slotItem = gearset->GetItem(slotIndex);
            if (slotItem.ItemId == 0) continue;

            var baseId = ItemUtil.GetBaseId(slotItem.ItemId).ItemId;
            if (baseId == 0) continue;

            var previewId = slotItem.GlamourId != 0 ? slotItem.GlamourId : baseId;

            if (ExcelService.TryGetRow<Item>(previewId, out var itemRow)) {
                var slotCat = itemRow.EquipSlotCategory.RowId;
                if (slotCat is 0 or 6 or 17) continue;
            }

            // For the first item respect the user's keepOutfit preference.
            // For subsequent items in this batch always keep so they stack.
            if (agent != null)
                agent->SaveDeleteOutfit = !first || keepOutfit;

            AgentTryon.TryOn(0, previewId, slotItem.Stain0Id, slotItem.Stain1Id, slotItem.GlamourId);
            first = false;
        }
    }

    // Move: Inventory → Armoury
    private void MoveSingleItemToArmoury(GearsetItem slotItem) {
        var invSrc = slotItem.FindGearsetItemInInventory();
        if (invSrc == null) return;

        var armouryType = slotItem.GetInventoryType();
        if (armouryType == default) return;

        Context.Plugin.ItemMover.Enqueue(invSrc.Value, armouryType);
    }

    private bool ShouldSkipSlotForMove(GearsetItemIndex slotIndex) {
        if (slotIndex == GearsetItemIndex.Belt)
            return true;

        if (_moveIgnoreWeapons && (slotIndex == GearsetItemIndex.MainHand || slotIndex == GearsetItemIndex.OffHand))
            return true;

        if (_moveIgnoreAccessories && (slotIndex == GearsetItemIndex.Ears
                                    || slotIndex == GearsetItemIndex.Neck
                                    || slotIndex == GearsetItemIndex.Wrists
                                    || slotIndex == GearsetItemIndex.RingRight
                                    || slotIndex == GearsetItemIndex.RingLeft))
            return true;

        if (_moveIgnoreSoulCrystal && slotIndex == GearsetItemIndex.SoulStone)
            return true;

        return false;
    }

    private void MoveGearsetInventoryItemsToArmoury(GearsetEntry* gearset, GearsetItemIndex[] slotIndices) {
        foreach (var slotIndex in slotIndices) {
            if (ShouldSkipSlotForMove(slotIndex)) continue;

            var slotItem = gearset->GetItem(slotIndex);
            if (slotItem.ItemId == 0) continue;
            if (slotItem.IsInArmoury()) continue;

            var invSrc = slotItem.FindGearsetItemInInventory();
            if (invSrc == null) continue;

            var armouryType = slotItem.GetInventoryType();
            if (armouryType == default) continue;

            Context.Plugin.ItemMover.Enqueue(invSrc.Value, armouryType);
        }
    }

    // Move: Armoury → Inventory
    private void MoveSingleItemToInventory(GearsetItem slotItem) {
        var armouryType = slotItem.GetInventoryType();
        if (armouryType == default) return;

        var container = InventoryManager.Instance()->GetInventoryContainer(armouryType);
        var armourySrc = InventoryHelper.FindGearsetItemInArmoury(slotItem, container);
        if (armourySrc == null) return;

        var emptySlot = InventoryHelper.FindFirstEmptyInventorySlot();
        if (emptySlot == null) return;

        Context.Plugin.ItemMover.Enqueue(armourySrc.Value, emptySlot.Value.Type, emptySlot.Value.Slot);
    }

    private void MoveGearsetArmouryItemsToInventory(GearsetEntry* gearset, GearsetItemIndex[] slotIndices) {
        foreach (var slotIndex in slotIndices) {
            if (ShouldSkipSlotForMove(slotIndex)) continue;

            var slotItem = gearset->GetItem(slotIndex);
            if (slotItem.ItemId == 0) continue;
            if (!slotItem.IsInArmoury()) continue;

            var armouryType = slotItem.GetInventoryType();
            if (armouryType == default) continue;

            var container = InventoryManager.Instance()->GetInventoryContainer(armouryType);
            var armourySrc = InventoryHelper.FindGearsetItemInArmoury(slotItem, container);
            if (armourySrc == null) continue;

            var emptySlot = InventoryHelper.FindFirstEmptyInventorySlot();
            if (emptySlot == null) break; // no more inventory space

            Context.Plugin.ItemMover.Enqueue(armourySrc.Value, emptySlot.Value.Type, emptySlot.Value.Slot);
        }
    }

    // Empty Slot Placeholder
    private static void DrawEmptySlotPlaceholder(GearsetItemIndex slotIndex) {
        var startPos = ImGui.GetCursorPos();
        var screenBase = ImGui.GetCursorScreenPos();
        var scaledIcon = IconSize * ImGuiHelpers.GlobalScale;
        var scaledInset = IconInset * ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();

        // Dark translucent background
        drawList.AddRectFilled(screenBase, screenBase + scaledIcon, 0x44000000u, 2f);

        // Subtle slot outline
        drawList.AddRect(screenBase + scaledInset, screenBase + scaledIcon - scaledInset, 0x33FFFFFFu, 1f);

        // Faint placeholder icon for this slot type
        var placeholderIconId = GetSlotPlaceholderIconId(slotIndex);
        if (placeholderIconId != 0) {
            ImGui.SetCursorPos(startPos + scaledInset);
            DalamudApi.TextureProvider.DrawIcon(
                new GameIconLookup(placeholderIconId),
                (IconSize - IconInset * 2f) * ImGuiHelpers.GlobalScale,
                tintCol: new Vector4(1f, 1f, 1f, 0.25f));
        }

        ImGui.SetCursorPos(startPos + new Vector2(0, (IconSize.Y - 3) * ImGuiHelpers.GlobalScale));
    }

    private static uint GetSlotPlaceholderIconId(GearsetItemIndex slotIndex) => slotIndex switch {
        GearsetItemIndex.MainHand => 60011,
        GearsetItemIndex.OffHand => 60110,
        GearsetItemIndex.Head => 60124,
        GearsetItemIndex.Body => 60126,
        GearsetItemIndex.Hands => 60129,
        GearsetItemIndex.Legs => 60128,
        GearsetItemIndex.Feet => 60130,
        GearsetItemIndex.Ears => 60133,
        GearsetItemIndex.Neck => 60132,
        GearsetItemIndex.Wrists => 60134,
        GearsetItemIndex.RingRight => 60135,
        GearsetItemIndex.RingLeft => 60135,
        GearsetItemIndex.SoulStone => 61762,
        _ => 0,
    };

    // Aggregate Status
    private static (int inArmoury, int inInventory, int missing) GetGearsetItemStatus(
        GearsetEntry* gearset,
        GearsetItemIndex[] slotIndices) {

        int armoury = 0;
        int inventory = 0;
        int missing = 0;

        foreach (var slotIndex in slotIndices) {
            if (slotIndex == GearsetItemIndex.Belt) continue;

            var slotItem = gearset->GetItem(slotIndex);
            if (slotItem.ItemId == 0) continue;

            if (slotItem.IsInArmoury() || IsItemEquipped(slotItem))
                armoury++;
            else if (slotItem.IsInInventory())
                inventory++;
            else
                missing++;
        }

        return (armoury, inventory, missing);
    }

    // Shared Helpers
    private record ItemInGearsetEntry(byte Id, string Name);

    private static List<ItemInGearsetEntry> GetItemInGearsetsList(uint itemId, GearsetItemIndex slotIndex) {
        var list = new List<ItemInGearsetEntry>();

        var raptureGearsetModule = RaptureGearsetModule.Instance();
        var permittedGearsetCount = InventoryManager.Instance()->GetPermittedGearsetCount();
        for (var gearsetIndex = 0; gearsetIndex < permittedGearsetCount; gearsetIndex++) {
            var gearset = raptureGearsetModule->GetGearset(gearsetIndex);
            if (!gearset->Flags.HasFlag(GearsetFlag.Exists))
                continue;

            ref var it = ref gearset->GetItem(slotIndex);
            if (it.ItemId == itemId)
                list.Add(new(gearset->Id, gearset->NameString));
        }

        return list;
    }

    public Vector4 GetItemRarityColor(ItemHandle item, bool isEdgeColor = false) {
        if (item.ItemId == 0) return Vector4.One;

        try {
            var colorType = ItemUtil.GetItemRarityColorType(item.ItemId, isEdgeColor);
            var atkStage = AtkStage.Instance();
            if (atkStage != null && atkStage->AtkUIColorHolder != null) {
                var rawColor = atkStage->AtkUIColorHolder->GetColor(false, colorType);
                if (rawColor != 0) {
                    var col = ImGui.ColorConvertU32ToFloat4(rawColor);
                    // Ensure not completely transparent or dark
                    if (col.W > 0.05f && (col.X > 0.05f || col.Y > 0.05f || col.Z > 0.05f))
                        return new Vector4(col.X, col.Y, col.Z, 1f);
                }
            }
        } catch {
            // ignore and fallback
        }

        // Secondary fallback by Item.Rarity from Lumina sheet
        if (ExcelService.TryGetRow<Item>(item.ItemId, out var itemRow)) {
            return itemRow.Rarity switch {
                2 => new Vector4(0.51f, 0.94f, 0.35f, 1f), // Green / Uncommon
                3 => new Vector4(0.35f, 0.68f, 1f, 1f),   // Blue / Rare
                4 => new Vector4(0.78f, 0.52f, 1f, 1f),   // Purple / Relic
                7 => new Vector4(1f, 0.55f, 0.8f, 1f),    // Pink / Aetherial
                _ => Vector4.One                          // Common / White
            };
        }

        return Vector4.One;
    }

    public ReadOnlySeString GetItemName(ItemHandle item, bool includeIcon = false)
        => ItemUtil.GetItemName(item.ItemId, includeIcon, DalamudApi.ClientState.ClientLanguage);

    public uint GetItemIcon(ItemHandle item)
        => ItemHelper.GetIconId(item.ItemId);
}
