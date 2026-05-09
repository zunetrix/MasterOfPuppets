using System;
using System.Linq;
using System.Numerics;

using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;

namespace MasterOfPuppets.Formations;

public sealed record FormationResolvedAnchor(Vector3 Position, float Rotation, ulong? ContentId = null, string Name = "");

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
                    GetLocalPlayerNameWorld());
                return true;
            case FormationAnchorKind.Target:
                if (player.TargetObject == null) {
                    failureReason = "no target selected";
                    failureKind = FormationAnchorFailureKind.NoTargetSelected;
                    return false;
                }

                resolved = new FormationResolvedAnchor(
                    player.TargetObject.Position,
                    player.TargetObject.Rotation,
                    null,
                    player.TargetObject.Name.TextValue);
                return true;
            case FormationAnchorKind.Named:
                return TryResolveNamed(anchor.Name ?? string.Empty, out resolved, out failureReason, out failureKind);
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

        if (string.IsNullOrWhiteSpace(objectName)) {
            failureReason = "anchor name is empty";
            failureKind = FormationAnchorFailureKind.AnchorNameEmpty;
            return false;
        }

        var candidates = DalamudApi.ObjectTable
            .Where(actor => actor != null && actor.Name.TextValue.Length > 0)
            .Select(actor => {
                var name = actor!.Name.TextValue;
                var fullName = name;
                if (actor.ObjectKind == ObjectKind.Pc &&
                    actor is IPlayerCharacter player &&
                    player.HomeWorld.ValueNullable is { } world)
                    fullName = $"{name}@{world.Name}";

                return new AnchorCandidate(actor, name, fullName);
            })
            .ToList();

        AnchorCandidate? match =
            candidates.FirstOrDefault(candidate => candidate.FullName.Equals(objectName, StringComparison.InvariantCultureIgnoreCase))
            ?? candidates.FirstOrDefault(candidate => candidate.Name.Equals(objectName, StringComparison.InvariantCultureIgnoreCase))
            ?? candidates.FirstOrDefault(candidate => candidate.FullName.Contains(objectName, StringComparison.InvariantCultureIgnoreCase))
            ?? candidates.FirstOrDefault(candidate => candidate.Name.Contains(objectName, StringComparison.InvariantCultureIgnoreCase));

        if (match == null) {
            failureReason = $"anchor not visible: \"{objectName}\"";
            failureKind = FormationAnchorFailureKind.AnchorNotVisible;
            return false;
        }

        resolved = new FormationResolvedAnchor(
            match.Actor.Position,
            match.Actor.Rotation,
            null,
            match.FullName);
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

            resolved = new FormationResolvedAnchor(player.Position, player.Rotation, anchorCid, GetLocalPlayerNameWorld());
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
        var name = DalamudApi.PlayerState.CharacterName;
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        var world = DalamudApi.PlayerState.HomeWorld.Value.Name.ToString();
        return string.IsNullOrWhiteSpace(world) ? name : $"{name}@{world}";
    }

    private sealed record AnchorCandidate(IGameObject Actor, string Name, string FullName);
}
