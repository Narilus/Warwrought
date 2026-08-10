using System;
using Warwrought.Core;

namespace Warwrought.Battle.Model;

/// <summary>
/// Stable identity for one committed battle.
/// </summary>
public readonly struct BattleId : IEquatable<BattleId>, IComparable<BattleId>
{
    private readonly StableId _value;

    public BattleId(string value) => _value = new StableId(value);

    public string Value => _value.Value;

    public bool IsValid => !string.IsNullOrWhiteSpace(Value);

    public int CompareTo(BattleId other) => _value.CompareTo(other._value);

    public bool Equals(BattleId other) => _value.Equals(other._value);

    public override bool Equals(object? obj) => obj is BattleId other && Equals(other);

    public override int GetHashCode() => _value.GetHashCode();

    public override string ToString() => Value;

    public static bool operator ==(BattleId left, BattleId right) => left.Equals(right);

    public static bool operator !=(BattleId left, BattleId right) => !left.Equals(right);

    public static bool operator <(BattleId left, BattleId right) => left.CompareTo(right) < 0;

    public static bool operator >(BattleId left, BattleId right) => left.CompareTo(right) > 0;

    public static bool operator <=(BattleId left, BattleId right) => left.CompareTo(right) <= 0;

    public static bool operator >=(BattleId left, BattleId right) => left.CompareTo(right) >= 0;
}

/// <summary>
/// Stable identity for one side in a committed battle.
/// </summary>
public readonly struct BattleSideId : IEquatable<BattleSideId>, IComparable<BattleSideId>
{
    private readonly StableId _value;

    public BattleSideId(string value) => _value = new StableId(value);

    public string Value => _value.Value;

    public bool IsValid => !string.IsNullOrWhiteSpace(Value);

    public int CompareTo(BattleSideId other) => _value.CompareTo(other._value);

    public bool Equals(BattleSideId other) => _value.Equals(other._value);

    public override bool Equals(object? obj) => obj is BattleSideId other && Equals(other);

    public override int GetHashCode() => _value.GetHashCode();

    public override string ToString() => Value;

    public static bool operator ==(BattleSideId left, BattleSideId right) => left.Equals(right);

    public static bool operator !=(BattleSideId left, BattleSideId right) => !left.Equals(right);

    public static bool operator <(BattleSideId left, BattleSideId right) => left.CompareTo(right) < 0;

    public static bool operator >(BattleSideId left, BattleSideId right) => left.CompareTo(right) > 0;

    public static bool operator <=(BattleSideId left, BattleSideId right) => left.CompareTo(right) <= 0;

    public static bool operator >=(BattleSideId left, BattleSideId right) => left.CompareTo(right) >= 0;
}

/// <summary>
/// Stable identity for one squad/formation in a committed battle.
/// </summary>
public readonly struct BattleSquadId : IEquatable<BattleSquadId>, IComparable<BattleSquadId>
{
    private readonly StableId _value;

    public BattleSquadId(string value) => _value = new StableId(value);

    public string Value => _value.Value;

    public bool IsValid => !string.IsNullOrWhiteSpace(Value);

    public int CompareTo(BattleSquadId other) => _value.CompareTo(other._value);

    public bool Equals(BattleSquadId other) => _value.Equals(other._value);

    public override bool Equals(object? obj) => obj is BattleSquadId other && Equals(other);

    public override int GetHashCode() => _value.GetHashCode();

    public override string ToString() => Value;

    public static bool operator ==(BattleSquadId left, BattleSquadId right) => left.Equals(right);

    public static bool operator !=(BattleSquadId left, BattleSquadId right) => !left.Equals(right);

    public static bool operator <(BattleSquadId left, BattleSquadId right) => left.CompareTo(right) < 0;

    public static bool operator >(BattleSquadId left, BattleSquadId right) => left.CompareTo(right) > 0;

    public static bool operator <=(BattleSquadId left, BattleSquadId right) => left.CompareTo(right) <= 0;

    public static bool operator >=(BattleSquadId left, BattleSquadId right) => left.CompareTo(right) >= 0;
}

/// <summary>
/// Stable identity for one member unit in a committed battle.
/// </summary>
public readonly struct BattleUnitId : IEquatable<BattleUnitId>, IComparable<BattleUnitId>
{
    private readonly StableId _value;

    public BattleUnitId(string value) => _value = new StableId(value);

    public string Value => _value.Value;

    public bool IsValid => !string.IsNullOrWhiteSpace(Value);

    public int CompareTo(BattleUnitId other) => _value.CompareTo(other._value);

    public bool Equals(BattleUnitId other) => _value.Equals(other._value);

    public override bool Equals(object? obj) => obj is BattleUnitId other && Equals(other);

    public override int GetHashCode() => _value.GetHashCode();

    public override string ToString() => Value;

    public static bool operator ==(BattleUnitId left, BattleUnitId right) => left.Equals(right);

    public static bool operator !=(BattleUnitId left, BattleUnitId right) => !left.Equals(right);

    public static bool operator <(BattleUnitId left, BattleUnitId right) => left.CompareTo(right) < 0;

    public static bool operator >(BattleUnitId left, BattleUnitId right) => left.CompareTo(right) > 0;

    public static bool operator <=(BattleUnitId left, BattleUnitId right) => left.CompareTo(right) <= 0;

    public static bool operator >=(BattleUnitId left, BattleUnitId right) => left.CompareTo(right) >= 0;
}
