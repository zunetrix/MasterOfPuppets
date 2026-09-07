using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;

using MasterOfPuppets.LuaScripting;
using MasterOfPuppets.LuaScripting.Runs;
using MasterOfPuppets.LuaScripting.Runtime;
using MasterOfPuppets.Extensions.Dalamud;
using MasterOfPuppets.Resources;
using MasterOfPuppets.Util.ImGuiExt;

namespace MasterOfPuppets;

public sealed class LuaScriptEditorWindow : Window {
    private readonly Plugin _plugin;
    private readonly PluginUi _ui;
    private readonly ImGuiComboSearch _tagCombo = new();
    private List<string> _availableTags = new();
    private LuaScriptDefinition _script = NewDefinition();
    private int _scriptIndex = -1;
    private bool _editingExisting;
    private string _tagName = string.Empty;
    private string _tagSelected = string.Empty;
    private float _rightPanelWidth = 300f;
    private readonly IReadOnlyList<LuaCapabilityDescriptor> _availableCapabilities = LuaCapabilityRegistry.Discover().Descriptors;

    public LuaScriptEditorWindow(Plugin plugin, PluginUi ui)
        : base($"{Plugin.Name} Script Editor###LuaScriptEditorWindow") {
        _plugin = plugin;
        _ui = ui;
        Size = ImGuiHelpers.ScaledVector2(880, 680);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints {
            MinimumSize = ImGuiHelpers.ScaledVector2(650, 460),
        };
    }

    public override void PreDraw() {
        Flags = ImGuiWindowFlags.None;
        if (!_plugin.Config.AllowMovement) Flags |= ImGuiWindowFlags.NoMove;
        if (!_plugin.Config.AllowResize) Flags |= ImGuiWindowFlags.NoResize;
        base.PreDraw();
    }

    public override void OnClose() {
        ResetState();
        base.OnClose();
    }

    public void AddNewScript() {
        ResetState();
        _script.Name = UniqueName("New Lua Script");
        RefreshAvailableTags();
        IsOpen = true;
    }

    public void EditScript(int scriptIndex) {
        if (scriptIndex < 0 || scriptIndex >= _plugin.Config.LuaScripts.Count)
            return;

        ResetState();
        _script = _plugin.Config.LuaScripts[scriptIndex].Clone();
        _scriptIndex = scriptIndex;
        _editingExisting = true;
        RefreshAvailableTags();
        IsOpen = true;
    }

    public override void Draw() {
        DrawHeader();
        ImGui.Separator();
        ImGui.Spacing();
        DrawMainArea();
    }

    private void DrawHeader() {
        ImGui.TextUnformatted("Script Name");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(MathF.Max(220f * ImGuiHelpers.GlobalScale, ImGui.GetContentRegionAvail().X - 95f * ImGuiHelpers.GlobalScale));
        var name = _script.Name;
        if (ImGui.InputText("##LuaScriptName", ref name, 100))
            _script.Name = name;

        ImGui.SameLine();
        using (ImRaii.PushColor(ImGuiCol.Button, Style.Components.ButtonSuccessNormal)
            .Push(ImGuiCol.ButtonHovered, Style.Components.ButtonSuccessHovered)
            .Push(ImGuiCol.ButtonActive, Style.Components.ButtonSuccessActive)) {
            if (ImGuiUtil.IconButton(FontAwesomeIcon.Save, "##SaveLuaScript", "Save script") && Save())
                IsOpen = false;
        }

        ImGui.SameLine();
        using (ImRaii.PushColor(ImGuiCol.Button, Style.Components.ButtonDangerNormal)
            .Push(ImGuiCol.ButtonHovered, Style.Components.ButtonDangerHovered)
            .Push(ImGuiCol.ButtonActive, Style.Components.ButtonDangerActive)) {
            if (ImGui.Button("Cancel##CancelLuaScript"))
                IsOpen = false;
        }
    }

