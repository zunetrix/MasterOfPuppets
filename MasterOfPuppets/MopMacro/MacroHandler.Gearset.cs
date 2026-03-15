using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MasterOfPuppets;

public partial class MacroHandler {
    private async Task HandleMopMoveGearsets(string macroId, string args, CancellationToken token) {
        var indices = new List<int>();
        foreach (var part in args.Split(',', System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries)) {
            if (int.TryParse(part, out int n) && n is >= 1 and <= 100)
                indices.Add(n - 1);
            else
                DalamudApi.PluginLog.Warning($"[mopmovegearsets] Invalid gearset number: \"{part}\"");
        }

        if (indices.Count == 0) return;

        await DalamudApi.Framework.RunOnFrameworkThread(
            () => GearsetManager.MoveGearsetsToArmoury(Plugin, indices));

        await Plugin.ItemMover.WhenComplete().WaitAsync(token);
    }
}
