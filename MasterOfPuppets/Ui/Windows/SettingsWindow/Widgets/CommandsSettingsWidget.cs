using System;
using System.Collections.Generic;
using System.Linq;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

using MasterOfPuppets.Util.ImGuiExt;

namespace MasterOfPuppets;

public class CommandsSettingsWidget : Widget {
    public override string Title => "Commands";
    public override FontAwesomeIcon Icon => FontAwesomeIcon.Terminal;

    // commandKey → { defaultAlias → current input text }
    private readonly Dictionary<string, Dictionary<string, string>> _aliasInputs = new();

    public CommandsSettingsWidget(WidgetContext ctx) : base(ctx) {
    }

    public override void OnShow() {
        var Plugin = Context.Plugin;
        foreach (var def in PluginCommandManager.Definitions) {
            _aliasInputs[def.Key] = new();
            foreach (var alias in def.DefaultAliases) {
                if (alias.Equals(def.DefaultCommand, StringComparison.OrdinalIgnoreCase)) continue;
                _aliasInputs[def.Key][alias] = GetEffectiveAliasName(Plugin, def.Key, alias);
            }
        }
        base.OnShow();
    }

    public override void Draw() {
        var Plugin = Context.Plugin;

        ImGui.TextWrapped("Enable and rename aliases for built-in commands. Changes are synced to all clients.");
        ImGuiUtil.HelpMarker("Example: enable '/br' as alias for '/mopbr', or rename it to '/broadcast'.");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        foreach (var def in PluginCommandManager.Definitions) {
            var visibleAliases = def.DefaultAliases
                .Where(a => !a.Equals(def.DefaultCommand, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (visibleAliases.Length == 0) continue;

            ImGui.TextDisabled(def.DefaultCommand);

            Plugin.Config.EnabledCommandAliases.TryGetValue(def.Key, out var enabledAliases);
            if (!_aliasInputs.ContainsKey(def.Key)) _aliasInputs[def.Key] = new();

            foreach (var alias in visibleAliases) {
                ImGui.Indent(16);

                bool enabled = enabledAliases != null && enabledAliases.Contains(alias, StringComparer.OrdinalIgnoreCase);
                if (ImGui.Checkbox($"##chk_{def.Key}_{alias}", ref enabled)) {
                    if (!Plugin.Config.EnabledCommandAliases.ContainsKey(def.Key))
                        Plugin.Config.EnabledCommandAliases[def.Key] = new();
                    if (enabled)
                        Plugin.Config.EnabledCommandAliases[def.Key].Add(alias);
                    else
                        Plugin.Config.EnabledCommandAliases[def.Key].Remove(alias);
                    SaveAndRefreshCommands(Plugin);
                }

                ImGui.SameLine();

                if (!_aliasInputs[def.Key].TryGetValue(alias, out var aliasInput))
                    aliasInput = _aliasInputs[def.Key][alias] = GetEffectiveAliasName(Plugin, def.Key, alias);

                string stored = GetEffectiveAliasName(Plugin, def.Key, alias);
                bool inputDiffers = !aliasInput.Equals(stored, StringComparison.OrdinalIgnoreCase);
                bool isValid = aliasInput.StartsWith('/') && aliasInput.Length >= 2 && !aliasInput.Contains(' ');

                ImGui.SetNextItemWidth(120 * ImGuiHelpers.GlobalScale);
                if (ImGui.InputText($"##inp_{def.Key}_{alias}", ref aliasInput, 64))
                    _aliasInputs[def.Key][alias] = aliasInput;

                ImGui.SameLine();
                using (ImRaii.Disabled(!inputDiffers || !isValid)) {
                    if (ImGui.Button($"Apply##aliasApply_{def.Key}_{alias}")) {
                        if (!Plugin.Config.CustomAliasNames.ContainsKey(def.Key))
                            Plugin.Config.CustomAliasNames[def.Key] = new();
                        Plugin.Config.CustomAliasNames[def.Key][alias] = aliasInput.Trim().ToLowerInvariant();
                        SaveAndRefreshCommands(Plugin);
                    }
                }

                bool hasCustom = Plugin.Config.CustomAliasNames.TryGetValue(def.Key, out var cn) && cn.ContainsKey(alias);
                ImGui.SameLine();
                using (ImRaii.Disabled(!hasCustom)) {
                    if (ImGui.Button($"Reset##aliasReset_{def.Key}_{alias}")) {
                        Plugin.Config.CustomAliasNames.TryGetValue(def.Key, out var rn);
                        rn?.Remove(alias);
                        _aliasInputs[def.Key][alias] = alias;
                        SaveAndRefreshCommands(Plugin);
                    }
                }

                ImGui.Unindent(16);
            }

            ImGui.Spacing();
        }
    }

    private string GetEffectiveAliasName(Plugin Plugin, string key, string defaultAlias) {
        if (Plugin.Config.CustomAliasNames.TryGetValue(key, out var names) &&
            names.TryGetValue(defaultAlias, out var custom) &&
            !string.IsNullOrWhiteSpace(custom))
            return custom;
        return defaultAlias;
    }

    private void SaveAndRefreshCommands(Plugin Plugin) {
        Plugin.Config.Save();
        Plugin.IpcProvider.SyncConfiguration();
        Plugin.IpcProvider.RefreshCommands();
    }
}
