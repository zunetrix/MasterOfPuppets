using System;

namespace MasterOfPuppets.LuaScripting.Synchronization;

public sealed record LuaDistributedClockDiagnostics(
    string RunToken,
    LuaClockEstimate Estimate,
    DateTimeOffset LastProbeAt);
