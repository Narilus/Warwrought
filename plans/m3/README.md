# M3 — Battle System Expansion & Doctrine

- **Status:** Active for planning; only M3.1 is **READY**. M3 implementation has not started.
- **Depends on:** Completed M0–M2, including M2's accepted direct/visible/exported-player evidence at `1bc56e6950aa65596cba29c10c03daf927f7213b`.
- **Completes before:** M4 campaign/province work.
- **Authority:** `DESIGN.md` §§11–12; `plans/implementation_plan.md` §§8 and 12 (DG-D, M3.1–M3.12); `AGENTS.md`; `deterministic-battle-authority`, `godot-2p5d-presentation`, and `godot-project-operations` skills.

## Purpose

M3 turns the completed M1 duel into a representative deterministic auto-battle: multiple formations, differentiated roles, doctrine-driven targeting and movement, ranged attacks, a small authoritative ability set, commander influence, representative morale, and inspectable decisions. It does not begin campaign systems, final content, final art direction, or a rendering rewrite.

The existing authoritative path remains the only combat authority:

```text
committed BattleDefinition -> AuthoritativeBattleResolver -> retained BattleResolution
                                                     |                 |
                                                     v                 v
                                                skip result       BattleLab playback
```

Godot terrain, camera, Sprite3D views, effects, and playback only present the retained outcome. M2's ordinary centralized Node3D/Sprite3D baseline remains selected; M3 does not pre-authorize MultiMesh, ECS, pooling, jobs/threads, unsafe code, custom rendering, NavMesh/pathfinding, physics-authoritative projectiles, an event bus, DI, or an arbitrary ability scripting language.

## Current-code audit

| Area | Current reality | M3 consequence |
| --- | --- | --- |
| Committed model | `BattleDefinition` already has ordered sides, ordered squads, rectangular formations, per-squad advance orders, stable unit IDs, and a canonical digest. | Retain these immutable boundaries; relax the M1-only one-squad validation in M3.1 without changing the canonical M1 input/result fixture. |
| Resolver | It builds a mutable formation state for every committed squad and already keeps morale, members, anchors, and retreat destination per formation. But a global `_contactActive`, `_formations[0]/[1]`, two-formation melee pairing, opposing-axis calculation, and terminal result logic make it a duel. | M3.1 must replace only those duel assumptions with stable, formation-pair-local state and side aggregation. |
| Orders/targets | The only committed order is `AdvanceOrder`; there is no stance, target policy, target state, role metadata, commander relation, ranged attack, or ability definition. | M3.1 uses a documented fixture-only pairing/advance rule. DG-D is required before user-facing doctrine vocabulary or policy scoring. |
| Transcript | Semantic events and keyframes already carry side, squad, other squad, unit IDs, reason codes, formation state, morale, and arbitrary keyframe member lists. It lacks doctrine/target-choice/ability/commander event families. | M3.1 can retain pair identity through current contact fields. Later packets extend explicit semantic records rather than inferring decisions in playback. |
| Playback | Member views are created from transcript keyframes and already hold squad IDs; event dispatch and routing are squad-aware. Startup incorrectly requires exactly two tick-zero keyframes. | M3.1 removes the two-keyframe assumption; playback remains transcript-only and centralized. |
| Runtime/reporting | `BattleLab` and its report read each side's first squad for unit counts; ScaleLab's 300v300/500v500 records are explicitly synthetic presentation stress data. | M3.1 aggregates real formations for a new production scenario. Synthetic ScaleLab data cannot prove M3 combat authority. |
| Morale/command | Casualty loss affects only the target formation, but the M1 formula is a fixture constant. `BattleResult.CommanderStatuses` is intentionally empty. | Formation-local isolation can be preserved in M3.1. Representative morale and commanders wait for a separate design decision. |

## Execution route

