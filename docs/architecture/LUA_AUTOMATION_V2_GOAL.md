# Master of Puppets — Lua Automation V2 Long-Term Goal

This is the authoritative long-term architecture and implementation specification for Master of Puppets Lua automation. Relevant work must remain compatible with this direction, while individual tasks should implement only their requested milestone unless broader execution is explicitly requested.

You are the senior engineer responsible for turning the existing Master of Puppets Lua work into a production-quality, safe, deterministic, general-purpose FFXIV scripting platform for band performances, theatrical choreography, coordinated routines, combat actions, and future scenarios that have not yet been designed.

Repository checkouts used by the two project collaborators:

`C:\Users\Utilisateur\OneDrive\Music\MasterofPuppets`

`C:\Users\john\OneDrive\Music\MasterofPuppets`

At the beginning of the task, detect which checkout exists on the current machine and use that path as the repository root. Do not assume that both paths exist, do not create a missing checkout path, and do not copy or synchronize files between the two locations automatically. All repository-relative instructions in this prompt refer to the detected checkout. Preserve the active collaborator's uncommitted work exactly as described below.

## Mission

Inspect the current implementation, then implement Lua Automation V2 as a coherent extension of the existing code. Do not replace working systems blindly and do not stop after writing a design document. Deliver tested code, migrations, documentation, diagnostics, and representative scripts.

“Full Lua scripting” means:

1. Normal Lua 5.2 language features and safe standard-library functionality appropriate to an embedded runtime.
2. A broad, stable, versioned `mop` API and provider system capable of representing every game-facing capability exposed by Dalamud, FFXIVClientStructs, Master of Puppets, and approved plugin IPC integrations—not merely a fixed list of choreography helpers.
3. Deterministic multi-client execution with reliable cancellation, resource limits, observability, and recovery.

It does **not** mean exposing arbitrary file access, process execution, networking, environment access, CLR reflection, unsafe memory access, or raw native pointers. Those are host-security boundaries, not restrictions on which in-game scenarios Lua may express.

## Repository rules and preservation

- Inspect `git status` before editing. The working tree contains extensive uncommitted and untracked Lua-development work. Preserve it. Do not reset, clean, revert, or overwrite unrelated changes.
- Treat `%APPDATA%\XIVLauncher\pluginConfigs\MasterOfPuppets.json` as read-only unless the user explicitly authorizes changing it. It is the authoritative client-data source for understanding actual scripts, macros, formations, groups, and characters.
- Do not build or copy directly into the live Dalamud-watched release directory. Do not hot-deploy unless the user explicitly asks. Build and test in staging.
- Prefer public Dalamud APIs, then FFXIVClientStructs only where necessary. Isolate native/game-version-sensitive code behind adapters.
- Preserve existing macro behavior, formation behavior, configuration data, import/export compatibility, and existing flat Lua functions.

## Long-term project structure and repository hygiene

Treat organization, readability, and maintainability as first-class acceptance requirements. This is a long-lived two-person project, not a disposable prototype. Do not leave the repository with temporary experiments, duplicate implementations, contradictory documentation, dead compatibility layers, or an ever-growing collection of ambiguously named files.

Before restructuring, inventory the existing source, tests, documentation, scripts, tools, generated artifacts, and uncommitted work. Propose and apply a coherent domain-oriented structure with clear ownership boundaries, for example runtime, API providers, synchronization, choreography/movement, script storage, UI/editor, infrastructure adapters, tests, documentation, packaged examples, and developer tooling. Follow established project conventions where they are already sound.

Required hygiene rules:

