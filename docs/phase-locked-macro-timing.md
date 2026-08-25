# Phase-Locked Macro Timing

## Problem

Repeating movement macros previously used relative waits:

```text
/mopformationgoto ...
/mopwait 0.50
/moploop
```

`/mopformationgoto` awaits `DalamudApi.Framework.RunOnFrameworkThread` before
returning. The macro runner then applies the configured global delay, followed
by `/mopwait`. The effective interval was therefore:

```text
framework dispatch latency + command execution + global delay + mopwait
```

Framework dispatch latency differs slightly between FFXIV processes and can
vary from one iteration to the next. Because every wait was relative to the
completion time of the preceding work, that latency was permanently added to
the macro timeline on every leg. Long-running formations exposed this as
cumulative phase drift: clients that initially started together gradually
overlapped adjacent formation slots.

Natural movement made the drift easier to observe, but did not create the
underlying scheduling behavior. All `/mopformationgoto` modes use framework
thread dispatch, and all `SimpleInputMovement` strategies update on framework
frames. Natural mode additionally recalculates its live anchor-relative target
on each framework update.

## Implementation

The fix adds the opt-in command:

```text
/mopphasewait <seconds>
```

`MacroHandler.ExecuteActions` creates one `MacroPhaseClock` when a macro
execution begins. The clock uses the monotonic `Stopwatch` timestamp source and
maintains a cumulative target offset from that start time.

For every phase wait:

1. Add the requested interval to the cumulative target offset.
2. Calculate the absolute deadline as `macro start + cumulative target`.
3. Subtract the elapsed monotonic time.
4. Wait only for the positive remainder.
5. If the phase is already overdue, wait zero without rebasing future phases.

Conceptually:

```text
target += phaseInterval
remaining = max(0, target - elapsedSinceMacroStart)
await delay(remaining)
```

This changes a slow framework frame from cumulative drift into bounded,
temporary jitter. Later phases retain their original deadlines and recover the
lost time when sufficient slack exists.

The clock persists across `/moploop` and `/moploopstart`/`/moploopend`
iterations for the lifetime of that queued macro execution. Starting the macro
again creates a new clock.

## Command semantics

The argument is the complete phase interval. It includes time consumed by
command execution and the configured global delay.

For example, a movement loop that previously used a `0.25` second global delay
and `/mopwait 0.50` has a nominal interval of `0.75` seconds, so its phase-locked
equivalent is:

```text
/mopformationgoto "Formation" 2 anchor="Anchor Character@World" natural
/mopphasewait 0.75
```

`/mopphasewait` itself skips the normal post-command global delay. The movement
command before it still receives the configured global delay, but that time is
inside the absolute `0.75` second phase budget.

Intervals are parsed with invariant culture, rounded to two decimal places,
and must be finite and non-negative. Cancellation continues through the macro
runner's existing cancellation token and `Task.Delay` path.

## Compatibility

Existing `/mopwait` behavior is unchanged. Phase locking is opt-in so macros
that intentionally require a relative delay after an action keep their current
semantics.

All participating clients must run a plugin build containing `/mopphasewait`.
The same phase-locked macro must also be present on each machine.

## Synchronization boundary

The implementation prevents accumulating drift after a macro begins. It does
not provide a shared clock epoch between computers. Any difference in chat
delivery or macro-start time remains as a constant initial offset; it no longer
grows on every loop.

If a client remains slower than the requested phase interval for a sustained
period, phase waits become zero while it attempts to catch up. The clock does
not skip actions or rebase that client, because either behavior would silently
change the movement sequence. The interval must still provide enough time for
the commands and physical movement under normal load.

Pausing a macro does not pause the monotonic phase clock. After resume, overdue
phase waits return immediately until the execution catches its original
timeline. A future enhancement could explicitly shift the phase epoch by the
paused duration if pause/resume behavior needs to preserve spacing rather than
wall-clock phase.

## Relevant files

- `MasterOfPuppets/MopMacro/MacroPhaseClock.cs`: monotonic deadline calculation.
- `MasterOfPuppets/MopMacro/MacroHandler.cs`: command parsing and integration
  with whole-macro and block loops.
- `MasterOfPuppets/MopMacro/CommandHelp/MopCommandsHelper.MacroAction.cs`:
  in-plugin command documentation.
- `MasterOfPuppetsTests/MacroTests.cs`: compensation, overdue-phase, validation,
  and global-delay classification tests.

## Validation

The focused macro test set passes 55/55 tests. The Release build completes with
zero warnings and zero errors. At the time of implementation, the full test
suite passes 298/303 tests; the five failures are pre-existing cross-world-name
normalization tests unrelated to macro timing.
