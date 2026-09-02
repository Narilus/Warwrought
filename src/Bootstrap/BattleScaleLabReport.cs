using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Warwrought.Battle.Model;
using Warwrought.Presentation.Battle;

namespace Warwrought.Bootstrap;

/// <summary>
/// Runtime-owned Phase 1 ScaleLab evidence. The report is populated by the maintained
/// production scene after it has retained the real authoritative resolution and observed
/// production transcript presentation.
/// </summary>
public sealed class BattleScaleLabReport
{
    public const int CurrentSchemaVersion = 1;
    public const string AuthoritativeRealResolverSource = BattleScaleFixtureFactory.SourceClassification;
    public const string SyntheticPresentationSource = BattleScalePresentationStressFactory.SourceClassification;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    public string Scenario { get; init; } = string.Empty;
    public string Scene { get; init; } = string.Empty;
    public string ScenePath { get; init; } = string.Empty;
    public string GodotVersion { get; init; } = string.Empty;
    public string BuildRuntimeIdentifier { get; init; } = string.Empty;
    public string RuntimeIdentifier { get; init; } = string.Empty;
    public string ProjectIdentity { get; init; } = string.Empty;
    public string ProjectVersion { get; init; } = string.Empty;
    public string BattleId { get; init; } = string.Empty;
    public ulong Seed { get; init; }
    public string SimulationVersion { get; init; } = string.Empty;
    public string AuthoritativeInputDigest { get; init; } = string.Empty;
    public string SourceIdentityDigest { get; init; } = string.Empty;
    public string TranscriptDigest { get; init; } = string.Empty;
    public string ResultDigest { get; init; } = string.Empty;
    public string Result { get; init; } = string.Empty;
    public string ResolutionSourceClassification { get; init; } = string.Empty;
    public bool AuthoritativeResolutionRetained { get; init; }
    public bool ResolutionIdentityShared { get; init; }
    public bool SkipWatchEquivalent { get; init; }
    public int RequestedUnitsPerSide { get; init; }
    public int ExpectedTotalUnitCount { get; init; }
    public int ActualSideAUnitCount { get; init; }
    public int ActualSideBUnitCount { get; init; }
    public int ActualTotalUnitCount { get; init; }
    public int UnitsSpawned { get; init; }
    public int ProjectedUnitCount { get; init; }
    public int ActiveUnitViewCount { get; init; }
    public int ActiveRemainsViewCount { get; init; }
    public int ActiveEffectViewCount { get; init; }
    public int RelevantActiveNodeCount { get; init; }
    public int PeakRelevantActiveNodeCount { get; init; }
    public int EffectsSpawned { get; init; }
    public double ScenePresentationSpawnElapsedMilliseconds { get; init; }
    public string FrameSampleSource { get; init; } = string.Empty;
    public double ProfilingPlaybackSpeed { get; init; }
    public string WarmupPolicy { get; init; } = string.Empty;
    public int FrameSampleCount { get; init; }
    public double MeasuredSampleWindowDurationMilliseconds { get; init; }
    public int WarmupExcludedSampleCount { get; init; }
    public double WarmupExcludedDurationMilliseconds { get; init; }
    public double MedianPlaybackFrameMilliseconds { get; init; }
    public double P95PlaybackFrameMilliseconds { get; init; }
    public bool MemoryMeasurementAvailable { get; init; }
    public string MemoryMeasurementSource { get; init; } = string.Empty;
    public string MemoryMeasurementUnit { get; init; } = string.Empty;
    public long MemoryBytes { get; init; }
    public int TranscriptEventCount { get; init; }
    public int TranscriptKeyframeCount { get; init; }
    public int TranscriptEventsConsumed { get; init; }
    public int MovementKeyframesConsumed { get; init; }
    public int ContactEventsPresented { get; init; }
    public int AttackEventsPresented { get; init; }
    public int DamageEventsPresented { get; init; }
    public int DeathEventsPresented { get; init; }
    public int RemainsSpawned { get; init; }
    public int RoutedFormationsShown { get; init; }
    public int ControlTransitions { get; init; }
    public bool ResultShown { get; init; }
    public bool PlaybackCompleted { get; init; }
    public string BattlefieldProfile { get; init; } = string.Empty;
    public ulong BattlefieldSeed { get; init; }
    public string BattlefieldDigest { get; init; } = string.Empty;
    public int TerrainMeshTriangleCount { get; init; }
    public int TerrainMeshVertexCount { get; init; }
    public bool TerrainMeshSamplerAgreement { get; init; }
    public int FoliagePlacementCount { get; init; }
    public string FoliageDigest { get; init; } = string.Empty;
    public int PropCount { get; init; }
    public bool CameraOrthographic { get; init; }
    public int CameraPanOperations { get; init; }
    public int CameraZoomOperations { get; init; }
    public int CameraResetOperations { get; init; }
    public bool CameraControlsObserved { get; init; }
    public int UnexpectedErrors { get; init; }
    public bool Passed { get; init; }
    public string? FailureCategory { get; init; }
    public string? FailureMessage { get; init; }