- Keep classes and files focused, consistently named, and placed in the domain they belong to.
- Split oversized files when doing so creates meaningful boundaries; do not fragment code into trivial one-method files.
- Keep public contracts separate from Dalamud/native implementations and test doubles.
- Eliminate duplicate helpers and parallel implementations once callers have migrated to the canonical version.
- Replace obsolete files with their newer implementation when appropriate. Update all references, project items, documentation links, tests, packaging rules, and build scripts in the same change.
- Remove superseded source files, scripts, documentation, and generated artifacts only after proving they are no longer referenced or needed for migration. Use `rg`, project/build metadata, tests, and Git history/status to verify the exact targets first.
- Never delete or overwrite unknown uncommitted collaborator work merely because it appears old. If ownership or obsolescence is uncertain, preserve it and report the ambiguity instead of guessing.
- Prefer Git-tracked deletion for repository files so removal remains recoverable. Do not use broad recursive deletion, globs, `git clean`, destructive resets, or deletion outside the detected repository root.
- Keep generated, build, cache, recording, export, and personal-machine artifacts out of source directories. Add precise ignore rules where appropriate without hiding files that should be versioned.
- Consolidate overlapping documentation into one authoritative location per topic. Remove stale machine-specific paths, outdated feasibility claims, and contradictory handoff notes after transferring still-relevant information.
- Maintain a concise root README and documentation index that make the architecture, build/test workflow, Lua extension process, and important directories easy for either collaborator to understand.
- Add or update architecture documentation describing dependency direction, extension points, naming conventions, file placement rules, and deprecation/removal procedure.
- Do not retain commented-out legacy blocks as an archive; Git history is the archive. Preserve comments that explain non-obvious design constraints.
- Do not commit build outputs, temporary diagnostics, scratch files, local configuration, or copied personal data.
- Run a final repository-hygiene audit: stale references, duplicate types, orphaned files, outdated docs, unexpected generated files, inconsistent namespaces, and build/test artifacts.

Organization changes must be performed incrementally with build/test verification after meaningful moves. The final report must include a structure summary, a list of files moved/replaced/removed and why, confirmation that references were updated, and any legacy material intentionally retained for compatibility.

## Verified current baseline

Confirm these facts against the working tree before coding and report any differences:

- Target: .NET 10 Windows, Dalamud API 15.
- Runtime: `LuaCSharp` 0.5.6, a managed Lua 5.2 interpreter with async/await, coroutines, custom module loaders, cancellation, and platform abstractions.
- Current Lua runner exposes 18 flat functions: `log`, `wait`, `time`, `is_running`, `get_name`, `get_slot`, `get_count`, `get_seed`, `get_run_target`, `get_var`, `get_number`, `names_match`, `set_anchor`, `trajectory_update`, `follow_actor`, `chat`, `say`, and `wait_for_chat`.
- Only the math library is deliberately opened. File, OS, debug, unrestricted module, IO, and CLR access are intentionally unavailable.
- One Lua run is active per client; starting another cancels the first. A run has a ten-minute maximum lifetime.
- Local IPC transmits script source. Cross-PC Chat Sync sends a script hash and requires an identical local copy.
- Cross-PC start synchronization currently uses whole server seconds and independent local stopwatches. There is no readiness barrier, heartbeat, ongoing clock correction, or late-join protocol.
- `trajectory_update` publishes only relative position and facing. The controller feeds a moving point into the general natural-movement controller with full forward input. This has already caused overshoot, chord cutting, and per-PC phase clusters; switching affected characters to walk mode materially improves the same formation.
- Lua has no first-class macro, saved-formation, actor-query, generic event, shared-state, or module API.
- The editor is a multiline text box with structural validation only; it lacks compile validation, line-aware diagnostics, API help, run logs, and debugging controls.
- The last documented suite result is 374 passing tests, but currently reproduce the baseline yourself. If package assets are stale or inconsistent, repair the restore/build setup without deleting user work or weakening locked dependency behavior.

Relevant starting points include:

- `MasterOfPuppets/Lua/LuaScriptRunner.cs`
- `MasterOfPuppets/Lua/LuaScriptManager.cs`
- `MasterOfPuppets/Lua/LuaScriptContext.cs`
- `MasterOfPuppets/Lua/LuaChoreographyClock.cs`
- `MasterOfPuppets/Lua/LuaTrajectoryController.cs`
- `MasterOfPuppets/Lua/LuaActorFollowController.cs`
- `MasterOfPuppets/Lua/LuaScriptCatalog.cs`
- `MasterOfPuppets/Ipc/IpcProvider.Lua.cs`
- `MasterOfPuppets/Game/ChatWatcher.cs`
- `MasterOfPuppets/Ui/Windows/LuaScriptEditorWindow.cs`
- `MasterOfPuppets/Ui/Windows/LuaScriptsWindow.cs`
- `MasterOfPuppetsTests/LuaScriptRunnerTests.cs`
- `docs/lua-scripting.md`

