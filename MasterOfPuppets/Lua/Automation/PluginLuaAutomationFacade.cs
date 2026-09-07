using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using MasterOfPuppets.Formations;
using MasterOfPuppets.LuaScripting.Runs;
using MasterOfPuppets.Movement;

namespace MasterOfPuppets.LuaScripting.Automation;

internal sealed class PluginLuaAutomationFacade : ILuaAutomationFacade {
    private readonly Plugin _plugin;
    private readonly Action<LuaResourceKind, string> _ensureResource;

    public PluginLuaAutomationFacade(Plugin plugin, Action<LuaResourceKind, string> ensureResource) {
        _plugin = plugin;
        _ensureResource = ensureResource;
    }

    public Task<IReadOnlyList<LuaMacroAssetSnapshot>> ListMacrosAsync(CancellationToken cancellationToken) =>
        OnFramework(() => (IReadOnlyList<LuaMacroAssetSnapshot>)_plugin.Config.Macros
            .Select((macro, index) => new LuaMacroAssetSnapshot(
                index + 1,
                macro.Name,
                (macro.Tags ?? []).ToArray(),
                macro.Commands?.Count ?? 0))
            .ToArray(), cancellationToken);

    public Task<LuaMacroQueueSnapshot> GetMacroStatusAsync(CancellationToken cancellationToken) =>
        OnFramework(() => new LuaMacroQueueSnapshot(
            _plugin.MacroHandler.MacroCurrentId != null || _plugin.MacroHandler.MacroPendingQueue.Count > 0,
            _plugin.MacroHandler.IsMacroQueuePaused,
            _plugin.MacroHandler.MacroCurrentId ?? string.Empty,
            _plugin.MacroHandler.MacroCurrentIndex,
            _plugin.MacroHandler.MacroCurrentActions.Count,
            _plugin.MacroHandler.MacroPendingQueue.Count), cancellationToken);

    public Task<LuaAutomationResult> RunMacroAsync(
        string selector,
        IReadOnlyDictionary<string, string> variables,
        string scope,
        CancellationToken cancellationToken) {
        _ensureResource(LuaResourceKind.MacroQueue, "macros.run");
        return OnFramework(() => {
            int index;
            try {
                index = _plugin.MacroManager.FindMacroIndex(selector);
            } catch (ArgumentException ex) {
                return LuaAutomationResult.Rejected(ex.Message);
            }
            var values = new Dictionary<string, string>(variables, StringComparer.OrdinalIgnoreCase);
            switch (NormalizeScope(scope)) {
                case "local":
                    _plugin.MacroHandler.ExecuteMacro(index, values);
                    break;
                case "current_pc":
                    _plugin.IpcProvider.RunMacro(index, values, includeSelf: true);
                    break;
                default:
                    return LuaAutomationResult.Rejected("macro scope must be 'local' or 'current_pc'");
            }
            return LuaAutomationResult.Queued($"macro '{_plugin.Config.Macros[index].Name}'");
        }, cancellationToken);
    }

    public Task<LuaAutomationResult> ControlMacrosAsync(string action, string scope, CancellationToken cancellationToken) {
        _ensureResource(LuaResourceKind.MacroQueue, $"macros.{action}");
        return OnFramework(() => {
            var normalizedScope = NormalizeScope(scope);
            if (normalizedScope is not ("local" or "current_pc"))
                return LuaAutomationResult.Rejected("macro scope must be 'local' or 'current_pc'");
            switch (action.Trim().ToLowerInvariant()) {
                case "pause":
                    if (normalizedScope == "local") _plugin.MacroHandler.PauseMacroQueue();
                    else _plugin.IpcProvider.PauseMacroExecution();
                    return LuaAutomationResult.Completed("macro queue paused");
                case "resume":
                    if (normalizedScope == "local") _plugin.MacroHandler.ResumeMacroQueue();
                    else _plugin.IpcProvider.ResumeMacroExecution();
                    return LuaAutomationResult.Completed("macro queue resumed");
                case "stop":
                    if (normalizedScope == "local") _plugin.MacroHandler.StopMacroQueueExecution();
                    else _plugin.IpcProvider.StopMacroExecution();
                    return LuaAutomationResult.Completed("macro queue stopped");
                default:
                    return LuaAutomationResult.Rejected("macro action must be pause, resume, or stop");
            }
        }, cancellationToken);
    }

