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
/// Maintained production Phase 1 ScaleLab. It commits the deterministic 100v100 fixture,
/// resolves it once through CommittedBattleResolution, retains that result/transcript pair,
/// and hands the exact retained resolution to the existing production playback controller.
/// </summary>
public partial class BattleScaleLab : Node3D
{
    public const string SceneIdentity = "BattleScaleLab";
    public const string ScenePath = "res://scenes/Labs/BattleScaleLab.tscn";
    public const int ExpectedTotalUnitCount = BattleScaleFixtureFactory.UnitsPerSide * 2;

    private BattleScaleLabArguments? _arguments;
    private BattleDefinition? _definition;
    private CommittedBattleResolution? _committedBattle;
    private BattleResolution? _resolution;
    private BattleResult? _skipResult;
    private BattlePlaybackController? _playbackController;
    private BattlefieldTerrainView? _terrainView;
    private BattleCameraRig? _cameraRig;
    private BattlefieldDefinition? _battlefieldDefinition;
    private BattlefieldPresentationProjector? _presentationProjector;
    private int _unexpectedErrors;
    private string? _failureCategory;
    private string? _failureMessage;
    private bool _reportWritten;
    private bool _cameraControlsObserved;

    public override void _Ready()
    {
        try
        {
            _arguments = BattleScaleLabArguments.Parse(OS.GetCmdlineUserArgs());
            if (!_arguments.IsAcceptanceRequested)
            {
                InitializeProductionBattle();
                _playbackController!.Play();
                GD.Print($"BattleScaleLab ready: maintained production 100v100 transcript playback, profile={_battlefieldDefinition?.BattlefieldId.Value}, views={_playbackController.UnitsSpawned}, controls=WASD/wheel/R.");
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
                GD.PushError($"BattleScaleLab bootstrap exception: {exception}");
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

    private void RunAcceptance(BattleScaleLabArguments arguments)
    {
        if (!arguments.IsValid)
        {
            RecordFailure(
                "InvalidArguments",
                arguments.ValidationError ?? "BattleScaleLab acceptance arguments are invalid.",
                countAsUnexpectedError: false);
            CompleteAcceptance(arguments, passed: false, processExitCode: 1);
            return;
        }

        try
        {
            InitializeProductionBattle();

            // Exercise the same presentation controls exposed by the production HUD. These
            // calls affect only playback/camera presentation, never the committed resolution.
            _playbackController!.ResetPlayback();
            _playbackController.Pause();
            _playbackController.Play();
            _playbackController.SetSpeed(1.0);
            _playbackController.SetSpeed(2.0);
            _playbackController.SetSpeed(8.0);
            ObserveCameraControls();
        }
        catch (Exception exception)
        {
            RecordFailure("BattleScaleLabInitialization", exception.ToString(), countAsUnexpectedError: true);
            CompleteAcceptance(arguments, passed: false, processExitCode: 1);
        }
    }

    private void InitializeProductionBattle()
    {
        ValidateProductionSceneStructure();

        var fixtureInput = BattleScaleFixtureFactory.Create100v100FixtureInput();
        if (!BattleDefinition.TryCommit(fixtureInput, out var committedDefinition, out var validation) || committedDefinition is null)
        {
            throw new BattleDefinitionValidationException(validation);
        }

        if (committedDefinition.TotalUnitCount != ExpectedTotalUnitCount)
        {
            throw new InvalidOperationException($"ScaleLab committed {committedDefinition.TotalUnitCount} units; expected {ExpectedTotalUnitCount}.");
        }

        _definition = committedDefinition;
        _committedBattle = CommittedBattleResolution.Commit(_definition);
        _skipResult = _committedBattle.SkipToResult();
        _resolution = _committedBattle.WatchResolution;
        _battlefieldDefinition = SelectBattlefield(_arguments?.BattlefieldProfileId);
        _presentationProjector = new BattlefieldPresentationProjector(_battlefieldDefinition);
        _terrainView = GetNode<BattlefieldTerrainView>("BattlefieldRoot/TerrainRoot");
        _terrainView.Configure(_presentationProjector);
        _cameraRig = GetNode<BattleCameraRig>("BattlefieldRoot/CameraRig");
        _cameraRig.Configure(_battlefieldDefinition);
        _playbackController = GetNode<BattlePlaybackController>("BattlePlaybackController");
        _playbackController.Initialize(_resolution, _presentationProjector);
        BindControlSurface(_playbackController);
        BindCameraControlSurface(_cameraRig);
        UpdateTerrainHud();
    }

    private void ValidateProductionSceneStructure()
    {
        _ = GetNode<Node3D>("BattlefieldRoot");
        _ = GetNode<Node3D>("BattlefieldRoot/TerrainRoot");
        _ = GetNode<BattlefieldTerrainView>("BattlefieldRoot/TerrainRoot");
        _ = GetNode<MeshInstance3D>("BattlefieldRoot/TerrainRoot/Ground");
        _ = GetNode<Node3D>("BattlefieldRoot/TerrainRoot/FoliageRoot");
        _ = GetNode<Node3D>("BattlefieldRoot/TerrainRoot/PropsRoot");
        _ = GetNode<Node3D>("BattlefieldRoot/UnitRoot");
        _ = GetNode<Node3D>("BattlefieldRoot/RemainsRoot");
        _ = GetNode<Node3D>("BattlefieldRoot/EffectsRoot");
        _ = GetNode<Node3D>("BattlefieldRoot/CameraRig");
        _ = GetNode<BattleCameraRig>("BattlefieldRoot/CameraRig");
        _ = GetNode<Camera3D>("BattlefieldRoot/CameraRig/Camera3D");
        _ = GetNode<BattlePlaybackController>("BattlePlaybackController");
        _ = GetNode<CanvasLayer>("BattleHUD");
        _ = GetNode<Control>("BattleHUD/Panel");
        _ = GetNode<Button>("BattleHUD/Panel/Content/Controls/ResetButton");
        _ = GetNode<Button>("BattleHUD/Panel/Content/Controls/PauseButton");
        _ = GetNode<Button>("BattleHUD/Panel/Content/Controls/PlayButton");
        _ = GetNode<Button>("BattleHUD/Panel/Content/Controls/Speed1Button");
        _ = GetNode<Button>("BattleHUD/Panel/Content/Controls/Speed2Button");
        _ = GetNode<Button>("BattleHUD/Panel/Content/Controls/PanLeftButton");
        _ = GetNode<Button>("BattleHUD/Panel/Content/Controls/PanRightButton");
        _ = GetNode<Button>("BattleHUD/Panel/Content/Controls/ZoomInButton");
        _ = GetNode<Button>("BattleHUD/Panel/Content/Controls/ZoomOutButton");
        _ = GetNode<Button>("BattleHUD/Panel/Content/Controls/CameraResetButton");
        _ = GetNode<Label>("BattleHUD/Panel/Content/Terrain");
    }

    private void BindControlSurface(BattlePlaybackController controller)
    {
        GetNode<Button>("BattleHUD/Panel/Content/Controls/ResetButton").Pressed += controller.ResetPlayback;
        GetNode<Button>("BattleHUD/Panel/Content/Controls/PauseButton").Pressed += controller.Pause;
        GetNode<Button>("BattleHUD/Panel/Content/Controls/PlayButton").Pressed += controller.Play;
        GetNode<Button>("BattleHUD/Panel/Content/Controls/Speed1Button").Pressed += () => controller.SetSpeed(1.0);
        GetNode<Button>("BattleHUD/Panel/Content/Controls/Speed2Button").Pressed += () => controller.SetSpeed(2.0);
    }

    private void BindCameraControlSurface(BattleCameraRig cameraRig)
    {
        GetNode<Button>("BattleHUD/Panel/Content/Controls/PanLeftButton").Pressed += () => cameraRig.Pan(-BattleCameraRig.PanStepWorldUnits, 0.0f);
        GetNode<Button>("BattleHUD/Panel/Content/Controls/PanRightButton").Pressed += () => cameraRig.Pan(BattleCameraRig.PanStepWorldUnits, 0.0f);
        GetNode<Button>("BattleHUD/Panel/Content/Controls/ZoomInButton").Pressed += () => cameraRig.ZoomBy(-4.0f);
        GetNode<Button>("BattleHUD/Panel/Content/Controls/ZoomOutButton").Pressed += () => cameraRig.ZoomBy(4.0f);
        GetNode<Button>("BattleHUD/Panel/Content/Controls/CameraResetButton").Pressed += cameraRig.ResetToBattleFraming;
    }

    private void ObserveCameraControls()
    {
        if (_cameraRig is null || _resolution is null || _playbackController is null)
        {
            throw new InvalidOperationException("ScaleLab camera observation requires the configured camera and retained resolution.");
        }

        var originalResultDigest = _resolution.Result.CanonicalDigest;
        var originalTranscriptDigest = _resolution.Transcript.CanonicalDigest;
        _cameraRig.Pan(BattleCameraRig.PanStepWorldUnits, 0.0f);
        _cameraRig.ZoomBy(-4.0f);
        _cameraRig.ResetToBattleFraming();
        _cameraControlsObserved = _cameraRig.IsOrthographic &&
                                  _cameraRig.CurrentOrthographicSize > 0.0f &&
                                  _resolution.Result.CanonicalDigest == originalResultDigest &&
                                  _resolution.Transcript.CanonicalDigest == originalTranscriptDigest &&
                                  _playbackController.UnitsSpawned == ExpectedTotalUnitCount;
        if (!_cameraControlsObserved)
        {
            throw new InvalidOperationException("ScaleLab camera controls failed to restore a valid orthographic frame or changed authoritative resolution identity.");
        }
    }

    private static BattlefieldDefinition SelectBattlefield(string? profileId)
    {
        return string.Equals(profileId, BattleScaleLabArguments.BroadHighlandProfile, StringComparison.Ordinal)
            ? BattlefieldFixtureFactory.CreateM2BroadHighlandProfile()
            : BattlefieldFixtureFactory.CreateM2OpenMeadowProfile();
    }

    private void UpdateTerrainHud()
    {
        if (_battlefieldDefinition is null || _terrainView is null)
        {
            return;
        }

        GetNode<Label>("BattleHUD/Panel/Content/Terrain").Text =
            $"M2.3 SCALELAB  //  100v100 / 200 views  //  {_battlefieldDefinition.BattlefieldId.Value}  //  mesh {_terrainView.MeshTriangleCount} facets  //  foliage {_terrainView.FoliagePlacementCount}  //  pan/zoom/reset: WASD, wheel, R";
    }

    private void CompleteAcceptance(BattleScaleLabArguments arguments, bool passed, int processExitCode)
    {
        if (_reportWritten)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(arguments.ReportPath))
        {
            GD.PushError("BattleScaleLab acceptance cannot complete without an explicit --report path.");
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
            GD.Print($"BattleScaleLab runtime report written: {fullReportPath}");
            GD.Print($"BattleScaleLab acceptance result: passed={report.Passed}, unexpectedErrors={report.UnexpectedErrors}, spawned={report.UnitsSpawned}.");
            GD.Print($"BattleScaleLab requesting intentional exit with code {processExitCode}.");
            GetTree().Quit(processExitCode);
        }
        catch (Exception exception)
        {
            RecordFailure("ReportWrite", exception.ToString(), countAsUnexpectedError: true);
            TryWriteFailureReport(arguments, reportPath, processExitCode: 2);
        }
    }

    private void TryWriteFailureReport(BattleScaleLabArguments arguments, string reportPath, int processExitCode)
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
            GD.PushError($"BattleScaleLab wrote a failed report after the initial report write failed: {fullReportPath}");
        }
        catch (Exception secondException)
        {
            GD.PushError($"BattleScaleLab could not write a failure report: {secondException}");
        }

        _reportWritten = true;
        GetTree().Quit(processExitCode);
    }

