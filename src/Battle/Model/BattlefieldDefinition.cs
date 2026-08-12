using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Warwrought.Core;

namespace Warwrought.Battle.Model;

/// <summary>
/// Stable identity for one deterministic M2 battlefield fixture. Battlefield selection is
/// intentionally separate from the canonical M1 BattleDefinition input.
/// </summary>
public readonly struct BattlefieldId : IEquatable<BattlefieldId>, IComparable<BattlefieldId>
{
    private readonly StableId _value;

    public BattlefieldId(string value) => _value = new StableId(value);

    public string Value => _value.Value;

    public bool IsValid => !string.IsNullOrWhiteSpace(Value);

    public int CompareTo(BattlefieldId other) => _value.CompareTo(other._value);

    public bool Equals(BattlefieldId other) => _value.Equals(other._value);

    public override bool Equals(object? obj) => obj is BattlefieldId other && Equals(other);

    public override int GetHashCode() => _value.GetHashCode();

    public override string ToString() => Value;

    public static bool operator ==(BattlefieldId left, BattlefieldId right) => left.Equals(right);

    public static bool operator !=(BattlefieldId left, BattlefieldId right) => !left.Equals(right);

    public static bool operator <(BattlefieldId left, BattlefieldId right) => left.CompareTo(right) < 0;

    public static bool operator >(BattlefieldId left, BattlefieldId right) => left.CompareTo(right) > 0;

    public static bool operator <=(BattlefieldId left, BattlefieldId right) => left.CompareTo(right) <= 0;

    public static bool operator >=(BattlefieldId left, BattlefieldId right) => left.CompareTo(right) >= 0;
}

public enum BattlefieldValidationCode
{
    NullInput,
    UnsupportedSchemaVersion,
    InvalidBattlefieldId,
    SeedOutOfBounds,
    InvalidBounds,
    InvalidResolution,
    InvalidGenerationParameters,
    InvalidDeploymentZoneCount,
    InvalidHeightSampleCount,
    HeightSampleOutOfBounds,
    InvalidTerrainRegionId,
    DuplicateTerrainRegionId,
    InvalidTerrainRegionSample,
    UnknownTerrainRegion,
    InvalidDeploymentZone,
    DuplicateDeploymentZoneId,
    DeploymentZoneOutOfBounds,
    DeploymentZoneMargin,
}

public sealed record BattlefieldValidationError
{
    public BattlefieldValidationError(BattlefieldValidationCode code, string path, string message)
    {
        Code = code;
        Path = path;
        Message = message;
    }

    public BattlefieldValidationCode Code { get; }

    public string Path { get; }

    public string Message { get; }

    public override string ToString() => $"{Code} at {Path}: {Message}";
}

public sealed class BattlefieldValidationResult
{
    private readonly ReadOnlyCollection<BattlefieldValidationError> _errors;

    internal BattlefieldValidationResult(IEnumerable<BattlefieldValidationError> errors)
    {
        _errors = Array.AsReadOnly(errors.ToArray());
    }

    public bool IsValid => _errors.Count == 0;

    public IReadOnlyList<BattlefieldValidationError> Errors => _errors;

    public bool HasCode(BattlefieldValidationCode code)
    {
        for (var index = 0; index < _errors.Count; index++)
        {
            if (_errors[index].Code == code)
            {
                return true;
            }
        }

        return false;
    }

    public override string ToString() => IsValid ? "Battlefield definition is valid." : string.Join(Environment.NewLine, _errors);
}

public sealed class BattlefieldDefinitionValidationException : InvalidOperationException
{
    public BattlefieldDefinitionValidationException(BattlefieldValidationResult validation)
        : base($"Battlefield definition validation failed:{Environment.NewLine}{validation}")
    {
        Validation = validation;
    }

    public BattlefieldValidationResult Validation { get; }
}

