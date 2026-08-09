using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Warwrought.Bootstrap;

public sealed class BootstrapLabReport
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

        AddRequiredFieldError(errors, Scenario, nameof(Scenario));
        AddRequiredFieldError(errors, Scene, nameof(Scene));
        AddRequiredFieldError(errors, ScenePath, nameof(ScenePath));
        AddRequiredFieldError(errors, GodotVersion, nameof(GodotVersion));
        AddRequiredFieldError(errors, BuildRuntimeIdentifier, nameof(BuildRuntimeIdentifier));
        AddRequiredFieldError(errors, RuntimeIdentifier, nameof(RuntimeIdentifier));
        AddRequiredFieldError(errors, ProjectIdentity, nameof(ProjectIdentity));
        AddRequiredFieldError(errors, ProjectVersion, nameof(ProjectVersion));

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

        if (!Passed && string.IsNullOrWhiteSpace(FailureCategory))
        {
            errors.Add("A failed report requires a failure category.");
        }

        if (!Passed && string.IsNullOrWhiteSpace(FailureMessage))
        {
            errors.Add("A failed report requires a failure message.");
        }

        return errors;
    }

    public static BootstrapLabReport Create(
        string scenario,
        string scene,
        string scenePath,
        string godotVersion,
        string buildRuntimeIdentifier,
        string runtimeIdentifier,
        string projectIdentity,
        string projectVersion,
        int unexpectedErrors,
        bool passed,
        string? failureCategory,
        string? failureMessage)
    {
        return new BootstrapLabReport
        {
            Scenario = scenario,
            Scene = scene,
            ScenePath = scenePath,
            GodotVersion = godotVersion,
            BuildRuntimeIdentifier = buildRuntimeIdentifier,
            RuntimeIdentifier = runtimeIdentifier,
            ProjectIdentity = projectIdentity,
            ProjectVersion = projectVersion,
            UnexpectedErrors = unexpectedErrors,
            Passed = passed,
            FailureCategory = failureCategory,
            FailureMessage = failureMessage,
        };
    }

    private static void AddRequiredFieldError(List<string> errors, string value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"{fieldName} is required.");
        }
    }
}
