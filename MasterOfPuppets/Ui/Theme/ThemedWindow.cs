using System;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace MasterOfPuppets;

/// <summary>
/// Window that pushes the active <see cref="UiTheme"/> for its whole draw (PreDraw → PostDraw),
/// so child windows / popups opened inside Draw inherit it too.
/// Overrides of PreDraw/PostDraw must call the base implementation.
/// </summary>
public abstract class ThemedWindow : Window {
    private IDisposable? _themeScope;

    /// <summary>Set to false to keep the Dalamud style on a specific window.</summary>
    public bool ApplyTheme { get; set; } = true;

    protected ThemedWindow(string name, ImGuiWindowFlags flags = ImGuiWindowFlags.None, bool forceMainWindow = false)
        : base(name, flags, forceMainWindow) { }

    public override void PreDraw() {
        _themeScope?.Dispose();
        _themeScope = ApplyTheme ? ThemeManager.PushCurrent() : null;
        base.PreDraw();
    }

    public override void PostDraw() {
        _themeScope?.Dispose();
        _themeScope = null;
        base.PostDraw();
    }
}
