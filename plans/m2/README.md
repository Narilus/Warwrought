# M2 — Procedural 2.5D Battlefield & Rendering Scale

- **Status:** Complete; M2.1–M2.3 and the M2 gate are complete
- **Source milestone:** `plans/implementation_plan.md` §11
- **Depends on:** M0 and M1 complete, including the frozen operations contract and accepted M1 authority boundary
- **Completes before:** M3 combat/doctrine breadth and all campaign/faction work

## Purpose

M2 replaces M1's intentionally flat presentation equivalent with Warwrought's first real 2.5D battlefield foundation: seeded low-poly terrain, deterministic X/Z-to-height projection, decorative billboard foliage/placeholder props, a readable battle camera, and measured normal-`Sprite3D` scale through a 500v500 visual stress path. It remains a Godot product milestone, not a standalone terrain generator.

M1's battle is already authoritative and complete. M2 adds no new combat decision. The M1 canonical fixture keeps its committed definition and accepted identities; M2 environments are selected as explicit deterministic battlefield data alongside that fixture rather than silently added to its canonical digest. When later authoritative terrain mechanics require it, they must consume the explicit `BattlefieldDefinition`/sampler data, never a rendered mesh or physics query.

## Task sequence

| Order | Task | Status | Dependency | Purpose |
|---|---|---|---|---|
| 1 | [M2.1 Deterministic battlefield data and height field](M2.1_deterministic-battlefield-data-and-heightfield.md) | **Complete** | M1 accepted | Establish validated seed-driven battlefield data, height/region/deployment queries, and reproducible terrain identity without changing M1 combat. |
| 2 | [M2.2 Production terrain, foliage, and camera integration](M2.2_production-terrain-foliage-and-camera.md) | **Complete** | M2.1 accepted | Make the production BattleLab consume M2 data for low-poly terrain, height-projected billboards, decorative foliage/props, lighting, and camera control. |
| 3 | [M2.3 Scale lab, profiling, and M2 gate](M2.3_scale-lab-profiling-and-m2-gate.md) | **Complete** | M2.2 accepted | Measure normal-node presentation at 100v100, 300v300, and 500v500; prove the real runtime/exported player and retain M1 regressions. |

M2.1 through M2.3 and the M2 gate have passed their required production, regression, visible-runtime, and exported-player evidence. M3 remains unstarted. A Builder may not weaken an activated packet's objective, required behaviour, acceptance criteria, or non-goals.

## Milestone-wide contracts

- Godot 4.7.1 .NET/C# with Forward+ remains the canonical runtime. Use the frozen M0 direct build/test/Godot/export/player contract and the existing transparent verifier; no new runner, verifier, wrapper, or parallel application.
- `BattleDefinition → CommittedBattleResolution → AuthoritativeBattleResolver.Resolve → retained BattleResolution → skip/watch` remains intact. Terrain meshes, camera state, Sprite3D transforms, foliage, render timing, physics, and visual effects cannot feed outcomes back into that path.
- Authoritative horizontal state remains deterministic integer X/Z. The sole visual conversion is `SimPosition(x,z) → BattlefieldHeightSampler → Vector3(x,y,z)`. Never raycast the terrain mesh to find authoritative or visual troop height.
- Battlefield data is deterministic, validated, and source-inspectable. Height/region/deployment data and any future terrain modifiers are explicit; decorative foliage is a seed-derived presentation consumer and creates no hidden collision, obstacle, pathfinding, or movement authority.
- M2 does not add M3 mechanics, province/campaign generation, faction/content systems, saves/replay persistence, authored art dependencies, ECS, MultiMesh, custom rendering, jobs/threads, unsafe code, pooling frameworks, physics combat, or NavMesh/A*.
- Use text-authored resources, procedural meshes/materials, existing placeholder sprites, and simple geometric props. Final textures, foliage art, troop art, effects, and content pipelines are deliberately not prerequisites.

## Proof responsibilities

| Concern | Required proof |
|---|---|
| Battlefield seed, digest, height/region/deployment queries, bounds, and deterministic scatter | Pure deterministic C# tests |
| Mesh construction, scene roots/resources, height-projected troops/remains, foliage/props, lighting, and camera wiring | Actual production Godot `BattleLab` runtime and visible Forward+ inspection |
| 100v100 real-resolution baseline; 100/300/500v500 visual construction/playback metrics | Production `BattleScaleLab`/BattleLab runtime-owned reports plus profiling evidence |
| Windows packaging of the M2 production path | Newly exported Windows player evidence at the M2 gate |
| Existing M1 authority and canonical identity | Existing M1 deterministic tests and direct/exported `battlelab.m1.melee` regression |

## Performance policy

M2 first measures ordinary `Sprite3D`/Node3D presentation with the existing centralized playback owner. It records scene spawn time, measured playback-frame samples, memory, node count, effect count, and progression/completion state separately from headless simulation time. The 500v500 path may use deterministic static/synthetic transcript data to isolate presentation, but the 100v100 baseline must use the real authoritative resolver.

M2 contains no pre-authorized optimization tier. If observed 500v500 evidence shows a median presentation-frame sample above 33.3 ms, a 95th-percentile sample above 50 ms, failure to complete the scripted playback/control observation, memory growth inconsistent with bounded scene lifetime, or another demonstrated normal-node bottleneck, Builder records the raw evidence and returns to Planner. Those are escalation triggers, not permission to introduce MultiMesh, custom rendering, threading, ECS, or a data-oriented rewrite. Any later optimization packet must preserve the same resolution/transcript behaviour and prove an improvement against the recorded baseline.

## M2 gate

M2 passes only when independent review can establish that:

1. multiple fixed seeds reproduce validated terrain data and low-poly mesh topology/appearance;
2. all troop, remains, foliage, and simple props use deterministic battlefield presentation data, with troop/remains Y derived through the tested sampler rather than visual collision;
3. normal camera angles keep billboards, formations, foliage, and terrain readable, while pan/zoom/reset affect presentation only;
4. the production scale path spawns and views 500v500 with recorded metrics and no catastrophic functional degradation;
5. no optimization tier beyond normal nodes and the existing centralized update path was introduced without profiler evidence and explicit planning;
6. canonical M1 input/transcript/result digests and same-resolution skip/watch behaviour remain unchanged;
7. direct Godot runtime and a newly exported Windows player produce clean inspected reports/logs through the existing verification path.

## Explicit deferrals

- Multiple squads/formations, doctrine, target priorities, ranged combat, abilities, commander influence, representative morale, and terrain movement effects: M3 and its design gate.
- Province graph/world generation and province-derived battlefield selection: M4. M2 battlefield profiles are local fixture/presentation inputs, not campaign provinces.
- Content registry, terrain/visual metadata pipeline, faction visuals, generated factions, AI, campaign application, saves, replay persistence, post-battle analysis, authored art production, final weather/biomes, and final optimization/production budgets: later milestones.
