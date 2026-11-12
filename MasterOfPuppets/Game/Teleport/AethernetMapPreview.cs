using System;
using System.Threading;

using FFXIVClientStructs.FFXIV.Client.UI.Agent;

using MasterOfPuppets.Util;

namespace MasterOfPuppets;

/// <summary>
/// Based on HaselTweaks SimpleAethernetList.
/// Opens the native aethernet window (<c>TelepotTown</c>) while the overlay Aethernet submenu is open and
/// moves its map to the destination hovered in the overlay. Local only (never broadcast).
/// </summary>
internal static unsafe class AethernetMapPreview {

    private const int CleanupTimeoutMs = 2000;

    private static CancellationTokenSource? _openCts;
    private static CancellationTokenSource? _cleanupCts;

    /// <summary>True while the native window is (being) opened by the preview and should be closed with it.</summary>
    private static bool _owned;

    private static string? _previewedName;

    public static bool IsActive => _owned;

    /// <summary>Interacts with the aetheryte in range to open <c>TelepotTown</c> (no-op when already open).</summary>
    public static void Open() {
        if (_owned) return;
        _cleanupCts?.Cancel();
        _cleanupCts = null;

        _previewedName = null;
        if (GameDialogManager.IsAddonVisible(GameDialogManager.AddonName.TelepotTown)) return; // opened by the user: leave it alone

        _owned = true;
        _openCts = new CancellationTokenSource();
        AethernetTeleportManager.OpenAethernetWindow(cancellationToken: _openCts.Token);
    }

    /// <summary>Closes the native window if the preview opened it (also when it is still opening).</summary>
    public static void Close() {
        RestoreSelection();
        _previewedName = null;
        if (!_owned) return;
        _owned = false;

        _openCts?.Cancel();
        _openCts = null;

        // the interaction may still be pending: close whichever aetheryte menu shows up for a short while
        _cleanupCts = new CancellationTokenSource();
        Coroutine.StartRunOnFramework(
            runFunction: () => {
                if (GameDialogManager.IsAddonVisible(GameDialogManager.AddonName.TelepotTown))
                    GameDialogManager.CloseAddon(GameDialogManager.AddonName.TelepotTown);
                else if (GameDialogManager.IsAddonVisible(GameDialogManager.AddonName.SelectString))
                    GameDialogManager.CloseAddon(GameDialogManager.AddonName.SelectString);
            },
            timeoutMs: CleanupTimeoutMs,
            cancellationToken: _cleanupCts.Token);
    }

    /// <summary>A travel took over the open window: keep it open (the travel flow closes it).</summary>
    public static void HandOff() {
        _owned = false;
        _previewedName = null;
        _openCts?.Cancel();
        _openCts = null;
        _cleanupCts?.Cancel();
        _cleanupCts = null;
    }

    /// <summary>Call every frame while the submenu is open with the hovered destination (null = none).</summary>
    public static void Update(string? hoveredName) {
        if (hoveredName == _previewedName) return;

        if (hoveredName == null) {
            RestoreSelection();
            _previewedName = null;
            return;
        }

        if (Select(hoveredName))
            _previewedName = hoveredName;
    }

    private static bool Select(string name) {
        if (!GameDialogManager.IsAddonVisible(GameDialogManager.AddonName.TelepotTown)) return false;

        uint? index = null;
        foreach (var d in GameDialogManager.GetTelepotTownDestinations()) {
            if (d.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) { index = d.CallbackData; break; }
        }
        if (index == null) return false;

        var agent = AgentTelepotTown.Instance();
        if (agent == null || agent->Data == null) return false;

        agent->Data->SelectedAetheryte = (byte)index.Value;
        agent->Data->Flags |= 2; // refresh map
        return true;
    }

    private static void RestoreSelection() {
        if (_previewedName == null) return;
        if (!GameDialogManager.IsAddonVisible(GameDialogManager.AddonName.TelepotTown)) return;

        var agent = AgentTelepotTown.Instance();
        if (agent == null || agent->Data == null) return;

        agent->Data->SelectedAetheryte = agent->Data->CurrentAetheryte;
        agent->Data->Flags |= 2;
    }
}
