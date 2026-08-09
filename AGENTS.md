# AGENTS.md — Warwrought Repository Contract

This file defines the always-on operating rules for agents working in the Warwrought repository. It is intentionally concise relative to the design and implementation documents. Read it before consequential work and follow the linked sources of truth rather than reconstructing project intent from memory.

## 1. Project Identity

- **Project:** Warwrought
- **Engine:** Godot 4.7.1 .NET/C#
- **Renderer:** Forward+
- **Primary target:** Windows desktop
- **Local project root:** `D:\Dev\Projects\warwrought`
- **Public repository:** `https://github.com/Narilus/Warwrought`
- **Canonical design document:** `/DESIGN.md`
- **Canonical implementation plan:** `/plans/implementation_plan.md`
- **OpenCode agent definitions:** `/.opencode/agents/`
- **Portable project skills:** `/.agents/skills/`

### Installed Godot executables

Use the pinned Godot 4.7.1 .NET/C# build with the Forward+ project configuration unless the project explicitly changes that locked decision. The installed executable filenames retain Godot's historical `mono` label:

- **Editor / visible GUI:** `D:\Dev\Godot\Godot_v4.7.1\Godot_v4.7.1-stable_mono_win64.exe`
- **Console / CLI:** `D:\Dev\Godot\Godot_v4.7.1\Godot_v4.7.1-stable_mono_win64_console.exe`

Prefer the console executable for command-line, headless, acceptance, logging, and export workflows. Use the GUI executable when a visible editor/runtime session is specifically required.

Do not silently substitute another Godot installation or version.

## 2. Authority Order

When sources disagree, use this precedence and surface the conflict to Planner rather than silently choosing:

1. Explicit user decisions made for the current task.
2. Locked decisions in `/DESIGN.md`.
3. `/plans/implementation_plan.md`.
4. Approved milestone/task documents.
5. This `AGENTS.md` operating contract.
6. Project skills under `/.agents/skills/`.
7. Existing implementation and comments.
8. Model memory or general convention.

The repository is the durable project memory. A long chat/session is not a substitute for reading current files.

## 3. Session Start

Before substantial planning, implementation, review, or recovery work:

1. Read this file.
2. Read the relevant sections of `/DESIGN.md` and `/plans/implementation_plan.md`.
3. Read the current milestone/task document when one exists.
4. Read `worklog.md` or the current project-state/history document when it exists.
5. Inspect the relevant repository state before making assumptions about what is implemented.
6. Load only the project skills relevant to the task.

Do not reread the entire repository indiscriminately when a focused inspection is sufficient.

## 4. Locked Architectural Invariants

The following are foundational project decisions. Do not reinterpret them as optional architecture preferences.

### Godot is the product runtime

Warwrought is one Godot/C# application. The deterministic simulation may have clean dependency boundaries and may be exercised by fast .NET tests, but the project must **not** become an independently completable C# game/backend with Godot reduced to a thin or optional wrapper.

Separation between simulation and presentation is a **dependency boundary**, not a delivery boundary.

A gameplay feature is not complete merely because a pure C# subsystem passes tests. Relevant production Godot scenes, resources, wiring, runtime behaviour, and logs must also work.

### One authoritative battle simulation

Combat has one authoritative deterministic simulation.

- **Auto-resolve:** runs that simulation without rendering.
- **Watch battle:** visualizes/playbacks that same authoritative battle.
- There is no weaker score-based resolver that produces different outcomes from watched combat.
- Rendering, frame rate, animation, particles, collision presentation, or camera state must not feed back into authoritative outcomes after battle commitment.

The battle viewer tells the story of the result; it does not decide the result.

### 2.5D presentation

The intended battle presentation is:

- procedurally generated low-poly 3D terrain;
- billboarded 2D troop sprites;
- billboarded 2D foliage where appropriate;
- simple 3D props/landmarks where useful;
- restrained lighting, particles, weather, shaders, and VFX;
- Forward+ as the renderer baseline.

Do not pre-emptively downgrade the project to a purely 2D battlefield without a design decision.

### Minimal combat animation

Ordinary units are intentionally symbolic simulation pieces, not fully animated characters.

Expected presentation vocabulary includes:

- translation/interpolation;
- formation movement;
- brief contact/bump motion;
- red or otherwise meaningful hit flashes;
- projectile/VFX cues where relevant;
- rotation/fall on death;
- replacement with remains/corpse/wreck sprites.

