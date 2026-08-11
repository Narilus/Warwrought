using System;
using System.Collections.Generic;
using System.Linq;
using Warwrought.Battle.Model;
using Warwrought.Battle.Playback;
using Warwrought.Battle.Simulation;
using Warwrought.Battle.Transcript;
using Warwrought.Bootstrap;
using Xunit;

namespace Warwrought.Tests;

public sealed class M1BattlePlaybackTests
{
    [Fact]
    public void PlaybackMapsPresentationTickBetweenAuthoritativeKeyframes()
    {
        var resolution = ResolveFixture();
        var playback = new BattleTranscriptPlayback(resolution);
        var initial = playback.ResetAndConsume();

        Assert.Equal(0, initial.CurrentTick);
        Assert.Equal(2, initial.KeyframesConsumed.Count);
        Assert.Equal(BattleEventType.BattleStarted, initial.EventsConsumed[0].Type);

        var sideAKeyframes = resolution.Transcript.Keyframes
            .Where(keyframe => keyframe.SideId.Value == "side.a")
            .ToArray();
        var first = sideAKeyframes[0];
        var second = sideAKeyframes[1];
        var unitId = first.Members[0].UnitId;
        var halfwayTick = (first.Tick.Value + second.Tick.Value) / 2.0;
        var sample = playback.SampleMemberPosition(unitId, halfwayTick);

        Assert.Equal(unitId, sample.UnitId);
        Assert.Equal(0.5, sample.Alpha, precision: 10);
        Assert.Equal((first.Members[0].Position.X + second.Members[0].Position.X) / 2.0, sample.X, precision: 10);
        Assert.Equal((first.Members[0].Position.Z + second.Members[0].Position.Z) / 2.0, sample.Z, precision: 10);
        Assert.Equal(BattleUnitState.Active, sample.State);
    }

    [Fact]
    public void PlaybackDispatchesEventsInStoredAuthoritativeOrder()
    {
        var resolution = ResolveFixture();
        var playback = new BattleTranscriptPlayback(resolution);
        var dispatched = new List<BattleSemanticEvent>();
        var initial = playback.ResetAndConsume();
        dispatched.AddRange(initial.EventsConsumed);

        playback.Clock.Play();
        while (!playback.IsComplete)
        {
            dispatched.AddRange(playback.Advance(1.0).EventsConsumed);
        }

        Assert.Equal(resolution.Transcript.EventCount, dispatched.Count);
        Assert.Equal(
            resolution.Transcript.Events.Select(@event => @event.Sequence),
            dispatched.Select(@event => @event.Sequence));
        Assert.Equal(BattleEventType.BattleStarted, dispatched[0].Type);
        Assert.Equal(BattleEventType.BattleEnded, dispatched[^1].Type);
        Assert.True(dispatched[^1].IsTerminal);
    }

    [Fact]
    public void PlaybackControlsChangeOnlyPresentationClockAndRetainAuthoritativeDigests()
    {
        var resolution = ResolveFixture();
        var playback = new BattleTranscriptPlayback(resolution);
        var resultDigest = resolution.Result.CanonicalDigest;
        var transcriptDigest = resolution.Transcript.CanonicalDigest;

        playback.ResetAndConsume();
        playback.Clock.Play();
        playback.Clock.SetSpeed(1.0);
        playback.Clock.Pause();
        playback.Clock.Play();
        playback.Clock.SetSpeed(2.0);
        playback.Advance(0.25);
        playback.Clock.SetSpeed(8.0);
        playback.Clock.Pause();
        playback.Clock.Reset();

        Assert.Equal(resultDigest, playback.Result.CanonicalDigest);
        Assert.Equal(transcriptDigest, playback.Transcript.CanonicalDigest);
        Assert.Equal(resultDigest, resolution.CanonicalDigest);
        Assert.True(playback.Clock.ControlTransitions >= 8);
        Assert.Equal(0, playback.Clock.CurrentTick);
    }

    [Fact]
    public void BattleLabAcceptanceArgumentsAllowOnlyTheDeclaredScenarioAndExplicitReport()
    {
        var valid = BattleLabArguments.Parse(
            new[] { "--acceptance=battlelab.m1.melee", "--report=artifacts/local/m1.3/report.json" });
        var wrongScenario = BattleLabArguments.Parse(
            new[] { "--acceptance=battlelab.m1.bad", "--report=artifacts/local/m1.3/report.json" });
        var unknownOption = BattleLabArguments.Parse(
            new[] { "--acceptance=battlelab.m1.melee", "--report=report.json", "--speed=8" });
        var missingReport = BattleLabArguments.Parse(new[] { "--acceptance=battlelab.m1.melee" });
        var normal = BattleLabArguments.Parse(Array.Empty<string>());

        Assert.True(valid.IsAcceptanceMode);
        Assert.Equal(BattleLabArguments.AcceptanceScenario, valid.ScenarioId);
        Assert.False(wrongScenario.IsValid);
        Assert.Contains("Unsupported acceptance scenario", wrongScenario.ValidationError, StringComparison.Ordinal);
        Assert.False(unknownOption.IsValid);
        Assert.Contains("Unknown BattleLab user argument", unknownOption.ValidationError, StringComparison.Ordinal);
        Assert.False(missingReport.IsValid);
        Assert.Contains("--report", missingReport.ValidationError, StringComparison.Ordinal);
        Assert.True(normal.IsValid);
        Assert.False(normal.IsAcceptanceRequested);
    }

    private static BattleResolution ResolveFixture()
    {
        return AuthoritativeBattleResolver.Resolve(BattleFixtureFactory.CreateM1MeleeFixture());
    }
}
