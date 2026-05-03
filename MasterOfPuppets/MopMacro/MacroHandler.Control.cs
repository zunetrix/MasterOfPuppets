using System;
using System.Threading;
using System.Threading.Tasks;

namespace MasterOfPuppets;

public partial class MacroHandler {
    private async Task HandleMopWait(string macroId, string args, CancellationToken token) {
        if (string.IsNullOrWhiteSpace(args) ||
            !double.TryParse(args, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double seconds)) {
            DalamudApi.PluginLog.Warning($"[mopwait] invalid argument: \"{args}\"");
            return;
        }

        var secondsRound = Math.Round(seconds, 2, MidpointRounding.AwayFromZero);
        var delayMs = TimeSpan.FromSeconds(secondsRound);

        DalamudApi.PluginLog.Debug($"[mopwait] {delayMs.TotalMinutes:00}:{delayMs.Seconds:00}.{delayMs.Milliseconds:00}...");
        await Task.Delay(delayMs, token);
    }

    private async Task HandleMopMacro(string macroId, string args, CancellationToken token) {
        var macroName = args.Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(macroName)) {
            DalamudApi.PluginLog.Warning($"[mopmacro] invalid argument: \"{args}\"");
            return;
        }

        await ResolveAndEnqueueLocalPlayerMacroActions(macroName, null, token);
    }
}
