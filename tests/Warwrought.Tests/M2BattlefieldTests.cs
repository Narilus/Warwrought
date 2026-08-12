using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Warwrought.Battle.Model;
using Warwrought.Battle.Simulation;
using Warwrought.Core;
using Xunit;

namespace Warwrought.Tests;

public sealed class M2BattlefieldTests
{
    [Fact]
    public void FixedProfilesRepeatOrderedDataAndHaveRepresentativeStableDigests()
    {
        var meadow = BattlefieldFixtureFactory.CreateM2OpenMeadowProfile();
        var meadowRepeat = BattlefieldFixtureFactory.CreateM2OpenMeadowProfile();
        var highland = BattlefieldFixtureFactory.CreateM2BroadHighlandProfile();

        Assert.Equal(BattlefieldDefinition.CurrentSchemaVersion, meadow.SchemaVersion);
        Assert.Equal(new BattlefieldId("battlefield.m2.open-meadow"), meadow.BattlefieldId);
        Assert.Equal(BattlefieldFixtureFactory.M2OpenMeadowSeed, meadow.Seed);
        Assert.Equal(meadow.Bounds, meadowRepeat.Bounds);
        Assert.Equal(meadow.Resolution, meadowRepeat.Resolution);
        Assert.Equal(meadow.HeightSamples, meadowRepeat.HeightSamples);
        Assert.Equal(meadow.TerrainRegionSamples, meadowRepeat.TerrainRegionSamples);
        Assert.Equal(meadow.DeploymentZones, meadowRepeat.DeploymentZones);
        Assert.Equal(meadow.CanonicalDigest, meadowRepeat.CanonicalDigest);
        Assert.Equal(new BattlefieldHeightSampler(meadow).Sample(12_345, -2_345), new BattlefieldHeightSampler(meadowRepeat).Sample(12_345, -2_345));
        Assert.Equal(new BattlefieldHeightSampler(meadow).RegionAt(12_345, -2_345), new BattlefieldHeightSampler(meadowRepeat).RegionAt(12_345, -2_345));

        Assert.Equal("567f72c3d9e754722839fb8f1fb034fea7d9f647bc66690a36416ed0e7dc5443", meadow.CanonicalDigest);
        Assert.Equal("5973015037208b1d09e4d50be3e0353b3bf200dd06fd1a1ccdf77ec4f691326f", highland.CanonicalDigest);
        Assert.Equal(1_815, meadow.GetHeightSample(8, 6));
        Assert.Equal(10_156, highland.GetHeightSample(8, 6));
        var meadowSampler = new BattlefieldHeightSampler(meadow);
        var highlandSampler = new BattlefieldHeightSampler(highland);
        Assert.Equal(0, meadow.HeightSamples.Min());
        Assert.Equal(12_017, meadow.HeightSamples.Max());
        Assert.Equal(0, highland.HeightSamples.Min());
        Assert.Equal(19_502, highland.HeightSamples.Max());
        Assert.Equal(3_482, meadowSampler.Sample(12_345, -2_345).HeightUnits);
        Assert.Equal(new StableId("terrain.low"), meadowSampler.RegionAt(12_345, -2_345));
        Assert.Equal(7_492, highlandSampler.Sample(12_345, -2_345).HeightUnits);
        Assert.Equal(new StableId("terrain.middle"), highlandSampler.RegionAt(12_345, -2_345));

        Assert.NotEqual(meadow.CanonicalDigest, highland.CanonicalDigest);
        Assert.NotEqual(meadow.HeightSamples, highland.HeightSamples);
        Assert.NotEqual(meadow.TerrainRegionSamples, highland.TerrainRegionSamples);
        Assert.NotEqual(meadow.BattlefieldId, highland.BattlefieldId);
        Assert.Equal(2, meadow.DeploymentZones.Count);
        Assert.Equal(2, highland.DeploymentZones.Count);
        Assert.All(meadow.HeightSamples, height => Assert.InRange(height, meadow.Generation.MinimumElevationUnits, meadow.Generation.MaximumElevationUnits));
        Assert.All(highland.HeightSamples, height => Assert.InRange(height, highland.Generation.MinimumElevationUnits, highland.Generation.MaximumElevationUnits));
        Assert.All(meadow.TerrainRegionSamples, region => Assert.Contains(region, meadow.TerrainRegionIds));
        Assert.All(highland.TerrainRegionSamples, region => Assert.Contains(region, highland.TerrainRegionIds));
    }

