using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Godot;
using Warwrought.Battle.Model;
using Warwrought.Battle.Simulation;
using Warwrought.Battle.Transcript;
using Warwrought.Presentation.Battle;

namespace Warwrought.Bootstrap;

/// <summary>
/// Maintained production BattleLab bootstrap. It commits the fixture, calls the sole M1.2
/// resolver once, retains that immutable resolution, and hands only the resolution to the
/// transcript-authoritative playback controller.
/// </summary>
public partial class BattleLab : Node3D
{
    public const string SceneIdentity = "BattleLab";
    public const string ScenePath = "res://scenes/Labs/BattleLab.tscn";

    private BattleLabArguments? _arguments;
    private BattleDefinition? _definition;
    private BattleResolution? _resolution;
    private BattlePlaybackController? _playbackController;
    private int _unexpectedErrors;
    private string? _failureCategory;
    private string? _failureMessage;
    private bool _reportWritten;

    public override void _Ready()
    {
        try
        {
            _arguments = BattleLabArguments.Parse(OS.GetCmdlineUserArgs());
            if (!_arguments.IsAcceptanceRequested)
            {
                InitializeProductionBattle();
                _playbackController!.Play();
                GD.Print("BattleLab ready: maintained production transcript playback scene, normal launch mode.");
                return;
            }

            RunAcceptance(_arguments);
        }
        catch (Exception exception)
        {
            RecordFailure("UnhandledRuntimeException", exception.ToString(), countAsUnexpectedError: true);
            if (_arguments?.IsAcceptanceRequested == true)
            {
                CompleteAcceptance(_arguments, passed: false, processExitCode: 1);
            }
            else
            {
                GD.PushError($"BattleLab bootstrap exception: {exception}");
                GetTree().Quit(1);
            }
        }
    }

    public override void _Process(double delta)
    {
        _ = delta;
        if (_arguments?.IsAcceptanceMode != true || _reportWritten || _playbackController is null)
        {
            return;
        }

        if (_playbackController.PlaybackCompleted)
        {
            CompleteAcceptance(_arguments, passed: true, processExitCode: 0);
        }
    }

    private void RunAcceptance(BattleLabArguments arguments)
    {
        if (!arguments.IsValid)
        {
            RecordFailure(
                "InvalidArguments",
                arguments.ValidationError ?? "BattleLab acceptance arguments are invalid.",
                countAsUnexpectedError: false);
            CompleteAcceptance(arguments, passed: false, processExitCode: 1);
            return;
        }

        try
        {
            InitializeProductionBattle();

            // Exercise the production control surface through the same controller methods as
            // the HUD buttons. These calls change presentation clock state only.
            _playbackController!.ResetPlayback();
            _playbackController.Pause();
            _playbackController.Play();
            _playbackController.SetSpeed(1.0);
            _playbackController.SetSpeed(2.0);
            _playbackController.SetSpeed(8.0);
        }
        catch (Exception exception)
        {
            RecordFailure("BattleLabInitialization", exception.ToString(), countAsUnexpectedError: true);
            CompleteAcceptance(arguments, passed: false, processExitCode: 1);
        }
    }

    private void InitializeProductionBattle()
    {
        ValidateProductionSceneStructure();

        var fixtureInput = BattleFixtureFactory.CreateM1MeleeFixtureInput();
        if (!BattleDefinition.TryCommit(fixtureInput, out var committedDefinition, out var validation) || committedDefinition is null)
        {
            throw new BattleDefinitionValidationException(validation);
        }

        _definition = committedDefinition;

        // This is intentionally the only M1.2 resolution call in the production BattleLab path.
        // The immutable pair is retained and passed unchanged to transcript playback.
        _resolution = AuthoritativeBattleResolver.Resolve(_definition);
        _playbackController = GetNode<BattlePlaybackController>("BattlePlaybackController");
        _playbackController.Initialize(_resolution);
        BindControlSurface(_playbackController);
    }

