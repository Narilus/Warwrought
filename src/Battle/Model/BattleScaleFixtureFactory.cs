using Warwrought.Battle.Simulation;
using Warwrought.Core;

namespace Warwrought.Battle.Model;

/// <summary>
/// Builds the deliberately narrow M2.3 Phase 1 100v100 scale fixture. It reuses the M1
/// fixture statistics and authoritative model, but keeps its larger shape and identity
/// separate from the canonical M1 64v64 proof fixture.
/// </summary>
public static class BattleScaleFixtureFactory
{
    public const ulong DefaultSeed = 82_742;
    public const int UnitsPerSide = 100;
    public const int FormationFileCount = 10;
    public const int FormationRankCount = 10;
    public const int FileSpacingUnits = 1_000;
    public const int RankSpacingUnits = 1_000;
    public const int DeploymentHalfDistanceUnits = 20_000;
    public const int AdvanceStopRangeUnits = 1_500;
    public const string BattleIdValue = "battle.m2.scale.100v100.fixture";
    public const string SourceClassification = "authoritative.real-resolver";

    public static BattleDefinition Create100v100Fixture(ulong seed = DefaultSeed)
    {
        return BattleDefinition.Commit(Create100v100FixtureInput(seed));
    }

    public static BattleDefinitionInput Create100v100FixtureInput(ulong seed = DefaultSeed)
    {
        var fixtureDefinitions = new[]
        {
            new FixtureUnitDefinition(
                new StableId("fixture.infantry.scale.side-a"),
                M1BattleSettings.FixtureMaximumHealth,
                M1BattleSettings.FixtureMeleeAttack,
                M1BattleSettings.FixtureMeleeDefense,
                M1BattleSettings.FixtureStartingMorale,
                M1BattleSettings.FixtureMoraleLossPerCasualty,
                M1BattleSettings.FixtureMeleeRangeUnits,
                M1BattleSettings.FixtureAttackCooldownTicks),
            new FixtureUnitDefinition(
                new StableId("fixture.infantry.scale.side-b"),
                M1BattleSettings.FixtureMaximumHealth,
                M1BattleSettings.FixtureMeleeAttack,
                M1BattleSettings.FixtureMeleeDefense,
                M1BattleSettings.FixtureStartingMorale,
                M1BattleSettings.FixtureMoraleLossPerCasualty,
                M1BattleSettings.FixtureMeleeRangeUnits,
                M1BattleSettings.FixtureAttackCooldownTicks),
        };

        var sideAId = new BattleSideId("side.a");
        var sideBId = new BattleSideId("side.b");
        var squadAId = new BattleSquadId("squad.scale.a.infantry");
        var squadBId = new BattleSquadId("squad.scale.b.infantry");
        var sideAUnitIds = CreateUnitIds("unit.scale.a", UnitsPerSide);
        var sideBUnitIds = CreateUnitIds("unit.scale.b", UnitsPerSide);

        var sideAUnits = CreateUnits(sideAUnitIds, sideAId, squadAId, fixtureDefinitions[0].DefinitionId);
        var sideBUnits = CreateUnits(sideBUnitIds, sideBId, squadBId, fixtureDefinitions[1].DefinitionId);

        var sideAFormation = new RectangularFormationInput(
            new SimPosition(0, -DeploymentHalfDistanceUnits),
            FormationFacing.South,
            FormationFileCount,
            FormationRankCount,
            FileSpacingUnits,
            RankSpacingUnits,
            sideAUnitIds);
        var sideBFormation = new RectangularFormationInput(
            new SimPosition(0, DeploymentHalfDistanceUnits),
            FormationFacing.North,
            FormationFileCount,
            FormationRankCount,
            FileSpacingUnits,
            RankSpacingUnits,
            sideBUnitIds);

        var sideASquad = new BattleSquadInput(
            squadAId,
            sideAId,
            sideAUnits,
            sideAFormation,
            new AdvanceOrderInput(
                new SimPosition(0, DeploymentHalfDistanceUnits),
                AdvanceStopRangeUnits));
        var sideBSquad = new BattleSquadInput(
            squadBId,
            sideBId,
            sideBUnits,
            sideBFormation,
            new AdvanceOrderInput(
                new SimPosition(0, -DeploymentHalfDistanceUnits),
                AdvanceStopRangeUnits));

        return new BattleDefinitionInput(
            M1BattleSettings.SimulationVersion,
            new BattleId(BattleIdValue),
            seed,
            fixtureDefinitions,
            new[]
            {
                new BattleSideInput(sideAId, new[] { sideASquad }),
                new BattleSideInput(sideBId, new[] { sideBSquad }),
            });
    }

    private static BattleUnitId[] CreateUnitIds(string prefix, int count)
    {
        var ids = new BattleUnitId[count];
        for (var index = 0; index < count; index++)
        {
            ids[index] = new BattleUnitId($"{prefix}.{index:000}");
        }

        return ids;
    }

    private static BattleUnitInput[] CreateUnits(
        BattleUnitId[] unitIds,
        BattleSideId sideId,
        BattleSquadId squadId,
        StableId fixtureDefinitionId)
    {
        var units = new BattleUnitInput[unitIds.Length];
        for (var index = 0; index < unitIds.Length; index++)
        {
            units[index] = new BattleUnitInput(unitIds[index], sideId, squadId, fixtureDefinitionId);
        }

        return units;
    }
}