| Packet | Status | Dependency | Scope |
| --- | --- | --- | --- |
| [M3.1 Multi-formation authoritative foundation](M3.1_multi-formation-authoritative-foundation.md) | **READY** | M2 complete | Three formations per side, independent anchors/contact/targets/morale, retained resolver/transcript/playback compatibility. |
| M3.2 Doctrine, targeting, and formation-v1 behaviour | Planned — **blocked by DG-D** | M3.1 + DG-D | Supported stances, target priorities, hold/advance/limited chase semantics, required formation variants, explainable stable candidate scoring. Add a spatial index only if M3.1 or later representative evidence shows a concrete query bottleneck. |
| M3.3 Authoritative ranged combat | Planned | M3.2 | One ranged role and authoritative range/cooldown/outcome plus semantic projectile event; presentation-only tracer/impact, never collision physics. |
| M3.4 Abilities, commanders, and representative morale | Planned — **blocked by DG-D and Morale v1 decision** | M3.3 + decisions | Explicit supported effects, deterministic ability policies, commander relationship/death event, and reason-coded morale refinement. |
| M3.5 Inspection, fixture matrix, and M3 gate | Planned | M3.2–M3.4 | Selection/explainability surface, representative fixtures, real authoritative 300v300 proof, direct/visible/exported-player gate. |

This route deliberately groups the implementation-plan subsections into coherent reviewable slices rather than creating one packet per numbered heading.

## DG-D — Battle doctrine v1 surface

DG-D is unresolved. It must be decided before M3.2 (and therefore before the doctrine-dependent ranged, ability-policy, commander, morale, inspection, and final-gate slices). M3.1 is safe before DG-D because it uses neither a public doctrine UI nor a selectable policy vocabulary.

The minimum user decisions are:

1. Which army stances are supported in v1, and whether they constrain squad orders.
2. Which squad target priorities are actually selectable in v1.
3. The exact semantics and limits of hold, advance, engage, and chase.
4. The v1 formation variants and their intended gameplay/footprint differences.
5. The small explicit set of ability-policy conditions available to doctrine rules.

M3.1 may use only a fixture-declared provisional advance/pairing relationship: it is not a selectable target-priority, formation, stance, or ability-policy decision and must be clearly identified as temporary fixture data.

## Morale v1 design decision

M1's casualty-only morale loss and fixed rout threshold are protected fixture behaviour, not an approved representative morale model. A separate Morale v1 decision is required before M3.4; it does not block M3.1. It must specify the supported inputs (for example casualty rate/tempo, commander loss, nearby rout, pressure, ability effects), recovery/persistence policy, rout/retreat thresholds, and the explainable reason-code vocabulary. Do not introduce a final formula or balance values before that decision.

## Spatial-query decision

No spatial grid is READY now. At M3.1's six formations, a stable ordered list and the fixture's three declared opposing pairs are both simpler and inspectable. M3.2 may introduce a narrow deterministic grid/hash only if target-selection/ranged queries or representative 300v300 authoritative evidence demonstrates an actual all-vs-all query cost; its bucket assignment and query order must then be deterministic. General navigation and NavMesh remain out of scope.

## M3 gate

M3 is complete only when at least three squads per side execute distinct approved doctrine; target priorities visibly alter behaviour; ranged attacks and one major ability are authoritative and correctly presented; morale/routing forms understandable collapse; selected squads explain their choices; repeat identities are stable; a real authoritative 300v300 battle resolves headlessly substantially faster than playback; and direct production, visible Forward+, and exported-player evidence are clean. The M2 synthetic ScaleLab scenarios cannot substitute for that authoritative 300v300 proof.

## Protected foundations

- M1 canonical input/transcript/result digests remain `65ff279dac01cc31305336485af41cb595c37137edd25e04753aa5c62ec84787`, `75dc2d6f0dda9fc71c364a5c265f1c640ce7bc949435f7522dc08896272faf58`, and `7faa2ec40c0830f317f4c87296c2e54c27d6f43def9b18f92d113834e982da1c`; retain `SideAWin`, tick `576`, survivors `100`, casualties `28`, retreated `49`, `resolutionIdentityShared=true`, and `skipWatchEquivalent=true`.
- M2.1 battlefield digests remain meadow `567f72c3d9e754722839fb8f1fb034fea7d9f647bc66690a36416ed0e7dc5443` and highland `5973015037208b1d09e4d50be3e0353b3bf200dd06fd1a1ccdf77ec4f691326f`; M2.2 scatter digests remain meadow `adda947b93cd28b4d1170138d5538b419dc4237efcc70f6f19b8baa516c863e6` and highland `9a12bd67baf35b084e0fc6c0ee8fa22c6bc9e40ea8706f359d7d239fb3d83d83`.
- The accepted M2 presentation baseline remains production low-poly terrain, sampler projection, authored foliage, FixedY troop/foliage/remains billboards, full-facing hit effects, and the orthographic camera at `Vector3(42, 30, 26)`, `Vector3(-36, 58, 0)`, size `60`.

