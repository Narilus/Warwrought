using System.Linq;
using Warwrought.Battle.Model;
using Warwrought.Battle.Playback;
using Warwrought.Battle.Simulation;
using Warwrought.Battle.Transcript;
using Xunit;

namespace Warwrought.Tests;

public sealed class M1CommittedResolutionTests
{
    [Fact]
    public void RepeatedCommittedFixtureResolutionIsStableAndSkipWatchShareIdentity()
    {
        var first = CommitFixture();
        var second = CommitFixture();

        var firstSkip = first.SkipToResult();
        var firstWatchResolution = first.WatchResolution;
        var firstWatch = firstWatchResolution.Result;

        Assert.NotSame(first.Resolution, second.Resolution);
        Assert.Same(first.Resolution, firstWatchResolution);
        Assert.Same(first.Resolution.Result, firstSkip);
        Assert.Same(first.Resolution.Result, firstWatch);
        Assert.True(firstSkip.IsExactlyEqualTo(firstWatch));
        Assert.True(first.Resolution.Result.IsExactlyEqualTo(second.Resolution.Result));
        Assert.Equal(first.Resolution.Transcript.CanonicalDigest, second.Resolution.Transcript.CanonicalDigest);
        Assert.Equal(first.Resolution.CanonicalDigest, second.Resolution.CanonicalDigest);
    }

    [Fact]
    public void SkipWatchEqualityCoversAllCampaignRelevantResultFields()
    {
        var committed = CommitFixture();
        var skipped = committed.SkipToResult();
        var watched = committed.WatchResolution.Result;

        Assert.Equal(skipped.BattleId, watched.BattleId);
        Assert.Equal(skipped.SimulationVersion, watched.SimulationVersion);
        Assert.Equal(skipped.Seed, watched.Seed);
        Assert.Equal(skipped.ResultType, watched.ResultType);
        Assert.Equal(skipped.WinnerSideId, watched.WinnerSideId);
        Assert.Equal(skipped.TerminalTick, watched.TerminalTick);
        Assert.Equal(
            skipped.OrderedSurvivors,
            watched.OrderedSurvivors);
        Assert.Equal(
            skipped.OrderedCasualties,
            watched.OrderedCasualties);
        Assert.Equal(
            skipped.OrderedRoutedUnits,
            watched.OrderedRoutedUnits);
        Assert.Equal(
            skipped.OrderedRetreatedUnits,
            watched.OrderedRetreatedUnits);
        Assert.Equal(skipped.CommanderStatuses, watched.CommanderStatuses);
        Assert.Equal(skipped.TranscriptDigest, watched.TranscriptDigest);
        Assert.Equal(skipped.CanonicalDigest, watched.CanonicalDigest);
        Assert.True(skipped.IsExactlyEqualTo(watched));
    }

    [Fact]
    public void PlaybackAtOneXAcceleratedAndPauseResumeKeepsStoredResolutionUnchanged()
    {
        var committed = CommitFixture();
        var playback = new BattleTranscriptPlayback(committed.WatchResolution);
        var result = committed.Resolution.Result;
        var resultDigest = result.CanonicalDigest;
        var transcript = committed.Resolution.Transcript;
        var transcriptDigest = transcript.CanonicalDigest;

        Assert.Equal(48_000.0, playback.NominalDurationMilliseconds, precision: 10);

        playback.ResetAndConsume();
        playback.Clock.SetSpeed(1.0);
        playback.Clock.Play();
        playback.Advance(0.25);
        playback.Clock.Pause();
        var pausedTick = playback.Clock.CurrentTick;
        playback.Advance(10.0);
        Assert.Equal(pausedTick, playback.Clock.CurrentTick);
        playback.Clock.Play();
        playback.Advance(100.0);
        Assert.True(playback.IsComplete);

        Assert.Same(result, playback.Result);
        Assert.Same(transcript, playback.Transcript);
        Assert.Equal(resultDigest, playback.Result.CanonicalDigest);
        Assert.Equal(transcriptDigest, playback.Transcript.CanonicalDigest);

        playback.ResetAndConsume();
        playback.Clock.SetSpeed(8.0);
        playback.Clock.Play();
        playback.Advance(0.1);
        playback.Clock.Pause();
        var acceleratedPausedTick = playback.Clock.CurrentTick;
        playback.Advance(10.0);
        Assert.Equal(acceleratedPausedTick, playback.Clock.CurrentTick);
        playback.Clock.Play();
        playback.Advance(100.0);

        Assert.True(playback.IsComplete);
        Assert.Same(result, playback.Result);
        Assert.Same(transcript, playback.Transcript);
        Assert.Equal(resultDigest, playback.Result.CanonicalDigest);
        Assert.Equal(transcriptDigest, playback.Transcript.CanonicalDigest);
        Assert.Equal(result.OrderedSurvivors.Select(record => record.UnitId), playback.Result.OrderedSurvivors.Select(record => record.UnitId));
        Assert.Equal(result.OrderedCasualties.Select(record => record.UnitId), playback.Result.OrderedCasualties.Select(record => record.UnitId));
    }

    private static CommittedBattleResolution CommitFixture()
    {
        return CommittedBattleResolution.Commit(BattleFixtureFactory.CreateM1MeleeFixture());
    }
}
