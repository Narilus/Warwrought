# M0 — Project Foundation & Verification Contract

- **Status:** Complete; M0.4 corrective reproof passed
- **Source milestone:** `/plans/implementation_plan.md` §9 (M0)
- **Depends on:** The current baseline scaffold only
- **Completes before:** Any M1 implementation work

## Purpose

M0 establishes Warwrought as one buildable Godot 4.7.1 .NET/C# application using Forward+, with a real production runtime path, fast deterministic tests, Windows export, and a small verification contract that is actually proven before it is frozen.

This milestone is infrastructure for the product runtime, not an excuse to produce a separately completable .NET application. `BootstrapLab` is a maintained production Godot scene and is the M0 runtime acceptance surface. M0 does not implement combat, campaign, factions, or any M1 behaviour.

## Task sequence

| Order | Task | Status | Dependency | Purpose |
|---|---|---|---|---|
| 1 | [M0.1 Project foundation](M0.1_project-foundation.md) | **READY** | None | Establish the pinned Godot/.NET project, window/export configuration, ignore policy, solution, and buildable scaffold. |
| 2 | [M0.2 BootstrapLab runtime contract](M0.2_bootstraplab-runtime-contract.md) | **READY** | M0.1 accepted | Establish the Section 3 source layout and prove a real C#-backed Godot scene, acceptance report/logging behaviour, and fast test path. |
| 3 | [M0.3 Windows export and player smoke](M0.3_windows-export-smoke.md) | **READY** | M0.2 accepted | Export the production scene and prove the exported player emits its own runtime evidence. |
| 4 | [M0.4 Freeze and repeat the verification contract](M0.4_freeze-verification-contract.md) | **COMPLETE — corrective reproof passed** | M0.1–M0.3 accepted | Run the complete command sequence twice and freeze only the empirically proven commands in `AGENTS.md`. |

M0.1–M0.3 have credible acceptance evidence. M0.4 corrected and re-proved its frozen contract, so M0 is complete; no M1 implementation work was started.

The independent review rejected the initial M0.4 gate because a literal fresh-root execution reached export without creating `$runRoot` and `$runRoot\export`; Godot reported `ERROR: Prepare Template: The given export path doesn't exist.` The prior two-run count is discarded. The corrective repair is limited to the direct `AGENTS.md` block: it creates both directories inside the block and captures simple per-run command/build/test provenance without changing production code, scenes, verifier, preset, runtime architecture, or verification design. Corrective evidence uses fresh ignored `artifacts/local/m0.4/reproof-run-1/` and `reproof-run-2/` roots.

## Milestone-wide constraints

- Use only the pinned executables in `AGENTS.md`:
  - GUI: `D:\Dev\Godot\Godot_v4.7.1\Godot_v4.7.1-stable_mono_win64.exe`
  - Console/CLI: `D:\Dev\Godot\Godot_v4.7.1\Godot_v4.7.1-stable_mono_win64_console.exe`
- Godot 4.7.1 .NET/C# with Forward+ is locked. Do not substitute another engine, Godot installation, renderer, or a compatibility renderer.
- The Godot project is the product runtime. A pure .NET test project is permitted only as a sibling test path referencing the production project; it cannot stand in for the Godot runtime proof.
- Before M0.4, all command invocations are **provisional candidates**. Record what actually worked, but do not freeze commands in `AGENTS.md` early.
- Keep verification direct and small: `dotnet build`, `dotnet test`, direct Godot production-scene acceptance, official Godot export, exported-player smoke, and inspection of the resulting reports/logs. At most one transparent PowerShell helper is permitted only if direct invocation cannot reliably create/validate required artifact paths; it must show the official commands and must not fabricate reports.
- Generated caches, build outputs, exports, acceptance artifacts, logs, and local configuration remain untracked. In particular, `.godot/` must never be committed.
- Builder may not weaken this milestone’s task contracts. Any conflict with a locked decision, acceptance criterion, or unproven engine API must return to Planner.

## M0 gate

M0 passes only when a fresh terminal can run the documented build, test, production `BootstrapLab` acceptance, Windows export, and exported-player smoke sequence **twice consecutively**, without manual editor action, using the pinned toolchain. Each run must produce its own expected acceptance JSON and explicit logs; both reports must pass and the logs must contain no unexpected errors, exceptions, assertions, missing-resource failures, invalid-node/resource failures, or error floods.

The exact successful commands, required export-template prerequisite, artifact locations, and clean-log inspection rule are recorded in `AGENTS.md` after the corrected literal fresh-root sequence passed twice consecutively. The initial M0.4 freeze omitted artifact-directory creation; the corrected reproof supersedes its `run-1/` and `run-2/` evidence. Successful evidence is under ignored `artifacts/local/m0.4/reproof-run-1/` and `reproof-run-2/`: command transcripts, `dotnet build`/`dotnet test` output and exit results, BootstrapLab scene reports and logs, export logs and outputs, and exported-player reports and logs. Runtime artifacts are inspected evidence, not committed source.

## Non-goals for M0

- No battle simulator, `BattleDefinition`, transcript, BattleLab, combat visuals, campaign graph, faction system, or M1 gameplay primitives.
- No ECS, DI container, service/event bus, custom test framework, CI system, alternate runner, editor automation layer, or wrapper stack.
- No change to design gates, locked architecture, or future milestone acceptance criteria.