## Required approach

Begin with an exhaustive machine-generated capability inventory plus a concise gap matrix covering language support, sandboxing, host APIs, lifecycle, scheduling, synchronization, movement, events, persistence, editor tooling, compatibility, security, and testing. Then present a phased implementation plan with explicit file-level changes and risks. After that, implement it. Do not merely propose pseudocode.

Use interfaces and deterministic pure components so that game-independent behavior can be tested without Dalamud or a running FFXIV client. Any state read by Lua must be an immutable snapshot or be marshalled safely to the framework thread. Never access a `LuaState` concurrently; Lua-CSharp documents it as non-thread-safe.

## 0. Exhaustive Dalamud and game-capability coverage contract

Do not treat the hand-written API examples in this prompt as the complete boundary. They are minimum examples. The long-term requirement is that no current or future Dalamud capability can be silently overlooked.

The locally installed development assemblies currently report:

- Dalamud `15.0.3.2`
- 44 concrete public service interfaces in `Dalamud.Plugin.Services`, plus the `IDalamudService` marker
- 673 exported types from `Dalamud.dll`
- 29,792 exported types across 125 namespaces from `FFXIVClientStructs.dll`

Confirm these values from the installed assemblies rather than hardcoding them. At build or development time, reflect over the referenced assemblies and generate a versioned coverage snapshot. Do not perform unrestricted CLR reflection from user Lua at runtime.

Maintain provider coverage through focused runtime tests. The provider surface must account for every public service and relevant public member, including:

- assembly and version
- namespace, service/type, and member signature
- property, method, event, or lifecycle source
- functional category
- Lua provider and Lua name
- access type: snapshot/read, event, command/action, mutation, infrastructure, or native/unsafe
- thread affinity and lifetime rules
- determinism and synchronization implications
- required capability/permission
- support state: `exposed`, `wrapped`, `event-adapted`, `advanced-provider`, `not-applicable`, or `blocked-by-platform-version`
- a concrete rationale for anything not exposed
- tests and documentation links

No member may remain unclassified. Add a CI test that compares the current assemblies with the checked-in coverage snapshot and fails with a readable diff when a service, member, event, enum value, or supported ClientStructs adapter changes. A Dalamud API bump must therefore create visible Lua-coverage work instead of silently shrinking the scripting surface.

The installed public service audit must include, at minimum:

- addon/UI lifecycle: `IAddonEventManager`, `IAddonLifecycle`, `IAgentLifecycle`, `IGameGui`, `IContextMenu`, `INamePlateGui`, `IDtrBar`, `IFlyTextGui`, `IToastGui`, `INotificationManager`, `ITitleScreenMenu`
- game/client state: `IClientState`, `IPlayerState`, `ICondition`, `IObjectTable`, `ITargetManager`, `IPartyList`, `IBuddyList`, `IAetheryteList`, `IFateTable`, `IDutyState`, `IJobGauges`, `IGameInventory`, `IUnlockState`, `IGameConfig`, `IGamepadState`, `IKeyState`
- chat/commands/text/data: `IChatGui`, `ICommandManager`, `ISeStringEvaluator`, `IDataManager`
- framework/lifecycle: `IFramework`, `IGameLifecycle`
- ecosystem/events: `IMarketBoard`, `IPartyFinderGui`, typed Dalamud IPC through `IDalamudPluginInterface`
- rendering/assets: `ITextureProvider`, `ITextureReadbackProvider`, `ITextureSubstitutionProvider`
- infrastructure/advanced: `IPluginLog`, `IConsole`, `IReliableFileStorage`, `ISelfTestRegistry`, `IGameInteropProvider`, `ISigScanner`, and `IDalamudPluginInterface`

Do not expose these as raw CLR objects. Implement an `ILuaCapabilityProvider`/registry architecture so each domain can contribute documented Lua modules, data converters, events, permissions, and version checks independently. Providers must be discoverable without editing the core runner.

