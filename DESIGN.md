# Game Design Document — Procedural Faction Strategy / Auto-Battle Wargame

**Working title:** TBD  
**Document status:** Foundation GDD — standalone agent reference  
**Date:** 9 August 2026  
**Primary engine:** Godot 4.7.1 .NET  
**Primary language:** C#  
**Primary target:** Desktop PC, Windows first unless revised  

---

## 1. Purpose of This Document

This document defines the current agreed vision, constraints, architecture, presentation model, and foundational design decisions for a strategy game inspired in broad structure by games such as *Dominions*, while remaining its own project.

It is written to be understandable without access to prior conversations. An implementation or planning agent should treat the items marked **LOCKED** as project constraints rather than suggestions. Items marked **TBD** are intentionally unresolved and must not be silently invented or treated as established canon.

The project is intended to be a single-player, turn-based strategy game built around:

- creating a custom faction and custom lord/god/leader;
- expanding across a procedurally generated province map;
- recruiting units, constructing infrastructure, creating armies, and assigning commanders;
- defining army, squad, unit-role, and hero combat behaviour before battle;
- resolving contested provinces through deterministic automated battles;
- optionally watching those battles play out as 2.5D visual simulations;
- iterating on faction design, army composition, doctrine, and strategic decisions based on the observed results.

The game is **not** intended to become a conventional real-time strategy game or a manually controlled tactical battle game.

---

# 2. High-Level Vision

## 2.1 Player Fantasy

The core fantasy is:

> **Design a faction, build an army, program how it should fight, send it into a living strategic world, and watch the consequences of those decisions unfold.**

The player is primarily a **designer, ruler, planner, and commander before contact**, rather than a click-intensive battlefield operator.

The enjoyment should come from:

- constructing coherent or unusual factions;
- finding mechanical synergies between race traits, faction doctrine, leaders, units, and abilities;
- building armies for specific strategic purposes;
- deciding where and when to fight;
- setting battlefield doctrine and target priorities;
- watching an automated simulation validate or punish those assumptions;
- inspecting why a battle was won or lost;
- revising the faction, army, or doctrine for future engagements.

The game should feel systemic and replayable rather than authored around a single fixed campaign.

---

## 2.2 Core Design Pillars

### Pillar A — Creation Before Execution

The player should make meaningful decisions before battle rather than micromanaging individual soldiers during battle.

The most important actions happen in:

- faction creation;
- leader creation;
- recruitment and army composition;
- formation assignment;
- squad behaviour;
- ability policy;
- target priority;
- strategic positioning.

Once combat begins, the player watches the plan execute.

### Pillar B — One Real Battle Simulation

There is no simplified score-based auto-resolve that can disagree with the visible battle.

**LOCKED:** every battle is determined by the same authoritative deterministic simulation. “Auto-resolve” means **skip the visual playback**, not “use a different combat formula.”

### Pillar C — Readable Simulation, Not Animation Spectacle

Battles may contain hundreds of units. Individual soldiers do not need conventional character animation.

The presentation should communicate state clearly with minimal visual language:

- movement;
- formation contact;
- small attack bumps;
- damage flashes;
- projectiles/effects where needed;
- rotation/fall on death;
- remains or wreckage replacement;
- morale collapse and routing;
- strong visual treatment for major abilities and exceptional units.

### Pillar D — Mechanics and Visuals Are Loosely Coupled

Faction mechanics must not require a bespoke modular sprite-generation system.

The player may build a heavily magical lord and choose a heavily armoured warrior sprite. A faction may use a visual pack that does not literally encode every mechanical trait.

Visuals represent the faction; they do not mechanically define it.

### Pillar E — Procedural Strategic Replayability

The campaign map, factions, province composition, landmarks, and strategic circumstances should support many different runs rather than a fixed sequence of authored levels.

### Pillar F — Explainable Outcomes

Because the player cannot take direct control once battle begins, the game must make battle outcomes understandable.

The player should be able to answer questions such as:

- Why did this line collapse?
- Why did this unit select that target?
- Why did the cavalry fail to reach the back line?
- Why did a caster fail to use an ability?
- What killed the commander?
- When did morale break?
- Which part of the doctrine worked or failed?

---

# 3. Scope and Explicit Non-Goals

## 3.1 Foundational Scope

The foundation should support:

- custom faction creation;
- custom lord/god/leader creation;
- procedural 2D province campaign maps;
- province ownership and adjacency;
- landmarks and province bonuses;
- recruitment and structures;
- army creation;
- commander assignment;
- pre-battle behaviour and doctrine;
- deterministic automated combat;
- auto-resolve through the full simulation;
- 2.5D watched battle playback;
- strategic AI factions;
- generated AI faction visuals using sprite metadata/tags;
- hundreds of units in combat;
- replayable combat data and post-battle analysis.

## 3.2 Explicit Non-Goals for the Foundation

Unless this document is later revised, do **not** assume the project requires:

- direct real-time unit control during battles;
- Total War-style tactical micromanagement;
- per-soldier NavMesh pathfinding;
- rigidbody-driven combat;
- realistic ballistic physics;
- skeletal character animation;
- fluid weapon swing animations;
- body-part modular sprite construction;
- armour/weapon attachment-point systems;
- bespoke art for every faction/unit combination;
- multiplayer or networking;
- AAA cinematic battle presentation;
- an attempt to match decades of *Dominions* content at initial release.

The project succeeds by having strong interacting systems, not by matching the content volume of a long-running commercial series.

---

# 4. Core Game Loop

The baseline campaign loop is:

