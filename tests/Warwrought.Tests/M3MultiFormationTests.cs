using System;
using System.Collections.Generic;
using System.Linq;
using Warwrought.Battle.Model;
using Warwrought.Battle.Playback;
using Warwrought.Battle.Simulation;
using Warwrought.Battle.Transcript;
using Warwrought.Core;
using Xunit;

namespace Warwrought.Tests;

public sealed class M3MultiFormationTests
{
    [Fact]
    public void MultiFormationFixtureCommitsThreeOrderedSquadsPerSide()
    {
        var input = M3MultiFormationFixtureFactory.CreateM3MultiFormationFixtureInput();
        var validation = BattleDefinition.Validate(input);

        Assert.True(validation.IsValid, validation.ToString());
        var definition = BattleDefinition.Commit(input);

        Assert.Equal(M3MultiFormationFixtureFactory.BattleId, definition.BattleId.Value);
        Assert.Equal(6, definition.Sides.Sum(side => side.Squads.Count));
        Assert.Equal(192, definition.GetSide(new BattleSideId("side.a")).Squads.Sum(squad => squad.Members.Count));
        Assert.Equal(192, definition.GetSide(new BattleSideId("side.b")).Squads.Sum(squad => squad.Members.Count));
        Assert.Equal(
            new[] { "squad.a.01.left", "squad.a.02.centre", "squad.a.03.right" },
            definition.GetSide(new BattleSideId("side.a")).Squads.Select(squad => squad.Id.Value));
        Assert.Equal(
            new[] { "squad.b.01.left", "squad.b.02.centre", "squad.b.03.right" },
            definition.GetSide(new BattleSideId("side.b")).Squads.Select(squad => squad.Id.Value));
        Assert.Equal(384, definition.TotalUnitCount);
    }

    [Fact]
    public void MultiFormationCommitCopiesCallerCollectionsAndRejectsMalformedSquadData()
    {
        var source = M3MultiFormationFixtureFactory.CreateM3MultiFormationFixtureInput();
        var mutableSides = source.Sides.ToArray();
        var mutableSquads = mutableSides[0].Squads.ToArray();
        var mutableMembers = mutableSquads[0].Members.ToArray();
        var copiedInput = new BattleDefinitionInput(
            source.SimulationVersion,
            source.BattleId,
            source.Seed,
            source.FixtureUnitDefinitions,
            new[]
            {
                new BattleSideInput(mutableSides[0].Id, mutableSquads),
                mutableSides[1],
            });
        var committed = BattleDefinition.Commit(copiedInput);

        mutableMembers[0] = new BattleUnitInput(
            new BattleUnitId("unit.mutated"),
            mutableSides[0].Id,
            mutableSquads[0].Id,
            mutableSquads[0].Members[0].FixtureUnitDefinitionId);
        mutableSquads[0] = new BattleSquadInput(
            new BattleSquadId("squad.mutated"),
            mutableSides[0].Id,
            mutableMembers,
            source.Sides[0].Squads[0].Formation,
            source.Sides[0].Squads[0].AdvanceOrder);

        Assert.Equal("squad.a.01.left", committed.GetSide(new BattleSideId("side.a")).Squads[0].Id.Value);
        Assert.Equal("unit.a.01.left.000", committed.OrderedUnits[0].Id.Value);

        var emptySide = new BattleDefinitionInput(
            source.SimulationVersion,
            source.BattleId,
            source.Seed,
            source.FixtureUnitDefinitions,
            new[]
            {
                new BattleSideInput(source.Sides[0].Id, Array.Empty<BattleSquadInput>()),
                source.Sides[1],
            });
        var emptyResult = BattleDefinition.Validate(emptySide);
        Assert.False(emptyResult.IsValid);
        Assert.True(emptyResult.HasCode(BattleValidationCode.InvalidSquadCount));

        var original = source.Sides[0].Squads[0];
        var malformedFormation = new BattleSquadInput(
            original.Id,
            original.SideId,
            original.Members,
            new RectangularFormationInput(
                original.Formation!.Anchor,
                original.Formation.Facing,
                0,
                original.Formation.RankCount,
                original.Formation.FileSpacingUnits,
                original.Formation.RankSpacingUnits,
                original.Formation.MemberIds),
            original.AdvanceOrder);
        var malformedResult = BattleDefinition.Validate(ReplaceSquad(source, 0, 0, malformedFormation));
        Assert.False(malformedResult.IsValid);
        Assert.True(malformedResult.HasCode(BattleValidationCode.InvalidFormationDimensions));

        var malformedDeployment = new BattleSquadInput(
            original.Id,
            original.SideId,
            original.Members,
            original.Formation,
            new AdvanceOrderInput(original.Formation.Anchor, 0));
        var deploymentResult = BattleDefinition.Validate(ReplaceSquad(source, 0, 0, malformedDeployment));
        Assert.False(deploymentResult.IsValid);
        Assert.True(deploymentResult.HasCode(BattleValidationCode.InvalidDeployment));
        Assert.True(deploymentResult.HasCode(BattleValidationCode.InvalidRange));
    }

