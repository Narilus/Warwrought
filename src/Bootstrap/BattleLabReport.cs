using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Warwrought.Bootstrap;

/// <summary>
/// Runtime-owned BattleLab evidence. Presentation counters are populated by the production
/// BattleLab controller after it observes real transcript events and Sprite3D/remains views;
/// no external wrapper constructs or fills this report.
/// </summary>
public sealed class BattleLabReport
{
    public const int CurrentSchemaVersion = 1;

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

    public string TranscriptDigest { get; init; } = string.Empty;

    public string ResultDigest { get; init; } = string.Empty;

    public string Digest { get; init; } = string.Empty;

    public string SkipResultDigest { get; init; } = string.Empty;

    public string WatchedResultDigest { get; init; } = string.Empty;

    public string Result { get; init; } = string.Empty;

    public string SkipResult { get; init; } = string.Empty;

    public string WatchedResult { get; init; } = string.Empty;

    public bool SkipWatchEquivalent { get; init; }

    public bool ResolutionIdentityShared { get; init; }

    public string? WinnerSideId { get; init; }

    public long TerminalTick { get; init; }

    public int SurvivorCount { get; init; }

    public int CasualtyCount { get; init; }

    public int RetreatedUnitCount { get; init; }

    public int SideAUnitCount { get; init; }

    public int SideBUnitCount { get; init; }

    public int TranscriptEventCount { get; init; }

    public int TranscriptKeyframeCount { get; init; }

    public int TranscriptEventsConsumed { get; init; }

    public int UnitsSpawned { get; init; }

    public int MovementKeyframesConsumed { get; init; }

    public int ContactEventsPresented { get; init; }

    public int AttackEventsPresented { get; init; }

    public int DamageEventsPresented { get; init; }

    public int DeathEventsPresented { get; init; }

    public int RemainsSpawned { get; init; }

    public int RoutedFormationsShown { get; init; }

    public int ControlTransitions { get; init; }

    public double HeadlessResolutionElapsedMilliseconds { get; init; }

    public double NominalTranscriptDurationMilliseconds { get; init; }

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

    public int ProjectedUnitCount { get; init; }

    public int ProjectedRemainsCount { get; init; }

    public int ProjectedEffectCount { get; init; }

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

        AddRequired(errors, Scenario, nameof(Scenario));
        AddRequired(errors, Scene, nameof(Scene));
        AddRequired(errors, ScenePath, nameof(ScenePath));
        AddRequired(errors, GodotVersion, nameof(GodotVersion));
        AddRequired(errors, BuildRuntimeIdentifier, nameof(BuildRuntimeIdentifier));
        AddRequired(errors, RuntimeIdentifier, nameof(RuntimeIdentifier));
        AddRequired(errors, ProjectIdentity, nameof(ProjectIdentity));
        AddRequired(errors, ProjectVersion, nameof(ProjectVersion));

        if (UnexpectedErrors < 0)
        {
            errors.Add("UnexpectedErrors cannot be negative.");
        }

        if (Passed && UnexpectedErrors != 0)
        {
            errors.Add("A passing report cannot contain unexpected errors.");
        }

