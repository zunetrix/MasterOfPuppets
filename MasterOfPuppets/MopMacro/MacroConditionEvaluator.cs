using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using MasterOfPuppets.Extensions;
using MasterOfPuppets.Extensions.Dalamud;

namespace MasterOfPuppets;

public static class MacroConditionEvaluator {
    private static readonly Regex BinaryOpRegex = new(@"^\s*(.*?)\s*(==|!=)\s*(.*?)\s*$", RegexOptions.Compiled);
    private static readonly Regex ExistsRegex = new(@"^(?:visible|exists)\s+(.+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Evaluates a condition asynchronously on the main framework thread to ensure safe access to game objects and Dalamud services.
    /// </summary>
    public static async Task<bool> EvaluateAsync(string condition, Plugin plugin) {
        if (string.IsNullOrWhiteSpace(condition))
            return true;

        return await DalamudApi.Framework.RunOnFrameworkThread(() => Evaluate(condition, plugin));
    }

    /// <summary>
    /// Evaluates a condition string against the current game state and variables.
    /// Supports:
    /// - Logical operators: &amp;&amp;, and, ||, or
    /// - Unary negation: !, not
    /// - Targeting: hastarget, notarget, targetisplayer, targetisnpc, hasfocustarget, nofocustarget
    /// - Comparisons: target == "Name", "$var" == "val", "$var" != "", etc.
    /// - Player state: incombat, outcombat, isperforming, isalive, isdead, isleader, inparty
    /// - Object queries: visible "Name", exists "Name"
    /// </summary>
    public static bool Evaluate(string condition, Plugin plugin) {
        if (string.IsNullOrWhiteSpace(condition))
            return true;

        condition = condition.Trim();

        // Check top-level OR
        var orParts = SplitTopLevel(condition, ["||", " or "]);
        if (orParts.Count > 1) {
            return orParts.Any(part => Evaluate(part, plugin));
        }

        // Check top-level AND
        var andParts = SplitTopLevel(condition, ["&&", " and "]);
        if (andParts.Count > 1) {
            return andParts.All(part => Evaluate(part, plugin));
        }

        return EvaluateSingle(condition, plugin);
    }

    private static bool EvaluateSingle(string expr, Plugin plugin) {
        expr = expr.Trim();
        if (string.IsNullOrEmpty(expr))
            return true;

        // Unary NOT
        if (expr.StartsWith("!", StringComparison.Ordinal))
            return !EvaluateSingle(expr[1..], plugin);
        if (expr.StartsWith("not ", StringComparison.OrdinalIgnoreCase))
            return !EvaluateSingle(expr[4..], plugin);

        // Strip enclosing parentheses if present
        if (expr.StartsWith('(') && expr.EndsWith(')')) {
            var inner = expr[1..^1].Trim();
            if (!string.IsNullOrEmpty(inner))
                return Evaluate(inner, plugin);
        }

        // Check binary comparison (==, !=)
        var match = BinaryOpRegex.Match(expr);
        if (match.Success) {
            var left = match.Groups[1].Value.Trim();
            var op = match.Groups[2].Value;
            var right = match.Groups[3].Value.Trim();

            return EvaluateComparison(left, op, right, plugin);
        }

        // Check visible/exists "Name"
        var existsMatch = ExistsRegex.Match(expr);
        if (existsMatch.Success) {
            var targetName = CleanQuotes(existsMatch.Groups[1].Value.Trim());
            return DalamudApi.ObjectTable != null && DalamudApi.ObjectTable.Any(o =>
                o != null &&
                (o.Name.TextValue.Equals(targetName, StringComparison.OrdinalIgnoreCase) ||
                 o.Name.TextValue.Contains(targetName, StringComparison.OrdinalIgnoreCase)));
        }

        // Built-in keywords
        var localPlayer = DalamudApi.ObjectTable?.LocalPlayer;
        switch (expr.ToLowerInvariant()) {
            case "hastarget":
                return localPlayer?.TargetObject != null;

            case "notarget":
                return localPlayer == null || localPlayer.TargetObject == null;

            case "targetisplayer":
                return localPlayer?.TargetObject?.ObjectKind == ObjectKind.Pc;

            case "targetisnpc":
                return localPlayer?.TargetObject != null &&
                       (localPlayer.TargetObject.ObjectKind == ObjectKind.BattleNpc ||
                        localPlayer.TargetObject.ObjectKind == ObjectKind.EventNpc);

            case "hasfocustarget":
                return DalamudApi.TargetManager?.FocusTarget != null;

            case "nofocustarget":
                return DalamudApi.TargetManager == null || DalamudApi.TargetManager.FocusTarget == null;

            case "incombat":
                return DalamudApi.Condition != null && DalamudApi.Condition[ConditionFlag.InCombat];

            case "outcombat":
                return DalamudApi.Condition != null && !DalamudApi.Condition[ConditionFlag.InCombat];

            case "isperforming":
                return DalamudApi.Condition != null && DalamudApi.Condition[ConditionFlag.Performing];

            case "isdead":
                return (localPlayer?.CurrentHp ?? 0) == 0 || (DalamudApi.Condition != null && DalamudApi.Condition[ConditionFlag.Unconscious]);

            case "isalive":
                return (localPlayer?.CurrentHp ?? 0) > 0 && (DalamudApi.Condition == null || !DalamudApi.Condition[ConditionFlag.Unconscious]);

            case "isleader":
                return DalamudApi.PartyList != null && DalamudApi.PartyList.IsPartyLeader();

            case "inparty":
                return DalamudApi.PartyList != null && DalamudApi.PartyList.IsInParty();

            case "true":
            case "1":
                return true;

            case "false":
            case "0":
            case "null":
            case "\"\"":
            case "''":
                return false;

            default:
                // If it's a non-empty string variable value, treat non-empty as true
                var cleaned = CleanQuotes(expr);
                return !string.IsNullOrWhiteSpace(cleaned) &&
                       !cleaned.Equals("0", StringComparison.Ordinal) &&
                       !cleaned.Equals("false", StringComparison.OrdinalIgnoreCase) &&
                       !cleaned.Equals("<t>", StringComparison.OrdinalIgnoreCase);
        }
    }

    private static bool EvaluateComparison(string left, string op, string right, Plugin plugin) {
        var leftVal = ResolveValue(left);
        var rightVal = ResolveValue(right);

        bool isEqual = string.Equals(leftVal, rightVal, StringComparison.OrdinalIgnoreCase) ||
                       (leftVal.Length > 0 && rightVal.Length > 0 &&
                        (leftVal.Contains(rightVal, StringComparison.OrdinalIgnoreCase) ||
                         rightVal.Contains(leftVal, StringComparison.OrdinalIgnoreCase)));

        // If either side was explicitly quoted empty string (""), compare exact emptiness
        if ((left.Equals("\"\"", StringComparison.Ordinal) || left.Equals("''", StringComparison.Ordinal) ||
             right.Equals("\"\"", StringComparison.Ordinal) || right.Equals("''", StringComparison.Ordinal))) {
            isEqual = string.IsNullOrEmpty(leftVal) == string.IsNullOrEmpty(rightVal);
        }

        return op switch {
            "==" => isEqual,
            "!=" => !isEqual,
            _ => false,
        };
    }

    private static string ResolveValue(string token) {
        var trimmed = token.Trim();
        var lower = trimmed.ToLowerInvariant();

        var localPlayer = DalamudApi.ObjectTable?.LocalPlayer;

        if (lower is "target" or "target.name" or "<t>") {
            return localPlayer?.TargetObject?.Name.TextValue ?? string.Empty;
        }

        if (lower is "focustarget" or "focustarget.name" or "<f>") {
            return DalamudApi.TargetManager?.FocusTarget?.Name.TextValue ?? string.Empty;
        }

        if (lower is "me" or "self" or "<me>") {
            return localPlayer?.Name.TextValue ?? string.Empty;
        }

        if (lower is "job" or "class") {
            return (DalamudApi.PlayerState != null && DalamudApi.PlayerState.IsLoaded)
                ? DalamudApi.PlayerState.ClassJob.Value.Abbreviation.ToString()
                : string.Empty;
        }

        return CleanQuotes(trimmed);
    }

    private static string CleanQuotes(string s) {
        s = s.Trim();
        if (s.Length >= 2 && ((s.StartsWith('"') && s.EndsWith('"')) || (s.StartsWith('\'') && s.EndsWith('\'')))) {
            return s[1..^1];
        }
        return s;
    }

    private static List<string> SplitTopLevel(string text, string[] delimiters) {
        var result = new List<string>();
        int depth = 0;
        int lastIndex = 0;

        for (int i = 0; i < text.Length; i++) {
            char c = text[i];
            if (c == '(') depth++;
            else if (c == ')') depth--;
            else if (depth == 0) {
                foreach (var delim in delimiters) {
                    if (i + delim.Length <= text.Length &&
                        string.Equals(text.Substring(i, delim.Length), delim, StringComparison.OrdinalIgnoreCase)) {
                        result.Add(text[lastIndex..i].Trim());
                        lastIndex = i + delim.Length;
                        i += delim.Length - 1;
                        break;
                    }
                }
            }
        }

        result.Add(text[lastIndex..].Trim());
        return result.Where(s => !string.IsNullOrEmpty(s)).ToList();
    }
}
