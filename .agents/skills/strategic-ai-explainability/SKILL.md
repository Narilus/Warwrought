---
name: strategic-ai-explainability
description: Use for strategic AI, utility scoring, AI knowledge, army roles, recruitment, battle forecasting, doctrine generation, procedural opponent generation, AI debug tooling, or campaign soak tests. Keeps AI player-equivalent, inspectable, deterministic where intended, and non-cheating.
compatibility: Agent Skills; project-specific strategic AI
metadata:
  project-area: ai
  project-phase: M8-M13
---

# Strategic AI and Explainability

## Design objective

The initial strategic AI should be **valid, active, understandable, and debuggable** before it is brilliant.

Prefer a small transparent utility system over a general-purpose planner or opaque architecture.

AI factions participate through the same campaign, faction, army, doctrine, and battle systems as the player unless an explicit design rule says otherwise.

## Knowledge boundary

Define what the AI is allowed to know before scoring actions.

Distinguish as the game supports them:

- own authoritative state;
- visible/known enemy state;
- public province information;
- hidden information.

Do not read hidden enemy data merely because it exists in process memory.

The AI debug panel may expose ground truth to the developer, but decision code must use the AI knowledge model.

## Utility framework

Represent meaningful candidate actions explicitly.

Each evaluated candidate should be able to expose:

- candidate/action ID;
- eligibility/disqualifying reason;
- component scores;
- final score;
- stable deterministic tie-break;
- selected/not-selected reason.

Example expansion components may include:

```text
+ economic/strategic value
+ landmark value
+ connectivity
+ estimated vulnerability
- travel cost
- forecast casualties
- frontier exposure/risk
```

Exact weights are design/content values, not defined by this skill.

Avoid one unexplained `ScoreProvince()` number that cannot be decomposed when AI behaviour looks wrong.

## Stable decision behaviour

Where randomness is used for variety, it should use explicit deterministic seeded randomness and stable candidate ordering.

Do not rely on:

- dictionary enumeration;
- object allocation order;
- timestamps;
- engine-global RNG;
- unstable floating ties.

Determinism is useful for debugging AI campaign failures even when the final design intentionally allows seeded variation.

## Army roles

Introduce only roles that solve observed strategic needs, such as:

- expansion force;
- field army;
- frontier defence;
- emergency defence;
- raider.

Roles guide recruitment/movement/doctrine preferences. They do not grant hidden combat bonuses.

Do not create an elaborate hierarchy of dozens of strategic agent classes before the campaign demonstrates need.

## Recruitment/composition

AI composition should use the same available faction roster and economic rules as the player.

Inputs may include:

- resource budget;
- army role;
- known enemy composition;
- doctrine preference;
- commander requirements;
- current strategic goals.

Avoid fixed hard-coded armies that bypass player-facing content systems merely to make AI functional.

## Battle forecasting must not cheat

The AI needs a risk estimate before committing combat, but it must not resolve the exact hidden future committed battle and read the answer.

Approved design space includes:

- composition heuristics with matchup terms;
- limited sample simulations using forecast seeds **distinct from the committed battle seed**;
- a hybrid approach.

The forecast should be able to be wrong.

The authoritative committed battle remains surprising within the limits of the model.

If forecast simulations are used, they must not mutate campaign state or consume/advance the committed battle's RNG state.

## Doctrine generation

AI uses the same doctrine/order schema available to the player.

Given faction traits, roster, commander, army role, and known enemy context, it may choose:

- formation;
- target priorities;
- hold/advance behaviour;
- ability policies;
- commander assignment.

Do not create AI-only combat commands unavailable to the player unless the design explicitly approves them.

## Procedural AI factions

Generate coherent concepts, not independent random rolls.

Preferred sequence:

```text
concept/archetype
→ compatible mechanical chassis/packages
→ strengths/liabilities
→ roster priorities
→ doctrine tendencies
→ lord profile
→ visual profile
→ tagged sprite selection
→ validation/repair
```

Bound repair/retry attempts and keep them deterministic for the same faction seed/content version.

Visual coherence uses the faction visual-system skill rather than inferring mechanics from art.

## Explainability is production truth

Debug/UI explanations should read the same reasons/component scores produced by the real AI decision process.

Do not implement a parallel explanatory model that approximates what the AI "probably" considered.

Useful developer output includes:

- current strategic goals;
- army roles;
- candidate actions;
- component scores;
- disqualifications;
- battle forecast and confidence/uncertainty if used;
- recruitment reasoning;
- doctrine choice;
- procedural faction concept/visual profile.

## Campaign soak

Accelerated/headless AI campaigns are valuable once the real strategic loop exists.

Track concrete validity/health indicators such as:

- crashes/errors;
- invalid states;
- armies stuck indefinitely;
- no-resource/deadlock loops;
- repeated nonsensical move loops;
- failure to recruit;
- runaway object/state growth;
- battle count;
- faction survival duration;
- turn progression.

Do not turn soak success into a requirement that the AI always wins or plays optimally. Early AI quality gates are about valid participation and diagnosability.

## Common mistakes to reject

- AI reads every enemy army regardless of visibility rules;
- exact committed battle is secretly run before the AI decides whether to attack;
- AI receives combat stat bonuses because its planner is weak;
- utility score is opaque and cannot expose components;
- tie-breaking depends on collection order;
- AI uses special doctrine commands the player cannot use;
- procedural faction is random mechanics + random sprites with no concept/profile;
- debug explanation recomputes a different score than the decision actually used;
- dozens of strategy-agent classes appear before the basic utility system is proven.
