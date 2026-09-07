using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

using Lumina.Excel.Sheets;

namespace MasterOfPuppets;

internal readonly record struct CosmeticActionResolution(
    bool Success,
    uint ActionId,
    bool UsedFallback,
    string Category,
    string Message);

internal readonly record struct CosmeticFallbackEligibility(
    bool Success,
    string Kind,
    uint RequestedId,
    string Category,
    IReadOnlyList<uint> CandidateIds,
    IReadOnlyList<uint> UniverseIds,
    string EligibilityToken,
    string UniverseSignature,
    string Message);

internal static class CosmeticActionFallbackResolver {
    internal readonly record struct Candidate(
        uint Id,
        string Name,
        string TextCommand,
        string Category,
        bool IsPersistent = false);

    public static CosmeticActionResolution Resolve(
        string kind,
        uint requestedId,
        bool? requestedPersistent = null,
        bool allowFallback = true) {
        kind = NormalizeKind(kind);
        var requested = GetRequested(kind, requestedId);
        if (requested == null)
            return new(false, 0, false, string.Empty, $"unknown {kind} ID {requestedId}");

        if (kind == "emote" && requestedPersistent.HasValue)
            requested = requested.Value with { IsPersistent = requestedPersistent.Value };

        var unlocked = GetUnlocked(kind);
        if (unlocked.Any(candidate => candidate.Id == requestedId))
            return new(true, requestedId, false, requested.Value.Category, $"{kind} {requestedId} is available");

        if (!allowFallback)
            return new(false, 0, false, requested.Value.Category,
                $"exact {kind} {requestedId} is not unlocked on this character");

        var fallback = ChooseFallback(requested.Value, unlocked, Random.Shared);
        if (fallback == null)
            return new(false, 0, false, requested.Value.Category,
                $"{kind} {requestedId} is unavailable and no owned {requested.Value.Category} fallback exists");

        return new(true, fallback.Value.Id, true, requested.Value.Category,
            $"{kind} {requestedId} is unavailable; using owned {requested.Value.Category} fallback {fallback.Value.Name} ({fallback.Value.Id})");
    }

    public static CosmeticFallbackEligibility EligibleFallbacks(
        string kind,
        uint requestedId,
        bool? requestedPersistent = null) {
        kind = NormalizeKind(kind);
        if (kind == "job") {
            var jobs = GearsetManager.GetAvailableClassJobIds()
                .Where(id => id != requestedId)
                .OrderBy(id => id)
                .ToArray();
            var universe = Enumerable.Range(1, byte.MaxValue).Select(id => (uint)id).ToArray();
            return CreateEligibility(true, kind, requestedId, "job", jobs, universe,
                "available local class/job gearsets");
        }
        var requested = GetRequested(kind, requestedId);
        if (requested == null)
            return CreateEligibility(false, kind, requestedId, string.Empty, [], [],
                $"unknown {kind} ID {requestedId}");
        if (kind == "emote" && requestedPersistent.HasValue)
            requested = requested.Value with { IsPersistent = requestedPersistent.Value };
        var candidates = GetUnlocked(kind)
            .Where(candidate => candidate.Id != requestedId
                && candidate.Category.Equals(requested.Value.Category, StringComparison.OrdinalIgnoreCase)
                && candidate.IsPersistent == requested.Value.IsPersistent)
            .Select(candidate => candidate.Id)
            .Distinct()
            .OrderBy(id => id)
            .ToArray();
        var universeCandidates = GetAll(kind)
            .Where(candidate => candidate.Id != requestedId
                && candidate.Category.Equals(requested.Value.Category, StringComparison.OrdinalIgnoreCase)
                && candidate.IsPersistent == requested.Value.IsPersistent)
            .Select(candidate => candidate.Id)
            .Distinct()
            .OrderBy(id => id)
            .ToArray();
        return CreateEligibility(true, kind, requestedId, requested.Value.Category, candidates, universeCandidates,
            candidates.Length == 0
                ? $"no owned {requested.Value.Category} fallback exists"
                : $"{candidates.Length} owned {requested.Value.Category} fallback candidates");
    }