Use three implementation tiers:

1. **Dalamud public-service tier:** expose game-facing public services through typed snapshots, event adapters, and validated operations.
2. **FFXIVClientStructs tier:** cover functionality absent from Dalamud with version-guarded adapters that convert pointers and native structures into safe immutable Lua data. Lua must never retain a pointer or native wrapper beyond the framework-thread snapshot that produced it.
3. **Advanced integration tier:** support new hooks, signatures, native calls, and third-party Dalamud IPC through separately registered providers with explicit version guards, capability declarations, schemas, diagnostics, and opt-in trust. Never hand arbitrary pointers, delegates, reflection, or generic CLR invocation to Lua.

Add Lua introspection so scripts can adapt across installations and versions:

- `mop.capabilities.list()`
- `mop.capabilities.has(name, minimum_version)`
- `mop.capabilities.describe(name)`
- `mop.capabilities.require(name, minimum_version)`
- `mop.runtime.dalamud_version()`
- `mop.runtime.game_version()`
- `mop.runtime.clientstructs_version()`

Capability absence must produce an explicit, actionable result. Scripts must be able to branch or degrade gracefully instead of failing because an API silently disappeared.

This coverage contract does not require a nonsensical one-to-one Lua wrapper for every rendering helper, generic overload, raw address, or plugin-development utility. It does require that every public capability be discovered and deliberately handled, and that every game-facing capability have a path to a typed Lua provider without redesigning the runtime.

## 1. Runtime host, sandbox, and resource governance

Introduce a dedicated runtime host instead of continuing to grow one monolithic registration method.

- Expose safe base, math, string, table, and coroutine functionality supported by the pinned Lua-CSharp version. Feature-detect and document runtime differences from native Lua 5.2. Do not claim unsupported functions.
- Continue to deny `io`, `os`, unrestricted `debug`, arbitrary `package` searchers, `dofile`, `loadfile`, raw filesystem/network access, environment access, process launch, and unrestricted CLR/userdata access.
- If `load` or dynamic code compilation is exposed, justify it, constrain it, and test its cancellation and budget behavior; otherwise keep it unavailable.
- Add a sandboxed in-memory module system for reusable user modules. Module names must be normalized, traversal-safe, size-limited, cycle-detected, cached per run, and included in the synchronized dependency manifest/hash. Never resolve arbitrary disk paths from Lua.
- Add enforceable limits: total run duration, cancellation latency, instruction/time-slice budget, maximum source/module size, recursion/stack protection where the runtime permits it, event queue size, pending waiter count, chat/action rate limits, and bounded log output. A tight infinite loop must remain cancellable promptly.
- Return structured Lua errors with script/module name, line/column when available, stack trace, error category, run ID, and participant slot. Do not swallow failures.
- Provide an injectable clock, scheduler, logger, game facade, and transport so tests use virtual time instead of real sleeps where possible.

## 2. Versioned and namespaced host API

Add `mop.api_version()` and a namespaced V2 API while retaining every existing flat function as a compatibility wrapper. Validate every argument, return consistent Lua tables, and document nil/error behavior. Prefer awaitable operations that return completion or a structured failure over fire-and-forget calls.

Implement at least these game, performance, and orchestration capabilities, reusing existing MoP services rather than duplicating command parsing:

### Runtime and lifecycle

- `mop.runtime.info()`, `mop.runtime.status()`, `mop.runtime.cancelled()`
- high-resolution shared choreography time, local monotonic time, run ID, seed, slot, participant roster, conductor identity, and launch variables
- pause, resume, stop, timeout-aware waits, periodic ticks, and `wait_until(predicate/options)` without busy polling
- deterministic per-run random streams; do not depend on ambient/global random state

### Actor and game-state observations

