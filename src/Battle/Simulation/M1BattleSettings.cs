using Warwrought.Core;

namespace Warwrought.Battle.Simulation;

/// <summary>
/// Narrow, provisional settings for the M1 fixture. These are not final balance or content constants.
/// </summary>
public static class M1BattleSettings
{
    /// <summary>
    /// All authoritative M1 horizontal positions and ranges use 1,000 integer units per simulation metre.
    /// </summary>
    public const int PositionUnitsPerMetre = SimPosition.UnitsPerMetre;

    /// <summary>
    /// Starting fixed logical frequency for the M1 resolver. This remains a provisional
    /// implementation setting rather than final battle-design law.
    /// </summary>
    public const int TicksPerSecond = 12;

    public static SimulationVersion SimulationVersion => SimulationVersion.M1;

    public const int FixtureUnitsPerSide = 64;
    public const int FixtureFormationFileCount = 8;
    public const int FixtureFormationRankCount = 8;
    public const int FixtureFileSpacingUnits = 1_000;
    public const int FixtureRankSpacingUnits = 1_000;
    public const int FixtureDeploymentHalfDistanceUnits = 20_000;
    public const int FixtureAdvanceStopRangeUnits = 1_500;

    // Fixture-only values intentionally bounded for later melee/morale tasks, not reusable final content definitions.
    public const int FixtureMaximumHealth = 100;
    public const int FixtureMeleeAttack = 12;
    public const int FixtureMeleeDefense = 8;
    public const int FixtureStartingMorale = 100;

    /// <summary>
    /// Provisional M1 rule: each casualty subtracts this fixed amount from its target
    /// formation's morale, clamped at zero. It is fixture proof data, not final morale design.
    /// </summary>
    public const int FixtureMoraleLossPerCasualty = 4;
    public const int FixtureMeleeRangeUnits = 1_500;
    public const int FixtureAttackCooldownTicks = 12;

    /// <summary>
    /// Fixed integer movement step used by the narrow fixture. It is intentionally not a
    /// final unit movement stat.
    /// </summary>
    public const int FixtureAdvanceStepUnits = 500;

    /// <summary>
    /// Casualty morale threshold for the provisional fixture rule.
    /// </summary>
    public const int FixtureRoutMoraleThreshold = 40;

    /// <summary>
    /// Integer retreat distance away from the deployment centre after routing.
    /// </summary>
    public const int FixtureRetreatDistanceUnits = 12_000;

    public const int FixtureRetreatStepUnits = 500;

    /// <summary>
    /// Seeded damage variation is deliberately tiny: base attack minus defence plus a
    /// deterministic roll in [0, FixtureDamageRollMaxExclusive). It makes the explicit
    /// fixture seed authoritative without pretending to be final combat balance.
    /// </summary>
    public const int FixtureDamageRollMaxExclusive = 3;

    /// <summary>
    /// Maximum number of logical ticks processed by one resolution, including tick zero.
    /// A battle still unresolved after this bound returns NonTerminalFailure at the last
    /// processed tick; it never waits indefinitely or fabricates a winner.
    /// </summary>
    public const int MaximumSimulationTicks = 4_096;

    public const int TranscriptKeyframeIntervalTicks = TicksPerSecond;
}