    private BattleScaleLabReport CreateReport(BattleScaleLabArguments arguments, bool passed)
    {
        var resolution = _resolution;
        var result = resolution?.Result;
        var transcript = resolution?.Transcript;
        var controller = _playbackController;
        var watchedResolution = controller?.PresentedResolution;
        var watchedResult = controller?.PresentedResult;
        var resolutionIdentityShared = _committedBattle is not null &&
                                       watchedResolution is not null &&
                                       ReferenceEquals(_committedBattle.Resolution, watchedResolution);
        var skipWatchEquivalent = _skipResult is not null &&
                                  watchedResult is not null &&
                                  resolutionIdentityShared &&
                                  _skipResult.IsExactlyEqualTo(watchedResult);
        var actualSideAUnitCount = _definition?.GetSide(new BattleSideId("side.a")).Squads[0].Members.Count ?? 0;
        var actualSideBUnitCount = _definition?.GetSide(new BattleSideId("side.b")).Squads[0].Members.Count ?? 0;

        return new BattleScaleLabReport
        {
            Scenario = arguments.ScenarioId ?? BattleScaleLabArguments.AcceptanceScenario,
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
            Result = result?.ResultType.ToString() ?? string.Empty,
            ResolutionSourceClassification = BattleScaleFixtureFactory.SourceClassification,
            AuthoritativeResolutionRetained = _committedBattle is not null &&
                                              _resolution is not null &&
                                              ReferenceEquals(_committedBattle.Resolution, _resolution),
            ResolutionIdentityShared = resolutionIdentityShared,
            SkipWatchEquivalent = skipWatchEquivalent,
            RequestedUnitsPerSide = BattleScaleFixtureFactory.UnitsPerSide,
            ExpectedTotalUnitCount = ExpectedTotalUnitCount,
            ActualSideAUnitCount = actualSideAUnitCount,
            ActualSideBUnitCount = actualSideBUnitCount,
            ActualTotalUnitCount = actualSideAUnitCount + actualSideBUnitCount,
            UnitsSpawned = controller?.UnitsSpawned ?? 0,
            ProjectedUnitCount = controller?.UnitProjectionCount ?? 0,
            TranscriptEventCount = transcript?.EventCount ?? 0,
            TranscriptKeyframeCount = transcript?.KeyframeCount ?? 0,
            TranscriptEventsConsumed = controller?.TranscriptEventsConsumed ?? 0,
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
            BattlefieldProfile = _battlefieldDefinition?.BattlefieldId.Value ?? string.Empty,
            BattlefieldSeed = _battlefieldDefinition?.Seed ?? 0,
            BattlefieldDigest = _battlefieldDefinition?.CanonicalDigest ?? string.Empty,
            TerrainMeshTriangleCount = _terrainView?.MeshTriangleCount ?? 0,
            TerrainMeshVertexCount = _terrainView?.MeshVertexCount ?? 0,
            TerrainMeshSamplerAgreement = _terrainView?.MeshSamplerAgreement ?? false,
            FoliagePlacementCount = _terrainView?.FoliagePlacementCount ?? 0,
            FoliageDigest = _terrainView?.FoliageDigest ?? string.Empty,
            PropCount = _terrainView?.PropCount ?? 0,
            CameraOrthographic = _cameraRig?.IsOrthographic ?? false,
            CameraPanOperations = _cameraRig?.PanOperations ?? 0,
            CameraZoomOperations = _cameraRig?.ZoomOperations ?? 0,
            CameraResetOperations = _cameraRig?.ResetOperations ?? 0,
            CameraControlsObserved = _cameraControlsObserved,
            UnexpectedErrors = _unexpectedErrors,
            Passed = passed && _unexpectedErrors == 0,
            FailureCategory = passed && _unexpectedErrors == 0 ? null : _failureCategory ?? "AcceptanceFailure",
            FailureMessage = passed && _unexpectedErrors == 0 ? null : _failureMessage ?? "BattleScaleLab acceptance did not pass.",
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
        GD.PushError($"BattleScaleLab failure [{category}]: {message}");
    }

    private static string GetGodotVersion()
    {
        var versionInfo = Engine.GetVersionInfo();
        return versionInfo["string"].ToString();
    }

    private static string GetBuildRuntimeIdentifier()
    {
        var assemblyName = typeof(BattleScaleLab).GetTypeInfo().Assembly.GetName();
        var version = assemblyName.Version?.ToString() ?? "development";
        return $"{assemblyName.Name ?? "Warwrought"}@{version}";
    }

    private static string GetProjectSetting(string settingName, string fallback)
    {
        var value = ProjectSettings.GetSetting(settingName, fallback).ToString();
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }
}