1. **Create or select a faction.**
2. **Create the primary lord/god/leader.**
3. **Generate a campaign world.**
4. **Begin from an owned capital/start province.**
5. **Collect resources and evaluate nearby provinces.**
6. **Recruit units and leaders.**
7. **Construct buildings/infrastructure.**
8. **Create armies and assign commanders.**
9. **Configure formations and battle behaviour.**
10. **Move armies between adjacent provinces.**
11. **Capture neutral provinces or contest hostile provinces.**
12. **When armies clash, commit a BattleDefinition.**
13. **Resolve the complete battle headlessly using the authoritative simulator.**
14. The player chooses to:
    - **Watch Battle** — play back the already-resolved battle in the battle viewer; or
    - **Skip to Result / Auto-Resolve** — immediately apply the same simulation result.
15. **Apply casualties, retreats, ownership changes, and other consequences.**
16. **Use the result to adjust army design, faction development, doctrine, or strategic plans.**
17. Repeat until the campaign victory or defeat condition is reached.

### Baseline victory concept

The current baseline is conquest: defeat all opposing factions / remove their ability to continue resistance. Exact rules such as capital capture, throne-style victory points, elimination conditions, or alternate victories are **TBD**.

---

# 5. Campaign World

## 5.1 World Presentation

**LOCKED:** the strategic world should be a procedurally generated **2D province map** rather than a continuously navigated 3D overworld.

The presentation may be attractive and map-like, but mechanically the world is fundamentally a graph:

```text
Province A <-> Province B <-> Province C
       \          |
        \         |
         -> Province D
```

A province contains data such as:

```text
Province
- ID
- adjacency / neighbours
- owner
- terrain/biome
- economic output
- recruitment or manpower properties
- structures
- landmarks
- armies present
- strategic modifiers
- battle-generation seed/data
```

This keeps strategic movement deterministic and simple. Armies move between adjacent provinces rather than continuously navigating terrain.

---

## 5.2 Procedural Generation

The world generator should create:

- province topology;
- adjacency;
- terrain/biomes;
- starting regions;
- landmarks;
- resource/economic variation;
- strategically valuable regions;
- sensible expansion routes;
- AI and player starting positions.

The map should be validated after generation for basic playability, including connectivity and reasonable start conditions.

A Voronoi-style visual representation is a natural option, but the exact algorithm is not locked.

### Important principle

The visual polygon map is a presentation of the province graph. Strategic game rules should not depend on fragile screen-space geometry.

---

## 5.3 Province Identity

Provinces should matter for more than ownership count.

Potential province properties include:

- terrain type;
- population/manpower;
- income/resources;
- recruitment modifiers;
- magical or supernatural properties;
- landmarks;
- defensive value;
- infrastructure capacity;
- special local units;
- battle terrain characteristics.

The exact economy and resource set are **TBD**.

---

## 5.4 Structures and Development

The player can build structures in owned territory.

Potential roles include:

- recruitment;
- income;
- defence;
- magical/research functions;
- strategic mobility;
- resource extraction;
- special unit access.

Exact structure slots, construction time, costs, and tech prerequisites are **TBD**.

---

## 5.5 Turn Structure

The campaign is turn-based.

**TBD:** whether faction orders resolve sequentially, simultaneously, or through a hybrid phase structure.

Agents must not silently assume *Dominions*-style simultaneous turns unless explicitly approved later.

---

# 6. Faction Creation

## 6.1 Philosophy

Faction creation should be expressive but controlled enough that combinations remain understandable and balanceable.

The player builds the faction mechanically through traits, presets, packages, and choices rather than by modifying visual body parts.

Potential mechanical dimensions include:

- species/race chassis;
- culture or doctrine;
- national strengths;
- national liabilities;
- magical/supernatural affinities;
- economy modifiers;
- recruitment characteristics;
- strategic movement traits;
- morale/discipline tendencies;
- access to unusual unit types;
- sacred/elite mechanics;
- environmental preferences.

The exact taxonomy and point-buy rules are **TBD**, but the system should favour coherent packages and meaningful trade-offs over hundreds of unrestricted numerical sliders.

---

## 6.2 Mechanical and Visual Independence

**LOCKED:** visual appearance and faction mechanics are separate systems.

A player may create a faction with one mechanical identity and intentionally choose visuals that imply another.

Examples:

- a heavily armoured-looking faction may mechanically be fragile but supernatural;
- skeletal-looking units do not have to be mechanically undead;
- a robed lord sprite may represent a melee demigod;
- an armoured lord sprite may represent a pure caster.

The UI may recommend visually appropriate choices but must not hard-lock them without a very strong reason.

---

# 7. Visual Faction System

## 7.1 Preset Sprite Library

**LOCKED:** do not build a modular body/armour/weapon attachment-point character generator.

Instead, create a broad library of complete preset unit sprites and themed sprite packs.

A visual pack might contain multiple options for roles such as:

- light infantry;
- line infantry;
- heavy infantry;
- ranged units;
- cavalry;
- casters;
- commanders;
- elites;
- support/siege units.

The player may choose a whole themed pack or select individual sprites, depending on final UI design.

---

## 7.2 Universal Creature Sprites

Some entities should deliberately have shared visuals regardless of the faction using them.

Examples:

- wyverns;
- giant spiders;
- common elementals;
- generic constructs;
- summoned beasts;
- mercenaries;
- other world-level creatures.

A wyvern does not need a unique sprite for every faction.

Faction ownership can be communicated through:

- palette accents;
- base/ring colours;
- banners;
- selection outlines;
- health-bar treatment;
- optional tinting.