- immutable snapshots for self, selected target, focus target, run target, party members, synchronized participants, and visible actors
- make each band member fully self-aware within the information available to its own client: canonical name/world, content/entity/game-object identity, local/remote distinction, participant slot and role, job and level, HP/MP/CP/GP and maxima, position, rotation, velocity/movement state, target and target-of-target, status flags/effects, casting state/action/target/timing, performing state, mount, minion, online status, territory/map/instance, PvP/GPose/duty state, conditions, inventory/unlocks where requested, and current run/resource ownership
- expose awareness of other participants and visible actors through the same normalized schema, while distinguishing locally authoritative fields, visible-object observations, party data, and synchronized peer-reported state; never fabricate fields the receiving client cannot actually observe
- actor lookup by stable identity or normalized name with explicit ambiguity results; never silently choose a weak name match
- wait operations for actor visible/lost, target changed, condition changed, and proximity reached
- `mop.self.snapshot()`, `mop.target.snapshot(kind)`, `mop.actors.find(query)`, `mop.actors.list(filter)`, `mop.party.list()`, `mop.buddies.list()`, `mop.participants.list()`, and corresponding bounded event/wait APIs

Do not impose hard-coded category restrictions such as “combat” versus “non-combat” on the Lua architecture. If a capability is already supported by Master of Puppets or is reliably available through Dalamud/FFXIVClientStructs, design a validated Lua abstraction for it, including combat state and combat-action routines. Distinguish observation, one-shot action execution, continuous control, and cross-client automation through capability metadata and user-configurable permissions rather than permanently excluding whole scenarios. Document operational risks, throttling, game-state preconditions, and platform-policy implications without reducing the generality of the scripting model.

### Actions and commands

- safe single-line game/chat command dispatch with centralized length validation, throttling, and cancellation
- convenience APIs for emotes, expressions, fashion accessories, minions, mounts, gearsets, walk/run mode, facing, and stop movement, backed by existing MoP services
- distinguish local-only, current-PC broadcast, participant-targeted, and cross-PC synchronized scope explicitly; never make a local call broadcast accidentally
- support band dialogue and conversation engines across all intentionally supported chat channels: send text/commands, observe typed chat events, filter by channel/speaker/payload, wait for a line or pattern, assign speakers by participant role, coordinate turn-taking, handle timeouts/retries, and preserve deterministic ordering across clients
- expose action execution and observable action/cast state through typed IDs and Lumina-backed metadata so scripts can reason about what they and visible actors are doing instead of parsing display strings

### Existing macros and formations

- find/list/run/await/stop/pause/resume macros by stable name or ID, pass variables, and report completion/error/cancellation
- find/list/inspect/run/await/stop saved formations and formation points without mutating the definitions
- expose participant formation order and group membership read-only
- define resource ownership so a Lua run, macro, formation, and movement controller cannot silently fight over movement or action queues

### Movement and paths

- point movement, relative movement, face actor/direction, follow actor with fallback candidates, stop, and wait for arrival
- a path/choreography API accepting desired position, tangent, intended world speed, optional curvature/look-ahead, and phase
- clear anchor-loss, zoning, user-input, stuck, and cancellation policies

### Events and coordination

- a bounded event stream or awaitable event API for chat, actor visibility, target changes, condition changes, movement completion/failure, macro/formation completion, variable changes, synchronization messages, pause/resume, and cancellation
- subscriptions must be disposed automatically with the run, use bounded queues, be thread-safe, and never invoke the same Lua state concurrently
- shared run variables and typed participant messages with versioned schemas, ordering, deduplication, size limits, and timeouts

If a requested API cannot be implemented reliably from current Dalamud/public game state, omit it and document the limitation rather than fabricating behavior or depending on unstable memory access without isolation and tests.

## 3. Deterministic distributed choreography

Replace the current second-boundary launch with a versioned run protocol.

