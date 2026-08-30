using System.Linq;
using Godot;
using Warwrought.Battle.Model;
using Warwrought.Battle.Playback;
using Warwrought.Battle.Simulation;
using Warwrought.Presentation.Battle;
using Xunit;

namespace Warwrought.Tests;

public sealed class M2BattlePresentationTests
{
    [Fact]
    public void MeshTopologyUsesSamplerHeightsAndFacetedTriangleCounts()
    {
        var definition = BattlefieldFixtureFactory.CreateM2OpenMeadowProfile();
        var mesh = BattlefieldTerrainMeshData.Build(definition);

        Assert.Equal(17 * 13, mesh.GridVertexCount);
        Assert.Equal((17 - 1) * (13 - 1) * 2, mesh.TriangleCount);
        Assert.Equal(mesh.TriangleCount * 3, mesh.VertexCount);
        Assert.True(mesh.HasSamplerAgreement());
        Assert.Equal(definition.GetHeightSample(8, 6), mesh.Triangles
            .SelectMany(triangle => new[] { triangle.A, triangle.B, triangle.C })
            .First(vertex => vertex.GridXIndex == 8 && vertex.GridZIndex == 6).HeightUnits);
    }

    [Fact]
    public void ProjectorUsesSamplerHeightForUnitRemainsAndEffectOffsets()
    {
        var definition = BattlefieldFixtureFactory.CreateM2BroadHighlandProfile();
        var projector = new BattlefieldPresentationProjector(definition);
        var position = new Warwrought.Core.SimPosition(12_345, -2_345);
        var sample = projector.Sampler.Sample(position);

        var unit = projector.Project(position, BattlefieldPresentationProjector.UnitVerticalOffset, BattlefieldProjectionPurpose.Unit);
        var remains = projector.Project(position, BattlefieldPresentationProjector.RemainsVerticalOffset, BattlefieldProjectionPurpose.Remains);
        var effect = projector.Project(position, BattlefieldPresentationProjector.EffectVerticalOffset, BattlefieldProjectionPurpose.Effect);

        Assert.Equal(sample.HeightMetres + BattlefieldPresentationProjector.UnitVerticalOffset, unit.Y, precision: 5);
        Assert.Equal(sample.HeightMetres + BattlefieldPresentationProjector.RemainsVerticalOffset, remains.Y, precision: 5);
        Assert.Equal(sample.HeightMetres + BattlefieldPresentationProjector.EffectVerticalOffset, effect.Y, precision: 5);
        Assert.Equal(1, projector.GetProjectionCount(BattlefieldProjectionPurpose.Unit));
        Assert.Equal(1, projector.GetProjectionCount(BattlefieldProjectionPurpose.Remains));
        Assert.Equal(1, projector.GetProjectionCount(BattlefieldProjectionPurpose.Effect));
    }

    [Fact]
    public void FoliageScatterIsStableBoundedAndExcludesDeploymentsAndContact()
    {
        var definition = BattlefieldFixtureFactory.CreateM2OpenMeadowProfile();
        var firstScatter = new BattlefieldFoliageScatter(definition);
        var first = firstScatter.CreatePlacements();
        var second = new BattlefieldFoliageScatter(BattlefieldFixtureFactory.CreateM2OpenMeadowProfile()).CreatePlacements();

        Assert.Equal(first, second);
        Assert.Equal(firstScatter.ComputePlacementDigest(first), new BattlefieldFoliageScatter(definition).ComputePlacementDigest(second));
        Assert.Equal(BattlefieldFoliageScatter.DefaultPlacementCount, first.Count);
        Assert.All(first, placement =>
        {
            Assert.InRange(placement.XUnits, definition.Bounds.MinX, definition.Bounds.MaxX);
            Assert.InRange(placement.ZUnits, definition.Bounds.MinZ, definition.Bounds.MaxZ);
            Assert.InRange(placement.ScalePermille, BattlefieldFoliageScatter.MinimumScalePermille, BattlefieldFoliageScatter.MaximumScalePermille);
            Assert.False(firstScatter.IsExcluded(placement.XUnits, placement.ZUnits));
            Assert.Contains(placement.TerrainRegionId, definition.TerrainRegionIds);
        });
    }

    [Fact]
    public void AuthoredFoliageMappingIsStableAndReachesEveryCommittedSource()
    {
        var profiles = new[]
        {
            BattlefieldFixtureFactory.CreateM2OpenMeadowProfile(),
            BattlefieldFixtureFactory.CreateM2BroadHighlandProfile(),
        };

        var reachablePaths = profiles
            .SelectMany(profile => new BattlefieldFoliageScatter(profile).CreatePlacements())
            .Where(placement => placement.Kind != BattlefieldDecorationKind.Rock)
            .Select(BattlefieldTerrainView.SelectFoliageTexturePath)
            .Distinct()
            .OrderBy(path => path)
            .ToArray();

        var expectedPaths = BattlefieldTerrainView.AuthoredFoliageTexturePaths
            .OrderBy(path => path)
            .ToArray();

        Assert.Equal(expectedPaths, reachablePaths);

        var repeat = profiles
            .SelectMany(profile => new BattlefieldFoliageScatter(profile).CreatePlacements())
            .Where(placement => placement.Kind != BattlefieldDecorationKind.Rock)
            .Select(BattlefieldTerrainView.SelectFoliageTexturePath)
            .ToArray();
        var first = profiles
            .SelectMany(profile => new BattlefieldFoliageScatter(profile).CreatePlacements())
            .Where(placement => placement.Kind != BattlefieldDecorationKind.Rock)
            .Select(BattlefieldTerrainView.SelectFoliageTexturePath)
            .ToArray();

        Assert.Equal(first, repeat);
    }

    [Fact]
    public void AuthoredFoliageIntegrationRetainsProtectedScatterDigests()
    {
        var meadow = BattlefieldFixtureFactory.CreateM2OpenMeadowProfile();
        var highland = BattlefieldFixtureFactory.CreateM2BroadHighlandProfile();

        var meadowScatter = new BattlefieldFoliageScatter(meadow);
        var highlandScatter = new BattlefieldFoliageScatter(highland);

        Assert.Equal(
            "adda947b93cd28b4d1170138d5538b419dc4237efcc70f6f19b8baa516c863e6",
            meadowScatter.ComputePlacementDigest(meadowScatter.CreatePlacements()));
        Assert.Equal(
            "9a12bd67baf35b084e0fc6c0ee8fa22c6bc9e40ea8706f359d7d239fb3d83d83",
            highlandScatter.ComputePlacementDigest(highlandScatter.CreatePlacements()));
    }

    [Fact]
    public void CameraAndPresentationControlsDoNotMutateRetainedResolutionDigests()
    {
        var committed = CommittedBattleResolution.Commit(BattleFixtureFactory.CreateM1MeleeFixture());
        var inputDigest = committed.Definition.CanonicalInputDigest;
        var transcriptDigest = committed.Resolution.Transcript.CanonicalDigest;
        var resultDigest = committed.Resolution.Result.CanonicalDigest;
        var playback = new BattleTranscriptPlayback(committed.Resolution);
        playback.Clock.SetSpeed(2.0);
        playback.Clock.Pause();
        playback.Clock.Reset();

        Assert.Equal(inputDigest, committed.Definition.CanonicalInputDigest);
        Assert.Equal(transcriptDigest, committed.Resolution.Transcript.CanonicalDigest);
        Assert.Equal(resultDigest, committed.Resolution.Result.CanonicalDigest);
        Assert.Same(committed.Resolution, committed.WatchResolution);
        Assert.Same(committed.Resolution.Result, committed.SkipToResult());
    }
}
