# Warwrought Worklog

## 2026-08-09 — M0.1 Project Foundation

- Status: Complete; M0.1 acceptance verification succeeded.
- Established the pinned Godot 4.7.1 .NET/C# project identity, Forward+ feature, desktop window baseline, candidate Windows Desktop preset, root solution/project, initial M0 directories, C# conventions, and ignore policy.
- `dotnet build --nologo` and `dotnet build Warwrought.sln --nologo` both succeeded with 0 warnings and 0 errors; the solution lists `Warwrought.csproj`.
- Pinned console `--headless --editor --import`, pinned GUI `--headless --editor --import`, pinned console `--build-solutions --quit`, and a visible pinned GUI `--editor --import` all exited 0. The visible import reported `D3D12 12_0 - Forward+`; final import/build logs contain no unexpected error-pattern matches.
- Evidence is under ignored `artifacts/local/m0.1/` (`dotnet-build*.log`, `dotnet-solution-list.log`, and Godot import/build logs). Commands remain provisional; M0.4 owns command freezing.
- Export execution, player smoke, BootstrapLab, runtime reports, acceptance scanners, tests, and gameplay were intentionally not performed; those belong to later tasks/non-goals.
- An intentionally early exploratory GUI `--quit-after 1` invocation emitted Godot's `Scan thread aborted` warning; it was not used as acceptance evidence and was superseded by the clean supported `--import` run.
