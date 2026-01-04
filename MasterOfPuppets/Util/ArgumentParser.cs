using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace MasterOfPuppets.Util;

public static class ArgumentParser {
    // public static List<string> ParseChatArgs(string args) {
    //     var list = new List<string>();
    //     if (string.IsNullOrWhiteSpace(args))
    //         return list;

    //     args = args.TrimStart();

    //     // extract first token (command)
    //     var match = Regex.Match(args, @"^(\S+)\s*(.*)$", RegexOptions.Singleline);
    //     if (!match.Success)
    //         return list;

    //     string command = match.Groups[1].Value;
    //     string remainder = match.Groups[2].Value;

    //     list.Add(command);

    //     if (string.IsNullOrEmpty(remainder))
    //         return list;

    //     // check if second token is quoted
    //     if (remainder.StartsWith("\"")) {
    //         var quoteMatch = Regex.Match(remainder, "^\"([^\"]*)\"\\s*(.*)$", RegexOptions.Singleline);
    //         if (quoteMatch.Success) {
    //             string quotedArg = quoteMatch.Groups[1].Value;
    //             string rest = quoteMatch.Groups[2].Value;

    //             list.Add(quotedArg);

    //             if (!string.IsNullOrEmpty(rest))
    //                 list.Add(TranspileChatTextCommand(rest));
    //         } else {
    //             // unmatched quote fallback
    //             list.Add(remainder.Trim('"'));
    //         }
    //     } else {
    //         // second token not quoted > everything else is a single argument
    //         list.Add(TranspileChatTextCommand(remainder));
    //     }

    //     return list;
    // }

    public static List<string> ParseChatArgs(string args) {
        var list = new List<string>();
        if (string.IsNullOrWhiteSpace(args))
            return list;

        args = args.TrimStart();

        // command token
        var match = Regex.Match(args, @"^(\S+)\s*(.*)$", RegexOptions.Singleline);
        if (!match.Success)
            return list;

        string command = match.Groups[1].Value;
        string remainder = match.Groups[2].Value ?? string.Empty;

        list.Add(command);

        if (string.IsNullOrEmpty(remainder))
            return list;

        // quoted content
        var quoteMatch = Regex.Match(remainder, "^\"([^\"]*)\"(?:\\s+(.*))?$", RegexOptions.Singleline);
        if (quoteMatch.Success) {
            // text
            var quotedArg = quoteMatch.Groups[1].Value;
            list.Add(quotedArg);

            // rest
            if (quoteMatch.Groups.Count >= 3) {
                var rest = quoteMatch.Groups[2].Success ? quoteMatch.Groups[2].Value.TrimStart() : string.Empty;
                if (!string.IsNullOrEmpty(rest))
                    list.Add(TranspileChatTextCommand(rest));
            }

            return list;
        }

        list.Add(TranspileChatTextCommand(remainder));
        return list;
    }

    public static string TranspileChatTextCommand(string textCommand) {
        // string[] tokens = { "[1]", "[2]", "[3]", "[4]", "[5]", "[6]", "[7]", "[8]", "[t]", "[me]", "[tt]" };
        // replace to the original game tokens that canot be sent via chat <me> will be translated to the char name
        return Regex.Replace(
            textCommand,
            @"\[(1|2|3|4|5|6|7|8|t|me|tt)\]",
            m => $"<{m.Groups[1].Value}>",
            RegexOptions.IgnoreCase
        );
    }

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
                // split 2 parts
                var parts = input.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
                result.AddRange(parts);
                return result;
            }

            // 2+ spaces
            result.Add(input);
            return result;
        }

        // Tokenize: quoted or unquoted, preserving quoted sections
        var rawMatches = Regex.Matches(input, "\"[^\"]*\"|[^ ]+");
        var tokens = new List<string>();
        foreach (Match m in rawMatches) {
            string token = m.Value;
            if (token.StartsWith("\"") && token.EndsWith("\"") && token.Length >= 2)
                token = token.Substring(1, token.Length - 2);
            tokens.Add(token);
        }

        // another command (starting with /)everything from that token onward should be a single part (keeping quotes)
        int slashIndex = tokens.FindIndex(t => t.StartsWith("/"));
        if (slashIndex != -1) {
            // rebuild original input to preserve quotes
            var matchesCollection = Regex.Matches(input, "\"[^\"]*\"|[^ ]+");
            var slashMatch = matchesCollection[slashIndex];
            string rest = input.Substring(slashMatch.Index).TrimStart();

            tokens = tokens.Take(slashIndex).ToList();
            tokens.Add(rest); // keep quotes as-is
        }

        result.AddRange(tokens);
        return result;
    }

    // parse args and preserve quoted content as single arg
    public static List<string> ParseMacroArgs(string args) {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(args))
            return result;

        var sb = new StringBuilder();
        bool inQuotes = false;

        foreach (char c in args) {
            if (c == '"') {
                inQuotes = !inQuotes; // double quote
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
