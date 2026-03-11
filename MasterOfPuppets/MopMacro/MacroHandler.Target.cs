using System.Threading;
using System.Threading.Tasks;

namespace MasterOfPuppets;

public partial class MacroHandler {
    private Task HandleMopTarget(string macroId, string args, CancellationToken token) {
        string targetName = args.Trim().Trim('"');
        GameTargetManager.TargetObject(targetName);
        DalamudApi.PluginLog.Debug($"[moptarget] {targetName}");
        return Task.CompletedTask;
    }

    private Task HandleMopTargetOf(string macroId, string args, CancellationToken token) {
        string targetName = args.Trim().Trim('"');
        GameTargetManager.TargetOf(targetName);
        DalamudApi.PluginLog.Debug($"[moptargetof] {targetName}");
        return Task.CompletedTask;
    }

    private Task HandleMopTargetClear(string macroId, string args, CancellationToken token) {
        GameTargetManager.TargetClear();
        DalamudApi.PluginLog.Debug("[moptargetclear]");
        return Task.CompletedTask;
    }

    private Task HandleMopTargetMyMinion(string macroId, string args, CancellationToken token) {
        GameTargetManager.TargetMyMinion();
        DalamudApi.PluginLog.Debug("[moptargetmyminion]");
        return Task.CompletedTask;
    }
}
