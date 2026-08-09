# Warwrought — Implementation Plan

- **Project:** Warwrought
- **Document status:** Implementation foundation and milestone plan
- **Canonical location:** `/plans/implementation_plan.md`
- **Source design:** `/DESIGN.md`
- **Engine:** Godot 4.7.1 .NET/C#
- **Renderer:** Forward+
- **Primary target:** Windows desktop
- **Plan date:** 9 August 2026

---

# 1. Purpose

This document converts the foundation GDD into an implementation sequence that can be executed by an engineering agent without access to the design discussion that produced it.

It is not a second GDD. Where a design choice is deliberately unresolved in the GDD, this plan must not silently invent a permanent answer. Instead it identifies the design gate that must be resolved before dependent work begins.

The plan is designed around several project-specific realities:

- The game will be developed heavily through coding agents.
- Runtime integration is therefore a first-class engineering requirement, not a final polish phase.
- The authoritative battle simulation is deterministic and resolves before playback.
- Auto-resolve and watched combat are the same battle; only presentation differs.
- Battles should support hundreds of units while avoiding conventional RTS complexity.
- Godot scenes/resources should remain inspectable and source-control friendly.
- Test and verification infrastructure must remain small, supported, and difficult for an implementer to bypass or weaken.

The implementation sequence deliberately starts with a complete battle vertical slice before campaign breadth. The project must prove that deterministic simulation, transcript generation, Godot playback, clean logging, and exported-runtime verification all work together before a large strategic game is built on top.

---

# 2. Non-Negotiable Engineering Constraints

The following constraints are inherited from the GDD and apply to every milestone.

## 2.1 One authoritative battle simulator

There must never be a separate simplified auto-resolve formula.

```text
BattleDefinition
      |
      v
Authoritative deterministic simulator
      |
      +----------------------+----------------------+
      |                                             |
      v                                             v
BattleResult                                 BattleTranscript
      |                                             |
      v                                             v
Campaign consequence                        Godot battle playback
```

`Watch Battle` and `Skip to Result` consume the same committed battle result.

## 2.2 The battle viewer is not authoritative

Presentation may:

- interpolate;
- animate sprite bumps;
- flash damage;
- spawn visual projectiles;
- rotate dead sprites;
- place remains;
- change playback speed;
- pause;
- later rewind/scrub.

Presentation may not decide:

- hit success;
- target selection;
- damage;
- death;
- routing;
- morale;
- ability outcome;
- battle winner.

## 2.3 Runtime completion is mandatory

A gameplay task is not complete because `dotnet test` passes.

A milestone is not complete until its production Godot scene/runtime path is exercised successfully and its runtime evidence is captured.

## 2.4 Production scenes are acceptance surfaces

Acceptance must exercise production code and production scenes. A test-only substitute scene cannot establish that a production feature works.

Dedicated laboratory/debug scenes are allowed and encouraged when they are themselves production-maintained tools that use the same production components and data paths as the game.

## 2.5 Clean runtime logs are part of passing

Unexpected Godot/runtime errors, exceptions, missing-resource failures, invalid-node errors, or assertion failures invalidate acceptance even if another test reports success.

## 2.6 Acceptance criteria are protected

The implementing agent must not manufacture a pass by:

- deleting tests;
- skipping/ignoring failures;
- changing expected hashes to match unintended output;
- reducing expected counts;
- converting runtime tests into mocks;
- swallowing exceptions;
- replacing production integrations with facades;
- rewriting acceptance criteria after implementation without an explicit design/planning decision.

## 2.7 Prefer transparent project state

Use text `.tscn` scenes and source-controlled data definitions where practical. Prefer stable content IDs over path/name inference.

## 2.8 Avoid speculative architecture

Do not introduce ECS, GDExtension, MultiMesh batching, complex plugin systems, abstract backend interfaces, custom scripting languages, or general-purpose event buses merely because they might eventually be useful.

Add complexity only against a demonstrated requirement or profiler result.

---

# 3. Architectural Baseline

## 3.1 Repository shape

The exact folder names may vary slightly, but the repository should start with one Godot project and one solution-level test project rather than a standalone game backend.

Recommended shape:

```text
/
├── project.godot
├── Warwrought.csproj
├── Warwrought.sln
├── export_presets.cfg
├── AGENTS.md
├── DESIGN.md
├── docs/
│   ├── architecture/
│   ├── decisions/
│   └── milestones/
├── plans/
│   └── implementation_plan.md
├── src/
│   ├── Core/
│   ├── Content/
│   ├── Battle/
│   │   ├── Model/
│   │   ├── Simulation/
│   │   ├── Transcript/
│   │   └── Playback/
│   ├── Campaign/
│   ├── Factions/
│   ├── AI/
│   ├── Presentation/
│   │   ├── Battle/
│   │   ├── Campaign/
│   │   └── Shared/
│   ├── UI/
│   ├── Debug/
│   └── Bootstrap/
├── scenes/
│   ├── Main/
│   ├── Battle/
│   ├── Campaign/
│   ├── UI/
│   └── Labs/
├── content/
│   ├── units/
│   ├── abilities/
│   ├── sprites/
│   ├── terrain/
│   ├── fixtures/
│   └── schemas/
├── assets/
│   ├── sprites/
│   ├── terrain/
│   ├── ui/
│   ├── vfx/
│   └── audio/
├── tests/
│   └── Warwrought.Tests/
├── scripts/
└── artifacts/
    └── .gitkeep
```

The production C# code remains part of the Godot project. A sibling `.NET` test project may reference `Warwrought.csproj` for fast deterministic logic tests; it is not a second application or alternate game runtime.

## 3.2 Responsibility boundaries

### `Core`

Owns small cross-domain primitives only:

- stable IDs;
- deterministic RNG;
- deterministic numeric helpers;
- hashing helpers;
- result/error types where genuinely useful;
- version identifiers;
- common serialization helpers.

Do not turn `Core` into a dumping ground or framework.

### `Content`

Owns:

- loading source-controlled definitions;
- stable-ID lookup;
- schema validation;
- cross-reference validation;
- presentation metadata lookup;
- fixture content.

### `Battle.Model`

Owns immutable/authoritative battle input and state models:

- `BattleDefinition`;
- armies;
- commanders;
- squads;
- units;
- doctrine/order records;
- terrain sample data;
- simulation version.

### `Battle.Simulation`

Owns:

- fixed-tick simulation;
- movement;
- formation contact;
- target selection;
- attack resolution;
- ability resolution;
- morale/routing;
- terminal state;
- authoritative events.

It must not depend on Node lifecycle, scene tree timing, input, camera, VFX, audio, or render FPS.

### `Battle.Transcript`

Owns:

- transcript header/version;
- semantic event storage;
- presentation keyframes/snapshots;
- battle result summary;
- deterministic digest/hash;
- transcript serialization for debug/replay where required.

### `Battle.Playback`

Owns the translation between transcript time and presentation state. It may calculate interpolation but cannot alter authoritative outcomes.

### `Campaign`

Owns:

- province graph;
- campaign state;
- province ownership;
- army locations;
- economy/recruitment/structure rules once designed;
- turn processing once designed;
- battle commitment/application of battle results;
- victory evaluation once designed.

### `Factions`

Owns:

- faction/lord mechanical definitions;
- trait/package composition;
- generated faction records;
- visual selection IDs;
- faction validation.

### `AI`

Owns strategic/faction-generation decisions. It may call approved forecast/evaluation functions but must not bypass fog/information rules to inspect committed battle outcomes before commitment.

### `Presentation` and `UI`

Own Godot-facing nodes, rendering, controls, camera, visual feedback, and input. They consume authoritative data and emit player commands/configuration choices.

---

# 4. Determinism Strategy

Determinism is foundational to auto-resolve equivalence, replay, debugging, and testability. It must be established before combat breadth.

## 4.1 RNG

Do not use `System.Random`, engine-global RNG, timestamps, frame timing, object hash codes, iteration over unordered collections, or incidental node order for authoritative randomness.

Implement one small versioned deterministic RNG abstraction with:

- explicit seed;
- stable documented algorithm;
- explicit state;
- methods for integer ranges and deterministic probability checks;
- no hidden global instance.

All random simulation decisions receive RNG explicitly or through a battle-owned simulation context.

Changing RNG algorithm requires a simulation-version change.

## 4.2 Authoritative numeric representation

Avoid branch-critical floating-point behaviour where a small platform/runtime variation could change an outcome.

Preferred baseline:

- authoritative horizontal coordinates use scaled integers;
- distances/ranges use the same units;
- health, morale, cooldown ticks, pressure, and resource quantities use integers where possible;
- fixed ratios use explicit scaled integer arithmetic or carefully centralized conversion helpers;
- Godot `Vector2`/`Vector3` floats are presentation values derived from authoritative state.

Do **not** build a general-purpose fixed-point mathematics framework unless required. A small `SimPosition`/scaled-integer convention is sufficient for the early game.

Example convention:

```text
1 simulation metre = 1000 position units
```

The exact scale should be selected once in M0/M1 and documented.

## 4.3 Stable iteration

Authoritative loops must use stable ordering. If entities are stored in maps/dictionaries for lookup, do not rely on dictionary enumeration order for outcome decisions.

Typical pattern:

- authoritative ordered arrays/lists for iteration;
- dictionaries only for ID -> index lookup;
- stable IDs assigned deterministically.

## 4.4 Deterministic digest

Do not hash arbitrary object serialization, `.tscn` files, JSON formatting, or memory representations.

Create a canonical battle digest from explicitly ordered authoritative values:

```text
simulation version
battle seed
terminal tick
winner/result
ordered surviving unit records
ordered casualty records
ordered semantic event fields
```

Feed primitive values into one dedicated binary digest writer in a fixed order. SHA-256 is acceptable and easy to inspect; a faster stable hash can be introduced later only if profiling proves necessary.

The digest is evidence, not gameplay state.

---

# 5. Transcript and Playback Baseline

The transcript must be sufficient to present the battle without rerunning authoritative decision logic in the viewer.

## 5.1 Transcript layers

Use three conceptual layers:

1. **Header** — version, seed, battlefield identity, sides, duration.
2. **Semantic events** — attacks, damage, deaths, morale, target/order changes, abilities, routs, battle end.
3. **Motion/presentation keyframes** — enough state to reconstruct positions/facing between events.

Do not record render frames.

## 5.2 Early keyframe policy

Prioritize correctness over compression in early milestones.

For formation-first units, store squad anchor/facing/formation state at a predictable simulation interval. Individual unit positions should normally derive from:

- squad anchor;
- formation layout;
- deterministic slot assignment;
- alive/routed state.

Independent heroes/monsters may later receive individual motion keyframes if required.

