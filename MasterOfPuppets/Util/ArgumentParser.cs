using System.Linq;
using System.Collections.Generic;
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

        // no quote/subcommand treat as a single argument
        if (!input.Contains('"') && !input.Contains('/')) {
            result.Add(input);
            return result;
        }

        // Split tokens: quoted or unquoted, preserving quoted sections
        var tokens = new List<string>();
        var matches = Regex.Matches(input, @"[\""].+?[\""]|[^ ]+");

        foreach (Match m in matches) {
            string token = m.Value;
            if (token.StartsWith("\"") && token.EndsWith("\""))
                token = token.Substring(1, token.Length - 2);
            tokens.Add(token);
        }

        // another command (starting with /)everything from that token onward should be a single part (keeping quotes)
        int slashIndex = tokens.FindIndex(t => t.StartsWith("/"));
        if (slashIndex != -1) {
            // rebuild original input to preserve quotes
            var slashMatch = Regex.Matches(input, @"[\""].+?[\""]|[^ ]+")[slashIndex];
            string rest = input.Substring(slashMatch.Index).TrimStart();

            tokens = tokens.Take(slashIndex).ToList();
            tokens.Add(rest); // keep quotes as-is
        }

        result.AddRange(tokens);
        return result;
    }
}
