# Lua Automation V2 runtime architecture

This document records the implemented V2 runtime foundation and the phased path
to the full acceptance target in
[`LUA_AUTOMATION_V2_GOAL.md`](LUA_AUTOMATION_V2_GOAL.md). It describes current
code, not aspirational APIs, unless an item is explicitly marked as planned.

## Dependency direction

```text
LuaScriptRunner
    -> LuaRuntimeHost
        -> LuaCapabilityRegistry
            -> ILuaCapabilityProvider implementations
        -> LuaInMemoryModuleLoader
        -> LuaScriptValidator
        -> LuaScriptContext (host callbacks and immutable launch data)

Dalamud/native adapters
    -> immutable snapshots / validated operations
        -> capability providers
            -> Lua tables and scalar values
```

Lua code never receives a Dalamud service, CLR type, delegate, pointer, native
wrapper, filesystem, process, network client, or reflection API. Game state must
be captured on the framework thread and converted to immutable data before it
crosses into a run.

## Implemented foundation

- `LuaRuntimeHost` owns exactly one non-thread-safe Lua state for one run.
- `LuaCapabilityRegistry` discovers parameterless providers from the plugin
  assembly and rejects duplicate capability names. Adding a provider does not
  require editing the runner.
- `RuntimeLuaCapabilityProvider` exposes API version 4.0 and capability/runtime
  introspection.
- `LegacyLuaCapabilityProvider` preserves all 18 original flat functions.
- Non-empty capability declarations allowlist providers per run; empty
  declarations retain the legacy all-provider compatibility mode.
- `GameStateLuaCapabilityProvider` 4.0 exposes immutable self, target, visible
  actor, party, participant, player-profile, territory, and condition snapshots,
  plus shared arbitrary-player watches with monotonic revisions, cached
  visibility/proximity/change/job waits, and source-scoped action, emote,
  jump, Sprint, appearance, target, idle-pose, and weapon reactions
  captured on the framework thread. `AutomationLuaCapabilityProvider` composes existing macro
  and formation managers with explicit execution scope.
- Safe base, math, string, table, bitwise, and coroutine libraries are open.
  `io`, `os`, `debug`, `package`, `dofile`, `loadfile`, and dynamic `load` are
  unavailable. `print` is routed through the host logger.
- `math.random` and `math.randomseed` use a stable run-local XorShift64* stream
  seeded from the synchronized run seed.
- `LuaScriptValidator` compiles source before save/import and immediately before
  launch, returning chunk, line, column, and nearby-token diagnostics.
- `LuaInMemoryModuleLoader` implements bundle-only `require`. Module names are
  dot-separated identifiers; traversal and filesystem paths are rejected.
  Modules are syntax checked, size limited, cached per Lua state, and included in
  deterministic dependency and bundle hashes.
- `LuaRunQuota` centrally bounds log lines/bytes, pending asynchronous waiters,
  total chat actions, and chat action rate. Runtime introspection reports current
  quota usage. Observed chat replay history is capped independently.
- Local IPC and Chat Sync execute only a matching, locally installed bundle.
  Protocol envelope v6 verifies the complete schema-v2 contract hash, including
  source, modules, capabilities, resources, typed parameters, and stable ID. It
  also carries replay-protected message identity and creation time.
- Cross-PC Lua control uses the normal Chat Sync channel and optional sender
  whitelist, without separate conductor authorization. A bounded replay window
  rejects duplicate, stale, future-dated, and malformed control envelopes.
- The pure distributed protocol core models authoritative rosters,
  PREPARE/STAGING/READY/GO phases, settled-position readiness and regression,
  explicit timeout policies, heartbeats, participant terminal state, and
  idempotent future GO epochs. A bounded NTP-style estimator supplies clock
  offset, RTT, and jitter diagnostics.
- A checksummed compact wire codec covers PREPARE, STAGE/READY, GO, clock
  probe/reply, conductor-owned shared variables, ordered schema-versioned
  participant messages, heartbeat,
  ACK/NACK, stop, completion, and error messages. Its 32-CID PREPARE frame is
  verified below the 500-byte chat command limit. Internal Chat Sync handlers
  enforce sender/CID identity, run ownership, replay protection, roster
  membership, and coordinator phase ordering; a bounded registry feeds
  participant diagnostics to the Scripts window. An opt-in public launch bridge
  stages the local formation slot under a temporary resource lease, emits
  settled/regression observations, schedules conductor GO, launches pending Lua,
  reports heartbeats/terminal state, and continuously filters conductor clock
  offset. The corrected shared time feeds Lua runtime elapsed time and movement
  trajectory timestamps. It defaults off until live validation.
- `mop.events` 3.0 exposes a per-run bounded typed stream with non-blocking poll,
  cancellable non-destructive filters, overflow accounting, opt-in raw
  high-volume streams, and automatic disposal. Producers do
  not invoke Lua directly. A throttled pure snapshot differ emits target,
  condition, and participant visibility changes in addition to lifecycle and
  chat events. Automatic event observation uses a narrow framework-thread
  capture containing only the fields consumed by that differ; the full visible
  actor snapshot is built only for an explicit game-state API request. Distributed
  launch work runs at 20 Hz and session timeout housekeeping at 4 Hz rather than
  allocating at render rate.
- `mop.actions` 4.0 adds validated single-line commands, typed action/general
  action/item operations, local jump/sprint helpers, script-selected exact
  gearsets/jobs, appearance and target operations, walk mode, and movement stop
  with explicit local/current-PC scope, resource leases, and the centralized
  action quota.