Optimize transcript density only after representative 500v500 recordings are measured.

## 5.3 Playback clock

The viewer owns a playback clock distinct from simulation tick execution.

Required early controls:

- pause;
- resume;
- 1x;
- accelerated playback;
- seek-to-start/reset for debug.

Future timeline scrubbing should be enabled by architecture but is not required in the first slice.

---

# 6. Verification Contract

M0 must establish the exact commands. After they are proven, they are documented in `AGENTS.md` and treated as stable project infrastructure.

Godot supports direct scene execution, `--headless`, `--log-file`, user arguments after `--`, and command-line export. The implementation should use those supported capabilities rather than an invented editor-control layer.

Official references:

- https://docs.godotengine.org/en/4.7/tutorials/editor/command_line_tutorial.html
- https://docs.godotengine.org/en/4.7/tutorials/export/exporting_projects.html
- https://docs.godotengine.org/en/4.7/classes/class_sprite3d.html
- https://docs.godotengine.org/en/4.7/classes/class_surfacetool.html
- https://docs.godotengine.org/en/4.7/classes/class_multimesh.html

## 6.1 Approved verification categories

The project should expose only a few obvious verification paths:

### A. Compile

```text
dotnet build
```

### B. Fast deterministic logic tests

```text
dotnet test
```

This verifies algorithms and rules, not final runtime integration.

### C. Production scene acceptance

Launch a production-maintained Godot scene directly with a known fixture and acceptance arguments. It must:

- run the actual production bootstrap/components;
- perform the requested deterministic scenario;
- emit an acceptance report;
- exit with a documented status/result;
- produce an explicit log file.

The exact command syntax is frozen in M0 after being tested on the development machine.

### D. Windows export

Use the committed Windows export preset and Godot's supported command-line export path.

### E. Exported-player smoke

Launch the exported executable with an acceptance fixture and verify its report/log independently of the editor.

## 6.2 Thin scripts are allowed; alternate runners are not

A tiny PowerShell script may normalize paths, locate the configured Godot binary, create artifact directories, and invoke the approved command. It must remain a transparent wrapper around official commands.

Do not build a custom test orchestration framework that duplicates Godot/.NET functionality.

## 6.3 Runtime acceptance report

All laboratory/runtime acceptance paths should use one small shared reporting format, for example:

```json
{
  "schemaVersion": 1,
  "scenario": "battlelab.m1.melee",
  "scene": "BattleLab",
  "seed": 82741,
  "simulationVersion": 1,
  "result": "SideAWin",
  "terminalTick": 1182,
  "digest": "...",
  "runtime": {
    "unitsSpawned": 200,
    "damageEventsPresented": 151,
    "deathEventsPresented": 37,
    "remainsSpawned": 37,
    "resultPresented": true
  },
  "unexpectedErrors": 0,
  "passed": true
}
```

The production runtime creates this data from actual observed state. A wrapper may collect it but may not fabricate it.

---

# 7. Milestone Overview

The recommended implementation order is:

| Milestone | Name | Primary proof |
|---|---|---|
| M0 | Project Foundation & Verification Contract | Godot/.NET project builds, scene acceptance and Windows smoke are repeatable |
| M1 | Battle Laboratory — Deterministic Melee Vertical Slice | One pre-resolved 50–100v50–100 battle plays correctly in Godot |
| M2 | Procedural 2.5D Battlefield & Rendering Scale | Seeded low-poly terrain, billboards, foliage, camera; 500v500 visual stress path |
| M3 | Battle System Expansion & Doctrine | Multiple formations, ranged, abilities, morale, target priorities and doctrine |
| M4 | Campaign Graph & Procedural World Vertical Slice | Generated province graph/map, ownership, armies, movement and persistence |
| M5 | Content Registry, Units, Factions & Visual Tagging | Data-driven content and coherent fixture/generated visual identity |
| M6 | Faction/Lord Creation & Army Management UI | Player can construct a valid faction/lord/army and configure squads |
| M7 | Campaign Economy, Recruitment, Structures & Turn Resolution | Playable strategic growth loop after required design gates |
| M8 | Strategic AI & Procedural AI Factions | AI builds coherent factions, expands, recruits, moves and commits battles |
| M9 | Full Campaign-to-Battle Loop | Province conflict commits one battle; watch/skip apply identical consequences |
| M10 | Explainability, Replay & Post-Battle Analysis | Player can understand why outcomes occurred |
| M11 | Save/Load, Versioning & Recovery | Campaigns persist robustly; battle/replay compatibility policy implemented |
| M12 | Scale, Performance & Production Hardening | Representative large battles and campaigns meet measured budgets |
| M13 | Content/UX Expansion & Release Candidate | Foundation becomes a coherent playable product slice |

M1 is the first hard architectural gate. M9 is the first complete game-loop gate.

---

# 8. Design Gates

Several GDD areas remain intentionally unresolved. Implementation must not bury permanent design decisions inside code before these gates are closed.

## DG-A — Faction creation rules

Required before M6 production faction creator.

Must define:

- mechanical taxonomy;
- package/chassis model;
- trait costs or selection limits;
- strengths/liabilities rules;
- affinity structure;
- validation constraints;
- how unit roster access is derived.

Until then, use fixture factions and generic schema fields that do not require final balance values.

## DG-B — Campaign economy and recruitment

Required before M7.

Must define:

- strategic resources;
- province yields;
- unit recruitment costs/timing;
- manpower/recruitment-pool model if any;
- upkeep if any;
- structure costs/effects;
- construction slots/timing.

## DG-C — Turn resolution

Required before M7 turn processor is finalized.

Must explicitly choose:

- sequential;
- simultaneous;
- or hybrid phase resolution;

and define how conflicting moves are ordered/resolved.

## DG-D — Battle doctrine v1 surface

Required during M3 before public-facing doctrine UI is finalized.

Must define the v1 supported set of:

- army stances;
- squad target priorities;
- hold/advance/chase behaviour;
- ability policy conditions;
- formations.

M1 may use hard-coded orders.

## DG-E — Campaign victory and defeat

Required before M9 full-loop acceptance.

Must define at minimum:

- elimination condition;
- capital role;
- loss condition;
- whether conquest of all opponents is sufficient for v1.

## DG-F — Replay retention policy

Required before M11 persistence work.

Must decide whether saved campaigns retain:

- no historical transcripts;
- recent transcripts;
- explicitly saved replays only;
- all battles.

---

# 9. M0 — Project Foundation & Verification Contract

## Goal

Create the smallest reliable Godot/.NET repository in which an agent can build, run a real scene, generate clean runtime evidence, export Windows, and run the exported build without inventing new infrastructure.

No game breadth belongs here.

## M0.1 Pin toolchain and project settings

Tasks:

- Create the Godot 4.7.1 .NET/C# project using the Forward+ renderer.
- Target the .NET version expected by the installed Godot .NET editor.
- Commit `project.godot`, `.csproj`, `.sln`, `.gitignore`, and `export_presets.cfg`.
- Set the project name and assembly identifier to `Warwrought`.
- Use Forward+ as the desktop 3D renderer baseline. A renderer change requires an explicit design revision; do not substitute a compatibility renderer locally.
- Configure window defaults suitable for desktop development.
- Document exact Godot executable path resolution on Windows.
- Record installed Godot and .NET versions in `docs/environment.md`.

Acceptance:

- clean checkout opens in Godot;
- `dotnet build` succeeds from repository root;
- no generated cache directories are committed.

## M0.2 Establish source layout and naming conventions

Tasks:

- Create repository folders from Section 3.
- Add namespace conventions matching responsibilities.
- Add stable ID primitive types for foundational domains only as needed.
- Add nullable/reference-type and analyzer settings appropriate for the project without introducing highly restrictive analyzer packages.

Constraints:

- warnings should be useful, not a blocker-generating zero-warning absolutism;
- no architecture framework or DI container.

## M0.3 Create production `BootstrapLab` scene

Create a tiny production-maintained scene that:

- starts through a real C# Node;
- reads user arguments after `--`;
- can execute an acceptance mode;
- writes one JSON acceptance report;
- exits intentionally after completion in acceptance mode;
- otherwise displays a minimal visual scene.

This is infrastructure proof, not the battle implementation.

Acceptance report fields should include:

- scenario;
- Godot version;
- build/runtime identifier;
- current project version;
- scene name;
- unexpected-error count;
- pass/fail.

## M0.4 Logging contract

Implement a small application logging policy:

- domain logs through one thin project logger or direct Godot logging helpers;
- expected informational output separated from errors;
- acceptance runtime records application-caught unexpected failures;
- command line always uses explicit `--log-file` during acceptance.

Do not attempt to replace Godot's logger.

Create a verification step that fails when the acceptance log contains unexpected error/exception patterns or the report declares an error.

Acceptance:

- deliberate runtime error produces failed acceptance;
- fixed runtime produces clean pass;
- no false pass because process exit happened before report completion.

## M0.5 Fast .NET test project

Create `tests/Warwrought.Tests` using one mainstream .NET test framework already well supported by `dotnet test`.

Initial tests:

- trivial source-reference test;
- stable ID equality/order test;
- deterministic RNG known-sequence test once RNG exists.

The test project is for pure logic. It is not the milestone acceptance path.

## M0.6 Windows export and player smoke

Tasks:

- Commit a Windows Desktop export preset.
- Document installation requirement for Godot export templates.
- Export via official command line.
- Launch exported player with acceptance arguments.
- Write report/log to an explicit artifact path.

Acceptance:

- editor/scene acceptance passes;
- release/debug exported player launches;
- exported player produces the expected report;
- exported player log has no unexpected errors.

## M0.7 Freeze approved commands

Create `AGENTS.md` containing exact tested commands for:

- build;
- test;
- run a specific scene in acceptance mode;
- export Windows;
- run exported smoke;
- artifact/log locations.

Include a rule:

> Do not replace or redesign these commands because a feature test is inconvenient. Change the verification contract only through an explicit infrastructure task with evidence that the existing command is genuinely insufficient.

### M0 gate

M0 passes only when a fresh terminal can run the documented sequence twice consecutively with identical success and no manual editor action.

Required evidence:

- command transcript;
- `dotnet test` result;
- BootstrapLab acceptance JSON;
- BootstrapLab Godot log;
- Windows export log;
- exported-player acceptance JSON/log.

---

# 10. M1 — Battle Laboratory: Deterministic Melee Vertical Slice

## Goal

Prove the complete defining architecture of the game with a real watched battle:

- a committed deterministic `BattleDefinition`;
- full headless resolution first;
- `BattleResult` plus transcript;
- Godot playback of that transcript;
- 50–100 units per side;
- formation movement, contact, damage, death, morale, rout, terminal result;
- exact equivalence between skip and watch outcomes.

