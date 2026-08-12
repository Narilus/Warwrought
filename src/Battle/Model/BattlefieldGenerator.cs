using System;
using System.Collections.Generic;
using System.Linq;
using Warwrought.Core;

namespace Warwrought.Battle.Model;

/// <summary>
/// Immutable generation request for the narrow M2 battlefield fixture model.
/// </summary>
public sealed record BattlefieldGenerationRequest
{
    public BattlefieldGenerationRequest(
        int schemaVersion,
        BattlefieldId battlefieldId,
        ulong seed,
        BattlefieldBounds bounds,
        BattlefieldResolution resolution,
        BattlefieldGenerationParameters? generation,
        IEnumerable<BattlefieldDeploymentZoneInput?> deploymentZones)
    {
        ArgumentNullException.ThrowIfNull(deploymentZones);
        SchemaVersion = schemaVersion;
        BattlefieldId = battlefieldId;
        Seed = seed;
        Bounds = bounds;
        Resolution = resolution;
        Generation = generation;
        DeploymentZones = Array.AsReadOnly(deploymentZones.ToArray());
    }

    public int SchemaVersion { get; }

    public BattlefieldId BattlefieldId { get; }

    public ulong Seed { get; }

    public BattlefieldBounds Bounds { get; }

    public BattlefieldResolution Resolution { get; }

    public BattlefieldGenerationParameters? Generation { get; }

    public IReadOnlyList<BattlefieldDeploymentZoneInput?> DeploymentZones { get; }
}

/// <summary>
/// CPU-only integer battlefield generator. It combines seeded lattice value noise for broad hills,
/// seeded per-sample signed variation for roughness, and deterministic integer flattening around
/// explicit deployment rectangles. The seed/hash, lattice order, interpolation, and clamping are
/// all explicit; no platform random generator, Godot RNG/noise, render state, time, files, or unordered
/// iteration participates in the result.
/// </summary>
public static class BattlefieldGenerator
{
    private const int NoiseUnit = 1_000;
    private const ulong BroadNoiseSalt = 0xA53C9E17D42B6F01UL;
    private const ulong RoughNoiseSalt = 0x6D2B79F5AA13C4E7UL;
    private const ulong HashMultiplierA = 0x9E3779B185EBCA87UL;
    private const ulong HashMultiplierB = 0xC2B2AE3D27D4EB4FUL;
    private const ulong HashMultiplierC = 0x165667B19E3779F9UL;

    public static BattlefieldValidationResult Validate(BattlefieldGenerationRequest? request)
    {
        if (request is null)
        {
            return new BattlefieldValidationResult(new[]
            {
                new BattlefieldValidationError(
                    BattlefieldValidationCode.NullInput,
                    "generationRequest",
                    "A battlefield generation request is required."),
            });
        }

        if (request.Generation is null)
        {
            return new BattlefieldValidationResult(new[]
            {
                new BattlefieldValidationError(
                    BattlefieldValidationCode.InvalidGenerationParameters,
                    "generation",
                    "Generation parameters are required."),
            });
        }

        var sampleCount = request.Resolution.IsValid ? checked((int)request.Resolution.SampleCount) : 1;
        var placeholderHeight = Enumerable.Repeat(request.Generation.AverageElevationUnits, sampleCount);
        var placeholderRegion = new StableId("terrain.placeholder");
        var placeholderRegions = Enumerable.Repeat(placeholderRegion, sampleCount);
        var input = new BattlefieldDefinitionInput(
            request.SchemaVersion,
            request.BattlefieldId,
            request.Seed,
            request.Bounds,
            request.Resolution,
            request.Generation,
            placeholderHeight,
            placeholderRegions,
            new[] { placeholderRegion },
            request.DeploymentZones);
        return BattlefieldDefinition.Validate(input);
    }

    public static BattlefieldDefinition Generate(BattlefieldGenerationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var validation = Validate(request);
        if (!validation.IsValid)
        {
            throw new BattlefieldDefinitionValidationException(validation);
        }

        var generation = request.Generation!;
        var resolution = request.Resolution;
        var heights = GenerateHeights(request, generation);
        var regions = new StableId[heights.Length];
        for (var index = 0; index < heights.Length; index++)
        {
            regions[index] = RegionForHeight(heights[index], generation);
        }

        var input = new BattlefieldDefinitionInput(
            request.SchemaVersion,
            request.BattlefieldId,
            request.Seed,
            request.Bounds,
            resolution,
            generation,
            heights,
            regions,
            new[] { RegionLow, RegionMiddle, RegionHigh },
            request.DeploymentZones);
        return BattlefieldDefinition.Commit(input);
    }