/// <summary>
/// Inclusive horizontal X/Z limits. The regular height field places its first and last
/// samples exactly on these bounds. Width/depth are scaled integer distances, not render units.
/// </summary>
public readonly record struct BattlefieldBounds(int MinX, int MaxX, int MinZ, int MaxZ)
{
    public long WidthUnits => (long)MaxX - MinX;

    public long DepthUnits => (long)MaxZ - MinZ;

    public bool IsValid =>
        MinX >= -SimPosition.MaxCoordinateUnits &&
        MaxX <= SimPosition.MaxCoordinateUnits &&
        MinZ >= -SimPosition.MaxCoordinateUnits &&
        MaxZ <= SimPosition.MaxCoordinateUnits &&
        MinX < MaxX &&
        MinZ < MaxZ &&
        WidthUnits <= BattlefieldDefinition.MaxDimensionUnits &&
        DepthUnits <= BattlefieldDefinition.MaxDimensionUnits;

    public bool ContainsInclusive(int x, int z) => x >= MinX && x <= MaxX && z >= MinZ && z <= MaxZ;

    public SimPosition Center => new(
        checked((int)(((long)MinX + MaxX) / 2L)),
        checked((int)(((long)MinZ + MaxZ) / 2L)));

    public BattlefieldBounds Inset(int marginUnits)
    {
        return new BattlefieldBounds(
            checked(MinX + marginUnits),
            checked(MaxX - marginUnits),
            checked(MinZ + marginUnits),
            checked(MaxZ - marginUnits));
    }
}

/// <summary>
/// Coarse regular height-field dimensions. Samples are ordered row-major by Z, then X.
/// </summary>
public readonly record struct BattlefieldResolution(int SampleCountX, int SampleCountZ)
{
    public const int MinSamplesPerAxis = 2;
    public const int MaxSamplesPerAxis = 129;

    public long SampleCount => (long)SampleCountX * SampleCountZ;

    public bool IsValid =>
        SampleCountX >= MinSamplesPerAxis &&
        SampleCountX <= MaxSamplesPerAxis &&
        SampleCountZ >= MinSamplesPerAxis &&
        SampleCountZ <= MaxSamplesPerAxis &&
        SampleCount <= (long)MaxSamplesPerAxis * MaxSamplesPerAxis;
}

/// <summary>
/// Bounded integer generation controls. Elevation uses 1,000 integer units per metre;
/// roughness and flattening strengths are permille values, and broad hill frequency is the
/// number of broad value-noise cells across each axis.
/// </summary>
public sealed record BattlefieldGenerationParameters
{
    public BattlefieldGenerationParameters(
        int averageElevationUnits,
        int roughnessPermille,
        int broadHillFrequency,
        int deploymentFlatteningStrengthPermille,
        int deploymentFlatteningRadiusUnits,
        int usableDeploymentMarginUnits,
        int minimumElevationUnits,
        int maximumElevationUnits)
    {
        AverageElevationUnits = averageElevationUnits;
        RoughnessPermille = roughnessPermille;
        BroadHillFrequency = broadHillFrequency;
        DeploymentFlatteningStrengthPermille = deploymentFlatteningStrengthPermille;
        DeploymentFlatteningRadiusUnits = deploymentFlatteningRadiusUnits;
        UsableDeploymentMarginUnits = usableDeploymentMarginUnits;
        MinimumElevationUnits = minimumElevationUnits;
        MaximumElevationUnits = maximumElevationUnits;
    }

    public int AverageElevationUnits { get; }

    public int RoughnessPermille { get; }

    public int BroadHillFrequency { get; }

    public int DeploymentFlatteningStrengthPermille { get; }

    public int DeploymentFlatteningRadiusUnits { get; }

    public int UsableDeploymentMarginUnits { get; }

    public int MinimumElevationUnits { get; }

    public int MaximumElevationUnits { get; }
}

/// <summary>
/// Candidate deployment rectangle. The generator/definition derives its usable inset from
/// the global generation margin and retains the rectangle explicitly in the committed data.
/// </summary>
public sealed record BattlefieldDeploymentZoneInput
{
    public BattlefieldDeploymentZoneInput(StableId id, BattlefieldBounds bounds)
    {
        Id = id;
        Bounds = bounds;
    }

    public StableId Id { get; }

    public BattlefieldBounds Bounds { get; }
}