This milestone takes priority over campaign/faction breadth.

## M1.1 Battle primitives and deterministic RNG

Implement:

- `BattleId` / stable entity IDs;
- `SimulationVersion`;
- deterministic RNG with known-sequence tests;
- scaled integer simulation coordinates;
- tick type/count;
- canonical deterministic hashing writer.

Tests:

- same seed => same RNG sequence;
- different seeds => expected divergence;
- integer coordinate arithmetic boundaries;
- digest input ordering is stable.

## M1.2 Fixture content and immutable `BattleDefinition`

Create minimal fixture definitions:

- one basic infantry unit type per side;
- one commander per side if required by model;
- one simple line formation;
- one `advance` order;
- one melee attack;
- basic morale values.

`BattleDefinition` should be immutable after commitment and contain all authoritative inputs.

Validation must reject:

- missing unit definition IDs;
- duplicate unit IDs;
- units assigned to missing squads;
- invalid deployment slots;
- unsupported simulation version.

## M1.3 Formation state model

Implement one formation model sufficient for rectangular ranks/files:

- anchor position;
- facing;
- width/rank configuration;
- deterministic slot positions;
- member ordering;
- destination;
- target formation;
- cohesion/morale state;
- routed flag.

Do not implement generalized arbitrary formations yet.

Unit visuals derive from slot + anchor rather than independent pathfinding.

## M1.4 Fixed-tick simulator loop

Implement a simulator that:

1. initializes battle state from `BattleDefinition`;
2. advances a fixed logical tick;
3. updates formation motion;
4. detects contact;
5. schedules/resolves attacks;
6. applies casualties;
7. updates morale;
8. starts rout when threshold/rule is reached;
9. completes when one side is defeated/routed beyond recovery or a safety tick cap is reached;
10. emits result and transcript.

Exact tick rate remains configurable during M1; choose a starting value and profile rather than treating it as final design law.

Hard safety requirement:

- simulator has a maximum tick bound and returns an explicit non-terminal failure result if a bug prevents completion; it must not hang indefinitely.

## M1.5 Contact and melee baseline

Use formation-level contact; do not give every soldier a broad target-search loop.

Baseline:

- two formation footprints approach;
- contact relationship is established;
- front-rank member pairings are deterministic;
- valid attackers resolve cooldown-based melee;
- damage is integer and intentionally simple for the fixture;
- killed units become inactive and no longer occupy active combat roles;
- remaining formation slots may reform deterministically.

The first damage formula can be minimal and fixture-oriented. Do not turn M1 into final balance design.

## M1.6 Morale and rout baseline

Implement a deliberately small first morale model:

- morale state exists per formation;
- casualties reduce morale according to a documented fixture rule;
- reaching threshold starts rout;
- routed formation moves away from engagement/deployment centre;
- battle terminal logic recognizes routed/defeated sides.

Mark the formula as provisional until the later combat design pass. Architecture must support replacement without changing presentation contracts.

## M1.7 Transcript v1

Define versioned transcript records.

Required semantic event types:

- battle started;
- formation movement state/change where needed;
- contact started/ended;
- attack resolved;
- damage dealt;
- unit killed;
- morale changed;
- rout started;
- battle ended.

Required motion data:

- periodic formation anchor/facing keyframes;
- sufficient member alive/slot state to reconstruct formation visuals.

The transcript must be serializable to a debug artifact for inspection, but the first runtime can consume it directly in memory.

## M1.8 Battle result and digest

`BattleResult` must include campaign-relevant output:

- winner/result type;
- terminal tick;
- ordered survivors;
- ordered casualties;
- routed/retreated units as distinct from dead units if applicable;
- commander status;
- deterministic digest.

Tests:

- same fixture+seed repeated N times gives identical result/digest;
- result is independent of any playback invocation;
- changing seed or an authoritative stat changes digest as expected.

## M1.9 Production `BattleLab.tscn`

Create a real maintained scene containing production components:

```text
BattleLab
├── BattlefieldRoot
│   ├── TerrainRoot
│   ├── UnitRoot
│   ├── RemainsRoot
│   ├── EffectsRoot
│   └── CameraRig
├── BattlePlaybackController
└── BattleHUD
```

Exact node names may vary, but responsibilities should remain visible in `.tscn`.

The scene must:

- load fixture `BattleDefinition`;
- resolve it headlessly before playback;
- retain result/transcript;
- create troop views from transcript start state;
- playback transcript;
- show final result.

## M1.10 Unit visual v1

Use `Sprite3D` baseline.

Ordinary unit view requires:

- alive sprite;
- billboard mode;
- faction ownership identifier (base/ring/tint acceptable);
- position/facing interpolation;
- transient red damage flash;
- short melee bump derived from attack event;
- death rotation/fall;
- remains replacement.

No walking or weapon animation is required.

Use placeholder art if necessary, but it must be visually distinct enough to validate sides, deaths, and remains.

## M1.11 Playback controller

Implement:

- play from beginning;
- pause/resume;
- 1x;
- at least one accelerated speed;
- deterministic mapping from playback time to transcript tick/keyframes;
- no simulation decision calls from playback.

Acceptance test should be able to inspect that the viewer processed expected event counts.

## M1.12 Skip vs watch equivalence

Create one battle fixture used by both paths:

- `Skip to Result` consumes `BattleResult` without opening playback;
- `Watch` opens `BattleLab` and then exposes the same `BattleResult` after playback.

Assert identical:

- winner;
- terminal tick;
- survivors/casualties;
- digest.

## M1.13 Runtime acceptance scenario

Create acceptance scenario `battlelab.m1.melee`.

Minimum report fields:

- battle seed;
- simulation version;
- side unit counts;
- transcript event count;
- movement keyframe count;
- units spawned;
- damage events presented;
- death events presented;
- remains spawned;
- routed formations shown;
- final result shown;
- authoritative digest;
- unexpected errors.

### M1 gate

M1 does not pass until:

- all deterministic tests pass;
- same seed repeats with same digest;
- headless simulation finishes far faster than watched playback for the fixture;
- production `BattleLab.tscn` directly launches and plays the resolved transcript;
- expected units visibly advance/contact/die/rout;
- report event counts match transcript expectations;
- skip and watch results are identical;
- log is clean;
- Windows exported build runs the same acceptance scenario successfully.

No campaign implementation should begin before this gate is trustworthy.

---
# 11. M2 — Procedural 2.5D Battlefield & Rendering Scale

## Goal

Replace the M1 flat/simple battle presentation with the intended visual foundation:

- deterministic low-poly terrain;
- 2D simulation positions projected onto 3D terrain;
- billboard units and foliage;
- camera suitable for reading formations;
- scale tests up to 500v500;
- no change to authoritative combat outcome from terrain rendering.

## M2.1 Battlefield definition model

Introduce a deterministic `BattlefieldDefinition` consumed by both simulation and presentation.

Initial data should include:

- battlefield seed;
- dimensions;
- height-field dimensions/resolution;
- height samples or parameters that deterministically generate them;
- terrain regions/types;
- deployment zones;
- optional vegetation density fields;
- landmark placeholders;
- movement/terrain modifier samples where authoritative mechanics require them.

Do not make the rendered mesh the source of truth for gameplay.

## M2.2 Deterministic height-field generator

Implement one simple terrain generator using deterministic noise/shape composition.

Required controls:

- seed;
- average elevation;
- roughness;
- broad hill frequency;
- flattening around deployment zones if needed;
- optional biome profile.

Tests:

- same inputs produce identical sampled height values;
- bounds are respected;
- deployment regions remain usable;
- no NaN/infinite values enter presentation.

Avoid depending on GPU noise or render-derived data for authoritative generation.

## M2.3 Low-poly mesh presenter

Generate the visible terrain mesh through Godot procedural geometry (`SurfaceTool`/`ArrayMesh` or another equally direct supported API).

Requirements:

- deterministic topology from the battlefield definition;
- faceted/low-poly visual treatment;
- vertex colour/material assignment by terrain region as appropriate;
- correct normals;
- no requirement for collision mesh unless later tooling needs it;
- regeneration in `BattleLab` from fixture seeds.

Authoritative `HeightAt(x,z)` should come from battlefield data/interpolation, not a physics raycast against the visual mesh.

## M2.4 Height projection

Implement a single tested conversion path:

```text
authoritative SimPosition(x,z)
        -> BattlefieldHeightSampler
        -> visual Vector3(x,height,z)
```

All troop/remains visual positioning should use this path.

If a terrain mesh detail differs slightly from interpolation, fix the generator/sampler relationship; do not add ad-hoc raycasts per unit.

## M2.5 Billboard foliage baseline

Add simple tagged foliage definitions:

- tree;
- bush/ground decoration;
- optional rock/prop mesh.

Scatter from deterministic battlefield presentation seed/density.

Foliage is initially decorative. It must not create hidden authoritative obstacles.

Visual rules:

- randomization must be deterministic for a battlefield seed;
- bounded size/tint/flip variation is allowed;
- billboard trees must remain readable at intended camera angles.

## M2.6 Camera rig

Implement a production battle camera with:

- pan;
- zoom;
- optional controlled orbit/angle adjustment if it does not undermine billboard readability;
- bounds/soft limits;
- focus selected squad/unit;
- reset-to-battle framing.

The camera never influences simulation.

## M2.7 Presentation scalability harness

Add `BattleScaleLab` scenarios:

- 100v100;
- 300v300;
- 500v500;

These may use synthetic/static transcript data when isolating rendering performance, but the primary 100v100 acceptance must still come from the real simulator.

Measure separately:

- headless simulation time;
- transcript creation time;
- scene spawn time;
- playback CPU frame cost;
- GPU/frame time where practical;
- memory;
- node count;
- effect count.

Do not optimize based on intuition alone.

## M2.8 Centralized presentation updates

If M1 uses per-node `_Process` methods, consolidate obvious repeated work before scaling:

- one playback controller evaluates time;
- one unit-view manager applies bulk updates;
- effects are pooled where repeated;
- units should not independently search the scene tree or query battle state.

Do not move to MultiMesh yet unless measured node/draw overhead is already unacceptable.

## M2.9 LOD/readability behaviour

Introduce minimal zoom/speed-dependent presentation controls:

- hide individual health/details at distant zoom;
- suppress routine damage effects at high playback speeds if required;
- preserve major death/rout/ability signals;
- avoid spawning one noisy effect per low-value event when the camera cannot resolve it.

### M2 gate

M2 passes when:

- multiple fixed seeds create repeatable low-poly terrain;
- unit Y placement follows deterministic height samples;
- billboard troops and foliage render correctly from normal camera angles;
- camera can inspect and frame large battles;
- 500v500 can be spawned and viewed without catastrophic degradation;
- performance metrics are recorded rather than guessed;
- no optimization tier beyond normal nodes/centralized updates is introduced without evidence;
- M1 battle digest remains unchanged by the visual upgrade;
- production runtime logs remain clean in editor and Windows export.