    [Fact]
    public void SamplerUsesGridInterpolationNearestRegionAndIndependentOutOfBoundsClamping()
    {
        var definition = BattlefieldFixtureFactory.CreateM2OpenMeadowProfile();
        var sampler = new BattlefieldHeightSampler(definition);
        var bounds = definition.Bounds;

        var corner = sampler.Sample(bounds.MinX, bounds.MinZ);
        Assert.Equal(definition.GetHeightSample(0, 0), corner.HeightUnits);
        Assert.Equal(definition.GetTerrainRegionSample(0, 0), corner.TerrainRegionId);
        Assert.False(corner.WasClamped);

        var oppositeCorner = sampler.Sample(bounds.MaxX, bounds.MaxZ);
        Assert.Equal(definition.GetHeightSample(definition.Resolution.SampleCountX - 1, definition.Resolution.SampleCountZ - 1), oppositeCorner.HeightUnits);
        Assert.Equal(definition.GetTerrainRegionSample(definition.Resolution.SampleCountX - 1, definition.Resolution.SampleCountZ - 1), oppositeCorner.TerrainRegionId);

        var edge = sampler.Sample(bounds.MinX, 0);
        Assert.Equal(bounds.MinX, edge.SampledX);
        Assert.Equal(0, edge.SampledZ);
        Assert.Equal(definition.GetHeightSample(0, 6), edge.HeightUnits);

        var interior = sampler.Sample(1_234, -2_345);
        Assert.InRange(interior.HeightUnits, definition.Generation.MinimumElevationUnits, definition.Generation.MaximumElevationUnits);
        Assert.InRange(interior.HeightMetres, 0.0, BattlefieldDefinition.MaxElevationUnits / (double)BattlefieldDefinition.HeightUnitsPerMetre);
        Assert.False(double.IsNaN(interior.HeightMetres));
        Assert.False(double.IsInfinity(interior.HeightMetres));
        Assert.False(interior.WasClamped);

        var outOfBounds = sampler.Sample(bounds.MinX - 7_777, bounds.MaxZ + 8_888);
        Assert.True(outOfBounds.WasClamped);
        Assert.Equal(bounds.MinX, outOfBounds.SampledX);
        Assert.Equal(bounds.MaxZ, outOfBounds.SampledZ);
        Assert.Equal(sampler.HeightAt(bounds.MinX, bounds.MaxZ), outOfBounds.HeightUnits);
        Assert.Equal(sampler.HeightAt(bounds.MinX - 7_777, bounds.MaxZ + 8_888), outOfBounds.HeightUnits);
        Assert.Equal(sampler.RegionAt(bounds.MinX - 7_777, bounds.MaxZ + 8_888), outOfBounds.TerrainRegionId);
        Assert.False(double.IsNaN(outOfBounds.HeightMetres));
        Assert.False(double.IsInfinity(outOfBounds.HeightMetres));

        var exactInteriorGridSample = sampler.Sample(0, 0);
        Assert.Equal(definition.GetHeightSample(8, 6), exactInteriorGridSample.HeightUnits);
        Assert.Equal(definition.GetTerrainRegionSample(8, 6), exactInteriorGridSample.TerrainRegionId);
    }

