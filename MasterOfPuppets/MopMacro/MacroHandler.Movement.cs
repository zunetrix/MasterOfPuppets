using System.Globalization;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

using MasterOfPuppets.Util;

namespace MasterOfPuppets;

public partial class MacroHandler {
    private Task HandleMopMove(string macroId, string args, CancellationToken token) {
        if (string.IsNullOrWhiteSpace(args)) return Task.CompletedTask;

        var parts = ArgumentParser.ParseMacroArgs(args);
        if (parts.Count != 3) return Task.CompletedTask;

        if (!float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x)
        || !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y)
        || !float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float z)) {
            DalamudApi.PluginLog.Warning($"[mopmove] invalid argument parse: \"{args}\"");
            return Task.CompletedTask;
        }

        Plugin.MovementManager.MoveToPosition(new Vector3(x, y, z));
        DalamudApi.PluginLog.Debug($"[mopmove] {x}, {y}, {z}");
        return Task.CompletedTask;
    }

    private Task HandleMopMoveRelativeTo(string macroId, string args, CancellationToken token) {
        var parts = ArgumentParser.ParseMacroArgs(args);
        if (parts.Count != 4) return Task.CompletedTask;

        string relativeCharacterName = parts[3];

        if (!float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x)
        || !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y)
        || !float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float z)) {
            DalamudApi.PluginLog.Warning($"[mopmoverelativeto] invalid argument parse: \"{args}\"");
            return Task.CompletedTask;
        }

        Plugin.MovementManager.MoveToPositionRelative(new Vector3(x, y, z), relativeCharacterName);
        DalamudApi.PluginLog.Debug($"[mopmoverelativeto] {x}, {y}, {z} ({relativeCharacterName})");
        return Task.CompletedTask;
    }

    private Task HandleMopMoveToTarget(string macroId, string args, CancellationToken token) {
        Plugin.MovementManager.MoveToTargetPosition();
        DalamudApi.PluginLog.Debug("[mopmovetotarget]");
        return Task.CompletedTask;
    }

    private Task HandleMopMoveToCharacter(string macroId, string args, CancellationToken token) {
        if (string.IsNullOrWhiteSpace(args)) {
            DalamudApi.PluginLog.Warning("[mopmovetocharacter] Invalid arguments expected character name");
            return Task.CompletedTask;
        }

        Plugin.MovementManager.MoveToObject(args.Replace("\"", ""));
        DalamudApi.PluginLog.Debug($"[mopmovetocharacter] {args}");
        return Task.CompletedTask;
    }

    private Task HandleMopStopMove(string macroId, string args, CancellationToken token) {
        Plugin.MovementManager.StopMove();
        DalamudApi.PluginLog.Debug("[mopstopmove]");
        return Task.CompletedTask;
    }

    private Task HandleMopEnableWalk(string macroId, string args, CancellationToken token) {
        Plugin.MovementManager.SetWalking(true);
        DalamudApi.PluginLog.Debug("[mopenablewalk]");
        return Task.CompletedTask;
    }

    private Task HandleMopDisableWalk(string macroId, string args, CancellationToken token) {
        Plugin.MovementManager.SetWalking(false);
        DalamudApi.PluginLog.Debug("[mopdisablewalk]");
        return Task.CompletedTask;
    }

    private Task HandleMopToggleWalk(string macroId, string args, CancellationToken token) {
        Plugin.MovementManager.ToggleWalking();
        DalamudApi.PluginLog.Debug("[moptogglewalk]");
        return Task.CompletedTask;
    }
}
