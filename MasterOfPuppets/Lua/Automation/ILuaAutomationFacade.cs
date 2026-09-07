using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MasterOfPuppets.LuaScripting.Automation;

public sealed record LuaMacroAssetSnapshot(int Index, string Name, IReadOnlyList<string> Tags, int CommandCount);
public sealed record LuaMacroQueueSnapshot(bool Active, bool Paused, string CurrentId, int CurrentActionIndex, int CurrentActionCount, int PendingCount);
public sealed record LuaFormationPointSnapshot(int Index, float X, float Y, float Z, float FacingDegrees, IReadOnlyList<string> Groups, int AssignedCharacterCount);
public sealed record LuaFormationAssetSnapshot(string Name, IReadOnlyList<LuaFormationPointSnapshot> Points);
public sealed record LuaAutomationResult(bool Success, string Status, string Message) {
    public static LuaAutomationResult Queued(string message) => new(true, "queued", message);
    public static LuaAutomationResult Completed(string message) => new(true, "completed", message);
    public static LuaAutomationResult Rejected(string message) => new(false, "rejected", message);
    public static LuaAutomationResult TimedOut(string message) => new(false, "timeout", message);
}

public interface ILuaAutomationFacade {
    Task<IReadOnlyList<LuaMacroAssetSnapshot>> ListMacrosAsync(CancellationToken cancellationToken);
    Task<LuaMacroQueueSnapshot> GetMacroStatusAsync(CancellationToken cancellationToken);
    Task<LuaAutomationResult> RunMacroAsync(string selector, IReadOnlyDictionary<string, string> variables, string scope, CancellationToken cancellationToken);
    Task<LuaAutomationResult> WaitForMacrosAsync(TimeSpan timeout, CancellationToken cancellationToken);
    Task<LuaAutomationResult> ControlMacrosAsync(string action, string scope, CancellationToken cancellationToken);
    Task<IReadOnlyList<LuaFormationAssetSnapshot>> ListFormationsAsync(CancellationToken cancellationToken);
    Task<LuaAutomationResult> RunFormationAsync(string name, string anchor, string movementMode, string scope, CancellationToken cancellationToken);
    Task<LuaAutomationResult> WaitForFormationAsync(TimeSpan timeout, CancellationToken cancellationToken);
    Task<LuaAutomationResult> StopFormationAsync(string scope, CancellationToken cancellationToken);
}
