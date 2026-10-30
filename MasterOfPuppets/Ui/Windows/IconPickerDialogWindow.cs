using System;
using System.Collections.Generic;
using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;

using MasterOfPuppets.Extensions.Dalamud;
using MasterOfPuppets.Util;
using MasterOfPuppets.Util.ImGuiIconPicker;

namespace MasterOfPuppets;

// Class to pick the icon for a macro
// based on:
// QoLBar IconBrowser: https://github.com/UnknownX7/QoLBar/blob/f80d64ab064e4e7574a42b2efc678beebdcb1af9/UI/IconBrowserUI.cs
// Dalamud IconBrowserWidget: https://github.com/goatcorp/Dalamud/blob/deef16cdd742ca9faa403e388602795e9d3b54e9/Dalamud/Interface/Internal/Windows/Data/Widgets/IconBrowserWidget.cs#L16
public class IconPickerDialogWindow : Window {
    private uint? CurrentIconId { get; set; }
    private string searchText = string.Empty;
    private bool showIconNames = false;
    private Action<uint>? Callback { get; set; }
    private readonly IconPickerIndex iconInfoIndex = new();
    private List<IconInfo> searchedIconInfo = new();
    private IconInfoCategory? _selectedCategory = null;
    private IconInfoCategory? selectedCategory {
        get { return _selectedCategory; }
        set {
            _selectedCategory = value;
            RefreshSearch();
        }
    }

    private float _sidebarWidth = 220f;

    public IconPickerDialogWindow() : base("Icon Picker###IconPIckerDialogWindow") {
        Size = ImGuiHelpers.ScaledVector2(650, 480);
        SizeCondition = ImGuiCond.FirstUseEver;

        Flags = ImGuiWindowFlags.NoScrollbar;
        // SizeConstraints = new WindowSizeConstraints {
        //     MinimumSize = ImGuiHelpers.ScaledVector2(450, 300),
        //     MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        // };

        iconInfoIndex.StartIndexing(() => {
            RefreshSearch();
        });
    }

    private void RefreshSearch() {
        if (searchText == string.Empty) {
            searchedIconInfo = iconInfoIndex.All(selectedCategory);
        } else {
            searchedIconInfo = iconInfoIndex.NameSearch(searchText, selectedCategory);
        }
    }

    public override void OnClose() {
        base.OnClose();
    }

    public void Open(Action<uint> callback) => Open(null, callback);

    public void Open(uint? initialIcon, Action<uint> callback) {
        CurrentIconId = initialIcon;
        Callback = callback;

        if (!IsOpen) {
            IsOpen = true;
        } else {
            ImGui.SetWindowFocus(WindowName);
        }
    }

    public override void Draw() {
        float iconSize = 48 * ImGuiHelpers.GlobalScale;

        DrawCategorySidebar();
        ImGui.SameLine();
        DrawSidebarSplitter();
        ImGui.SameLine();
        DrawContentPane(iconSize);
    }

    private void DrawCategorySidebar() {
        float width = _sidebarWidth * ImGuiHelpers.GlobalScale;
        using var child = ImRaii.Child("Search##CategoryTree", new Vector2(width, -1), true);
        if (!child) return;

        var flags = ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.OpenOnDoubleClick
            | ImGuiTreeNodeFlags.SpanAvailWidth | ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.NoTreePushOnOpen;
        if (selectedCategory == null) { flags |= ImGuiTreeNodeFlags.Selected; }
        ImGui.TreeNodeEx("All", flags);
        if (ImGui.IsItemClicked()) {
            selectedCategory = null;
            ImGui.CloseCurrentPopup();
        }
        foreach (var categoryTree in iconInfoIndex.CategoryRoots()) {
            DrawCategoryGroupTree(categoryTree);
        }
    }

