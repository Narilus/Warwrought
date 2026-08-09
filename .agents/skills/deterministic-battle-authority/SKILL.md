---
name: deterministic-battle-authority
description: Use for battle simulation, auto-resolve, transcript, replay, digest, fixed-tick movement, or battle-commit work. Preserves the single authoritative deterministic simulator and prevents watched combat, presentation, forecasting, or engine state from becoming a second source of truth.
compatibility: Agent Skills; project-specific battle architecture
metadata:
  project-area: battle
  project-phase: M1-M13
---

# Deterministic Battle Authority

## Foundational rule

There is exactly one authoritative battle simulation.

```text
BattleDefinition
      |
      v
Authoritative Simulator
      |
      +--------------------+
      |                    |
      v                    v
BattleResult        BattleTranscript
      |                    |
      v                    v
Campaign State       Battle Playback
```

`Skip to Result` and `Watch Battle` are two presentations of the **same committed battle**, not two resolution systems.

## When to load this skill

Use it when implementing or reviewing:

- `BattleDefinition` or battle commitment;
- RNG/determinism;
- formation/unit simulation;
- damage, morale, routing, targeting, abilities;
- auto-resolve;
- battle transcript/keyframes;
- playback/replay;
- battle digests/golden fixtures;
- battle-result application;
- AI-vs-AI battle execution.

Do not use this skill to justify resolving an AI strategic forecast with the exact committed future battle seed. Forecasting is intentionally separate and uncertain.

## Commit boundary

A battle becomes immutable when committed.

The committed definition must contain all authoritative inputs needed for resolution, including as applicable:

- simulation version;
- deterministic battle seed;
- battlefield identity/data;
- sides/armies;
- commanders;
- squads/formations;
- unit-definition snapshot or stable versioned references sufficient for exact resolution;
- deployment;
- doctrine/orders;
- ability policies;
- terrain/province/environment modifiers.

After commitment, UI state, Sprite3D transforms, animation state, resource reloads, camera state, frame timing, or later edits to mutable campaign/content objects must not change the battle outcome.

## Deterministic simulation rules

### RNG

Use one small, explicit, versioned deterministic RNG abstraction.

Do not use authoritative randomness from:

- `System.Random`;
- Godot/global RNG state;
- timestamps;
- frame timing;
- object hash codes;
- unordered collection enumeration;
- node/scene incidental order.

Pass RNG/state through explicit simulation ownership. Changing the algorithm changes simulation compatibility and requires an intentional simulation-version decision.

### Numeric representation

Prefer deterministic integers for branch-critical authoritative state:

- X/Z simulation position in scaled integer units;
- health;
- morale;
- pressure;
- cooldown/arrival ticks;
- ranges/distances in the same scaled position convention;
- fixed ratios through explicit scaled arithmetic where needed.

Godot `Vector2`/`Vector3` floats are presentation values derived from simulation state, not the authoritative coordinate store.

Do not build a generic fixed-point framework unless measured requirements demand one.

### Stable ordering

Authoritative outcome loops use stable ordering.

- lists/arrays for ordered iteration;
- maps/dictionaries for lookup only unless explicitly sorted;
- stable deterministic IDs;
- deterministic tie-breaking.

If two actions score equally, tie resolution must not depend on hash/table enumeration or object allocation order.

### Fixed tick

Simulation advances in logical ticks independent of render frame rate.

Never use `_Process(delta)` or visual interpolation time to decide authoritative:

- movement distance;
- attack completion;
- damage timing;
- projectile arrival;
- morale;
- ability outcomes;
- victory.

## Formation-first model

Ordinary troops should derive most decisions from their squad/formation.

Formation state may own:

- anchor X/Z;
- facing;
- layout/width/depth;
- destination;
- stance;
- target formation;
- cohesion;
- morale;
- pressure;
- retreat/rout state.

Individual units may own:

- stable ID;
- formation and slot;
- health/statuses;
- action cooldown/next-action tick;
- target only when needed;
- alive/routed state.

Avoid per-soldier high-complexity tactical brains. Use spatial indexing/cached targets when local individual queries become necessary.

## Terrain relationship

The simulator operates fundamentally on horizontal X/Z data.

