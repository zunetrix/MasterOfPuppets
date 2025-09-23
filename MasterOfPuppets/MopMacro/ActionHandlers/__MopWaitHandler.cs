using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

using MasterOfPuppets;

// TODO: refactor macro action handlers into strategy pattern
public class MopWaitHandler : IMacroActionHandler
{
    public string Command => "mopwait";

    public async Task ExecuteAsync(string macroId, string args, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(args) ||
            !double.TryParse(args, NumberStyles.Float, CultureInfo.InvariantCulture, out double seconds))
        {
            DalamudApi.PluginLog.Warning($"[mopwait] invalid argument: \"{args}\"");
            return;
        }

        var delayMs = TimeSpan.FromSeconds(Math.Round(seconds, 2));
        DalamudApi.PluginLog.Debug($"[mopwait] {delayMs.TotalMinutes:00}:{delayMs.Seconds:00}...");
        await Task.Delay(delayMs, token);
    }
}