    [JsonIgnore]
    public bool IsCleanPass => Passed && UnexpectedErrors == 0 && Validate().Count == 0;

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (SchemaVersion != CurrentSchemaVersion)
        {
            errors.Add($"Unsupported schema version {SchemaVersion}.");
        }

        foreach (var required in new[]
        {
            (Scenario, nameof(Scenario)),
            (Scene, nameof(Scene)),
            (ScenePath, nameof(ScenePath)),
            (GodotVersion, nameof(GodotVersion)),
            (BuildRuntimeIdentifier, nameof(BuildRuntimeIdentifier)),
            (RuntimeIdentifier, nameof(RuntimeIdentifier)),
            (ProjectIdentity, nameof(ProjectIdentity)),
            (ProjectVersion, nameof(ProjectVersion)),
        })
        {
            if (string.IsNullOrWhiteSpace(required.Item1))
            {
                errors.Add($"{required.Item2} is required.");
            }
        }

        if (UnexpectedErrors < 0)
        {
            errors.Add("UnexpectedErrors cannot be negative.");
        }

        if (Passed && UnexpectedErrors != 0)
        {
            errors.Add("A passing report cannot contain unexpected errors.");
        }

        if (!Passed)
        {
            if (string.IsNullOrWhiteSpace(FailureCategory))
            {
                errors.Add("A failed report requires a failure category.");
            }

            if (string.IsNullOrWhiteSpace(FailureMessage))
            {
                errors.Add("A failed report requires a failure message.");
            }

            return errors;
        }

        foreach (var required in new[]
        {
            (BattleId, nameof(BattleId)),
            (SimulationVersion, nameof(SimulationVersion)),
            (SourceIdentityDigest, nameof(SourceIdentityDigest)),
            (TranscriptDigest, nameof(TranscriptDigest)),
            (ResultDigest, nameof(ResultDigest)),
            (Result, nameof(Result)),
            (ResolutionSourceClassification, nameof(ResolutionSourceClassification)),
            (BattlefieldProfile, nameof(BattlefieldProfile)),
            (BattlefieldDigest, nameof(BattlefieldDigest)),
        })
        {
            if (string.IsNullOrWhiteSpace(required.Item1))
            {
                errors.Add($"{required.Item2} is required for a passing ScaleLab report.");
            }
        }

        if (ResolutionSourceClassification != AuthoritativeRealResolverSource &&
            ResolutionSourceClassification != SyntheticPresentationSource)
        {
            errors.Add($"ResolutionSourceClassification must be '{AuthoritativeRealResolverSource}' or '{SyntheticPresentationSource}'.");
        }

        var isAuthoritativeScenario = ResolutionSourceClassification == AuthoritativeRealResolverSource;
        var expectedUnitsPerSide = Scenario switch
        {
            BattleScaleLabArguments.AcceptanceScenario => BattleScaleFixtureFactory.UnitsPerSide,
            BattleScaleLabArguments.Presentation300Scenario => 300,
            BattleScaleLabArguments.Presentation500Scenario => 500,
            _ => 0,
        };
        if (expectedUnitsPerSide == 0)
        {
            errors.Add($"Unsupported ScaleLab scenario '{Scenario}'.");
        }

        if (isAuthoritativeScenario && string.IsNullOrWhiteSpace(AuthoritativeInputDigest))
        {
            errors.Add("AuthoritativeInputDigest is required for the real-resolver ScaleLab scenario.");
        }

        if (!isAuthoritativeScenario && !string.IsNullOrWhiteSpace(AuthoritativeInputDigest))
        {
            errors.Add("Synthetic presentation ScaleLab scenarios must not claim an authoritative input digest.");
        }

        if (isAuthoritativeScenario && (!AuthoritativeResolutionRetained || !ResolutionIdentityShared || !SkipWatchEquivalent))
        {
            errors.Add("ScaleLab must retain one committed authoritative resolution shared by skip and watch.");
        }