```text
simulation: (x, z)
presentation: (x, HeightAt(x,z), z)
```

Terrain may provide deterministic movement/elevation/biome modifiers through battlefield data, but presentation physics is not authoritative.

Forbidden as outcome sources after commit include:

- Sprite3D overlap;
- physics collision deciding melee damage;
- projectile collider impact causing damage;
- animation completion triggering attacks;
- render raycasts deciding authoritative position;
- visual effect success/failure deciding ability success.

## Transcript contract

The transcript must be sufficient to visualize the already-resolved battle **without rerunning authoritative decision logic in the viewer**.

Use three conceptual layers:

1. header/version/identity;
2. semantic authoritative events;
3. periodic movement/presentation keyframes.

Do not record render frames.

Useful semantic event families include:

```text
TARGET_ACQUIRED
SQUAD_ORDER_CHANGED
CONTACT_STARTED
ATTACK_RESOLVED
DAMAGE_DEALT
ABILITY_CAST
PROJECTILE_EVENT
STATUS_APPLIED
MORALE_CHANGED
FORMATION_BROKEN
UNIT_KILLED
ROUT_STARTED
RETREAT_COMPLETED
COMMANDER_KILLED
BATTLE_ENDED
```

Events should carry stable IDs, authoritative tick, and reason/source data needed for later explainability. Do not force the battle viewer to reconstruct *why* something happened from visual state.

Early keyframes should prioritize correctness and inspectability over compression. Optimize transcript density only after representative large battles are measured.

## Playback contract

Playback owns presentation time, not battle outcomes.

Playback may:

- pause;
- resume;
- change speed;
- interpolate;
- reset/seek using transcript/keyframe data;
- spawn visual projectiles/effects;
- bump sprites;
- flash damage;
- rotate/replace dead sprites.

Playback must never decide:

- hit/miss;
- damage;
- target selection;
- death;
- morale;
- routing;
- winner.

## Auto-resolve/watch equivalence

For a given committed battle:

- auto-resolve runs/consumes the authoritative simulation result and skips presentation;
- watch mode consumes the matching transcript and presents the same result;
- campaign consequences are applied from the authoritative result, not inferred from what the renderer displayed.

A key acceptance property is:

```text
commit identity A
→ skip result digest X
→ watch result digest X
```

If they diverge, treat it as an architecture/correctness bug, not a balancing difference.

## Canonical digest

Do not hash arbitrary serializer output, JSON text formatting, node state, memory representations, or `.tscn` files.

Build a canonical digest writer that feeds explicitly ordered primitive authoritative values, such as:

- simulation version;
- battle seed;
- terminal tick;
- result/winner;
- ordered survivor/casualty records;
- ordered semantic event fields selected by the digest contract.

SHA-256 is a clear baseline unless the implementation plan explicitly changes it after measurement.

The digest is verification evidence, not gameplay state.

## Headless performance

The simulator must run without rendering and should resolve representative battles substantially faster than watched real time.

Do not optimize prematurely, but preserve this property architecturally:

- no dependency on scene tree traversal per tick;
- no requirement for Sprite3D/Node instances to resolve combat;
- no render-frame waits;
- no physics step requirement;
- no visual projectile simulation.

This does **not** mean Godot is an optional wrapper. The playable product and milestone completion remain vertically integrated through production Godot scenes.

## Failure smells

Stop and correct the architecture if you find:

- `AutoResolvePowerScore()` determining real campaign battle results while watched battle uses another resolver;
- viewer code applying damage because an effect arrived;
- replay re-running target AI instead of consuming transcript truth;
- RNG calls hidden inside presentation;
- unordered dictionary iteration affecting outcomes;
- digest expectations changed merely to match a broken new result;
- mutable campaign/content objects referenced directly by an already-committed battle;
- a standalone non-Godot game executable becoming the actual delivery target.

## Minimum verification for authoritative battle changes

Depending on task scope, prove the relevant subset of:

- same seed + same definition + same simulation version => same digest;
- different render/playback speed => same result;
- skip and watch => same authoritative result/digest;
- headless simulation requires no presentation nodes;
- production Godot BattleLab consumes the real transcript;
- runtime counters agree with transcript events where acceptance defines them;
- runtime logs are clean.
