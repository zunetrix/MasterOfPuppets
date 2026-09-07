using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using MasterOfPuppets.Extensions;
using MasterOfPuppets.LuaScripting.Runs;

namespace MasterOfPuppets.LuaScripting;

internal static class LuaScriptCatalog {
    internal const int MaximumImportJsonBytes = 1024 * 1024;
    internal const int MaximumEncodedImportLength = 2 * 1024 * 1024;
    public const string DefaultBeeSwarmName = "Bee Swarm";
    private const string DefaultBeeSwarmFileName = "bee_swarm.lua";
    private const string PreviousBeeSwarmHash = "a311f2dce8840c78e04103309835e0ea87ed51998e0dd99666cdbd711117054e";
    private const string DefaultBeeSwarmVariables = "$radius = 1.45\n$spread = 0.38\n$speed = 1.0";
    public const string DefaultSixteenVoicesName = "Sixteen Voices";
    private const string DefaultSixteenVoicesFileName = "sixteen_voices.lua";
    public const string DefaultDynamicCongaName = "Dynamic Conga Line";
    private const string DefaultDynamicCongaFileName = "dynamic_conga.lua";
    public const string DefaultMarchingFormationName = "Marching (32)";
    private const string DefaultMarchingFormationFileName = "marching_formation.lua";
    // Keep every shipped stock source hash here. Upgrade checks canonicalize line
    // endings because collaborators can receive LF or CRLF copies of the same
    // packaged script through their checkout/sync configuration.
    private static readonly HashSet<string> KnownMarchingFormationHashes = new(StringComparer.OrdinalIgnoreCase) {
        "fbe657869ef4838590dd3d565668af08a367fcef2896188a6b4bbb61661f3477",
        "b651870ea407130a5dea8a8d603d3a6f27511c4615ee9a6f5ccbb8ff7cff33ab",
        "f945499c5483aac85df106af8e344fb8044d888694ba3ecf799b00043da1da51",
        "a54a2600a41233552d4a2895826d3384f7d7f94a1906097de08c774ece5bcbae",
        "fbc89cc18f7b151ec387376634d0634a81cc119b0284909ae546245cc70759bc",
        "a4b6105a4aa9076cbc722dfed8cac076f81e6a87be6b6a6442db46085fb15de0",
        "dd2ae3c57f1b3a9cbbda4e0ab8ca4dce9bcd04799328bc6074687bb8f769fba0",
    };
    private const string DefaultMarchingFormationVariables = "$anchor =\n$group = \"32 Ordered\"\n$visible_only = true\n$rows = 4\n$columns = 8\n$horizontal = 1.0\n$vertical = 1.5\n$precision = 0.12\n$neighbor_correction = 0.25\n$maximum_neighbor_correction = 0.35\n$preserve_emote = false";
    public const string DefaultSwirlingVortexName = "Swirling Vortex";
    private const string DefaultSwirlingVortexFileName = "swirling_vortex.lua";
    public const string DefaultTripleRingVortexName = "Swirling Vortex - Triple Ring";
    private const string DefaultTripleRingVortexFileName = "swirling_vortex_triple_ring.lua";
    public const string DefaultEventDrivenCurtainCallName = "Event-Driven Curtain Call";
    private const string DefaultEventDrivenCurtainCallFileName = "event_driven_curtain_call.lua";
    public const string DefaultMirrorTargetCombatName = "Mirror Target Combat";
    private const string DefaultMirrorTargetCombatFileName = "mirror_target_combat.lua";
    public const string DefaultMirrorCombatTargetName = "Mirror Combat Target";
    private const string DefaultMirrorCombatTargetFileName = "mirror_combat_target.lua";
    private const string CurrentMirrorTargetCombatHash = "3ce370a212818b306dd6836f5d7008b8be10d0b0602850d201433f5d7670ca53";
    public const string DefaultMirrorTargetJobName = "Mirror Target Job";
    private const string DefaultMirrorTargetJobFileName = "mirror_target_job.lua";
    private const string CurrentMirrorTargetJobHash = "97a0c0538b01b1d6f3c77cc2a91d043c4093017987ec570e772b5a0d1fbd3f3d";
    public const string DefaultMirrorTargetEmotesName = "Mirror Target Emotes";
    private const string DefaultMirrorTargetEmotesFileName = "mirror_target_emotes.lua";
    private static readonly HashSet<string> KnownPreviousMirrorTargetEmotesHashes = new(StringComparer.OrdinalIgnoreCase) {
        "4aecb0cd6f1125cbfa54fe80b6598aeda537d026704b741965eae0a37ea5a2fb",
        "ea726d4c1b40ea5c1f50ca3b6a4b9089cb1bc5612a8409e0e9d6edce6ab0f4d5",
        "85df26293ba2a72f399d79bb729eb8cbe1a768ab310fa324a4304e57a87c2125",
        "5a045dddcecacc5593c2c573b70dd86897f7010476480629a7bae29d45869284",
        "5ed3eda9d673562da7c4dfac8520ecf7f1736aee47cd7fcd53308ca23487bab1",
        "5f6ce987b76e094499f5dd2d7ce5000cd62d9948cf31ab0d79cebdbb360a0698",
        "eae20ddd950b71e83e02d1e0fa5ed37ebc41be6ddcd78187aa369adba8673a8e",
        "6de54a9c5cb9bbe642acd1b4515e529ecbacaf0b256d7ae52b9afc6c1f4941fd",
    };
    private const string CurrentMirrorTargetEmotesHash = "9eea1fa8efbef414077302072d8598b8b480cb87ac9db1d1b9bfbc7e55da515f";
    public const string MirrorScriptV2Name = "Mirror Script V2";
    private const string MirrorScriptV2FileName = "mirror_v2.lua";
    private const string LegacyMirrorScriptV2Hash = "59b62cfde65d76d7800bc8315c730383efe7d321c6722bbce13d4b20a6cf7937";
    private const string PreviousMirrorScriptV2Hash = "53ce164441b31d3db7f01103edf3711f6c6192ad27dde99d2ebc930f1d148d3a";
    private const string MovementSafeMirrorScriptV2Hash = "85405fd0e1da70bc671bfa5bbe18a178bc36e96356569fa56a2030df9cd2bf7d";
    private const string DiagnosableMirrorScriptV2Hash = "f3fafa284d64b83b2fe72d13d8431d7d15888a2059881598c1832c3ed2b86776";
    private const string DirectChatSafeMirrorScriptV2Hash = "2e4f1a8bcd4fbd5c65aa98d434b79af4acad03180887c1e11bb488a24f3c1f44";
    private const string PersistentEmoteSafeMirrorScriptV2Hash = "ac5abd6eb2ca32249489b61d9697a6f5c03e5ec59b265950cedd69d91d2056e5";
    private const string CurrentMirrorScriptV2Hash = "efe7c100942b40ac2fb94ee5817e2211dc0c1c43a49731e8176d7af324f3df67";
    public const string DefaultMovingEmoteLockName = "Moving Emote Lock";
    private const string DefaultMovingEmoteLockFileName = "moving_emote_lock.lua";
    private const string FirstMovingEmoteLockHash = "b24316f6e06a8d53537e23e30efa448bf66154baa0149485f582043c5da91cf5";
    private const string FirstSwirlingVortexHash = "b5f3d50bf92f71c22193f4a34f2970662d1e7934a0a1ee5834450055f7d9a9c0";
    private const string PreviousSwirlingVortexHash = "acbf6c84d72815215a78a8863777883b17ac491151ee00f7d90535a9b73586df";
    private const string CurrentSwirlingVortexHash = "3b7919ae9719319566f8661caa5504e7ae8315c7c5cccd3e9a0a85a10c6b1a21";
    private const string DefaultSwirlingVortexFormation = "Swarm - Honeycomb Hive (Wide)";
    private const string DefaultSwirlingVortexVariables = "$inner = 1.50\n$outer = 2.80\n$speed = 1.00\n$spacing = 0.80\n$stage = 0.0\n$ramp = 1.0";
    private const string DefaultTripleRingVortexVariables = "$radius = 1.60\n$spread = 0.75\n$pace = 2.70\n$stage = 7.0\n$ramp = 2.5";