    internal static CosmeticFallbackEligibility CreateEligibility(
        bool success,
        string kind,
        uint requestedId,
        string category,
        IReadOnlyList<uint> candidates,
        IReadOnlyList<uint> universe,
        string message) {
        var owned = candidates.ToHashSet();
        var bits = new byte[(universe.Count + 7) / 8];
        for (var index = 0; index < universe.Count; index++)
            if (owned.Contains(universe[index]))
                bits[index / 8] |= (byte)(1 << (index % 8));
        var token = Convert.ToBase64String(bits).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var signatureText = string.Join(',', universe);
        var signature = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(signatureText)))[..16]
            .ToLowerInvariant();
        return new(success, kind, requestedId, category, candidates, universe, token, signature, message);
    }

    private static IReadOnlyList<Candidate> GetAll(string kind) => kind switch {
        "emote" => DalamudApi.DataManager.GetExcelSheet<Emote>()
            .Where(row => row.RowId != 0 && row.TextCommand.ValueNullable != null)
            .Select(row => EmoteHelper.GetExecutableAction(row.RowId))
            .Where(action => action != null).Select(action => ToCandidate(kind, action!)).ToArray(),
        "mount" => DalamudApi.DataManager.GetExcelSheet<Mount>()
            .Select(row => MountHelper.GetExecutableAction(row.RowId))
            .Where(action => action != null).Select(action => ToCandidate(kind, action!)).ToArray(),
        "minion" => DalamudApi.DataManager.GetExcelSheet<Companion>()
            .Select(row => MinionHelper.GetExecutableAction(row.RowId))
            .Where(action => action != null).Select(action => ToCandidate(kind, action!)).ToArray(),
        "facewear" => DalamudApi.DataManager.GetExcelSheet<Glasses>()
            .Select(row => FacewearHelper.GetExecutableAction(row.RowId))
            .Where(action => action != null).Select(action => ToCandidate(kind, action!)).ToArray(),
        "fashion_accessory" => DalamudApi.DataManager.GetExcelSheet<Ornament>()
            .Select(row => FashionAccessoriesHelper.GetExecutableAction(row.RowId))
            .Where(action => action != null).Select(action => ToCandidate(kind, action!)).ToArray(),
        _ => [],
    };

    internal static Candidate? ChooseFallback(Candidate requested, IReadOnlyList<Candidate> unlocked, Random random) {
        ArgumentNullException.ThrowIfNull(random);
        var sameCategory = unlocked
            .Where(candidate => candidate.Id != requested.Id
                && candidate.Category.Equals(requested.Category, StringComparison.OrdinalIgnoreCase)
                && candidate.IsPersistent == requested.IsPersistent)
            .ToArray();
        return sameCategory.Length == 0 ? null : sameCategory[random.Next(sameCategory.Length)];
    }

    internal static string ClassifyEmote(string name, string textCommand, string sheetCategory) {
        var text = $"{name} {textCommand}".ToLowerInvariant();
        if (ContainsAny(text,
            "lightstick", "cheer on", "/cheeron", "cheer wave", "/cheerwave",
            "cheer jump", "/cheerjump", "cheer rhythm", "/cheerrhythm",
            "cheer lights", "/cheerlights"))
            return "Lightstick";
        if (ContainsAny(text, "dance", "step", "jig", "bees knees", "harvest", "manderville", "moonlift", "sundrop", "yol dance", "bomb dance", "gold dance", "thavnairian", "little ladies"))
            return "Dance";
        if (ContainsAny(text, "push-up", "pushup", "sit-up", "situp", "squat", "stretch", "exercise", "yoga"))
            return "Exercise";
        if (ContainsAny(text, "eat", "drink", "toast", "tea", "coffee", "apple", "bread", "egg", "pizza", "chocolate"))
            return "Food and drink";
        if (ContainsAny(text, "play dead", "doze", "sleep", "nap", "sit", "lean", "rest"))
            return "Resting";
        if (ContainsAny(text, "songbird", "instrument", "music", "flute", "lute", "violin", "harp", "drum"))
            return "Musical performance";
        if (ContainsAny(text, "wave", "greet", "bow", "salute", "welcome", "goodbye", "farewell"))
            return "Greeting";
        if (ContainsAny(text, "cheer", "clap", "applaud", "victory", "high five", "fist bump", "joy"))
            return "Celebration";
        if (ContainsAny(text, "hug", "embrace", "kiss", "dote", "love"))
            return "Affection";
        return string.IsNullOrWhiteSpace(sheetCategory) ? "General emote" : sheetCategory.Trim();
    }

    internal static string ClassifyAccessory(string name) {
        var text = name?.ToLowerInvariant() ?? string.Empty;
        if (ContainsAny(text, "umbrella", "parasol")) return "Umbrella";
        if (ContainsAny(text, "wing", "angel", "demon")) return "Wings";
        if (ContainsAny(text, "backpack", "satchel", "bag")) return "Back accessory";
        return "Fashion accessory";
    }

    internal static string ClassifyFacewear(string name) {
        var text = name?.ToLowerInvariant() ?? string.Empty;
        if (ContainsAny(text, "sunglass", "shades")) return "Sunglasses";
        if (ContainsAny(text, "eyepatch", "eye patch")) return "Eyepatch";
        if (ContainsAny(text, "monocle")) return "Monocle";
        if (ContainsAny(text, "goggle")) return "Goggles";
        return "Glasses";
    }

    private static Candidate? GetRequested(string kind, uint id) {
        var action = kind switch {
            "emote" => EmoteHelper.GetExecutableAction(id),
            "mount" => MountHelper.GetExecutableAction(id),
            "minion" => MinionHelper.GetExecutableAction(id),
            "facewear" => FacewearHelper.GetExecutableAction(id),
            "fashion_accessory" => FashionAccessoriesHelper.GetExecutableAction(id),
            _ => null,
        };
        return action == null ? null : ToCandidate(kind, action);
    }

    private static IReadOnlyList<Candidate> GetUnlocked(string kind) => kind switch {
        "emote" => EmoteHelper.GetAllowedItems().Select(action => ToCandidate(kind, action)).ToArray(),
        "mount" => MountHelper.GetAllowedItems().Select(action => ToCandidate(kind, action)).ToArray(),
        "minion" => MinionHelper.GetAllowedItems().Select(action => ToCandidate(kind, action)).ToArray(),
        "facewear" => FacewearHelper.GetAllowedItems().Select(action => ToCandidate(kind, action)).ToArray(),
        "fashion_accessory" => FashionAccessoriesHelper.GetAllowedItems().Select(action => ToCandidate(kind, action)).ToArray(),
        _ => [],
    };

    private static Candidate ToCandidate(string kind, ExecutableAction action) {
        var category = kind switch {
            "emote" => ClassifyEmote(action.ActionName, action.TextCommand, action.Category ?? string.Empty),
            "fashion_accessory" => ClassifyAccessory(action.ActionName),
            "facewear" => ClassifyFacewear(action.ActionName),
            _ => string.IsNullOrWhiteSpace(action.Category) ? kind.Replace('_', ' ') : action.Category,
        };
        return new(
            action.ActionId,
            action.ActionName ?? action.ActionId.ToString(),
            action.TextCommand ?? string.Empty,
            category,
            kind == "emote" && EmoteHelper.IsPersistent(action.ActionId));
    }

    private static string NormalizeKind(string kind) => (kind ?? string.Empty)
        .Trim().ToLowerInvariant().Replace('-', '_') switch {
            "companion" => "minion",
            "ornament" or "accessory" or "fashion" => "fashion_accessory",
            var normalized => normalized,
        };

    private static bool ContainsAny(string text, params string[] values) =>
        values.Any(value => text.Contains(value, StringComparison.OrdinalIgnoreCase));
}
