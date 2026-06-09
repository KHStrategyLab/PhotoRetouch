# PhotoRetouch Beard Shadow Policy

This file is UTF-8.

## Status

Design note only. Do not change the current order sequence for this note.

## Definition

`BeardShadowAmount`, if added to `BlemishToolset` or a related filter later, means:

- Shaving mark reduction
- Beard shadow reduction
- Blue or gray cast reduction around beard areas

It does not mean:

- Removing real beard hair
- Removing mustache hair
- Removing sideburn hair
- Smearing beard texture into skin

## Product Rule

Real beard and mustache hair are protected details.

Shaving marks, blue cast, and dark beard shadow are tone-softening targets.

This belongs conceptually inside the SkinRetouch masked correction layer group until a separate `BeardToolset` is introduced.

It should behave like a skin-area tone correction mask, not like hair removal.

## Future BeardToolset Candidate

`BeardToolset` may later be separated from `BlemishToolset`.

Candidate fields:

- `EnableBeardShadowReduce`
- `GlobalBeardShadowAmount`
- `MustacheShadowAmount`
- `ChinBeardShadowAmount`
- `JawBeardShadowAmount`
- `SideburnShadowAmount`
- `BlueToneReduceAmount`
- `BeardHairProtectAmount`

## Area Rules

Mustache shadow:

- Treat weakly.
- Protect lip edge, nostrils, and under-nose shadow.
- Do not flatten the philtrum or nose base.

Chin beard shadow:

- Reduce blue/dark cast conservatively.
- Preserve chin shape, jaw structure, and natural skin texture.

Jaw beard shadow:

- Reduce tone discoloration without reshaping the jawline.
- Respect existing jaw and neck boundary tools.

Sideburn shadow:

- Treat carefully because it is close to hair and face outline.
- Avoid conflicts with `HairMask`.

## Mask Rules

- Exclude `HardProtectMask`.
- Protect `BeardMask` and `MustacheMask` as hair/detail masks.
- Use beard-shadow reduction only on skin-tone discoloration around beard areas.
- Treat mask failure as a protection problem before increasing filter strength.

## Implementation Timing

This note should be considered during:

- `BlemishToolset` refinement
- `ToneEvenFilter` implementation
- Filter quality tuning
- Future `BeardToolset` separation

Do not interrupt the current active ORDER sequence for this note.