Colour swapping is useful but should remain a lightweight enhancement rather than a mandatory complex recolouring pipeline.

---

## 7.3 Sprite Metadata and Tags

**LOCKED:** sprites should carry descriptive metadata/tags.

Example tags:

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

These tags are primarily **visual descriptors**, not mechanical rules.

They serve several purposes:

1. **Player recommendations** — recommend sprites that visually match a unit/lord build.
2. **AI faction generation** — allow procedural factions to select coherent visual sets.
3. **Readability** — help generated opponents visually communicate broad battlefield roles.
4. **Search/filtering** — allow the faction creator to filter large sprite libraries.
5. **Remains selection** — optionally map broad body/material tags to appropriate remains categories.

### AI visual profile example

```text
Primary visual tags:
- bestial
- furred
- primitive

Secondary:
- armoured
- shamanic

Avoid:
- mechanical
- regal
- skeletal
```

The AI then selects appropriate sprites by role while allowing occasional intentional exceptions.

---

# 8. Lord / God / Leader Creation

The player creates a primary custom leader mechanically and selects its presentation independently.

Potential mechanical properties include:

- chassis/archetype;
- physical attributes;
- magical affinities;
- leadership;
- strategic abilities;
- personal combat abilities;
- traits;
- equipment or innate powers;
- faction-wide effects.

Visual selections may include:

- battlefield sprite;
- portrait;
- name;
- title;
- faction colour accent;
- optional sound/voice identity later.

**LOCKED:** a lord sprite is representational. It does not have to literally match the chosen mechanics.

---

# 9. Units and Army Composition

## 9.1 Unit Roles

The system should be capable of representing broad roles such as:

- light infantry;
- heavy infantry;
- ranged infantry;
- cavalry;
- skirmishers;
- monsters;
- flying units;
- casters;
- support units;
- commanders/heroes;
- summons.

These are behavioural/compositional categories, not necessarily hard classes.

---

## 9.2 Armies and Commanders

Armies consist of:

- one or more commanders/leaders;
- squads/formations;
- units assigned to those squads;
- doctrine/behaviour configuration;
- deployment configuration where applicable.

Command structure should matter enough to create strategic and tactical choices without becoming an administrative simulator.

The exact commander capacity and leadership statistics are **TBD**.

---

# 10. Pre-Battle Doctrine and Behaviour

This is one of the project’s potential signature systems.

The player should be able to define combat intent before the battle is committed.

The system should be expressive but constrained and deterministic. Avoid unrestricted scripting languages unless a future design explicitly calls for them.

---

## 10.1 Army-Level Doctrine

Possible examples:

- hold ground;
- advance steadily;
- aggressive advance;
- defensive posture;
- preserve reserves;
- withdraw under defined conditions;
- protect command assets;
- prioritize battlefield control.

---

## 10.2 Squad-Level Orders

Possible configuration:

- deployment region;
- formation type;
- formation width/depth;
- advance/hold behaviour;
- target priorities;
- preferred engagement range;
- chase policy;
- retreat behaviour;
- screening/guard role.

Example target priorities:

- closest enemy;
- enemy ranged units;
- enemy cavalry;
- enemy commander;
- weakest valid formation;
- largest threat;
- summoned units;
- monsters;
- wounded targets.

---

## 10.3 Ability Policies

Casters and heroes may receive rules such as:

```text
1. If threatened by 3+ enemies, use Escape ability.
2. If an enemy commander is valid, prioritize commander.
3. If health < 40%, use self-preservation ability.
4. If 5+ enemies are clustered, use area spell.
5. Otherwise use default offensive ability.
```

The final interface may be priority rules, conditional slots, behaviour cards, or another UI pattern. The underlying system should compile to deterministic rules.

---

# 11. Battle Architecture — Authoritative Simulation

## 11.1 Core Rule

**LOCKED AND FOUNDATIONAL:** every battle is resolved by one authoritative deterministic simulator.

There is no separate “real battle” and “auto-resolve battle.”

The flow is:

```text
BattleDefinition
      |
      v
Authoritative Headless Simulator
      |
      +-------------------+
      |                   |
      v                   v
BattleResult       BattleTranscript
      |                   |
      v                   v
Campaign State      Battle Viewer
```

The battle is already determined before visual playback begins.

### Consequence

**Auto-resolve is not an approximation.** It is the same battle with the visual playback skipped.

The user-facing choice is conceptually:

```text
[ Watch Battle ]
[ Skip to Result ]
```

not:

```text
[ Accurate Battle ]
[ Approximate Auto Resolve ]
```

---

## 11.2 BattleDefinition

A committed battle should contain all authoritative inputs required for deterministic resolution.

Conceptual structure:

```text
BattleDefinition
- simulation/version ID
- battle seed
- battlefield definition/seed
- weather/environment
- side A army
- side B army
- commanders
- squads/formations
- unit definitions
- pre-battle orders
- ability policies
- terrain effects
- province modifiers
- deployment data
```

After commitment, runtime visual state must not change the simulation result.

---

## 11.3 Determinism

Given the same:

- simulation version;
- BattleDefinition;
- seed;

…the simulator must produce the same result and authoritative transcript.

Determinism enables:

- exact bug reproduction;
- headless testing;
- battle hashes;
- reliable auto-resolve;
- replays;
- bulk balance simulation;
- AI-vs-AI campaign resolution;
- strong milestone verification.

---

## 11.4 Fixed-Tick Simulation

The battle simulation should use a fixed logical tick rather than depending on render frame rate.