    [Fact]
    public void RepeatedMultiFormationResolutionRetainsThreePairsAndNoCrossPairMelee()
    {
        var definition = M3MultiFormationFixtureFactory.CreateM3MultiFormationFixture();
        var first = AuthoritativeBattleResolver.Resolve(definition);
        var second = AuthoritativeBattleResolver.Resolve(M3MultiFormationFixtureFactory.CreateM3MultiFormationFixture());

        Assert.True(first.Result.IsTerminal, DescribeFinalState(first));
        Assert.Equal("9d4985222ae1381c6745636e20affa7ee5ef7cf203ef1133843c1a4d8086fe60", definition.CanonicalInputDigest);
        Assert.Equal("f8df89ca8acf722c8f77fd92c17da9691489ac50710f29319077085d0961fbc2", first.Transcript.CanonicalDigest);
        Assert.Equal("9b22f07361c5506fbdcc00871264a7a446bf7e7ebb455a3eed8449fb05352556", first.Result.CanonicalDigest);
        Assert.Equal(BattleResultType.Draw, first.Result.ResultType);
        Assert.Equal(598, first.Result.TerminalTick.Value);
        Assert.Equal("all formations reached terminal-ready paired outcomes", first.Result.DiagnosticReason);
        AssertTerminalFormationStates(first);
        Assert.Equal(first.CanonicalDigest, second.CanonicalDigest);
        Assert.Equal(first.Transcript.CanonicalDigest, second.Transcript.CanonicalDigest);
        Assert.Equal(384, first.Result.Survivors.Count + first.Result.Casualties.Count);

        var startKeyframes = first.Transcript.Keyframes
            .TakeWhile(keyframe => keyframe.Tick.Value == 0)
            .ToArray();
        Assert.Equal(6, startKeyframes.Length);
        Assert.Equal(6, startKeyframes.Select(keyframe => $"{keyframe.SideId.Value}|{keyframe.SquadId.Value}").Distinct(StringComparer.Ordinal).Count());

        var contacts = first.Transcript.Events.Where(@event => @event.Type == BattleEventType.ContactStarted).ToArray();
        Assert.Equal(3, contacts.Length);
        Assert.All(contacts, @event =>
        {
            Assert.True(@event.SideId.HasValue);
            Assert.True(@event.SquadId.HasValue);
            Assert.True(@event.OtherSideId.HasValue);
            Assert.True(@event.OtherSquadId.HasValue);
            Assert.StartsWith("squad.a.", @event.SquadId.Value.Value, StringComparison.Ordinal);
            Assert.StartsWith("squad.b.", @event.OtherSquadId.Value.Value, StringComparison.Ordinal);
            Assert.Equal(@event.SquadId.Value.Value[^3..], @event.OtherSquadId.Value.Value[^3..]);
        });

        var unitToSquad = definition.OrderedUnits.ToDictionary(unit => unit.Id.Value, unit => unit.SquadId.Value, StringComparer.Ordinal);
        var expectedOpponent = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["squad.a.01.left"] = "squad.b.01.left",
            ["squad.a.02.centre"] = "squad.b.02.centre",
            ["squad.a.03.right"] = "squad.b.03.right",
            ["squad.b.01.left"] = "squad.a.01.left",
            ["squad.b.02.centre"] = "squad.a.02.centre",
            ["squad.b.03.right"] = "squad.a.03.right",
        };
        var attacks = first.Transcript.Events.Where(@event => @event.Type == BattleEventType.AttackResolved).ToArray();
        Assert.NotEmpty(attacks);
        Assert.All(attacks, @event =>
        {
            var sourceSquad = @event.SquadId!.Value.Value;
            var targetSquad = unitToSquad[@event.TargetUnitId!.Value.Value];
            Assert.Equal(expectedOpponent[sourceSquad], targetSquad);
        });