---

# 12. M3 — Battle System Expansion & Doctrine

## Goal

Turn the M1 duel into a representative auto-battle system with multiple squads, role differentiation, targeting priorities, ranged attacks, simple abilities, and doctrine-driven behaviour.

This milestone should prove the game is strategically interesting without implementing final launch content.

## M3.1 Multiple formations per army

Generalize army state to support multiple squads with:

- independent anchors;
- deployment positions;
- formation dimensions;
- role metadata;
- current target formation;
- stance/order;
- morale/cohesion;
- commander association.

Create fixture battles with at least three squads per side.

## M3.2 Spatial indexing

Introduce a simple deterministic spatial grid/hash only where needed for nearby formation/unit queries.

Requirements:

- deterministic bucket assignment;
- stable query ordering;
- no all-vs-all per-tick target scan;
- benchmark query counts in stress fixtures.

Do not implement a general navigation mesh.

## M3.3 Target-priority engine

Implement deterministic candidate scoring/filtering for the v1 doctrine subset established by DG-D.

Likely initial priorities:

- closest valid formation;
- ranged formation;
- cavalry/mobile formation;
- commander/command formation;
- weakest formation;
- largest threat/weight;
- monster/summoned category if tags exist.

Architecture:

```text
TargetPolicy
 -> collect allowed candidates
 -> deterministic score tuple
 -> stable tie-break by entity ID
 -> chosen target
```

Expose a debug explanation containing candidate scores and rejection reasons.

## M3.4 Chase/hold/advance behaviour

Implement a small set of squad actions:

- hold;
- advance;
- engage target;
- limited chase;
- retreat/rout.

Avoid arbitrary visual scripting.

The order model should be serializable and inspectable.

## M3.5 Formation variants

Add only formations required by DG-D v1, likely a small set such as:

- line;
- deep/block;
- loose/skirmish;
- compact/guard.

Each formation defines deterministic slot generation and footprint properties.

Do not make formation shape an unrestricted editor.

## M3.6 Ranged combat

Add one ranged unit fixture.

Authoritative simulation owns:

- range validation;
- target selection;
- cooldown/fire timing;
- hit/outcome calculation;
- arrival/resolution tick if projectile travel time matters;
- damage/status.

Viewer owns:

- arrow/tracer/bolt visual;
- launch/impact feedback.

No collision physics for ordinary projectiles.

## M3.7 Ability framework v1

Create a small data-driven effect pipeline sufficient for representative abilities without building a universal scripting language.

Start with explicit supported effect types such as:

- damage target;
- damage area;
- modify stat/status for duration;
- morale damage/heal;
- self/ally heal;
- simple displacement if needed.

Ability definition should include:

- ID;
- targeting rule;
- cooldown/cost if applicable;
- effect list;
- AI/use-policy tags;
- presentation event ID.

Do not introduce arbitrary executable expression strings.

## M3.8 Ability policies

Implement the v1 deterministic priority-rule model from DG-D.

Example structure:

```text
Rule 0: if self health <= 40%, use defensive ability
Rule 1: if enemy cluster count >= 5, use area ability
Rule 2: if enemy commander valid, use commander-target ability
Fallback: use default attack
```

Conditions must be from an explicit supported list.

Rules are evaluated in stable priority order.

## M3.9 Commander influence

Add a minimal commander relationship that can affect:

- morale/discipline;
- order availability;
- ability access;
- squad behaviour.

Commander death must emit a major semantic event and be visible in analysis.

Keep leadership capacity details provisional until the design is finalized.

## M3.10 Morale v1 refinement

Replace M1 fixture morale with the first representative model after design review.

Potential inputs supported by architecture:

- casualty percentage;
- rapid casualties;
- commander death;
- nearby routing formations;
- formation pressure;
- flanking state when implemented;
- fear/ability modifiers;
- faction/unit discipline.

All morale changes should emit explainable reason codes.

## M3.11 Battle inspection panel

In BattleLab add developer/player-readable selection information:

- squad ID/name;
- current order;
- current target;
- target-priority policy;
- morale;
- cohesion;
- member count;
- recent major events.

This is an early foundation for explainability and doubles as debugging instrumentation.

## M3.12 Representative fixture matrix

Create deterministic battle fixtures covering:

- melee vs melee;
- melee screen protecting ranged;
- fast flank unit trying to reach ranged;
- ranged focus fire;
- commander death morale shock;
- area ability hitting a cluster;
- rout cascade.

Each fixture gets invariant assertions, but do not overfit exact casualty counts unless that exact number is intentionally part of a golden deterministic regression.

### M3 gate

M3 passes when:

- at least three squads per side execute distinct doctrine;
- target priorities visibly alter behaviour;
- ranged attacks and one major ability are authoritative and correctly presented;
- no ordinary projectile physics/NavMesh is used;
- morale/routing creates understandable line collapse;
- selected squads show why they chose their current behaviour;
- repeat hashes remain stable;
- a 300v300 representative battle resolves headlessly substantially faster than playback;
- production BattleLab and exported build run cleanly.

---

# 13. M4 — Campaign Graph & Procedural World Vertical Slice

## Goal

Create a real strategic map foundation without yet depending on unresolved economy/faction-creator rules.

The player must be able to start a generated map, inspect provinces, move fixture armies between adjacent provinces, and save enough campaign state for later expansion.

## M4.1 Campaign state model

Implement stable models for:

- `CampaignId` / seed;
- factions;
- provinces;
- province adjacency;
- province owner;
- terrain/biome identity;
- landmark IDs;
- army location;
- capital/start markers;
- turn number;
- pending battle/engagement references where applicable.

Do not embed Godot Node references in authoritative campaign state.

## M4.2 Province graph generation

Implement a first deterministic logical graph generator independent of polygon rendering.

Requirements:

- connected graph;
- configurable province count;
- minimum/maximum degree controls or repair rules;
- sufficiently separated starting provinces;
- deterministic seed;
- graph validation.

The exact long-term map geometry algorithm is not required to be final here.

Tests:

- connectivity;
- no duplicate/self edges;
- start separation;
- same seed identical graph;
- generation succeeds over a large seed sample or reports explicit invalid generation rather than silently producing broken maps.

## M4.3 Province terrain/identity assignment

Assign deterministic province descriptors such as:

- plains;
- forest;
- hills;
- wetland;
- mountain-like/highland;
- other minimal fixture biomes.

Also support:

- one or two landmark fixtures;
- future economic fields as placeholders without final economy assumptions;
- stable battlefield seed derived/stored per province.

## M4.4 Campaign map presentation v1

Create production `CampaignMap.tscn`.

Initial presentation can use graph nodes/edges if necessary while polygon generation matures, but it must be designed to progress toward a 2D province map.

Required:

- visible provinces;
- adjacency readable;
- ownership treatment;
- capital markers;
- army markers;
- province selection;
- camera pan/zoom;
- selected province detail panel.

Do not make click logic depend on fragile label/text geometry.

## M4.5 Province polygon presentation

Add a province-shaped visual representation once the graph is stable.

Possible implementation approaches include Voronoi/Delaunay-derived cells or another deterministic irregular region method. The plan does not require a specific algorithm, but the resulting renderer must preserve logical adjacency rather than derive authoritative adjacency from rendered edges.

If polygon generation proves disproportionately costly, retain a readable node map temporarily and log the visual-map algorithm as a bounded follow-up rather than blocking all campaign logic.

## M4.6 Army location and movement commands

Implement campaign commands:

- create/place fixture army;
- select army;
- request movement to adjacent province;
- validate adjacency/ownership/movement rules available at this stage;
- update authoritative location.

No continuous world-space pathfinding.

## M4.7 Campaign Lab runtime acceptance

Create seeded acceptance map with:

- known province count;
- at least two factions;
- capitals;
- armies;
- several terrain types;
- move one army along a valid edge;
- reject one invalid non-adjacent move;
- emit campaign digest/report.

### M4 gate

M4 passes when:

- generated maps are deterministic and connected over a documented seed sample;
- production campaign scene renders the generated state;
- selection and adjacent movement use authoritative province IDs;
- invalid movement is rejected without corrupting state;
- repeated seed produces stable campaign digest;
- runtime acceptance and Windows build logs are clean.

---

# 14. M5 — Content Registry, Units, Factions & Visual Tagging

## Goal

Move fixture definitions into a maintainable, diffable content pipeline that supports later faction creation, AI generation, visual recommendations, and universal creature sprites.

## M5.1 Choose mechanical content representation

Select one primary source-controlled format after a small spike:

- JSON; or
- text `.tres` resources; or
- another plainly diffable format with strongly typed load/validation.

Evaluation criteria:

- easy agent edits;
- human-readable diffs;
- schema validation;
- stable IDs;
- cross-reference validation;
- straightforward Godot resource references for visuals.

Avoid storing core balance/content only in inspector-edited binary assets.

Document the choice in `docs/decisions/`.

## M5.2 Content registry and validation

Implement one startup-loadable registry for:

- unit definitions;
- abilities;
- visual sprite records;
- terrain records;
- faction fixture records;
- lord fixture records.

Validation must report all discoverable errors in one pass where practical:

- duplicate IDs;
- missing references;
- invalid numeric ranges;
- invalid tags;
- missing presentation resources;
- circular references if any;
- incompatible simulation version/content version where relevant.

A content validation failure should prevent starting a campaign rather than fail later in battle.

## M5.3 Unit-definition schema v1

Support fields needed by M3:

- stable unit ID;
- display name/localization key placeholder;
- category/role tags;
- health;
- movement;
- melee/ranged attack reference;
- morale/discipline;
- footprint/formation size class;
- ability IDs;
- strategic/recruitment fields as placeholders until DG-B;
- recommended visual tags;
- selected visual ID where fixed.

Do not add dozens of speculative stats.

## M5.4 Sprite visual metadata schema

Implement locked visual descriptors:

- stable sprite visual ID;
- texture/atlas reference;
- billboard configuration;
- size/scale;
- pivot/ground offset;
- visual tags;
- recommended roles;
- faction-colour/tint support metadata;
- remains category;
- optional portrait pairing.

Visual tags are descriptors, not mechanics.

## M5.5 Visual search/recommendation service

Implement deterministic filtering/scoring:

```text
requested/recommended tags
+ role compatibility
+ faction visual profile
- avoid tags
= ranked visual candidates
```

Player can ignore recommendations.

AI generation may use stronger coherence weighting.

## M5.6 Universal creatures

