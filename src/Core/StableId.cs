using System;

namespace Warwrought.Core;

/// <summary>
/// A deliberately small, ordinal-comparable identifier for source-controlled domain IDs.
/// </summary>
public readonly struct StableId : IEquatable<StableId>, IComparable<StableId>
{
    private readonly string? _value;

    public StableId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A stable ID must contain a non-whitespace value.", nameof(value));
        }

        _value = value;
    }

    public string Value => _value ?? string.Empty;

    public int CompareTo(StableId other) => StringComparer.Ordinal.Compare(Value, other.Value);

    public bool Equals(StableId other) => StringComparer.Ordinal.Equals(_value, other._value);

    public override bool Equals(object? obj) => obj is StableId other && Equals(other);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

    public override string ToString() => Value;

    public static bool operator ==(StableId left, StableId right) => left.Equals(right);

    public static bool operator !=(StableId left, StableId right) => !left.Equals(right);

    public static bool operator <(StableId left, StableId right) => left.CompareTo(right) < 0;

    public static bool operator >(StableId left, StableId right) => left.CompareTo(right) > 0;

    public static bool operator <=(StableId left, StableId right) => left.CompareTo(right) <= 0;

    public static bool operator >=(StableId left, StableId right) => left.CompareTo(right) >= 0;
}
