using System;
using System.Collections.Generic;
using System.Linq;
using Warwrought.Core;

namespace Warwrought.Battle.Model;

/// <summary>
/// Fixture-only unit values for M1.1. This is deliberately not the final content/unit schema.
/// </summary>
public sealed record FixtureUnitDefinition
{
    public FixtureUnitDefinition(
        StableId definitionId,
        int maximumHealth,
        int meleeAttack,
        int meleeDefense,
        int startingMorale,
        int moraleLossPerCasualty,
        int meleeRangeUnits,
        int attackCooldownTicks)
    {
        DefinitionId = definitionId;
        MaximumHealth = maximumHealth;
        MeleeAttack = meleeAttack;
        MeleeDefense = meleeDefense;
        StartingMorale = startingMorale;
        MoraleLossPerCasualty = moraleLossPerCasualty;
        MeleeRangeUnits = meleeRangeUnits;
        AttackCooldownTicks = attackCooldownTicks;
    }

    public StableId DefinitionId { get; }

    public int MaximumHealth { get; }

    public int MeleeAttack { get; }

    public int MeleeDefense { get; }

    public int StartingMorale { get; }

    public int MoraleLossPerCasualty { get; }

    /// <summary>
    /// Range in the same scaled integer units as <see cref="SimPosition"/>.
    /// </summary>
    public int MeleeRangeUnits { get; }

    public int AttackCooldownTicks { get; }
}

/// <summary>
/// The only M1 order supported by the committed fixture.
/// </summary>
public sealed record AdvanceOrderInput
{
    public AdvanceOrderInput(SimPosition destination, int stopRangeUnits)
    {
        Destination = destination;
        StopRangeUnits = stopRangeUnits;
    }

    public SimPosition Destination { get; }

    /// <summary>
    /// Positive scaled integer distance at which the later formation advance may stop.
    /// </summary>
    public int StopRangeUnits { get; }
}

/// <summary>
/// Candidate rectangular formation/deployment data. Validation occurs at battle commitment.
/// </summary>
public sealed record RectangularFormationInput
{
    public RectangularFormationInput(
        SimPosition anchor,
        FormationFacing facing,
        int fileCount,
        int rankCount,
        int fileSpacingUnits,
        int rankSpacingUnits,
        IEnumerable<BattleUnitId> memberIds)
    {
        ArgumentNullException.ThrowIfNull(memberIds);
        Anchor = anchor;
        Facing = facing;
        FileCount = fileCount;
        RankCount = rankCount;
        FileSpacingUnits = fileSpacingUnits;
        RankSpacingUnits = rankSpacingUnits;
        MemberIds = CopyReadOnly(memberIds, nameof(memberIds));
    }

    public SimPosition Anchor { get; }

    public FormationFacing Facing { get; }

    public int FileCount { get; }

    public int RankCount { get; }

    public int FileSpacingUnits { get; }

    public int RankSpacingUnits { get; }

    public IReadOnlyList<BattleUnitId> MemberIds { get; }

    internal static IReadOnlyList<T> CopyReadOnly<T>(IEnumerable<T> values, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(values, parameterName);
        return Array.AsReadOnly(values.ToArray());
    }
}

/// <summary>
/// Candidate unit membership in a committed squad.
/// </summary>
public sealed record BattleUnitInput
{
    public BattleUnitInput(
        BattleUnitId id,
        BattleSideId sideId,
        BattleSquadId squadId,
        StableId fixtureUnitDefinitionId)
    {
        Id = id;
        SideId = sideId;
        SquadId = squadId;
        FixtureUnitDefinitionId = fixtureUnitDefinitionId;
    }

    public BattleUnitId Id { get; }

    public BattleSideId SideId { get; }

    public BattleSquadId SquadId { get; }

    public StableId FixtureUnitDefinitionId { get; }
}

/// <summary>
/// Candidate squad with one rectangular formation and one advance order.
/// </summary>
public sealed record BattleSquadInput
{
    public BattleSquadInput(
        BattleSquadId id,
        BattleSideId sideId,
        IEnumerable<BattleUnitInput> members,
        RectangularFormationInput? formation,
        AdvanceOrderInput? advanceOrder)
    {
        ArgumentNullException.ThrowIfNull(members);
        Id = id;
        SideId = sideId;
        Members = RectangularFormationInput.CopyReadOnly(members, nameof(members));
        Formation = formation;
        AdvanceOrder = advanceOrder;
    }

    public BattleSquadId Id { get; }

    public BattleSideId SideId { get; }

    public IReadOnlyList<BattleUnitInput> Members { get; }

    public RectangularFormationInput? Formation { get; }

    public AdvanceOrderInput? AdvanceOrder { get; }
}

/// <summary>
/// Candidate side in a narrow M1 battle commitment.
/// </summary>
public sealed record BattleSideInput
{
    public BattleSideInput(BattleSideId id, IEnumerable<BattleSquadInput> squads)
    {
        ArgumentNullException.ThrowIfNull(squads);
        Id = id;
        Squads = RectangularFormationInput.CopyReadOnly(squads, nameof(squads));
    }

    public BattleSideId Id { get; }

    public IReadOnlyList<BattleSquadInput> Squads { get; }
}

/// <summary>
/// Public construction snapshot for the narrow M1 battle fixture.
/// Every collection is copied at this boundary; a committed definition never retains caller-owned lists.
/// </summary>
public sealed record BattleDefinitionInput
{
    public BattleDefinitionInput(
        SimulationVersion simulationVersion,
        BattleId battleId,
        ulong seed,
        IEnumerable<FixtureUnitDefinition> fixtureUnitDefinitions,
        IEnumerable<BattleSideInput> sides)
    {
        ArgumentNullException.ThrowIfNull(fixtureUnitDefinitions);
        ArgumentNullException.ThrowIfNull(sides);
        SimulationVersion = simulationVersion;
        BattleId = battleId;
        Seed = seed;
        FixtureUnitDefinitions = RectangularFormationInput.CopyReadOnly(fixtureUnitDefinitions, nameof(fixtureUnitDefinitions));
        Sides = RectangularFormationInput.CopyReadOnly(sides, nameof(sides));
    }

    public SimulationVersion SimulationVersion { get; }

    public BattleId BattleId { get; }

    public ulong Seed { get; }

    public IReadOnlyList<FixtureUnitDefinition> FixtureUnitDefinitions { get; }

    public IReadOnlyList<BattleSideInput> Sides { get; }
}
