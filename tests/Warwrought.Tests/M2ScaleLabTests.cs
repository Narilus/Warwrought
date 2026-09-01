using System.Linq;
using Warwrought.Battle.Model;
using Warwrought.Battle.Simulation;
using Warwrought.Battle.Transcript;
using Warwrought.Bootstrap;
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
    public void ScaleLabArgumentsDeclareOnlyThePhaseOneAuthoritativeScenario()
    {
        var valid = BattleScaleLabArguments.Parse(
            new[] { "--acceptance=battlescalelab.m2.100v100", "--report=artifacts/local/m2.3/scale.json" });
        var highland = BattleScaleLabArguments.Parse(
            new[] { "--acceptance=battlescalelab.m2.100v100", "--battlefield-profile=battlefield.m2.broad-highland", "--report=artifacts/local/m2.3/scale.json" });
        var wrongScenario = BattleScaleLabArguments.Parse(
            new[] { "--acceptance=battlescalelab.m2.300v300", "--report=artifacts/local/m2.3/scale.json" });

        Assert.True(valid.IsAcceptanceMode);
        Assert.Equal(BattleScaleLabArguments.AcceptanceScenario, valid.ScenarioId);
        Assert.Equal(BattleScaleLabArguments.OpenMeadowProfile, valid.BattlefieldProfileId);
        Assert.True(highland.IsAcceptanceMode);
        Assert.Equal(BattleScaleLabArguments.BroadHighlandProfile, highland.BattlefieldProfileId);
        Assert.False(wrongScenario.IsValid);
        Assert.Contains("Unsupported acceptance scenario", wrongScenario.ValidationError, StringComparison.Ordinal);
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
