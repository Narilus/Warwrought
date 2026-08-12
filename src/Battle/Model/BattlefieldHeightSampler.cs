using System;
using Warwrought.Core;

namespace Warwrought.Battle.Model;

/// <summary>
/// A finite presentation-facing result from the authoritative integer height field.
/// HeightUnits remains the branch-relevant value; HeightMetres is a derived finite value for a
/// future visual presenter and is never used by M1 combat.
/// </summary>
public sealed record BattlefieldHeightSample
{
    internal BattlefieldHeightSample(int requestedX, int requestedZ, int sampledX, int sampledZ, int heightUnits, StableId terrainRegionId)
    {
        RequestedX = requestedX;
        RequestedZ = requestedZ;
        SampledX = sampledX;
        SampledZ = sampledZ;
        HeightUnits = heightUnits;
        TerrainRegionId = terrainRegionId;
    }

    public int RequestedX { get; }

    public int RequestedZ { get; }

    public int SampledX { get; }

    public int SampledZ { get; }

    public SimPosition SampledPosition => new(SampledX, SampledZ);

    public bool WasClamped => RequestedX != SampledX || RequestedZ != SampledZ;

    public int HeightUnits { get; }

    public int Height => HeightUnits;

    public double HeightMetres => HeightUnits / (double)BattlefieldDefinition.HeightUnitsPerMetre;

    public StableId TerrainRegionId { get; }
}

/// <summary>
/// The one authoritative X/Z battlefield query path for M2. The sampler uses the committed
/// regular grid directly; it never reads a mesh, node, physics body, render state, or engine RNG.
///
/// Convention: the grid's first/last samples lie on the inclusive X/Z bounds. Height is bilinear
/// interpolation using integer floor division: X is interpolated first, then Z. Terrain region is
/// the nearest grid sample in each axis; an exact half-way tie chooses the higher sample index.
/// Any coordinate outside the inclusive bounds is clamped independently to the nearest bound and
/// returned with WasClamped=true. Thus every query has a bounded integer height and finite metres.
/// </summary>
public sealed class BattlefieldHeightSampler
{
    private readonly BattlefieldDefinition _definition;

    public BattlefieldHeightSampler(BattlefieldDefinition definition)
    {
        _definition = definition ?? throw new ArgumentNullException(nameof(definition));
    }

    public BattlefieldDefinition Definition => _definition;

    public BattlefieldHeightSample Sample(SimPosition position) => Sample(position.X, position.Z);

    /// <summary>
    /// Returns the exact committed sample at one regular-grid index. Presentation mesh builders
    /// use this narrow sampler operation so mesh vertex heights cannot drift from the definition.
    /// </summary>
    public BattlefieldHeightSample SampleGrid(int xIndex, int zIndex)
    {
        ValidateSampleIndex(xIndex, zIndex);
        var x = GridCoordinateUnits(_definition.Bounds.MinX, _definition.Bounds.MaxX, xIndex, _definition.Resolution.SampleCountX);
        var z = GridCoordinateUnits(_definition.Bounds.MinZ, _definition.Bounds.MaxZ, zIndex, _definition.Resolution.SampleCountZ);
        var roundedX = checked((int)Math.Round(x, MidpointRounding.AwayFromZero));
        var roundedZ = checked((int)Math.Round(z, MidpointRounding.AwayFromZero));
        return new BattlefieldHeightSample(
            roundedX,
            roundedZ,
            roundedX,
            roundedZ,
            _definition.GetHeightSample(xIndex, zIndex),
            _definition.GetTerrainRegionSample(xIndex, zIndex));
    }

    public double GridXUnits(int xIndex)
    {
        ValidateXSampleIndex(xIndex);
        return GridCoordinateUnits(_definition.Bounds.MinX, _definition.Bounds.MaxX, xIndex, _definition.Resolution.SampleCountX);
    }

    public double GridZUnits(int zIndex)
    {
        ValidateZSampleIndex(zIndex);
        return GridCoordinateUnits(_definition.Bounds.MinZ, _definition.Bounds.MaxZ, zIndex, _definition.Resolution.SampleCountZ);
    }

