# Macro Variables & Expressions

Macros can use **variables** (named values) and **expressions** (arithmetic and
tokens evaluated when each action runs) so a single macro adapts per character,
per loop iteration, and to live changes without editing or restarting.

This page is the reference for the built-in variables, how to declare and
override variables, the arithmetic/expression features, and the difference
between the two wait commands (`/mopwait` vs `/mopphasewait`).

---

## 1. Declaring Variables

A variable is a line of the form `$name=value` in the macro's **Variables**
field or as one of the command's action lines:

```text
$emote=/surprised
$interval=0.80
$count=7
```

A variable name must start with a letter or `_` and contain letters, digits, or
`_` (e.g. `$totalWait`). Values may be any text, and may reference other
variables or arithmetic (see [Expressions](#4-expressions)).

### 1.1 Precedence

When the same name is defined in more than one place, the highest-precedence
one wins:

| Source | Precedence |
| :--- | :--- |
| Characters / runtime values (`$me`, `$target`, ...) | 1 (lowest) |
| Macro body `Variables` field | 2 · overrides runtime vars |
| Command action-line `$var=...` | 3 · overrides macro vars |
| Launch / call-time inline vars (`-var=$name=value`) | 4 |
| Built-in auto vars (`$commandIndex`, `$commandCount`, ...) | 5 (highest) |

This lets a whole macro share a variable, while a single command can shadow it,
and while a live update can override it further.

---

## 2. Built-in Variables

These are provided by the plugin automatically; you do not declare them.

### 2.1 Runtime variables

Populated from the local client's game state when a macro starts. Overridable
by action-line or inline definitions.

| Variable | Meaning |
| :--- | :--- |
| `$me` | The local character's name, formatted `Name@World` when a world is known. |
| `$target` | The local character's current target's name. |
| `$ftarget` | The local character's current focus target's name. |
| `$job` | The local character's current job abbreviation (e.g. `DNC`, `SGE`). `$class` is an alias. |
| `$level` | The local character's current level. |
| `$world` | The local character's home world name. |
| `$leader` | The local party leader's name, formatted `Name@World` when a world is known. Empty when not in a party. |
| `$mop_origin` | The macro launching character (same as `$me` on the local client). |
| `$mop_origin_target` | What the launching character was targeting. |
| `$mop_origin_ftarget` | What the launching character had as focus target. |
| `$globaldelay` | The plugin's configured **Delay Between Actions** in seconds (default `0.5`). Useful for validating or deriving timing against the real global delay. |

### 2.2 Structure auto-variables

These come in two families. Both are **authoritative**: you can't override
them. Use them to derive per-character stagger lanes without hardcoding a
count or an index.

**Command level — the macro's structure.** Which command is running, and how
many commands the macro has.

| Variable | Meaning |
| :--- | :--- |
| `$commandIndex` | 0-based position of this command within the macro's command list. |
| `$commandCount` | Total number of commands in the macro. |

**Assignment level — this command's targets.** Which character "lane" this
character is among everyone the matched command targets. A command can target
characters directly (its `cids`) **and/or** via configured groups (`groupIds`).
The assignment lane is the **union** of those targets, in **author-listing
order**: direct `cids` first (in the order listed), then each group's cids (in
the group's listed order), de-duplicated so the first-seen position wins.

| Variable | Meaning |
| :--- | :--- |
| `$assignmentIndex` | This character's 0-based stagger lane among the matched command's unioned targets. |
| `$assignmentCount` | Total number of characters the matched command targets (union size). |

### 2.3 Which do I use?

Use **`$assignmentIndex`/`$assignmentCount`** for anything that staggers or
spreads the characters a single command targets (rotating emotes, staggered
moves). Because the lane is derived from the command's own targets — including
group-assigned characters — a **single command with identical text** drives the
whole stagger, and adding a character (direct or via a group) renumbers the
lanes for you.

Use **`$commandIndex`/`$commandCount`** when you need to know the macro's own
structure — e.g. "this is the 2nd command of 3", so the first command can do
setup while later commands run the repeating body.

Example contrast — macro with 3 commands, where command #2 targets characters
`A`,`B`,`C`:

| For character C | `$commandIndex` | `$assignmentIndex` | `$commandCount` | `$assignmentCount` |
| :--- | :--- | :--- | :--- | :--- |
| command #2 of 3, lane 2 of 3 | 1 | 2 | 3 | 3 |

The `$assignment*` values describe where the character sits among its
command's targets; the `$command*` values describe where its command sits in
the macro.

### 2.4 Inline placeholders

Passed on the command line and resolved against runtime values:

| Placeholder | Meaning |
| :--- | :--- |
| `[me]` | The local character's name (in inline variable values). |
| `[t]` | The local character's current target (in inline variable values). |

---

## 3. Referencing Variables

Use `$name` anywhere in an action line; it is substituted with the variable's
resolved value:

```text
/mopphasewait $totalWait
$emote
```

Substitution is **case-sensitive**, repeats until stable, and simple
`$var→value` replacement. Referencing a variable that is itself an expression
resolves to the value (see below).

---

## 4. Expressions

### 4.1 Arithmetic in variable definitions

A variable whose value is a pure arithmetic expression is evaluated
automatically. Supported operators: `+ - * / % ^`, parentheses, and unary
minus; numbers are parsed culture-invariantly and results round to 2 decimals.

```text
$interval=0.80
$count=7
$offset=$assignmentIndex * $interval      → e.g. 0, 0.8, 1.6, ...
$totalWait=$count * $interval            → 5.6
$scaled=($offset + 1) * 2                → nested arithmetic
```

Because evaluation happens when each action resolves, **live `/mop setvar`
updates re-derive dependent variables automatically** (see
[Configuration & Live Variables](configuration-and-live-variables.md)).

Non-arithmetic values (e.g. `/clap`, `some name`) are left untouched, so a
variable can hold a command or a name and be referenced safely.

### 4.2 `{random(...)}` token

Replaced at execution time by a random value; re-evaluated on **every**
execution, including each loop iteration.

| Form | Behavior |
| :--- | :--- |
| `{random(a,b)}` — integers | Random integer from `a`..`b` inclusive. `{random(1,5)}` → 1, 2, 3, 4, or 5. |
| `{random(a,b)}` — decimals | Random float in `[a,b]`, 2 decimal places. `{random(1.5,3.5)}` → e.g. 2.17. |
| `{random(v1,v2,v3,...)}` — list | Picks one value from the set. `{random(1,3,7,12)}` → one of 1, 3, 7, 12. |

```text
/mopwait {random(1,3)}
/gs change {random(1,3,7,12)}
/say Rolling {random(1,100)}!
```

### 4.3 `{calc(...)}` token

Evaluates an arithmetic expression inline at execution time. Runs after
`{random(...)}` and after `$var` substitution, so it can combine both:

```text
/mopphasewait {calc(0.8 * 7)}         → 5.6
/mopphasewait {calc($interval * 7)}   → uses substituted variables
/mopphasewait {calc($k * {random(1,3)})} → random folded into arithmetic
```

An invalid expression is left as-is rather than breaking the action.

`{calc(...)}` is for a **one-off inline** computation on a single action line.
If the expression is a value you want to name and reuse (and have stripped from
the emitted action), **declare a variable** instead: `$totalWait=$count * $interval`
(see [§1 Declaring Variables](#1-declaring-variables) and §4.1) and reference
`$totalWait`. Both evaluate the same arithmetic; declared variables give you a
named, reusable shorthand while `{calc(...)}` computes inline without a name.

---

## 5. Worked Example: Staggered Rotating Emote

The 7-character emote loop is **one command** targeting all 7 characters with
**identical text**. There is no hardcoded count and no per-character index —
each character derives its own offset from its own `$assignmentIndex`.

Macro **Variables**:
```text
$emote=/surprised
$interval=0.80
```

The **single command** (all seven `cids`, one shared `actions` template):
```json
{
  "cids": [CID_1, CID_2, CID_3, CID_4, CID_5, CID_6, CID_7],
  "actions": "$offset=$assignmentIndex * $interval\n$totalWait=$assignmentCount * $interval\n$tail=$totalWait - $offset\n/mopphasewait $offset\n$emote\n/mopphasewait $tail\n/moploop"
}
```

* Every character runs the same text, but `$assignmentIndex` differs per
  character (its lane among the `cids`): the 5th listed character gets index
  `4` and starts at `4 * 0.8 = 3.2s`.
* Total cycle is `count(7) * 0.8 = 5.6s`.
* The two phase-waits per loop advance the absolute phase clock by exactly one
  cycle: `$offset` (this character's lane) + `$tail` (`$totalWait - $offset`) =
  `$totalWait`. Without the `$tail` correction the clock would advance `$offset`
  too far every loop and the emote would fall behind on each rotation.
* Add/remove a CID and every offset and the total recompute automatically.

You get the same targeting from a configured character group instead of a
literal CID list — set the command's `groupIds` to the group name. Group-assigned
characters receive their `$assignmentIndex` lanes automatically (union order:
direct `cids` first, then groups).

Launch across all clients with chat-sync broadcast:
```text
mopbr moprun "Rotating Emote"
```

---

## 6. `/mopwait` vs `/mopphasewait`

Both commands pause the macro; the difference is **how the pause is measured**.

| | `/mopwait <seconds>` | `/mopphasewait <seconds>` |
| :--- | :--- | :--- |
| Timing | **Relative** — always waits the full duration from the moment it runs. | **Absolute** — keeps a timeline from macro start; waits only the time left until the phase deadline. |
| Drift | Latency (command exec, framework frames, global delay) **accumulates** every loop → phases slowly drift apart. | Latency is absorbed: an overdue phase waits zero without pushing later phases later. Drift stays **bounded** (jitter), not cumulative. |
| Global delay | The configured global delay is applied **in addition**. | The phase interval is the **complete** budget (includes the global delay); `/mopphasewait` itself skips the extra global delay. |
| Use case | Simple "wait this long then continue". | Repeating/synchronized loops where characters must stay in lock-step (formations, rotating emotes). |
| On late phase | Always adds full time. | Returns immediately, timeline not rebased. |

```text
/mopwait 0.5        # relative: always 0.5s, plus global delay, plus latency
/mopphasewait 0.5   # absolute: fits this action into a 0.5s phase slot
```

Practical effect for a repeating loop:

* `/mopwait 0.5` → every iteration is `0.5s + latency`, so the cycle
  slowly gets longer over many loops.
* `/mopphasewait X` (where `X` = full interval, e.g. `0.75` for a `0.25`
  global delay + `0.5` action) → every iteration lands on the same absolute
  schedule; latency just causes tiny temporary jitter that recovers.

For a rotating emote you want all seven characters to hit their phases on a
shared schedule, so use `/mopphasewait` with `$offset` and `$totalWait`—as in
the worked example above. Use `/mopwait` only when you genuinely need a fixed
relative pause after a specific action.

See also [Phase-Locked Macro Timing](phase-locked-macro-timing.md) for the
full drift-elimination design.
