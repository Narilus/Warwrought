---
name: province-world-generation
description: Use for procedural campaign maps, province topology, adjacency, biomes, landmarks, start placement, map polygons, army movement, or province-to-battlefield derivation. Keeps the campaign graph authoritative and the visible province geometry representational.
compatibility: Agent Skills; project-specific campaign generation
metadata:
  project-area: campaign
  project-phase: M4-M13
---

# Province World Generation

## Foundational model

The strategic world is a procedurally generated **2D province graph**.

The graph is authoritative. The polygon/map artwork represents it.

```text
Province A <-> Province B <-> Province C
       \          |
        \         |
         -> Province D
```

Never make campaign rules depend on fragile screen-space polygon adjacency when the authoritative neighbour list already exists.

## Province authority

A province should have a stable ID and explicit data such as, when implemented by the relevant milestone:

- neighbour IDs;
- owner;
- terrain/biome;
- economy/manpower/resource fields approved by design gates;
- structures;
- landmarks;
- armies present;
- strategic modifiers;
- battle-generation identity/seed/data.

Do not invent unresolved economy/resource fields simply because the schema has room for them.

## Generation pipeline

Prefer a staged deterministic generator whose stages can be tested and diagnosed independently:

1. create topology/province centres or equivalent graph basis;
2. derive/validate adjacency;
3. ensure required connectivity;
4. assign terrain/biome/environment identity;
5. place starts with the current fairness constraints;
6. place landmarks/resources only after their design gate is defined;
7. derive province battle identity;
8. build visual polygon/region data;
9. run validation/repair or bounded regeneration;
10. emit a generation report useful for tests/debugging.

The exact topology algorithm (Voronoi, Delaunay-derived, another graph generator) is not locked. Choose the smallest algorithm that meets the current milestone.

## Deterministic random streams

Avoid one giant sequential RNG stream when minor content additions would unnecessarily reshuffle every downstream map property.

A good pattern is to derive stable sub-seeds from the campaign seed and stage/identity, for example conceptually:

```text
campaign seed
  ├─ topology stream
  ├─ terrain stream
  ├─ start-placement stream
  ├─ landmark stream
  └─ per-province battlefield base seed
```

Do not introduce an elaborate random framework solely for this. Use the project's deterministic seed/RNG utilities and stable IDs.

## Validation principles

Generation should detect invalid worlds before gameplay.

Validate the subset relevant to the current milestone, such as:

- every playable province is reachable as required;
- no accidental isolated provinces unless intentionally supported;
- neighbour relations are symmetric where the game treats them as undirected;
- IDs are unique/stable;
- starting positions satisfy approved spacing/reachability rules;
- players/AI have at least a viable initial expansion route where required;
- landmarks/resources do not reference undefined content;
- visual region count/IDs map one-to-one with authoritative provinces;
- deterministic regeneration reproduces the same authoritative world.

Do not invent numeric fairness thresholds before they are approved by the relevant design gate.

## Visual map boundary

The 2D map may use Voronoi-like polygons, stylized borders, biome fills, icons, roads, labels, and ownership overlays.

But authoritative actions should use province IDs and graph state:

```text
MoveArmy(armyId, fromProvinceId, toProvinceId)
```

not:

```text
MoveArmyToScreenCoordinate(...)
```

Map hit-testing may map a click to a province ID; after that, the domain uses IDs/adjacency.

## Province identity and battlefields

Battlefield generation should derive from province identity rather than producing an unrelated arena.

A province may contribute:

- base battlefield seed;
- elevation tendency;
- biome/forest density;
- water tendency;
- landmark/fortification data;
- terrain movement/combat modifiers;
- season/weather inputs where later supported.

Preferred relationship:

```text
Province data
    ↓
BattlefieldDefinition
    ├─ deterministic height field
    ├─ terrain regions/modifiers
    ├─ vegetation scatter seed/data
    ├─ landmark placement
    └─ deployment zones
```

Repeated battles in the same province should be capable of sharing the same recognizable underlying landform if the final design retains the preferred stable-base-seed policy.

Do not hard-lock weather/season persistence until the design resolves it.

## Army movement

Strategic army movement is graph traversal between legal adjacent provinces or whatever explicit strategic movement rule later supersedes it.

Do not add continuous overworld NavMesh/pathfinding merely because the battle scene is 3D.

## Debuggability

Generation tools/reports should make it easy to inspect:

- seed;
- province count;
- adjacency;
- connected components;
- starts;
- terrain distribution;
- landmarks once supported;
- province IDs and battlefield seeds;
- any repair/regeneration reason.

When a generated campaign fails, preserve the seed and authoritative generated data so the failure is reproducible.

## Common mistakes to reject

- deriving mechanical adjacency from rendered polygon edges every turn;
- letting map labels/display names become province IDs;
- random generation using timestamps or global engine RNG;
- assuming simultaneous turn resolution before the design gate resolves it;
- adding economy/resource mechanics before DG-B defines them;
- creating 3D continuous campaign movement because battles use 3D terrain;
- regenerating a different battlefield every time from unrelated random state.
