using System.Collections.Generic;
using System.Linq;

using MasterOfPuppets.Extensions.Dalamud;

namespace MasterOfPuppets.Formations;

internal static class FormationMemberVisibility {
    internal static string CaptureEligibleMemberBits(Plugin plugin, Formation formation) {
        var formationCids = formation.Points
            .SelectMany(point => point.GetEffectiveCids(plugin.Config.CidsGroups))
            .Where(cid => cid != 0)
            .ToHashSet();
        var visible = new HashSet<ulong>();
        var localCid = DalamudApi.PlayerState.ContentId;
        if (formationCids.Contains(localCid))
            visible.Add(localCid);

        var visibleNames = DalamudApi.ObjectTable
            .Where(actor => actor is { Address: not 0 })
            .Select(actor => actor.GetPlayerNameWorld() ?? actor.Name.TextValue)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToArray();
        foreach (var character in plugin.Config.Characters) {
            if (!formationCids.Contains(character.Cid) || string.IsNullOrWhiteSpace(character.Name))
                continue;
            if (visibleNames.Any(name => FormationCharacterName.Matches(character.Name, name)))
                visible.Add(character.Cid);
        }

        return FormationChatSyncCodec.EncodeEligibleMembers(
            formation,
            plugin.Config.CidsGroups,
            visible);
    }

    internal static bool IsLocalMemberEligible(
        Plugin plugin,
        Formation formation,
        string eligibleMemberBits) =>
        FormationChatSyncCodec.IsEligibleMember(
            formation,
            plugin.Config.CidsGroups,
            DalamudApi.PlayerState.ContentId,
            eligibleMemberBits);
}
