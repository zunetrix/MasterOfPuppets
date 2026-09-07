using System;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

using MasterOfPuppets.Extensions;
using MasterOfPuppets.Formations;
using MasterOfPuppets.Util;

namespace MasterOfPuppets;

public partial class MacroHandler {
    /// <summary>
    /// /moppetplace X Y Z [anchor]
    /// Places the summoned pet at an offset relative to the specified anchor.
    /// X = left(+) / right(-), Y = up(+) / down(-), Z = forward(+) / back(-).
    /// Anchor can be: self, target, ftarget, or "Character Name@World".
    /// Default anchor: target if selected, otherwise self.
    /// </summary>
    private async Task HandleMopPetPlace(string macroId, string args, CancellationToken token) {
        if (string.IsNullOrWhiteSpace(args)) return;

        var parts = ArgumentParser.ParseMacroArgs(args);
        if (parts.Count < 3) {
            DalamudApi.PluginLog.Warning($"[moppetplace] requires at least X Y Z offsets: \"{args}\"");
            return;
        }

        if (!float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x)
            || !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y)
            || !float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float z)) {
            DalamudApi.PluginLog.Warning($"[moppetplace] invalid coordinate offsets: \"{args}\"");
            return;
        }

        await DalamudApi.Framework.RunOnFrameworkThread(() => {
            var defaultAnchor = DalamudApi.TargetManager.Target != null
                ? FormationAnchorReference.Target
                : FormationAnchorReference.Self;

            var anchorParse = FormationAnchorArgumentParser.ParseAnchorAndArrival(
                parts.Skip(3),
                defaultAnchor);

            if (!FormationAnchorResolver.TryResolve(Plugin, new Formation(), anchorParse.Anchor, out var resolved, out var failureReason, out _)) {
                if (anchorParse.Fallback != null && FormationAnchorResolver.TryResolve(Plugin, new Formation(), anchorParse.Fallback, out var fallbackResolved, out _, out _)) {
                    resolved = fallbackResolved;
                } else {
                    DalamudApi.PluginLog.Warning($"[moppetplace] failed to resolve anchor: {failureReason}");
                    return;
                }
            }

            var offset = new Vector3(x, y, z);
            var worldPos = offset.ApplyLeaderRotation(resolved.Rotation, resolved.Position);
            worldPos.Y = resolved.Position.Y + y;

            bool isSelf = anchorParse.Anchor.Kind == FormationAnchorKind.Self;
            var targetActor = resolved.Actor ?? (resolved.GameObjectId.HasValue ? DalamudApi.ObjectTable.FirstOrDefault(a => a != null && a.GameObjectId == resolved.GameObjectId) : null);

            GameActionManager.PlacePet(worldPos, targetActor, isSelf);
            DalamudApi.PluginLog.Debug($"[moppetplace] offset=({x}, {y}, {z}) anchor={anchorParse.Anchor} world=({worldPos.X:F2}, {worldPos.Y:F2}, {worldPos.Z:F2}) actor='{targetActor?.Name}' isSelf={isSelf}");
        });
    }

    /// <summary>
    /// /moppetformationplace "Formation Name" pointNumber [anchor=self|target|ftarget|"Character Name@World"]
    /// Places the summoned pet at one saved formation point using point 1 as the live anchor.
    /// Point numbers are 1-based.
    /// </summary>
    private async Task HandleMopPetFormationPlace(string macroId, string args, CancellationToken token) {
        var options = ParseFormationGotoCommandArgs(args);
        if (options == null) {
            DalamudApi.PluginLog.Warning("[moppetformationplace] missing formation name or point number");
            return;
        }

        if (options.InvalidArgument != null)
            DalamudApi.PluginLog.Warning($"[moppetformationplace] invalid argument: \"{options.InvalidArgument}\"");

        if (options.PointIndex < 0) {
            DalamudApi.PluginLog.Warning($"[moppetformationplace] invalid point number: \"{options.InvalidArgument}\"");
            return;
        }

        var fallbackAnchor = options.Fallback;
        if (fallbackAnchor == null) {
            var currentPlan = _macroState.CurrentPlan ?? _loopState.CurrentPlan;
            if (currentPlan != null && currentPlan.TryGetVariable("mop_origin", out var origin) && !string.IsNullOrWhiteSpace(origin)) {
                fallbackAnchor = FormationAnchorReference.Named(origin);
            }
        }

        await DalamudApi.Framework.RunOnFrameworkThread(() =>
            FormationLocalMovementExecutor.ExecuteFormationPetPlace(
                Plugin,
                options.FormationName,
                options.PointIndex,
                options.Anchor,
                logPrefix: "moppetformationplace",
                fallbackAnchor: fallbackAnchor));
    }
}