- Explicit multi-run instances provide deterministic IDs, lifecycle history,
  pause/resume/stop/restart, structured errors, instruction/stack/duration
  limits, and exclusive leases for movement, chat/actions, macro queues,
  formation tracking, synchronized control, and game actions.
- A pure circular trajectory follower supplies forward-only phase/radial
  correction, bounded acceleration/turning, walk/run/hold selection, and
  center-crossing protection. Its moving-anchor acceptance simulation covers 32
  participants for 75 seconds.
- The generated coverage ledger classifies every public Dalamud service/member,
  every exported ClientStructs type, and all public ClientStructs enum values.
  A test compares it to the installed SDK assemblies.

## Provider contract

Each `ILuaCapabilityProvider` declares a stable name, semantic version,
description, and required permissions. Registration receives the run-local
`mop` table, Lua state, immutable launch context, monotonic clock, and capability
descriptors. Providers should create a focused namespace such as `mop.runtime`,
`mop.actors`, or `mop.macros`; they must not add raw CLR objects.

Provider files belong under `MasterOfPuppets/Lua/Providers`. Runtime mechanisms
belong under `MasterOfPuppets/Lua/Runtime`. Game-independent contracts and pure
algorithms must stay testable without a running FFXIV client. Dalamud and native
implementations should live in adapter-specific directories as they are added.

## Phased implementation plan

### Phase 0 — inventory and runtime foundation (implemented)

Capability decisions are maintained with the runtime providers and tests,
`Lua/Runtime/*`, `Lua/Providers/*`, `LuaScriptDefinition`, IPC envelope codec,
and Lua tests.

Risk controlled: silent SDK capability drift, sandbox expansion, source syntax
failures at runtime, nondeterministic random state, and module path escape.

### Phase 1 — run instances and governance (implemented foundation)

The manager now owns explicit run records, status/history, structured errors,
pause/resume/stop/restart, instruction and stack budgets, duration timeouts, and
deterministic resource leases while preserving legacy stop-all behavior. Plugin
unload, logout/character transition, and territory changes cancel active runs
and stop movement before normal terminal cleanup releases leases.

Primary files: `LuaScriptManager`, new `Lua/Runs/*`, runtime limits and scheduler,
command/UI status surfaces, and lifecycle tests.

Risk: resource cleanup spans the framework thread and background Lua task.
Movement must always stop and leases must release on every terminal path.

### Phase 2 — typed game, macro, formation, and event providers (partial)

Immutable actor/game snapshots, validated game actions, and typed adapters for existing macros and saved
formations are implemented with explicit scope and lease checks. Cancellable
local macro/formation completion waits publish terminal automation events.
Bounded typed events are implemented; broader Dalamud event adapters and
cross-PC completion aggregation and broader service families remain pending. The coverage ledger is updated
from gaps to concrete provider paths as members are handled.

Primary files: new provider contracts and Dalamud adapters, macro/formation
completion contracts, event queues, framework-thread marshalling tests.

Risk: current macro APIs are queue-oriented and not all have completion handles.
Adapters must report honest completion/failure rather than fabricate it.

### Phase 3 — trusted distributed run protocol (implementation complete; live validation pending)

The pure coordinator implements authoritative roster/manifests,
`PREPARE`/staging/`READY`/`GO`, heartbeats, stop/terminal state, readiness
aggregation and regression, timeout policies, and bounded clock estimation.
Chat control now has exact conductor validation and replay protection. Compact
transport envelopes carry these phases plus clock probes/replies, and bounded
continuous correction feeds the shared Lua choreography clock. A client up to
five seconds late resumes at the original phase; older joins are rejected.
Conductor heartbeat loss deterministically stops the session and local run;
automatic conductor election is deliberately not attempted.

Primary files: `Lua/Synchronization/*`, Chat Sync policy/configuration, IPC
transport adapters, protocol and virtual-clock tests.

Risk: the 500-byte chat command ceiling. Source remains local for cross-PC runs;
compact envelopes must reject oversized rosters instead of truncating them.

### Phase 4 — dedicated choreography locomotion (simulation foundation implemented)

A pure trajectory guidance model now supplies tangential travel, radial/phase
correction, bounded steering/acceleration, walk/run/hold selection, and a
framework-thread movement adapter with fallback for non-circular scripts. The
32-actor moving-anchor simulation passes; multi-PC live validation remains.

Primary files: `Lua/Choreography/*`, movement adapter, simulation/property tests,
and the stable single-ring example.

Risk: visually plausible tests are insufficient. The acceptance gate is a
32-participant moving-anchor ring with no center cutting, reversal, slot swaps,
or persistent per-PC phase clusters.

### Phase 5 — bundle/editor and polished examples (bundle/editor foundation implemented)

The versioned bundle schema, typed parameters/capabilities/resources, migration,
module editor, per-run lifecycle controls, and bounded retained run/error log
console and participant protocol diagnostics are implemented. API completion
and the multi-PC validation checklist remain. The packaged **Event-Driven
Curtain Call** demonstrates typed cue handling plus optional formation/macro
composition and completion waits.

## Removal and migration policy

Existing flat functions and source-only configuration entries remain supported.
New fields use defaults so old JSON deserializes without data loss. A legacy
surface may be removed only after all callers, imports, docs, tests, and packaged
examples migrate and a documented compatibility window exists. Git history is
the archive; obsolete parallel implementations should not remain as commented
blocks.