    public static bool EnsureDefaults(Configuration configuration) {
        configuration.LuaScripts ??= new();
        configuration.Formations ??= new();
        var changed = false;
        changed |= EnsureDefault(
            configuration,
            DefaultBeeSwarmName,
            DefaultBeeSwarmFileName,
            "Fast, smooth arcs, loops, and spiral motion around a selected target.",
            DefaultBeeSwarmVariables);
        changed |= EnsureDefault(
            configuration,
            DefaultSixteenVoicesName,
            DefaultSixteenVoicesFileName,
            "A synchronized, pre-written conversation performed by the Artemis and Kazuko bands.");
        changed |= EnsureDefault(
            configuration,
            DefaultDynamicCongaName,
            DefaultDynamicCongaFileName,
            "A target-led Lua conga with dynamic fallback and automatic chain repair for sixteen performers.");
        changed |= EnsureDefault(
            configuration,
            DefaultMarchingFormationName,
            DefaultMarchingFormationFileName,
            "Maintains a centered row-major marching grid behind a freely controlled leader using a rate-limited rigid formation frame.",
            DefaultMarchingFormationVariables,
            requiredResources: LuaResourceKind.Movement | LuaResourceKind.GameActions,
            declaredCapabilities: ["legacy.flat-api", "mop.actions", "mop.game-state"]);
        changed |= EnsureDefault(
            configuration,
            DefaultSwirlingVortexName,
            DefaultSwirlingVortexFileName,
            "A tight pair of rings around a selected target. Visible roster members are compacted into even spacing; walkers inside, runners outside.",
            DefaultSwirlingVortexVariables,
            configuration.Formations.Any(formation => formation.Name.Equals(
                DefaultSwirlingVortexFormation,
                StringComparison.OrdinalIgnoreCase))
                    ? DefaultSwirlingVortexFormation
                    : string.Empty);
        changed |= EnsureDefault(
            configuration,
            DefaultTripleRingVortexName,
            DefaultTripleRingVortexFileName,
            "Three synchronized concentric walking rings with one shared target and equal ground pace.",
            DefaultTripleRingVortexVariables,
            configuration.Formations.Any(formation => formation.Name.Equals(
                DefaultSwirlingVortexFormation,
                StringComparison.OrdinalIgnoreCase))
                    ? DefaultSwirlingVortexFormation
                    : string.Empty);
        changed |= EnsureDefault(
            configuration,
            DefaultEventDrivenCurtainCallName,
            DefaultEventDrivenCurtainCallFileName,
            "Waits for a typed /say cue, optionally composes a saved formation and macro, then gives a synchronized response.",
            "$cue = places everyone\n$macro =\n$formation =");
        changed |= EnsureDefault(
            configuration,
            DefaultMirrorTargetCombatName,
            DefaultMirrorTargetCombatFileName,
            "All configured characters mirror a loaded target; job changes use the script's explicit gearset map.",
            "$all_configured = true",
            requiredResources: LuaResourceKind.GameActions,
            declaredCapabilities: ["legacy.flat-api", "mop.actions", "mop.events", "mop.game-state"]);
        changed |= EnsureDefault(
            configuration,
            DefaultMirrorTargetJobName,
            DefaultMirrorTargetJobFileName,
            "Mirrors any visible run target's class/job swaps using exact local gearset names or numbers coded in the script.",
            "$all_configured = true\n$poll_timeout = 30",
            requiredResources: LuaResourceKind.GameActions,
            declaredCapabilities: ["legacy.flat-api", "mop.actions", "mop.game-state"]);
        changed |= EnsureDefault(
            configuration,
            DefaultMirrorTargetEmotesName,
            DefaultMirrorTargetEmotesFileName,
            "Mirrors any visible run target's one-shot and looping emotes; optional ID remaps stay entirely in Lua.",
            "$all_configured = true",
            requiredResources: LuaResourceKind.GameActions,
            declaredCapabilities: ["mop.actions", "mop.events", "mop.game-state", "mop.runtime"]);
        changed |= EnsureDefault(
            configuration,
            MirrorScriptV2Name,
            MirrorScriptV2FileName,
            "Mirrors one immutable real-player target through typed state, action, event, and coordination capabilities.",
            requiredResources: LuaResourceKind.GameActions | LuaResourceKind.Movement,
            declaredCapabilities: ["mop.actions", "mop.coordination", "mop.events", "mop.game-state", "mop.runtime"]);
        changed |= EnsureDefault(
            configuration,
            DefaultMovingEmoteLockName,
            DefaultMovingEmoteLockFileName,
            "Replays a recent looping emote after the source toggles their weapon so plugin-controlled movement can retain the animation.",
            "$all_configured = true",
            requiredResources: LuaResourceKind.ChatActionBudget | LuaResourceKind.GameActions,
            declaredCapabilities: ["legacy.flat-api", "mop.actions", "mop.game-state", "mop.runtime"]);
        changed |= UpgradeUnmodifiedBeeSwarm(configuration);
        changed |= UpgradeUnmodifiedMarchingFormation(configuration);
        changed |= UpgradeUnmodifiedSwirlingVortex(configuration);
        changed |= UpgradeMirrorTargetCombat(configuration);
        changed |= UpgradeMirrorTargetJob(configuration);
        changed |= UpgradeUnmodifiedMirrorTargetEmotes(configuration);
        changed |= UpgradeUnmodifiedMirrorScriptV2(configuration);
        changed |= UpgradeUnmodifiedMovingEmoteLock(configuration);
        changed |= AssignDefaultSwirlingVortexFormation(configuration);
        foreach (var script in configuration.LuaScripts)
            changed |= script.MigrateMetadata();
        return changed;
    }

