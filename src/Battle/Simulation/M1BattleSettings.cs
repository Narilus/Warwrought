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
    /// Starting fixed logical frequency for later M1 resolution work; no loop is implemented by M1.1.
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
    public const int FixtureMoraleLossPerCasualty = 4;
    public const int FixtureMeleeRangeUnits = 1_500;
    public const int FixtureAttackCooldownTicks = 12;
}
