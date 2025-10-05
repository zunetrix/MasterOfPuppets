using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace MasterOfPuppets.Util;

public static class ArgumentParser {
    public static List<string> ParseChatArgs(string args) {
        var list = new List<string>();
        if (string.IsNullOrWhiteSpace(args))
            return list;

        args = args.TrimStart();

        // extract first token (command)
        var match = Regex.Match(args, @"^(\S+)\s*(.*)$", RegexOptions.Singleline);
        if (!match.Success)
            return list;

        string command = match.Groups[1].Value;
        string remainder = match.Groups[2].Value;

        list.Add(command);

        if (string.IsNullOrEmpty(remainder))
            return list;

        // check if second token is quoted
        if (remainder.StartsWith("\"")) {
            var quoteMatch = Regex.Match(remainder, "^\"([^\"]*)\"\\s*(.*)$", RegexOptions.Singleline);
            if (quoteMatch.Success) {
                string quotedArg = quoteMatch.Groups[1].Value;
                string rest = quoteMatch.Groups[2].Value;

                list.Add(quotedArg);

                if (!string.IsNullOrEmpty(rest))
                    list.Add(TranspileChatTextCommand(rest));
            } else {
                // unmatched quote fallback
                list.Add(remainder.Trim('"'));
            }
        } else {
            // second token not quoted → everything else is a single argument
            list.Add(TranspileChatTextCommand(remainder));
        }

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

    public static List<string> ParseCommandArgs(string args) {
        var list = new List<string>();
        if (string.IsNullOrWhiteSpace(args))
            return list;

        args = args.TrimStart();

        // preserve first token
        Match firstMatch = Regex.Match(args, @"^(""[^""]*""|\S+)");
        if (!firstMatch.Success)
            return list;

        string firstToken = firstMatch.Value;
        if (firstToken.StartsWith("\"") && firstToken.EndsWith("\"")) {
            firstToken = firstToken.Substring(1, firstToken.Length - 2); // remove quotes
        }
        list.Add(firstToken);

        // everything after first token
        string remainder = args.Substring(firstMatch.Length).TrimStart();
        if (!string.IsNullOrEmpty(remainder))
            list.Add(remainder); // preserve quotes

        return list;
    }
}