A likely starting target is roughly 10–20 simulation updates per second, but the exact rate must be profiled and is **not yet locked**.

The 3D viewer may render at any normal frame rate and interpolate between simulation states.

---

# 12. Battle Transcript and Playback

## 12.1 Playback Model

**LOCKED:** a watched battle is effectively a visualization/playback of the already-determined authoritative battle.

The viewer does not decide:

- hits;
- damage;
- deaths;
- morale;
- targets;
- ability outcomes;
- winner.

It visualizes them.

This allows pause, camera movement, speed changes, and potentially rewind/scrubbing without changing the result.

---

## 12.2 Transcript Content

Do **not** record every render-frame coordinate.

The transcript should use semantic events and sufficient periodic state/keyframes to reconstruct presentation.

Possible events:

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

The viewer can convert an event such as `ATTACK_RESOLVED` into a visual bump and `DAMAGE_DEALT` into a red flash.

Periodic snapshots may include:

- squad anchors;
- unit positions where necessary;
- facing;
- major state;
- health/morale summary;
- active targets.

Exact transcript storage and keyframe frequency are **TBD**.

---

## 12.3 Replay Persistence

A seed alone is sufficient to reproduce a battle only while the same simulation rules remain installed.

If long-term replays must survive balance patches, preserve a transcript or implement simulation-version compatibility.

**TBD:** whether campaigns retain full historical battle replays by default, only recent battles, or only explicitly saved replays.

---

# 13. Battle Simulation Model

## 13.1 Formation-First Logic

Most conventional troops should behave primarily through their squad/formation rather than as fully autonomous agents.

A squad may own:

```text
- formation anchor
- facing
- width/depth
- formation type
- destination
- current target formation
- stance
- cohesion
- morale
- pressure
- retreat state
```

Individual units can still own:

```text
- ID
- squad
- local slot / position
- health
- statuses
- cooldowns
- current state
- target when required
- next action tick
```

This avoids turning a 1,000-unit battle into 1,000 high-complexity AI agents.

---

## 13.2 Movement

The simulation should operate fundamentally in a 2D horizontal plane, e.g. X/Z coordinates.

The 3D terrain supplies height and terrain information but is not the authoritative movement physics system.

Conceptually:

```text
simulation position = (x, z)
visual position = (x, HeightAt(x,z), z)
```

This is a key simplification.

---

## 13.3 Pathfinding

**LOCKED NON-GOAL:** do not begin with per-unit A*, NavMesh agents, or high-complexity local pathfinding.

Initial battlefields should avoid maze-like obstacle layouts.

Use combinations of:

- direct formation movement;
- formation anchors;
- local separation;
- simple obstacle steering where necessary;
- terrain movement costs;
- engagement constraints.

Special units may later have distinct movement rules, but they should not force the entire simulation into conventional RTS pathfinding.

---

## 13.4 Formation Contact and Pressure

When formations meet, create a contact relationship rather than having every soldier independently search the entire enemy army.

Front-line units can perform attacks against nearby valid enemies while the formation as a whole tracks pressure, cohesion, and position.

A stronger formation may gradually push a weaker formation backward.

This provides visible battle-line movement without physics.

---

## 13.5 Target Acquisition

Never perform naive all-vs-all target searches every frame.

Use:

- squad-level targets;
- spatial partitioning/grid/hash where individual queries are required;
- cached targets;
- periodic reconsideration;
- trigger-based target invalidation.

A unit should not reacquire its target every render frame.

---

## 13.6 Melee

Ordinary melee requires no weapon animation.

Presentation sequence:

1. unit/front rank is in valid contact;
2. authoritative attack resolves;
3. viewer briefly moves/bump-shifts attacking sprite toward target;
4. target flashes red or receives other feedback;
5. attacker returns to formation slot;
6. if target dies, death presentation triggers.

The bump is visual feedback only.

---

## 13.7 Ranged Combat

Projectiles need not be physical simulation objects.

The authoritative simulator can determine:

- shot initiated;
- target;
- arrival/resolution tick;
- hit/damage/status result.

The viewer may spawn an arrow, tracer, bolt, or spell projectile between known positions purely for presentation.

Projectile collision physics are not required for ordinary attacks.

---

## 13.8 Abilities and Magic

Abilities may apply:

- direct damage;
- area damage;
- statuses;
- buffs/debuffs;
- summons;
- displacement;
- morale effects;
- terrain/environment effects where supported.

Major abilities should receive stronger visual effects than routine attacks so battle readability is preserved.

---

## 13.9 Morale and Routing

Morale is important because it allows armies to collapse before every individual unit is dead.

Potential contributors:

- casualties;
- commander death;
- flanking;
- formation break;
- terrifying monsters;
- abilities;
- faction traits;
- nearby routing units.

Exact morale formula is **TBD**.

A routed formation should visibly lose order and retreat rather than continue fighting normally.

---

# 14. Visual Battle Language

## 14.1 Art Direction

**LOCKED:** preferred battle presentation is **2.5D**:

- low-poly 3D terrain;
- 2D billboarded unit sprites;
- 2D billboarded foliage;
- simple 3D landmarks/props where useful;
- modern lighting, particles, fog, weather, and spell VFX;
- restrained unit animation.

The desired result is a stylized simulated battlefield rather than a conventional 3D character-action scene.

---

## 14.2 Low-Poly Terrain

Battle terrain should preferably be generated at runtime from deterministic battlefield data/height fields.

A likely method:

1. generate or load height values;
2. construct a coarse mesh;
3. triangulate;
4. calculate normals;
5. colour/material regions by terrain;
6. place water/ground overlays where needed;
7. scatter foliage/props;
8. place deployment zones and landmarks.