    private void ValidateProductionSceneStructure()
    {
        _ = GetNode<Node3D>("BattlefieldRoot");
        _ = GetNode<Node3D>("BattlefieldRoot/TerrainRoot");
        _ = GetNode<MeshInstance3D>("BattlefieldRoot/TerrainRoot/Ground");
        _ = GetNode<Node3D>("BattlefieldRoot/UnitRoot");
        _ = GetNode<Node3D>("BattlefieldRoot/RemainsRoot");
        _ = GetNode<Node3D>("BattlefieldRoot/EffectsRoot");
        _ = GetNode<Node3D>("BattlefieldRoot/CameraRig");
        _ = GetNode<Camera3D>("BattlefieldRoot/CameraRig/Camera3D");
        _ = GetNode<BattlePlaybackController>("BattlePlaybackController");
        _ = GetNode<CanvasLayer>("BattleHUD");
        _ = GetNode<Control>("BattleHUD/Panel");
        _ = GetNode<Button>("BattleHUD/Panel/Content/Controls/ResetButton");
        _ = GetNode<Button>("BattleHUD/Panel/Content/Controls/PauseButton");
        _ = GetNode<Button>("BattleHUD/Panel/Content/Controls/PlayButton");
        _ = GetNode<Button>("BattleHUD/Panel/Content/Controls/Speed1Button");
        _ = GetNode<Button>("BattleHUD/Panel/Content/Controls/Speed2Button");
    }

    private void BindControlSurface(BattlePlaybackController controller)
    {
        GetNode<Button>("BattleHUD/Panel/Content/Controls/ResetButton").Pressed += controller.ResetPlayback;
        GetNode<Button>("BattleHUD/Panel/Content/Controls/PauseButton").Pressed += controller.Pause;
        GetNode<Button>("BattleHUD/Panel/Content/Controls/PlayButton").Pressed += controller.Play;
        GetNode<Button>("BattleHUD/Panel/Content/Controls/Speed1Button").Pressed += () => controller.SetSpeed(1.0);
        GetNode<Button>("BattleHUD/Panel/Content/Controls/Speed2Button").Pressed += () => controller.SetSpeed(2.0);
    }

    private void CompleteAcceptance(BattleLabArguments arguments, bool passed, int processExitCode)
    {
        if (_reportWritten)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(arguments.ReportPath))
        {
            GD.PushError("BattleLab acceptance cannot complete without an explicit --report path.");
            _reportWritten = true;
            GetTree().Quit(2);
            return;
        }