/// <summary>
/// Immutable committed deployment rectangle and its explicit playable inset.
/// </summary>
public sealed record BattlefieldDeploymentZone
{
    internal BattlefieldDeploymentZone(StableId id, BattlefieldBounds bounds, BattlefieldBounds usableBounds)
    {
        Id = id;
        Bounds = bounds;
        UsableBounds = usableBounds;
    }

    public StableId Id { get; }

    public BattlefieldBounds Bounds { get; }

    public BattlefieldBounds UsableBounds { get; }

    public SimPosition Center => Bounds.Center;

    public bool Contains(int x, int z) => Bounds.ContainsInclusive(x, z);

    public bool ContainsUsable(int x, int z) => UsableBounds.ContainsInclusive(x, z);
}

/// <summary>
/// Immutable construction snapshot for a battlefield. A generated battlefield fills the sample
/// collections before committing this input; hand-authored malformed definitions can be validated
/// through the same path without touching Godot or filesystem data.
/// </summary>
public sealed record BattlefieldDefinitionInput
{
    public BattlefieldDefinitionInput(
        int schemaVersion,
        BattlefieldId battlefieldId,
        ulong seed,
        BattlefieldBounds bounds,
        BattlefieldResolution resolution,
        BattlefieldGenerationParameters? generation,
        IEnumerable<int> heightSamples,
        IEnumerable<StableId> terrainRegionSamples,
        IEnumerable<StableId> terrainRegionIds,
        IEnumerable<BattlefieldDeploymentZoneInput?> deploymentZones)
    {
        ArgumentNullException.ThrowIfNull(heightSamples);
        ArgumentNullException.ThrowIfNull(terrainRegionSamples);
        ArgumentNullException.ThrowIfNull(terrainRegionIds);
        ArgumentNullException.ThrowIfNull(deploymentZones);

        SchemaVersion = schemaVersion;
        BattlefieldId = battlefieldId;
        Seed = seed;
        Bounds = bounds;
        Resolution = resolution;
        Generation = generation;
        HeightSamples = Array.AsReadOnly(heightSamples.ToArray());
        TerrainRegionSamples = Array.AsReadOnly(terrainRegionSamples.ToArray());
        TerrainRegionIds = Array.AsReadOnly(terrainRegionIds.ToArray());
        DeploymentZones = Array.AsReadOnly(deploymentZones.ToArray());
    }

    public int SchemaVersion { get; }

    public BattlefieldId BattlefieldId { get; }

    public ulong Seed { get; }

    public BattlefieldBounds Bounds { get; }

    public BattlefieldResolution Resolution { get; }

    public BattlefieldGenerationParameters? Generation { get; }

    public IReadOnlyList<int> HeightSamples { get; }

    public IReadOnlyList<StableId> TerrainRegionSamples { get; }

    public IReadOnlyList<StableId> TerrainRegionIds { get; }

    public IReadOnlyList<BattlefieldDeploymentZoneInput?> DeploymentZones { get; }
}

/// <summary>
/// Immutable, validated deterministic battlefield data. It contains only integer/scaled
/// environmental data and has no Godot, mesh, physics, render, or filesystem dependency.
/// </summary>
public sealed class BattlefieldDefinition
{
    public const int CurrentSchemaVersion = 1;
    public const int HeightUnitsPerMetre = 1_000;
    public const int MaxDimensionUnits = 1_000_000;
    public const int MaxElevationUnits = 100_000;
    /// <summary>
    /// M2 accepts a non-negative 63-bit seed. This explicit bound keeps fixture inputs
    /// inspectable while retaining ample deterministic seed space.
    /// </summary>
    public const ulong MaxSeed = 0x7FFF_FFFF_FFFF_FFFFUL;
    public const string CanonicalDigestDomain = "warwrought.battlefield-definition.m2.v1";