    public async Task<LuaAutomationResult> WaitForMacrosAsync(TimeSpan timeout, CancellationToken cancellationToken) {
        ValidateTimeout(timeout);
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline) {
            cancellationToken.ThrowIfCancellationRequested();
            if (!(await GetMacroStatusAsync(cancellationToken)).Active)
                return LuaAutomationResult.Completed("macro queue completed");
            await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);
        }
        return LuaAutomationResult.TimedOut("macro queue did not complete before the timeout");
    }

    public Task<IReadOnlyList<LuaFormationAssetSnapshot>> ListFormationsAsync(CancellationToken cancellationToken) =>
        OnFramework(() => (IReadOnlyList<LuaFormationAssetSnapshot>)_plugin.Config.Formations
            .Select(formation => new LuaFormationAssetSnapshot(
                formation.Name,
                formation.Points.Select((point, index) => new LuaFormationPointSnapshot(
                    index + 1,
                    point.Offset.X,
                    point.Offset.Y,
                    point.Offset.Z,
                    point.Angle,
                    (point.GroupIds ?? []).ToArray(),
                    point.Cids?.Count ?? 0)).ToArray()))
            .ToArray(), cancellationToken);

    public Task<LuaAutomationResult> RunFormationAsync(
        string name,
        string anchor,
        string movementMode,
        string scope,
        CancellationToken cancellationToken) {
        _ensureResource(LuaResourceKind.FormationTracking | LuaResourceKind.Movement, "formations.run");
        return OnFramework(() => {
            if (NormalizeScope(scope) != "current_pc")
                return LuaAutomationResult.Rejected("formation execution currently supports only explicit 'current_pc' scope");
            var formation = _plugin.Config.Formations.FirstOrDefault(candidate =>
                candidate.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase));
            if (formation == null)
                return LuaAutomationResult.Rejected($"formation '{name}' was not found");
            if (!SimpleInputMovement.TryParseMode(movementMode.Trim(), out var mode))
                return LuaAutomationResult.Rejected("movement mode must be continuous, precise, forward, or natural");
            var reference = ParseAnchor(anchor);
            _plugin.IpcProvider.ExecuteFormation(formation.Name, reference, mode);
            return LuaAutomationResult.Queued($"formation '{formation.Name}'");
        }, cancellationToken);
    }

    public Task<LuaAutomationResult> StopFormationAsync(string scope, CancellationToken cancellationToken) {
        _ensureResource(LuaResourceKind.FormationTracking | LuaResourceKind.Movement, "formations.stop");
        return OnFramework(() => {
            switch (NormalizeScope(scope)) {
                case "local":
                    _plugin.StopNonLuaMovementLocal();
                    break;
                case "current_pc":
                    _plugin.IpcProvider.StopManagedMovement();
                    break;
                default:
                    return LuaAutomationResult.Rejected("formation scope must be 'local' or 'current_pc'");
            }
            return LuaAutomationResult.Completed("formation movement stopped");
        }, cancellationToken);
    }

    public async Task<LuaAutomationResult> WaitForFormationAsync(TimeSpan timeout, CancellationToken cancellationToken) {
        ValidateTimeout(timeout);
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline) {
            cancellationToken.ThrowIfCancellationRequested();
            var active = await OnFramework(
                () => _plugin.SimpleInputMovement.IsMoving || _plugin.FormationTrackingSession.IsActive,
                cancellationToken);
            if (!active)
                return LuaAutomationResult.Completed("local formation movement completed");
            await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);
        }
        return LuaAutomationResult.TimedOut("local formation movement did not complete before the timeout");
    }

    private Task<T> OnFramework<T>(Func<T> action, CancellationToken cancellationToken) =>
        DalamudApi.Framework.RunOnFrameworkThread(action).WaitAsync(cancellationToken);

    private static string NormalizeScope(string? scope) =>
        string.IsNullOrWhiteSpace(scope) ? "local" : scope.Trim().ToLowerInvariant().Replace('-', '_');

    private static void ValidateTimeout(TimeSpan timeout) {
        if (timeout <= TimeSpan.Zero || timeout > TimeSpan.FromMinutes(10))
            throw new ArgumentOutOfRangeException(nameof(timeout));
    }

    private static FormationAnchorReference ParseAnchor(string? anchor) {
        var value = anchor?.Trim() ?? "self";
        return value.ToLowerInvariant() switch {
            "self" => FormationAnchorReference.Self,
            "target" => FormationAnchorReference.Target,
            "focus" or "focus_target" or "ftarget" => FormationAnchorReference.FocusTarget,
            "default" => FormationAnchorReference.Default,
            _ => FormationAnchorReference.Named(value),
        };
    }
}
