using System;
using System.Collections.Generic;

using Dalamud.Game.ClientState.Objects.SubKinds;
using MasterOfPuppets.Extensions.Dalamud;

namespace MasterOfPuppets;

public sealed class MacroRuntimeVariables {
    public string Me { get; init; } = string.Empty;
    public string Target { get; init; } = string.Empty;
    public string FocusTarget { get; init; } = string.Empty;
    public string Job { get; init; } = string.Empty;
    public string Level { get; init; } = string.Empty;
    public string World { get; init; } = string.Empty;
    public string Leader { get; init; } = string.Empty;
    public string MopOrigin { get; init; } = string.Empty;
    public string MopOriginTarget { get; init; } = string.Empty;
    public string MopOriginFocusTarget { get; init; } = string.Empty;
    public double GlobalDelaySeconds { get; init; } = 0.5;

    public Dictionary<string, string> ToDictionary() => new() {
        ["me"] = Me,
        ["target"] = Target,
        ["ftarget"] = FocusTarget,
        ["job"] = Job,
        ["class"] = Job,
        ["level"] = Level,
        ["world"] = World,
        ["leader"] = Leader,
        ["mop_origin"] = MopOrigin,
        ["mop_origin_target"] = MopOriginTarget,
        ["mop_origin_ftarget"] = MopOriginFocusTarget,
        ["globaldelay"] = GlobalDelaySeconds.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture),
    };

    public Dictionary<string, string> ResolveInlinePlaceholders(Dictionary<string, string>? inlineVars) {
        var result = new Dictionary<string, string>();
        if (inlineVars == null)
            return result;

        foreach (var (key, value) in inlineVars)
            result[key] = ResolvePlaceholder(value);

        return result;
    }

    private string ResolvePlaceholder(string value) {
        if (value.Equals("[me]", StringComparison.OrdinalIgnoreCase))
            return Me;
        if (value.Equals("[t]", StringComparison.OrdinalIgnoreCase))
            return Target;
        return value;
    }

    public static MacroRuntimeVariables Empty { get; } = new();

    public static MacroRuntimeVariables FromCurrentGameState(double globalDelaySeconds = 0.5) {
        var me = string.Empty;
        var world = string.Empty;
        var job = string.Empty;
        var level = string.Empty;
        var leader = string.Empty;
        try {
            if (!string.IsNullOrWhiteSpace(DalamudApi.PlayerState.CharacterName)) {
                world = DalamudApi.PlayerState.HomeWorld.Value.Name.ToString() ?? string.Empty;
                me = string.IsNullOrWhiteSpace(world)
                    ? DalamudApi.PlayerState.CharacterName
                    : $"{DalamudApi.PlayerState.CharacterName}@{world}";
            }

            var localPlayer = DalamudApi.ObjectTable?.LocalPlayer as IPlayerCharacter;
            job = DalamudApi.PlayerState.ClassJob.ValueNullable?.Abbreviation.ToString() ?? string.Empty;
            level = localPlayer?.Level.ToString() ?? string.Empty;

            if (DalamudApi.PartyList.IsInParty()) {
                var partyLeader = DalamudApi.PartyList.GetPartyLeader();
                if (partyLeader != null) {
                    var leaderWorld = partyLeader.World.ValueNullable?.Name.ToString() ?? string.Empty;
                    var leaderName = partyLeader.Name.ToString();
                    leader = string.IsNullOrWhiteSpace(leaderWorld)
                        ? leaderName
                        : $"{leaderName}@{leaderWorld}";
                }
            }
        } catch {
            // Game state may be partially unavailable; leave fields empty rather than throwing.
        }

        return new MacroRuntimeVariables {
            Me = me,
            Target = GameTargetManager.GetTargetName(),
            FocusTarget = GameTargetManager.GetFocusTargetName(),
            Job = job,
            Level = level,
            World = world,
            Leader = leader,
            MopOrigin = me,
            MopOriginTarget = GameTargetManager.GetTargetName(),
            MopOriginFocusTarget = GameTargetManager.GetFocusTargetName(),
            GlobalDelaySeconds = globalDelaySeconds,
        };
    }
}
