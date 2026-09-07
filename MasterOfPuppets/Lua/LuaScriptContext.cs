using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using MasterOfPuppets.LuaScripting.Runs;
using MasterOfPuppets.LuaScripting.Snapshots;
using MasterOfPuppets.LuaScripting.Automation;
using MasterOfPuppets.LuaScripting.Events;
using MasterOfPuppets.LuaScripting.Coordination;
using MasterOfPuppets.LuaScripting.Watches;

namespace MasterOfPuppets.LuaScripting;

public sealed record LuaScriptContext(
    int Slot,
    int CharacterCount,
    int Seed,
    Action<LuaTrajectorySample> PublishTrajectory,
    Action<string>? Log = null,
    Action<string>? SendChat = null,
    string CharacterName = "",
    Func<string, string, TimeSpan, CancellationToken, Task<bool>>? WaitForChat = null,
    string RunTargetName = "",
    Func<string, CancellationToken, Task>? SetAnchor = null,
    Func<LuaActorFollowRequest, CancellationToken, Task>? FollowActor = null,
    IReadOnlyDictionary<string, string>? Variables = null,
    string RunId = "",
    string ScriptName = "",
    string DalamudVersion = "",
    string GameVersion = "",
    string ClientStructsVersion = "",
    IReadOnlyDictionary<string, string>? Modules = null,
    Runtime.LuaRuntimeLimits? Limits = null,
    LuaExecutionControl? ExecutionControl = null,
    Action<string>? RequestStop = null,
    IReadOnlyList<string>? DeclaredCapabilities = null,
    Func<CancellationToken, Task<LuaGameSnapshot>>? CaptureGameSnapshot = null,
    ILuaAutomationFacade? Automation = null,
    ILuaActionFacade? Actions = null,
    LuaEventHub? Events = null,
    string ConductorName = "",
    Func<double>? SharedTimeSeconds = null,
    Func<double>? LocalTimeSeconds = null,
    Runtime.ILuaScheduler? Scheduler = null,
    ILuaCoordinationFacade? Coordination = null,
    Func<double>? GetTargetSpeed = null,
    Func<string, IReadOnlyList<string>?>? GetGroup = null,
    Func<bool>? IsWalking = null,
    Func<(int Slot, int Count)>? GetVisibleRoster = null,
    Func<string, CancellationToken, Task<LuaActorWatchRegistration>>? WatchActor = null,
    Func<string, CancellationToken, Task<bool>>? UnwatchActor = null,
    Func<string, CancellationToken, Task<bool>>? RequestGlobalStop = null,
    Func<uint, bool, ulong, CancellationToken, Task<bool>>? RequestEmoteResync = null,
    Func<LuaActorEventSources>? GetActorEventSources = null);