        Assert.Contains(first.Transcript.Events, @event => @event.Type == BattleEventType.RoutStarted);
        Assert.Contains(first.Transcript.Keyframes, keyframe => keyframe.State == BattleFormationState.Routing);
        Assert.Contains(first.Transcript.Keyframes, keyframe => keyframe.State == BattleFormationState.Retreated);
        Assert.Contains(first.Transcript.Events, @event => @event.Type == BattleEventType.MoraleChanged && @event.SquadId!.Value.Value.Contains(".01.", StringComparison.Ordinal));
    }

    [Fact]
    public void StaggeredPairContactDoesNotTerminateWhileAnotherFormationRemainsUnresolved()
    {
        var source = M3MultiFormationFixtureFactory.CreateM3MultiFormationFixtureInput();
        var sideA = source.Sides[0];
        var right = sideA.Squads[2];
        var originalFormation = right.Formation!;
        var rightUnits = right.Members.Select(member => member.Id).ToArray();
        var fartherRightFormation = new RectangularFormationInput(
            new SimPosition(9_000, -30_000),
            originalFormation.Facing,
            originalFormation.FileCount,
            originalFormation.RankCount,
            originalFormation.FileSpacingUnits,
            originalFormation.RankSpacingUnits,
            rightUnits);
        var fartherRight = new BattleSquadInput(
            right.Id,
            right.SideId,
            right.Members,
            fartherRightFormation,
            new AdvanceOrderInput(new SimPosition(9_000, 30_000), right.AdvanceOrder!.StopRangeUnits));
        var staggered = ReplaceSquad(source, 0, 2, fartherRight);

        var definition = BattleDefinition.Commit(staggered);
        var resolution = AuthoritativeBattleResolver.Resolve(definition);
        var contacts = resolution.Transcript.Events
            .Where(@event => @event.Type == BattleEventType.ContactStarted)
            .ToArray();

        Assert.Equal(3, contacts.Length);
        Assert.True(contacts.Max(@event => @event.Tick.Value) > contacts.Min(@event => @event.Tick.Value));
        var firstContactTick = contacts.Min(@event => @event.Tick.Value);
        Assert.Contains(
            resolution.Transcript.Events,
            @event => @event.Type == BattleEventType.FormationMoved && @event.Tick.Value > firstContactTick && @event.SquadId!.Value.Value.Contains(".03.", StringComparison.Ordinal));
        Assert.True(resolution.Result.IsTerminal, DescribeFinalState(resolution));
        var repeated = AuthoritativeBattleResolver.Resolve(BattleDefinition.Commit(staggered));
        Assert.Equal(resolution.CanonicalDigest, repeated.CanonicalDigest);
        AssertTerminalFormationStates(resolution);
    }

    [Fact]
    public void PlaybackConsumesAllSixTickZeroFormationKeyframesAndRetainsResolution()
    {
        var resolution = AuthoritativeBattleResolver.Resolve(M3MultiFormationFixtureFactory.CreateM3MultiFormationFixture());
        var playback = new BattleTranscriptPlayback(resolution);
        var resultDigest = resolution.Result.CanonicalDigest;
        var transcriptDigest = resolution.Transcript.CanonicalDigest;

        var initial = playback.ResetAndConsume();
        Assert.Equal(6, initial.KeyframesConsumed.Count);
        playback.Clock.Play();
        playback.Clock.SetSpeed(2.0);
        while (!playback.IsComplete)
        {
            playback.Advance(1.0);
        }

        Assert.Equal(resultDigest, playback.Result.CanonicalDigest);
        Assert.Equal(transcriptDigest, playback.Transcript.CanonicalDigest);
        Assert.Equal(resolution.Transcript.EventCount, playback.EventsConsumed);
        Assert.Equal(resolution.Transcript.KeyframeCount, playback.KeyframesConsumed);
    }

    private static BattleDefinitionInput ReplaceSquad(
        BattleDefinitionInput input,
        int sideIndex,
        int squadIndex,
        BattleSquadInput replacement)
    {
        var sides = input.Sides.ToArray();
        var squads = sides[sideIndex].Squads.ToArray();
        squads[squadIndex] = replacement;
        sides[sideIndex] = new BattleSideInput(sides[sideIndex].Id, squads);
        return new BattleDefinitionInput(input.SimulationVersion, input.BattleId, input.Seed, input.FixtureUnitDefinitions, sides);
    }

    private static string DescribeFinalState(BattleResolution resolution)
    {
        var finalStates = GetFinalFormationKeyframes(resolution)
            .Values
            .OrderBy(keyframe => keyframe.SideId.Value, StringComparer.Ordinal)
            .ThenBy(keyframe => keyframe.SquadId.Value, StringComparer.Ordinal)
            .Select(keyframe => $"{keyframe.SideId.Value}/{keyframe.SquadId.Value}:{keyframe.State}:{keyframe.Morale}");
        return $"{resolution.Result.DiagnosticReason}; {string.Join(", ", finalStates)}";
    }

    private static void AssertTerminalFormationStates(BattleResolution resolution)
    {
        var finalStates = GetFinalFormationKeyframes(resolution);
        Assert.Equal(6, finalStates.Count);
        Assert.Equal(3, finalStates.Values.Count(keyframe => keyframe.State == BattleFormationState.Retreated));
        Assert.Equal(3, finalStates.Values.Count(keyframe => keyframe.State == BattleFormationState.Secured));
        Assert.DoesNotContain(finalStates.Values, keyframe => keyframe.State == BattleFormationState.Advancing);
        Assert.DoesNotContain(finalStates.Values, keyframe => keyframe.State == BattleFormationState.Engaged);
        Assert.DoesNotContain(finalStates.Values, keyframe => keyframe.State == BattleFormationState.Routing);
        Assert.All(finalStates.Values, keyframe => Assert.Contains(
            keyframe.State,
            new[] { BattleFormationState.Defeated, BattleFormationState.Retreated, BattleFormationState.Secured }));

        var finalMembers = finalStates.Values.SelectMany(keyframe => keyframe.Members).ToArray();
        Assert.Equal(resolution.Result.Survivors.Count + resolution.Result.Casualties.Count, finalMembers.Length);
        Assert.Equal(resolution.Result.Casualties.Count, finalMembers.Count(member => member.State == BattleUnitState.Dead));
        Assert.Equal(resolution.Result.Survivors.Count, finalMembers.Count(member => member.State != BattleUnitState.Dead));
        Assert.Equal(resolution.Result.RetreatedUnits.Count, finalMembers.Count(member => member.State == BattleUnitState.Retreated));
    }

    private static IReadOnlyDictionary<string, BattleFormationKeyframe> GetFinalFormationKeyframes(BattleResolution resolution)
    {
        return resolution.Transcript.Keyframes
            .GroupBy(keyframe => $"{keyframe.SideId.Value}|{keyframe.SquadId.Value}", StringComparer.Ordinal)
            .Select(group => group.Last())
            .ToDictionary(
                keyframe => $"{keyframe.SideId.Value}|{keyframe.SquadId.Value}",
                keyframe => keyframe,
                StringComparer.Ordinal);
    }
}
