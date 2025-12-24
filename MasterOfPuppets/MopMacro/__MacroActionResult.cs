using System;

namespace MasterOfPuppets;

public record MacroActionResult(
    bool HandledResult,
    MacroEnqueue? EnqueueMacro,
    TimeSpan? OverrideDelay
) {
    public static MacroActionResult Handled() => new(true, null, null);

    public static MacroActionResult NotHandled(TimeSpan? overrideDelay = null) => new(false, null, overrideDelay);

    public static MacroActionResult Enqueue(MacroEnqueue enqueue, TimeSpan? overrideDelay = null) => new(true, enqueue, overrideDelay);
}

public record MacroEnqueue(string MacroId, string[] Actions, double Delay, bool IsLoop);