        var report = CreateReport(arguments, passed && _unexpectedErrors == 0);
        var reportPath = arguments.ReportPath;
        try
        {
            var validationErrors = report.Validate();
            if (validationErrors.Count > 0 && report.Passed)
            {
                RecordFailure("ReportValidation", string.Join(" ", validationErrors), countAsUnexpectedError: true);
                report = CreateReport(arguments, passed: false);
                processExitCode = 2;
            }

            var fullReportPath = Path.GetFullPath(reportPath);
            var reportDirectory = Path.GetDirectoryName(fullReportPath);
            if (!string.IsNullOrEmpty(reportDirectory))
            {
                Directory.CreateDirectory(reportDirectory);
            }

            File.WriteAllText(fullReportPath, report.ToJson());
            _reportWritten = true;
            GD.Print($"BattleLab runtime report written: {fullReportPath}");
            GD.Print($"BattleLab acceptance result: passed={report.Passed}, unexpectedErrors={report.UnexpectedErrors}.");
            GD.Print($"BattleLab requesting intentional exit with code {processExitCode}.");
            GetTree().Quit(processExitCode);
        }
        catch (Exception exception)
        {
            RecordFailure("ReportWrite", exception.ToString(), countAsUnexpectedError: true);
            TryWriteFailureReport(arguments, reportPath, processExitCode: 2);
        }
    }

    private void TryWriteFailureReport(BattleLabArguments arguments, string reportPath, int processExitCode)
    {
        try
        {
            var fullReportPath = Path.GetFullPath(reportPath);
            var reportDirectory = Path.GetDirectoryName(fullReportPath);
            if (!string.IsNullOrEmpty(reportDirectory))
            {
                Directory.CreateDirectory(reportDirectory);
            }

            var failureReport = CreateReport(arguments, passed: false);
            File.WriteAllText(fullReportPath, failureReport.ToJson());
            GD.PushError($"BattleLab wrote a failed report after the initial report write failed: {fullReportPath}");
        }
        catch (Exception secondException)
        {
            GD.PushError($"BattleLab could not write a failure report: {secondException}");
        }

        _reportWritten = true;
        GetTree().Quit(processExitCode);
    }

    private BattleLabReport CreateReport(BattleLabArguments arguments, bool passed)
    {
        var resolution = _resolution;
        var result = resolution?.Result;
        var transcript = resolution?.Transcript;
        var controller = _playbackController;
        var definition = _definition;

        return new BattleLabReport
        {
            Scenario = arguments.ScenarioId ?? BattleLabArguments.AcceptanceScenario,
            Scene = SceneIdentity,
            ScenePath = ScenePath,
            GodotVersion = GetGodotVersion(),
            BuildRuntimeIdentifier = GetBuildRuntimeIdentifier(),
            RuntimeIdentifier = RuntimeInformation.RuntimeIdentifier,
            ProjectIdentity = GetProjectSetting("application/config/name", "Warwrought"),
            ProjectVersion = GetProjectSetting("application/config/version", "0.1.0"),
            BattleId = result?.BattleId.Value ?? string.Empty,
            Seed = result?.Seed ?? 0,
            SimulationVersion = result is null ? string.Empty : $"{result.SimulationVersion.Major}.{result.SimulationVersion.Minor}",
            AuthoritativeInputDigest = transcript?.Header.CanonicalInputDigest ?? string.Empty,
            TranscriptDigest = transcript?.CanonicalDigest ?? string.Empty,
            ResultDigest = result?.CanonicalDigest ?? string.Empty,
            Digest = result?.CanonicalDigest ?? string.Empty,
            Result = result?.ResultType.ToString() ?? string.Empty,
            WinnerSideId = result?.WinnerSideId?.Value,
            TerminalTick = result?.TerminalTick.Value ?? 0,
            SurvivorCount = result?.Survivors.Count ?? 0,
            CasualtyCount = result?.Casualties.Count ?? 0,
            RetreatedUnitCount = result?.RetreatedUnits.Count ?? 0,
            SideAUnitCount = definition is null ? 0 : definition.GetSide(new BattleSideId("side.a")).Squads[0].Members.Count,
            SideBUnitCount = definition is null ? 0 : definition.GetSide(new BattleSideId("side.b")).Squads[0].Members.Count,
            TranscriptEventCount = transcript?.EventCount ?? 0,
            TranscriptKeyframeCount = transcript?.KeyframeCount ?? 0,
            TranscriptEventsConsumed = controller?.TranscriptEventsConsumed ?? 0,
            UnitsSpawned = controller?.UnitsSpawned ?? 0,
            MovementKeyframesConsumed = controller?.MovementKeyframesConsumed ?? 0,
            ContactEventsPresented = controller?.ContactEventsPresented ?? 0,
            AttackEventsPresented = controller?.AttackEventsPresented ?? 0,
            DamageEventsPresented = controller?.DamageEventsPresented ?? 0,
            DeathEventsPresented = controller?.DeathEventsPresented ?? 0,
            RemainsSpawned = controller?.RemainsSpawned ?? 0,
            RoutedFormationsShown = controller?.RoutedFormationsShown ?? 0,
            ControlTransitions = controller?.ControlTransitions ?? 0,
            ResultShown = controller?.ResultShown ?? false,
            PlaybackCompleted = controller?.PlaybackCompleted ?? false,
            UnexpectedErrors = _unexpectedErrors,
            Passed = passed && _unexpectedErrors == 0,
            FailureCategory = passed && _unexpectedErrors == 0 ? null : _failureCategory ?? "AcceptanceFailure",
            FailureMessage = passed && _unexpectedErrors == 0 ? null : _failureMessage ?? "BattleLab acceptance did not pass.",
        };
    }

    private void RecordFailure(string category, string message, bool countAsUnexpectedError)
    {
        if (countAsUnexpectedError)
        {
            _unexpectedErrors++;
        }

        _failureCategory ??= category;
        _failureMessage ??= message;
        GD.PushError($"BattleLab failure [{category}]: {message}");
    }

    private static string GetGodotVersion()
    {
        var versionInfo = Engine.GetVersionInfo();
        return versionInfo["string"].ToString();
    }

    private static string GetBuildRuntimeIdentifier()
    {
        var assemblyName = typeof(BattleLab).GetTypeInfo().Assembly.GetName();
        var version = assemblyName.Version?.ToString() ?? "development";
        return $"{assemblyName.Name ?? "Warwrought"}@{version}";
    }

    private static string GetProjectSetting(string settingName, string fallback)
    {
        var value = ProjectSettings.GetSetting(settingName, fallback).ToString();
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }
}
