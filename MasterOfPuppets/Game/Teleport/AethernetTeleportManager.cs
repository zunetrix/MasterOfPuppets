using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using Dalamud.Game.ClientState.Objects.Types;

using MasterOfPuppets.Util;

namespace MasterOfPuppets;

/// <summary>
/// Handles in-city aethernet travel via the aetheryte menu <c>TelepotTown</c> flow.
/// </summary>
internal static class AethernetTeleportManager {

    private const long RetryIntervalMs = 1000;
    private const int MaxSelectAttempts = 3;

    // flow:
    // Interact with aetheryte > (main aetheryte only) SelectString > "Aethernet." > TelepotTown > Select destination
    /// <summary>
    /// Initiates the aetheryte-menu flow to teleport to the given aethernet destination (name in the client language).
    /// Requires the player to be near an aetheryte / aethernet shard of the same city.
    /// </summary>
    public static void TeleportToAethernet(string destination, Plugin plugin) {
        if (string.IsNullOrWhiteSpace(destination)) return;

        DalamudApi.Framework.RunOnFrameworkThread(() => {
            // normalize to the sheet name when it resolves (e.g. partial input "arcan" -> "Arcanists' Guild")
            // residential shards of the current territory first, then city aethernet
            AethernetCatalog.TryResolveDestination(destination, out var target, out var rowId);

            if (rowId != 0 && AethernetCatalog.TryGetContext(out var ctx) && ctx.CurrentRowId == rowId) {
                DalamudApi.PluginLog.Debug($"[TeleportToAethernet] already at '{target}'");
                // window may have been opened by the overlay map preview
                AethernetMapPreview.HandOff();
                GameDialogManager.CloseAddon(GameDialogManager.AddonName.TelepotTown);
                return;
            }

            // window already open (overlay map preview): select directly
            if (GameDialogManager.IsAddonVisible(GameDialogManager.AddonName.TelepotTown)) {
                AethernetMapPreview.HandOff();
                Step_SelectDestination(target);
                return;
            }

            var player = DalamudApi.ObjectTable.LocalPlayer;
            if (player == null) return;

            // aetheryte or aethernet shard (residential shards may be event objects)
            var aetheryte = AethernetCatalog.GetNearestAethernetObject();

            if (aetheryte != null && !AethernetCatalog.IsInInteractRange(player.Position, aetheryte)) {
                plugin.MovementManager.MoveTo(aetheryte.GameObjectId);
                Step_WaitUntilCloseAndTargetAetheryte(target, aetheryte, plugin);
            } else {
                GameTargetManager.TargetNearestAetheryte();
                Step_WaitForAetheryteTarget(() => Step_SelectDestination(target));
            }
        });
    }

    /// <summary>
    /// Opens the native <c>TelepotTown</c> window of the aetheryte in range without selecting a destination
    /// (target > interact > "Aethernet."). <paramref name="onOpened"/> runs once the window is visible.
    /// </summary>
    public static void OpenAethernetWindow(Action? onOpened = null, CancellationToken cancellationToken = default) {
        DalamudApi.Framework.RunOnFrameworkThread(() => {
            if (cancellationToken.IsCancellationRequested) return;
            if (GameDialogManager.IsAddonVisible(GameDialogManager.AddonName.TelepotTown)) {
                onOpened?.Invoke();
                return;
            }
            GameTargetManager.TargetNearestAetheryte();
            Step_WaitForAetheryteTarget(() => onOpened?.Invoke(), cancellationToken);
        });
    }

    private static void Step_WaitUntilCloseAndTargetAetheryte(string destination, IGameObject aetheryte, Plugin plugin) {
        var ok = false;
        Coroutine.StartRunOnFramework(
            runFunction: () => { },
            stopWhen: () => {
                var player = DalamudApi.ObjectTable.LocalPlayer;
                if (player == null || aetheryte == null) return false;
                ok = AethernetCatalog.IsInInteractRange(player.Position, aetheryte);
                return ok;
            },
            callback: () => {
                plugin.MovementManager.StopMove();
                if (!ok) { DalamudApi.PluginLog.Warning("[TeleportToAethernet] move timeout: aetheryte still out of interact range"); return; }
                GameTargetManager.TargetNearestAetheryte();
                Step_WaitForAetheryteTarget(() => Step_SelectDestination(destination));
            },
            timeoutMs: 30000);
    }