    private BattlefieldDefinition(
        int schemaVersion,
        BattlefieldId battlefieldId,
        ulong seed,
        BattlefieldBounds bounds,
        BattlefieldResolution resolution,
        BattlefieldGenerationParameters generation,
        IEnumerable<int> heightSamples,
        IEnumerable<StableId> terrainRegionSamples,
        IEnumerable<StableId> terrainRegionIds,
        IEnumerable<BattlefieldDeploymentZone> deploymentZones)
    {
        SchemaVersion = schemaVersion;
        BattlefieldId = battlefieldId;
        Seed = seed;
        Bounds = bounds;
        Resolution = resolution;
        Generation = new BattlefieldGenerationParameters(
            generation.AverageElevationUnits,
            generation.RoughnessPermille,
            generation.BroadHillFrequency,
            generation.DeploymentFlatteningStrengthPermille,
            generation.DeploymentFlatteningRadiusUnits,
            generation.UsableDeploymentMarginUnits,
            generation.MinimumElevationUnits,
            generation.MaximumElevationUnits);
        HeightSamples = Array.AsReadOnly(heightSamples.ToArray());
        TerrainRegionSamples = Array.AsReadOnly(terrainRegionSamples.ToArray());
        TerrainRegionIds = Array.AsReadOnly(terrainRegionIds.OrderBy(id => id.Value, StringComparer.Ordinal).ToArray());
        DeploymentZones = Array.AsReadOnly(deploymentZones.OrderBy(zone => zone.Id.Value, StringComparer.Ordinal).ToArray());
        CanonicalDigest = ComputeCanonicalDigest();
    }

    public int SchemaVersion { get; }

    public BattlefieldId BattlefieldId { get; }

    public BattlefieldId Id => BattlefieldId;

    public ulong Seed { get; }

    public BattlefieldBounds Bounds { get; }

    public long WidthUnits => Bounds.WidthUnits;

    public long DepthUnits => Bounds.DepthUnits;

    public BattlefieldResolution Resolution { get; }

    public BattlefieldGenerationParameters Generation { get; }

    /// <summary>
    /// Ordered row-major height samples: index = zIndex * SampleCountX + xIndex.
    /// </summary>
    public IReadOnlyList<int> HeightSamples { get; }

    /// <summary>
    /// Region ID at each corresponding height sample, using the same row-major ordering.
    /// </summary>
    public IReadOnlyList<StableId> TerrainRegionSamples { get; }

    public IReadOnlyList<StableId> TerrainRegionIds { get; }

    public IReadOnlyList<BattlefieldDeploymentZone> DeploymentZones { get; }

    public string CanonicalDigest { get; }

    public static BattlefieldValidationResult Validate(BattlefieldDefinitionInput? input)
    {
        var errors = new List<BattlefieldValidationError>();
        if (input is null)
        {
            Add(errors, BattlefieldValidationCode.NullInput, "battlefield", "A battlefield definition input is required.");
            return new BattlefieldValidationResult(errors);
        }

        if (input.SchemaVersion != CurrentSchemaVersion)
        {
            Add(
                errors,
                BattlefieldValidationCode.UnsupportedSchemaVersion,
                "schemaVersion",
                $"Battlefield schema version {input.SchemaVersion} is unsupported; M2 accepts only {CurrentSchemaVersion}.");
        }

        if (!input.BattlefieldId.IsValid)
        {
            Add(errors, BattlefieldValidationCode.InvalidBattlefieldId, "battlefieldId", "Battlefield ID must contain a non-whitespace stable value.");
        }

        if (input.Seed > MaxSeed)
        {
            Add(errors, BattlefieldValidationCode.SeedOutOfBounds, "seed", $"Battlefield seed must be between 0 and {MaxSeed}.");
        }

        ValidateBounds(input.Bounds, errors);
        ValidateResolution(input.Resolution, errors);
        ValidateGeneration(input.Generation, errors);

        var expectedSampleCount = input.Resolution.IsValid ? input.Resolution.SampleCount : -1L;
        if (expectedSampleCount >= 0 && input.HeightSamples.Count != expectedSampleCount)
        {
            Add(
                errors,
                BattlefieldValidationCode.InvalidHeightSampleCount,
                "heightSamples",
                $"Resolution requires {expectedSampleCount} ordered height samples, but {input.HeightSamples.Count} were supplied.");
        }

        if (input.Generation is not null)
        {
            for (var index = 0; index < input.HeightSamples.Count; index++)
            {
                var height = input.HeightSamples[index];
                if (height < input.Generation.MinimumElevationUnits || height > input.Generation.MaximumElevationUnits)
                {
                    Add(
                        errors,
                        BattlefieldValidationCode.HeightSampleOutOfBounds,
                        $"heightSamples[{index}]",
                        $"Height {height} must be within [{input.Generation.MinimumElevationUnits}, {input.Generation.MaximumElevationUnits}] integer units.");
                }
            }
        }

        var regionIds = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < input.TerrainRegionIds.Count; index++)
        {
            var id = input.TerrainRegionIds[index];
            if (string.IsNullOrWhiteSpace(id.Value))
            {
                Add(errors, BattlefieldValidationCode.InvalidTerrainRegionId, $"terrainRegionIds[{index}]", "Terrain region ID must contain a stable value.");
            }
            else if (!regionIds.Add(id.Value))
            {
                Add(errors, BattlefieldValidationCode.DuplicateTerrainRegionId, $"terrainRegionIds[{index}]", $"Terrain region ID '{id.Value}' is duplicated.");
            }
        }