        if (Passed && (!string.IsNullOrWhiteSpace(FailureCategory) || !string.IsNullOrWhiteSpace(FailureMessage)))
        {
            errors.Add("A passing report cannot contain a failure category or message.");
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

        AddRequired(errors, BattleId, nameof(BattleId));
        AddRequired(errors, SimulationVersion, nameof(SimulationVersion));
        AddRequired(errors, AuthoritativeInputDigest, nameof(AuthoritativeInputDigest));
        AddRequired(errors, TranscriptDigest, nameof(TranscriptDigest));
        AddRequired(errors, ResultDigest, nameof(ResultDigest));
        AddRequired(errors, Digest, nameof(Digest));
        AddRequired(errors, Result, nameof(Result));
        AddRequired(errors, SkipResultDigest, nameof(SkipResultDigest));
        AddRequired(errors, WatchedResultDigest, nameof(WatchedResultDigest));
        AddRequired(errors, SkipResult, nameof(SkipResult));
        AddRequired(errors, WatchedResult, nameof(WatchedResult));

        if (Seed == 0)
        {
            errors.Add("Seed must be non-zero for the canonical M1 fixture acceptance.");
        }

        if (TerminalTick < 0)
        {
            errors.Add("TerminalTick cannot be negative.");
        }

        if (SideAUnitCount <= 0 || SideBUnitCount <= 0)
        {
            errors.Add("Both authoritative sides must contain units.");
        }

        if (SurvivorCount < 0 || CasualtyCount < 0 || RetreatedUnitCount < 0)
        {
            errors.Add("Authoritative survivor, casualty, and retreat counts cannot be negative.");
        }

        if (SurvivorCount + CasualtyCount != SideAUnitCount + SideBUnitCount)
        {
            errors.Add("Authoritative survivor and casualty counts must cover both fixture sides.");
        }

        if (RetreatedUnitCount > SurvivorCount)
        {
            errors.Add("Retreated units must be a subset of authoritative survivors.");
        }

        if (TranscriptEventCount <= 0 || TranscriptKeyframeCount <= 0)
        {
            errors.Add("The authoritative transcript must contain events and keyframes.");
        }

        if (TranscriptEventsConsumed != TranscriptEventCount)
        {
            errors.Add("Runtime event consumption did not reach the complete transcript.");
        }

        if (MovementKeyframesConsumed != TranscriptKeyframeCount)
        {
            errors.Add("Runtime keyframe consumption did not reach the complete transcript.");
        }

        if (UnitsSpawned <= 0 || ContactEventsPresented <= 0 || AttackEventsPresented <= 0 ||
            DamageEventsPresented <= 0 || DeathEventsPresented <= 0 || RemainsSpawned <= 0 ||
            RoutedFormationsShown <= 0 || ControlTransitions <= 0)
        {
            errors.Add("BattleLab acceptance requires non-zero observed presentation and control counters.");
        }

        if (!ResultShown || !PlaybackCompleted)
        {
            errors.Add("BattleLab acceptance requires the terminal transcript result to be shown after playback completion.");
        }

        if (ResultDigest != Digest)
        {
            errors.Add("ResultDigest and Digest must identify the same authoritative result.");
        }

        if (!ResolutionIdentityShared)
        {
            errors.Add("Skip and watch must receive the same committed resolution identity.");
        }

        if (!SkipWatchEquivalent)
        {
            errors.Add("Skip and watch authoritative outcomes are not exactly equal.");
        }

        if (string.Equals(Scenario, BattleLabArguments.OpenMeadowScenario, StringComparison.Ordinal) ||
            string.Equals(Scenario, BattleLabArguments.BroadHighlandScenario, StringComparison.Ordinal))
        {
            AddRequired(errors, BattlefieldProfile, nameof(BattlefieldProfile));
            AddRequired(errors, BattlefieldDigest, nameof(BattlefieldDigest));
            if (BattlefieldSeed == 0)
            {
                errors.Add("BattlefieldSeed must be non-zero for an M2 terrain acceptance.");
            }

            if (TerrainMeshTriangleCount <= 0 || TerrainMeshVertexCount <= 0 || !TerrainMeshSamplerAgreement)
            {
                errors.Add("M2 terrain acceptance requires a non-empty mesh whose vertices agree with the sampler.");
            }

            if (FoliagePlacementCount <= 0 || string.IsNullOrWhiteSpace(FoliageDigest) || PropCount < 0)
            {
                errors.Add("M2 terrain acceptance requires deterministic foliage/prop evidence.");
            }

            if (ProjectedUnitCount <= 0 || ProjectedRemainsCount <= 0 || ProjectedEffectCount <= 0)
            {
                errors.Add("M2 terrain acceptance requires sampler-derived unit, remains, and effect projections.");
            }

            if (!CameraOrthographic || CameraPanOperations <= 0 || CameraZoomOperations <= 0 || CameraResetOperations <= 0 || !CameraControlsObserved)
            {
                errors.Add("M2 terrain acceptance requires observed orthographic pan, zoom, and reset controls.");
            }
        }

        if (!string.Equals(SkipResult, Result, StringComparison.Ordinal) ||
            !string.Equals(WatchedResult, Result, StringComparison.Ordinal))
        {
            errors.Add("Skip, watched, and authoritative result classifications must match.");
        }

        if (!string.Equals(SkipResultDigest, ResultDigest, StringComparison.Ordinal) ||
            !string.Equals(WatchedResultDigest, ResultDigest, StringComparison.Ordinal))
        {
            errors.Add("Skip, watched, and authoritative result digests must match.");
        }

        if (double.IsNaN(HeadlessResolutionElapsedMilliseconds) ||
            double.IsInfinity(HeadlessResolutionElapsedMilliseconds) ||
            HeadlessResolutionElapsedMilliseconds < 0.0)
        {
            errors.Add("HeadlessResolutionElapsedMilliseconds must be a finite non-negative value.");
        }

        if (double.IsNaN(NominalTranscriptDurationMilliseconds) ||
            double.IsInfinity(NominalTranscriptDurationMilliseconds) ||
            NominalTranscriptDurationMilliseconds <= 0.0)
        {
            errors.Add("NominalTranscriptDurationMilliseconds must be a finite positive value.");
        }
        else if (HeadlessResolutionElapsedMilliseconds >= NominalTranscriptDurationMilliseconds / 5.0)
        {
            errors.Add("Headless resolution must complete in less than one fifth of nominal 1x transcript duration.");
        }

        return errors;
    }

    private static void AddRequired(List<string> errors, string value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"{fieldName} is required.");
        }
    }
}
