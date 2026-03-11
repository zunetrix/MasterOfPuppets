using System;
using System.Threading;
using System.Threading.Tasks;

namespace MasterOfPuppets;

public partial class MacroHandler {
    private Task HandleMopObjectQuantity(string macroId, string args, CancellationToken token) {
        if (!Enum.TryParse<SettingsDisplayObjectLimitType>(args, ignoreCase: true, out var displayObjectLimitType)
            || !Enum.IsDefined(typeof(SettingsDisplayObjectLimitType), displayObjectLimitType)) {
            DalamudApi.PluginLog.Warning($"[mopsetobjectquantity] Invalid object quantity value (0-5): {displayObjectLimitType}");
            return Task.CompletedTask;
        }

        GameSettingsManager.SetDisplayObjectLimit(displayObjectLimitType);
        return Task.CompletedTask;
    }
}
