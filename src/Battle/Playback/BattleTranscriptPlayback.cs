using System;
using System.Collections.Generic;
using Warwrought.Battle.Model;
using Warwrought.Battle.Transcript;
using Warwrought.Core;

namespace Warwrought.Battle.Playback;

/// <summary>
/// A presentation cursor over one immutable authoritative resolution. It dispatches existing
/// transcript records in order and samples existing keyframes for visual interpolation. It has
/// no combat rules, RNG, target selection, damage, morale, or winner logic.
/// </summary>
public sealed class BattleTranscriptPlayback
{
    private readonly BattleResolution _resolution;
    private readonly IReadOnlyList<BattleSemanticEvent> _events;
    private readonly IReadOnlyList<BattleFormationKeyframe> _keyframes;
    private readonly Dictionary<string, List<MemberKeyframeSample>> _memberSamples = new(StringComparer.Ordinal);
    private int _nextEventIndex;
    private int _nextKeyframeIndex;

    public BattleTranscriptPlayback(BattleResolution resolution)
    {
        ArgumentNullException.ThrowIfNull(resolution);
        _resolution = resolution;
        _events = resolution.Transcript.Events;
        _keyframes = resolution.Transcript.Keyframes;
        Clock = new BattlePlaybackClock(
            resolution.Transcript.Header.TicksPerSecond,
            resolution.Result.TerminalTick.Value);

        for (var keyframeIndex = 0; keyframeIndex < _keyframes.Count; keyframeIndex++)
        {
            var keyframe = _keyframes[keyframeIndex];
            for (var memberIndex = 0; memberIndex < keyframe.Members.Count; memberIndex++)
            {
                var member = keyframe.Members[memberIndex];
                var id = member.UnitId.Value;
                if (!_memberSamples.TryGetValue(id, out var samples))
                {
                    samples = new List<MemberKeyframeSample>();
                    _memberSamples.Add(id, samples);
                }

                samples.Add(new MemberKeyframeSample(keyframe.Tick.Value, member.Position, member.State));
            }
        }
    }

    public BattleResolution Resolution => _resolution;

    public BattleTranscript Transcript => _resolution.Transcript;

    public BattleResult Result => _resolution.Result;

    public BattlePlaybackClock Clock { get; }

    public int EventsConsumed => _nextEventIndex;

    public int KeyframesConsumed => _nextKeyframeIndex;

    public bool IsComplete => Clock.IsAtEnd && _nextEventIndex >= _events.Count && _nextKeyframeIndex >= _keyframes.Count;

    public PlaybackStep ResetAndConsume()
    {
        Clock.Reset();
        _nextEventIndex = 0;
        _nextKeyframeIndex = 0;
        return ConsumeDueRecords();
    }

    public PlaybackStep Advance(double realDeltaSeconds)
    {
        Clock.Advance(realDeltaSeconds);
        return ConsumeDueRecords();
    }

    /// <summary>
    /// Returns a presentation-only interpolation sample for one member. The discrete state is
    /// taken from the most recent authoritative keyframe; positions between keyframes are the
    /// only values interpolated here.
    /// </summary>
    public PlaybackMemberSample SampleMemberPosition(BattleUnitId unitId, double exactTick)
    {
        if (!_memberSamples.TryGetValue(unitId.Value, out var samples) || samples.Count == 0)
        {
            throw new KeyNotFoundException($"Transcript does not contain member '{unitId.Value}'.");
        }

        var clampedTick = Math.Clamp(exactTick, 0.0, Result.TerminalTick.Value);
        if (clampedTick <= samples[0].Tick)
        {
            return PlaybackMemberSample.From(unitId, samples[0], samples[0], 0.0);
        }

        for (var index = 1; index < samples.Count; index++)
        {
            var next = samples[index];
            if (clampedTick > next.Tick)
            {
                continue;
            }

            var previous = samples[index - 1];
            var tickSpan = next.Tick - previous.Tick;
            var alpha = tickSpan <= 0 ? 1.0 : (clampedTick - previous.Tick) / tickSpan;
            return PlaybackMemberSample.From(unitId, previous, next, Math.Clamp(alpha, 0.0, 1.0));
        }

        var last = samples[^1];
        return PlaybackMemberSample.From(unitId, last, last, 0.0);
    }

    private PlaybackStep ConsumeDueRecords()
    {
        var events = new List<BattleSemanticEvent>();
        while (_nextEventIndex < _events.Count && _events[_nextEventIndex].Tick.Value <= Clock.CurrentTick)
        {
            events.Add(_events[_nextEventIndex]);
            _nextEventIndex++;
        }

        var keyframes = new List<BattleFormationKeyframe>();
        while (_nextKeyframeIndex < _keyframes.Count && _keyframes[_nextKeyframeIndex].Tick.Value <= Clock.CurrentTick)
        {
            keyframes.Add(_keyframes[_nextKeyframeIndex]);
            _nextKeyframeIndex++;
        }

        return new PlaybackStep(
            Clock.CurrentTick,
            Clock.CurrentTickExact,
            keyframes.AsReadOnly(),
            events.AsReadOnly(),
            IsComplete);
    }

    internal readonly record struct MemberKeyframeSample(long Tick, SimPosition Position, BattleUnitState State);
}

public sealed record PlaybackStep
{
    public PlaybackStep(
        long currentTick,
        double currentTickExact,
        IReadOnlyList<BattleFormationKeyframe> keyframesConsumed,
        IReadOnlyList<BattleSemanticEvent> eventsConsumed,
        bool isComplete)
    {
        CurrentTick = currentTick;
        CurrentTickExact = currentTickExact;
        KeyframesConsumed = keyframesConsumed;
        EventsConsumed = eventsConsumed;
        IsComplete = isComplete;
    }

    public long CurrentTick { get; }

    public double CurrentTickExact { get; }

    public IReadOnlyList<BattleFormationKeyframe> KeyframesConsumed { get; }

    public IReadOnlyList<BattleSemanticEvent> EventsConsumed { get; }

    public bool IsComplete { get; }
}

public readonly record struct PlaybackMemberSample(
    BattleUnitId UnitId,
    double X,
    double Z,
    BattleUnitState State,
    long PreviousTick,
    long NextTick,
    double Alpha)
{
    internal static PlaybackMemberSample From(
        BattleUnitId unitId,
        BattleTranscriptPlayback.MemberKeyframeSample previous,
        BattleTranscriptPlayback.MemberKeyframeSample next,
        double alpha)
        {
        return new PlaybackMemberSample(
            unitId,
            previous.Position.X + ((next.Position.X - previous.Position.X) * alpha),
            previous.Position.Z + ((next.Position.Z - previous.Position.Z) * alpha),
            alpha >= 1.0 ? next.State : previous.State,
            previous.Tick,
            next.Tick,
            alpha);
    }
}
