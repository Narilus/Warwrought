using System;
using System.Collections.Generic;
using Warwrought.Battle.Model;
using Warwrought.Core;

namespace Warwrought.Presentation.Battle;

/// <summary>
/// One presentation-only vertex input. The grid index and sampled integer height are retained so
/// tests and the Godot mesh presenter can prove that topology is sourced from the M2.1 sampler.
/// </summary>
public readonly record struct BattlefieldTerrainMeshVertex(
    int GridXIndex,
    int GridZIndex,
    double XUnits,
    double ZUnits,
    int HeightUnits,
    StableId TerrainRegionId);

/// <summary>
/// A consistently wound triangle in the coarse battlefield grid. Godot front faces are clockwise;
/// the ordered vertices therefore produce an upward-facing terrain surface for the presenter.
/// </summary>
public readonly record struct BattlefieldTerrainMeshTriangle(
    BattlefieldTerrainMeshVertex A,
    BattlefieldTerrainMeshVertex B,
    BattlefieldTerrainMeshVertex C);

/// <summary>
/// Deterministic coarse mesh input derived directly from one BattlefieldHeightSampler. This class
/// deliberately stops before Godot geometry/material APIs so topology and sampler agreement remain
/// fast, inspectable supporting behaviour rather than becoming render state.
/// </summary>
public sealed class BattlefieldTerrainMeshData
{
    private BattlefieldTerrainMeshData(
        BattlefieldDefinition definition,
        int gridVertexCount,
        IReadOnlyList<BattlefieldTerrainMeshTriangle> triangles)
    {
        Definition = definition;
        GridVertexCount = gridVertexCount;
        Triangles = triangles;
        VertexCount = checked(triangles.Count * 3);
    }

    public BattlefieldDefinition Definition { get; }

    public int GridVertexCount { get; }

    public int TriangleCount => Triangles.Count;

    /// <summary>
    /// Duplicate triangle vertices are intentional: each triangle receives one explicit normal and
    /// region colour, preserving the faceted low-poly visual language without smooth normal sharing.
    /// </summary>
    public int VertexCount { get; }

    public IReadOnlyList<BattlefieldTerrainMeshTriangle> Triangles { get; }

    public static BattlefieldTerrainMeshData Build(BattlefieldDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var sampler = new BattlefieldHeightSampler(definition);
        var resolution = definition.Resolution;
        var triangles = new List<BattlefieldTerrainMeshTriangle>(checked((resolution.SampleCountX - 1) * (resolution.SampleCountZ - 1) * 2));

        for (var zIndex = 0; zIndex < resolution.SampleCountZ - 1; zIndex++)
        {
            for (var xIndex = 0; xIndex < resolution.SampleCountX - 1; xIndex++)
            {
                var lowerLeft = CreateVertex(sampler, xIndex, zIndex);
                var upperLeft = CreateVertex(sampler, xIndex, zIndex + 1);
                var lowerRight = CreateVertex(sampler, xIndex + 1, zIndex);
                var upperRight = CreateVertex(sampler, xIndex + 1, zIndex + 1);

                // Clockwise front winding for Godot's triangle primitive, viewed from above.
                triangles.Add(new BattlefieldTerrainMeshTriangle(lowerLeft, lowerRight, upperLeft));
                triangles.Add(new BattlefieldTerrainMeshTriangle(lowerRight, upperRight, upperLeft));
            }
        }

        return new BattlefieldTerrainMeshData(
            definition,
            checked(resolution.SampleCountX * resolution.SampleCountZ),
            triangles.AsReadOnly());
    }

    public bool HasSamplerAgreement()
    {
        var sampler = new BattlefieldHeightSampler(Definition);
        for (var triangleIndex = 0; triangleIndex < Triangles.Count; triangleIndex++)
        {
            var triangle = Triangles[triangleIndex];
            if (!MatchesSampler(sampler, triangle.A) ||
                !MatchesSampler(sampler, triangle.B) ||
                !MatchesSampler(sampler, triangle.C))
            {
                return false;
            }
        }

        return true;
    }

    private static BattlefieldTerrainMeshVertex CreateVertex(BattlefieldHeightSampler sampler, int xIndex, int zIndex)
    {
        var sample = sampler.SampleGrid(xIndex, zIndex);
        return new BattlefieldTerrainMeshVertex(
            xIndex,
            zIndex,
            sampler.GridXUnits(xIndex),
            sampler.GridZUnits(zIndex),
            sample.HeightUnits,
            sample.TerrainRegionId);
    }

    private static bool MatchesSampler(BattlefieldHeightSampler sampler, BattlefieldTerrainMeshVertex vertex)
    {
        var sample = sampler.SampleGrid(vertex.GridXIndex, vertex.GridZIndex);
        return sample.HeightUnits == vertex.HeightUnits && sample.TerrainRegionId == vertex.TerrainRegionId;
    }
}