- Give every run a unique run ID, protocol version, script hash, module/dependency manifest hash, authoritative ordered participant CIDs, conductor identity, target identity, seed, variables, and a high-resolution future epoch.
- Implement `PREPARE/STAGE -> READY -> GO -> RUNNING -> STOP/COMPLETE/ERROR` states.
- Stage every participant at a static initial slot. A participant becomes ready only after it is within configurable position/facing tolerance and settled for a configured interval.
- Aggregate readiness across clients on the same PC, then across PCs. Start with a distinct future `GO` epoch only when the authoritative roster is ready or a clearly configured timeout policy is reached.
- Estimate shared-clock offset and continuously correct phase with filtered, bounded adjustments. Do not permanently preserve receipt-time or frame-stall errors in independent stopwatches. Do not snap characters when correcting clock error.
- Add heartbeat, ACK/NACK, timeout, idempotency, replay protection, duplicate suppression, protocol-version rejection, conductor-loss handling, late-start recovery, and explicit diagnostics.
- Respect the 500 UTF-8-byte chat-command limit. Keep source/modules local for cross-PC runs and verify the entire dependency manifest. Use compact versioned envelopes and chunking only if ordering, integrity, timeout, and abuse limits are implemented and tested.
- Add a trusted-conductor policy for cross-PC run and stop messages. At minimum support an explicit allowlist and “self only” default. Validate the actual chat sender separately from envelope content. A random chat participant must not be able to run or stop scripts.
- Do not execute source received over local IPC by default merely because its self-declared hash matches. Prefer matching an installed local trusted script/dependency manifest. If transmitted-source mode is retained, make it explicit, opt-in, visibly warned, size/rate limited, and covered by trust tests.

## 4. Dedicated choreography locomotion

Do not use the generic formation `MoveTo` controller as the final implementation for time-varying paths.

Create a dedicated, testable trajectory/path follower that combines:

- forward tangential travel that preserves the requested path direction
- cross-track/radial correction
- along-track phase-error correction
- bounded steering/turn rate
- automatic walk/run selection, safe duty cycling, or another proven speed-control method appropriate to available FFXIV input
- speed/acceleration limits and a look-ahead target
- early/late policies: slow or hold early actors; allow late actors to catch up or use a safe forward intercept
- prevention of reverse direction and center-cutting unless the script explicitly enables it
- stable behavior when the anchor moves, disappears, or reappears
- graceful late join and displacement recovery without changing the participant’s assigned slot

Keep path math pure and unit-testable. Add diagnostics for requested speed, chosen locomotion mode, cross-track error, phase error, clock offset, readiness, current anchor identity, and recovery state.

The first real acceptance choreography is one stable 32-participant ring around a moving target. Do not move on to elaborate spirals or bee-like randomness until the one-ring test is correct.

## 5. Run management and resource arbitration

Replace the single opaque `_runCts` model with explicit run instances and statuses.

- Support unique run IDs and queryable histories.
- Multiple scripts may coexist only when their declared capabilities/resources do not conflict.
- Add leases/arbitration for movement, chat/action rate budget, macro queue, formation tracking, and synchronized control. Reject or queue conflicts deterministically and show the reason.
- Stop, pause, resume, restart, and status commands must be targetable by run ID or script name while preserving the legacy “stop current/all” behavior.
- Plugin unload, logout, zoning, character change, and exceptions must cancel runs, dispose subscriptions, release resources, and stop injected movement safely.

## 6. Script schema, compatibility, and editing experience

- Add a versioned script/bundle schema with stable script ID, display name, source, modules, declared capabilities, typed parameter definitions/defaults/ranges, participant formation, tags, revision, and content/dependency hashes.
- Migrate current `LuaScriptDefinition` entries without data loss. Existing scripts and all 18 current flat APIs must continue to run.
- User-imported scripts must never be overwritten implicitly. Make upgrades explicit and diffable.
- Validate compile/syntax before save and before launch. Show line-aware errors in the editor.
- Add an API reference/capability panel, parameter controls generated from typed definitions, run/stop/pause controls, status by participant, and a bounded per-run log/error console.
- If practical in the existing ImGui stack, add line numbers and basic syntax coloring. Correctness and diagnostics take priority over a sophisticated editor widget.
- Import/export must be versioned, size-limited, validated, and backward compatible. Reject malformed or unexpectedly large data without changing configuration.

## 7. Tests and verification

Repair and establish a reproducible baseline first. Then add tests proportional to risk.

Required automated coverage:

