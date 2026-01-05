
using System;
using System.Linq;

using MasterOfPuppets.Util.ImGuiExt;

namespace MasterOfPuppets.Debug;

public sealed class ConflictingPluginDebugWidget : Widget {
    public override string Title => "Conflicting Plugin";

    public ConflictingPluginDebugWidget(WidgetContext ctx) : base(ctx) {
    }

    public override void Draw() {

        var conflictPluginName = GetConflictingPluginName();
        if (!string.IsNullOrEmpty(conflictPluginName))
            ImGuiUtil.DrawColoredBanner($"Conflicting Plugin Detected: {conflictPluginName}", Style.Colors.Red);
    }

    public string? GetConflictingPluginName() {
        var conflictingPluginNames = new[] { "WrathCombo", "RotationSolver", "BossMod" };

        var plugin = DalamudApi.PluginInterface.InstalledPlugins
            .FirstOrDefault(p =>
                p.IsLoaded &&
                conflictingPluginNames.Contains(p.InternalName, StringComparer.OrdinalIgnoreCase));

        return plugin?.InternalName;
    }
}