        if (!isAuthoritativeScenario && (AuthoritativeResolutionRetained || ResolutionIdentityShared || SkipWatchEquivalent))
        {
            errors.Add("Synthetic presentation ScaleLab scenarios must not claim authoritative skip/watch ownership.");
        }

        if (expectedUnitsPerSide > 0 && (RequestedUnitsPerSide != expectedUnitsPerSide ||
            ExpectedTotalUnitCount != expectedUnitsPerSide * 2 ||
            ActualSideAUnitCount != expectedUnitsPerSide ||
            ActualSideBUnitCount != expectedUnitsPerSide ||
            ActualTotalUnitCount != ExpectedTotalUnitCount ||
            UnitsSpawned != ExpectedTotalUnitCount))
        {
            errors.Add($"ScaleLab must request, construct, and spawn exactly {expectedUnitsPerSide} units per side / {expectedUnitsPerSide * 2} total.");
        }

        if (TranscriptEventCount <= 0 || TranscriptKeyframeCount <= 0 ||
            TranscriptEventsConsumed != TranscriptEventCount ||
            MovementKeyframesConsumed != TranscriptKeyframeCount)
        {
            errors.Add("ScaleLab acceptance requires complete authoritative transcript observation.");
        }

        if (ContactEventsPresented <= 0 || AttackEventsPresented <= 0 || DamageEventsPresented <= 0 ||
            DeathEventsPresented <= 0 || RemainsSpawned <= 0 || RoutedFormationsShown <= 0 ||
            ProjectedUnitCount <= 0 || !ResultShown || !PlaybackCompleted)
        {
            errors.Add("ScaleLab acceptance requires non-zero production presentation and completed playback evidence.");
        }

        if (ActiveUnitViewCount != ExpectedTotalUnitCount || RelevantActiveNodeCount <= 0 ||
            PeakRelevantActiveNodeCount < RelevantActiveNodeCount || EffectsSpawned <= 0 ||
            ScenePresentationSpawnElapsedMilliseconds < 0.0 ||
            string.IsNullOrWhiteSpace(FrameSampleSource) || ProfilingPlaybackSpeed <= 0.0 ||
            string.IsNullOrWhiteSpace(WarmupPolicy) || FrameSampleCount <= 0 ||
            MeasuredSampleWindowDurationMilliseconds <= 0.0 || WarmupExcludedSampleCount < 0 ||
            WarmupExcludedDurationMilliseconds < 0.0 || MedianPlaybackFrameMilliseconds < 0.0 ||
            P95PlaybackFrameMilliseconds < MedianPlaybackFrameMilliseconds)
        {
            errors.Add("ScaleLab acceptance requires bounded production frame samples, warm-up accounting, active-node counts, and median/p95 metrics.");
        }

        if (!double.IsFinite(ScenePresentationSpawnElapsedMilliseconds) ||
            !double.IsFinite(ProfilingPlaybackSpeed) ||
            !double.IsFinite(MeasuredSampleWindowDurationMilliseconds) ||
            !double.IsFinite(WarmupExcludedDurationMilliseconds) ||
            !double.IsFinite(MedianPlaybackFrameMilliseconds) ||
            !double.IsFinite(P95PlaybackFrameMilliseconds))
        {
            errors.Add("ScaleLab timing metrics must be finite.");
        }

        if (MemoryMeasurementAvailable)
        {
            if (MemoryBytes < 0 || string.IsNullOrWhiteSpace(MemoryMeasurementSource) ||
                string.IsNullOrWhiteSpace(MemoryMeasurementUnit))
            {
                errors.Add("Available ScaleLab memory measurement requires non-negative bytes and an explicit source/unit.");
            }
        }

        if (string.IsNullOrWhiteSpace(BattlefieldDigest) || BattlefieldSeed == 0 ||
            TerrainMeshTriangleCount <= 0 || TerrainMeshVertexCount <= 0 || !TerrainMeshSamplerAgreement ||
            FoliagePlacementCount <= 0 || string.IsNullOrWhiteSpace(FoliageDigest))
        {
            errors.Add("ScaleLab acceptance requires the reused M2 terrain/presentation evidence.");
        }

        if (!CameraOrthographic || CameraPanOperations <= 0 || CameraZoomOperations <= 0 ||
            CameraResetOperations <= 0 || !CameraControlsObserved)
        {
            errors.Add("ScaleLab acceptance requires observed orthographic pan, zoom, and reset controls.");
        }

        return errors;
    }
}
