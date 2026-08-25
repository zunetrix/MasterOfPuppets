# Macro Conditionals (`/mopif`, `/mopelseif`, `/mopelse`, `/mopendif`)

## Overview

Master of Puppets macros support branching and dynamic control flow using conditional statements. Macros can inspect the current game state, local player conditions, party status, target properties, entity visibility, and custom variables before executing actions.

Conditionals are supported in two forms:
1. **Block syntax**: Multi-line branching using `/mopif`, `/mopelseif` (or `/mopelif`), `/mopelse`, and `/mopendif` (or `/mopfi`).
2. **Inline syntax**: Single-line conditional execution using `/mopif <condition> /<action>`.

---

## 1. Syntax Forms

### 1.1 Block Syntax

```text
/mopif <condition>
    /command 1
    /command 2
/mopelseif <condition2>
    /command 3
/mopelse
    /command 4
/mopendif
```

* Blocks can be nested to arbitrary depths.
* `/mopelif` is supported as an alias for `/mopelseif`.
* `/mopfi` is supported as an alias for `/mopendif`.

### 1.2 Inline Syntax

```text
/mopif <condition> /<action>
```

If the condition is met, the single action following the slash is executed immediately.

**Examples:**
```text
/mopif target /mopformationgoto "Circle" 2 target precise
/mopif notarget /mopmoverelativeto 0 0 1 "Leader"
/mopif incombat /say Entering combat mode!
```

---

## 2. Condition Expression Reference

### 2.1 Targeting State

| Keyword / Condition | True When |
| :--- | :--- |
| `target`, `hastarget`, `has_target`, `hast` | Local player currently has a valid target selected. |
| `notarget`, `no_target` | Local player currently has no target. |
| `targetispc`, `targetisplayer` | Current target is a player character (PC). |
| `targetisnpc` | Current target is a battle NPC or event NPC. |
| `focustarget`, `hasfocustarget`, `has_focustarget` | Local player has a valid focus target set. |
| `nofocustarget`, `no_focustarget` | Local player does not have a focus target. |

### 2.2 Player & Party State

| Keyword / Condition | True When |
| :--- | :--- |
| `incombat`, `inbattle` | Local player is currently in combat (`ConditionFlag.InCombat`). |
| `outcombat`, `notincombat`, `outofcombat` | Local player is not in combat. |
| `isperforming`, `performing` | Local player is in performance mode (`ConditionFlag.Performing`). |
| `notperforming` | Local player is not performing. |
| `isalive`, `alive` | Local player HP > 0 and not unconscious. |
| `isdead`, `dead` | Local player HP == 0 or in unconscious state. |
| `isleader`, `leader` | Local character is the leader of the current party. |
| `inparty` | Local character is in a party. |

### 2.3 Object Table Queries

| Expression | True When |
| :--- | :--- |
| `visible "<Name>"` | An entity with `<Name>` in its character name is present and visible in the local object table. |
| `exists "<Name>"` | Alias for `visible "<Name>"`. |

*Note: The name query is case-insensitive and supports substring matching.*

### 2.4 Value Comparisons (`==` and `!=`)

You can compare special game variables, target names, player jobs, or custom macro variables using equality (`==`) or inequality (`!=`):

```text
/mopif target == "Boss Name"
/mopif job == "WHM"
/mopif "$role" == "Tank"
/mopif "$mode" != ""
```

#### Supported Left/Right Resolvers:
* **Target Name**: `target`, `target.name`, `<t>` → Resolves to current target's name (or empty string if none).
* **Focus Target Name**: `focustarget`, `focustarget.name`, `<f>` → Resolves to focus target's name.
* **Self Name**: `me`, `self`, `<me>` → Resolves to local player's name.
* **Current Class / Job**: `job`, `class` → Resolves to the 3-letter class/job abbreviation (e.g. `PLD`, `WHM`, `BLM`, `DNC`).
* **Macro Variables**: Any `$variable` name declared in the macro or passed via `-var=$variable=value`.

*Note: String comparisons are case-insensitive and allow substring containment unless comparing against an empty string `""`.*

### 2.5 Logical Operators & Grouping

Expressions can be combined with boolean logic:
* **Conjunction (AND)**: `&&` or ` and `
* **Disjunction (OR)**: `||` or ` or `
* **Negation (NOT)**: `!` or `not `
* **Grouping**: Enclosing sub-expressions in parentheses `( ... )`

---

## 3. Practical Examples

### 3.1 Formation Target Fallback

Move into formation around the current target if available, otherwise anchor to the party leader:

```text
/mopif target
    /mopformationgoto "Circle" 2 target precise
/mopelse
    /mopformationgoto "Circle" 2 anchor="Leader Character@World" precise
/mopendif
```

### 3.2 Role-Based Job Branching

Execute different actions depending on the character's active job:

```text
/mopif job == "WHM" || job == "AST" || job == "SGE" || job == "SCH"
    /mopmoverelativeto -5 0 5 "PartyLeader@World"
    /say Healer in position
/mopelseif job == "PLD" || job == "WAR" || job == "DRK" || job == "GNB"
    /mopmoverelativeto 0 0 -5 "PartyLeader@World"
    /say Tank in position
/mopelse
    /mopmoverelativeto 5 0 5 "PartyLeader@World"
    /say DPS in position
/mopendif
```

### 3.3 Dynamic Boss / NPC Interaction

Check if a specific NPC exists in range before attempting interaction:

```text
/mopif visible "Striking Dummy"
    /target "Striking Dummy"
    /mopwait 0.5
    /mopaction "Auto-attack"
/mopelse
    /say No target dummy nearby!
/mopendif
```

### 3.4 Nested Combat State Checks

```text
/mopif inparty && isleader
    /mopif incombat
        /say Party leader calling defensive formation!
        /mopformationgoto "Wedge" 1 precise
    /mopelse
        /say Regrouping out of combat
        /mopformationgoto "Line" 1 natural
    /mopendif
/mopendif
```

---

## 4. Relevant Files

* `MasterOfPuppets/MopMacro/MacroConditionEvaluator.cs`: Expression tokenization, parsing, object table inspection, and evaluation logic.
* `MasterOfPuppets/MopMacro/MacroHandler.cs`: Control flow stack (`ConditionalFrame`), block skipping, and inline conditional dispatch.
* `MasterOfPuppets/MopMacro/CommandHelp/MopCommandsHelper.MacroAction.cs`: In-game UI command help and autocomplete suggestions.