The mesh may deliberately retain a low-poly faceted aesthetic.

Godot provides `ArrayMesh` and `SurfaceTool` for procedural geometry construction in C#.

---

## 14.3 Battlefields Derived from Provinces

Preferred model:

```text
Campaign Province Data
        |
        v
Deterministic Battlefield Generator
        |
        +-> terrain/height field
        +-> vegetation distribution
        +-> landmark placement
        +-> deployment zones
        +-> terrain modifiers
```

A forested hill province should generate a recognizably forested, elevated battlefield rather than an unrelated generic arena.

**Preferred but not yet hard-locked:** a province has a stable base battlefield seed so repeated battles in the same province reproduce the same underlying landform, while weather/season may vary.

---

## 14.4 Billboard Units

Godot `Sprite3D` is the natural baseline for troop visuals because it displays 2D textures inside a 3D environment and supports billboard behaviour.

A normal unit view needs very little state:

```text
- sprite/texture
- 3D transform
- faction identification
- selection/highlight state
- shadow
- transient flash/tint
- alive/dead state
```

No skeletal rig is required.

---

## 14.5 Foliage

Trees, bushes, flowers, and other decorative elements can also use billboards.

A forest may visually consist of many billboard trees while the simulator uses a simpler region/density model.

The simulation need not know the exact physical position of every decorative tree unless a later mechanic requires it.

---

## 14.6 Death Presentation

**LOCKED baseline:** a dead ordinary unit may:

1. flash red;
2. rotate/fall flat;
3. be replaced by a remains sprite.

Possible broad remains categories:

- humanoid body;
- bones;
- beast carcass;
- construct wreckage;
- ash/residue;
- magical fade.

Sprite visual tags may help choose a category.

Remains should persist long enough to make battlefield history readable.

---

## 14.7 Visual Readability Rules

Avoid turning large battles into effect noise.

Routine attacks should be visually restrained.

Reserve strong visual treatment for:

- commander deaths;
- large monsters;
- major spells;
- formation breaks;
- routs;
- mass casualties;
- summons;
- decisive charges.

At wider camera zooms or higher playback speeds, the renderer may simplify per-unit feedback and emphasize squad-level information.

---

# 15. Camera and Battle Viewer

The battle viewer should support:

- pause;
- resume;
- playback speed controls;
- tactical camera movement;
- unit/squad selection;
- basic order/target inspection;
- result transition.

The transcript architecture should make future features feasible, including:

- rewind;
- timeline scrubbing;
- jump to commander death;
- jump to formation break;
- key-event bookmarks;
- replay save/export.

These advanced replay tools are desirable but not all required for the first vertical slice.

---

# 16. Battle Analysis and Explainability

Post-battle reporting is important because learning from the simulation is part of the core loop.

Potential reports:

- winner;
- battle duration;
- casualties by side;
- casualties by squad;
- damage dealt;
- damage received;
- ability usage;
- commander survival;
- morale break times;
- routes/retreats;
- high-impact units;
- major timeline events.

The battle viewer should eventually allow selected units/squads to expose:

```text
Current order
Current target
Target priority
Formation state
Morale state
Active modifiers
Commander effects
Recent important events
```

Debug/explanation tooling should share underlying data rather than inventing a separate approximation of why the AI acted.

---

# 17. Strategic AI

## 17.1 Responsibilities

Campaign AI must be able to decide:

- what to recruit;
- where to expand;
- what provinces matter;
- where to defend;
- when to consolidate armies;
- when to attack;
- which structures to build;
- how to configure army composition;
- how to assign commanders;
- how to configure battle doctrine.

---

## 17.2 Utility-Based Baseline

A utility/scoring approach is a reasonable starting architecture.

Example province desirability:

```text
resource value
+ landmark value
+ strategic connectivity
+ enemy vulnerability estimate
- travel cost
- expected casualties
- risk created elsewhere
```

The exact AI framework is not locked, but decisions must be inspectable and debuggable.

AI logs/debug panels should be able to show scored alternatives rather than only the final decision.

---

## 17.3 Army Roles

Possible strategic army roles include:

- expansion force;
- main field army;
- capital defence;
- frontier defence;
- raider;
- siege force;
- emergency response.

These are candidate concepts, not locked final categories.

---

## 17.4 AI Battle Forecasting Must Not Cheat

The actual battle is deterministic after commitment, but strategic AI should not automatically receive perfect foreknowledge of a seeded battle outcome unless the game intentionally grants such information.

For deciding whether to attack, AI should use:

- heuristics;
- known unit composition;
- estimated matchup strength;
- possibly sampled/approximate simulations using only information it is allowed to know.

Once an engagement is committed, the real authoritative simulator determines the result.

---

# 18. Procedural AI Factions

AI opponents can use the same mechanical faction creation framework as the player.

A generated AI faction should ideally have:

- a mechanical concept;
- strengths and weaknesses;
- army doctrine;
- recruitment preferences;
- a visual identity profile;
- a lord profile;
- coherent sprite selections based on visual tags.

This reduces the need to hand-author every opponent while making procedural factions readable.

Generated factions should feel intentional, not like random independent trait rolls.

---

# 19. Engine and Technology Decision

## 19.1 Engine

**LOCKED CURRENT CHOICE:** **Godot 4.7.1 .NET**.

Godot 4.7.1 is the current stable maintenance release as of this document date. The project should use the .NET build to support C#.

Primary reasons:

