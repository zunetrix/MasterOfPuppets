using System;
using System.Globalization;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

using MasterOfPuppets;

public record MopMoveArgs(float X, float Y, float Z);

public class MopMoveHandler : IMacroActionHandler<MopMoveArgs> {
    public string Command => "mopmove";

    private readonly Plugin _plugin;

    public MopMoveHandler(Plugin plugin) {
        _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
    }

    public Task<MacroActionResult> ExecuteAsync(string macroId, MopMoveArgs args, CancellationToken token) {
        _plugin.MovementManager.MoveToPosition(new Vector3(args.X, args.Y, args.Z));
        return Task.FromResult(MacroActionResult.Handled());
    }
}