        if (input.TerrainRegionIds.Count == 0)
        {
            Add(errors, BattlefieldValidationCode.InvalidTerrainRegionId, "terrainRegionIds", "At least one terrain region ID is required.");
        }

        if (expectedSampleCount >= 0 && input.TerrainRegionSamples.Count != expectedSampleCount)
        {
            Add(
                errors,
                BattlefieldValidationCode.InvalidHeightSampleCount,
                "terrainRegionSamples",
                $"Resolution requires {expectedSampleCount} ordered terrain region samples, but {input.TerrainRegionSamples.Count} were supplied.");
        }

        for (var index = 0; index < input.TerrainRegionSamples.Count; index++)
        {
            var id = input.TerrainRegionSamples[index];
            if (string.IsNullOrWhiteSpace(id.Value))
            {
                Add(errors, BattlefieldValidationCode.InvalidTerrainRegionSample, $"terrainRegionSamples[{index}]", "Terrain region sample ID must contain a stable value.");
            }
            else
            {
                if (!regionIds.Contains(id.Value))
                {
                    Add(errors, BattlefieldValidationCode.UnknownTerrainRegion, $"terrainRegionSamples[{index}]", $"Terrain region '{id.Value}' is not declared in terrainRegionIds.");
                }
            }
        }

        if (input.DeploymentZones.Count == 0)
        {
            Add(errors, BattlefieldValidationCode.InvalidDeploymentZoneCount, "deploymentZones", "At least one explicit deployment zone is required.");
        }

