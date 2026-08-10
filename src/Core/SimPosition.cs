using System;

namespace Warwrought.Core;

/// <summary>
/// Authoritative horizontal X/Z position represented in scaled integer units.
/// The M1 convention is 1 simulation metre = 1,000 position units.
/// </summary>
public readonly record struct SimPosition
{
    public const int UnitsPerMetre = 1_000;

    // This narrow bound keeps checked distance arithmetic safe without introducing a general fixed-point framework.
    public const int MaxCoordinateUnits = 1_000_000_000;

    public SimPosition(int x, int z)
    {
        ValidateCoordinate(x, nameof(x));
        ValidateCoordinate(z, nameof(z));
        X = x;
        Z = z;
    }

    public int X { get; }

    public int Z { get; }

    public bool IsValid => IsCoordinateInRange(X) && IsCoordinateInRange(Z);

    public static bool TryCreate(int x, int z, out SimPosition position)
    {
        if (!IsCoordinateInRange(x) || !IsCoordinateInRange(z))
        {
            position = default;
            return false;
        }

        position = new SimPosition(x, z);
        return true;
    }

    public SimPosition Translate(int xUnits, int zUnits)
    {
        try
        {
            return new SimPosition(checked(X + xUnits), checked(Z + zUnits));
        }
        catch (OverflowException exception)
        {
            throw new ArgumentOutOfRangeException(nameof(xUnits), $"The translated simulation position would overflow: {exception.Message}");
        }
    }

    public long DistanceSquaredTo(SimPosition other)
    {
        var differenceX = (long)other.X - X;
        var differenceZ = (long)other.Z - Z;
        return checked((differenceX * differenceX) + (differenceZ * differenceZ));
    }

    public long ManhattanDistanceTo(SimPosition other)
    {
        var differenceX = Math.Abs((long)other.X - X);
        var differenceZ = Math.Abs((long)other.Z - Z);
        return checked(differenceX + differenceZ);
    }

    private static bool IsCoordinateInRange(int value) => value >= -MaxCoordinateUnits && value <= MaxCoordinateUnits;

    private static void ValidateCoordinate(int value, string parameterName)
    {
        if (!IsCoordinateInRange(value))
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                $"Simulation coordinates must be between {-MaxCoordinateUnits} and {MaxCoordinateUnits} scaled units.");
        }
    }
}
