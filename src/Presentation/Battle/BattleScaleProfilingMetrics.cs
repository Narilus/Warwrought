using System;
using System.Collections.Generic;

namespace Warwrought.Presentation.Battle;

/// <summary>
/// Runtime-owned aggregation for a bounded sequence of production Godot frame durations.
/// This class does not run a benchmark or create a second presentation path.
/// </summary>
public static class BattleScaleProfilingMetrics
{
    public static BattleScaleProfilingSummary Summarize(
        IReadOnlyList<double> frameDurationsMilliseconds,
        double warmupDurationMilliseconds,
        double sampleWindowDurationMilliseconds)
    {
        ArgumentNullException.ThrowIfNull(frameDurationsMilliseconds);
        if (!double.IsFinite(warmupDurationMilliseconds) || warmupDurationMilliseconds < 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(warmupDurationMilliseconds));
        }

        if (!double.IsFinite(sampleWindowDurationMilliseconds) || sampleWindowDurationMilliseconds <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(sampleWindowDurationMilliseconds));
        }

        var warmupExcludedSampleCount = 0;
        var warmupExcludedDurationMilliseconds = 0.0;
        var measuredSampleWindowDurationMilliseconds = 0.0;
        var samples = new List<double>();
        var elapsedMilliseconds = 0.0;

        foreach (var frameDurationMilliseconds in frameDurationsMilliseconds)
        {
            if (!double.IsFinite(frameDurationMilliseconds) || frameDurationMilliseconds <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(frameDurationsMilliseconds), "Frame durations must be finite and positive.");
            }

            if (elapsedMilliseconds < warmupDurationMilliseconds)
            {
                warmupExcludedSampleCount++;
                warmupExcludedDurationMilliseconds += frameDurationMilliseconds;
                elapsedMilliseconds += frameDurationMilliseconds;
                continue;
            }

            if (measuredSampleWindowDurationMilliseconds >= sampleWindowDurationMilliseconds)
            {
                break;
            }

            if (measuredSampleWindowDurationMilliseconds + frameDurationMilliseconds > sampleWindowDurationMilliseconds)
            {
                break;
            }

            samples.Add(frameDurationMilliseconds);
            measuredSampleWindowDurationMilliseconds += frameDurationMilliseconds;
            elapsedMilliseconds += frameDurationMilliseconds;
        }

        if (samples.Count == 0)
        {
            throw new InvalidOperationException("The bounded profiling window did not contain a complete frame sample.");
        }

        return new BattleScaleProfilingSummary(
            samples,
            measuredSampleWindowDurationMilliseconds,
            warmupExcludedSampleCount,
            warmupExcludedDurationMilliseconds);
    }

    public static double Median(IReadOnlyList<double> samples)
    {
        var ordered = CopyAndValidate(samples);
        var middle = ordered.Length / 2;
        return ordered.Length % 2 == 0
            ? (ordered[middle - 1] + ordered[middle]) / 2.0
            : ordered[middle];
    }

    public static double Percentile95(IReadOnlyList<double> samples)
    {
        var ordered = CopyAndValidate(samples);
        if (ordered.Length == 1)
        {
            return ordered[0];
        }

        var position = (ordered.Length - 1) * 0.95;
        var lowerIndex = (int)Math.Floor(position);
        var upperIndex = (int)Math.Ceiling(position);
        if (lowerIndex == upperIndex)
        {
            return ordered[lowerIndex];
        }

        var fraction = position - lowerIndex;
        return ordered[lowerIndex] + (ordered[upperIndex] - ordered[lowerIndex]) * fraction;
    }

    private static double[] CopyAndValidate(IReadOnlyList<double> samples)
    {
        ArgumentNullException.ThrowIfNull(samples);
        if (samples.Count == 0)
        {
            throw new ArgumentException("At least one frame sample is required.", nameof(samples));
        }

        var ordered = new double[samples.Count];
        for (var index = 0; index < samples.Count; index++)
        {
            var sample = samples[index];
            if (!double.IsFinite(sample) || sample <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(samples), "Frame samples must be finite and positive.");
            }

            ordered[index] = sample;
        }

        Array.Sort(ordered);
        return ordered;
    }
}

public sealed class BattleScaleProfilingSummary
{
    internal BattleScaleProfilingSummary(
        IReadOnlyList<double> samples,
        double measuredSampleWindowDurationMilliseconds,
        int warmupExcludedSampleCount,
        double warmupExcludedDurationMilliseconds)
    {
        Samples = samples;
        MeasuredSampleWindowDurationMilliseconds = measuredSampleWindowDurationMilliseconds;
        WarmupExcludedSampleCount = warmupExcludedSampleCount;
        WarmupExcludedDurationMilliseconds = warmupExcludedDurationMilliseconds;
    }

    public IReadOnlyList<double> Samples { get; }

    public double MeasuredSampleWindowDurationMilliseconds { get; }

    public int WarmupExcludedSampleCount { get; }

    public double WarmupExcludedDurationMilliseconds { get; }

    public double MedianMilliseconds => BattleScaleProfilingMetrics.Median(Samples);

    public double P95Milliseconds => BattleScaleProfilingMetrics.Percentile95(Samples);
}