    //  Step 1
    //  Wait until the nearest aetheryte is targeted, then interact.
    private static void Step_WaitForAetheryteTarget(Action onWindowOpen, CancellationToken cancellationToken = default) {
        var ok = false;
        Coroutine.StartRunOnFramework(
            runFunction: () => { },
            stopWhen: () => ok = AethernetCatalog.IsAethernetObject(DalamudApi.TargetManager.Target),
            callback: () => {
                if (cancellationToken.IsCancellationRequested) return;
                if (!ok) { DalamudApi.PluginLog.Warning("[TeleportToAethernet] step1 timeout: no aetheryte targeted"); return; }
                GameTargetManager.InteractWithTarget();
                Step_WaitForAethernetMenu(onWindowOpen, cancellationToken);
            },
            timeoutMs: 3000,
            cancellationToken: cancellationToken);
    }

    //  Step 2
    //  Main aetheryte opens SelectString first ("Aethernet."), aethernet shards open TelepotTown directly.
    //  Residential menus have other entries: match by text only
    private static void Step_WaitForAethernetMenu(Action onWindowOpen, CancellationToken cancellationToken = default) {
        var ok = false;
        var fallbackIndex = AethernetCatalog.IsInResidentialZone() ? -1 : AetheryteMenuText.IndexAethernet;
        Coroutine.StartRunOnFramework(
            runFunction: () => {
                if (GameDialogManager.IsAddonVisible(GameDialogManager.AddonName.SelectString))
                    GameDialogManager.SelectStringByTextOrIndex(AetheryteMenuText.Aethernet, fallbackIndex);
            },
            stopWhen: () => ok = GameDialogManager.IsAddonVisible(GameDialogManager.AddonName.TelepotTown),
            callback: () => {
                if (cancellationToken.IsCancellationRequested) return;
                if (!ok) { DalamudApi.PluginLog.Warning("[TeleportToAethernet] step2 timeout: TelepotTown not visible"); return; }
                onWindowOpen();
            },
            timeoutMs: 7000,
            cancellationToken: cancellationToken);
    }

    //  Step 3
    //  Wait for TelepotTown destinations, fire the destination callback, retry until the addon closes.
    //  Ls: Callback.Fire(telep, true, 11, callbackData)
    private static void Step_SelectDestination(string destination) {
        var ok = false;
        var attempts = 0;
        long lastAttempt = 0;
        GameDialogManager.AethernetDestination? match = null;

        Coroutine.StartRunOnFramework(
            runFunction: () => {
                if (!GameDialogManager.IsAddonVisible(GameDialogManager.AddonName.TelepotTown)) return;

                if (match == null) {
                    var destinations = GameDialogManager.GetTelepotTownDestinations();
                    if (destinations.Count == 0) return;

                    match = FindDestination(destinations, destination);
                    if (match == null) {
                        DalamudApi.PluginLog.Warning($"[TeleportToAethernet] '{destination}' not available. Found: [{string.Join(", ", destinations.Select(d => d.Name))}]");
                        GameDialogManager.CloseAddon(GameDialogManager.AddonName.TelepotTown);
                        attempts = MaxSelectAttempts;
                        return;
                    }
                }

                var now = Environment.TickCount64;
                if (attempts >= MaxSelectAttempts || now - lastAttempt < RetryIntervalMs) return;
                lastAttempt = now;
                attempts++;
                GameDialogManager.SelectTelepotTownDestination(match.Value.CallbackData);
            },
            stopWhen: () => ok = !GameDialogManager.IsAddonVisible(GameDialogManager.AddonName.TelepotTown),
            callback: () => {
                if (!ok) {
                    DalamudApi.PluginLog.Warning($"[TeleportToAethernet] step3 timeout: '{destination}' not selected");
                    GameDialogManager.CloseAddon(GameDialogManager.AddonName.TelepotTown);
                }
            },
            timeoutMs: 6000);
    }

    private static GameDialogManager.AethernetDestination? FindDestination(List<GameDialogManager.AethernetDestination> destinations, string name) {
        foreach (var d in destinations)
            if (d.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) return d;
        foreach (var d in destinations)
            if (d.Name.Contains(name, StringComparison.OrdinalIgnoreCase) || name.Contains(d.Name, StringComparison.OrdinalIgnoreCase)) return d;
        return null;
    }
}