Add at least one universal creature fixture such as a wyvern or giant beast using the same visual independent of owning faction, with ownership conveyed through base/tint/marker.

Prove mechanics and visual identity are decoupled.

## M5.7 Faction visual profile

Create data model:

- primary visual tags;
- secondary visual tags;
- avoid tags;
- palette/ownership identity;
- preferred pack IDs if packs exist.

Generate a coherent sprite assignment for a fixture faction.

## M5.8 Content Browser Lab

Create a development scene/UI that can:

- list unit definitions;
- list sprite visuals;
- filter by tags;
- preview Sprite3D representation;
- show remains category;
- show recommendation score for selected unit/faction profile;
- surface validation errors.

This is production-maintained tooling, not a test-only substitute.

### M5 gate

M5 passes when:

- gameplay fixtures load through the registry rather than hard-coded constructors;
- invalid content produces clear validation failure;
- visual tags can recommend and filter sprites;
- AI-style visual profile produces coherent deterministic selections;
- universal creature visual reuse works across at least two faction identities;
- M3 battles still reproduce stable expected outcomes after migration;
- Content Browser Lab and exported build have clean logs.

---
# 15. M6 — Faction/Lord Creation & Army Management UI

## Goal

Create the player-facing construction loop once DG-A has defined the faction creation rules. The player should be able to create a mechanically valid faction and lord, choose independent visuals, build an army from available units, organize squads, and configure doctrine.

## Preconditions

- DG-A is closed and documented.
- DG-D has defined the v1 doctrine surface.
- M5 content registry and visual tagging are stable.

## M6.1 Faction construction domain model

Implement a validated faction build record containing the approved DG-A concepts, likely including some subset of:

- race/species chassis;
- culture/doctrine package;
- affinities;
- strengths;
- liabilities;
- faction-wide modifiers;
- roster access rules;
- visual profile;
- display metadata.

The implementation must reflect the design gate exactly rather than preserving earlier fixture assumptions.

Validation should return actionable reasons:

- budget exceeded;
- incompatible selection;
- missing required choice;
- duplicated mutually exclusive package;
- invalid roster state.

## M6.2 Mechanical modifier resolution

Create a deterministic, inspectable composition path that turns faction choices into effective modifiers.

Avoid hidden modifier application scattered through unrelated systems.

Recommended approach:

```text
Base unit definition
   + faction modifiers
   + lord/global modifiers
   + campaign/local modifiers
   -> EffectiveUnitSnapshot for a committed battle
```

The committed `BattleDefinition` should contain or reference resolved authoritative values in a way that cannot change during playback.

Modifier application order must be documented and tested.

## M6.3 Lord build model

Implement the DG-A-approved lord/god/leader creation surface:

- chassis/archetype;
- attributes/affinities;
- leadership;
- abilities;
- faction effects;
- name/title;
- battlefield visual ID;
- portrait ID if available.

Visual choice remains independent of mechanics.

## M6.4 Faction Creator UI

Build a production `FactionCreator` flow using Godot `Control` UI.

Required capabilities:

- browse/select mechanical packages;
- show current cost/budget/constraints if used;
- explain mechanical effects;
- show validation issues before confirmation;
- choose faction name/colour/visual profile;
- select or browse unit visuals using tag recommendations;
- deliberately choose a non-recommended visual;
- save faction build into campaign setup state.

Do not expose every raw numeric field as an editable slider unless explicitly approved by DG-A.

## M6.5 Lord Creator UI

Required:

- construct mechanical build;
- select name/title;
- choose from broad lord sprite list;
- visual recommendations based on tags;
- no hard lock between caster/melee appearance and mechanics;
- preview on neutral 2.5D stage if useful.

## M6.6 Army model

Implement campaign-facing army composition:

- army ID/name;
- commander/lord assignments;
- unit stacks/individual members as required by the battle model;
- squad assignment;
- squad formation;
- squad doctrine;
- army-level doctrine;
- validation for units without valid commander/squad if the design requires it.

Do not decide final leadership-capacity mechanics if still unresolved; use the approved rule only.

## M6.7 Army Management UI

Required:

- inspect available units;
- assign units to squads;
- create/delete/reorder squads;
- assign commander;
- choose formation;
- choose deployment role/relative position;
- choose target priority;
- choose hold/advance/chase policy;
- configure supported ability policies;
- show invalid configurations clearly.

The UI should display plain-language doctrine summary, e.g.:

```text
Left Wing — 32 Riders
Formation: Loose
Order: Advance
Target priority: Enemy ranged
Chase: Limited
Commander: Varyn
```

## M6.8 Battle Preview fixture integration

From the army UI, allow a development/preview action that commits the configured army against a fixture opponent and opens the real battle path.

This is a critical anti-facade acceptance feature: faction/lord/army UI choices must produce the authoritative battle input consumed by M3/M2 production systems.

## M6.9 Round-trip build serialization

Faction/lord/army builds must serialize and deserialize without semantic changes.

Use stable content IDs rather than embedding texture paths or editor instance IDs in mechanical state.

### M6 gate

M6 passes when a user can, in the production runtime:

1. create a valid faction;
2. deliberately choose visuals independently of mechanics;
3. create a lord;
4. create an army;
5. organize at least three squads;
6. assign doctrine;
7. preview/commit that exact configuration into the authoritative battle system;
8. watch the battle and inspect doctrine effects;
9. serialize/reload the created build;
10. do all of the above with clean runtime logs.

---

# 16. M7 — Campaign Economy, Recruitment, Structures & Turn Resolution

## Goal

Turn the M4 map into a strategic growth game using the explicit rules established by DG-B and DG-C.

## Preconditions

- DG-B campaign economy/recruitment is closed.
- DG-C turn resolution is closed.
- M6 faction/army data can be instantiated into campaign state.

## M7.1 Resource ledger

Implement authoritative campaign resources exactly as defined by DG-B.

Requirements:

- deterministic per-turn calculation;
- source breakdown by province/structure/modifier;
- affordability checks centralized;
- no negative-resource exploit unless design explicitly allows debt;
- debug explanation of deltas.

## M7.2 Province yields

Apply province characteristics to economy/recruitment according to design.

Province detail UI must show:

- base yield;
- structure effects;
- faction effects;
- landmark effects;
- final yield.

Avoid unexplained final totals.

## M7.3 Recruitment

Implement:

- available roster determination;
- recruitment cost;
- timing/queue if required;
- location restrictions;
- unit creation with stable IDs;
- placement into province/garrison/army according to design.

Recruitment must use the same unit definitions later committed into battle.

## M7.4 Structures

Implement the approved structure model:

- availability;
- cost;
- slot restrictions;
- build timing;
- completion;
- effects;
- destruction/capture handling if defined.

Do not implement speculative tech trees or siege systems unless DG-B explicitly includes them.

## M7.5 Turn command buffer

Represent player strategic decisions as explicit commands rather than direct UI mutation of campaign state.

Example command categories:

- recruit;
- construct;
- move army;
- transfer units;
- change army doctrine;
- end/commit turn.

Validation must occur before or during resolution according to DG-C rules.

## M7.6 Turn resolver

Implement the chosen sequential/simultaneous/hybrid model.

Required properties:

- deterministic order resolution;
- explicit conflict outcomes;
- battle creation when hostile armies contest the same province according to rules;
- no dependency on UI update order;
- reproducible campaign digest from same starting state and command set.

## M7.7 Battle pending state

A conflict should produce a `PendingBattle`/committed battle record containing:

- province;
- participants;
- committed army snapshots;
- battlefield definition/seed;
- doctrine;
- simulation version;
- battle seed.

Once committed, changes elsewhere in the UI cannot mutate that battle input.

The simulator resolves it exactly once for the campaign state transition. Playback may occur before or after consequence application depending on UX, but the result must not be recalculated inconsistently.

## M7.8 Strategic UI integration

Campaign map must support:

- resource display;
- province details;
- recruitment;
- structure construction;
- army inspection;
- legal move visualization;
- turn commit/end action;
- pending battle notification.

## M7.9 Campaign-turn acceptance fixtures

Create deterministic fixtures that prove:

- income collection;
- recruitment;
- construction completion;
- valid movement;
- invalid movement rejection;
- hostile collision creates a pending battle;
- identical commands produce identical resulting campaign digest.

### M7 gate

M7 passes when a human can play several turns on a generated map using real production UI, grow an army, construct at least one meaningful structure, move, generate a real pending battle, and receive clean deterministic campaign state transitions.

---

# 17. M8 — Strategic AI & Procedural AI Factions

## Goal

Create opponents that can participate in the same strategic systems without hidden special-case game rules.

AI should be understandable, deterministic for a given state/seed where randomness is used, and instrumented so bad decisions can be diagnosed.

## M8.1 AI knowledge model

Define the information available to strategic AI.

The AI must not inspect information the player-equivalent faction should not know merely because it is easy to access in memory.

At minimum distinguish:

- own state;
- visible/known enemy state;
- public province information;
- hidden information if the design later includes it.

## M8.2 Utility decision framework

Implement a small transparent scoring framework rather than a general planner.

Each candidate action should expose:

- candidate ID/action;
- component scores;
- final score;
- disqualifying rule if rejected;
- stable tie-break.

Example expansion score:

```text
+ economic value
+ landmark value
+ strategic connectivity
+ estimated vulnerability
- travel cost
- forecast casualties
- newly exposed frontier risk
```

Weights belong in inspectable configuration/content where practical.

## M8.3 Strategic army roles

Introduce only roles demonstrated useful by the current campaign, e.g.:

- expansion force;
- field army;
- frontier defence;
- emergency defence;
- raider.

Roles guide recruitment and movement preferences; they are not hard-coded combat bonuses.

## M8.4 Recruitment/composition AI

AI should build armies based on:

- available faction roster;
- current resource budget;
- army role;
- known enemy composition;
- doctrine preferences;
- minimum commander requirements.

Avoid fixed scripted armies that bypass the player-facing content model.

## M8.5 Battle forecast model

Strategic AI needs to estimate risk before committing combat without cheating by resolving the exact hidden future battle seed and reading its result.

Implement one approved estimation approach, such as:

- heuristic composition score with matchup terms; or
- limited sample simulations with forecast seeds distinct from the committed battle seed; or
- a hybrid.

Forecast output should include uncertainty/confidence if useful.

The exact committed battle remains authoritative and may surprise the forecast.

## M8.6 Doctrine generation

Given faction traits and army role, AI chooses:

- formation;
- target priorities;
- hold/advance behaviour;
- ability policies;
- commander assignment.

Choices must use the same doctrine schema as the player.

## M8.7 Procedural faction concept generation

Using DG-A rules, generate coherent factions through packages/concepts rather than independent random rolls.

A generation sequence should look like:

