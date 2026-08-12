using System;
using Godot;
using Warwrought.Battle.Model;
using Warwrought.Core;

namespace Warwrought.Presentation.Battle;

public enum BattlefieldProjectionPurpose
{
    TerrainMesh,
    ContactMarker,
    Unit,
    Remains,
    Effect,
    Decoration,
}

/// <summary>
/// The single production X/Z-to-Vector3 conversion for battlefield presentation. It owns one
/// M2.1 sampler and applies only documented visual offsets; no mesh collision, raycast, physics, or
/// render state participates. Simulation coordinates remain integer scaled units until this edge.
/// </summary>
public sealed class BattlefieldPresentationProjector
{
    public const double SimulationUnitsPerWorldMetre = BattlefieldDefinition.HeightUnitsPerMetre;

    // These offsets are presentation pivots, not terrain or combat rules. Sprite textures are
    // centred around their Node3D origin, so the unit offset leaves the symbolic infantry standing
    // above the sampled surface; remains/effect offsets keep those cues readable without collision.
    public const float UnitVerticalOffset = 0.88f;
    public const float RemainsVerticalOffset = 0.18f;
    public const float EffectVerticalOffset = 0.46f;
    public const float ContactMarkerVerticalOffset = 0.05f;
    public const float TreeVerticalOffset = 2.10f;
    public const float BushVerticalOffset = 0.52f;
    public const float PropVerticalOffset = 0.35f;

    private readonly BattlefieldHeightSampler _sampler;
    private readonly int[] _purposeCounts = new int[Enum.GetValues<BattlefieldProjectionPurpose>().Length];

    public BattlefieldPresentationProjector(BattlefieldDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        _sampler = new BattlefieldHeightSampler(definition);
    }

    public BattlefieldDefinition Definition => _sampler.Definition;

    public BattlefieldHeightSampler Sampler => _sampler;

    public int TotalProjectionRequests { get; private set; }

    public int ClampedProjectionRequests { get; private set; }

    public int GetProjectionCount(BattlefieldProjectionPurpose purpose) => _purposeCounts[(int)purpose];

    public Vector3 Project(SimPosition position, float verticalOffset, BattlefieldProjectionPurpose purpose)
    {
        return Project(position.X, position.Z, verticalOffset, purpose);
    }

    public Vector3 Project(double xUnits, double zUnits, float verticalOffset, BattlefieldProjectionPurpose purpose)
    {
        var roundedX = checked((int)Math.Round(xUnits, MidpointRounding.AwayFromZero));
        var roundedZ = checked((int)Math.Round(zUnits, MidpointRounding.AwayFromZero));
        var sample = _sampler.Sample(roundedX, roundedZ);
        RecordProjection(purpose, sample.WasClamped);
        return new Vector3(
            (float)(xUnits / SimulationUnitsPerWorldMetre),
            (float)sample.HeightMetres + verticalOffset,
            (float)(zUnits / SimulationUnitsPerWorldMetre));
    }

    /// <summary>
    /// Projects an exact coarse mesh grid sample through the same sampler-owned conversion. Grid
    /// coordinates remain fractional when a battlefield span is not evenly divisible by its sample
    /// count; the sampled height still comes from the committed integer grid value.
    /// </summary>
    public Vector3 ProjectGridVertex(int xIndex, int zIndex, float verticalOffset, BattlefieldProjectionPurpose purpose)
    {
        var sample = _sampler.SampleGrid(xIndex, zIndex);
        RecordProjection(purpose, sample.WasClamped);
        return new Vector3(
            (float)(_sampler.GridXUnits(xIndex) / SimulationUnitsPerWorldMetre),
            (float)sample.HeightMetres + verticalOffset,
            (float)(_sampler.GridZUnits(zIndex) / SimulationUnitsPerWorldMetre));
    }

    private void RecordProjection(BattlefieldProjectionPurpose purpose, bool wasClamped)
    {
        TotalProjectionRequests++;
        _purposeCounts[(int)purpose]++;
        if (wasClamped)
        {
            ClampedProjectionRequests++;
        }
    }
}