    [Fact]
    public void SamplerUsesDocumentedIntegerBilinearInterpolationAndNearestRegionTieRule()
    {
        var definition = BattlefieldDefinition.Commit(new BattlefieldDefinitionInput(
            BattlefieldDefinition.CurrentSchemaVersion,
            new BattlefieldId("battlefield.test.sampler"),
            7,
            new BattlefieldBounds(-1_000, 1_000, -1_000, 1_000),
            new BattlefieldResolution(3, 3),
            new BattlefieldGenerationParameters(1_100, 0, 1, 1_000, 200, 100, 0, 3_000),
            new[]
            {
                0, 100, 200,
                1_000, 1_100, 1_200,
                2_000, 2_100, 2_200,
            },
            new[]
            {
                new StableId("terrain.low"), new StableId("terrain.middle"), new StableId("terrain.high"),
                new StableId("terrain.low"), new StableId("terrain.high"), new StableId("terrain.high"),
                new StableId("terrain.middle"), new StableId("terrain.high"), new StableId("terrain.high"),
            },
            new[]
            {
                new StableId("terrain.low"), new StableId("terrain.middle"), new StableId("terrain.high"),
            },
            new[]
            {
                new BattlefieldDeploymentZoneInput(new StableId("deployment.test"), new BattlefieldBounds(-800, 800, -800, 0)),
            }));
        var sampler = new BattlefieldHeightSampler(definition);

        var exactGrid = sampler.Sample(0, 0);
        Assert.Equal(1_100, exactGrid.HeightUnits);
        Assert.Equal(new StableId("terrain.high"), exactGrid.TerrainRegionId);

        var halfway = sampler.Sample(-500, -500);
        Assert.Equal(550, halfway.HeightUnits);
        Assert.Equal(new StableId("terrain.high"), halfway.TerrainRegionId);
        Assert.False(halfway.WasClamped);
    }

    [Fact]
    public void DeploymentZonesAreExplicitUsableAndFlattenedInGeneratedProfiles()
    {
        foreach (var definition in new[]
                 {
                     BattlefieldFixtureFactory.CreateM2OpenMeadowProfile(),
                     BattlefieldFixtureFactory.CreateM2BroadHighlandProfile(),
                 })
        {
            var sampler = new BattlefieldHeightSampler(definition);
            Assert.NotEmpty(definition.DeploymentZones);

            foreach (var zone in definition.DeploymentZones)
            {
                Assert.True(zone.Bounds.IsValid);
                Assert.True(zone.UsableBounds.IsValid);
                Assert.True(definition.Bounds.ContainsInclusive(zone.Bounds.MinX, zone.Bounds.MinZ));
                Assert.True(definition.Bounds.ContainsInclusive(zone.Bounds.MaxX, zone.Bounds.MaxZ));
                Assert.True(zone.UsableBounds.WidthUnits > 0);
                Assert.True(zone.UsableBounds.DepthUnits > 0);
                Assert.True(zone.ContainsUsable(zone.UsableBounds.Center.X, zone.UsableBounds.Center.Z));

                var flattenedHeights = new List<int>();
                for (var zIndex = 0; zIndex < definition.Resolution.SampleCountZ; zIndex++)
                {
                    for (var xIndex = 0; xIndex < definition.Resolution.SampleCountX; xIndex++)
                    {
                        var x = CoordinateAt(definition.Bounds.MinX, definition.Bounds.MaxX, xIndex, definition.Resolution.SampleCountX);
                        var z = CoordinateAt(definition.Bounds.MinZ, definition.Bounds.MaxZ, zIndex, definition.Resolution.SampleCountZ);
                        if (zone.ContainsUsable(x, z))
                        {
                            flattenedHeights.Add(definition.GetHeightSample(xIndex, zIndex));
                        }
                    }
                }

                Assert.NotEmpty(flattenedHeights);
                Assert.True(flattenedHeights.Max() - flattenedHeights.Min() <= 1);
                Assert.InRange(sampler.HeightAt(zone.UsableBounds.Center), definition.Generation.MinimumElevationUnits, definition.Generation.MaximumElevationUnits);
            }
        }
    }