    public static readonly StableId RegionLow = new("terrain.low");

    public static readonly StableId RegionMiddle = new("terrain.middle");

    public static readonly StableId RegionHigh = new("terrain.high");

    private static int[] GenerateHeights(BattlefieldGenerationRequest request, BattlefieldGenerationParameters generation)
    {
        var resolution = request.Resolution;
        var heights = new int[checked((int)resolution.SampleCount)];
        var xCoordinates = CreateAxisCoordinates(request.Bounds.MinX, request.Bounds.MaxX, resolution.SampleCountX);
        var zCoordinates = CreateAxisCoordinates(request.Bounds.MinZ, request.Bounds.MaxZ, resolution.SampleCountZ);
        var orderedDeploymentZones = request.DeploymentZones
            .OrderBy(zone => zone!.Id.Value, StringComparer.Ordinal)
            .ToArray();
        var heightRange = (long)generation.MaximumElevationUnits - generation.MinimumElevationUnits;
        var broadAmplitude = heightRange / 3L;
        var roughAmplitude = (heightRange * generation.RoughnessPermille) / 2_000L;

        for (var zIndex = 0; zIndex < resolution.SampleCountZ; zIndex++)
        {
            for (var xIndex = 0; xIndex < resolution.SampleCountX; xIndex++)
            {
                var broadNoise = BroadHillNoise(request.Seed, xIndex, zIndex, resolution, generation.BroadHillFrequency);
                var roughNoise = SignedNoise(request.Seed, xIndex, zIndex, RoughNoiseSalt);
                var elevation = generation.AverageElevationUnits +
                                 ((broadNoise * broadAmplitude) / NoiseUnit) +
                                 ((roughNoise * roughAmplitude) / NoiseUnit);
                heights[(zIndex * resolution.SampleCountX) + xIndex] = ClampHeight(elevation, generation);
            }
        }

        ApplyDeploymentFlattening(orderedDeploymentZones, generation, xCoordinates, zCoordinates, heights);
        return heights;
    }

    private static void ApplyDeploymentFlattening(
        BattlefieldDeploymentZoneInput?[] deploymentZones,
        BattlefieldGenerationParameters generation,
        int[] xCoordinates,
        int[] zCoordinates,
        int[] heights)
    {
        var resolution = new BattlefieldResolution(xCoordinates.Length, zCoordinates.Length);
        for (var zoneIndex = 0; zoneIndex < deploymentZones.Length; zoneIndex++)
        {
            var zone = deploymentZones[zoneIndex]!;
            var targetHeight = CalculateDeploymentTarget(zone.Bounds, xCoordinates, zCoordinates, resolution, heights, generation);
            for (var zIndex = 0; zIndex < resolution.SampleCountZ; zIndex++)
            {
                for (var xIndex = 0; xIndex < resolution.SampleCountX; xIndex++)
                {
                    var distance = DistanceOutsideRectangle(zone.Bounds, xCoordinates[xIndex], zCoordinates[zIndex]);
                    if (distance > generation.DeploymentFlatteningRadiusUnits)
                    {
                        continue;
                    }

                    var influence = generation.DeploymentFlatteningRadiusUnits == 0
                        ? generation.DeploymentFlatteningStrengthPermille
                        : (long)generation.DeploymentFlatteningStrengthPermille *
                          (generation.DeploymentFlatteningRadiusUnits - distance) /
                          generation.DeploymentFlatteningRadiusUnits;
                    var sampleIndex = (zIndex * resolution.SampleCountX) + xIndex;
                    var currentHeight = heights[sampleIndex];
                    var flattened = currentHeight + ((targetHeight - (long)currentHeight) * influence / NoiseUnit);
                    heights[sampleIndex] = ClampHeight(flattened, generation);
                }
            }
        }
    }

    private static int CalculateDeploymentTarget(
        BattlefieldBounds zone,
        int[] xCoordinates,
        int[] zCoordinates,
        BattlefieldResolution resolution,
        int[] heights,
        BattlefieldGenerationParameters generation)
    {
        long total = 0;
        var count = 0;
        for (var zIndex = 0; zIndex < resolution.SampleCountZ; zIndex++)
        {
            for (var xIndex = 0; xIndex < resolution.SampleCountX; xIndex++)
            {
                if (!zone.ContainsInclusive(xCoordinates[xIndex], zCoordinates[zIndex]))
                {
                    continue;
                }

                total += heights[(zIndex * resolution.SampleCountX) + xIndex];
                count++;
            }
        }

        if (count > 0)
        {
            return ClampHeight(total / count, generation);
        }

        var center = zone.Center;
        var nearestDistance = long.MaxValue;
        var nearestHeight = generation.AverageElevationUnits;
        for (var zIndex = 0; zIndex < resolution.SampleCountZ; zIndex++)
        {
            for (var xIndex = 0; xIndex < resolution.SampleCountX; xIndex++)
            {
                var distance = Math.Abs((long)xCoordinates[xIndex] - center.X) + Math.Abs((long)zCoordinates[zIndex] - center.Z);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestHeight = heights[(zIndex * resolution.SampleCountX) + xIndex];
                }
            }
        }

