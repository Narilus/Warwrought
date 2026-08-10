using System;

namespace Warwrought.Core;

/// <summary>
/// Explicit deterministic SplitMix64 stream used by authoritative code.
/// The algorithm and state transition are versioned by the owning simulation version;
/// this type has no global state and never consults engine or system randomness.
/// </summary>
public sealed class DeterministicRng
{
    private const ulong StateIncrement = 0x9E3779B97F4A7C15UL;
    private const ulong MixMultiplierA = 0xBF58476D1CE4E5B9UL;
    private const ulong MixMultiplierB = 0x94D049BB133111EBUL;

    private readonly ulong _seed;
    private ulong _state;

    public DeterministicRng(ulong seed)
    {
        _seed = seed;
        _state = seed;
    }

    public ulong Seed => _seed;

    public ulong State => _state;

    public void Reset() => _state = _seed;

    public ulong NextUInt64()
    {
        _state = unchecked(_state + StateIncrement);
        var mixed = _state;
        mixed = unchecked((mixed ^ (mixed >> 30)) * MixMultiplierA);
        mixed = unchecked((mixed ^ (mixed >> 27)) * MixMultiplierB);
        return mixed ^ (mixed >> 31);
    }

    public uint NextUInt32() => unchecked((uint)NextUInt64());

    public int NextInt(int maxExclusive)
    {
        if (maxExclusive <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxExclusive), maxExclusive, "The exclusive upper bound must be positive.");
        }

        return NextInt(0, maxExclusive);
    }

    public int NextInt(int minInclusive, int maxExclusive)
    {
        if (minInclusive >= maxExclusive)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxExclusive),
                maxExclusive,
                "The maximum must be greater than the minimum.");
        }

        var range = (ulong)((long)maxExclusive - minInclusive);
        var rejectionThreshold = unchecked(0UL - range) % range;

        while (true)
        {
            var sample = NextUInt64();
            if (sample < rejectionThreshold)
            {
                continue;
            }

            var result = (long)minInclusive + (long)(sample % range);
            return checked((int)result);
        }
    }

    public bool CheckProbability(int numerator, int denominator)
    {
        if (denominator <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(denominator), denominator, "The probability denominator must be positive.");
        }

        if (numerator < 0 || numerator > denominator)
        {
            throw new ArgumentOutOfRangeException(
                nameof(numerator),
                numerator,
                "The probability numerator must be between zero and the denominator.");
        }

        return NextInt(denominator) < numerator;
    }
}
