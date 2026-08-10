using System;

namespace Warwrought.Core;

/// <summary>
/// Non-negative logical time for authoritative simulation work.
/// M1.1 defines the value type only; no simulation loop is implemented here.
/// </summary>
public readonly record struct SimulationTick : IComparable<SimulationTick>
{
    public SimulationTick(long value)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "A simulation tick cannot be negative.");
        }

        Value = value;
    }

    public long Value { get; }

    public static SimulationTick Zero => new(0);

    public SimulationTick Advance(long ticks)
    {
        if (ticks < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ticks), ticks, "Simulation ticks can only advance by a non-negative amount.");
        }

        try
        {
            return new SimulationTick(checked(Value + ticks));
        }
        catch (OverflowException exception)
        {
            throw new ArgumentOutOfRangeException(nameof(ticks), ticks, $"The simulation tick value would overflow: {exception.Message}");
        }
    }

    public SimulationTick Next() => Advance(1);

    public int CompareTo(SimulationTick other) => Value.CompareTo(other.Value);

    public static bool operator <(SimulationTick left, SimulationTick right) => left.Value < right.Value;

    public static bool operator <=(SimulationTick left, SimulationTick right) => left.Value <= right.Value;

    public static bool operator >(SimulationTick left, SimulationTick right) => left.Value > right.Value;

    public static bool operator >=(SimulationTick left, SimulationTick right) => left.Value >= right.Value;

    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
