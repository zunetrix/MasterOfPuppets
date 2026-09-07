using System;
using System.Collections.Generic;
using System.Linq;

namespace MasterOfPuppets.LuaScripting.Automation;

internal static class LuaBandLeaderResolver {
    public static bool TryResolve(
        IReadOnlyList<CidGroup> groups,
        IReadOnlyList<Character> characters,
        ulong localCid,
        out ulong leaderCid,
        out string leaderName) {
        leaderCid = 0;
        leaderName = string.Empty;
        if (localCid == 0)
            return false;

        var configured = (characters ?? [])
            .Where(character => character.Cid != 0)
            .GroupBy(character => character.Cid)
            .ToDictionary(group => group.Key, group => group.First().Name ?? string.Empty);
        var group = (groups ?? [])
            .FirstOrDefault(candidate => {
                var members = (candidate.Cids ?? []).Where(cid => cid != 0).Distinct().ToArray();
                return members.Length == 8
                    && members.Contains(localCid)
                    && members.All(configured.ContainsKey);
            });
        if (group == null)
            return false;

        leaderCid = group.Cids.FirstOrDefault(cid => cid != 0);
        return leaderCid != 0
            && configured.TryGetValue(leaderCid, out leaderName)
            && !string.IsNullOrWhiteSpace(leaderName);
    }
}
