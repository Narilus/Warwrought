using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Warwrought.Battle.Model;
using Warwrought.Battle.Simulation;
using Warwrought.Battle.Transcript;
using Warwrought.Core;
using Xunit;

namespace Warwrought.Tests;

public sealed class M1AuthoritativeResolutionTests
{
    [Fact]
    public void RepeatedCanonicalFixtureResolutionIsStableAndClassifiesOrderedRecords()
    {
        var first = ResolveFixture();
        var second = ResolveFixture();
        var third = ResolveFixture();

        Assert.True(first.Result.IsTerminal, first.Result.DiagnosticReason);
        Assert.NotEqual(BattleResultType.NonTerminalFailure, first.Result.ResultType);
        Assert.Equal(first.CanonicalDigest, second.CanonicalDigest);
        Assert.Equal(second.CanonicalDigest, third.CanonicalDigest);
        Assert.Equal(first.Transcript.CanonicalDigest, second.Transcript.CanonicalDigest);
        Assert.Equal("75dc2d6f0dda9fc71c364a5c265f1c640ce7bc949435f7522dc08896272faf58", first.Transcript.CanonicalDigest);
        Assert.Equal("7faa2ec40c0830f317f4c87296c2e54c27d6f43def9b18f92d113834e982da1c", first.Result.CanonicalDigest);
        Assert.Equal(BattleTranscriptHeader.TranscriptIdentity, first.Transcript.Header.Identity);
        Assert.Equal(BattleTranscriptHeader.CurrentVersion, first.Transcript.Header.Version);
        Assert.Equal(BattleFixtureFactory.DefaultSeed, first.Transcript.Header.Seed);
        Assert.Equal(first.Result.TerminalTick, second.Result.TerminalTick);
        Assert.Equal(
            first.Result.OrderedSurvivors.Select(record => record.UnitId.Value),
            second.Result.OrderedSurvivors.Select(record => record.UnitId.Value));
        Assert.Equal(
            first.Result.OrderedCasualties.Select(record => record.UnitId.Value),
            second.Result.OrderedCasualties.Select(record => record.UnitId.Value));

        Assert.NotEmpty(first.Result.OrderedSurvivors);
        Assert.NotEmpty(first.Result.OrderedCasualties);
        Assert.NotEmpty(first.Result.OrderedRoutedUnits);
        Assert.NotEmpty(first.Result.OrderedRetreatedUnits);
        Assert.All(first.Result.OrderedRoutedUnits, record => Assert.True(record.IsRouted));
        Assert.All(first.Result.OrderedRetreatedUnits, record => Assert.True(record.IsRetreated));

        var allResultIds = first.Result.OrderedSurvivors
            .Select(record => record.UnitId.Value)
            .Concat(first.Result.OrderedCasualties.Select(record => record.UnitId.Value))
            .ToArray();
        Assert.Equal(128, allResultIds.Length);
        Assert.Equal(allResultIds.Length, allResultIds.Distinct(StringComparer.Ordinal).Count());
        Assert.True(IsOrdinalAscending(first.Result.OrderedSurvivors.Select(record => record.UnitId.Value).ToArray()));
        Assert.True(IsOrdinalAscending(first.Result.OrderedCasualties.Select(record => record.UnitId.Value).ToArray()));
        Assert.Equal(first.Result.OrderedRoutedUnits.Select(record => record.UnitId), first.Result.OrderedRetreatedUnits.Select(record => record.UnitId));

        var killEvents = first.Transcript.SemanticEvents.Where(@event => @event.Type == BattleEventType.UnitKilled).ToArray();
        var killedIds = killEvents.Select(@event => @event.TargetUnitId!.Value.Value).ToArray();
        Assert.Equal(killedIds.Length, killedIds.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(first.Result.Casualties.Count, killEvents.Length);
    }

    [Fact]
    public void ChangedAuthoritativeSeedChangesSeededDamageStreamAndResolutionDigest()
    {
        var original = ResolveFixture();
        var changedSeed = AuthoritativeBattleResolver.Resolve(
            BattleFixtureFactory.CreateM1MeleeFixture(BattleFixtureFactory.DefaultSeed + 1));

        Assert.NotEqual(original.CanonicalDigest, changedSeed.CanonicalDigest);
        Assert.NotEqual(original.Transcript.CanonicalDigest, changedSeed.Transcript.CanonicalDigest);
        Assert.NotEqual(
            DamageSignature(original),
            DamageSignature(changedSeed));
        Assert.Equal(BattleFixtureFactory.DefaultSeed + 1, changedSeed.Transcript.Header.Seed);
    }

    [Fact]
    public void TranscriptLifecycleIsOrderedAndKilledOrRoutedMembersNeverPairLater()
    {
        var resolution = ResolveFixture();
        var events = resolution.Transcript.SemanticEvents;

        Assert.Equal(BattleEventType.BattleStarted, events[0].Type);
        Assert.Equal(BattleEventType.BattleEnded, events[^1].Type);
        Assert.True(events[^1].IsTerminal);
        for (var index = 0; index < events.Count; index++)
        {
            Assert.Equal(index, events[index].Sequence);
            if (index > 0)
            {
                Assert.True(events[index - 1].Tick <= events[index].Tick);
            }
        }

        var movementIndex = IndexOf(events, BattleEventType.FormationMoved);
        var contactStartedIndex = IndexOf(events, BattleEventType.ContactStarted);
        var attackIndex = IndexOf(events, BattleEventType.AttackResolved);
        var damageIndex = IndexOf(events, BattleEventType.DamageDealt);
        var killedIndex = IndexOf(events, BattleEventType.UnitKilled);
        var moraleIndex = IndexOf(events, BattleEventType.MoraleChanged);
        var routIndex = IndexOf(events, BattleEventType.RoutStarted);
        var contactEndedIndex = IndexOf(events, BattleEventType.ContactEnded);
        var retreatIndex = IndexOf(events, BattleEventType.RetreatCompleted);
        var endIndex = IndexOf(events, BattleEventType.BattleEnded);

        Assert.Single(events, @event => @event.Type == BattleEventType.ContactStarted);
        Assert.Single(events, @event => @event.Type == BattleEventType.ContactEnded);
        Assert.Single(events, @event => @event.Type == BattleEventType.RoutStarted);
        var movementEvents = events.Where(@event => @event.Type == BattleEventType.FormationMoved).ToArray();
        Assert.NotEmpty(movementEvents);
        Assert.All(movementEvents, @event =>
        {
            Assert.Equal(M1BattleSettings.FixtureAdvanceStepUnits, @event.Amount);
            Assert.True(@event.Position.HasValue);
            Assert.True(@event.Facing.HasValue);
            Assert.True(@event.Facing.Value is FormationFacing.North or FormationFacing.South);
        });

        Assert.True(movementIndex < contactStartedIndex);
        Assert.True(contactStartedIndex < attackIndex);
        Assert.True(attackIndex < damageIndex);
        Assert.True(damageIndex < killedIndex);
        Assert.True(killedIndex < moraleIndex);
        Assert.True(moraleIndex < routIndex);
        Assert.True(routIndex < contactEndedIndex);
        Assert.True(contactEndedIndex < retreatIndex);
        Assert.True(retreatIndex < endIndex);

        var firstDamage = events[damageIndex];
        Assert.Equal(BattleEventType.AttackResolved, events[damageIndex - 1].Type);
        Assert.Equal(firstDamage.SourceUnitId, events[damageIndex - 1].SourceUnitId);
        Assert.Equal(firstDamage.TargetUnitId, events[damageIndex - 1].TargetUnitId);

        var routTick = events[routIndex].Tick;
        Assert.DoesNotContain(events.Skip(routIndex + 1), @event => @event.Type == BattleEventType.AttackResolved);
        Assert.DoesNotContain(events.Skip(routIndex + 1), @event => @event.Type == BattleEventType.UnitKilled);
        Assert.All(
            events.Where(@event => @event.Type is BattleEventType.AttackResolved or BattleEventType.DamageDealt),
            @event => Assert.True(@event.Tick <= routTick));

        var killedAtSequence = events
            .Where(@event => @event.Type == BattleEventType.UnitKilled)
            .ToDictionary(@event => @event.TargetUnitId!.Value.Value, @event => @event.Sequence, StringComparer.Ordinal);
        Assert.All(
            events.Where(@event => @event.Type == BattleEventType.AttackResolved),
            @event =>
            {
                Assert.True(!killedAtSequence.TryGetValue(@event.SourceUnitId!.Value.Value, out var sourceKilledAt) || sourceKilledAt > @event.Sequence);
                Assert.True(!killedAtSequence.TryGetValue(@event.TargetUnitId!.Value.Value, out var targetKilledAt) || targetKilledAt > @event.Sequence);
            });

        Assert.Contains(resolution.Transcript.Keyframes, keyframe => keyframe.Members.Any(member => member.State == BattleUnitState.Dead));
        Assert.Contains(resolution.Transcript.Keyframes, keyframe => keyframe.State == BattleFormationState.Routing);
        Assert.Contains(resolution.Transcript.Keyframes, keyframe => keyframe.State == BattleFormationState.Retreated);
        Assert.All(resolution.Transcript.Keyframes, keyframe =>
        {
            Assert.Equal(64, keyframe.Members.Count);
            Assert.All(keyframe.Members, member =>
            {
                if (member.State == BattleUnitState.Dead)
                {
                    Assert.Equal(-1, member.SlotIndex);
                }
                else
                {
                    Assert.True(member.IsAlive);
                    Assert.True(member.SlotIndex >= 0);
                }
            });
        });

        var routedSideId = events[routIndex].SideId!.Value;
        var routedKeyframe = resolution.Transcript.Keyframes
            .Where(keyframe => keyframe.SideId == routedSideId && keyframe.Tick <= events[routIndex].Tick)
            .Last();
        var retreatKeyframe = resolution.Transcript.Keyframes
            .Where(keyframe => keyframe.SideId == routedSideId)
            .Last();
        Assert.True(
            retreatKeyframe.Anchor.ManhattanDistanceTo(new SimPosition(0, 0)) >
            routedKeyframe.Anchor.ManhattanDistanceTo(new SimPosition(0, 0)));
    }

    [Fact]
    public void CanonicalFixtureContainsDamageEventsWithoutRequiringARecentCasualty()
    {
        var resolution = ResolveFixture();
        var damageEvents = resolution.Transcript.SemanticEvents
            .Where(@event => @event.Type == BattleEventType.DamageDealt)
            .ToArray();

        Assert.NotEmpty(damageEvents);
        Assert.Contains(
            damageEvents,
            damage => damage.TargetUnitId.HasValue &&
                      resolution.Result.Casualties.All(casualty => casualty.UnitId != damage.TargetUnitId.Value));
    }

    [Fact]
    public void NoDamageFixtureReturnsExplicitSafetyCapFailureWithoutFabricatingWinner()
    {
        var noDamage = CreateNoDamageFixture();
        var resolution = AuthoritativeBattleResolver.Resolve(noDamage);

        Assert.False(resolution.Result.IsTerminal);
        Assert.True(resolution.Result.IsFailure);
        Assert.Equal(BattleResultType.NonTerminalFailure, resolution.Result.ResultType);
        Assert.Null(resolution.Result.WinnerSideId);
        Assert.Equal(M1BattleSettings.MaximumSimulationTicks - 1L, resolution.Result.TerminalTick.Value);
        Assert.Contains("maximum logical tick", resolution.Result.DiagnosticReason, StringComparison.Ordinal);
        Assert.Equal(BattleEventType.BattleEnded, resolution.Transcript.SemanticEvents[^1].Type);
        Assert.False(resolution.Transcript.SemanticEvents[^1].IsTerminal);

        var attacks = resolution.Transcript.SemanticEvents.Where(@event => @event.Type == BattleEventType.AttackResolved).ToArray();
        var damages = resolution.Transcript.SemanticEvents.Where(@event => @event.Type == BattleEventType.DamageDealt).ToArray();
        Assert.NotEmpty(attacks);
        Assert.Equal(attacks.Length, damages.Length);
        Assert.Contains(damages, @event => @event.Amount == 0);
        Assert.DoesNotContain(resolution.Transcript.SemanticEvents, @event => @event.Type == BattleEventType.UnitKilled);
        Assert.DoesNotContain(resolution.Transcript.SemanticEvents, @event => @event.Type == BattleEventType.RoutStarted);
    }

    [Fact]
    public void ResolverRunsWithoutPresentationNodesAndDebugArtifactRoundTripsCanonicalData()
    {
        var resolution = ResolveFixture();
        var json = BattleDebugSerializer.Serialize(resolution);
        var document = BattleDebugSerializer.Deserialize(json);

        Assert.Contains("warwrought.battle-debug-artifact.m1.v1", json, StringComparison.Ordinal);
        Assert.Equal(resolution.Transcript.Header.BattleId.Value, document.BattleId);
        Assert.Equal(resolution.Transcript.Header.Seed, document.Seed);
        Assert.Equal(resolution.Transcript.CanonicalDigest, document.TranscriptDigest);
        Assert.Equal(resolution.CanonicalDigest, document.CanonicalDigest);
        Assert.Equal(resolution.Transcript.Events.Count, document.Events.Count);
        Assert.Equal(resolution.Transcript.Keyframes.Count, document.Keyframes.Count);
        Assert.Equal(resolution.Result.Casualties.Count, document.Result.Casualties.Count);
        Assert.Equal(resolution.Result.Survivors.Count, document.Result.Survivors.Count);
        Assert.Equal(
            resolution.Transcript.Events.Select(@event => $"{@event.Sequence}:{@event.Tick.Value}:{@event.Type}:{@event.Amount}:{@event.Value}:{@event.TargetUnitId?.Value}"),
            document.Events.Select(@event => $"{@event.Sequence}:{@event.Tick}:{@event.Type}:{@event.Amount}:{@event.Value}:{(@event.HasTargetUnitId ? @event.TargetUnitId : string.Empty)}"));
        Assert.Equal(
            resolution.Transcript.Keyframes.Select(keyframe => $"{keyframe.Tick.Value}:{keyframe.SideId.Value}:{keyframe.Anchor.X}:{keyframe.Anchor.Z}:{keyframe.State}:{keyframe.Members.Count}"),
            document.Keyframes.Select(keyframe => $"{keyframe.Tick}:{keyframe.SideId}:{keyframe.AnchorX}:{keyframe.AnchorZ}:{keyframe.State}:{keyframe.Members.Count}"));

        var artifactPath = Path.Combine(Path.GetTempPath(), $"warwrought-m1.2-{Guid.NewGuid():N}.json");
        try
        {
            BattleDebugSerializer.WriteJson(artifactPath, resolution);
            var fileDocument = BattleDebugSerializer.ReadJson(artifactPath);
            Assert.Equal(resolution.CanonicalDigest, fileDocument.CanonicalDigest);
        }
        finally
        {
            if (File.Exists(artifactPath))
            {
                File.Delete(artifactPath);
            }
        }
    }

    private static BattleResolution ResolveFixture()
    {
        return AuthoritativeBattleResolver.Resolve(BattleFixtureFactory.CreateM1MeleeFixture());
    }

    private static BattleDefinition CreateNoDamageFixture()
    {
        var input = BattleFixtureFactory.CreateM1MeleeFixtureInput();
        var definitions = input.FixtureUnitDefinitions
            .Select(definition => new FixtureUnitDefinition(
                definition.DefinitionId,
                definition.MaximumHealth,
                1,
                100,
                definition.StartingMorale,
                definition.MoraleLossPerCasualty,
                definition.MeleeRangeUnits,
                definition.AttackCooldownTicks))
            .ToArray();

        return BattleDefinition.Commit(new BattleDefinitionInput(
            input.SimulationVersion,
            input.BattleId,
            input.Seed,
            definitions,
            input.Sides));
    }

    private static string DamageSignature(BattleResolution resolution)
    {
        return string.Join(
            ";",
            resolution.Transcript.SemanticEvents
                .Where(@event => @event.Type == BattleEventType.DamageDealt)
                .Select(@event => $"{@event.Tick.Value}:{@event.SourceUnitId?.Value}:{@event.TargetUnitId?.Value}:{@event.Amount}:{@event.Value}"));
    }

    private static int IndexOf(IReadOnlyList<BattleSemanticEvent> events, BattleEventType type)
    {
        for (var index = 0; index < events.Count; index++)
        {
            if (events[index].Type == type)
            {
                return index;
            }
        }

        throw new Xunit.Sdk.XunitException($"Expected transcript event family {type}.");
    }

    private static bool IsOrdinalAscending(IReadOnlyList<string> values)
    {
        for (var index = 1; index < values.Count; index++)
        {
            if (StringComparer.Ordinal.Compare(values[index - 1], values[index]) >= 0)
            {
                return false;
            }
        }

        return true;
    }
}