Do not introduce skeletal rigs, complex attack animation graphs, root motion, ragdolls, or AAA-style attack choreography as baseline requirements.

### Formation-first combat

Ordinary troops should be driven primarily by squad/formation decisions. Individual state may exist for damage, position, morale, abilities, death, and presentation, but hundreds of soldiers must not become hundreds of heavyweight tactical agents.

Do not introduce per-soldier NavMesh agents, general A* pathfinding, rigid-body combat, or physics projectiles unless a later explicit design decision requires them.

### Visuals and mechanics are deliberately decoupled

Player and AI factions use preset/tagged visual assets. Mechanical construction and visual selection are related through recommendations/tags but are not required to correspond literally.

A caster lord may use a warrior-looking sprite. A wyvern may use a universal wyvern sprite regardless of faction, perhaps with minor ownership/palette treatment.

Do not invent modular body-part/equipment sprite assembly as a baseline system.

## 5. Implementation Philosophy

- Prefer the smallest mechanism that satisfies the current locked requirement and leaves a clear expansion seam.
- Do not build generic frameworks for hypothetical future needs.
- Do not add DI containers, service layers, event buses, ECS, custom runners, or abstraction hierarchies without demonstrated need.
- Do not convert temporary tooling uncertainty into permanent architecture.
- Do not weaken acceptance criteria, tests, or runtime checks to obtain a pass.
- Do not replace a failing blessed workflow with a second bespoke workflow merely to bypass the failure.
- Strictness is not a project goal. Verification exists to prove the actual game works, not to make ordinary Godot development hostile.
- Warnings and analyzers should be useful. Do not create zero-warning absolutism or hyper-strict controls that routinely block valid engine workflows.

When a simple Godot-native solution is adequate, prefer it over a custom infrastructure layer.

## 6. Godot and C# Operating Rules

Use the `godot-project-operations` skill whenever building, running, accepting, logging, or exporting the real project.

### Current bootstrap state

The repository is at the initial scaffold / pre-M0 stage. Not every M0 artifact or command exists yet. In particular, do not assume the final `.sln`, `.csproj`, export preset, test project, acceptance scripts, or production lab scenes exist until repository inspection proves they do.

### Provisional command policy

Before M0.7, official Godot/.NET command shapes may be used to establish the infrastructure, but they are **provisional**. M0 must empirically prove the exact repository commands.

At M0.7, update this file with the exact tested commands for:

- C# build;
- fast deterministic tests;
- production scene acceptance;
- Windows export;
- exported-player smoke;
- acceptance artifact/log locations.

Once frozen, those exact commands become the default operating contract. Change them only through an explicit infrastructure task with evidence that the existing contract is genuinely insufficient.

### Production runtime evidence

When a task requires Godot integration, evidence must come from the relevant production runtime path. Test-only substitute scenes/nodes do not prove production integration.

A nominally successful run is not accepted when logs contain unexpected errors, exceptions, assertion failures, missing-resource failures, invalid node/resource references, or repeated error floods.

Do not treat a successful process exit code as sufficient evidence without inspecting the expected report/log when the task defines them.

### Current API accuracy

Warwrought targets Godot 4.7.1 .NET/C# with Forward+. C# API names and usage may differ from GDScript examples and older Godot versions.

If an exact engine API matters and the repository does not already demonstrate it, use the `researcher` agent or current official Godot 4.7 documentation. Do not build compatibility wrappers around an unverified remembered API.

## 7. Testing and Acceptance

Testing is layered, but lower layers cannot substitute for higher ones.

### Fast deterministic tests

Use pure .NET tests for deterministic algorithms and domain logic such as:

- IDs and ordering;
- deterministic RNG;
- battle rules;
- campaign graph generation;
- content validation;
- modifier composition;
- AI scoring where appropriate.

These are valuable and should be fast. They do **not** prove the Godot application works.

### Production-scene acceptance

Use real production scenes and production wiring for runtime acceptance. Required nodes/resources must fail clearly when absent rather than being silently manufactured by tests.

### Built-player smoke

When a milestone requires exported-player evidence, editor success does not substitute for running the exported Windows build.

### Protected criteria

Once Planner marks a task `READY`, Builder may not rewrite the task's:

- objective;
- required behaviour;
- acceptance criteria;
- non-goals.

If the task contract is wrong or contradictory, return it to Planner rather than changing the goalposts during implementation.

Do not delete, ignore, weaken, trivialize, or route around acceptance checks to manufacture completion.

