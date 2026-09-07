using System;
using System.Linq;
using System.Numerics;

using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;

namespace MasterOfPuppets.Formations;

public sealed record FormationResolvedAnchor(
    Vector3 Position,
    float Rotation,
    ulong? ContentId = null,
    string Name = "",
    ulong? GameObjectId = null,
    IGameObject? Actor = null);

public static class FormationAnchorResolver {
    public static bool TryResolve(
        Plugin plugin,
        Formation formation,
        FormationAnchorReference anchor,
        out FormationResolvedAnchor resolved,
        out string failureReason,
        out FormationAnchorFailureKind failureKind) {
        resolved = new FormationResolvedAnchor(default, default);
        failureReason = string.Empty;
        failureKind = FormationAnchorFailureKind.None;

        var player = DalamudApi.ObjectTable.LocalPlayer;
        if (player == null) {
            failureReason = "local player is unavailable";
            failureKind = FormationAnchorFailureKind.ConfigurationError;
            return false;
        }

        switch (anchor.Kind) {
            case FormationAnchorKind.Default:
                return TryResolvePointOneAnchor(plugin, formation, out resolved, out failureReason, out failureKind);
            case FormationAnchorKind.Self:
                resolved = new FormationResolvedAnchor(
                    player.Position,
                    player.Rotation,
                    DalamudApi.PlayerState.ContentId,
                    GetLocalPlayerNameWorld(),
                    player.GameObjectId,
                    player);
                return true;
            case FormationAnchorKind.Sender:
                if (string.IsNullOrWhiteSpace(anchor.Name)) {
                    failureReason = "sender name is empty";
                    failureKind = FormationAnchorFailureKind.AnchorNameEmpty;
                    return false;
                }

                if (!TryResolveNamed(anchor.Name, out resolved, out failureReason, out failureKind))
                    return false;

                resolved = resolved with { ContentId = ResolveContentIdFromName(plugin, resolved.Name) };
                return true;
            case FormationAnchorKind.Target: {
                var target = DalamudApi.TargetManager.SoftTarget
                    ?? DalamudApi.TargetManager.Target
                    ?? player.TargetObject;
                if (target == null) {
                    failureReason = "no target selected";
                    failureKind = FormationAnchorFailureKind.NoTargetSelected;
                    return false;
                }

                resolved = new FormationResolvedAnchor(
                    target.Position,
                    target.Rotation,
                    null,
                    target.Name.TextValue,
                    target.GameObjectId,
                    target);
                return true;
            }
            case FormationAnchorKind.FocusTarget:
                var focusTarget = DalamudApi.TargetManager.FocusTarget;
                if (focusTarget == null) {
                    failureReason = "no focus target selected";
                    failureKind = FormationAnchorFailureKind.NoFocusTargetSelected;
                    return false;
                }

                resolved = new FormationResolvedAnchor(
                    focusTarget.Position,
                    focusTarget.Rotation,
                    null,
                    focusTarget.Name.TextValue,
                    focusTarget.GameObjectId,
                    focusTarget);
                return true;
            case FormationAnchorKind.Named:
                if (string.Equals(anchor.Name, "<t>", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(anchor.Name, "[t]", StringComparison.OrdinalIgnoreCase)) {
                    var namedTarget = DalamudApi.TargetManager.SoftTarget
                        ?? DalamudApi.TargetManager.Target
                        ?? player.TargetObject;
                    if (namedTarget == null) {
                        failureReason = "no target selected";
                        failureKind = FormationAnchorFailureKind.NoTargetSelected;
                        return false;
                    }

                    resolved = new FormationResolvedAnchor(
                        namedTarget.Position,
                        namedTarget.Rotation,
                        null,
                        namedTarget.Name.TextValue,
                        namedTarget.GameObjectId,
                        namedTarget);
                    return true;
                }

                if (string.Equals(anchor.Name, "<f>", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(anchor.Name, "[f]", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(anchor.Name, "<focus>", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(anchor.Name, "[focus]", StringComparison.OrdinalIgnoreCase)) {
                    var namedFocus = DalamudApi.TargetManager.FocusTarget;
                    if (namedFocus == null) {
                        failureReason = "no focus target selected";
                        failureKind = FormationAnchorFailureKind.NoFocusTargetSelected;
                        return false;
                    }

                    resolved = new FormationResolvedAnchor(
                        namedFocus.Position,
                        namedFocus.Rotation,
                        null,
                        namedFocus.Name.TextValue,
                        namedFocus.GameObjectId,
                        namedFocus);
                    return true;
                }

                if (!TryResolveNamed(anchor.Name ?? string.Empty, out resolved, out failureReason, out failureKind))
                    return false;

                resolved = resolved with { ContentId = ResolveContentIdFromName(plugin, resolved.Name) };
                return true;
            default:
                failureReason = $"unsupported anchor kind {anchor.Kind}";
                failureKind = FormationAnchorFailureKind.Unsupported;
                return false;
        }
    }

    public static bool TryResolveNamed(
        string objectName,
        out FormationResolvedAnchor resolved,
        out string failureReason,
        out FormationAnchorFailureKind failureKind) {
        resolved = new FormationResolvedAnchor(default, default);
        failureReason = string.Empty;
        failureKind = FormationAnchorFailureKind.None;

        objectName = FormationCharacterName.NormalizeWorldSeparator(objectName);
        if (string.IsNullOrWhiteSpace(objectName)) {
            failureReason = "anchor name is empty";
            failureKind = FormationAnchorFailureKind.AnchorNameEmpty;
            return false;
        }

        var candidates = DalamudApi.ObjectTable
            .Where(actor => actor is { Address: not 0 } && actor.Name.TextValue.Length > 0)
            .Select(actor => {
                var name = actor!.Name.TextValue;
                var fullName = name;
                if (actor.ObjectKind == ObjectKind.Pc &&
                    actor is IPlayerCharacter player &&
                    player.HomeWorld.ValueNullable is { } world)
                    fullName = $"{name}@{world.Name}";

                return new AnchorCandidate(
                    actor,
                    FormationCharacterName.NormalizeWorldSeparator(name),
                    FormationCharacterName.NormalizeWorldSeparator(fullName));
            })
            .ToList();

        if (ulong.TryParse(objectName, out var actorId)) {
            var idMatch = candidates.FirstOrDefault(candidate =>
                candidate.Actor.GameObjectId == actorId || candidate.Actor.EntityId == actorId);
            if (idMatch != null) {
                resolved = ToResolved(idMatch);
                return true;
            }
        }

        var matches = candidates
            .Where(candidate => candidate.FullName.Equals(objectName, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (matches.Length == 0) {
            matches = candidates
                .Where(candidate => candidate.Name.Equals(objectName, StringComparison.OrdinalIgnoreCase))
                .ToArray();
        }
        if (matches.Length == 0) {
            matches = candidates
                .Where(candidate => candidate.FullName.Contains(objectName, StringComparison.OrdinalIgnoreCase)
                    || candidate.Name.Contains(objectName, StringComparison.OrdinalIgnoreCase))
                .ToArray();
        }

        if (matches.Length == 0) {
            failureReason = $"anchor not visible: \"{objectName}\"";
            failureKind = FormationAnchorFailureKind.AnchorNotVisible;
            return false;
        }
        if (matches.Length > 1) {
            failureReason = $"anchor is ambiguous: \"{objectName}\" matches {matches.Length} visible objects; use Name@World or a live actor ID";
            failureKind = FormationAnchorFailureKind.AnchorNotVisible;
            return false;
        }

        resolved = ToResolved(matches[0]);
        return true;
    }

    private static bool TryResolvePointOneAnchor(
        Plugin plugin,
        Formation formation,
        out FormationResolvedAnchor resolved,
        out string failureReason,
        out FormationAnchorFailureKind failureKind) {
        resolved = new FormationResolvedAnchor(default, default);
        failureKind = FormationAnchorFailureKind.None;
        if (!FormationPointMovement.TryGetPointOneAnchorCid(formation, plugin.Config.CidsGroups, out var anchorCid, out failureReason)) {
            failureKind = FormationAnchorFailureKind.ConfigurationError;
            return false;
        }

        if (anchorCid == DalamudApi.PlayerState.ContentId) {
            var player = DalamudApi.ObjectTable.LocalPlayer;
            if (player == null) {
                failureReason = "local point-1 anchor is unavailable";
                failureKind = FormationAnchorFailureKind.ConfigurationError;
                return false;
            }

            resolved = new FormationResolvedAnchor(player.Position, player.Rotation, anchorCid, GetLocalPlayerNameWorld(), player.GameObjectId, player);
            return true;
        }

        var configuredName = plugin.Config.Characters.FirstOrDefault(character => character.Cid == anchorCid)?.Name;
        if (string.IsNullOrWhiteSpace(configuredName)) {
            failureReason = $"point 1 character {anchorCid} is not configured";
            failureKind = FormationAnchorFailureKind.ConfigurationError;
            return false;
        }

        if (TryResolveNamed(configuredName, out resolved, out failureReason, out failureKind)) {
            resolved = resolved with { ContentId = anchorCid };
            return true;
        }

        return false;
    }

    private static string GetLocalPlayerNameWorld() {
        var name = FormationCharacterName.NormalizeWorldSeparator(DalamudApi.PlayerState.CharacterName);
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        var world = FormationCharacterName.NormalizeWorldSeparator(DalamudApi.PlayerState.HomeWorld.Value.Name.ToString());
        return string.IsNullOrWhiteSpace(world) ? name : $"{name}@{world}";
    }

    private static ulong? ResolveContentIdFromName(Plugin plugin, string actorFullName) {
        actorFullName = FormationCharacterName.NormalizeWorldSeparator(actorFullName);
        if (string.IsNullOrWhiteSpace(actorFullName))
            return null;

        var matches = plugin.Config.Characters
            .Where(character => character.Cid != 0
                && FormationCharacterName.Matches(character.Name, actorFullName))
            .Select(character => character.Cid)
            .Distinct()
            .Take(2)
            .ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }

    private static FormationResolvedAnchor ToResolved(AnchorCandidate match) => new(
        match.Actor.Position,
        match.Actor.Rotation,
        null,
        match.FullName,
        match.Actor.GameObjectId,
        match.Actor);

    private sealed record AnchorCandidate(IGameObject Actor, string Name, string FullName);
}
