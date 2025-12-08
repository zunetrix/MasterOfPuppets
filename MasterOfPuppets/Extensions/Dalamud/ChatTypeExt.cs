using Dalamud.Game.Text;

namespace MasterOfPuppets.Extensions.Dalamud;

public static class ChatTypeExt {
    public static string ToChatPrefix(this XivChatType type) {
        return type switch {
            XivChatType.Party => "/p",
            XivChatType.FreeCompany => "/fc",

            XivChatType.Ls1 => "/l1",
            XivChatType.Ls2 => "/l2",
            XivChatType.Ls3 => "/l3",
            XivChatType.Ls4 => "/l4",
            XivChatType.Ls5 => "/l5",
            XivChatType.Ls6 => "/l6",
            XivChatType.Ls7 => "/l7",
            XivChatType.Ls8 => "/l8",

            XivChatType.CrossLinkShell1 => "/cwl1",
            XivChatType.CrossLinkShell2 => "/cwl2",
            XivChatType.CrossLinkShell3 => "/cwl3",
            XivChatType.CrossLinkShell4 => "/cwl4",
            XivChatType.CrossLinkShell5 => "/cwl5",
            XivChatType.CrossLinkShell6 => "/cwl6",
            XivChatType.CrossLinkShell7 => "/cwl7",
            XivChatType.CrossLinkShell8 => "/cwl8",

            _ => ""
        };
    }
}
