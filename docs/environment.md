# Warwrought Development Environment

This file records the machine-specific toolchain facts used to establish M0.1. It is documentation, not a replacement for the provisional command evidence captured during verification.

## Project configuration

- Project: `Warwrought`
- Engine/runtime: Godot 4.7.1 .NET/C#
- Renderer feature: `Forward Plus`
- Project assembly: `Warwrought`
- C# target framework: `net10.0`
- Godot SDK: `Godot.NET.Sdk/4.7.1`
- Nullable reference types: enabled
- .NET analyzers: enabled at the SDK's `latest-recommended` analysis level
- Warnings as errors: disabled; diagnostics remain visible without making the foundation unnecessarily brittle
- C# namespace convention: root namespace `Warwrought`; namespaces mirror responsibility directories; file-scoped namespaces are preferred

## Pinned Godot executables

The paths below are the only Godot executables permitted by the repository contract.

| Role | Absolute path | Observed version |
|---|---|---|
| GUI/editor | `D:\Dev\Godot\Godot_v4.7.1\Godot_v4.7.1-stable_mono_win64.exe` | `4.7.1.stable.mono.official.a13da4feb` |
| Console/CLI | `D:\Dev\Godot\Godot_v4.7.1\Godot_v4.7.1-stable_mono_win64_console.exe` | `4.7.1.stable.mono.official.a13da4feb` |

Both paths were present when this record was captured. Running `--version` on each pinned executable reported `4.7.1.stable.mono.official.a13da4feb`.

## Installed .NET environment

Observed with `dotnet --info` on 2026-08-09:

- SDK: `10.0.302`
- MSBuild: `18.6.11+35b593beb`
- .NET host runtime: `10.0.10`, x64
- OS: Windows 10, build `10.0.26200`, `win-x64`
- Installed runtimes relevant to this project include `Microsoft.NETCore.App 10.0.10` and `Microsoft.WindowsDesktop.App 10.0.10`
- Additional installed .NET runtimes: `Microsoft.NETCore.App`/`Microsoft.WindowsDesktop.App` 6.0.27, 5.0.17, and 3.1.32
- No `global.json` was present; the selected SDK is therefore the installed `10.0.302` SDK

Godot's verbose editor initialization also observed:

- hostfxr: `C:/Program Files/dotnet/host/fxr/10.0.10/hostfxr.dll`
- discovered .NET SDK: `10.0.302` at `C:\Program Files\dotnet\sdk\10.0.302`

## Desktop baseline

The initial development window is windowed and resizable with a 1280x720 viewport and 1280x720 window override. These values are a usable development baseline, not final UI requirements.

## Verification state

M0.1 command syntax remains provisional until the later M0.4 freeze task. The root solution build and Godot solution build both succeeded with the target framework and SDK above. A visible GUI import reported `D3D12 12_0 - Forward+` on the development GPU. Candidate build/import output is kept in ignored local artifact paths; the factual outcome and exact candidate invocations are summarized in `worklog.md`.