    public BattlefieldHeightSample Sample(int x, int z)
    {
        var sampledX = ClampToBounds(x, _definition.Bounds.MinX, _definition.Bounds.MaxX);
        var sampledZ = ClampToBounds(z, _definition.Bounds.MinZ, _definition.Bounds.MaxZ);
        var xAxis = Locate(sampledX, _definition.Bounds.MinX, _definition.Bounds.MaxX, _definition.Resolution.SampleCountX);
        var zAxis = Locate(sampledZ, _definition.Bounds.MinZ, _definition.Bounds.MaxZ, _definition.Resolution.SampleCountZ);

        var x0z0 = _definition.GetHeightSample(xAxis.LowerIndex, zAxis.LowerIndex);
        var x1z0 = _definition.GetHeightSample(xAxis.UpperIndex, zAxis.LowerIndex);
        var x0z1 = _definition.GetHeightSample(xAxis.LowerIndex, zAxis.UpperIndex);
        var x1z1 = _definition.GetHeightSample(xAxis.UpperIndex, zAxis.UpperIndex);
        var lowerRow = Interpolate(x0z0, x1z0, xAxis.Remainder, xAxis.Denominator);
        var upperRow = Interpolate(x0z1, x1z1, xAxis.Remainder, xAxis.Denominator);
        var height = Interpolate(lowerRow, upperRow, zAxis.Remainder, zAxis.Denominator);

        var nearestX = xAxis.Remainder * 2L >= xAxis.Denominator ? xAxis.UpperIndex : xAxis.LowerIndex;
        var nearestZ = zAxis.Remainder * 2L >= zAxis.Denominator ? zAxis.UpperIndex : zAxis.LowerIndex;
        var region = _definition.GetTerrainRegionSample(nearestX, nearestZ);
        return new BattlefieldHeightSample(x, z, sampledX, sampledZ, height, region);
    }

    public int HeightAt(int x, int z) => Sample(x, z).HeightUnits;

    public int HeightAt(SimPosition position) => HeightAt(position.X, position.Z);

    public StableId RegionAt(int x, int z) => Sample(x, z).TerrainRegionId;

    public StableId RegionAt(SimPosition position) => RegionAt(position.X, position.Z);

    private static int ClampToBounds(int coordinate, int minimum, int maximum)
    {
        if (coordinate < minimum)
        {
            return minimum;
        }

        return coordinate > maximum ? maximum : coordinate;
    }

    private void ValidateSampleIndex(int xIndex, int zIndex)
    {
        ValidateXSampleIndex(xIndex);
        ValidateZSampleIndex(zIndex);
    }

    private void ValidateXSampleIndex(int xIndex)
    {
        if (xIndex < 0 || xIndex >= _definition.Resolution.SampleCountX)
        {
            throw new ArgumentOutOfRangeException(nameof(xIndex), xIndex, "Height-field X sample index is outside the committed resolution.");
        }
    }

    private void ValidateZSampleIndex(int zIndex)
    {
        if (zIndex < 0 || zIndex >= _definition.Resolution.SampleCountZ)
        {
            throw new ArgumentOutOfRangeException(nameof(zIndex), zIndex, "Height-field Z sample index is outside the committed resolution.");
        }
    }

    private static double GridCoordinateUnits(int minimum, int maximum, int index, int sampleCount)
    {
        return minimum + (((double)maximum - minimum) * index / (sampleCount - 1));
    }

    private static AxisLocation Locate(int coordinate, int minimum, int maximum, int sampleCount)
    {
        var denominator = (long)maximum - minimum;
        var numerator = ((long)coordinate - minimum) * (sampleCount - 1L);
        var lower = (int)(numerator / denominator);
        var remainder = numerator % denominator;
        if (lower >= sampleCount - 1)
        {
            lower = sampleCount - 2;
            remainder = denominator;
        }

        return new AxisLocation(lower, lower + 1, remainder, denominator);
    }

    private static int Interpolate(int lower, int upper, long remainder, long denominator)
    {
        var numerator = ((long)lower * (denominator - remainder)) + ((long)upper * remainder);
        return checked((int)(numerator / denominator));
    }

    private readonly record struct AxisLocation(int LowerIndex, int UpperIndex, long Remainder, long Denominator);
}
