---
name: faction-visual-system
description: Use for sprite metadata, visual tags, faction visual profiles, lord/unit sprite selection, universal creature visuals, remains categories, AI visual coherence, or visual search/recommendation. Keeps mechanics and presentation deliberately loosely coupled.
compatibility: Agent Skills; project-specific faction visual/content system
metadata:
  project-area: factions
  project-phase: M5-M13
---

# Faction Visual System

## Foundational rule

Faction/unit mechanics and sprite appearance are **loosely coupled**.

A sprite is primarily a battlefield representation. Do not infer mechanical rules from its art unless an explicit content definition separately says so.

Examples that are intentionally allowed:

- a caster lord using a heavily armoured warrior sprite;
- visually skeletal troops that are not mechanically `Undead`;
- lightly dressed sprites representing mechanically durable troops;
- the same wyvern visual used by many factions with different mechanics.

## Do not build modular body-part assembly

The project uses preset sprites and/or themed sprite libraries.

Do not introduce:

- body-part attachment systems;
- equipment-layer combinatorics;
- skeletal sprite rigs;
- procedural weapon fitting;
- rules that force every mechanic to alter the sprite.

A faction may choose a themed pack or individual preset sprites depending on later UI decisions.

## Visual metadata

Each visual record should use a stable visual ID and may contain the implemented subset of:

- texture/atlas reference;
- billboard settings;
- scale/size;
- ground/pivot offset;
- visual tags;
- recommended roles;
- ownership tint/palette support;
- remains category;
- optional portrait pairing;
- optional pack/theme ID.

Do not use texture filenames as stable gameplay identities.

## Tag semantics

Visual tags are **descriptors**, not mechanics.

Useful categories may include:

```text
humanoid
armoured
shielded
robed
martial
mystical
bestial
furred
skeletal
undead-looking
regal
primitive
mechanical
mounted
flying
large
commander
caster
ranged
```

The exact controlled vocabulary should live in content/schema validation once M5 defines it. Do not let near-duplicate free-text tags proliferate silently.

### Tags may support

- player filtering/search;
- player recommendations;
- AI visual selection;
- broad battlefield role readability;
- remains-category recommendation;
- faction pack/theme coherence.

### Tags may not silently grant

- armour;
- flight;
- undead state;
- spellcasting;
- weapon stats;
- size mechanics;
- any other gameplay property.

Mechanical definitions may *recommend* visual tags, but the relationship remains explicit and one-way.

## Player selection

Player freedom has priority over visual recommendation.

A recommendation system may rank sprites that fit the mechanical role, but the player may choose a mismatched visual unless a later explicit design decision restricts a specific category for technical/readability reasons.

For lords in particular, visual choice is representational and intentionally independent from the build.

## AI visual profiles

Procedural AI factions need stronger coherence than unrestricted player choice.

Use a profile conceptually containing:

```text
primary tags
secondary tags
avoid tags
ownership palette/base identity
optional preferred pack/theme IDs
```

Selection should also consider the unit's broad role so generated opponents remain readable.

Conceptual deterministic scoring:

```text
+ primary-tag matches
+ secondary-tag matches
+ role compatibility
+ preferred pack/theme coherence
- avoid-tag matches
- repeated visual penalties where variety is desirable
= candidate score
```

Do not lock arbitrary numeric weights in this skill. Keep weights inspectable/configurable where practical and deterministic for a given faction seed/content version.

Use stable tie-breaking so identical input does not select different visuals due to collection order.

## Procedural faction generation relationship

Visual selection comes **after** a coherent faction concept/mechanical profile exists.

Preferred sequence:

```text
concept/archetype
→ mechanics/roster/doctrine tendencies
→ visual profile
→ role-aware tagged sprite selection
→ validation
```

Do not generate a faction by choosing random pretty sprites first and inventing mechanics from them.

## Universal creatures

Some world-level entities deliberately reuse the same visual regardless of owner, including examples such as:

- wyverns;
- giant spiders;
- common elementals;
- generic constructs;
- summoned beasts;
- mercenaries.

Ownership/readability can come from:

- base/ring;
- health-bar treatment;
- palette/tint accent;
- banner/icon;
- selection outline.

Colour swapping is optional and lightweight. Do not make universal-creature support depend on an elaborate recolouring pipeline.

## Readability

Sprites need to communicate broad battlefield identity more than exact stats.

Prioritize:

- silhouette;
- size/footprint;
- mounted/flying/large distinction where visually useful;
- caster/ranged/martial suggestions where available;
- ownership markers;
- selected/commander visibility.

Detailed mechanics belong in UI/inspection rather than forcing the art to encode every trait.

## Remains

Visual metadata may map to broad remains categories, for example:

- humanoid/body;
- bone pile;
- beast carcass;
- construct wreck;
- magical residue.

The remains choice is presentation only. It never decides whether a unit died.

## Content Browser expectations

When M5 tooling exists, it should make this system auditable:

- list visual records;
- filter by controlled tags;
- preview Sprite3D representation;
- inspect pivot/scale/remains category;
- show recommendation inputs and score;
- surface invalid tags/references clearly.

Do not hide recommendation logic in opaque UI-only code.

## Common mistakes to reject

- `if sprite has "flying" tag then unit can fly`;
- mechanics stored in filenames/texture paths;
- player denied a sprite solely because recommendation score is low;
- AI choosing each sprite independently with no faction visual profile;
- bespoke wyvern art required for every faction;
- attachment-point/customizable sprite engine added without design approval;
- nondeterministic visual selection from unordered candidate collections.
