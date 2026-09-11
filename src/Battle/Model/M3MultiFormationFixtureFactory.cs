using System;
using Warwrought.Battle.Simulation;
using Warwrought.Core;

namespace Warwrought.Battle.Model;

/// <summary>
/// Builds the M3.1 six-formation proof fixture. The ordered left/centre/right squad pairing is
/// provisional fixture data for this structural milestone, not a public doctrine or target policy.
/// </summary>
public static class M3MultiFormationFixtureFactory
{
    public const ulong DefaultSeed = 83_101;

    public const string BattleId = "battle.m3.multiformation.fixture";

    public static BattleDefinition CreateM3MultiFormationFixture(ulong seed = DefaultSeed)
    {
        return BattleDefinition.Commit(CreateM3MultiFormationFixtureInput(seed));
    }

    public static BattleDefinitionInput CreateM3MultiFormationFixtureInput(ulong seed = DefaultSeed)
    {
        var sideAId = new BattleSideId("side.a");
        var sideBId = new BattleSideId("side.b");
        var sideAFixtureId = new StableId("fixture.infantry.m3.side-a");
        var sideBFixtureId = new StableId("fixture.infantry.m3.side-b");
        var fixtureDefinitions = new[]
        {
            CreateFixtureDefinition(sideAFixtureId),
            CreateFixtureDefinition(sideBFixtureId),
        };

        var sideASquads = new[]
        {
            CreateSquad(sideAId, "01.left", -9_000, -M1BattleSettings.FixtureDeploymentHalfDistanceUnits, FormationFacing.South, sideAFixtureId),
            CreateSquad(sideAId, "02.centre", 0, -M1BattleSettings.FixtureDeploymentHalfDistanceUnits, FormationFacing.South, sideAFixtureId),
            CreateSquad(sideAId, "03.right", 9_000, -M1BattleSettings.FixtureDeploymentHalfDistanceUnits, FormationFacing.South, sideAFixtureId),
        };
        var sideBSquads = new[]
        {
            CreateSquad(sideBId, "01.left", -9_000, M1BattleSettings.FixtureDeploymentHalfDistanceUnits, FormationFacing.North, sideBFixtureId),
            CreateSquad(sideBId, "02.centre", 0, M1BattleSettings.FixtureDeploymentHalfDistanceUnits, FormationFacing.North, sideBFixtureId),
            CreateSquad(sideBId, "03.right", 9_000, M1BattleSettings.FixtureDeploymentHalfDistanceUnits, FormationFacing.North, sideBFixtureId),
        };

        return new BattleDefinitionInput(
            M1BattleSettings.SimulationVersion,
            new BattleId(BattleId),
            seed,
            fixtureDefinitions,
            new[]
            {
                new BattleSideInput(sideAId, sideASquads),
                new BattleSideInput(sideBId, sideBSquads),
            });
    }

    private static FixtureUnitDefinition CreateFixtureDefinition(StableId definitionId)
    {
        return new FixtureUnitDefinition(
            definitionId,
            M1BattleSettings.FixtureMaximumHealth,
            M1BattleSettings.FixtureMeleeAttack,
            M1BattleSettings.FixtureMeleeDefense,
            M1BattleSettings.FixtureStartingMorale,
            M1BattleSettings.FixtureMoraleLossPerCasualty,
            M1BattleSettings.FixtureMeleeRangeUnits,
            M1BattleSettings.FixtureAttackCooldownTicks);
    }

    private static BattleSquadInput CreateSquad(
        BattleSideId sideId,
        string positionId,
        int anchorX,
        int anchorZ,
        FormationFacing facing,
        StableId fixtureDefinitionId)
    {
        var squadId = new BattleSquadId($"squad.{sideId.Value[sideId.Value.Length - 1]}.{positionId}");
        var unitIds = CreateUnitIds($"unit.{sideId.Value[sideId.Value.Length - 1]}.{positionId}", M1BattleSettings.FixtureUnitsPerSide);
        var members = CreateUnits(unitIds, sideId, squadId, fixtureDefinitionId);
        var formation = new RectangularFormationInput(
            new SimPosition(anchorX, anchorZ),
            facing,
            fileCount: M1BattleSettings.FixtureFormationFileCount,
            rankCount: M1BattleSettings.FixtureFormationRankCount,
            fileSpacingUnits: M1BattleSettings.FixtureFileSpacingUnits,
            rankSpacingUnits: M1BattleSettings.FixtureRankSpacingUnits,
            unitIds);
        var destinationZ = -anchorZ;

        return new BattleSquadInput(
            squadId,
            sideId,
            members,
            formation,
            new AdvanceOrderInput(
                new SimPosition(anchorX, destinationZ),
                M1BattleSettings.FixtureAdvanceStopRangeUnits));
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