    // Install just this additional script on load; do not reset existing scripts
    // or reintroduce unrelated defaults into an established user catalog.
    internal static bool EnsureMirrorCombatTarget(Configuration configuration) {
        configuration.LuaScripts ??= new();
        var added = EnsureDefault(
            configuration,
            DefaultMirrorCombatTargetName,
            DefaultMirrorCombatTargetFileName,
            "Mirrors a visible player's combat and state without changing clients' selected targets. Ordinary actions use each client's current target.",
            "$all_configured = true",
            requiredResources: LuaResourceKind.GameActions,
            declaredCapabilities: ["legacy.flat-api", "mop.actions", "mop.events", "mop.game-state"]);
        if (added)
            Find(configuration, DefaultMirrorCombatTargetName)!.MigrateMetadata();
        return added;
    }

    private static bool EnsureDefault(
        Configuration configuration,
        string name,
        string fileName,
        string description,
        string variables = "",
        string participantFormation = "",
        LuaResourceKind? requiredResources = null,
        IReadOnlyList<string>? declaredCapabilities = null) {
        if (configuration.LuaScripts.Any(script =>
                script.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            return false;

        var source = LoadPackagedScript(fileName);
        if (string.IsNullOrWhiteSpace(source)) {
            DalamudApi.PluginLog.Warning($"[Lua] packaged default script was not found: {fileName}");
            return false;
        }

        configuration.LuaScripts.Add(new LuaScriptDefinition {
            Name = name,
            Description = description,
            Variables = variables,
            ParticipantFormation = participantFormation,
            Source = source,
            RequiredResources = requiredResources,
            DeclaredCapabilities = declaredCapabilities?.ToList() ?? [],
        });
        return true;
    }

    private static bool UpgradeUnmodifiedBeeSwarm(Configuration configuration) {
        var script = configuration.LuaScripts.FirstOrDefault(candidate =>
            candidate.Name.Equals(DefaultBeeSwarmName, StringComparison.OrdinalIgnoreCase));
        if (script == null || !script.Hash.Equals(PreviousBeeSwarmHash, StringComparison.OrdinalIgnoreCase))
            return false;

        script.Source = LoadPackagedScript(DefaultBeeSwarmFileName);
        if (string.IsNullOrWhiteSpace(script.Variables))
            script.Variables = DefaultBeeSwarmVariables;
        return true;
    }

    private static bool UpgradeUnmodifiedSwirlingVortex(Configuration configuration) {
        var script = configuration.LuaScripts.FirstOrDefault(candidate =>
            candidate.Name.Equals(DefaultSwirlingVortexName, StringComparison.OrdinalIgnoreCase));
        if (script == null)
            return false;

        var packaged = LoadPackagedScript(DefaultSwirlingVortexFileName);
        if (script.Source.Equals(packaged, StringComparison.Ordinal))
            return false;

        var unmodified = script.Hash.Equals(FirstSwirlingVortexHash, StringComparison.OrdinalIgnoreCase)
            || script.Hash.Equals(PreviousSwirlingVortexHash, StringComparison.OrdinalIgnoreCase)
            || script.Hash.Equals(CurrentSwirlingVortexHash, StringComparison.OrdinalIgnoreCase)
            || script.Source.Contains("local roster = mop.get_group(group_name)", StringComparison.Ordinal)
            || script.Source.Contains("requested_inner", StringComparison.Ordinal)
            || script.Source.Contains("visible_circle", StringComparison.Ordinal);
        if (!unmodified)
            return false;

        script.Source = packaged;
        script.Variables = DefaultSwirlingVortexVariables;
        script.Description = "Two concentric rings around a selected target: walking members inside, running members outside, spaced by the live roster.";
        return true;
    }

    private static bool UpgradeUnmodifiedMarchingFormation(Configuration configuration) {
        var script = configuration.LuaScripts.FirstOrDefault(candidate =>
            candidate.Name.Equals(DefaultMarchingFormationName, StringComparison.OrdinalIgnoreCase));
        if (script == null)
            return false;

        var packaged = LoadPackagedScript(DefaultMarchingFormationFileName);
        var savedHash = ComputeCanonicalSourceHash(script.Source);
        var packagedHash = ComputeCanonicalSourceHash(packaged);
        if (savedHash.Equals(packagedHash, StringComparison.OrdinalIgnoreCase)
            || !IsKnownMarchingFormationSource(script.Source))
            return false;

        script.Source = packaged;
        script.Variables = DefaultMarchingFormationVariables;
        script.RequiredResources = LuaResourceKind.Movement | LuaResourceKind.GameActions;
        script.DeclaredCapabilities = ["legacy.flat-api", "mop.actions", "mop.game-state"];
        script.Revision = Math.Max(1, script.Revision) + 1;
        return true;
    }

    internal static string ComputeCanonicalSourceHash(string source) =>
        LuaScriptDefinition.ComputeHash((source ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n'));

    internal static bool IsKnownMarchingFormationSource(string source) =>
        KnownMarchingFormationHashes.Contains(ComputeCanonicalSourceHash(source));

    private static bool UpgradeUnmodifiedMirrorScriptV2(Configuration configuration) {
        var script = configuration.LuaScripts.FirstOrDefault(candidate =>
            candidate.Name.Equals(MirrorScriptV2Name, StringComparison.OrdinalIgnoreCase));
        if (script == null)
            return false;

        if (string.Equals(script.Hash, CurrentMirrorScriptV2Hash, StringComparison.OrdinalIgnoreCase))
            return false;

        script.Source = LoadPackagedScript(MirrorScriptV2FileName);
        script.Description = "Mirrors one immutable real-player target through typed state, action, event, and coordination capabilities.";
        script.Variables = string.Empty;
        script.ParticipantFormation = string.Empty;
        script.RequiredResources = LuaResourceKind.GameActions | LuaResourceKind.Movement;
        script.DeclaredCapabilities = ["mop.actions", "mop.coordination", "mop.events", "mop.game-state", "mop.runtime"];
        script.Revision = Math.Max(1, script.Revision) + 1;
        return true;
    }

    private static bool UpgradeMirrorTargetCombat(Configuration configuration) {
        var script = configuration.LuaScripts.FirstOrDefault(candidate =>
            candidate.Name.Equals(DefaultMirrorTargetCombatName, StringComparison.OrdinalIgnoreCase));
        if (script == null || script.Hash.Equals(CurrentMirrorTargetCombatHash, StringComparison.OrdinalIgnoreCase))
            return false;

        script.Source = LoadPackagedScript(DefaultMirrorTargetCombatFileName);
        script.Description = "All configured characters mirror a loaded target; job changes use the script's explicit gearset map.";
        script.Variables = "$all_configured = true";
        script.ParticipantFormation = string.Empty;
        script.RequiredResources = LuaResourceKind.GameActions;
        script.DeclaredCapabilities = ["legacy.flat-api", "mop.actions", "mop.events", "mop.game-state"];
        script.Revision = Math.Max(1, script.Revision) + 1;
        return true;
    }

    private static bool UpgradeMirrorTargetJob(Configuration configuration) {
        var script = configuration.LuaScripts.FirstOrDefault(candidate =>
            candidate.Name.Equals(DefaultMirrorTargetJobName, StringComparison.OrdinalIgnoreCase));
        if (script == null || script.Hash.Equals(CurrentMirrorTargetJobHash, StringComparison.OrdinalIgnoreCase))
            return false;
        // Only migrate the pre-universal stock implementation. Once a script
        // contains the current observed->local policy model, differing source
        // means the owner edited their gearset policy and must be preserved.
        if (!script.Source.Contains("No gearset selector is coded for class/job", StringComparison.Ordinal)
            || !script.Source.Contains("mop.actions.job(target_job_id, selector)", StringComparison.Ordinal))
            return false;

        script.Source = LoadPackagedScript(DefaultMirrorTargetJobFileName);
        script.Description = "Mirrors any visible run target's class/job swaps using exact local gearset names or numbers coded in the script.";
        script.Variables = "$all_configured = true\n$poll_timeout = 30";
        script.ParticipantFormation = string.Empty;
        script.RequiredResources = LuaResourceKind.GameActions;
        script.DeclaredCapabilities = ["legacy.flat-api", "mop.actions", "mop.game-state"];
        script.Revision = Math.Max(1, script.Revision) + 1;
        return true;
    }

    private static bool UpgradeUnmodifiedMirrorTargetEmotes(Configuration configuration) {
        var script = configuration.LuaScripts.FirstOrDefault(candidate =>
            candidate.Name.Equals(DefaultMirrorTargetEmotesName, StringComparison.OrdinalIgnoreCase));
        if (script == null)
            return false;

        var savedHash = ComputeCanonicalSourceHash(script.Source);
        if (savedHash.Equals(CurrentMirrorTargetEmotesHash, StringComparison.OrdinalIgnoreCase)
            || !KnownPreviousMirrorTargetEmotesHashes.Contains(savedHash))
            return false;

        script.Source = LoadPackagedScript(DefaultMirrorTargetEmotesFileName);
        script.Description = "Mirrors any visible run target's one-shot and looping emotes without changing recipient targets; optional ID remaps stay entirely in Lua.";
        script.Variables = "$all_configured = true";
        script.ParticipantFormation = string.Empty;
        script.RequiredResources = LuaResourceKind.GameActions;
        script.DeclaredCapabilities = ["mop.actions", "mop.events", "mop.game-state", "mop.runtime"];
        script.Revision = Math.Max(1, script.Revision) + 1;
        return true;
    }

    private static bool UpgradeUnmodifiedMovingEmoteLock(Configuration configuration) {
        var script = configuration.LuaScripts.FirstOrDefault(candidate =>
            candidate.Name.Equals(DefaultMovingEmoteLockName, StringComparison.OrdinalIgnoreCase));
        if (script == null || !script.Hash.Equals(FirstMovingEmoteLockHash, StringComparison.OrdinalIgnoreCase))
            return false;

        script.Source = LoadPackagedScript(DefaultMovingEmoteLockFileName);
        script.RequiredResources = LuaResourceKind.ChatActionBudget | LuaResourceKind.GameActions;
        script.Revision = Math.Max(1, script.Revision) + 1;
        return true;
    }

    private static bool AssignDefaultSwirlingVortexFormation(Configuration configuration) {
        var script = configuration.LuaScripts.FirstOrDefault(candidate =>
            candidate.Name.Equals(DefaultSwirlingVortexName, StringComparison.OrdinalIgnoreCase));
        if (script == null
            || !string.IsNullOrWhiteSpace(script.ParticipantFormation)
            || !configuration.Formations.Any(formation => formation.Name.Equals(
                DefaultSwirlingVortexFormation,
                StringComparison.OrdinalIgnoreCase)))
            return false;

        script.ParticipantFormation = DefaultSwirlingVortexFormation;
        return true;
    }

    public static LuaScriptDefinition? Find(Configuration configuration, string name) {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        var normalized = name.Trim();
        return configuration.LuaScripts?.FirstOrDefault(script =>
            script.Name.Equals(normalized, StringComparison.OrdinalIgnoreCase)
            || (normalized.Equals(DefaultBeeSwarmFileName, StringComparison.OrdinalIgnoreCase)
                && script.Name.Equals(DefaultBeeSwarmName, StringComparison.OrdinalIgnoreCase))
            || (normalized.Equals(DefaultSixteenVoicesFileName, StringComparison.OrdinalIgnoreCase)
                && script.Name.Equals(DefaultSixteenVoicesName, StringComparison.OrdinalIgnoreCase))
            || (normalized.Equals(DefaultDynamicCongaFileName, StringComparison.OrdinalIgnoreCase)
                && script.Name.Equals(DefaultDynamicCongaName, StringComparison.OrdinalIgnoreCase))
            || (normalized.Equals(DefaultSwirlingVortexFileName, StringComparison.OrdinalIgnoreCase)
                && script.Name.Equals(DefaultSwirlingVortexName, StringComparison.OrdinalIgnoreCase))
            || (normalized.Equals(DefaultTripleRingVortexFileName, StringComparison.OrdinalIgnoreCase)
                && script.Name.Equals(DefaultTripleRingVortexName, StringComparison.OrdinalIgnoreCase))
            || (normalized.Equals(DefaultEventDrivenCurtainCallFileName, StringComparison.OrdinalIgnoreCase)
                && script.Name.Equals(DefaultEventDrivenCurtainCallName, StringComparison.OrdinalIgnoreCase))
            || (normalized.Equals(DefaultMirrorTargetCombatFileName, StringComparison.OrdinalIgnoreCase)
                && script.Name.Equals(DefaultMirrorTargetCombatName, StringComparison.OrdinalIgnoreCase))
            || (normalized.Equals(DefaultMirrorTargetJobFileName, StringComparison.OrdinalIgnoreCase)
                && script.Name.Equals(DefaultMirrorTargetJobName, StringComparison.OrdinalIgnoreCase))
            || (normalized.Equals(DefaultMirrorTargetEmotesFileName, StringComparison.OrdinalIgnoreCase)
                && script.Name.Equals(DefaultMirrorTargetEmotesName, StringComparison.OrdinalIgnoreCase))
            || (normalized.Equals(MirrorScriptV2FileName, StringComparison.OrdinalIgnoreCase)
                && script.Name.Equals(MirrorScriptV2Name, StringComparison.OrdinalIgnoreCase))
            || (normalized.Equals(DefaultMovingEmoteLockFileName, StringComparison.OrdinalIgnoreCase)
                && script.Name.Equals(DefaultMovingEmoteLockName, StringComparison.OrdinalIgnoreCase)));
    }

    public static string Export(LuaScriptDefinition script) {
        ArgumentNullException.ThrowIfNull(script);
        var portable = script.Clone();
        // Catalog identity is installation-local. The importer assigns a new identity, so
        // exporting these values leaks irrelevant state and can make equal copies look distinct.
        portable.Id = string.Empty;
        portable.Revision = 1;
        return portable.JsonSerialize().Compress();
    }

    public static LuaScriptDefinition Import(string text) {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Lua script import is empty.");

        text = text.Trim();
        if (text.Length > MaximumEncodedImportLength)
            throw new ArgumentException("Lua script import is too large.");
        var json = text.StartsWith('{') ? text : DecompressImport(text);
        if (Encoding.UTF8.GetByteCount(json) > MaximumImportJsonBytes)
            throw new ArgumentException("Lua script import expands beyond its size limit.");
        var script = json.JsonDeserialize<LuaScriptDefinition>()
            ?? throw new ArgumentException("Invalid Lua script data.");
        script.Validate();
        return script;
    }

    private static string DecompressImport(string text) {
        byte[] compressed;
        try {
            compressed = Convert.FromBase64String(text);
        } catch (FormatException exception) {
            throw new ArgumentException("Invalid Lua script share code.", nameof(text), exception);
        }
        if (compressed.Length > MaximumImportJsonBytes)
            throw new ArgumentException("Lua script import is too large.", nameof(text));

        try {
            using var input = new MemoryStream(compressed);
            using var gzip = new GZipStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            var buffer = new byte[8192];
            int read;
            while ((read = gzip.Read(buffer, 0, buffer.Length)) > 0) {
                output.Write(buffer, 0, read);
                if (output.Length > MaximumImportJsonBytes)
                    throw new ArgumentException("Lua script import expands beyond its size limit.", nameof(text));
            }
            return new UTF8Encoding(false, true).GetString(output.ToArray());
        } catch (InvalidDataException exception) {
            throw new ArgumentException("Invalid compressed Lua script share code.", nameof(text), exception);
        } catch (DecoderFallbackException exception) {
            throw new ArgumentException("Lua script share code is not valid UTF-8.", nameof(text), exception);
        }
    }

    public static string LoadPackagedScript(string fileName) {
        if (string.IsNullOrWhiteSpace(fileName)
            || fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || fileName.Contains('/')
            || fileName.Contains('\\'))
            throw new ArgumentException("A packaged Lua script file name is required.", nameof(fileName));

        var assembly = typeof(LuaScriptCatalog).Assembly;
        var resourceName = $"MasterOfPuppets.Lua.Scripts.{fileName}";
        using (var stream = assembly.GetManifestResourceStream(resourceName)) {
            if (stream != null) {
                using var reader = new StreamReader(stream, Encoding.UTF8, true);
                return reader.ReadToEnd();
            }
        }

        // Retain the loose-file fallback for older development outputs.
        var assemblyDirectory = Path.GetDirectoryName(assembly.Location);
        if (string.IsNullOrWhiteSpace(assemblyDirectory))
            assemblyDirectory = DalamudApi.PluginInterface?.AssemblyLocation.DirectoryName;
        if (string.IsNullOrWhiteSpace(assemblyDirectory))
            throw new DirectoryNotFoundException("Plugin assembly directory is unavailable.");
        var scriptsDirectory = Path.GetFullPath(Path.Combine(assemblyDirectory, "Scripts"));
        var scriptPath = Path.GetFullPath(Path.Combine(scriptsDirectory, fileName));
        if (!scriptPath.StartsWith(scriptsDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Packaged Lua script path escaped its directory.");

        return File.ReadAllText(scriptPath);
    }
}