- strong fit for mixed 2D/3D presentation;
- `Sprite3D` for billboarded troops and foliage;
- procedural mesh support;
- C#/.NET support;
- direct command-line scene execution;
- headless mode and command-line export;
- comparatively low editor/automation friction;
- human-readable `.tscn` scenes;
- good source-control and agent inspectability;
- no need for Unreal-level editor/binary-asset complexity;
- more natural 3D support than a PixiJS-only stack.

### Official references

- Godot 4.7.1 archive: https://godotengine.org/download/archive/
- Godot stable docs: https://docs.godotengine.org/en/stable/
- C#/.NET: https://docs.godotengine.org/en/stable/tutorials/scripting/c_sharp/
- CLI: https://docs.godotengine.org/en/stable/tutorials/editor/command_line_tutorial.html
- Sprite3D: https://docs.godotengine.org/en/stable/classes/class_sprite3d.html
- Procedural geometry / SurfaceTool: https://docs.godotengine.org/en/stable/tutorials/3d/procedural_geometry/surfacetool.html

---

## 19.2 Language

**LOCKED:** C# for primary project/game code.

Do not default to GDScript merely because Godot supports it.

Reasons include:

- substantial deterministic simulation code;
- strategic AI;
- procedural generation;
- trait composition;
- replay processing;
- strong typing/refactoring;
- bulk simulation/balance tooling;
- long-term maintainability.

Mixing GDScript and C# should require a concrete benefit rather than convenience.

---

## 19.3 Scene Format

Prefer `.tscn` text scenes rather than binary `.scn` where practical.

Godot’s TSCN format is mostly human-readable and version-control friendly. This is valuable for both source review and AI-assisted development.

An agent should often be able to inspect or modify scene structure through source rather than requiring the project owner to manually reproduce every node operation in the editor.

---

# 20. Technical Architecture

## 20.1 One Godot Project, Not a Separate Game Backend

The project should not be architected as a standalone non-Godot game plus a disposable Godot shell.

The deterministic simulator should be cleanly separated by responsibility, but **completion is always judged in the real Godot application**.

Recommended conceptual areas:

```text
Game/
  Core/
  Content/
  Campaign/
  Factions/
  AI/
  Battle/
    Simulation/
    Transcript/
    Playback/
  Presentation/
  UI/
  Scenes/
  Debug/
  Tests/
```

The exact folder structure can change, but the responsibility boundaries should remain clear.

---

## 20.2 Authoritative State vs Presentation

The simulation owns:

- game rules;
- battle outcome;
- campaign state transitions;
- RNG;
- orders;
- damage;
- morale;
- movement logic;
- AI decisions.

Godot presentation owns:

- scene lifecycle;
- camera;
- UI/input;
- sprite display;
- terrain rendering;
- particles;
- audio;
- interpolation;
- visual feedback;
- replay controls.

The renderer does not decide game outcomes.

However, this boundary must **not** be used as an excuse to declare features complete without exercising the production Godot scene.

---

# 21. Data and Content

Mechanics should prefer source-controlled, diffable definitions rather than opaque editor-only assets.

Possible formats include:

- JSON;
- text Godot Resources (`.tres`);
- strongly typed C# definitions generated/loaded from data.

The exact schema is **TBD**.

Guiding principle:

> Mechanical content should be easy for tools and agents to inspect, diff, validate, and bulk-edit.

Presentation records may reference:

- textures;
- sprites;
- materials;
- VFX;
- sound;
- visual tags;
- scale/pivot;
- billboard configuration;
- remains category.

Mechanics should reference visuals through stable IDs rather than by inferring rules from filenames.

---

# 22. Performance Philosophy

## 22.1 Target Scale

The game is expected to support **hundreds of units** in visible battle.

A reasonable early target is:

- normal proof battles around 100v100 to 300v300;
- stress testing around 500v500;
- future scaling based on profiling rather than assumption.

The exact final maximum is not locked.

---

## 22.2 Why the Scale Is Feasible

Ordinary units intentionally avoid expensive systems:

- no skeletons;
- no complex animation graphs;
- no rigidbody combat;
- no per-unit NavMesh agents;
- no frame-by-frame all-target searches;
- no realistic physics projectiles;
- minimal per-unit visual state.

The main performance risks are expected to be:

- inefficient targeting/search;
- excessive node/object overhead at very large counts;
- excessive effect spam;
- transcript/playback memory;
- strategic AI or battle simulation algorithms.

These must be profiled before introducing complex optimization architecture.

---

## 22.3 Optimization Escalation

Start simple.

Potential escalation path:

1. normal `Sprite3D`/Node3D views and pooled effects;
2. centralised update management and reduced per-node processing;
3. MultiMesh or other batched rendering for dense simple visuals if profiling proves necessary;
4. specialised native/GDExtension optimization only for proven CPU hotspots.

Do not begin by over-engineering for theoretical thousands of units.

---

# 23. Auto-Resolve Performance

Headless battle simulation should run substantially faster than viewed real time.

A future benchmark may target resolving a several-minute-equivalent 500v500 battle in well under one second on a typical development machine, but this is **not yet a contractual number**.

The important requirement is architectural:

> auto-resolve must not wait for rendering, animation, audio, physics, or the visual scene.

If headless simulation becomes slow, optimize the simulation rather than replacing it with a less accurate army-score shortcut.

---

# 24. Testing, Verification, and Anti-Facade Rules

This project is expected to use AI agents heavily. Previous Unity work demonstrated that agents can falsely declare milestones complete by relying on decoupled tests, weakening acceptance criteria, inventing obsolete test runners, or handwaving engine integration.

