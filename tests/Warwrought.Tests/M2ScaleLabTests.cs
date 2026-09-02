using System.Linq;
using Warwrought.Battle.Model;
using Warwrought.Battle.Simulation;
using Warwrought.Battle.Transcript;
using Warwrought.Bootstrap;
using Warwrought.Presentation.Battle;
using Xunit;

namespace Warwrought.Tests;

public sealed class M2ScaleLabTests
{
    [Fact]
    public void ScaleFixtureIsDeterministicAndHasExactlyOneHundredOrderedUnitsPerSide()
    {
        var first = BattleScaleFixtureFactory.Create100v100Fixture();
        var second = BattleScaleFixtureFactory.Create100v100Fixture();

        Assert.Equal(BattleScaleFixtureFactory.DefaultSeed, first.Seed);
        Assert.Equal(2, first.Sides.Count);
        Assert.Equal(200, first.TotalUnitCount);
        Assert.Equal(first.CanonicalInputDigest, second.CanonicalInputDigest);
        Assert.Equal(first.OrderedUnits.Select(unit => unit.Id), second.OrderedUnits.Select(unit => unit.Id));

        for (var sideIndex = 0; sideIndex < first.Sides.Count; sideIndex++)
        {
            var squad = first.Sides[sideIndex].Squads.Single();
            Assert.Equal(100, squad.Members.Count);
            Assert.Equal(10, squad.Formation.FileCount);
            Assert.Equal(10, squad.Formation.RankCount);
            Assert.Equal(100, squad.Formation.SlotCount);

            for (var memberIndex = 0; memberIndex < squad.Members.Count; memberIndex++)
            {
                Assert.Equal(squad.Members[memberIndex].Id, squad.Formation.Slots[memberIndex].UnitId);
                Assert.Equal(memberIndex / 10, squad.Formation.Slots[memberIndex].Rank);
                Assert.Equal(memberIndex % 10, squad.Formation.Slots[memberIndex].File);
                if (memberIndex > 0)
                {
                    Assert.True(
                        squad.Members[memberIndex - 1].Id.CompareTo(squad.Members[memberIndex].Id) < 0,
                        "Scale fixture member IDs must be ordinal and increasing.");
                }
            }
        }

        Assert.Equal("unit.scale.a.000", first.GetSide(new BattleSideId("side.a")).Squads.Single().Members[0].Id.Value);
        Assert.Equal("unit.scale.b.099", first.GetSide(new BattleSideId("side.b")).Squads.Single().Members[^1].Id.Value);
        Assert.True(BattleDefinition.Validate(BattleScaleFixtureFactory.Create100v100FixtureInput()).IsValid);
    }

    [Fact]
    public void ScaleFixtureUsesTheCommittedAuthoritativeResolutionIdentityForSkipAndWatch()
    {
        var committed = CommittedBattleResolution.Commit(BattleScaleFixtureFactory.Create100v100Fixture());

        Assert.Same(committed.Resolution, committed.WatchResolution);
        Assert.Same(committed.Resolution.Result, committed.SkipToResult());
        Assert.Equal(200, committed.Definition.TotalUnitCount);
        Assert.Equal(200, committed.Resolution.Result.Survivors.Count + committed.Resolution.Result.Casualties.Count);
        Assert.NotEmpty(committed.Resolution.Transcript.Events);
        Assert.NotEmpty(committed.Resolution.Transcript.Keyframes);
        Assert.True(committed.SkipToResult().IsExactlyEqualTo(committed.WatchResolution.Result));
        Assert.Equal(BattleScaleFixtureFactory.SourceClassification, BattleScaleLabReport.AuthoritativeRealResolverSource);
    }

    [Fact]
    public void ScaleLabArgumentsDeclareAuthoritativeAndPresentationStressScenarios()
    {
        var valid = BattleScaleLabArguments.Parse(
            new[] { "--acceptance=battlescalelab.m2.100v100", "--report=artifacts/local/m2.3/scale.json" });
        var highland = BattleScaleLabArguments.Parse(
            new[] { "--acceptance=battlescalelab.m2.100v100", "--battlefield-profile=battlefield.m2.broad-highland", "--report=artifacts/local/m2.3/scale.json" });
        var synthetic300 = BattleScaleLabArguments.Parse(
            new[] { "--acceptance=battlescalelab.m2.300v300", "--report=artifacts/local/m2.3/scale.json" });

        Assert.True(valid.IsAcceptanceMode);
        Assert.Equal(BattleScaleLabArguments.AcceptanceScenario, valid.ScenarioId);
        Assert.Equal(BattleScaleLabArguments.OpenMeadowProfile, valid.BattlefieldProfileId);
        Assert.True(highland.IsAcceptanceMode);
        Assert.Equal(BattleScaleLabArguments.BroadHighlandProfile, highland.BattlefieldProfileId);
        Assert.True(synthetic300.IsAcceptanceMode);
        Assert.Equal(BattleScaleLabArguments.Presentation300Scenario, synthetic300.ScenarioId);

        var visible500 = BattleScaleLabArguments.Parse(
            new[] { "--scale-scenario=battlescalelab.m2.500v500", "--battlefield-profile=battlefield.m2.broad-highland" });
        Assert.True(visible500.IsValid);
        Assert.False(visible500.IsAcceptanceRequested);
        Assert.Equal(BattleScaleLabArguments.Presentation500Scenario, visible500.ScenarioId);
        Assert.Equal(BattleScaleLabArguments.BroadHighlandProfile, visible500.BattlefieldProfileId);
    }