```text
Choose concept/archetype
 -> choose compatible mechanical chassis/packages
 -> choose strengths/liabilities
 -> derive roster priorities
 -> derive doctrine preferences
 -> create lord profile
 -> create visual profile
 -> choose tagged sprites
 -> validate faction
```

If validation fails, repair/retry deterministically within bounded attempts.

## M8.8 AI visual identity

Use M5 visual tags to select coherent visuals.

Requirements:

- primary/secondary/avoid tag profile;
- role-aware sprite choice;
- consistent ownership palette/base treatment;
- universal creature reuse when roster includes such entities;
- deterministic generation from faction seed.

## M8.9 AI Debug panel

Provide a developer overlay/tool showing:

- current goals/army roles;
- candidate provinces/actions;
- utility components;
- recruitment reasoning;
- battle forecast;
- chosen doctrine;
- visual-generation profile.

Do not rely on opaque log prose alone.

## M8.10 AI campaign soak

Run headless/accelerated campaign fixtures for many turns with multiple AI factions.

Track:

- crashes/errors;
- invalid states;
- armies stuck forever;
- zero-resource deadlocks;
- repeated nonsensical move loops;
- failure to recruit;
- runaway state growth;
- battle count;
- faction survival duration.

No requirement that AI be strategically brilliant yet. It must be valid, active, and debuggable.

### M8 gate

M8 passes when at least two generated AI factions can create valid armies, expand, recruit, build according to available rules, generate doctrine, commit battles, and continue a multi-turn campaign without special-case cheats or invalid-state accumulation.

---

# 18. M9 — Full Campaign-to-Battle Loop

## Goal

Deliver the first complete product loop from campaign setup through conflict and back to changed campaign state.

This is the most important product milestone after M1.

## Preconditions

- DG-E victory/defeat baseline is closed.
- M1–M8 gates pass independently.

## M9.1 New campaign flow

Production flow:

```text
Main Menu
 -> New Campaign
 -> Create/select faction
 -> Create/select lord
 -> Generate world
 -> Place player + AI factions
 -> Enter CampaignMap
```

Use loading/progress feedback where generation is non-trivial.

## M9.2 Campaign faction instantiation

Turn faction/lord build records into campaign entities with:

- starting province/capital;
- initial resources;
- initial roster/army;
- faction visual identity;
- lord entity;
- AI controller for opponents.

## M9.3 Conflict commit

When opposing armies contest a province:

1. finalize participants and doctrine;
2. derive battlefield definition from province;
3. derive deterministic committed battle seed according to documented campaign rule;
4. snapshot effective unit data/modifiers;
5. construct immutable `BattleDefinition`;
6. resolve authoritative battle headlessly;
7. persist pending `BattleResult` and transcript/replay handle;
8. offer player watch/skip where player-involved.

The battle is resolved once. `Watch` must never roll again.

## M9.4 Watch path

Production transition to battle viewer:

- load pre-resolved transcript;
- generate battlefield from committed battlefield definition;
- present the battle;
- allow pause/speed/camera/inspection;
- reach result screen;
- return to campaign.

## M9.5 Skip path

Apply the exact stored result immediately with no battle-scene requirement.

Skip must be fast enough for routine AI-vs-AI conflict resolution.

## M9.6 Campaign consequence application

Apply authoritative result:

- deaths/casualties;
- survivors;
- routed/retreated armies;
- commander/lord status;
- province ownership;
- army removal/merge/retreat according to design;
- battle history summary;
- victory/defeat checks.

This application must be idempotence-safe: a saved/loading transition must not apply the same result twice.

## M9.7 Province-to-battlefield identity

Connect M4 province descriptors to M2 terrain generation:

- province biome influences battlefield profile;
- elevation/forest/wetness/etc. influence deterministic terrain parameters;
- province stable battlefield seed gives recognizable repeated terrain where the GDD preference is adopted;
- weather/season modifiers can layer on if implemented.

## M9.8 End-to-end acceptance campaigns

Create compact deterministic campaigns designed to reach combat quickly.

Acceptance A — Watch:

- create/load fixture campaign;
- move into contested province;
- resolve battle;
- watch playback;
- return to map;
- verify casualties/ownership.

Acceptance B — Skip:

- same campaign state and commands;
- same battle seed;
- skip playback;
- verify final campaign digest identical to A after consequence application.

Acceptance C — AI-vs-AI:

- AI battle resolves headlessly;
- campaign continues without loading battle viewer;
- result stored/summarized correctly.

### M9 gate

M9 passes only when the game has a coherent repeatable loop:

> create faction -> enter map -> recruit/manage -> move -> contest province -> pre-resolve battle -> watch or skip -> apply identical consequences -> continue campaign -> reach baseline victory/defeat condition.

A milestone review must verify this in the exported Windows runtime, not solely from internal tests.

---

# 19. M10 — Explainability, Replay & Post-Battle Analysis

## Goal

Make automated outcomes understandable enough that the player can improve their faction, composition, and doctrine rather than seeing combat as opaque dice rolling.

## M10.1 Event reason codes

Ensure important simulation events carry reason data usable by UI/debugging.

Examples:

- target selected because `Priority:Ranged`;
- target rejected because out of allowed range;
- morale loss from casualty/commander death/rout aura;
- ability not used because condition false/cooldown/unavailable target;
- formation changed target due to target destruction;
- rout began because morale crossed threshold.

Do not derive explanations later from incomplete state if the authoritative system already knows the reason.

## M10.2 Battle timeline

Build timeline index from transcript:

- battle start/end;
- first contact;
- commander deaths;
- formation breaks;
- routs;
- major abilities;
- high-casualty moments.

Initial UI may be a list/bookmark panel rather than a fully draggable timeline.

## M10.3 Selection inspector

During playback show for selected squad/unit:

- current/last order;
- target;
- target-priority policy;
- health/morale summary;
- formation state;
- active statuses;
- commander effects;
- recent major events.

For pre-resolved playback, data shown must correspond to playback time/keyframe, not final-state values accidentally displayed throughout.

## M10.4 Post-battle report

Required baseline:

- winner;
- duration;
- starting/final unit counts;
- dead/routed/surviving counts;
- casualties by squad;
- damage dealt/received where tracked;
- commander status;
- morale break/rout times;
- major ability usage;
- major event summary.

## M10.5 Doctrine feedback

Where data supports it, call out useful factual observations without pretending to be an omniscient strategy coach.

Examples:

- squad never reached preferred target before routing;
- ranged unit spent N% of active time without a valid target;
- ability condition was never satisfied;
- commander death preceded morale collapse by N ticks;
- cavalry changed target X times due to chase policy.

## M10.6 Replay controls v1

Add:

- restart;
- jump to key event/bookmark;
- optional rewind/seek if keyframe architecture is already sufficient;
- speed controls beyond initial acceleration if useful.

Do not block M10 on perfect arbitrary-frame scrubbing if transcript design requires a larger change. Key-event jumps provide high value first.

### M10 gate

M10 passes when a reviewer can watch a deterministic loss and answer, using in-game data, which squads broke first, what they targeted, what major events occurred, and what caused the decisive morale/commander events.

---

# 20. M11 — Save/Load, Versioning & Recovery

## Goal

Persist campaigns safely while respecting deterministic battle/version constraints.

## Preconditions

- DG-F replay retention policy is closed.

## M11.1 Save schema

Define explicit versioned save DTOs rather than serializing live Godot nodes.

Persist:

- campaign seed/version;
- turn;
- faction builds;
- lord builds;
- province state;
- armies/units;
- resources/structures;
- AI persistent state required for deterministic continuation;
- content version/reference IDs;
- pending battle if one exists;
- replay history according to DG-F.

## M11.2 Save transaction safety

Use safe write pattern:

1. serialize to temporary file;
2. validate/read-back if reasonable;
3. replace previous save atomically where supported;
4. retain backup/previous save.

Avoid corrupting the only save on interruption.

## M11.3 Load validation

Reject or recover gracefully from:

- missing required content IDs;
- unsupported save version;
- corrupt data;
- incompatible simulation version for pending battle;
- malformed pending battle state.

Never silently substitute a different unit/trait because an ID is missing.

## M11.4 Migration framework

Create only the minimal explicit migration structure required for the first schema change. Do not invent a general database migration framework.

Each save schema version has a documented upgrade path if supported.

## M11.5 Pending battle recovery

Critical scenario:

- save exists after battle has been authoritatively resolved but before playback/consequence flow is finished.

On load the game must know whether:

- result is committed;
- consequences are already applied;
- playback is optional/pending;
- it is safe to resume without resolving again.

Use explicit battle state enum/transaction markers.

## M11.6 Replay persistence

Implement DG-F policy.

If preserving transcripts across game updates, include:

- transcript schema version;
- simulation version;
- content identifiers/snapshots required for faithful playback;
- fallback behavior when old playback assets are unavailable.

## M11.7 Save/load acceptance matrix

Test saves at:

- fresh campaign;
- after recruitment/construction;
- armies in motion/orders staged as applicable;
- immediately before pending battle;
- after battle resolution but before watch/skip completion;
- after consequences;
- campaign victory/defeat state.

### M11 gate

M11 passes when deterministic campaign state survives save/reload at all critical transaction boundaries without duplicate battle resolution or consequence application.

---

# 21. M12 — Scale, Performance & Production Hardening

## Goal

Profile representative workloads and optimize only proven bottlenecks while preserving deterministic behaviour and readable playback.

## M12.1 Benchmark suite

Create repeatable scenarios:

### Battle simulation

- 100v100;
- 300v300;
- 500v500;
- multiple squads;
- ranged/ability-heavy stress;
- rout cascade.

### Battle presentation

- 500v500 visible;
- dense foliage;
- many remains;
- major-effect burst;
- 1x and accelerated playback.

### Campaign

- small/medium/large province counts;
- 2/4/8 AI factions as reasonable;
- multiple battles in one turn;
- long AI soak.

Record machine specs with benchmark artifacts.

## M12.2 Simulation profiling

Measure:

- tick time;
- target-query count/time;
- formation update time;
- ability evaluation;
- transcript generation;
- allocations/GC;
- auto-resolve wall time.

Likely optimization order:

1. remove accidental allocations in hot loops;
2. improve spatial-query/data layout;
3. reduce unnecessary event/keyframe volume;
4. batch repeated calculations;
5. only then consider lower-level/native optimization.

## M12.3 Presentation profiling

Measure:

- node count;
- draw calls;
- Sprite3D overhead;
- transform update cost;
- material/tint churn;
- effect spawn/despawn;
- remains count;
- foliage cost.

If ordinary nodes are proven too costly, evaluate:

- centralized process/update;
- fewer Node3D instances for non-interactive props;
- `MultiMesh`/instancing for foliage or extremely simple repeated visuals;
- visual pooling.

