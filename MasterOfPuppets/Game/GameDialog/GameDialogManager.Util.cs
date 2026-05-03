using System;
using System.Collections.Generic;

using FFXIVClientStructs.FFXIV.Component.GUI;

using Lumina.Text.ReadOnly;
namespace MasterOfPuppets;

// from: Lifestream Utils
internal static unsafe partial class GameDialogManager {

    internal static string[] GetAvailableWorldDestinations() {
        if (TryGetAddonByName<AtkUnitBase>("WorldTravelSelect", out var addon) && IsAddonReady(addon)) {
            List<string> arr = [];
            for (var i = 3; i <= 9; i++) {
                var item = addon->UldManager.NodeList[4]->GetAsAtkComponentNode()->Component->UldManager.NodeList[i];
                var text = new ReadOnlySeStringSpan(item->GetAsAtkComponentNode()->Component->UldManager.NodeList[4]->GetAsAtkTextNode()->NodeText.AsSpan()).ExtractText();
                if (text == "") break;
                arr.Add(text);
            }
            return [.. arr];
        }
        return Array.Empty<string>();
    }

    internal static string[] GetAvailableAethernetDestinations() {
        if (TryGetAddonByName<AtkUnitBase>("TelepotTown", out var addon) && IsAddonReady(addon)) {
            List<string> arr = [];
            for (var i = 1; i <= 52; i++) {
                var item = addon->UldManager.NodeList[16]->GetAsAtkComponentNode()->Component->UldManager.NodeList[i];
                var text = new ReadOnlySeStringSpan(item->GetAsAtkComponentNode()->Component->UldManager.NodeList[3]->GetAsAtkTextNode()->NodeText.AsSpan()).ExtractText().Trim();
                if (text == "") break;
                arr.Add(text);
            }
            return [.. arr];
        }
        return Array.Empty<string>();
    }

    // internal static bool TrySelectSpecificEntry(string text, Func<bool> Throttle) {
    //     return TrySelectSpecificEntry(new string[] { text }, Throttle);
    // }

    // internal static bool TrySelectSpecificEntry(IEnumerable<string> text, Func<bool> Throttle) {
    //     if (TryGetAddonByName<AddonSelectString>("SelectString", out var addon) && IsAddonReady(&addon->AtkUnitBase)) {
    //         var entry = GetEntries(addon).FirstOrDefault(x => x.EqualsAny(text));
    //         if (entry != null) {
    //             var index = GetEntries(addon).IndexOf(entry);
    //             if (index >= 0 && Throttle()) {
    //                 FireCallback("SelectString", index);
    //                 DalamudApi.PluginLog.Debug($"TrySelectSpecificEntry: selecting {entry}/{index} as requested by {string.Join(", ", text)}");
    //                 return true;
    //             }
    //         }
    //     }
    //     return false;
    // }

    // internal static List<string> GetEntries(AddonSelectString* addon) {
    //     var list = new List<string>();
    //     for (var i = 0; i < addon->PopupMenu.PopupMenu.EntryCount; i++) {
    //         var ptr = addon->PopupMenu.PopupMenu.EntryNames[i].Value;
    //         if (ptr == null) continue;
    //         var text = new ReadOnlySeStringSpan(ptr).ExtractText().Trim();
    //         if (string.IsNullOrEmpty(text)) continue;
    //         list.Add(text);
    //     }
    //     return list;
    // }
}
