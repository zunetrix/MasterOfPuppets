using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Client.UI.Shell;

namespace MasterOfPuppets;

internal static class GameMacroManager {
    private enum MacroSet : uint {
        Individual = 0,
        Shared = 1,
    }

    private readonly unsafe struct MacroMatch {
        public MacroSet Set { get; }
        public uint Index { get; }
        public RaptureMacroModule.Macro* Macro { get; }
        public string SetName => Set == MacroSet.Shared ? "shared" : "individual";

        public MacroMatch(MacroSet set, uint index, RaptureMacroModule.Macro* macro) {
            Set = set;
            Index = index;
            Macro = macro;
        }
    }

    public static unsafe bool TryExecute(string macroIndexOrName, string? scopeArg, out string error) {
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(macroIndexOrName)) {
            error = "Usage: /mop gamemacro <index|name> [individual|i|shared|share|s]";
            return false;
        }

        if (!TryParseScope(scopeArg, out var explicitSet, out error))
            return false;

        if (uint.TryParse(macroIndexOrName, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index)) {
            return TryExecuteByIndex(index, explicitSet ?? MacroSet.Individual, out error);
        }

        return TryExecuteByName(macroIndexOrName.Trim().Trim('"'), explicitSet, out error);
    }

    private static bool TryParseScope(string? scopeArg, out MacroSet? set, out string error) {
        set = null;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(scopeArg))
            return true;

        switch (scopeArg.Trim().ToLowerInvariant()) {
            case "individual":
            case "i":
                set = MacroSet.Individual;
                return true;
            case "shared":
            case "share":
            case "s":
                set = MacroSet.Shared;
                return true;
            default:
                error = $"Invalid game macro scope '{scopeArg}'. Expected individual|i|shared|share|s.";
                return false;
        }
    }

    private static unsafe bool TryExecuteByIndex(uint index, MacroSet set, out string error) {
        error = string.Empty;

        if (index > 99) {
            error = "Invalid game macro index. Expected 0-99.";
            return false;
        }

        var macro = RaptureMacroModule.Instance()->GetMacro((uint)set, index);
        if (macro == null || !macro->IsNotEmpty()) {
            error = $"Game macro {index:00} in the {GetSetName(set)} macro set is empty or unavailable.";
            return false;
        }

        RaptureShellModule.Instance()->ExecuteMacro(macro);
        return true;
    }

    private static unsafe bool TryExecuteByName(string macroName, MacroSet? explicitSet, out string error) {
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(macroName)) {
            error = "Invalid game macro name.";
            return false;
        }

        var sets = explicitSet.HasValue
            ? [explicitSet.Value]
            : new[] { MacroSet.Individual, MacroSet.Shared };

        var matches = FindMacrosByName(macroName, sets);
        if (matches.Count == 0) {
            var scope = explicitSet.HasValue ? $" in the {GetSetName(explicitSet.Value)} macro set" : string.Empty;
            error = $"Game macro '{macroName}' was not found{scope}.";
            return false;
        }

        if (matches.Count > 1) {
            error = $"Game macro name '{macroName}' is ambiguous: {string.Join(", ", matches.Select(m => $"{m.SetName} {m.Index:00}"))}. Specify individual or shared, or rename one macro.";
            return false;
        }

        RaptureShellModule.Instance()->ExecuteMacro(matches[0].Macro);
        return true;
    }

    private static unsafe List<MacroMatch> FindMacrosByName(string macroName, IEnumerable<MacroSet> sets) {
        var matches = new List<MacroMatch>();
        var module = RaptureMacroModule.Instance();

        foreach (var set in sets) {
            for (uint index = 0; index < 100; index++) {
                var macro = module->GetMacro((uint)set, index);
                if (macro == null || !macro->IsNotEmpty())
                    continue;

                if (string.Equals(macro->Name.ToString(), macroName, StringComparison.OrdinalIgnoreCase))
                    matches.Add(new MacroMatch(set, index, macro));
            }
        }

        return matches;
    }

    private static string GetSetName(MacroSet set) => set == MacroSet.Shared ? "shared" : "individual";
}
