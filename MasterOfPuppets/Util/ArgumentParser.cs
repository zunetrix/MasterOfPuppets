using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace MasterOfPuppets.Util;

public static class ArgumentParser {
    private static readonly Regex TokenRegex =
        new("\"[^\"]*\"|[^ ]+", RegexOptions.Compiled);

    private static readonly Regex InlineVarRegex =
        new(@"\$(?<name>[A-Za-z_]\w*)\s*=\s*(?<value>""[^""]*""|'[^']*'|[^;$\s]+)",
            RegexOptions.Compiled);

    /// <summary>
    /// Scans for the first occurrence of ` --` (space + double-dash) outside
    /// double-quoted groups. Returns the index of the space, or -1 if not found.
    /// </summary>
    private static int IndexOfFlagsStart(string input) {
        bool inQuotes = false;
        for (int i = 0; i < input.Length - 2; i++) {
            if (input[i] == '"') { inQuotes = !inQuotes; continue; }
            if (!inQuotes && input[i] == ' ' && input[i + 1] == '-' && input[i + 2] == '-')
                return i;
        }
        return -1;
    }

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
    /// raw token (preserving inner quotes for nested game commands). Any `--flags` token
    /// (starting with `--` outside quotes) is extracted as one raw trailing token.
    ///
    /// Fast path (no quotes, no slash, no double-dash):
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
    ///   "run \"My Macro\" --var=$x=1;$y=2"              -> ["run", "My Macro", "--var=$x=1;$y=2"]
    /// </summary>
    public static List<string> ParseCommandArgs(string input) {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(input))
            return result;

        input = input.Trim();

        // Extract any --flags portion as a single raw trailing token before tokenization.
        string? flagsToken = null;
        int flagsIdx = IndexOfFlagsStart(input);
        if (flagsIdx >= 0) {
            flagsToken = input[(flagsIdx + 1)..];
            input = input[..flagsIdx];
        }

        if (!input.Contains('"') && !input.Contains('/')) {
            int spaceCount = input.Count(c => c == ' ');
            if (spaceCount == 0) {
                result.Add(input);
                if (flagsToken != null) result.Add(flagsToken);
                return result;
            }
            if (spaceCount == 1) {
                result.AddRange(input.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries));
                if (flagsToken != null) result.Add(flagsToken);
                return result;
            }

            // 2+ spaces without quotes or slash: treat entire input as single broadcast argument
            result.Add(input);
            if (flagsToken != null) result.Add(flagsToken);
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
        if (flagsToken != null) result.Add(flagsToken);
        return result;
    }

    /// <summary>
    /// Parses inline variable overrides from a <c>--var=</c> flags token into a dictionary.
    /// Pairs are semicolon-separated: <c>$name=value</c> where quoted values preserve spaces.
    /// Keys are returned without the leading <c>$</c> (consistent with variable extraction).
    ///
    /// Example: <c>--var=$emote=/clap;$delay=0.5;$target="Character Name"</c>
    ///   -> <c>{ "emote": "/clap", "delay": "0.5", "target": "Character Name" }</c>
    /// </summary>
    public static Dictionary<string, string> ParseInlineVars(string flagsToken) {
        const string prefix = "--var=";
        if (!flagsToken.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return new Dictionary<string, string>();

        var vars = new Dictionary<string, string>();
        foreach (Match m in InlineVarRegex.Matches(flagsToken[prefix.Length..])) {
            var name = m.Groups["name"].Value;
            var value = m.Groups["value"].Value;
            if ((value.StartsWith('"') && value.EndsWith('"')) ||
                (value.StartsWith('\'') && value.EndsWith('\'')))
                value = value[1..^1];
            vars[name] = value;
        }
        return vars;
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
