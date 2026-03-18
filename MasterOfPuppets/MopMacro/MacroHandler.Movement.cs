using System.Globalization;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

using MasterOfPuppets.Movement;
using MasterOfPuppets.Util;

namespace MasterOfPuppets;

public partial class MacroHandler {
    /// <summary>
    /// /mopmove X Y Z [angle]
    /// Moves to an offset from the player's current position.
    /// X = left(+) / right(-), Y = up(+) / down(-) [inactive], Z = forward(+) / back(-).
    /// Optional 4th argument: face this direction in degrees after arriving.
    /// </summary>
    private Task HandleMopMove(string macroId, string args, CancellationToken token) {
        if (string.IsNullOrWhiteSpace(args)) return Task.CompletedTask;

        var parts = ArgumentParser.ParseMacroArgs(args);
        if (parts.Count < 3) return Task.CompletedTask;

        if (!float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x)
        || !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y)
        || !float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float z)) {
            DalamudApi.PluginLog.Warning($"[mopmove] invalid arguments: \"{args}\"");
            return Task.CompletedTask;
        }

        Angle? facing = null;
        if (parts.Count >= 4 && float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float angleDeg))
            facing = angleDeg.Degrees();

        Plugin.MovementManager.MoveTo(new Vector3(x, y, z), facing);
        DalamudApi.PluginLog.Debug($"[mopmove] offset=({x}, {y}, {z}) facing={facing?.Deg:F0}°");
        return Task.CompletedTask;
    }

    /// <summary>
    /// /mopmoverelativeto X Y Z "Character Name" [angle]
    /// Moves to an offset relative to the specified character's position.
    /// Optional 5th argument: face this direction in degrees after arriving.
    /// </summary>
    private Task HandleMopMoveRelativeTo(string macroId, string args, CancellationToken token) {
        var parts = ArgumentParser.ParseMacroArgs(args);
        if (parts.Count < 4) return Task.CompletedTask;

        if (!float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x)
        || !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y)
        || !float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float z)) {
            DalamudApi.PluginLog.Warning($"[mopmoverelativeto] invalid arguments: \"{args}\"");
            return Task.CompletedTask;
        }

        string relativeCharacterName = parts[3];

        Angle? facing = null;
        if (parts.Count >= 5 && float.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out float angleDeg))
            facing = angleDeg.Degrees();

        Plugin.MovementManager.MoveTo(new Vector3(x, y, z), relativeCharacterName, facing);
        DalamudApi.PluginLog.Debug($"[mopmoverelativeto] offset=({x}, {y}, {z}) origin=\"{relativeCharacterName}\" facing={facing?.Deg:F0}°");
        return Task.CompletedTask;
    }

    /// <summary>/mopmovetotarget - moves to the current target's world position.</summary>
    private Task HandleMopMoveToTarget(string macroId, string args, CancellationToken token) {
        Plugin.MovementManager.MoveToTarget();
        DalamudApi.PluginLog.Debug("[mopmovetotarget]");
        return Task.CompletedTask;
    }

    /// <summary>/mopmovetocharacter "Name" - moves to the position of the named character or object.</summary>
    private Task HandleMopMoveToCharacter(string macroId, string args, CancellationToken token) {
        if (string.IsNullOrWhiteSpace(args)) {
            DalamudApi.PluginLog.Warning("[mopmovetocharacter] missing character name");
            return Task.CompletedTask;
        }

        Plugin.MovementManager.MoveTo(args.Replace("\"", ""));
        DalamudApi.PluginLog.Debug($"[mopmovetocharacter] \"{args}\"");
        return Task.CompletedTask;
    }

    /// <summary>/mopstopmove - immediately stops all movement and clears pending waypoints.</summary>
    private Task HandleMopStopMove(string macroId, string args, CancellationToken token) {
        Plugin.MovementManager.StopMove();
        DalamudApi.PluginLog.Debug("[mopstopmove]");
        return Task.CompletedTask;
    }

    /// <summary>
    /// /mopface angle - rotates the character by the given offset in degrees relative to their current facing.
    /// Positive values turn clockwise, negative values counter-clockwise.
    /// Examples: 90 turns right 90°, -90 turns left 90°, 180 turns around.
    /// </summary>
    private async Task HandleMopFace(string macroId, string args, CancellationToken token) {
        if (!float.TryParse(args.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float angleDeg)) {
            DalamudApi.PluginLog.Warning($"[mopface] invalid angle: \"{args}\"");
            return;
        }

        await DalamudApi.Framework.RunOnFrameworkThread(() => {
            var player = DalamudApi.ObjectTable.LocalPlayer;
            if (player == null) return;
            // Subtract because the Angle struct increases CCW while user-facing degrees are CW.
            var target = (player.Rotation.Radians() - angleDeg.Degrees()).Normalized();
            Plugin.MovementManager.FaceDirection(target);
            DalamudApi.PluginLog.Debug($"[mopface] +{angleDeg}° from {player.Rotation.Radians().Deg:F1}° → {target.Deg:F1}°");
        });
    }

    /// <summary>
    /// /mopfaceabs angle - rotates the character to face an absolute compass direction.
    /// 0 = north, 90 = east, 180 = south, 270 = west (increases clockwise).
    /// </summary>
    private Task HandleMopFaceAbs(string macroId, string args, CancellationToken token) {
        if (!float.TryParse(args.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float angleDeg)) {
            DalamudApi.PluginLog.Warning($"[mopfaceabs] invalid angle: \"{args}\"");
            return Task.CompletedTask;
        }

        // Convert compass CW (0=north) to Angle struct convention (0=south, increases CCW).
        var target = (180f - angleDeg).Degrees().Normalized();
        Plugin.MovementManager.FaceDirection(target);
        DalamudApi.PluginLog.Debug($"[mopfaceabs] {angleDeg}° → {target.Deg:F1}°");
        return Task.CompletedTask;
    }

    /// <summary>/mopenablewalk - enables walk mode.</summary>
    private Task HandleMopEnableWalk(string macroId, string args, CancellationToken token) {
        Plugin.MovementManager.SetWalking(true);
        DalamudApi.PluginLog.Debug("[mopenablewalk]");
        return Task.CompletedTask;
    }

    /// <summary>/mopdisablewalk - disables walk mode (back to running).</summary>
    private Task HandleMopDisableWalk(string macroId, string args, CancellationToken token) {
        Plugin.MovementManager.SetWalking(false);
        DalamudApi.PluginLog.Debug("[mopdisablewalk]");
        return Task.CompletedTask;
    }

    /// <summary>/moptogglewalk - toggles between walk and run.</summary>
    private Task HandleMopToggleWalk(string macroId, string args, CancellationToken token) {
        Plugin.MovementManager.ToggleWalking();
        DalamudApi.PluginLog.Debug("[moptogglewalk]");
        return Task.CompletedTask;
    }
}