This project must explicitly prevent that failure mode.

---

## 24.1 Fundamental Acceptance Rule

**LOCKED:** a gameplay feature is not complete merely because a pure C# simulation test passes.

Completion requires the relevant **production Godot scene/runtime path** to work.

Example:

A battle movement milestone is not complete until the real BattleLab scene launches, production presenters spawn, units visibly move according to the transcript, and runtime logs remain clean.

---

## 24.2 Keep Infrastructure Boring

Do not create layers of bespoke test runners to police other test runners unless genuinely unavoidable.

The project should establish a small set of known-good commands and freeze them in `AGENTS.md` / development documentation.

Likely workflow categories:

```text
dotnet build
run production scene directly
run approved automated tests
run headless simulation verification
export Windows build
run build smoke test
inspect runtime log/report
```

Godot officially supports direct scene execution, headless execution, explicit log files, and command-line exports.

Once commands are proven, agents should use them rather than repeatedly redesigning the verification infrastructure.

---

## 24.3 Clean Logs Are Part of Passing

A test returning “pass” while flooding the console with errors is not acceptable.

Unexpected runtime:

- errors;
- exceptions;
- assertion failures;
- missing-resource errors;
- invalid-node errors;

must fail acceptance even if a separate test result reports success.

Warnings should be triaged rather than blindly ignored, but not every harmless warning must be treated as fatal.

---

## 24.4 Production Assets and Scenes

Integration/acceptance tests should load the same production scenes and components used by the actual game.

A test must not silently create a substitute BattlePresenter, fake bootstrap, or empty test-only scene and then use that as proof the production feature works.

Test doubles are acceptable for narrow unit tests, not for milestone runtime acceptance.

---

## 24.5 Acceptance Criteria Must Not Be Weakened by the Implementing Agent

The implementing agent must not silently:

- delete failing acceptance tests;
- mark them ignored/skipped;
- replace substantive assertions with trivial ones;
- reduce expected counts;
- change hashes to match a broken result;
- catch and discard errors;
- swap production integration tests for mocks;
- rewrite milestone requirements so its implementation passes.

Any required acceptance change must be explicit and justified as a design/planning change.

---

## 24.6 Runtime Evidence

Milestone evidence should include machine-readable runtime information where practical.

Example BattleLab report:

```json
{
  "scene": "BattleLab",
  "seed": 82741,
  "unitsSpawned": 200,
  "transcriptLoaded": true,
  "movementEventsPresented": 412,
  "damageEventsPresented": 151,
  "deathEventsPresented": 37,
  "remainsSpawned": 37,
  "resultPresented": "SideAWin",
  "unexpectedErrors": 0
}
```

This report must be generated from the production runtime path, not manually fabricated by a separate reporting script.

---

# 25. Recommended First Vertical Proof

This is not a complete production milestone plan, but it defines the preferred first end-to-end proof.

## Battle Laboratory 0

Create one production `BattleLab` path that proves the complete architecture.

### Simulation

- two armies;
- 50–100 units per side;
- fixed formations;
- deterministic seed;
- advance;
- contact;
- melee damage;
- death;
- morale;
- rout;
- terminal result;
- transcript generation.

### Presentation

- generated low-poly terrain or simple initial equivalent;
- billboard unit sprites;
- formations visibly advance;
- front-line attack bump;
- red damage flash;
- death rotation;
- remains replacement;
- retreat movement;
- result screen;
- pause;
- 1x and accelerated playback.

### Auto-resolve equivalence

For the same committed battle:

- **Skip to Result** applies the simulator result;
- **Watch Battle** plays the transcript;
- both end with exactly the same campaign-relevant outcome.

### Acceptance

- deterministic hash/result stable;
- production scene launches directly;
- no unexpected runtime errors;
- production scene consumes the real transcript;
- Windows export launches;
- auto-resolve and watched battle agree;
- runtime report proves events were actually presented.

Do not progress into a large faction/campaign implementation until this path is reliable enough that an agent can modify it, run it, detect a break, and repair it without redesigning the entire toolchain.

---

# 26. UI / UX Direction

The game will require information-dense strategy UI, but it should remain legible rather than imitate a spreadsheet for its own sake.

Major surfaces are expected to include:

- main menu;
- new campaign setup;
- faction creator;
- lord creator;
- campaign map;
- province detail;
- recruitment;
- construction;
- army management;
- commander/squad doctrine;
- battle preview/commit;
- battle viewer;
- post-battle report;
- faction/encyclopaedia information.

Godot `Control`-based UI should be preferred for normal interface work.

Detailed visual language, theme, typography, and colour palette are **TBD**.

---

# 27. Audio and Effects

Audio should reinforce simulation clarity.

Routine unit contact does not need individually lavish sound design. Use layered/grouped audio carefully so a 500-unit melee does not become noise.

High-priority sounds include:

- major spell casts;
- formation break;
- commander death;
- rout;
- large monster attack;
- decisive impacts;
- battle start/end.

Effects should follow the same hierarchy: restrained routine feedback, stronger exceptional-event feedback.

---

# 28. Modularity Without Over-Engineering

The project benefits from clear responsibility boundaries, but architecture must serve the game rather than become an objective in itself.

Avoid:

- abstracting every system before a concrete second implementation exists;
- building multiple backends;
- inventing plugin frameworks too early;
- creating elaborate test orchestration layers;
- generalising away Godot simply to claim engine independence;
- forcing dependency boundaries that prevent straightforward runtime verification.

Preferred principle:

> Keep authoritative rules deterministic and testable, keep presentation separate enough not to control outcomes, but deliver every milestone as a real playable vertical integration inside Godot.

---

# 29. Key Locked Decisions Summary

An agent should treat the following as current project law unless the owner explicitly revises them:

1. **Godot 4.7.1 .NET is the chosen engine.**
2. **C# is the primary language.**
3. **The strategic campaign is turn-based and province-based.**
4. **The campaign map is procedurally generated and fundamentally a province graph.**
5. **The player creates a custom faction and custom lord/god/leader.**
6. **Faction mechanics and faction visuals are loosely coupled.**
7. **Visuals use preset sprites/sprite packs, not modular body-part construction.**
8. **Sprites carry descriptive visual tags for player recommendations and AI generation.**
9. **Some creatures use universal/shared sprites across factions, with optional light recolouring/ownership treatment.**
10. **Armies are built from squads/formations under commanders.**
11. **The player configures behaviour/doctrine before battle.**
12. **Battles are fully automated after commitment.**
13. **There is exactly one authoritative deterministic battle simulation.**
14. **Every battle is resolved by that simulator before watched playback.**
15. **Auto-resolve means skipping playback, not using a simpler formula.**
16. **The battle viewer visualizes the authoritative transcript/result and cannot alter it.**
17. **Combat logic should be formation-first, not 1,000 independent high-complexity agents.**
18. **No initial per-unit NavMesh/A* pathfinding.**
19. **No physics-driven ordinary combat.**
20. **No skeletal attack animation requirement.**
21. **Ordinary melee can be represented by sprite movement/contact bumps and red damage flashes.**
22. **Ordinary death can be represented by rotation/fall and remains replacement.**
23. **Preferred battle art direction is low-poly 3D terrain with billboarded 2D units and foliage.**
24. **The battle simulation operates primarily in 2D horizontal coordinates; terrain height is sampled for presentation/game modifiers.**
25. **Battle terrain should be procedurally derived from province/battlefield data.**
26. **Headless battle simulation must be substantially faster than watched real time.**
27. **Gameplay milestones are not complete without production Godot runtime verification.**
28. **Clean runtime logs are part of acceptance.**
29. **Agents may not weaken tests/acceptance criteria to manufacture passes.**
30. **Testing infrastructure should remain small, documented, and boring rather than being repeatedly redesigned.**

---

# 30. Deliberately Unresolved Decisions

These require future design work and should not be invented as if already decided:

- final game title;
- narrative/lore/theme;
- exact faction-creation taxonomy and point budget;
- exact unit stat model;
- economy/resource types;
- recruitment mechanics;
- structure list and province build slots;
- research/magic progression structure;
- exact campaign turn-resolution model;
- diplomacy;
- sieges and fortifications;
- naval/amphibious strategic systems;
- underground/alternate planes;
- exact victory conditions beyond baseline conquest;
- exact battle tick rate;
- exact formation set;
- exact morale formula;
- exact doctrine UI;
- permanent replay retention policy;
- final maximum battle size;
- visual UI theme;
- complete content counts for launch;
- whether Linux is a formally supported launch target;
- whether modding support becomes a first-class feature.

These can be explored in later design documents without invalidating the foundation above.

---

# 31. Design Litmus Tests

When evaluating a new feature, ask:

### Does it reinforce planning before battle?

Good examples:

- new squad doctrine;
- formation choices;
- faction traits;
- commander abilities;
- province strategy.

Less aligned:

- adding manual dodge controls during combat.

### Can the authoritative simulator resolve it without rendering?

If not, reconsider whether the mechanic belongs in the authoritative battle model.

### Can the player understand why it happened?

If a mechanic has major consequences but cannot be explained through UI/logs/replay, it needs additional observability.

### Does it require enormous art/animation complexity for little systemic value?

Prefer reuse and symbolic presentation.

### Is the implementation solving an observed problem or a hypothetical future one?

Avoid architecture driven by imagined scale before profiling.

### Can an agent prove it works in the actual Godot runtime?

If the answer is “only the isolated tests pass,” the feature is not complete.

---

# 32. Concise Product Statement

> A single-player procedural fantasy strategy game where the player creates a custom faction and lord, expands across a turn-based province map, builds armies and pre-programs their doctrine, then resolves wars through deterministic large-scale auto-battles. Every engagement is fully simulated once: players may instantly accept the result or watch the same pre-resolved battle play back on a stylized 2.5D battlefield of low-poly terrain, billboarded troops, simple formation movement, damage flashes, routing, magic, and persistent remains. The game prioritizes systemic faction construction, strategic planning, battle readability, replayability, and explainable outcomes over manual tactical control or animation complexity.

---

# 33. External Technical References

These references are implementation aids, not substitutes for the decisions in this GDD.

- Godot 4.7.1 archive / current release information: https://godotengine.org/download/archive/
- Godot stable documentation: https://docs.godotengine.org/en/stable/
- Godot C#/.NET documentation: https://docs.godotengine.org/en/stable/tutorials/scripting/c_sharp/
- Godot command-line workflow: https://docs.godotengine.org/en/stable/tutorials/editor/command_line_tutorial.html
- Godot `Sprite3D`: https://docs.godotengine.org/en/stable/classes/class_sprite3d.html
- Godot procedural geometry / `SurfaceTool`: https://docs.godotengine.org/en/stable/tutorials/3d/procedural_geometry/surfacetool.html
- Godot TSCN text scene format: https://docs.godotengine.org/en/stable/engine_details/file_formats/tscn.html

---

**End of foundation GDD.**
