using System.Text;

using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;

namespace MasterOfPuppets.Extensions.Dalamud;

public static class ChatGuiExtensions {

    public static void PrintPluginMessage(this IChatGui chat, string msg)
         => chat.Print(new XivChatEntry {
             Type = XivChatType.Echo,
             Message = new SeStringBuilder()
                 .AddUiForeground($"[Mop] ", 45)
                 .AddText(msg)
                 .Build()
         });

    public static void PrintPluginMessage(this IChatGui chat, SeString msg) {
        chat.Print(new XivChatEntry {
            Type = XivChatType.Echo,
            Message = new SeStringBuilder()
            .AddUiForeground($"[Mop] ", 45)
            .Append(msg)
            .Build()
        });
    }

    public static void PrintPluginMessage(this IChatGui chat, SeStringBuilder sb) => chat.PrintPluginMessage(sb.BuiltString);

    public static void PrintPluginMessage(this IChatGui chat, StringBuilder? sb) {
        if (sb != null)
            chat.PrintPluginMessage(sb.ToString());
    }
}