    [Fact]
    public void ChangedSeedChangesBattlefieldIdentityAndDoesNotChangeM1CanonicalFixture()
    {
        var original = BattlefieldFixtureFactory.CreateM2OpenMeadowProfile();
        var changedSeed = BattlefieldFixtureFactory.CreateM2OpenMeadowProfile(original.Seed + 1);
        var highland = BattlefieldFixtureFactory.CreateM2BroadHighlandProfile();

        Assert.NotEqual(original.CanonicalDigest, changedSeed.CanonicalDigest);
        Assert.NotEqual(original.HeightSamples, changedSeed.HeightSamples);
        Assert.NotEqual(original.CanonicalDigest, highland.CanonicalDigest);

        var m1 = BattleFixtureFactory.CreateM1MeleeFixture();
        var committed = CommittedBattleResolution.Commit(m1);
        Assert.Equal("65ff279dac01cc31305336485af41cb595c37137edd25e04753aa5c62ec84787", m1.CanonicalInputDigest);
        Assert.Equal("75dc2d6f0dda9fc71c364a5c265f1c640ce7bc949435f7522dc08896272faf58", committed.Resolution.Transcript.CanonicalDigest);
        Assert.Equal("7faa2ec40c0830f317f4c87296c2e54c27d6f43def9b18f92d113834e982da1c", committed.Resolution.Result.CanonicalDigest);
        Assert.Same(committed.Resolution.Result, committed.SkipToResult());
        Assert.Same(committed.Resolution, committed.WatchResolution);
        Assert.True(committed.SkipToResult().IsExactlyEqualTo(committed.WatchResolution.Result));
    }

    [Fact]
    public void DefinitionCopiesCallerCollectionsAndExposesReadOnlyAcceptedViews()
    {
        var heights = new[] { 1_000, 1_100, 1_200, 1_300, 1_400, 1_500, 1_600, 1_700, 1_800 };
        var regions = Enumerable.Repeat(new StableId("terrain.middle"), heights.Length).ToArray();
        var regionIds = new[] { new StableId("terrain.middle") };
        var zones = new[]
        {
            new BattlefieldDeploymentZoneInput(new StableId("deployment.test"), new BattlefieldBounds(-800, 800, -800, 0)),
        };
        var input = new BattlefieldDefinitionInput(
            BattlefieldDefinition.CurrentSchemaVersion,
            new BattlefieldId("battlefield.test.immutable"),
            99,
            new BattlefieldBounds(-1_000, 1_000, -1_000, 1_000),
            new BattlefieldResolution(3, 3),
            new BattlefieldGenerationParameters(1_400, 200, 2, 1_000, 400, 100, 0, 3_000),
            heights,
            regions,
            regionIds,
            zones);

        heights[0] = 3_000;
        regions[0] = new StableId("terrain.changed");
        regionIds[0] = new StableId("terrain.changed");
        zones[0] = new BattlefieldDeploymentZoneInput(new StableId("deployment.changed"), new BattlefieldBounds(-500, 500, -500, 0));

        var definition = BattlefieldDefinition.Commit(input);
        var originalDigest = definition.CanonicalDigest;
        heights[1] = 3_000;
        regions[1] = new StableId("terrain.changed.again");
        zones[0] = null!;

        Assert.Equal(originalDigest, definition.CanonicalDigest);
        Assert.Equal(1_000, definition.GetHeightSample(0, 0));
        Assert.Equal(new StableId("terrain.middle"), definition.GetTerrainRegionSample(0, 0));
        Assert.Equal(new StableId("deployment.test"), definition.DeploymentZones[0].Id);

        var heightView = Assert.IsAssignableFrom<IList<int>>(definition.HeightSamples);
        var regionView = Assert.IsAssignableFrom<IList<StableId>>(definition.TerrainRegionSamples);
        var zoneView = Assert.IsAssignableFrom<IList<BattlefieldDeploymentZone>>(definition.DeploymentZones);
        Assert.True(heightView.IsReadOnly);
        Assert.True(regionView.IsReadOnly);
        Assert.True(zoneView.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => heightView[0] = 0);
        Assert.Throws<NotSupportedException>(() => regionView.Clear());
        Assert.Throws<NotSupportedException>(() => zoneView.Clear());
    }

