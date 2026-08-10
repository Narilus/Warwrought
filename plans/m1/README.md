# M1 — Battle Laboratory: Deterministic Melee Vertical Slice

- **Status:** Planned; M1.1 is **READY**
- **Source milestone:** `/plans/implementation_plan.md` §10
- **Depends on:** M0 complete and its frozen verification contract in `/AGENTS.md`
- **Completes before:** M2 procedural battlefield work and all campaign/faction breadth

## Purpose

M1 proves Warwrought's defining battle architecture as one real Godot/C# vertical slice: a committed immutable battle resolves once in a deterministic headless simulator, produces an authoritative result and transcript, and a production `BattleLab` scene plays that transcript without deciding outcomes. The fixture is deliberately narrow—two basic infantry formations and simple fixture morale—but it must visibly advance, contact, damage, die, rout, and reach the already-determined result.

M1 is not a standalone C# battle product. Pure deterministic tests prove rules and repeatability; production `BattleLab.tscn` and an exported Windows player prove the playable Godot integration.

## Task sequence

| Order | Task | Status | Dependency | Purpose |
|---|---|---|---|---|
| 1 | [M1.1 Battle foundations, fixture, and formation model](M1.1_battle-foundations-fixture-and-formation.md) | **READY** | M0 accepted | Establish deterministic primitives, immutable fixture commitment/validation, and one rectangular formation layout. |
| 2 | [M1.2 Authoritative resolution and transcript v1](M1.2_authoritative-resolution-and-transcript.md) | Planned | M1.1 accepted | Resolve the fixture at fixed ticks into a deterministic result, semantic events, and formation keyframes. |
| 3 | [M1.3 Production BattleLab playback](M1.3_production-battlelab-playback.md) | Planned | M1.2 accepted | Deliver the maintained Godot scene, billboard presentation, playback controls, and runtime-owned battle evidence. |
| 4 | [M1.4 Watch/skip equivalence and M1 gate](M1.4_watch-skip-equivalence-and-m1-gate.md) | Planned | M1.3 accepted | Prove one committed result is used by skip and watch, then run the production and exported-player gate. |

The later packets are deliberately detailed now, but only M1.1 is READY. Planner must confirm predecessor acceptance before activating the next packet; Builder must not alter a READY packet's objective, required behaviour, acceptance criteria, or non-goals.

## Milestone-wide constraints

- Preserve Godot 4.7.1 .NET/C# and Forward+ as the one product runtime. Production code remains in `Warwrought.csproj`; `tests/Warwrought.Tests` remains a sibling fast-test project, not an alternate game application.
- Use one authoritative fixed-tick simulator. A committed `BattleDefinition` is immutable; watched playback consumes its result/transcript and cannot recalculate targets, hits, damage, death, morale, routing, or the winner.
- Auto-resolve/skip consumes the same stored authoritative result as watch. No score resolver, visual collision, animation callback, physics, camera, or frame rate may affect the committed outcome.
- Authoritative X/Z state, distances, health, morale, cooldowns, and branch-critical calculations use deterministic integer conventions. Godot float transforms are presentation-only.
- M1 uses one rectangular line formation, fixture-only orders/stats, formation-level contact, and deterministic front-rank pairing. Do not add doctrine UI, multiple formation types, ranged combat, abilities, content registries, campaign state, NavMesh/A*, physics combat, ECS, or performance batching.
- M1's battlefield may be a simple 3D presentation equivalent suitable for `Sprite3D`; procedural low-poly terrain, height fields, foliage, and scale work are M2.
- Reuse M0's frozen toolchain and direct build/test/Godot/export/player workflow. A direct M1 BattleLab scene invocation is a new scenario through that established path, not a new runner. Do not change the frozen M0 block in `AGENTS.md`, create wrappers/polling systems, or add a second verifier.
- Runtime reports are produced by the actual `BattleLab` scene. The existing transparent `scripts/verify-bootstraplab.ps1` may be minimally generalized for the BattleLab scenario while retaining its missing/invalid/failed/unexpected-error semantics; do not replace it with a new verification framework.
- Generated `.godot/`, `bin/`, `obj/`, exports, reports, screenshots, logs, and artifacts remain ignored. Record factual implementation/verification outcomes in `worklog.md` at accepted task boundaries.

## Verification layers

| Layer | Proves | Does not prove |
|---|---|---|
| Pure deterministic tests | RNG, IDs/order, integer position/formation layout, validation, fixed-tick resolution, transcript/digest stability, and skip/watch data equivalence | Godot scene/resource/node wiring or visual playback |
| Direct production `BattleLab.tscn` acceptance | Real C# scene wiring, real transcript consumption, Sprite3D lifecycle, playback controls/event presentation, runtime-owned report and clean Godot log | Windows packaging/player integration |
| Exported Windows `BattleLab` acceptance | The same production battle scenario in the newly exported player and managed runtime | Broader M2+ presentation scale/content |

## M1 gate

M1 passes only after an independent milestone review verifies, in addition to accepted task evidence:

1. fast deterministic tests pass and repeated same definition/seed/version results have the same digest;
2. the fixture resolves headlessly without Godot presentation nodes and substantially faster than its nominal watched 1x duration;
3. direct production `BattleLab.tscn` resolves before playback, consumes the real transcript, and visibly presents formation advance/contact, attack bump, damage flash, deaths/remains, rout, final result, pause/resume, 1x, and accelerated playback;
4. runtime counters agree with the authoritative transcript and logs/reports are clean;
5. skip and watch expose exactly equal authoritative winner, terminal tick, ordered survivors/casualties, routed state, and digest from one committed resolution;
6. the newly exported Windows player runs `battlelab.m1.melee` successfully with the same result/digest and clean evidence; and
7. no facade, second resolver, render/physics authority, unapproved infrastructure, or later-milestone system was introduced.

Use Reviewer at this M1 gate, not after each task. Do not begin M2 until M1 is independently accepted.

## Explicit deferrals

- Procedural low-poly terrain, height projection, foliage, battle camera scale, and 500v500 work: M2.
- Doctrine surface, multiple squads/formations, ranged attacks, abilities, commander influence, target-priority breadth, and representative morale: M3 / DG-D.
- Campaign, faction, content registry, visual-tag system, saves, AI, and campaign-to-battle transitions: M4 onward.
- Final battle tick rate, final morale formula, final formations, balance/content, replay retention, and arbitrary timeline scrubbing remain unresolved or later work. M1's starting tick rate and fixture morale values are bounded implementation choices, not permanent design canon.