Do not migrate all units to MultiMesh merely because the API exists. Selection, per-instance state, and death/remains presentation must remain practical.

## M12.4 Transcript memory/profile

Measure representative transcript size and creation cost.

Only after measurement consider:

- less frequent motion keyframes;
- delta encoding;
- compact binary serialization;
- pruning redundant routine events for long-term storage while preserving live analysis needs.

## M12.5 Visual noise/readability stress

Large battles must remain understandable.

Tune:

- routine damage flash rate;
- projectile density;
- audio aggregation;
- health/status overlays;
- foliage occlusion;
- distant LOD;
- remains visibility;
- major-event emphasis.

Performance optimization must not make the battle illegible.

## M12.6 Fault hardening

Add bounded handling for:

- impossible/stalemated battle tick cap;
- content validation failures;
- map generation repair exhaustion;
- missing presentation asset;
- invalid save;
- transcript mismatch;
- AI no-valid-action states.

Failures should surface diagnostically rather than cascade into silent corruption.

## M12.7 Build soak

Run exported Windows build through:

- repeated campaign turns;
- multiple watched battles;
- multiple skipped battles;
- save/load;
- AI soak;
- scene transitions.

Inspect logs for accumulated warnings/errors/resource leaks.

### M12 gate

M12 passes when representative target-scale scenarios have measured budgets, identified hotspots have evidence-based fixes, no speculative optimization architecture remains unproven, and exported-runtime soak remains stable.

---

# 22. M13 — Content/UX Expansion & Release Candidate Foundation

## Goal

Turn the technically complete loop into a coherent game slice suitable for broader balancing and content production.

This milestone is intentionally less prescriptive because exact launch content counts, lore, research, diplomacy, sieges, and other systems remain design-dependent.

## M13.1 Replace development fixtures with representative content set

Build enough content to demonstrate real variety across:

- multiple faction mechanical concepts;
- multiple lord archetypes;
- infantry/ranged/mobile/monster/caster roles;
- meaningful abilities;
- multiple visual packs;
- universal creatures;
- multiple province biomes/landmarks.

Content quantity should be chosen from production capacity, not a desire to match *Dominions* breadth.

## M13.2 Campaign UX pass

Improve:

- province readability;
- army selection;
- legal movement feedback;
- recruitment/construction flow;
- turn notifications;
- battle alerts;
- AI-turn feedback;
- victory/defeat flow.

## M13.3 Faction creator UX pass

Focus on comprehension:

- explain trade-offs;
- show derived effects;
- highlight invalid combinations;
- support visual browsing/filtering;
- preserve mechanical/visual independence;
- allow save/load of faction presets if approved.

## M13.4 Doctrine UX pass

Doctrine is a signature system and should avoid feeling like raw programming.

Improve:

- plain-language summaries;
- previews/tooltips;
- target-priority explanation;
- ability policy ordering;
- formation visual preview;
- invalid/contradictory rule warnings.

## M13.5 Battle presentation pass

Without adding expensive character animation, improve spectacle through:

- lighting;
- weather;
- shadows;
- terrain materials;
- restrained particles;
- major spell VFX;
- audio;
- formation movement readability;
- clear routes and deaths;
- better remains.

The art direction remains token/simulation-like rather than action-game animation.

## M13.6 Accessibility and settings baseline

Add practical desktop settings:

- resolution/window mode;
- audio volumes;
- UI scale if needed;
- camera speed;
- playback speed defaults;
- effect density/readability controls where useful;
- colour/ownership readability options where practical.

## M13.7 Release verification matrix

Define repeatable acceptance campaigns and battles representing:

- faction creation;
- campaign generation;
- recruitment/building;
- doctrine;
- watched battle;
- skipped battle;
- AI battle;
- save/load;
- campaign completion.

All must pass in exported Windows runtime with clean logs.

### M13 gate

M13 is complete when the foundation is no longer merely an engineering demo: a new player can create a faction, play a small campaign against AI, understand battle outcomes, and complete the baseline victory loop without developer intervention.

---

# 23. Cross-Cutting Implementation Rules

## 23.1 Stable IDs everywhere

Persistent/content-facing objects use stable IDs.

Never use:

- display names as primary keys;
- texture filenames to infer mechanics;
- Godot instance IDs in save state;
- dictionary hash codes as deterministic identities.

## 23.2 Commands for authoritative player actions

UI should request actions through explicit domain commands/validated services rather than mutating authoritative state directly.

This applies to:

- army movement;
- recruitment;
- construction;
- faction choices;
- doctrine changes;
- battle commitment;
- turn commit.

The goal is clear state transitions, not CQRS-style architecture for its own sake.

## 23.3 Immutable battle commitment

After a battle is committed, the authoritative input snapshot must not be affected by later content edits, UI state changes, or presentation object mutation.

## 23.4 Version authoritative formats deliberately

At minimum version:

- simulation rules;
- battle transcript schema;
- save schema;
- content schema if required.

Do not increment versions casually; use them when compatibility actually changes.

## 23.5 No hidden scene-tree dependencies

Production Nodes should receive dependencies through exported references, explicit construction/bootstrap, or direct known child paths that are validated on ready.

Avoid broad `GetTree().Root.FindChild(...)`-style discovery for foundational gameplay wiring.

If a required production node/resource is absent, fail clearly.

## 23.6 No frame-rate-dependent simulation

Authoritative simulation never uses `_Process(delta)` time to determine outcomes.

Playback may use frame delta for interpolation.

## 23.7 No presentation-to-simulation feedback after commit

Examples of forbidden outcome dependencies:

- projectile collision triggering damage;
- Sprite3D overlap determining melee contact;
- animation completion causing attack;
- terrain physics raycast changing authoritative unit position;
- effect spawn success deciding ability success.

## 23.8 Data validation before gameplay

Content references should fail at load/validation time, not minutes later when a battle tries to spawn a missing sprite or resolve a missing ability.

## 23.9 Debug tools should reuse truth

Battle inspector, AI explanation, and post-battle report should read the authoritative reasons/events/state already produced by systems. Do not implement parallel explanatory approximations that can disagree with the rules.

---

# 24. Testing Strategy by Layer

## 24.1 Pure deterministic tests

Use for:

- RNG;
- scaled coordinate helpers;
- stable IDs;
- content validation;
- modifier composition;
- province graph generation;
- deterministic campaign commands;
- battle resolution;
- doctrine target selection;
- morale calculations;
- replay digest;
- save migrations.

These tests should be numerous and fast.

## 24.2 Golden deterministic scenarios

Use sparingly for important invariants where exact output is valuable.

Examples:

- known RNG sequence;
- M1 battle result/digest;
- known province graph seed;
- committed battle snapshot -> digest;
- save round trip.

Do not create hundreds of brittle golden hashes that turn every intentional balance change into a mass update exercise.

When an intentional rules change alters a golden digest:

1. document the reason;
2. inspect the actual state/event diff;
3. update simulation version if compatibility semantics require it;
4. then update expected digest.

Never update a hash merely because the test failed.

## 24.3 Runtime acceptance scenarios

Each major production surface should have a bounded deterministic scenario:

- `bootstrap.m0`;
- `battlelab.m1.melee`;
- `battlelab.m3.doctrine`;
- `battlelab.m2.scale` where appropriate;
- `campaignlab.m4.map`;
- `campaign.m9.watch`;
- `campaign.m9.skip`;
- `save.m11.pending-battle`.

The scenario is selected through user command-line args and executes through production scenes/components.

## 24.4 Exported-player acceptance

At milestone gates, run the critical scenario in an exported Windows build.

The editor is not the product.

## 24.5 Visual evidence

For visual milestones, retain screenshots or deterministic movie/frame capture where useful, but do not treat an image alone as correctness proof.

Visual evidence should supplement:

- authoritative report;
- clean log;
- test results.

---

# 25. Performance Budgets and Measurement Policy

Exact final budgets should be calibrated on the target development machine and later on representative hardware. Until then use directional requirements rather than fabricated hard numbers.

## 25.1 Headless simulation

Requirement:

> representative battles must resolve substantially faster than watched real time.

Track equivalent battle duration vs wall time.

A several-minute 500v500 battle taking many seconds headlessly is a profiling signal; do not hide that with score-based auto-resolve.

## 25.2 Watched battles

Early functional target:

- normal 100v100 and 300v300 battles comfortably interactive;
- 500v500 stress path remains usable enough to profile and inspect.

Final supported maximum is decided from profiling/content needs.

## 25.3 Campaign AI

AI turns should remain responsive at intended faction/map counts. If not:

- profile candidate generation;
- cache stable evaluations;
- reduce unnecessary exact simulations;
- spread non-player work across deterministic phases if needed.

Do not reduce decision quality blindly without measurement.

---

# 26. Agent Workflow and Worklog Rules

This project is expected to be modified by multiple agents/models. Repository process should make handoff and independent review easy.

## 26.1 Before a task

Agent must read:

1. `AGENTS.md`;
2. relevant GDD section;
3. current implementation-plan milestone/task;
4. relevant architecture decision records;
5. latest worklog/checkpoint if maintained.

## 26.2 During a task

Agent should:

- implement only the requested scope plus necessary fixes;
- prefer production paths over facades;
- run targeted fast tests during iteration;
- use the frozen verification commands;
- avoid rewriting infrastructure to bypass a failure;
- report unexpected architectural conflict rather than silently weaken requirements.

## 26.3 At task completion

Record:

- task ID;
- files changed;
- behaviour implemented;
- tests/commands run;
- runtime scenario run;
- result/digest where relevant;
- known limitations;
- any design assumptions;
- whether acceptance criteria changed, and who/what authorized that change.

## 26.4 Milestone review

Milestone review should be read-only first:

- inspect source and scene structure;
- inspect test changes for weakening;
- run approved commands independently;
- compare runtime reports to expected criteria;
- inspect logs;
- run exported build.

A milestone does not become complete because the implementing agent says it is complete.

---

# 27. Risks and Mitigations

## Risk A — Testing infrastructure becomes the project

**Failure mode:** agent creates increasingly elaborate runners/wrappers/plugins, each introducing new errors.

**Mitigation:** M0 freezes a tiny supported verification contract; infrastructure changes require explicit tasks and evidence.

## Risk B — Clean simulation, broken Godot game

**Failure mode:** isolated logic tests pass while scenes/resources/UI are broken.

**Mitigation:** every gameplay milestone includes direct production-scene acceptance and exported-player smoke.

## Risk C — Renderer becomes authoritative by accident

**Failure mode:** raycasts/collisions/animations start affecting outcomes.

**Mitigation:** immutable pre-resolved battle; transcript-only viewer; deterministic result tested before scene launch.