        var deploymentIds = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < input.DeploymentZones.Count; index++)
        {
            var zone = input.DeploymentZones[index];
            var path = $"deploymentZones[{index}]";
            if (zone is null)
            {
                Add(errors, BattlefieldValidationCode.InvalidDeploymentZone, path, "Deployment zone cannot be null.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(zone.Id.Value))
            {
                Add(errors, BattlefieldValidationCode.InvalidDeploymentZone, $"{path}.id", "Deployment zone ID must contain a stable value.");
            }
            else if (!deploymentIds.Add(zone.Id.Value))
            {
                Add(errors, BattlefieldValidationCode.DuplicateDeploymentZoneId, $"{path}.id", $"Deployment zone ID '{zone.Id.Value}' is duplicated.");
            }

            if (!zone.Bounds.IsValid)
            {
                Add(errors, BattlefieldValidationCode.InvalidDeploymentZone, $"{path}.bounds", "Deployment zone bounds must be non-empty and within bounded battlefield coordinates.");
            }
            else if (input.Bounds.IsValid &&
                     (!input.Bounds.ContainsInclusive(zone.Bounds.MinX, zone.Bounds.MinZ) ||
                      !input.Bounds.ContainsInclusive(zone.Bounds.MaxX, zone.Bounds.MaxZ)))
            {
                Add(errors, BattlefieldValidationCode.DeploymentZoneOutOfBounds, $"{path}.bounds", "Deployment zone bounds must be contained by the battlefield bounds.");
            }

            if (input.Generation is not null && zone.Bounds.IsValid)
            {
                var margin = input.Generation.UsableDeploymentMarginUnits;
                if (zone.Bounds.WidthUnits <= 2L * margin || zone.Bounds.DepthUnits <= 2L * margin)
                {
                    Add(
                        errors,
                        BattlefieldValidationCode.DeploymentZoneMargin,
                        $"{path}.bounds",
                        $"Deployment zone must remain positive after the {margin}-unit usable margin is inset.");
                }
            }
        }

        return new BattlefieldValidationResult(errors);
    }

    public static BattlefieldDefinition Commit(BattlefieldDefinitionInput input)
    {
        var validation = Validate(input);
        if (!validation.IsValid)
        {
            throw new BattlefieldDefinitionValidationException(validation);
        }

        return Build(input);
    }

    public static bool TryCommit(
        BattlefieldDefinitionInput? input,
        out BattlefieldDefinition? definition,
        out BattlefieldValidationResult validation)
    {
        validation = Validate(input);
        if (!validation.IsValid)
        {
            definition = null;
            return false;
        }

        definition = Build(input!);
        return true;
    }

    public int GetHeightSample(int xIndex, int zIndex)
    {
        ValidateSampleIndex(xIndex, zIndex);
        return HeightSamples[(zIndex * Resolution.SampleCountX) + xIndex];
    }

    public StableId GetTerrainRegionSample(int xIndex, int zIndex)
    {
        ValidateSampleIndex(xIndex, zIndex);
        return TerrainRegionSamples[(zIndex * Resolution.SampleCountX) + xIndex];
    }

    public BattlefieldDeploymentZone GetDeploymentZone(StableId id)
    {
        for (var index = 0; index < DeploymentZones.Count; index++)
        {
            if (DeploymentZones[index].Id == id)
            {
                return DeploymentZones[index];
            }
        }

        throw new KeyNotFoundException($"Battlefield does not contain deployment zone '{id.Value}'.");
    }

    private static BattlefieldDefinition Build(BattlefieldDefinitionInput input)
    {
        var generation = input.Generation!;
        var zones = input.DeploymentZones
            .Select(zone => new BattlefieldDeploymentZone(
                zone!.Id,
                zone.Bounds,
                zone.Bounds.Inset(generation.UsableDeploymentMarginUnits)))
            .ToArray();

        return new BattlefieldDefinition(
            input.SchemaVersion,
            input.BattlefieldId,
            input.Seed,
            input.Bounds,
            input.Resolution,
            generation,
            input.HeightSamples,
            input.TerrainRegionSamples,
            input.TerrainRegionIds,
            zones);
    }

    private static void ValidateBounds(BattlefieldBounds bounds, List<BattlefieldValidationError> errors)
    {
        if (!bounds.IsValid)
        {
            Add(
                errors,
                BattlefieldValidationCode.InvalidBounds,
                "bounds",
                $"Battlefield bounds must be strictly ordered, within +/-{SimPosition.MaxCoordinateUnits} X/Z units, and no wider/deeper than {MaxDimensionUnits} units.");
        }
    }

    private static void ValidateResolution(BattlefieldResolution resolution, List<BattlefieldValidationError> errors)
    {
        if (!resolution.IsValid)
        {
            Add(
                errors,
                BattlefieldValidationCode.InvalidResolution,
                "resolution",
                $"Height-field resolution must have {BattlefieldResolution.MinSamplesPerAxis}..{BattlefieldResolution.MaxSamplesPerAxis} samples on each axis.");
        }
    }

    private static void ValidateGeneration(BattlefieldGenerationParameters? generation, List<BattlefieldValidationError> errors)
    {
        if (generation is null)
        {
            Add(errors, BattlefieldValidationCode.InvalidGenerationParameters, "generation", "Generation parameters are required.");
            return;
        }

        var valid = generation.MinimumElevationUnits >= 0 &&
                    generation.MaximumElevationUnits > generation.MinimumElevationUnits &&
                    generation.MaximumElevationUnits <= MaxElevationUnits &&
                    generation.AverageElevationUnits >= generation.MinimumElevationUnits &&
                    generation.AverageElevationUnits <= generation.MaximumElevationUnits &&
                    generation.RoughnessPermille >= 0 &&
                    generation.RoughnessPermille <= 1_000 &&
                    generation.BroadHillFrequency >= 1 &&
                    generation.BroadHillFrequency <= 8 &&
                    generation.DeploymentFlatteningStrengthPermille >= 0 &&
                    generation.DeploymentFlatteningStrengthPermille <= 1_000 &&
                    generation.DeploymentFlatteningRadiusUnits >= 0 &&
                    generation.DeploymentFlatteningRadiusUnits <= MaxDimensionUnits &&
                    generation.UsableDeploymentMarginUnits >= 0 &&
                    generation.UsableDeploymentMarginUnits <= MaxDimensionUnits;

        if (!valid)
        {
            Add(
                errors,
                BattlefieldValidationCode.InvalidGenerationParameters,
                "generation",
                $"Generation values must use bounded integer inputs: elevations [0,{MaxElevationUnits}], permille values [0,1000], broad hill frequency [1,8], and non-negative bounded radius/margin.");
        }
    }

    private void ValidateSampleIndex(int xIndex, int zIndex)
    {
        if (xIndex < 0 || xIndex >= Resolution.SampleCountX)
        {
            throw new ArgumentOutOfRangeException(nameof(xIndex), xIndex, "Height-field X sample index is outside the committed resolution.");
        }

        if (zIndex < 0 || zIndex >= Resolution.SampleCountZ)
        {
            throw new ArgumentOutOfRangeException(nameof(zIndex), zIndex, "Height-field Z sample index is outside the committed resolution.");
        }
    }

    private string ComputeCanonicalDigest()
    {
        using var writer = new CanonicalDigestWriter();
        writer.WriteString(CanonicalDigestDomain);
        writer.WriteInt32(SchemaVersion);
        writer.WriteString(BattlefieldId.Value);
        writer.WriteUInt64(Seed);
        writer.WriteInt32(Bounds.MinX);
        writer.WriteInt32(Bounds.MaxX);
        writer.WriteInt32(Bounds.MinZ);
        writer.WriteInt32(Bounds.MaxZ);
        writer.WriteInt32(Resolution.SampleCountX);
        writer.WriteInt32(Resolution.SampleCountZ);
        writer.WriteInt32(Generation.AverageElevationUnits);
        writer.WriteInt32(Generation.RoughnessPermille);
        writer.WriteInt32(Generation.BroadHillFrequency);
        writer.WriteInt32(Generation.DeploymentFlatteningStrengthPermille);
        writer.WriteInt32(Generation.DeploymentFlatteningRadiusUnits);
        writer.WriteInt32(Generation.UsableDeploymentMarginUnits);
        writer.WriteInt32(Generation.MinimumElevationUnits);
        writer.WriteInt32(Generation.MaximumElevationUnits);

        writer.WriteInt32(TerrainRegionIds.Count);
        for (var index = 0; index < TerrainRegionIds.Count; index++)
        {
            writer.WriteString(TerrainRegionIds[index].Value);
        }

        writer.WriteInt32(HeightSamples.Count);
        for (var index = 0; index < HeightSamples.Count; index++)
        {
            writer.WriteInt32(HeightSamples[index]);
        }

        writer.WriteInt32(TerrainRegionSamples.Count);
        for (var index = 0; index < TerrainRegionSamples.Count; index++)
        {
            writer.WriteString(TerrainRegionSamples[index].Value);
        }

        writer.WriteInt32(DeploymentZones.Count);
        for (var index = 0; index < DeploymentZones.Count; index++)
        {
            var zone = DeploymentZones[index];
            writer.WriteString(zone.Id.Value);
            writer.WriteInt32(zone.Bounds.MinX);
            writer.WriteInt32(zone.Bounds.MaxX);
            writer.WriteInt32(zone.Bounds.MinZ);
            writer.WriteInt32(zone.Bounds.MaxZ);
            writer.WriteInt32(zone.UsableBounds.MinX);
            writer.WriteInt32(zone.UsableBounds.MaxX);
            writer.WriteInt32(zone.UsableBounds.MinZ);
            writer.WriteInt32(zone.UsableBounds.MaxZ);
        }

        return writer.ComputeSha256Hex();
    }

    private static void Add(List<BattlefieldValidationError> errors, BattlefieldValidationCode code, string path, string message)
    {
        errors.Add(new BattlefieldValidationError(code, path, message));
    }
}