    [Fact]
    public void MalformedDefinitionsFailValidationWithActionableCodes()
    {
        var valid = CreateSmallInput();
        Assert.False(BattlefieldDefinition.Validate(null).IsValid);

        var unsupported = new BattlefieldDefinitionInput(
            99,
            valid.BattlefieldId,
            valid.Seed,
            valid.Bounds,
            valid.Resolution,
            valid.Generation,
            valid.HeightSamples,
            valid.TerrainRegionSamples,
            valid.TerrainRegionIds,
            valid.DeploymentZones);
        Assert.True(BattlefieldDefinition.Validate(unsupported).HasCode(BattlefieldValidationCode.UnsupportedSchemaVersion));

        var invalidSamples = new BattlefieldDefinitionInput(
            valid.SchemaVersion,
            valid.BattlefieldId,
            valid.Seed,
            valid.Bounds,
            valid.Resolution,
            valid.Generation,
            new[] { 1_000 },
            valid.TerrainRegionSamples,
            valid.TerrainRegionIds,
            valid.DeploymentZones);
        var sampleValidation = BattlefieldDefinition.Validate(invalidSamples);
        Assert.True(sampleValidation.HasCode(BattlefieldValidationCode.InvalidHeightSampleCount));

        var invalidBoundsInput = new BattlefieldDefinitionInput(
            valid.SchemaVersion,
            valid.BattlefieldId,
            valid.Seed,
            new BattlefieldBounds(-2_000_000, 2_000_000, -1_000, 1_000),
            valid.Resolution,
            valid.Generation,
            valid.HeightSamples,
            valid.TerrainRegionSamples,
            valid.TerrainRegionIds,
            valid.DeploymentZones);
        Assert.True(BattlefieldDefinition.Validate(invalidBoundsInput).HasCode(BattlefieldValidationCode.InvalidBounds));

        var invalidResolutionInput = new BattlefieldDefinitionInput(
            valid.SchemaVersion,
            valid.BattlefieldId,
            valid.Seed,
            valid.Bounds,
            new BattlefieldResolution(1, 3),
            valid.Generation,
            valid.HeightSamples,
            valid.TerrainRegionSamples,
            valid.TerrainRegionIds,
            valid.DeploymentZones);
        Assert.True(BattlefieldDefinition.Validate(invalidResolutionInput).HasCode(BattlefieldValidationCode.InvalidResolution));

        var invalidHeight = valid.HeightSamples.ToArray();
        invalidHeight[0] = valid.Generation!.MaximumElevationUnits + 1;
        var invalidHeightInput = new BattlefieldDefinitionInput(
            valid.SchemaVersion,
            valid.BattlefieldId,
            valid.Seed,
            valid.Bounds,
            valid.Resolution,
            valid.Generation,
            invalidHeight,
            valid.TerrainRegionSamples,
            valid.TerrainRegionIds,
            valid.DeploymentZones);
        Assert.True(BattlefieldDefinition.Validate(invalidHeightInput).HasCode(BattlefieldValidationCode.HeightSampleOutOfBounds));

        var unknownRegionInput = new BattlefieldDefinitionInput(
            valid.SchemaVersion,
            valid.BattlefieldId,
            valid.Seed,
            valid.Bounds,
            valid.Resolution,
            valid.Generation,
            valid.HeightSamples,
            Enumerable.Repeat(new StableId("terrain.unknown"), checked((int)valid.Resolution.SampleCount)).ToArray(),
            valid.TerrainRegionIds,
            valid.DeploymentZones);
        Assert.True(BattlefieldDefinition.Validate(unknownRegionInput).HasCode(BattlefieldValidationCode.UnknownTerrainRegion));

        var duplicateRegionInput = new BattlefieldDefinitionInput(
            valid.SchemaVersion,
            valid.BattlefieldId,
            valid.Seed,
            valid.Bounds,
            valid.Resolution,
            valid.Generation,
            valid.HeightSamples,
            valid.TerrainRegionSamples,
            new[] { new StableId("terrain.middle"), new StableId("terrain.middle") },
            valid.DeploymentZones);
        Assert.True(BattlefieldDefinition.Validate(duplicateRegionInput).HasCode(BattlefieldValidationCode.DuplicateTerrainRegionId));

        var invalidDeploymentInput = new BattlefieldDefinitionInput(
            valid.SchemaVersion,
            valid.BattlefieldId,
            valid.Seed,
            valid.Bounds,
            valid.Resolution,
            valid.Generation,
            valid.HeightSamples,
            valid.TerrainRegionSamples,
            valid.TerrainRegionIds,
            new[]
            {
                new BattlefieldDeploymentZoneInput(new StableId("deployment.invalid"), new BattlefieldBounds(-950, 950, -100, 100)),
            });
        Assert.True(BattlefieldDefinition.Validate(invalidDeploymentInput).HasCode(BattlefieldValidationCode.DeploymentZoneMargin));

        var outsideDeploymentInput = new BattlefieldDefinitionInput(
            valid.SchemaVersion,
            valid.BattlefieldId,
            valid.Seed,
            valid.Bounds,
            valid.Resolution,
            valid.Generation,
            valid.HeightSamples,
            valid.TerrainRegionSamples,
            valid.TerrainRegionIds,
            new[]
            {
                new BattlefieldDeploymentZoneInput(new StableId("deployment.outside"), new BattlefieldBounds(500, 1_500, -100, 100)),
            });
        Assert.True(BattlefieldDefinition.Validate(outsideDeploymentInput).HasCode(BattlefieldValidationCode.DeploymentZoneOutOfBounds));

        var invalidGenerationRequest = new BattlefieldGenerationRequest(
            BattlefieldDefinition.CurrentSchemaVersion,
            new BattlefieldId("battlefield.invalid-generation"),
            1,
            valid.Bounds,
            valid.Resolution,
            new BattlefieldGenerationParameters(0, 1_001, 0, 0, 0, 0, 2_000, 1_000),
            valid.DeploymentZones);
        Assert.True(BattlefieldGenerator.Validate(invalidGenerationRequest).HasCode(BattlefieldValidationCode.InvalidGenerationParameters));

        var invalidSeedInput = new BattlefieldDefinitionInput(
            valid.SchemaVersion,
            valid.BattlefieldId,
            BattlefieldDefinition.MaxSeed + 1,
            valid.Bounds,
            valid.Resolution,
            valid.Generation,
            valid.HeightSamples,
            valid.TerrainRegionSamples,
            valid.TerrainRegionIds,
            valid.DeploymentZones);
        Assert.True(BattlefieldDefinition.Validate(invalidSeedInput).HasCode(BattlefieldValidationCode.SeedOutOfBounds));
    }

