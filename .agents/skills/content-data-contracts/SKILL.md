---
name: content-data-contracts
description: Use when defining content schemas, stable IDs, registries, cross-references, validation, serialization, battle snapshots, versioned formats, or save-facing data. Keeps content source-controlled, diffable, validated early, and deterministic without introducing speculative schema complexity.
compatibility: Agent Skills; project-specific data/content discipline
metadata:
  project-area: data
  project-phase: cross-cutting
---

# Content and Data Contracts

## Purpose

Use this skill when adding or changing authoritative/content-facing data structures. The objective is reliable source-controlled data and stable references, not building a universal data framework.

## Stable IDs

Persistent/content-facing entities use explicit stable IDs.

Never use as primary identity:

- display/localized names;
- texture filenames;
- Godot instance IDs;
- object hash codes;
- list index positions that can change when content is reordered.

Examples that require stable IDs as they are introduced include:

- unit definitions;
- abilities;
- visual records;
- factions/lords;
- terrains;
- provinces;
- commanders/armies where persisted;
- transcript entities/events where cross-referenced.

Use IDs that are easy to diff and validate. Do not create GUID churn unless the project deliberately chooses GUIDs for a specific domain.

## Source-controlled content

Core balance/mechanical content should live in a format selected through M5's content representation spike and remain:

- human-readable/diffable;
- agent-editable;
- strongly validated on load;
- compatible with stable cross-references;
- not dependent on opaque inspector-only state for core mechanics.

The exact chosen representation (JSON, text `.tres`, or another plainly diffable format) remains a documented project decision. Do not pre-empt that design gate inside unrelated work.

## Registry and validation

Use one clear content-loading/registry path rather than every system parsing its own files.

Validation should accumulate all discoverable errors in one pass where practical, including the implemented subset of:

- duplicate IDs;
- missing references;
- invalid ranges;
- invalid/unknown tags;
- missing required presentation resources;
- invalid role/category values;
- circular references if the schema permits them;
- incompatible schema/simulation versions;
- invalid faction/army composition constraints once defined.

Invalid core content should fail before campaign/battle gameplay rather than explode minutes later in a random scene.

## Schema restraint

Add fields because current milestones need them.

Do not add dozens of speculative stats, modifiers, resource types, equipment slots, or inheritance layers merely because future strategy games often have them.

When a field is truly unresolved by a GDD design gate, either:

- leave it out; or
- use a narrow placeholder explicitly called out by the implementation plan when later integration requires one.

Do not turn `object`, free-form dictionaries, or arbitrary metadata blobs into an escape hatch from designing a real schema.

## Mechanical vs visual data

Keep visual records and mechanics explicitly separate.

A unit definition may contain:

- selected visual ID where fixed;
- recommended visual tags;

but the sprite metadata itself must not secretly become the source of gameplay stats.

Universal visual reuse is expected.

## Immutable battle commitment

When battle-relevant mutable content is committed into a battle, later edits must not change that already-committed battle.

Use one intentional strategy appropriate to the implemented version, such as:

- snapshotting required resolved values into `BattleDefinition`; or
- immutable/versioned content references whose exact definitions are guaranteed for replay compatibility.

Do not leave a committed battle reading live mutable campaign/UI/editor objects during resolution.

## Versioning

Version authoritative formats when compatibility actually changes.

At minimum, the project expects deliberate versioning for:

- simulation rules;
- battle transcript schema;
- save schema;
- content schema where required.

A version number is not a substitute for a migration/compatibility policy. Do not increment versions casually on every field addition if no compatibility boundary is crossed.

## Deterministic serialization/digests

Do not assume normal JSON/resource serialization order is a deterministic battle digest.

When a canonical digest is required, feed explicitly ordered primitive values through the dedicated digest contract.

For save/content serialization, prefer stable explicit field shapes and IDs. Preserve deterministic ordering where it matters to behaviour or reproducible diffs.

## Commands and state transitions

UI should request authoritative state changes through explicit domain commands/validated services where the implementation plan calls for them, such as:

- army movement;
- recruitment;
- construction;
- faction choices;
- doctrine changes;
- battle commitment;
- turn commit.

This is for clear validation/state transitions, not permission to build a full CQRS/event-sourcing architecture.

## Godot scene/resource references

Presentation resources may use normal Godot references, but foundational gameplay wiring should fail clearly if required production resources are absent.

Avoid hidden broad scene-tree searches as a data dependency system.

Do not persist Godot Node instance identity as game state.

## Data review checklist

Before accepting a new/changed schema, ask:

- Does every persistent cross-reference use a stable ID?
- Is the source format diffable and consistent with the project's chosen content representation?
- Can invalid references/ranges be detected before gameplay?
- Did we add only fields current milestones need?
- Are visual descriptors still separate from mechanics?
- Can a committed battle change because live content changed afterward?
- Does collection ordering affect deterministic outcomes?
- Is a schema/version change actually required and documented?

## Common mistakes to reject

- unit mechanics inferred from sprite filename;
- display name used as content key;
- Godot node/resource instance IDs written into saves;
- each subsystem creating a private content loader;
- silently skipping malformed records;
- hard-coded constructors remaining the real content source after M5 migration;
- giant generic modifier/property bags added before requirements exist;
- using serializer text as the authoritative deterministic digest.