    private void DrawSidebarSplitter() {
        const float minSidebarPx = 120f;
        const float maxSidebarPx = 500f;

        var splitterWidth = 6f * ImGuiHelpers.GlobalScale;
        using (ImRaii.PushStyle(ImGuiStyleVar.FramePadding, Vector2.Zero)) {
            ImGui.InvisibleButton("##CategorySplitter", new Vector2(splitterWidth, -1));

            if (ImGui.IsItemHovered())
                ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeEw);

            if (ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left)) {
                _sidebarWidth += ImGui.GetIO().MouseDelta.X / ImGuiHelpers.GlobalScale;
                _sidebarWidth = MathF.Max(minSidebarPx, MathF.Min(_sidebarWidth, maxSidebarPx));
            }
        }
    }

    private void DrawContentPane(float iconSize) {
        using var rightChild = ImRaii.Child("RightSide##IconPicker", new Vector2(0, -1), false);
        if (!rightChild) return;

        var searchHint = selectedCategory == null ? "Search" : $"Search {selectedCategory}";

        float checkboxWidth = ImGui.CalcTextSize("Show Icon Tags").X + ImGui.GetFrameHeight() + ImGui.GetStyle().ItemSpacing.X * 3;
        float inputWidth = MathF.Max(100f * ImGuiHelpers.GlobalScale, ImGui.GetContentRegionAvail().X - checkboxWidth);

        ImGui.SetNextItemWidth(inputWidth);
        if (ImGui.InputTextWithHint("###iconsearch", searchHint, ref searchText, 255)) {
            RefreshSearch();
        }

        ImGui.SameLine();
        ImGui.Checkbox("Show Icon Tags", ref showIconNames);

        using (var child = ImRaii.Child("Search##IconList", new Vector2(0, -1), false)) {
            if (child) {
                var columns = Math.Max(1, (int)((ImGui.GetContentRegionAvail().X - ImGui.GetStyle().WindowPadding.X) / (iconSize + ImGui.GetStyle().ItemSpacing.X)));
                if (iconInfoIndex.State == IconPickerIndex.IndexState.INDEXED) {
                    DrawSearchResults(iconSize, columns);
                } else {
                    var spinner = "|/-\\"[(int)(ImGui.GetTime() / 0.05f) % 3];
                    ImGui.Text($"Indexing... {spinner}");
                }
            }
        }
    }

    private void DrawCategoryGroupTree(IconInfoCategoryGroup categoryGroup) {
        ImGui.PushID(categoryGroup.Category.ToString());
        var flags = ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.OpenOnDoubleClick | ImGuiTreeNodeFlags.SpanAvailWidth;
        if (selectedCategory == categoryGroup.Category) {
            flags |= ImGuiTreeNodeFlags.Selected;
        }
        if (categoryGroup.SubcategoryIconInfos.Count == 0) {
            flags |= ImGuiTreeNodeFlags.Leaf;
        }

        bool categoryOpen = ImGui.TreeNodeEx(categoryGroup.Category.ToString(), flags);
        if (ImGui.IsItemClicked() && !ImGui.IsItemToggledOpen()) {
            selectedCategory = categoryGroup.Category;
            ImGui.CloseCurrentPopup();
        }
        if (categoryOpen) {
            foreach (var (subcategory, _) in categoryGroup.SubcategoryIconInfos) {
                var subFlags = ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.OpenOnDoubleClick
                    | ImGuiTreeNodeFlags.SpanAvailWidth | ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.NoTreePushOnOpen;
                if (selectedCategory == subcategory) {
                    subFlags |= ImGuiTreeNodeFlags.Selected;
                }

                ImGui.TreeNodeEx(subcategory.ToString(), subFlags);
                if (ImGui.IsItemClicked() && !ImGui.IsItemToggledOpen()) {
                    selectedCategory = subcategory;
                    ImGui.CloseCurrentPopup();
                }
            }
            ImGui.TreePop();
        }
        ImGui.PopID();
    }

    private void DrawSearchResults(float iconSize, int columns) {
        var lineHeight = iconSize + ImGui.GetStyle().ItemSpacing.Y;
        ImGuiClip.ClippedDraw(searchedIconInfo, (namedIcon) => {
            DalamudApi.TextureProvider.DrawIcon(namedIcon.IconId, new Vector2(iconSize));

            if (ImGui.IsItemHovered()) {
                ImGui.BeginTooltip();

                if (ImGui.IsMouseDown(ImGuiMouseButton.Right)) {
                    // Icon Preview
                    DalamudApi.TextureProvider.DrawIcon(namedIcon.IconId, new Vector2(300 * ImGuiHelpers.GlobalScale));
                } else {
                    // Icon Details
                    ImGui.Text($"Icon Id: {namedIcon.IconId}");
                    ImGui.Text($"Middle-Click to open in browser");

                    if (showIconNames) {
                        var columns = 3;
                        var currentColumn = 0;
                        foreach (var name in namedIcon.Names) {
                            ImGui.Text(name);
                            if (currentColumn > columns) {
                                currentColumn = 0;
                            } else {
                                ImGui.SameLine();
                                currentColumn += 1;
                            }
                        }
                    }
                }
                ImGui.EndTooltip();
            }
            if (ImGui.IsItemClicked() && Callback != null) {
                CurrentIconId = namedIcon.IconId;
                Callback(namedIcon.IconId);
                this.Callback = null;
                this.IsOpen = false;
            }
            if (ImGui.IsItemClicked(ImGuiMouseButton.Middle)) {
                WindowsApi.OpenUrl(TextureExporter.GetXivApiIconAssetUrl(namedIcon.IconId));
            }

        }, columns, lineHeight);
    }
}