    [Fact]
    public void BattlefieldDigestAndQueriesAreCultureIndependent()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("tr-TR");
            var turkish = BattlefieldFixtureFactory.CreateM2BroadHighlandProfile();
            var turkishSampler = new BattlefieldHeightSampler(turkish);

            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
            var english = BattlefieldFixtureFactory.CreateM2BroadHighlandProfile();
            var englishSampler = new BattlefieldHeightSampler(english);

            Assert.Equal(english.CanonicalDigest, turkish.CanonicalDigest);
            Assert.Equal(english.HeightSamples, turkish.HeightSamples);
            Assert.Equal(english.TerrainRegionSamples, turkish.TerrainRegionSamples);
            Assert.Equal(turkishSampler.Sample(12_345, -6_789), englishSampler.Sample(12_345, -6_789));
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    private static int CoordinateAt(int minimum, int maximum, int index, int count)
    {
        return checked((int)(minimum + ((long)index * (maximum - (long)minimum) / (count - 1L))));
    }

    private static BattlefieldDefinitionInput CreateSmallInput()
    {
        var generation = new BattlefieldGenerationParameters(1_400, 200, 2, 1_000, 400, 100, 0, 3_000);
        return new BattlefieldDefinitionInput(
            BattlefieldDefinition.CurrentSchemaVersion,
            new BattlefieldId("battlefield.test.valid"),
            99,
            new BattlefieldBounds(-1_000, 1_000, -1_000, 1_000),
            new BattlefieldResolution(3, 3),
            generation,
            Enumerable.Repeat(1_400, 9),
            Enumerable.Repeat(new StableId("terrain.middle"), 9),
            new[] { new StableId("terrain.middle") },
            new[]
            {
                new BattlefieldDeploymentZoneInput(new StableId("deployment.test"), new BattlefieldBounds(-800, 800, -800, 0)),
            });
    }
}