## Risk D — 1,000 independent agents cause performance collapse

**Failure mode:** each unit searches/paths/thinks independently every frame.

**Mitigation:** formation-first decisions, spatial queries, cached targets, fixed ticks, unit state kept simple.

## Risk E — Transcript grows uncontrollably

**Failure mode:** per-frame/per-unit positions produce huge replays.

**Mitigation:** semantic events + squad-level keyframes; measure before compression.

## Risk F — Procedural factions feel random/incoherent

**Failure mode:** traits/visuals selected independently.

**Mitigation:** concept/package generation, visual profiles, tagged sprite recommendation, validation/repair.

## Risk G — AI has perfect deterministic foresight

**Failure mode:** strategic AI queries exact future battle outcome before committing.

**Mitigation:** separate forecast model/information rules; committed battle seed/result inaccessible to decision logic until engagement commit.

## Risk H — Procedural battlefields introduce pathfinding complexity

**Failure mode:** terrain generates maze-like obstacles and forces NavMesh/A* work.

**Mitigation:** terrain initially influences elevation/movement modifiers/readability; avoid complex impassable geometry; 2D simulation coordinates remain authoritative.

## Risk I — Faction system becomes impossible to balance

**Failure mode:** hundreds of unrestricted numerical sliders multiply combinations.

**Mitigation:** DG-A favors packages/chassis/trade-offs and validation rather than arbitrary stat editing.

## Risk J — Content scope overwhelms systems work

**Failure mode:** trying to match long-running strategy-game breadth before the loop is proven.

**Mitigation:** fixture/representative content through M9; content expansion only after full loop is stable.

---

# 28. Explicit Deferred Systems

The following should not enter the critical path unless separately designed and approved:

- multiplayer/networking;
- diplomacy beyond what a later campaign design explicitly requires;
- tactical player control during battle;
- manual spell casting during battle;
- skeletal unit animation;
- modular paper-doll/attachment-point sprite generation;
- per-soldier NavMesh/A*;
- realistic projectile physics;
- destructible physics terrain;
- underground/alternate world layers;
- naval systems;
- complex siege simulation;
- research/magic tree until designed;
- mod/plugin SDK;
- native GDExtension optimization;
- arbitrary user scripting language for doctrine;
- always-retained full historical replays.

A deferred item may later become important. Its absence from the foundation is intentional, not an oversight.

---

# 29. Recommended First Implementation Sequence Inside M0–M1

If beginning from an empty repository, execute in this order rather than parallelizing too early:

1. Create the Godot 4.7.1 .NET/C# project with Forward+ and commit text project state.
2. Prove `dotnet build`.
3. Create `BootstrapLab.tscn` and command-line acceptance argument handling.
4. Prove explicit log file + JSON report + intentional exit.
5. Prove Windows command-line export and exported-player acceptance.
6. Freeze commands in `AGENTS.md`.
7. Implement deterministic RNG and stable digest writer with tests.
8. Implement `BattleDefinition` fixture and validation.
9. Implement two formation states with scaled integer positions.
10. Implement fixed-tick advance/contact.
11. Add deterministic melee/death.
12. Add morale/rout/terminal result.
13. Add transcript semantic events and formation keyframes.
14. Prove repeated headless digest stability.
15. Build production `BattleLab.tscn` using Sprite3D views.
16. Play transcript: move -> bump -> flash -> death -> remains -> rout.
17. Add pause/speed.
18. Add runtime event-count report.
19. Add skip/watch equivalence test.
20. Run exported-player BattleLab acceptance twice.
21. Review M1 without changing acceptance criteria.

Only then proceed to procedural terrain and broader battle mechanics.

---

# 30. Definition of Done

## Task-level done

A task is done when:

- requested production behaviour exists;
- relevant fast tests pass;
- relevant runtime scenario is run when the task touches runtime behaviour;
- logs are clean;
- documentation/worklog is updated;
- no known acceptance requirement is bypassed.

## Milestone-level done

A milestone is done when:

- every milestone acceptance criterion is met;
- independent rerun of approved commands succeeds;
- production scenes/assets are used;
- exported Windows runtime passes required smoke/acceptance;
- runtime logs are clean;
- deterministic digests match expected invariants;
- no tests were weakened to obtain the pass;
- no unresolved design gate was silently implemented as permanent canon;
- known limitations are documented.

## Product-foundation done

The foundation is successful when the following is true in one coherent exported build:

> The player creates a mechanically valid faction and lord, selects independent preset visuals, enters a generated province campaign, recruits and organizes armies, assigns doctrine, moves into a contested province, commits a deterministic battle, chooses either to skip or watch the exact same pre-resolved result on a procedural low-poly 2.5D battlefield of billboarded units, returns to a correctly updated campaign, can understand the important reasons for the battle outcome, can save/reload, and can eventually defeat or be defeated by functioning AI opponents.

---

# 31. Requirements Traceability Matrix

This matrix exists so reviewers can verify that the implementation sequence actually covers the GDD's locked foundations rather than merely mentioning them.

| Locked design requirement | Primary implementation proof |
|---|---|
| Godot 4.7.1 .NET/C# + Forward+ | M0 toolchain/build/export gate |
| Province-based turn-based campaign | M4 campaign graph; M7 turn resolver |
| Procedural province world | M4 graph/identity generation and map runtime |
| Custom faction and lord | DG-A + M6 creators |
| Mechanical/visual independence | M5 visual registry + M6 deliberate non-matching visual acceptance |
| Preset sprites, no modular body construction | M5 visual schema/content browser; deferred-system prohibition |
| Tagged sprite metadata | M5 tag filtering/recommendation + M8 AI visual identity |
| Universal creature visuals | M5 cross-faction universal creature fixture |
| Armies organized as squads/formations under commanders | M1 formation base; M3 multi-squad; M6 army management |
| Pre-battle doctrine | DG-D + M3 doctrine engine + M6 doctrine UI |
| Fully automated battle after commitment | M1 immutable battle commit and playback-only viewer |
| Exactly one deterministic battle simulator | M1 simulator/result/transcript gate |
| Battle resolves before watched playback | M1 BattleLab; M9 campaign conflict commit |
| Auto-resolve is skip playback, not approximation | M1 skip/watch equivalence; M9 end-to-end equivalence |
| Viewer cannot alter outcome | M1 transcript playback boundary + cross-cutting rule |
| Formation-first combat | M1/M3 formation state/contact/target architecture |
| No initial per-unit NavMesh/A* | M1/M3 movement rules; M2 terrain separation |
| No physics-driven ordinary combat | M1 melee; M3 ranged projectile presentation |
| Minimal symbolic animation | M1 Sprite3D bump/flash/death/remains |
| 2.5D low-poly terrain + billboards | M2 battlefield generator/presentation |
| Simulation uses 2D horizontal coordinates | M1 scaled SimPosition + M2 height projection |
| Province-derived battlefield | M4 stable battlefield identity + M9 province integration |
| Headless sim much faster than playback | M1/M3/M12 benchmark requirements |
| Runtime Godot verification mandatory | M0 frozen contract + every milestone gate |
| Clean logs required | M0 logging contract + every runtime gate |
| Acceptance cannot be weakened | Section 2/6/26 and independent milestone review |
| Testing infrastructure remains small/boring | M0 approved commands and infrastructure freeze |
| Explainable outcomes | M3 debug reasons + M10 analysis/replay |
| Strategic AI does not cheat exact battle future | M8 knowledge/forecast separation |
| Mechanics/content are diffable and inspectable | M5 registry/data-format decision |

Any locked GDD requirement lacking a concrete implementation proof should be treated as a plan defect and resolved before the dependent milestone is accepted.

---

# 32. Independent Review Checklist

A reviewer comparing this plan or an implementation against alternatives should challenge the following areas explicitly.

## Architecture

- Is there exactly one authoritative battle resolution path?
- Can watched playback affect the winner in any way?
- Does the simulator depend on Godot render frame timing, physics, scene collisions, or animation callbacks?
- Is the separation between simulation and presentation a responsibility boundary rather than permission to skip runtime integration?
- Are stable IDs and deterministic ordering used where required?

## Verification

- Can the approved build/test/scene/export commands be executed from a clean terminal?
- Are production scenes exercised rather than test-only facades?
- Do runtime reports come from production components?
- Are Godot/export logs clean?
- Were any tests, hashes, expected counts, or acceptance criteria weakened during implementation?
- Can an exported Windows build reproduce milestone evidence?

## Scope control

- Did implementation introduce NavMesh, physics combat, ECS, native extensions, custom doctrine scripting, or other deferred complexity without measured need?
- Did the implementation prematurely invent an unresolved economy/faction/turn/replay rule?
- Are content abstractions justified by actual content rather than hypothetical future variants?

## Battle quality

- Does formation-first logic actually avoid per-unit expensive decision loops?
- Does auto-resolve use the complete simulation?
- Can the viewer reproduce movement/events from transcript data without recalculating authoritative decisions?
- Is the result deterministic for repeated seed/definition/version?
- Can a player understand important target, morale, ability, death, and rout outcomes?

## Strategy quality

- Is province adjacency authoritative independently of rendered map polygons?
- Does AI use the same faction/army/doctrine systems as the player?
- Does AI forecasting avoid exact committed-battle foreknowledge?
- Does a province conflict transition cleanly into a committed battle and back into campaign consequences exactly once?

## Maintainability

- Are scene files/resources/source data inspectable in version control?
- Are missing content references detected early?
- Are save/transcript/simulation versions explicit where compatibility matters?
- Is instrumentation sufficient to diagnose a failure without adding another bespoke runner?

A plan or implementation that is sophisticated but cannot answer these questions with concrete evidence should not be preferred over a simpler implementation that can.

---

# 33. External Technical References

These are implementation references only. Project design constraints remain defined by the GDD and this plan.

- Godot 4.7 command-line tutorial: https://docs.godotengine.org/en/4.7/tutorials/editor/command_line_tutorial.html
- Godot 4.7 exporting projects: https://docs.godotengine.org/en/4.7/tutorials/export/exporting_projects.html
- Godot 4.7 C#/.NET overview: https://docs.godotengine.org/en/4.7/tutorials/scripting/c_sharp/
- Godot 4.7 `Sprite3D`: https://docs.godotengine.org/en/4.7/classes/class_sprite3d.html
- Godot 4.7 `SurfaceTool`: https://docs.godotengine.org/en/4.7/classes/class_surfacetool.html
- Godot 4.7 `MultiMesh`: https://docs.godotengine.org/en/4.7/classes/class_multimesh.html
- Godot TSCN text scene format: https://docs.godotengine.org/en/stable/engine_details/file_formats/tscn.html

---

**End of implementation plan.**
