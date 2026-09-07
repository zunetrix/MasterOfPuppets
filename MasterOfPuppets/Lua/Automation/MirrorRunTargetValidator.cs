using System;
using System.Collections.Generic;
using System.Globalization;

using MasterOfPuppets.Formations;

namespace MasterOfPuppets.LuaScripting.Automation;

internal readonly record struct MirrorRunTargetCandidate(
    bool IsPresent,
    bool IsPlayer,
    bool IsLoaded,
    ulong GameObjectId,
    uint EntityId,
    ulong ContentId,
    string Name,
    string ObjectKind);

internal readonly record struct MirrorRunTargetIdentity(
    string Name,
    ulong GameObjectId,
    uint EntityId,
    ulong ContentId,
    bool WasExplicitlySelected,
    string Source);

internal readonly record struct MirrorRunTargetDecision(
    bool Success,
    MirrorRunTargetIdentity Identity,
    string Error);

internal readonly record struct MirrorRunTargetBinding(
    string Name,
    ulong GameObjectId,
    uint EntityId,
    ulong ContentId,
    string Source,
    bool WasExplicitlySelected);

internal static class MirrorRunTargetValidator {
    private const ulong InvalidObjectId = 0xE0000000;
    internal const string LaunchNameKey = "mop_run_target_launch_name";
    internal const string LaunchGameObjectIdKey = "mop_run_target_launch_game_object_id";
    internal const string LaunchEntityIdKey = "mop_run_target_launch_entity_id";
    internal const string LaunchContentIdKey = "mop_run_target_launch_content_id";
    internal const string LaunchSourceKey = "mop_run_target_launch_source";
    internal const string LaunchWasSelectedKey = "mop_run_target_launch_was_selected";

    internal static bool AppliesTo(string? scriptName) =>
        string.Equals(
            scriptName,
            LuaScriptCatalog.MirrorScriptV2Name,
            StringComparison.OrdinalIgnoreCase)
        || string.Equals(
            scriptName,
            LuaScriptCatalog.DefaultMirrorCombatTargetName,
            StringComparison.OrdinalIgnoreCase);

    internal static bool RequiresPlayerRunTarget(string? scriptName) =>
        AppliesTo(scriptName)
        || string.Equals(
            scriptName,
            LuaScriptCatalog.DefaultMirrorTargetJobName,
            StringComparison.OrdinalIgnoreCase)
        || string.Equals(
            scriptName,
            LuaScriptCatalog.DefaultMirrorTargetCombatName,
            StringComparison.OrdinalIgnoreCase)
        || string.Equals(
            scriptName,
            LuaScriptCatalog.DefaultMirrorTargetEmotesName,
            StringComparison.OrdinalIgnoreCase);

    internal static MirrorRunTargetDecision Decide(
        in MirrorRunTargetCandidate selected,
        in MirrorRunTargetCandidate initiator,
        string? scriptName = null) {
        var label = string.IsNullOrWhiteSpace(scriptName)
            ? LuaScriptCatalog.MirrorScriptV2Name
            : scriptName.Trim();
        if (selected.IsPresent) {
            if (!IsValidPlayer(selected)) {
                var kind = string.IsNullOrWhiteSpace(selected.ObjectKind)
                    ? "invalid or unloaded object"
                    : selected.ObjectKind;
                return new MirrorRunTargetDecision(
                    false,
                    default,
                    $"{label} requires a loaded real-player target; the selected {kind} is not eligible. Clear it or select a player.");
            }

            return Accepted(selected, wasExplicitlySelected: true, "selected-player");
        }

        if (!IsValidPlayer(initiator)) {
            return new MirrorRunTargetDecision(
                false,
                default,
                $"{label} could not use the local initiator because the local player is not fully loaded.");
        }

        return Accepted(initiator, wasExplicitlySelected: false, "local-initiator");
    }

    internal static MirrorRunTargetBinding ResolveBinding(
        in MirrorRunTargetIdentity identity,
        string? anchorOverride) {
        if (string.IsNullOrWhiteSpace(anchorOverride)) {
            return new MirrorRunTargetBinding(
                identity.Name,
                identity.GameObjectId,
                identity.EntityId,
                identity.ContentId,
                identity.Source,
                identity.WasExplicitlySelected);
        }

        var anchorName = anchorOverride.Trim();
        var matchesLaunchIdentity = FormationCharacterName.Matches(identity.Name, anchorName);
        return new MirrorRunTargetBinding(
            anchorName,
            matchesLaunchIdentity ? identity.GameObjectId : 0,
            matchesLaunchIdentity ? identity.EntityId : 0,
            matchesLaunchIdentity ? identity.ContentId : 0,
            "anchor-override",
            identity.WasExplicitlySelected);
    }

    internal static void WriteMetadata(
        IDictionary<string, string> variables,
        in MirrorRunTargetBinding binding) {
        ArgumentNullException.ThrowIfNull(variables);
        variables[LaunchNameKey] = binding.Name;
        variables[LaunchGameObjectIdKey] = binding.GameObjectId.ToString(CultureInfo.InvariantCulture);
        variables[LaunchEntityIdKey] = binding.EntityId.ToString(CultureInfo.InvariantCulture);
        variables[LaunchContentIdKey] = binding.ContentId.ToString(CultureInfo.InvariantCulture);
        variables[LaunchSourceKey] = binding.Source;
        variables[LaunchWasSelectedKey] = binding.WasExplicitlySelected ? "true" : "false";
    }

    private static bool IsValidPlayer(in MirrorRunTargetCandidate candidate) =>
        candidate.IsPresent
        && candidate.IsPlayer
        && candidate.IsLoaded
        && candidate.GameObjectId is not (0 or InvalidObjectId)
        && candidate.EntityId is not (0 or (uint)InvalidObjectId)
        && !string.IsNullOrWhiteSpace(candidate.Name);

    private static MirrorRunTargetDecision Accepted(
        in MirrorRunTargetCandidate candidate,
        bool wasExplicitlySelected,
        string source) =>
        new(
            true,
            new MirrorRunTargetIdentity(
                candidate.Name.Trim(),
                candidate.GameObjectId,
                candidate.EntityId,
                candidate.ContentId,
                wasExplicitlySelected,
                source),
            string.Empty);
}
