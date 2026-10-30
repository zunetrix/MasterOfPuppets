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
    private bool _tryOnKeepOutfit = false;
    private uint _lastTryOnItemId = 0;
    private int _lastTryOnGsIdx = -1;

    //  Deferred action (avoids modifying game state inside table draw)
    private System.Action? _pendingAction = null;

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

    //  Header: search bar + category checkboxes + try-on toggle
    private void DrawHeader() {
        ImGui.SetNextItemWidth(220 * ImGuiHelpers.GlobalScale);
        ImGui.InputTextWithHint("##GearGridSearch", "Search…", ref _searchText, 128, ImGuiInputTextFlags.AutoSelectAll);

        ImGui.SameLine();
        ImGui.TextDisabled("in:");

        ImGui.SameLine();
        ImGui.Checkbox("Gearset", ref _searchByGearset);
        ImGui.SameLine();
        ImGui.Checkbox("Item", ref _searchByItem);
        ImGui.SameLine();
        ImGui.Checkbox("Dye", ref _searchByDye);

        ImGui.SameLine();
        ImGui.Spacing();
        ImGui.SameLine();

        // Try-On on hover toggle
        if (ImGui.Checkbox("Try On Hover", ref _tryOnOnHover)) {
            _lastTryOnItemId = 0;
            _lastTryOnGsIdx = -1;
        }
        ImGuiUtil.ToolTip("When enabled, hovering over an item icon will preview it (Try On).\nThrottled to avoid repeated calls.");

        // Keep outfit: controls AgentTryon.SaveDeleteOutfit so each new
        // Try-On doesn't replace the previous previewed pieces.
        if (_tryOnOnHover) {
            ImGui.SameLine();
            ImGui.Checkbox("Keep Outfit", ref _tryOnKeepOutfit);
            ImGuiUtil.ToolTip("When enabled, trying on a new item keeps previously previewed items visible\n(sets SaveDeleteOutfit on the game's Try-On agent).");
        }

        ImGui.Separator();
    }

    // Table
    private void DrawTable() {
        var slotIndices = Enum.GetValues<GearsetItemIndex>();

        using var padding = ImRaii.PushStyle(ImGuiStyleVar.CellPadding, Vector2.Zero)
                                  .Push(ImGuiStyleVar.FramePadding, Vector2.Zero);

        using var table = ImRaii.Table(
            "##TableGearsets",
            1 + slotIndices.Length,
            ImGuiTableFlags.ScrollY | ImGuiTableFlags.NoSavedSettings);

        if (!table) return;

        if (_resetScrollPosition) {
            ImGui.SetScrollY(0);
            _resetScrollPosition = false;
        }

        // First column: ID / class icon
        ImGui.TableSetupColumn(
            "##ID",
            ImGuiTableColumnFlags.WidthFixed,
            (24 + 14 + ImGui.GetStyle().ItemInnerSpacing.X * 2f + ImGui.GetStyle().ItemSpacing.X * 2f)
            * ImGuiHelpers.GlobalScale);

        foreach (var slot in slotIndices) {
            if (slot == GearsetItemIndex.Belt) continue;
            ImGui.TableSetupColumn($"##Slot{(int)slot}", ImGuiTableColumnFlags.WidthFixed, IconSize.X * ImGuiHelpers.GlobalScale);
        }

        var raptureGearsetModule = RaptureGearsetModule.Instance();
        var gearsetCount = InventoryManager.Instance()->GetPermittedGearsetCount();

        for (var gearsetIndex = 0; gearsetIndex < gearsetCount; gearsetIndex++) {
            var gearset = raptureGearsetModule->GetGearset(gearsetIndex);
            if (!gearset->Flags.HasFlag(GearsetFlag.Exists))
                continue;

            //  global search filter
            if (!GearsetPassesFilter(gearset))
                continue;

            using var id = ImRaii.PushId($"Gearset_{gearsetIndex}");

            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            DrawGearsetIdColumn(gearset, gearsetIndex, slotIndices);

            foreach (var slotIndex in slotIndices) {
                if (slotIndex == GearsetItemIndex.Belt) continue;

                using var slotId = ImRaii.PushId($"Slot{slotIndex}");

                var slotItem = gearset->GetItem(slotIndex);
                ImGui.TableNextColumn();

                var itemId = ItemUtil.GetBaseId(slotItem.ItemId).ItemId;
                if (itemId == 0) {
                    // Empty slot placeholder
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
        }
    }

    // Gearset ID column
    private void DrawGearsetIdColumn(GearsetEntry* gearset, int gearsetIndex, GearsetItemIndex[] slotIndices) {
        var startPos = ImGui.GetCursorPos();
        var region = ImGui.GetContentRegionAvail();
        var rowHeight = IconSize.Y * ImGuiHelpers.GlobalScale + ImGui.GetFrameHeight();

        if (ImGui.Selectable(
                "##Equip",
                gearsetIndex == RaptureGearsetModule.Instance()->CurrentGearsetIndex,
                ImGuiSelectableFlags.None,
                new Vector2(region.X, rowHeight))) {
            UIGlobals.PlaySoundEffect(8);
            RaptureGearsetModule.Instance()->EquipGearset(gearsetIndex);
        }

        // Gearset tooltip
        if (ImGui.IsItemHovered()) {
            using var tooltip = ImRaii.Tooltip();
            ImGui.Text($"[{gearset->Id + 1}] {gearset->NameString}");

            if (gearset->GlamourSetLink != 0)
                ImGui.Text($"{TextService.GetAddonText(3185)}: {gearset->GlamourSetLink}");

            // items-in-inventory summary
            var (_, inInventory, missing) = GetGearsetItemStatus(gearset, slotIndices);
            if (inInventory > 0 || missing > 0) {
                ImGui.Separator();
                if (inInventory > 0)
                    ImGui.TextColored(new Vector4(1f, 0.65f, 0f, 1f),
                        $"⚠ {inInventory} item(s) in Inventory (not Armoury)");
                if (missing > 0)
                    ImGui.TextColored(new Vector4(1f, 0.3f, 0.3f, 1f),
                        $"✖ {missing} item(s) not found anywhere");
            } else {
                ImGui.TextColored(new Vector4(0.4f, 0.9f, 0.4f, 1f), "✔ All items in Armoury Chest");
            }
        }

        // Right-click context menu on gearset row
        DrawGearsetContextMenu(gearset, gearsetIndex, slotIndices);

        // Class icon
        var iconSize = 28 * ImGuiHelpers.GlobalScale;
        var itemStartPos = startPos + new Vector2(region.X / 2f - iconSize / 2f, ImGui.GetStyle().ItemInnerSpacing.Y);

        ImGui.SetCursorPos(itemStartPos);
        DalamudApi.TextureProvider.DrawIcon(62100 + gearset->ClassJob, new Vector2(iconSize));

        // Gearset number
        var text = $"{gearsetIndex + 1}";
        ImGui.SetCursorPos(itemStartPos + new Vector2(iconSize / 2f - ImGui.CalcTextSize(text).X / 2f, iconSize));
        ImGui.Text(text);

        // orange dot indicator when items are in inventory
        var (_, invCount, _) = GetGearsetItemStatus(gearset, slotIndices);
        if (invCount > 0) {
            var drawList = ImGui.GetWindowDrawList();
            var screenPos = ImGui.GetCursorScreenPos(); // current cursor in screen space
            var cursorPos = ImGui.GetCursorPos();       // current cursor in window space
            var dotCenter = new Vector2(
                screenPos.X - cursorPos.X + itemStartPos.X + iconSize,
                screenPos.Y - cursorPos.Y + itemStartPos.Y);
            drawList.AddCircleFilled(dotCenter, 5f * ImGuiHelpers.GlobalScale, 0xFF0088FFu); // amber
        }
    }

    // Gearset context menu (right-click on ID column)
    private void DrawGearsetContextMenu(GearsetEntry* gearset, int gearsetIndex, GearsetItemIndex[] slotIndices) {
        using var popup = ImRaii.ContextPopupItem($"##GearsetCtx_{gearsetIndex}");
        if (!popup) return;

        if (ImGui.MenuItem($"Equip [{gearset->Id + 1}] {gearset->NameString}")) {
            var idx = gearsetIndex;
            _pendingAction = () => {
                UIGlobals.PlaySoundEffect(8);
                RaptureGearsetModule.Instance()->EquipGearset(idx);
            };
        }

        ImGui.Separator();

        // move all inventory items of this gearset to armoury
        if (ImGui.MenuItem("Move Inventory Items → Armoury")) {
            var gs = gearset;
            var slots = slotIndices;
            _pendingAction = () => MoveGearsetInventoryItemsToArmoury(gs, slots);
        }
        ImGuiUtil.ToolTip("Moves items belonging to this gearset that are currently in your Inventory to the Armoury Chest.");
    }

    // Item icon cell
    public void DrawItemIcon(GearsetEntry* gearset, int gearsetIndex, GearsetItemIndex slotIndex, GearsetItem slotItem, ItemHandle item) {
        var startPos = ImGui.GetCursorPos();
        var screenBase = ImGui.GetCursorScreenPos();

        // Item icon
        ImGui.SetCursorPos(startPos + IconInset * ImGuiHelpers.GlobalScale);
        DalamudApi.TextureProvider.DrawIcon(
            new GameIconLookup(GetItemIcon(item), ItemUtil.IsHighQuality(slotItem.ItemId)),
            (IconSize - IconInset * 2f) * ImGuiHelpers.GlobalScale);

        // border to indicate item is in inventory not armoury
        bool inArmoury = slotItem.IsInArmoury();
        bool inInventory = !inArmoury && slotItem.IsInInventory();

        if (inInventory) {
            // Orange border
            var drawList = ImGui.GetWindowDrawList();
            drawList.AddRect(
                screenBase,
                screenBase + IconSize * ImGuiHelpers.GlobalScale,
                0xFF0088FFu, 2f, ImDrawFlags.None, 2f * ImGuiHelpers.GlobalScale);
        } else if (slotItem.ItemId != 0 && !inArmoury) {
            // Red border - item not found anywhere
            var drawList = ImGui.GetWindowDrawList();
            drawList.AddRect(
                screenBase,
                screenBase + IconSize * ImGuiHelpers.GlobalScale,
                0xFF2222FFu, 2f, ImDrawFlags.None, 2f * ImGuiHelpers.GlobalScale);
        }

        // dye color dots
        DrawDyeDots(screenBase, slotItem.Stain0Id, slotItem.Stain1Id);

        // Reset cursor to bottom of icon cell
        ImGui.SetCursorPos(startPos + new Vector2(0, (IconSize.Y - 3) * ImGuiHelpers.GlobalScale));

        // try-On on hover
        if (_tryOnOnHover && ImGui.IsItemHovered()) {
            TryOnItemThrottled(gearsetIndex, slotItem.ItemId, slotItem.Stain0Id, slotItem.Stain1Id, slotItem.GlamourId);
        }

        // Right-click context menu
        DrawItemContextMenu(gearset, gearsetIndex, slotIndex, slotItem, item);

        // Tooltip
        if (!ImGui.IsItemHovered() || !ExcelService.TryGetRow<Item>(item.ItemId, out var itemRow))
            return;

        DrawItemTooltip(gearset, slotIndex, slotItem, item, itemRow, inArmoury, inInventory);
    }

    // Dye dot indicators (drawn with ImDrawList)
    private void DrawDyeDots(Vector2 screenBase, byte stain0Id, byte stain1Id) {
        if (stain0Id == 0 && stain1Id == 0) return;

        var drawList = ImGui.GetWindowDrawList();
        var iconPx = IconSize * ImGuiHelpers.GlobalScale;
        var dotR = DyeDotRadius * ImGuiHelpers.GlobalScale;
        var margin = 2f * ImGuiHelpers.GlobalScale;
        float dotY = screenBase.Y + iconPx.Y - margin - dotR;

        if (stain0Id != 0 && ExcelService.TryGetRow<Stain>(stain0Id, out var stain0)) {
            var color = StainColorToImGui(stain0.Color);
            // shift left if two dyes so they sit side by side
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

    // Lumina Stain.Color is packed as 0xRRGGBB00 (big-endian, alpha unused)
    // ImGui wants ABGR uint.
    private static uint StainColorToImGui(uint stainColor) {
        byte r = (byte)((stainColor >> 24) & 0xFF);
        byte g = (byte)((stainColor >> 16) & 0xFF);
        byte b = (byte)((stainColor >> 8) & 0xFF);
        return (uint)((0xFF << 24) | (b << 16) | (g << 8) | r); // ABGR
    }

    // Item context menu
    private void DrawItemContextMenu(GearsetEntry* gearset, int gearsetIndex, GearsetItemIndex slotIndex, GearsetItem slotItem, ItemHandle item) {
        using var popup = ImRaii.ContextPopupItem($"##ItemCtx_{gearsetIndex}_{(int)slotIndex}");
        if (!popup) return;

        // Copy values before lambdas capture them (avoids ref/in capture CS1628)
        var gsIdx = gearsetIndex;
        var itemId = item.ItemId;
        var s0 = slotItem.Stain0Id;
        var s1 = slotItem.Stain1Id;
        var glamId = slotItem.GlamourId;
        var isInInv = slotItem.IsInInventory();
        var itemName = GetItemName(item).ToString();
        var gsId = gearset->Id;
        var gsName = gearset->NameString;

        // Equip entire gearset
        if (ImGui.MenuItem($"Equip [{gsId + 1}] {gsName}")) {
            _pendingAction = () => {
                UIGlobals.PlaySoundEffect(8);
                RaptureGearsetModule.Instance()->EquipGearset(gsIdx);
            };
        }

        ImGui.Separator();

        // Try On
        if (ImGui.MenuItem("Try On")) {
            var keep = _tryOnKeepOutfit;
            _pendingAction = () => TryOnViaAgent(itemId, s0, s1, glamId, keep);
        }
        ImGui.SameLine();
        ImGui.TextDisabled(_tryOnKeepOutfit ? "(Keep Outfit ON)" : "(Keep Outfit OFF)");

        ImGui.Separator();

        // Move to Armoury
        using (ImRaii.Disabled(!isInInv)) {
            if (ImGui.MenuItem("Move to Armoury Chest")) {
                // capture a copy of slotItem for the lambda
                var capturedSlotItem = slotItem;
                _pendingAction = () => MoveSingleItemToArmoury(capturedSlotItem);
            }
        }
        if (!isInInv)
            ImGuiUtil.ToolTip("Item is not in your Inventory.");

        ImGui.Separator();

        // Copy item name
        if (ImGui.MenuItem($"Copy Name")) {
            ImGui.SetClipboardText(itemName);
        }
    }

    // Item tooltip (Features 1, 2, 7)
    private void DrawItemTooltip(
        GearsetEntry* gearset,
        GearsetItemIndex slotIndex,
        GearsetItem slotItem,
        ItemHandle item,
        Item itemRow,
        bool inArmoury,
        bool inInventory) {

        using var _ = ImRaii.Tooltip();

        // Item name
        ImGui.TextColored(GetItemRarityColor(item), GetItemName(item).ToString());

        var holdingShift = ImGui.IsKeyDown(ImGuiKey.LeftShift) || ImGui.IsKeyDown(ImGuiKey.RightShift);
        if (holdingShift) {
            ImGui.SameLine();
            ImGui.Text($"[{item.ItemId}]");
        }

        if (itemRow.ItemUICategory.IsValid) {
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() - ImGui.GetStyle().ItemSpacing.Y);
            ImGui.Text(itemRow.ItemUICategory.Value.Name.ToString() ?? string.Empty);
        }

        // location indicator
        ImGui.Separator();
        if (inArmoury) {
            ImGui.TextColored(new Vector4(0.4f, 0.9f, 0.4f, 1f), "✔ In Armoury Chest");
        } else if (inInventory) {
            ImGui.TextColored(new Vector4(1f, 0.65f, 0f, 1f), "⚠ In Inventory (not in Armoury Chest)");
            ImGui.TextDisabled("Right-click → Move to Armoury Chest");
        } else {
            ImGui.TextColored(new Vector4(1f, 0.3f, 0.3f, 1f), "✖ Item not found in Inventory or Armoury");
        }

        //  Glamour + dye info (Feature 2 tooltip)
        var glamourItem = new ItemHandle(slotItem.GlamourId);
        bool hasGlamourOrDye = !glamourItem.IsEmpty || slotItem.Stain0Id != 0 || slotItem.Stain1Id != 0;
        if (hasGlamourOrDye) ImGui.Separator();

        if (!glamourItem.IsEmpty) {
            ImGui.Text("Glamour:");
            ImGui.SameLine();
            ImGui.TextColored(GetItemRarityColor(glamourItem), GetItemName(glamourItem).ToString());
            if (holdingShift) {
                ImGui.SameLine();
                ImGui.Text($"[{slotItem.GlamourId}]");
            }
        }

        DrawDyeTooltipLine("Dye 1:", slotItem.Stain0Id, holdingShift);
        DrawDyeTooltipLine("Dye 2:", slotItem.Stain1Id, holdingShift);

        //  also used in list
        var usedInGearsets = GetItemInGearsetsList(slotItem.ItemId, slotIndex);
        if (usedInGearsets.Count > 1) {
            ImGui.Separator();
            DrawSharedGearsetsList(gearset->Id, usedInGearsets);
        }
    }

    // Helper: one dye line in the tooltip
    private static void DrawDyeTooltipLine(string label, byte stainId, bool holdingShift) {
        if (stainId == 0) return;
        if (!ExcelService.TryGetRow<Stain>(stainId, out var stain)) return;

        ImGui.Text(label);
        ImGui.SameLine();
        using (ImRaii.PushColor(ImGuiCol.Text, StainColorToImGui(stain.Color)))
            ImGui.Bullet();
        ImGui.SameLine(0, 0);
        ImGui.Text(TextService.GetStainName(stainId));
        if (holdingShift) {
            ImGui.SameLine();
            ImGui.Text($"[{stainId}]");
        }
    }

    // Also used in list
    private static void DrawSharedGearsetsList(byte currentGearsetId, List<ItemInGearsetEntry> usedInGearsets) {
        var others = usedInGearsets.FindAll(e => e.Id != currentGearsetId);
        if (others.Count == 0) return;

        ImGui.Text($"Also used in {others.Count} gearset(s):");

        using var tbl = ImRaii.Table("##UsedInGS", 2, ImGuiTableFlags.None);
        if (!tbl) {
            foreach (var e in others)
                ImGui.Text($"  [{e.Id + 1}] {e.Name}");
            return;
        }

        ImGui.TableSetupColumn("##Col1", ImGuiTableColumnFlags.WidthFixed, 140 * ImGuiHelpers.GlobalScale);
        ImGui.TableSetupColumn("##Col2", ImGuiTableColumnFlags.WidthFixed, 140 * ImGuiHelpers.GlobalScale);

        for (int i = 0; i < others.Count; i++) {
            if (i % 2 == 0) ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.Text($"[{others[i].Id + 1}] {others[i].Name}");
        }
    }

    // Global search / filter
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

    // Try-On with throttle
    private void TryOnItemThrottled(int gearsetIndex, uint rawItemId, byte stain0, byte stain1, uint glamId) {
        var baseId = ItemUtil.GetBaseId(rawItemId).ItemId;

        // Only fire when hovering a new item or after enough time passes
        if (baseId == _lastTryOnItemId && gearsetIndex == _lastTryOnGsIdx) return;
        if (!EzThrottler.Throttle("GearGridTryOn", 400)) return;

        _lastTryOnItemId = baseId;
        _lastTryOnGsIdx = gearsetIndex;

        // Copy values to avoid closure problems; set as pending action
        var iid = baseId;
        var s0 = stain0;
        var s1 = stain1;
        var gl = glamId;
        var keep = _tryOnKeepOutfit;
        _pendingAction = () => TryOnViaAgent(iid, s0, s1, gl, keep);
    }

    /// <summary>
    /// Calls AgentTryon.TryOn, optionally setting SaveDeleteOutfit so the
    /// game keeps previously previewed pieces visible when trying on a new item.
    /// </summary>
    private static void TryOnViaAgent(uint itemId, byte stain0, byte stain1, uint glamourId, bool keepOutfit) {
        var agent = AgentTryon.Instance();
        if (agent != null)
            agent->SaveDeleteOutfit = keepOutfit;

        AgentTryon.TryOn(0, itemId, stain0, stain1, glamourId);
    }

    // Move items to Armoury
    private void MoveSingleItemToArmoury(GearsetItem slotItem) {
        var invSrc = slotItem.FindGearsetItemInInventory();
        if (invSrc == null) return;

        var armouryType = slotItem.GetInventoryType();
        if (armouryType == default) return;

        Context.Plugin.ItemMover.Enqueue(invSrc.Value, armouryType);
    }

    private void MoveGearsetInventoryItemsToArmoury(GearsetEntry* gearset, GearsetItemIndex[] slotIndices) {
        foreach (var slotIndex in slotIndices) {
            if (slotIndex == GearsetItemIndex.Belt) continue;

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

    //  Empty slot placeholder (slot sem item atribuído no gearset)
    private static void DrawEmptySlotPlaceholder(GearsetItemIndex slotIndex) {
        var cursorPos = ImGui.GetCursorPos();
        var screenBase = ImGui.GetCursorScreenPos();
        var scaledIcon = IconSize * ImGuiHelpers.GlobalScale;
        var scaledInset = IconInset * ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();

        // Dark translucent background mimicking the HaselTweaks ULD background
        drawList.AddRectFilled(
            screenBase,
            screenBase + scaledIcon,
            0x44000000u,  // very dark semi-transparent
            2f);

        // Subtle slot-type icon drawn as a thin outlined square with a slot glyph
        var innerMin = screenBase + scaledInset;
        var innerMax = screenBase + scaledIcon - scaledInset;
        drawList.AddRect(innerMin, innerMax, 0x55FFFFFFu, 1f); // faint white border

        // Draw a small "empty" symbol (dash) centred inside
        var center = screenBase + scaledIcon * 0.5f;
        float dashHalf = 4f * ImGuiHelpers.GlobalScale;
        drawList.AddLine(
            new Vector2(center.X - dashHalf, center.Y),
            new Vector2(center.X + dashHalf, center.Y),
            0x55FFFFFFu, 1f);

        // Advance the cursor past the icon cell so layout is not broken
        ImGui.SetCursorPos(cursorPos + new Vector2(0, scaledIcon.Y));
    }

    //  aggregate item status for a whole gearset
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

            if (slotItem.IsInArmoury())
                armoury++;
            else if (slotItem.IsInInventory())
                inventory++;
            else
                missing++;
        }

        return (armoury, inventory, missing);
    }

    //  Shared helpers
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

    public uint GetItemRarityColor(ItemHandle item, bool isEdgeColor = false)
        => ItemUtil.GetItemRarityColorType(item, isEdgeColor);

    public ReadOnlySeString GetItemName(ItemHandle item, bool includeIcon = false)
        => ItemUtil.GetItemName(item.ItemId, includeIcon, DalamudApi.ClientState.ClientLanguage);

    public uint GetItemIcon(ItemHandle item)
        => ItemHelper.GetIconId(item.ItemId);
}
