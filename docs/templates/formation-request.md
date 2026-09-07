# Formation Request Template

Use this template when asking a contributor or coding agent to design a Master
Of Puppets formation. The result should be portable and importable; generating
it must not modify a live `MasterOfPuppets.json` configuration.

## Request

Create a formation with the following properties:

- **Name:** `<formation name>`
- **Performer count:** `<number>`
- **Shape or concept:** `<visual description>`
- **Anchor:** `<center, selected actor, named performer, or other reference>`
- **Facing rule:** `<fixed direction, face anchor, face outward, tangent, etc.>`
- **Spacing:** `<distance in yalms or a clear visual rule>`
- **Ordering:** `<how performers map to slots>`
- **Movement style:** `<walk/run, precision, transitions, timing expectations>`
- **Constraints:** `<venue size, exclusions, symmetry, late join behavior, etc.>`

If an existing formation is a useful reference, provide its export/share code or
its saved formation data. Treat names, IDs, and offsets from that reference as
input data rather than assumptions about every user's roster.

## Required output

Provide:

1. A concise explanation of the geometry and slot-ordering rule.
2. An importable Master Of Puppets formation share code or export artifact.
3. Any assumptions about coordinates, facing, roster identity, or runtime APIs.
4. Validation notes covering performer count, duplicate slots, spacing, and the
   expected visual result.

Do not edit the live plugin configuration unless the user explicitly requests
that separate action.

## Coordinate conventions

- `+X` is east and `-X` is west.
- `+Z` is south and `-Z` is north.
- `+Y` is upward.
- Facing is expressed in radians: south `0`, north `π`, east `-π/2`, west `π/2`.
- State clearly whether offsets are world-aligned or rotated relative to an
  anchor's facing.

See [Movement Coordinate System](../movement.md) for diagrams and details.
