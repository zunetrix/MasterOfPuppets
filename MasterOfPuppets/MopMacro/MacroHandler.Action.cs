using System;
using System.Threading;
using System.Threading.Tasks;

namespace MasterOfPuppets;

public partial class MacroHandler {
    private Task HandleMopAction(string macroId, string args, CancellationToken token) {
        var actionIdOrName = args.Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(actionIdOrName)) {
            DalamudApi.PluginLog.Warning($"[mopaction] invalid argument: \"{actionIdOrName}\"");
            return Task.CompletedTask;
        }

        if (uint.TryParse(actionIdOrName, out uint actionId)) {
            GameActionManager.UseAction(actionId);
            DalamudApi.PluginLog.Debug($"[mopaction] {actionId}");
        } else {
            GameActionManager.UseAction(actionIdOrName);
            DalamudApi.PluginLog.Debug($"[mopaction] {actionIdOrName}");
        }

        return Task.CompletedTask;
    }

    private Task HandleMopItem(string macroId, string args, CancellationToken token) {
        var itemIdOrName = args.Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(itemIdOrName)) {
            DalamudApi.PluginLog.Warning($"[mopitem] invalid argument: \"{itemIdOrName}\"");
            return Task.CompletedTask;
        }

        if (uint.TryParse(itemIdOrName, out uint itemId)) {
            GameActionManager.UseItem(itemId);
            DalamudApi.PluginLog.Debug($"[mopitem] {itemId}");
        } else {
            GameActionManager.UseItem(itemIdOrName);
            DalamudApi.PluginLog.Debug($"[mopitem] {itemIdOrName}");
        }

        return Task.CompletedTask;
    }

    private Task HandleMopPetBarSlot(string macroId, string args, CancellationToken token) {
        if (string.IsNullOrWhiteSpace(args) || !int.TryParse(args, out int slotIndex)) {
            DalamudApi.PluginLog.Warning($"[moppetbarslot] invalid argument: \"{args}\"");
            return Task.CompletedTask;
        }

        HotbarManager.ExecutePetHotbarActionByIndex((uint)(slotIndex - 1));
        DalamudApi.PluginLog.Debug($"[moppetbarslot] {slotIndex}");
        return Task.CompletedTask;
    }

    private Task HandleMopHotbar(string macroId, string args, CancellationToken token) {
        if (string.IsNullOrWhiteSpace(args)) {
            DalamudApi.PluginLog.Warning("[mophotbar] missing arguments");
            return Task.CompletedTask;
        }

        var parts = args.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) {
            DalamudApi.PluginLog.Warning($"[mophotbar] expected 2 arguments, got {parts.Length}: \"{args}\"");
            return Task.CompletedTask;
        }

        if (!int.TryParse(parts[0], out int hotbarIndex) || !int.TryParse(parts[1], out int slotIndex)) {
            DalamudApi.PluginLog.Warning($"[mophotbar] invalid numbers: \"{args}\"");
            return Task.CompletedTask;
        }

        int realHotbarIndex = hotbarIndex - 1;
        int realSlotIndex = slotIndex - 1;

        if (realHotbarIndex < 0 || realSlotIndex < 0) {
            DalamudApi.PluginLog.Warning($"[mophotbar] invalid index (must be >= 1): \"{args}\"");
            return Task.CompletedTask;
        }

        HotbarManager.ExecuteHotbarActionByIndex((uint)realHotbarIndex, (uint)realSlotIndex);
        DalamudApi.PluginLog.Debug($"[mophotbar] {realHotbarIndex} {realSlotIndex}");
        return Task.CompletedTask;
    }

    private Task HandleMopHotbarEmote(string macroId, string args, CancellationToken token) {
        if (string.IsNullOrWhiteSpace(args)) {
            DalamudApi.PluginLog.Warning("[mophotbaremote] missing arguments");
            return Task.CompletedTask;
        }

        if (!int.TryParse(args, out int actionId)) {
            DalamudApi.PluginLog.Warning($"[mophotbaremote] invalid numbers: \"{args}\"");
            return Task.CompletedTask;
        }

        HotbarManager.ExecuteHotbarEmoteAction((uint)actionId);
        DalamudApi.PluginLog.Debug($"[mophotbaremote] {actionId}");
        return Task.CompletedTask;
    }
}
