using System;

using Dalamud.Hooking;

using FFXIVClientStructs.FFXIV.Client.Game.Control;

namespace MasterOfPuppets;

/// <summary>
/// Observes the game's native emote playback function. Short emotes such as
/// Wave can begin and clear between actor-state samples, so this edge is the
/// authoritative discovery path while watches remain responsible for loop
/// state and stop transitions.
/// </summary>
internal sealed class EmoteObserver : IDisposable {
    private unsafe delegate bool PlayEmoteDelegate(
        EmoteController* controller,
        uint emoteId,
        EmoteController.PlayEmoteOption* option);

    private Hook<PlayEmoteDelegate>? _hook;

    public event Action<EmoteObservation>? EmotePlayed;
    public bool IsAvailable => _hook != null;

    public unsafe EmoteObserver() {
        var address = (nint)EmoteController.MemberFunctionPointers.PlayEmote;
        if (address == 0) {
            DalamudApi.PluginLog.Error("[EmoteObserver] EmoteController.PlayEmote was unresolved; emote mirroring is unavailable");
            return;
        }
        try {
            _hook = DalamudApi.GameInteropProvider.HookFromAddress<PlayEmoteDelegate>(address, PlayEmoteDetour);
            _hook.Enable();
            DalamudApi.PluginLog.Information($"[EmoteObserver] native emote hook enabled at 0x{address:X}");
        } catch (Exception ex) {
            _hook?.Dispose();
            _hook = null;
            DalamudApi.PluginLog.Error(ex, "[EmoteObserver] failed to install native emote hook");
        }
    }

    private unsafe bool PlayEmoteDetour(
        EmoteController* controller,
        uint emoteId,
        EmoteController.PlayEmoteOption* option) {
        var owner = controller == null ? null : controller->OwnerObject;
        var sourceEntityId = owner == null ? 0u : owner->Character.EntityId;
        var targetId = option == null ? 0UL : (ulong)option->TargetId;
        var result = _hook!.Original(controller, emoteId, option);
        if (result && sourceEntityId != 0 && emoteId != 0) {
            try {
                EmotePlayed?.Invoke(new EmoteObservation(
                    sourceEntityId,
                    emoteId,
                    targetId,
                    EmoteHelper.IsPersistent(emoteId),
                    DateTimeOffset.UtcNow));
            } catch (Exception ex) {
                DalamudApi.PluginLog.Warning(ex, "[EmoteObserver] observer callback failed");
            }
        }
        return result;
    }

    public void Dispose() {
        EmotePlayed = null;
        _hook?.Disable();
        _hook?.Dispose();
        _hook = null;
    }
}

internal sealed record EmoteObservation(
    uint SourceEntityId,
    uint EmoteId,
    ulong TargetId,
    bool IsPersistent,
    DateTimeOffset Timestamp);
