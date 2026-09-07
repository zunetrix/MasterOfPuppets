namespace MasterOfPuppets.LuaScripting.Runs;

public enum LuaRunState {
    Created,
    Waiting,
    Running,
    Paused,
    Completed,
    Stopped,
    Cancelled,
    TimedOut,
    Failed,
}

public static class LuaRunStateExtensions {
    public static bool IsTerminal(this LuaRunState state) => state is
        LuaRunState.Completed or LuaRunState.Stopped or LuaRunState.Cancelled
        or LuaRunState.TimedOut or LuaRunState.Failed;
}