## 8. Agent Roles and Delegation

Detailed role definitions live under `/.opencode/agents/`. This section establishes only repository-wide boundaries.

### Planner

Planner is the normal user-facing project orchestrator. Planner owns design clarification, recommendations, milestone/task planning, task documents, orchestration, and project-level decisions.

Planner should ask the user only when the answer materially affects design, architecture, acceptance, or another consequential choice. When asking, include a recommendation and relevant trade-off.

### Builder

Builder executes `READY` task packets in the real project. Builder owns local implementation decisions but not product redesign or acceptance changes.

Builder should verify its work rather than merely report that code was written. If repeated materially different attempts fail against the same acceptance criterion, stop thrashing and return a useful diagnosis to Planner.

### Reviewer

Reviewer independently checks actual requirements and evidence. Reviewer is not an architect-for-hire and should not fail work for speculative improvements that the task/design does not require.

### Researcher

Researcher resolves narrow current Godot/.NET/tool uncertainties from authoritative sources. Reusable discoveries should be persisted into project documentation or a relevant skill/reference.

### Git Steward

Git Steward owns routine bounded Git transactions after work is accepted/authorized. One request to commit and push authorizes the whole safe transaction: inspect, stage intended files, commit, push, verify.

Do not require the user to approve every ordinary Git subcommand separately.

Destructive/history-rewriting operations remain exceptional and require explicit authorization.

## 9. Git and Repository Hygiene

- Do not commit `.godot/` generated cache/state.
- Do not commit build outputs, temporary acceptance artifacts, logs, secrets, local machine configuration, or editor caches unless the repository explicitly tracks a specific fixture/reference artifact.
- Keep source, scenes, content, planning documents, agent definitions, and project skills versioned.
- Inspect staged changes before commit.
- Do not sweep unrelated dirty files into a task commit.
- Do not force-push, rebase shared history, reset away work, or otherwise rewrite history without explicit user authorization.
- Expected public remote: `https://github.com/Narilus/Warwrought`.

The repository may not yet be initialized/attached to the remote during the initial scaffold. Inspect rather than assume.

## 10. Documentation and Project State

### Canonical documents

- `/DESIGN.md` defines the game and locked design direction.
- `/plans/implementation_plan.md` defines implementation sequencing, design gates, milestone scope, and acceptance expectations.

Do not create duplicate replacement master documents because another organization looks cleaner.

### Task documents

Planner should persist substantial implementation tasks/milestones in the repository so Builder, Reviewer, and future sessions share the same contract. Task documents should contain enough context to execute without becoming miniature copies of the GDD.

### Worklog

When `worklog.md` is introduced, keep it factual and current. Record completed tasks, meaningful implementation decisions, validation results, known blockers, and the current continuation point. Do not use it as an unbounded transcript dump.

## 11. Skills

Load project skills only when relevant. Current domain/operational skills include:

- `godot-project-operations`
- `deterministic-battle-authority`
- `godot-2p5d-presentation`
- `province-world-generation`
- `faction-visual-system`
- `content-data-contracts`
- `strategic-ai-explainability`

Skills provide reusable procedures and domain constraints. They do not override locked decisions in `DESIGN.md` or the implementation plan.

Do not create generic "better programmer", "clean architecture", or duplicate role skills without demonstrated recurring need.

## 12. User Interaction Discipline

The user prefers an incremental workflow that can react to unexpected returns without losing track of what has been executed.

When the user must manually run something, paste a prompt elsewhere, or perform editor-only work:

- provide **one bounded prompt/action at a time**;
- explain surrounding context if useful, but do not queue multiple future prompts for execution;
- wait for the user to return the result before issuing the next prompt/action;
- do not repeat a question or instruction whose answer/result is already available.

Internal subagent delegation does not require the user to manually sequence every tool command; agents should complete their authorized bounded transactions autonomously.

For manual Godot editor instructions, give exact node/menu/property names and expected observable result. Do not rely on vague instructions such as "configure the scene appropriately."

## 13. Completion Standard

Do not declare a task or milestone complete because code exists, tests are green at a lower layer, or a report says PASS.

Completion means the defined acceptance criteria are supported by credible evidence through the required layers, including the actual Godot runtime whenever the feature touches it, with unexpected runtime errors resolved rather than ignored.

If implementation, tests, runtime behaviour, logs, or documentation disagree, the project is not complete until the discrepancy is understood and resolved or explicitly accepted by Planner/user.
