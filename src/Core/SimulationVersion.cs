using System;

namespace Warwrought.Core;

/// <summary>
/// Version of the authoritative simulation rules used by a committed battle.
/// </summary>
public readonly record struct SimulationVersion : IComparable<SimulationVersion>
{
    public SimulationVersion(int major, int minor)
    {
        if (major < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(major), major, "Simulation version major cannot be negative.");
        }

        if (minor < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minor), minor, "Simulation version minor cannot be negative.");
        }

        Major = major;
        Minor = minor;
    }

    public int Major { get; }

    public int Minor { get; }

    /// <summary>
    /// The only authoritative simulation version supported by the M1.1 foundation.
    /// Later rule changes require an explicit version decision.
    /// </summary>
    public static SimulationVersion M1 { get; } = new(1, 0);

    public static SimulationVersion Current => M1;

    public bool IsSupported => this == M1;

    public int CompareTo(SimulationVersion other)
    {
        var majorComparison = Major.CompareTo(other.Major);
        return majorComparison != 0 ? majorComparison : Minor.CompareTo(other.Minor);
    }

    public static bool operator <(SimulationVersion left, SimulationVersion right) => left.CompareTo(right) < 0;

    public static bool operator <=(SimulationVersion left, SimulationVersion right) => left.CompareTo(right) <= 0;

    public static bool operator >(SimulationVersion left, SimulationVersion right) => left.CompareTo(right) > 0;

    public static bool operator >=(SimulationVersion left, SimulationVersion right) => left.CompareTo(right) >= 0;

    public override string ToString() => $"{Major}.{Minor}";
}
