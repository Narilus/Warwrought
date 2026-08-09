---
name: godot-2p5d-presentation
description: Use for battlefield rendering, procedural low-poly terrain, Sprite3D troop/foliage billboards, height projection, camera, remains, VFX, or presentation scaling. Encodes the project's lightweight 2.5D visual language without letting rendering become authoritative simulation.
compatibility: Agent Skills; Godot 4.7 .NET/C# presentation
metadata:
  project-area: presentation
  project-phase: M1-M13
---

# Godot 2.5D Presentation

## Visual target

The battle scene is a stylized simulation tableau:

- deterministic low-poly 3D terrain;
- billboarded 2D troop sprites;
- billboarded 2D foliage;
- simple 3D props/landmarks where useful;
- restrained lighting/fog/weather/VFX;
- intentionally minimal unit animation.

Do not turn this into a conventional fully animated 3D RTS.

## Authority boundary

Presentation receives authoritative battle/battlefield data.

It does **not** determine battle outcomes.

The baseline coordinate relationship is:

```text
simulation X/Z
    + battlefield height sample
    ↓
Godot Vector3(X, Y, Z)
```

Visual interpolation may use floating-point Godot transforms. Authoritative movement remains in deterministic simulation coordinates.

## Procedural terrain baseline

Prefer a deterministic battlefield definition/height field that can be consumed both by simulation queries and presentation.

A normal low-poly mesh path is:

1. consume/generate deterministic height samples;
2. create a coarse regular/controlled vertex grid;
3. triangulate consistently;
4. assign vertex color/UV/material data as required;
5. create faceted or appropriately generated normals;
6. commit to an `ArrayMesh`;
7. present through a normal production scene node;
8. preserve a deterministic `HeightAt(x,z)`/terrain query independent of rendered mesh collision.

Godot `SurfaceTool` is an appropriate baseline API for scripted geometry. In Godot 4.7 C# it provides methods such as `Begin`, `SetColor`, `SetUV`, `AddVertex`, `GenerateNormals`, and `Commit`.

Important `SurfaceTool` rules:

- vertex attributes used by the surface format must be established before vertices that depend on them;
- Godot uses clockwise front-face winding for triangle primitive modes;
- flat/faceted normals can be created deliberately; current C# bindings require care around smooth-group values (`uint.MaxValue` is the documented C# value corresponding to the flat `-1` convention).

Do not add terrain collision merely to query height if the deterministic height field already answers the question.

## Sprite3D baseline

`Sprite3D` is the normal first implementation for troops and much foliage. It displays a 2D texture in a 3D world; billboard behaviour is defined on `SpriteBase3D`.

Useful presentation data per unit includes:

- visual ID / texture or atlas region;
- world transform;
- pixel size/scale;
- ground/pivot offset;
- billboard mode;
- ownership marker/tint where supported;
- selected/highlight state;
- transient damage flash;
- alive/dead/remains state.

Do not infer mechanics from the selected sprite.

### Billboard caution

If exact Godot property/enum names are needed, verify the current 4.7 C# binding rather than translating GDScript from memory. The current API exposes SpriteBase3D billboard configuration and Sprite3D texture/frame fields in C# idioms.

Billboard shadow behaviour can be camera-sensitive. Do not assume hundreds of billboard sprites need dynamic shadow casting. Profile and choose the simplest readable solution.

## Minimal combat language

Ordinary melee presentation:

```text
formation contact
→ authoritative ATTACK/DAMAGE event
→ attacker short bump/translation
→ target red flash
→ attacker returns visually
```

Death presentation:

```text
UNIT_KILLED
→ rotate/fall sprite
→ red flash/fade as desired
→ replace/hide with remains visual
```

Ranged presentation:

```text
authoritative projectile/attack event
→ optional visual projectile/tracer between known points
→ authoritative impact event
→ flash/effect
```

The visual projectile has no authority and does not need physics collision.

Major abilities may use stronger particles/lighting/ground effects. Routine attacks should stay visually quiet enough that hundreds of units remain readable.

## Formation readability

Avoid making contact look like random sprite vibration.

Prefer:

- only front/contact ranks perform routine attack bumps;
- stagger visual bumps according to authoritative attack events;
- preserve relatively stable rear ranks;
- visually show formation displacement/pressure through anchor motion;
- allow casualty gaps/reformation to be legible;
- routed formations visibly lose ordered structure.

## Foliage

Foliage is primarily presentation.

A forest may use many billboard trees/bushes while the simulator works with deterministic terrain regions/density/modifiers rather than exact decorative-tree collisions.

Use deterministic scatter seeds derived from battlefield/province identity so the visual battlefield is reproducible where required.

Do not make every tree an obstacle unless a specific design requirement demands it.

## Camera

The camera should support understanding the simulation:

- pan/zoom/rotate as approved by the task;
- inspect squads/units without changing outcomes;
- pause and speed controls remain playback operations;
- preserve billboard readability across intended angles;
- avoid camera-dependent rules in simulation.

## Performance escalation

Start simple and profile before escalating.

Preferred escalation ladder:

1. ordinary production `Sprite3D`/Node3D views with centralized update ownership;
2. reduce per-node scripts, scene-tree work, materials, sorting churn, and unnecessary effects;
3. pool/reuse transient effects and repeated visual objects where measurement supports it;
4. batch/instance suitable repeated visuals using Godot `MultiMesh`/related approaches when normal nodes are a measured bottleneck;
5. only then consider custom rendering techniques.

Godot 4.7 `MultiMesh` provides GPU instancing for large numbers of the same mesh and can carry per-instance transforms/colors/custom data. It is an optimization tool, not the required baseline architecture.

Do not pre-emptively rebuild the battlefield around MultiMesh merely because the final game may contain hundreds of units.

## Remains

Remains can be generic by broad visual/body category rather than bespoke for every unit:

- humanoid/body;
- bone pile;
- beast carcass;
- construct wreck;
- magical/ethereal residue.

A sprite's metadata may recommend a remains category. The remains asset is visual and must not alter authoritative casualty state.

## Common mistakes to reject

- adding NavMesh agents because the terrain is 3D;
- using terrain physics collision as the authoritative movement model;
- using sprite overlap to detect melee contact;
- creating skeletal rigs/attack animation graphs for ordinary troops;
- making projectiles physics-authoritative;
- giving every troop an independent `_Process` AI script;
- optimizing into instancing/custom shaders before a measured bottleneck;
- generating visual terrain independently from the authoritative battlefield data so terrain effects and visuals disagree.

## Official reference anchors

- Sprite3D: https://docs.godotengine.org/en/4.7/classes/class_sprite3d.html
- SpriteBase3D: https://docs.godotengine.org/en/4.7/classes/class_spritebase3d.html
- SurfaceTool: https://docs.godotengine.org/en/4.7/classes/class_surfacetool.html
- ArrayMesh: https://docs.godotengine.org/en/4.7/classes/class_arraymesh.html
- MultiMesh: https://docs.godotengine.org/en/4.7/classes/class_multimesh.html