- safe standard-library availability and denied-library/sandbox escape tests
- module loading, caching, cycles, traversal attempts, manifest hashing, and size limits
- syntax/runtime error locations and stack traces
- cancellation of waits, event waits, coroutines, and tight infinite loops within a defined latency
- instruction/time/log/event/action quotas
- legacy flat API compatibility
- argument validation and structured return/error behavior for every new API family
- immutable game snapshots and framework-thread marshalling
- macro/formation adapters and resource conflicts
- deterministic random streams and virtual-clock tests
- protocol encode/decode, maximum-size payloads, corruption, version mismatch, replay, duplicates, out-of-order messages, trust rejection, and sender/envelope mismatch
- readiness barriers, missing participants, timeouts, clock skew, heartbeat loss, phase correction, and late join
- pure movement simulations for on-path, displaced, early, late, slow/walk, fast/run, moving anchor, lost anchor, and reacquisition cases
- invariant/property tests: finite outputs, bounded acceleration/turning, no unintended reversal, no center crossing, stable slot order, and converging phase/cross-track error
- generic Lua runtime and coordination smoke tests using test-owned inline sources
- import/export and configuration migration round trips

Run restore, build, and the entire test suite. Report exact commands and results. Do not hide pre-existing failures; distinguish them from regressions. Do not weaken assertions or delete tests to get green results.

## 8. Documentation and sample deliverables

Update the user and developer documentation so it matches actual behavior. Remove stale machine-specific paths.

Deliver:

- Lua language/runtime compatibility and sandbox reference
- complete versioned `mop` API reference with signatures, return shapes, errors, scope, threading, and examples
- lifecycle, cancellation, quotas, and resource-conflict behavior
- local-PC versus cross-PC synchronization/trust behavior
- migration guide for existing scripts
- troubleshooting guide with run-ID diagnostics
- at least three polished scripts demonstrating: event-driven theatre, macro/formation composition, and synchronized path choreography
- a limitations/non-goals section that honestly explains what cannot or should not be automated

## Acceptance criteria

The work is not complete until all of the following are demonstrated:

1. Existing Lua scripts run unchanged, and the legacy API is covered by compatibility tests.
2. A new script can use safe string/table/coroutine features and reusable sandboxed modules without gaining disk, OS, network, process, debug, or CLR access.
3. Syntax errors are rejected before launch with actionable source locations.
4. Tight loops, waiters, event handlers, and long runs cancel promptly and release all movement/resources.
5. Lua can compose existing macros and saved formations, await their results, observe theatrical game state, and react to bounded events.
6. Unauthorized Chat Sync senders cannot run or stop Lua scripts. Replays, malformed envelopes, mismatched manifests, and unsupported protocol versions are rejected.
7. Distributed runs use a readiness barrier and high-resolution, corrected shared time; logs expose run ID, slot, clock offset, readiness, and phase error.
8. The 32-participant single ring converges to even spacing, preserves order/direction, follows a moving anchor, accepts a deliberately late participant without center-cutting, and does not form persistent per-PC phase clusters.
9. All automated tests pass from a clean restore/build sequence, with the exact count and commands reported.
10. No unrelated user changes or live configuration data are modified, and no live plugin deployment occurs without explicit permission.

## Working style and final report

- Make small, reviewable, cohesive changes and verify each phase before continuing.
- Prefer capability-oriented interfaces and composition over a giant `mop` registration class or calls back through text commands.
- Reuse existing services, but separate Lua-facing contracts from Dalamud/game implementations.
- When a requirement is blocked by an actual platform limitation, provide evidence, implement the strongest safe alternative, and document the limitation.
- Design toward maximum practical coverage of Dalamud-accessible game scenarios. Describe the implemented surface precisely, distinguish current capabilities from extension points, and identify only genuine technical or host-security limitations.

At the end, provide:

- the architecture and security decisions made
- a concise file-by-file change summary
- compatibility/migration notes
- exact restore/build/test results
- remaining limitations and risks
- a manual multi-PC validation checklist for the 32-character ring and other sample shows

---

## Why this specification is structured this way

The current project already proves that Lua code can execute, wait asynchronously, publish movement samples, coordinate exact `/say` lines, and launch across local and remote clients. The largest gap is not whether Lua is Turing-complete; it is whether the host API, distributed clock, movement control, trust model, lifecycle, and diagnostics are reliable enough for a large synchronized show. This prompt makes those foundations measurable before expanding into more elaborate scripts.
