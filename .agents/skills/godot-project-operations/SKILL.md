---
name: godot-project-operations
description: Use when building, running, testing, logging, or exporting the Godot .NET project. Provides the boring official CLI workflow, acceptance-artifact rules, and safeguards against inventing alternate runners when the project's blessed commands fail.
compatibility: Agent Skills; intended for Godot 4.7 .NET/C# project workflows
metadata:
  project-area: tooling
  project-phase: cross-cutting
---

# Godot Project Operations

## Purpose

Use this skill whenever work requires operating the actual Godot project rather than only inspecting source code. It exists to keep project operations small, repeatable, and based on supported Godot/.NET mechanisms.

This skill is **not** a test framework. It is an operating contract.

## Authority order

Before running anything, use this precedence:

1. `AGENTS.md` and any repository-frozen commands established by M0.
2. Existing transparent repository scripts that implement those frozen commands.
3. The command shapes in this skill as guidance when the repository has not yet frozen an equivalent.
4. Current official Godot 4.7/.NET documentation when a material detail remains uncertain.

Once M0 has proven and frozen a command, do not replace it merely because another invocation looks cleaner.

## Project discovery

Before issuing Godot commands:

- locate `project.godot`;
- locate the `.sln`/`.csproj` used by the Godot .NET project;
- locate `export_presets.cfg` when export is involved;
- read `AGENTS.md` for the configured Godot executable and accepted scripts;
- identify the exact production scene required by the task.

Do not assume the executable is named `godot`. Depending on installation it may be a configured path, `godot`, `godot-mono`, or a repository wrapper. Use the repository's proven configuration.

## Supported command shapes

These are patterns, not permission to override frozen project commands.

### Compile C#

Prefer the repository's pinned solution/project:

```text
dotnet build <solution-or-project>
```

If M0 establishes a different exact invocation, use that exact command thereafter.

### Fast deterministic tests

Use only the approved fast test project/path:

```text
dotnet test <approved-test-project>
```

These tests prove rules and algorithms. They do **not** prove Godot runtime integration.

### Run a production scene

Godot supports running a specific scene directly. A typical shape is:

```text
<godot> --path "<project-root>" --scene "res://path/to/ProductionScene.tscn"
```

For acceptance scenarios, use project-defined user arguments after `--` so they can be read via `OS.GetCmdlineUserArgs()`:

```text
<godot> --path "<project-root>" \
  --scene "res://path/to/ProductionScene.tscn" \
  --log-file "<artifact-log-path>" \
  -- \
  --acceptance=<scenario-id> \
  --report=<report-path>
```

The actual flags/scenario syntax are frozen by the repository in M0. Do not casually rename them later.

### Headless runtime path

Use `--headless` only for a runtime path that does not require visible rendering evidence:

```text
<godot> --headless --path "<project-root>" \
  --scene "res://path/to/ProductionScene.tscn" \
  --log-file "<artifact-log-path>" \
  -- \
  --acceptance=<scenario-id>
```

Do not use headless execution as a substitute when the acceptance criterion is specifically about rendered production presentation.

### Windows export

Use the committed export preset and the Godot editor binary:

```text
<godot> --headless --path "<project-root>" \
  --export-release "<Windows preset name>" "<output.exe>"
```

The preset name must match `export_presets.cfg`. Do not synthesize a new preset during routine verification.

### Exported-player smoke

Run the exported executable through the repository's accepted smoke arguments, capture an explicit log/report, and check its exit state. Editor success does not substitute for this when the milestone requires a built-player proof.

## Runtime acceptance artifacts

Production acceptance should produce evidence from the production runtime path. Where defined by the project, require:

- explicit scenario ID;
- scene/runtime identity;
- seed and simulation version where relevant;
- authoritative result/digest where relevant;
- presentation counters/observations where relevant;
- unexpected error count;
- boolean pass/fail;
- explicit runtime log file.

A thin PowerShell/shell wrapper may locate binaries, normalize paths, create artifact directories, invoke the command, and collect exit codes. It may **not** fabricate the runtime report.

## Clean-log rule

A nominally passing command is not a pass if the runtime log contains unexpected:

- errors;
- exceptions;
- assertion failures;
- missing-resource failures;
- invalid node/resource references;
- repeated error floods hidden behind a success exit code.

Warnings require triage rather than automatic failure. Known benign warnings should be documented explicitly instead of silently ignored.

## Failure handling

When a frozen command fails:

1. preserve the exact command and output;
2. inspect the explicit log/report;
3. determine whether the failure is compilation, project import, scene wiring, runtime logic, tool/version mismatch, or environment configuration;
4. repair the actual failure;
5. rerun the same blessed path.

Do **not** respond by creating `RunnerV2`, a parallel test scene, a bespoke editor-control layer, or a second verification framework unless Planner explicitly approves a change to the verification architecture.

## C# API caution

Godot C# API names use idiomatic C# conventions and may differ from GDScript examples. Do not mechanically translate a snake_case example from memory. If the repository does not already demonstrate the API and the exact binding matters, invoke the Researcher or verify current Godot 4.7 C# documentation.

## Completion checklist

Before using Godot execution as evidence, confirm:

- the command is a frozen/approved project path;
- the scene is the required **production** scene;
- the project compiled successfully;
- the expected report was emitted by production runtime code;
- the log was inspected, not merely the exit code;
- no test-only substitute path was used;
- exported-player proof was run when required by the task.

## Official reference anchors

- Godot command line: https://docs.godotengine.org/en/4.7/tutorials/editor/command_line_tutorial.html
- Godot C# API differences: https://docs.godotengine.org/en/4.7/tutorials/scripting/c_sharp/c_sharp_differences.html
- Godot `OS` command-line arguments: https://docs.godotengine.org/en/4.7/classes/class_os.html
- Exporting projects: https://docs.godotengine.org/en/4.7/tutorials/export/exporting_projects.html
