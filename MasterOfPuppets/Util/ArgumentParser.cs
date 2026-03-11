using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace MasterOfPuppets.Util;

public static class ArgumentParser {
    private static readonly Regex TokenRegex =
        new("\"[^\"]*\"|[^ ]+", RegexOptions.Compiled);

    /// <summary>
    /// Parses a full chat message into tokens, delegating to ParseCommandArgs
    /// and applying FFXIV token transpilation ([t] -> &lt;t&gt;, [me] -> &lt;me&gt;, etc.)
    /// to all argument tokens (everything after the command).
    ///
    /// Examples:
    ///   "moprun \"My Macro\""              -> ["moprun", "My Macro"]
    ///   "mopbr /ac heal [t]"               -> ["mopbr", "/ac heal <t>"]
    ///   "mopbrc \"Character Name\" /clap"  -> ["mopbrc", "Character Name", "/clap"]
    /// </summary>
    public static List<string> ParseChatArgs(string input) {
        var result = ParseCommandArgs(input);
        for (int i = 1; i < result.Count; i++)
            result[i] = TranspileChatTextCommand(result[i]);
        return result;
    }

    /// <summary>
    /// Replaces FFXIV chat macro tokens from bracket notation to angle bracket notation,
    /// e.g. [t] -> &lt;t&gt;, [me] -> &lt;me&gt;, [tt] -> &lt;tt&gt;, [1]..[8] -> &lt;1&gt;..&lt;8&gt;.
    /// Used so commands typed in chat can include target placeholders.
    /// </summary>
    public static string TranspileChatTextCommand(string textCommand) {
        return Regex.Replace(
            textCommand,
            @"\[(1|2|3|4|5|6|7|8|t|me|tt)\]",
            m => $"<{m.Groups[1].Value}>",
            RegexOptions.IgnoreCase
        );
    }

    /// <summary>
    /// Parses a command argument string into tokens, respecting double-quoted groups
    /// and treating everything from the first slash-prefixed token onwards as a single
    /// raw token (preserving inner quotes for nested game commands).
    ///
    /// Fast path (no quotes, no slash):
    ///   0 spaces  -> single token:  "mopstop"           -> ["mopstop"]
    ///   1 space   -> two tokens:    "run 1"              -> ["run", "1"]
    ///   2+ spaces -> single token:  "text with spaces"   -> ["text with spaces"]
    ///     (2+ spaces without quotes is treated as a single broadcast argument)
    ///
    /// Full tokenization:
    ///   "run \"Macro Name\""                            -> ["run", "Macro Name"]
    ///   "\"Character Name\" /clap"                      -> ["Character Name", "/clap"]
    ///   "\"Character Name\" /moptarget \"Name2\""       -> ["Character Name", "/moptarget \"Name2\""]
    ///   "mopbr /ac heal [t]"                            -> ["mopbr", "/ac heal [t]"]
    /// </summary>
    public static List<string> ParseCommandArgs(string input) {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(input))
            return result;

        input = input.Trim();

        if (!input.Contains('"') && !input.Contains('/')) {
            int spaceCount = input.Count(c => c == ' ');
            if (spaceCount == 0) {
                result.Add(input);
                return result;
            }
            if (spaceCount == 1) {
                result.AddRange(input.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries));
                return result;
            }

            // 2+ spaces without quotes or slash: treat entire input as single broadcast argument
            result.Add(input);
            return result;
        }

        var rawMatches = TokenRegex.Matches(input);
        var tokens = new List<string>();
        foreach (Match m in rawMatches) {
            string token = m.Value;
            if (token.StartsWith("\"") && token.EndsWith("\"") && token.Length >= 2)
                token = token[1..^1];
            tokens.Add(token);
        }

        // Everything from the first slash-prefixed token onwards becomes a single raw token,
        // preserving inner quotes so nested commands like /moptarget "Name" are passed intact.
        int slashIndex = tokens.FindIndex(t => t.StartsWith("/"));
        if (slashIndex != -1) {
            string rest = input[rawMatches[slashIndex].Index..].TrimStart();
            tokens = tokens.Take(slashIndex).ToList();
            tokens.Add(rest);
        }

        result.AddRange(tokens);
        return result;
    }

    /// <summary>
    /// Parses a macro argument string by splitting on whitespace while preserving
    /// double-quoted groups as single tokens (quotes are stripped from the result).
    /// Used for macro commands like /mopmove, /mopmoverelativeto where arguments
    /// may contain spaces (e.g. character names or coordinate groups).
    ///
    /// Examples:
    ///   "10.01 11.02 12.03"             -> ["10.01", "11.02", "12.03"]
    ///   "\"Character Name\" 1.0 2.0"    -> ["Character Name", "1.0", "2.0"]
    /// </summary>
    public static List<string> ParseMacroArgs(string args) {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(args))
            return result;

        var sb = new StringBuilder();
        bool inQuotes = false;

        foreach (char c in args) {
            if (c == '"') {
                inQuotes = !inQuotes;
                continue;
            }

            if (char.IsWhiteSpace(c) && !inQuotes) {
                if (sb.Length > 0) {
                    result.Add(sb.ToString());
                    sb.Clear();
                }
            } else {
                sb.Append(c);
            }
        }

        if (sb.Length > 0)
            result.Add(sb.ToString());

        return result;
    }
}