    private void DrawMainArea() {
        var available = ImGui.GetContentRegionAvail();
        var splitterWidth = 6f * ImGuiHelpers.GlobalScale;
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var minRightWidth = 240f * ImGuiHelpers.GlobalScale;
        var maxRightWidth = MathF.Max(minRightWidth, available.X - 300f * ImGuiHelpers.GlobalScale);
        _rightPanelWidth = Math.Clamp(_rightPanelWidth, minRightWidth, maxRightWidth);
        var leftWidth = available.X - _rightPanelWidth - splitterWidth - spacing * 2f;

        using (ImRaii.Child("##LuaScriptEditorMain", new Vector2(0, 0), false)) {
            using (ImRaii.Child("##LuaScriptEditorCode", new Vector2(leftWidth, 0), false))
                DrawCodePanel();

            ImGui.SameLine();
            using (ImRaii.PushStyle(ImGuiStyleVar.FramePadding, Vector2.Zero)) {
                ImGui.InvisibleButton("##LuaScriptEditorSplitter", new Vector2(splitterWidth, -1));
                if (ImGui.IsItemHovered())
                    ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeEw);
                if (ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left))
                    _rightPanelWidth -= ImGui.GetIO().MouseDelta.X;
            }

            ImGui.SameLine();
            using (ImRaii.Child("##LuaScriptEditorDetails", new Vector2(_rightPanelWidth, 0), true))
                DrawDetailsPanel();
        }
    }

    private void DrawCodePanel() {
        ImGui.TextUnformatted("Variables");
        ImGui.SetNextItemWidth(-1);
        var variables = _script.Variables;
        if (ImGui.InputTextMultiline("##LuaScriptVariables", ref variables, 4000,
                ImGuiHelpers.ScaledVector2(-1, 70)))
            _script.Variables = variables;
        ImGui.TextDisabled("Same launch-variable syntax as macros: $speed = 1.15");

        ImGui.Spacing();
        if (ImGui.CollapsingHeader($"Bundle Modules ({_script.Modules?.Count ?? 0})"))
            DrawModules();

        ImGui.Spacing();
        ImGui.TextUnformatted("Description");
        ImGui.SetNextItemWidth(-1);
        var description = _script.Description;
        if (ImGui.InputTextMultiline("##LuaScriptDescription", ref description, 1000,
                ImGuiHelpers.ScaledVector2(-1, 70)))
            _script.Description = description;

        ImGui.Spacing();
        ImGui.TextUnformatted("Lua Code");
        ImGui.SameLine();
        ImGui.TextDisabled($"SHA-256: {_script.Hash[..12]}");
        var codeHeight = MathF.Max(180f * ImGuiHelpers.GlobalScale, ImGui.GetContentRegionAvail().Y - 42f * ImGuiHelpers.GlobalScale);
        var source = _script.Source;
        if (ImGui.InputTextMultiline("##LuaScriptSource", ref source, LuaScriptDefinition.MaximumSourceLength,
                new Vector2(-1, codeHeight), ImGuiInputTextFlags.AllowTabInput))
            _script.Source = source;

        ImGui.TextDisabled("Read launch variables in Lua with mop.get_var(\"name\").");
    }

    private void DrawDetailsPanel() {
        ImGui.TextUnformatted("Details");
        ImGui.Separator();

        if (ImGui.CollapsingHeader("Participants", ImGuiTreeNodeFlags.DefaultOpen))
            DrawParticipants();

        if (ImGui.CollapsingHeader("Automation Contract", ImGuiTreeNodeFlags.DefaultOpen))
            DrawAutomationContract();

        if (ImGui.CollapsingHeader("Typed Parameters"))
            DrawParameters();

        if (ImGui.CollapsingHeader("Appearance", ImGuiTreeNodeFlags.DefaultOpen))
            DrawAppearance();

        if (ImGui.CollapsingHeader("Tags", ImGuiTreeNodeFlags.DefaultOpen))
            DrawTags();

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.TextWrapped("Commands are the primary launch method. The Scripts list also includes a local Run button for convenience.");
        ImGui.Spacing();
        ImGui.TextDisabled($"Local: /mop lua run \"{_script.Name}\" -var=$anchor=\"[t]\"");
        var chatPrefix = string.IsNullOrWhiteSpace(_plugin.Config.DefaultChatSyncPrefix)
            ? "/p"
            : _plugin.Config.DefaultChatSyncPrefix.Trim();
        ImGui.TextDisabled($"Cross-PC: {chatPrefix} mopluarun \"{_script.Name}\" -var=$anchor=\"<t>\"");
    }

    private void DrawModules() {
        _script.Modules ??= new Dictionary<string, string>(StringComparer.Ordinal);
        if (ImGui.Button("Add module##LuaModule")) {
            var name = "module";
            var suffix = 2;
            while (_script.Modules.ContainsKey(name))
                name = $"module{suffix++}";
            _script.Modules[name] = "return {}";
        }

        string? remove = null;
        foreach (var originalName in _script.Modules.Keys.OrderBy(name => name, StringComparer.Ordinal).ToArray()) {
            ImGui.PushID(originalName);
            var name = originalName;
            ImGui.SetNextItemWidth(MathF.Max(120f, ImGui.GetContentRegionAvail().X - 90f * ImGuiHelpers.GlobalScale));
            if (ImGui.InputText("##ModuleName", ref name, 128)
                && !string.IsNullOrWhiteSpace(name)
                && !_script.Modules.ContainsKey(name)) {
                var value = _script.Modules[originalName];
                _script.Modules.Remove(originalName);
                _script.Modules[name] = value;
            }
            ImGui.SameLine();
            if (ImGui.Button("Remove##Module"))
                remove = name;
            var source = _script.Modules.GetValueOrDefault(name) ?? string.Empty;
            ImGui.SetNextItemWidth(-1);
            if (ImGui.InputTextMultiline(
                    "##ModuleSource",
                    ref source,
                    LuaModuleManifest.MaximumModuleSourceLength,
                    ImGuiHelpers.ScaledVector2(-1, 120),
                    ImGuiInputTextFlags.AllowTabInput))
                _script.Modules[name] = source;
            ImGui.PopID();
        }
        if (remove != null)
            _script.Modules.Remove(remove);
        ImGui.TextDisabled("require(\"name\") loads only modules stored in this bundle.");
    }

    private void DrawParticipants() {
        ImGui.TextUnformatted("Participant Formation");
        var preview = string.IsNullOrWhiteSpace(_script.ParticipantFormation)
            ? "Select a formation"
            : _script.ParticipantFormation;
        ImGui.SetNextItemWidth(-1);
        if (ImGui.BeginCombo("##LuaParticipantFormation", preview)) {
            if (ImGui.Selectable("None", string.IsNullOrWhiteSpace(_script.ParticipantFormation)))
                _script.ParticipantFormation = string.Empty;
            foreach (var formation in _plugin.Config.Formations.OrderBy(formation => formation.Name)) {
                var selected = formation.Name.Equals(_script.ParticipantFormation, StringComparison.OrdinalIgnoreCase);
                if (ImGui.Selectable(formation.Name, selected))
                    _script.ParticipantFormation = formation.Name;
            }
            ImGui.EndCombo();
        }
        ImGui.TextWrapped("Formation point order defines Lua slot order. Unassigned characters ignore the launch.");
    }

    private void DrawAppearance() {
        ImGui.TextUnformatted("Color");
        ImGui.PushItemWidth(MathF.Max(120f, ImGui.GetContentRegionAvail().X - ImGui.GetFrameHeight() - ImGui.GetStyle().ItemSpacing.X));
        _script.Color = ImGuiComponents.ColorPickerWithPalette(1, "##LuaScriptColor", _script.Color);
        ImGui.PopItemWidth();
        ImGui.SameLine();
        if (ImGuiUtil.IconButton(FontAwesomeIcon.Undo, "##ResetLuaScriptColor", "Reset color"))
            _script.Color = Style.Colors.White;

        ImGui.Spacing();
        ImGui.TextUnformatted("Icon");
        var iconSize = ImGuiHelpers.ScaledVector2(36, 36);
        var position = ImGui.GetCursorScreenPos();
        DalamudApi.TextureProvider.DrawIcon(_script.IconId, iconSize);
        ImGui.GetWindowDrawList().AddRect(
            position,
            position + iconSize,
            ImGui.ColorConvertFloat4ToU32(Style.Components.TooltipBorderColor));
        if (ImGui.IsItemClicked())
            _ui.IconPickerDialogWindow.Open(_script.IconId, iconId => _script.IconId = iconId);
        ImGuiUtil.ToolTip("Click to select an icon");
        ImGui.Spacing();
    }

    private void DrawAutomationContract() {
        ImGui.TextDisabled($"Schema {_script.SchemaVersion}, revision {_script.Revision}");
        if (!string.IsNullOrWhiteSpace(_script.Id))
            ImGui.TextDisabled(_script.Id);

        var compatibilityMode = _script.DeclaredCapabilities == null || _script.DeclaredCapabilities.Count == 0;
        if (ImGui.Checkbox("Legacy API compatibility", ref compatibilityMode)) {
            _script.DeclaredCapabilities = compatibilityMode ? [] : ["mop.runtime"];
        }
        ImGui.TextWrapped("Compatibility mode exposes every installed provider. V2 mode exposes only checked providers plus mop.runtime.");
        if (!compatibilityMode) {
            _script.DeclaredCapabilities ??= [];
            foreach (var capability in _availableCapabilities) {
                var selected = _script.DeclaredCapabilities.Contains(capability.Name, StringComparer.OrdinalIgnoreCase);
                var mandatory = capability.Name.Equals("mop.runtime", StringComparison.OrdinalIgnoreCase);
                if (mandatory)
                    selected = true;
                ImGui.BeginDisabled(mandatory);
                if (ImGui.Checkbox($"{capability.Name}##LuaCapability", ref selected)) {
                    _script.DeclaredCapabilities.RemoveAll(value => value.Equals(capability.Name, StringComparison.OrdinalIgnoreCase));
                    if (selected)
                        _script.DeclaredCapabilities.Add(capability.Name);
                }
                ImGui.EndDisabled();
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(capability.Description);
            }
        }

        ImGui.Spacing();
        var legacyResources = !_script.RequiredResources.HasValue;
        if (ImGui.Checkbox("Legacy exclusive resources", ref legacyResources))
            _script.RequiredResources = legacyResources ? null : LuaResourceKind.None;
        if (legacyResources) {
            ImGui.TextWrapped("Claims movement, chat/actions, macro queue, formation tracking, and synchronized control for compatibility.");
            return;
        }

        var resources = _script.RequiredResources ?? LuaResourceKind.None;
        foreach (var resource in Enum.GetValues<LuaResourceKind>().Where(value => value != LuaResourceKind.None)) {
            var selected = (resources & resource) != 0;
            if (!ImGui.Checkbox($"{resource}##LuaResource", ref selected))
                continue;
            resources = selected ? resources | resource : resources & ~resource;
        }
        _script.RequiredResources = resources;
    }

    private void DrawParameters() {
        _script.Parameters ??= [];
        if (ImGui.Button("Add parameter##LuaParameter"))
            _script.Parameters.Add(new LuaScriptParameterDefinition { Name = UniqueParameterName("parameter") });
        var deleteIndex = -1;
        for (var index = 0; index < _script.Parameters.Count; index++) {
            var parameter = _script.Parameters[index];
            ImGui.PushID(index);
            var label = string.IsNullOrWhiteSpace(parameter.Name) ? $"Parameter {index + 1}" : parameter.Name;
            if (ImGui.TreeNodeEx($"{label}##Parameter", ImGuiTreeNodeFlags.DefaultOpen)) {
                var name = parameter.Name;
                ImGui.SetNextItemWidth(-1);
                if (ImGui.InputTextWithHint("##Name", "name", ref name, 64))
                    parameter.Name = name;
                var types = new[] { "string", "number", "integer", "boolean" };
                ImGui.SetNextItemWidth(-1);
                if (ImGui.BeginCombo("##Type", parameter.Type)) {
                    foreach (var type in types)
                        if (ImGui.Selectable(type, type == parameter.Type))
                            parameter.Type = type;
                    ImGui.EndCombo();
                }
                var defaultValue = parameter.DefaultValue;
                ImGui.SetNextItemWidth(-1);
                if (ImGui.InputTextWithHint("##Default", "default value", ref defaultValue, 1000))
                    parameter.DefaultValue = defaultValue;
                var description = parameter.Description;
                ImGui.SetNextItemWidth(-1);
                if (ImGui.InputTextWithHint("##Description", "description", ref description, 500))
                    parameter.Description = description;
                var required = parameter.Required;
                if (ImGui.Checkbox("Required", ref required))
                    parameter.Required = required;
                if (parameter.Type is "number" or "integer") {
                    var hasMinimum = parameter.Minimum.HasValue;
                    if (ImGui.Checkbox("Minimum", ref hasMinimum))
                        parameter.Minimum = hasMinimum ? 0 : null;
                    if (hasMinimum) {
                        var minimum = parameter.Minimum ?? 0;
                        ImGui.SetNextItemWidth(-1);
                        if (ImGui.InputDouble("##MinimumValue", ref minimum))
                            parameter.Minimum = minimum;
                    }
                    var hasMaximum = parameter.Maximum.HasValue;
                    if (ImGui.Checkbox("Maximum", ref hasMaximum))
                        parameter.Maximum = hasMaximum ? 1 : null;
                    if (hasMaximum) {
                        var maximum = parameter.Maximum ?? 1;
                        ImGui.SetNextItemWidth(-1);
                        if (ImGui.InputDouble("##MaximumValue", ref maximum))
                            parameter.Maximum = maximum;
                    }
                }
                if (ImGui.Button("Remove"))
                    deleteIndex = index;
                ImGui.TreePop();
            }
            ImGui.PopID();
        }
        if (deleteIndex >= 0)
            _script.Parameters.RemoveAt(deleteIndex);
    }

    private string UniqueParameterName(string prefix) {
        var name = prefix;
        var suffix = 2;
        while (_script.Parameters.Any(parameter => parameter.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            name = $"{prefix}{suffix++}";
        return name;
    }

    private void RefreshAvailableTags() {
        var macroTags = _plugin.MacroManager.GetAllTags();
        var scriptTags = _ui.LuaScriptsWindow.GetAllTags();
        var currentTags = _script.Tags ?? Enumerable.Empty<string>();
        _availableTags = macroTags
            .Union(scriptTags, StringComparer.OrdinalIgnoreCase)
            .Union(currentTags, StringComparer.OrdinalIgnoreCase)
            .OrderBy(t => t, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void DrawTags() {
        ImGui.BeginGroup();
        {
            ImGui.Text("Tag Name");
            ImGui.InputText("##LuaScriptTagName", ref _tagName, 100);

            ImGui.SameLine();
            if (ImGui.Button("Add Tag##AddLuaScriptTag")) {
                if (!string.IsNullOrWhiteSpace(_tagName)) {
                    AddTag(_tagName);
                    _tagName = string.Empty;
                    RefreshAvailableTags();
                }
            }

            ImGui.Spacing();
            ImGui.Spacing();

            using (ImRaii.PushColor(ImGuiCol.Border, Style.Components.TooltipBorderColor))
            using (ImRaii.PushStyle(ImGuiStyleVar.PopupBorderSize, 1)) {
                ImGui.SetNextItemWidth(-1);
                var selectableTags = _availableTags
                    .Except(_script.Tags ?? new List<string>(), StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (_tagCombo.Draw("##LuaScriptTagPicker", selectableTags, ref _tagSelected)) {
                    AddTag(_tagSelected);
                    _tagSelected = string.Empty;
                    RefreshAvailableTags();
                }
            }
        }
        ImGui.EndGroup();

        ImGuiHelpers.ScaledDummy(0, 10);

        {
            var deleteColWidth = ImGui.GetFrameHeight();
            var deleteTagIndex = -1;

            if (ImGui.BeginTable("##LuaScriptTagsTable", 2,
                ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY | ImGuiTableFlags.NoSavedSettings,
                new Vector2(-1, 150))) {
                ImGui.TableSetupScrollFreeze(0, 1);
                ImGui.TableSetupColumn("Tag", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, deleteColWidth);
                ImGui.TableHeadersRow();

                for (var index = 0; index < (_script.Tags?.Count ?? 0); index++) {
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted(_script.Tags![index]);
                    ImGui.TableNextColumn();
                    if (ImGuiUtil.IconButtonStyled(
                            FontAwesomeIcon.Trash,
                            ImGuiUtil.IconButtonStyle.Danger,
                            $"##DeleteLuaScriptTag_{index}",
                            Language.DeleteInstructionTooltip)
                        && ImGui.GetIO().KeyCtrl) {
                        deleteTagIndex = index;
                    }
                }

                ImGui.EndTable();
            }

            if (deleteTagIndex >= 0) {
                _script.Tags?.RemoveAt(deleteTagIndex);
                RefreshAvailableTags();
            }
        }

        ImGui.Spacing();
        ImGui.Spacing();
    }

    private void AddTag(string value) {
        var tag = value.Trim();
        _script.Tags ??= new List<string>();
        if (tag.Length > 0 && !_script.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase))
            _script.Tags.Add(tag);
    }

    private bool Save() {
        try {
            _script.Validate();
            if (_plugin.Config.LuaScripts.Where((_, index) => !_editingExisting || index != _scriptIndex)
                .Any(script => script.Name.Equals(_script.Name, StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException($"A Lua script named '{_script.Name}' already exists.");

            if (_editingExisting) {
                _script.Revision = Math.Max(1, _plugin.Config.LuaScripts[_scriptIndex].Revision) + 1;
                _plugin.Config.LuaScripts[_scriptIndex] = _script.Clone();
            } else
                _plugin.Config.LuaScripts.Add(_script.Clone());

            _plugin.IpcProvider.SyncConfiguration();
            DalamudApi.ShowNotification(
                _editingExisting ? "Script updated" : "Script saved",
                NotificationType.Success,
                4000);
            return true;
        } catch (Exception ex) {
            DalamudApi.ShowNotification(ex.Message, NotificationType.Error, 6000);
            return false;
        }
    }

    private string UniqueName(string requested) {
        var candidate = requested;
        var suffix = 2;
        while (_plugin.Config.LuaScripts.Any(script => script.Name.Equals(candidate, StringComparison.OrdinalIgnoreCase)))
            candidate = $"{requested} {suffix++}";
        return candidate;
    }

    private void ResetState() {
        _script = NewDefinition();
        _scriptIndex = -1;
        _editingExisting = false;
        _tagName = string.Empty;
        _tagSelected = string.Empty;
        RefreshAvailableTags();
    }

    private static LuaScriptDefinition NewDefinition() => new() {
        Source = "mop.log(\"script started\")\n\nwhile mop.is_running() do\n    mop.wait(0.1)\nend\n",
    };
}