    [Theory]
    [InlineData(300, "battlescalelab.m2.300v300", "battle.m2.scale.300v300.presentation", 83003, 20, 15)]
    [InlineData(500, "battlescalelab.m2.500v500", "battle.m2.scale.500v500.presentation", 83005, 25, 20)]
    public void PresentationStressFixturesAreDeterministicOrderedAndExplicitlyNonAuthoritative(
        int unitsPerSide,
        string scenarioId,
        string battleId,
        ulong seed,
        int fileCount,
        int rankCount)
    {
        var first = unitsPerSide == 300
            ? BattleScalePresentationStressFactory.Create300v300()
            : BattleScalePresentationStressFactory.Create500v500();
        var second = unitsPerSide == 300
            ? BattleScalePresentationStressFactory.Create300v300()
            : BattleScalePresentationStressFactory.Create500v500();

        Assert.Equal(scenarioId, first.ScenarioId);
        Assert.Equal(BattleScalePresentationStressFactory.SourceClassification, first.SourceClassification);
        Assert.Equal(battleId, first.Resolution.Result.BattleId.Value);
        Assert.Equal(seed, first.Resolution.Result.Seed);
        Assert.Equal(unitsPerSide, first.RequestedUnitsPerSide);
        Assert.Equal(unitsPerSide * 2, first.ExpectedTotalUnitCount);
        Assert.Equal(fileCount, first.FormationFileCount);
        Assert.Equal(rankCount, first.FormationRankCount);
        Assert.Equal(first.SourceIdentityDigest, second.SourceIdentityDigest);
        Assert.Equal(first.Resolution.Transcript.CanonicalDigest, second.Resolution.Transcript.CanonicalDigest);
        Assert.Equal(first.Resolution.Result.CanonicalDigest, second.Resolution.Result.CanonicalDigest);
        Assert.Equal(unitsPerSide * 2, first.Resolution.Result.Survivors.Count + first.Resolution.Result.Casualties.Count);
        Assert.Equal(30, first.Resolution.Result.Casualties.Count);
        Assert.Equal(first.SourceIdentityDigest, first.Resolution.Transcript.Header.CanonicalInputDigest);
        Assert.Equal(10, first.Resolution.Transcript.Keyframes.Count);
        Assert.Equal($"unit.synthetic.a.000", first.Resolution.Transcript.Keyframes[0].Members[0].UnitId.Value);
        Assert.Equal($"unit.synthetic.b.{unitsPerSide - 1:000}", first.Resolution.Transcript.Keyframes[1].Members[^1].UnitId.Value);
    }

    [Fact]
    public void ScaleProfilingWindowExcludesWarmupAndComputesMedianAndP95FromBoundedSamples()
    {
        var summary = BattleScaleProfilingMetrics.Summarize(
            new[] { 1.0, 2.0, 3.0, 4.0, 5.0, 6.0 },
            warmupDurationMilliseconds: 3.1,
            sampleWindowDurationMilliseconds: 9.0);

        Assert.Equal(3, summary.WarmupExcludedSampleCount);
        Assert.Equal(6.0, summary.WarmupExcludedDurationMilliseconds);
        Assert.Equal(2, summary.Samples.Count);
        Assert.Equal(9.0, summary.MeasuredSampleWindowDurationMilliseconds);
        Assert.Equal(4.5, summary.MedianMilliseconds);
        Assert.Equal(4.95, summary.P95Milliseconds, precision: 10);
    }

    [Fact]
    public void ScaleReportValidationRequiresProfilingEvidenceAndRejectsAuthoritativeClaimsForSyntheticSources()
    {
        var report = new BattleScaleLabReport
        {
            Scenario = BattleScaleLabArguments.Presentation300Scenario,
            Scene = BattleScaleLab.SceneIdentity,
            ScenePath = BattleScaleLab.ScenePath,
            GodotVersion = "4.7.1",
            BuildRuntimeIdentifier = "test",
            RuntimeIdentifier = "test",
            ProjectIdentity = "Warwrought",
            ProjectVersion = "0.1.0",
            ResolutionSourceClassification = BattleScalePresentationStressFactory.SourceClassification,
            AuthoritativeInputDigest = "must-not-be-present",
            Passed = true,
        };

        var errors = report.Validate();

        Assert.Contains(errors, error => error.Contains("must not claim an authoritative input digest", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("bounded production frame samples", StringComparison.Ordinal));
    }

    [Fact]
    public void ScaleFoundationDoesNotChangeCanonicalM1Identity()
    {
        var m1 = BattleFixtureFactory.CreateM1MeleeFixture();

        Assert.Equal("65ff279dac01cc31305336485af41cb595c37137edd25e04753aa5c62ec84787", m1.CanonicalInputDigest);
        var committed = CommittedBattleResolution.Commit(m1);
        Assert.Equal("75dc2d6f0dda9fc71c364a5c265f1c640ce7bc949435f7522dc08896272faf58", committed.Resolution.Transcript.CanonicalDigest);
        Assert.Equal("7faa2ec40c0830f317f4c87296c2e54c27d6f43def9b18f92d113834e982da1c", committed.Resolution.Result.CanonicalDigest);
        Assert.Equal(BattleResultType.SideAWin, committed.Resolution.Result.ResultType);
        Assert.Equal(576, committed.Resolution.Result.TerminalTick.Value);
        Assert.Equal(100, committed.Resolution.Result.Survivors.Count);
        Assert.Equal(28, committed.Resolution.Result.Casualties.Count);
        Assert.Equal(49, committed.Resolution.Result.RetreatedUnits.Count);
    }
}
