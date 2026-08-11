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

    public string Result { get; init; } = string.Empty;

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

    public bool ResultShown { get; init; }

    public bool PlaybackCompleted { get; init; }

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
