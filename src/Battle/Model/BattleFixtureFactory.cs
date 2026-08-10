using System;
using System.Collections.Generic;
using Warwrought.Battle.Simulation;
using Warwrought.Core;

namespace Warwrought.Battle.Model;

/// <summary>
/// Builds the intentionally narrow M1 melee fixture. It is fixture content for deterministic proof,
/// not a reusable final unit/content definition factory.
/// </summary>
public static class BattleFixtureFactory
{
    public const ulong DefaultSeed = 82_741;

    public static BattleDefinition CreateM1MeleeFixture(ulong seed = DefaultSeed)
    {
        return BattleDefinition.Commit(CreateM1MeleeFixtureInput(seed));
    }

    public static BattleDefinitionInput CreateM1MeleeFixtureInput(ulong seed = DefaultSeed)
    {
        var fixtureDefinitions = new[]
        {
            new FixtureUnitDefinition(
                new StableId("fixture.infantry.side-a"),
                M1BattleSettings.FixtureMaximumHealth,
                M1BattleSettings.FixtureMeleeAttack,
                M1BattleSettings.FixtureMeleeDefense,
                M1BattleSettings.FixtureStartingMorale,
                M1BattleSettings.FixtureMoraleLossPerCasualty,
                M1BattleSettings.FixtureMeleeRangeUnits,
                M1BattleSettings.FixtureAttackCooldownTicks),
            new FixtureUnitDefinition(
                new StableId("fixture.infantry.side-b"),
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
        var squadAId = new BattleSquadId("squad.a.infantry");
        var squadBId = new BattleSquadId("squad.b.infantry");
        var sideAUnitIds = CreateUnitIds("unit.a", M1BattleSettings.FixtureUnitsPerSide);
        var sideBUnitIds = CreateUnitIds("unit.b", M1BattleSettings.FixtureUnitsPerSide);

        var sideAUnits = CreateUnits(sideAUnitIds, sideAId, squadAId, fixtureDefinitions[0].DefinitionId);
        var sideBUnits = CreateUnits(sideBUnitIds, sideBId, squadBId, fixtureDefinitions[1].DefinitionId);

        var sideAFormation = new RectangularFormationInput(
            new SimPosition(0, -M1BattleSettings.FixtureDeploymentHalfDistanceUnits),
            FormationFacing.South,
            M1BattleSettings.FixtureFormationFileCount,
            M1BattleSettings.FixtureFormationRankCount,
            M1BattleSettings.FixtureFileSpacingUnits,
            M1BattleSettings.FixtureRankSpacingUnits,
            sideAUnitIds);
        var sideBFormation = new RectangularFormationInput(
            new SimPosition(0, M1BattleSettings.FixtureDeploymentHalfDistanceUnits),
            FormationFacing.North,
            M1BattleSettings.FixtureFormationFileCount,
            M1BattleSettings.FixtureFormationRankCount,
            M1BattleSettings.FixtureFileSpacingUnits,
            M1BattleSettings.FixtureRankSpacingUnits,
            sideBUnitIds);

        var sideASquad = new BattleSquadInput(
            squadAId,
            sideAId,
            sideAUnits,
            sideAFormation,
            new AdvanceOrderInput(
                new SimPosition(0, M1BattleSettings.FixtureDeploymentHalfDistanceUnits),
                M1BattleSettings.FixtureAdvanceStopRangeUnits));
        var sideBSquad = new BattleSquadInput(
            squadBId,
            sideBId,
            sideBUnits,
            sideBFormation,
            new AdvanceOrderInput(
                new SimPosition(0, -M1BattleSettings.FixtureDeploymentHalfDistanceUnits),
                M1BattleSettings.FixtureAdvanceStopRangeUnits));

        return new BattleDefinitionInput(
            M1BattleSettings.SimulationVersion,
            new BattleId("battle.m1.melee.fixture"),
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
