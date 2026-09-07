# Lua Emote Movement Policy

Automatic Lua movement must cancel persistent emotes when locomotion actually
begins. A follower that is already within its slot precision must not have its
persistent emote cancelled merely because a follow loop is running. The shared
`follow_actor` API and stock formation scripts must not expose a preservation
flag or timing-based exception.

If a future choreography genuinely needs an actor to retain an emote while
moving, implement it as a separate bespoke script with an explicit name and
documented opt-in behavior. Do not restore a general `preserve_emote` option to
`follow_actor` or the stock formations. That exception must be isolated to the
bespoke script so ordinary movement remains fail-closed.
