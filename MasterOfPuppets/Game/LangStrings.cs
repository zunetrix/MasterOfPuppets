using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace MasterOfPuppets;

public static class Langstrings {
    public static readonly List<Regex> LfgPatterns =
    [
        new Regex(@"Join .* party\?"),
        new Regex(@".*のパーティに参加します。よろしいですか？"),
        new Regex(@"Der Gruppe von .* beitreten\?"),
        new Regex(@"Rejoindre l'équipe de .*\?")
    ];

    public static readonly List<Regex> LeavePartyPatterns =
    [
        new Regex(@"Die Gruppe verlassen\?"),
        new Regex(@"Leave party\?")
    ];

    public static readonly List<Regex> PromotePatterns =
    [
        new Regex(@"Promote .* to party leader\?"),
        new Regex(@".* zum Gruppenanführer machen\?"),
        new Regex(@"Promouvoir .* au rang de chef d'équipe \?")
    ];

    internal static readonly List<Regex> Entrance =
    [
        new Regex(@"ハウスへ入る"),
        new Regex(@"进入房屋"),
        new Regex(@"進入房屋"),
        new Regex(@"Eingang"),
        new Regex(@"Entrance"),
        new Regex(@"Entrée")
    ];

    internal static readonly List<Regex> ConfirmHouseEntrance =
    [
        new Regex(@"「ハウス」へ入りますか\？"),
        new Regex(@"要进入这间房屋吗\？"),
        new Regex(@"要進入這間房屋嗎\？"),
        new Regex(@"Das Gebäude betreten\?"),
        new Regex(@"Entrer dans la maison \?"),
        new Regex(@"Enter the estate hall\?")
    ];

    internal static readonly List<Regex> ConfirmGroupTeleport =
    [
        new Regex(@"Accept Teleport to .*\?"),
        new Regex(@"Zum Ätheryten .* teleportieren lassen\?"),
        new Regex(@"Voulez-vous vous téléporter vers la destination .* \?")

    ];

    internal static readonly List<Regex> ConfirmLogout =
    [
        new Regex(@"Zum Titelbildschirm zurückkehren\?"),
        new Regex(@"Se déconnecter et retourner à l'écram titre \?"),
        new Regex(@"Log out and return to the title screen\?")
    ];

    internal static readonly List<Regex> ConfirmShutdown =
    [
        new Regex(@"Das Spiel beenden\?"),
        new Regex(@"Se déconnecter et quitter le jeu \?"),
        new Regex(@"Log out and exit the game\?")
    ];
}