        return ClampHeight(nearestHeight, generation);
    }

    private static long DistanceOutsideRectangle(BattlefieldBounds rectangle, int x, int z)
    {
        var distanceX = x < rectangle.MinX ? (long)rectangle.MinX - x : x > rectangle.MaxX ? (long)x - rectangle.MaxX : 0L;
        var distanceZ = z < rectangle.MinZ ? (long)rectangle.MinZ - z : z > rectangle.MaxZ ? (long)z - rectangle.MaxZ : 0L;
        return Math.Max(distanceX, distanceZ);
    }

    private static int[] CreateAxisCoordinates(int minimum, int maximum, int sampleCount)
    {
        var coordinates = new int[sampleCount];
        var span = (long)maximum - minimum;
        var denominator = sampleCount - 1L;
        for (var index = 0; index < sampleCount; index++)
        {
            coordinates[index] = checked((int)(minimum + ((long)index * span / denominator)));
        }

        return coordinates;
    }

    private static int BroadHillNoise(ulong seed, int xIndex, int zIndex, BattlefieldResolution resolution, int frequency)
    {
        var xSpan = resolution.SampleCountX - 1L;
        var zSpan = resolution.SampleCountZ - 1L;
        var xNumerator = xIndex * (long)frequency;
        var zNumerator = zIndex * (long)frequency;
        var xCell = (int)(xNumerator / xSpan);
        var zCell = (int)(zNumerator / zSpan);
        var xRemainder = xNumerator % xSpan;
        var zRemainder = zNumerator % zSpan;
        if (xCell >= frequency)
        {
            xCell = frequency - 1;
            xRemainder = xSpan;
        }

        if (zCell >= frequency)
        {
            zCell = frequency - 1;
            zRemainder = zSpan;
        }

        var lowerLeft = SignedNoise(seed, xCell, zCell, BroadNoiseSalt);
        var lowerRight = SignedNoise(seed, xCell + 1, zCell, BroadNoiseSalt);
        var upperLeft = SignedNoise(seed, xCell, zCell + 1, BroadNoiseSalt);
        var upperRight = SignedNoise(seed, xCell + 1, zCell + 1, BroadNoiseSalt);
        var lower = Interpolate(lowerLeft, lowerRight, xRemainder, xSpan);
        var upper = Interpolate(upperLeft, upperRight, xRemainder, xSpan);
        return checked((int)Interpolate(lower, upper, zRemainder, zSpan));
    }

    private static int SignedNoise(ulong seed, int x, int z, ulong salt)
    {
        var value = seed ^ salt;
        value = unchecked(value + ((ulong)(uint)x * HashMultiplierA));
        value ^= unchecked((ulong)(uint)z * HashMultiplierB);
        value = unchecked((value ^ (value >> 30)) * HashMultiplierC);
        value ^= value >> 27;
        value = unchecked(value * HashMultiplierA);
        value ^= value >> 31;
        return (int)(value % 2_001UL) - 1_000;
    }

    private static long Interpolate(long lower, long upper, long remainder, long denominator)
    {
        return ((lower * (denominator - remainder)) + (upper * remainder)) / denominator;
    }

    private static int ClampHeight(long elevation, BattlefieldGenerationParameters generation)
    {
        if (elevation < generation.MinimumElevationUnits)
        {
            return generation.MinimumElevationUnits;
        }

        if (elevation > generation.MaximumElevationUnits)
        {
            return generation.MaximumElevationUnits;
        }

        return checked((int)elevation);
    }

    private static StableId RegionForHeight(int height, BattlefieldGenerationParameters generation)
    {
        var range = generation.MaximumElevationUnits - generation.MinimumElevationUnits;
        var normalized = ((long)height - generation.MinimumElevationUnits) * NoiseUnit / range;
        return normalized < 333 ? RegionLow : normalized < 666 ? RegionMiddle : RegionHigh;
    }
}
