using System;
using System.Collections.Generic;
using System.Linq;
using Warwrought.Battle.Model;
using Warwrought.Battle.Simulation;
using Warwrought.Core;
using Xunit;

namespace Warwrought.Tests;

public sealed class M1BattleFoundationTests
{
    [Fact]
    public void DeterministicRngHasKnownSequenceAndSeedDivergence()
    {
        var zeroSeed = new DeterministicRng(0);

        Assert.Equal(0xE220A8397B1DCDAFUL, zeroSeed.NextUInt64());
        Assert.Equal(0x6E789E6AA1B965F4UL, zeroSeed.NextUInt64());
        Assert.Equal(0x06C45D188009454FUL, zeroSeed.NextUInt64());

        var differentSeed = new DeterministicRng(1);
        Assert.NotEqual(0xE220A8397B1DCDAFUL, differentSeed.NextUInt64());

        var ranged = new DeterministicRng(82741);
        for (var index = 0; index < 32; index++)
        {
            Assert.InRange(ranged.NextInt(-3, 7), -3, 6);
        }

        Assert.True(new DeterministicRng(1).CheckProbability(1, 1));
        Assert.False(new DeterministicRng(1).CheckProbability(0, 1));
    }

    [Fact]
    public void DeterministicRngSupportsWideSignedHalfOpenRangesAndReproduces()
    {
        var ranges = new (int MinInclusive, int MaxExclusive)[]
        {
            (int.MinValue, int.MaxValue),
            (-2_000_000_000, 2_000_000_000),
            (int.MinValue, int.MinValue + 32),
            (int.MaxValue - 32, int.MaxValue),
        };

        foreach (var (minInclusive, maxExclusive) in ranges)
        {
            var first = new DeterministicRng(82741);
            var second = new DeterministicRng(82741);

            for (var index = 0; index < 128; index++)
            {
                var firstValue = first.NextInt(minInclusive, maxExclusive);
                var secondValue = second.NextInt(minInclusive, maxExclusive);

                Assert.Equal(firstValue, secondValue);
                Assert.True(
                    firstValue >= minInclusive && firstValue < maxExclusive,
                    $"Returned {firstValue} outside [{minInclusive}, {maxExclusive}).");
            }
        }
    }

    [Fact]
    public void DeterministicRngRejectsInvalidSignedRanges()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new DeterministicRng(82741).NextInt(0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new DeterministicRng(82741).NextInt(1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new DeterministicRng(82741).NextInt(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new DeterministicRng(82741).NextInt(-1));
    }

    [Fact]
    public void ScaledPositionsUseIntegerArithmeticAndRejectOutOfBoundsValues()
    {
        var left = new SimPosition(-1_000, -2_000);
        var right = new SimPosition(2_000, 2_000);

        Assert.Equal(SimPosition.MaxCoordinateUnits, new SimPosition(-SimPosition.MaxCoordinateUnits, SimPosition.MaxCoordinateUnits).X * -1);
        Assert.Equal(new SimPosition(500, 250), left.Translate(1_500, 2_250));
        Assert.Equal(25_000_000L, left.DistanceSquaredTo(right));
        Assert.Equal(7_000L, left.ManhattanDistanceTo(right));
        Assert.True(SimPosition.TryCreate(SimPosition.MaxCoordinateUnits, -SimPosition.MaxCoordinateUnits, out var boundary));
        Assert.Equal(new SimPosition(SimPosition.MaxCoordinateUnits, -SimPosition.MaxCoordinateUnits), boundary);
        Assert.False(SimPosition.TryCreate(SimPosition.MaxCoordinateUnits + 1, 0, out _));

        Assert.Throws<ArgumentOutOfRangeException>(() => new SimPosition(SimPosition.MaxCoordinateUnits + 1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SimPosition(0, int.MinValue));
        Assert.Throws<ArgumentOutOfRangeException>(() => SimulationTick.Zero.Advance(-1));
        Assert.Equal(new SimulationTick(12), SimulationTick.Zero.Advance(M1BattleSettings.TicksPerSecond));
    }

    [Fact]
    public void CanonicalInputDigestIgnoresFixtureLookupInsertionOrder()
    {
        var orderedInput = BattleFixtureFactory.CreateM1MeleeFixtureInput(82741);
        var reversedLookupInput = new BattleDefinitionInput(
            orderedInput.SimulationVersion,
            orderedInput.BattleId,
            orderedInput.Seed,
            orderedInput.FixtureUnitDefinitions.Reverse(),
            orderedInput.Sides.Reverse());

        var ordered = BattleDefinition.Commit(orderedInput);
        var reversed = BattleDefinition.Commit(reversedLookupInput);

        Assert.Equal(ordered.CanonicalInputDigest, reversed.CanonicalInputDigest);
        Assert.Equal(ordered.Sides[0].Id, new BattleSideId("side.a"));
        Assert.Equal(ordered.Sides[1].Id, new BattleSideId("side.b"));
    }

    [Fact]
    public void RectangularGeneratorProducesStableCenteredRankFileSlots()
    {
        var memberIds = new[]
        {
            new BattleUnitId("unit.test.000"),
            new BattleUnitId("unit.test.001"),
            new BattleUnitId("unit.test.002"),
            new BattleUnitId("unit.test.003"),
        };

        var first = RectangularFormationLayout.GenerateSlots(
            new SimPosition(10_000, 20_000),
            FormationFacing.North,
            fileCount: 2,
            rankCount: 2,
            fileSpacingUnits: 1_000,
            rankSpacingUnits: 2_000,
            memberIds);
        var second = RectangularFormationLayout.GenerateSlots(
            new SimPosition(10_000, 20_000),
            FormationFacing.North,
            fileCount: 2,
            rankCount: 2,
            fileSpacingUnits: 1_000,
            rankSpacingUnits: 2_000,
            memberIds);

        Assert.Equal(first, second);
        Assert.Equal(new SimPosition(9_500, 20_000), first[0].Position);
        Assert.Equal(new SimPosition(10_500, 20_000), first[1].Position);
        Assert.Equal(new SimPosition(9_500, 22_000), first[2].Position);
        Assert.Equal(new SimPosition(10_500, 22_000), first[3].Position);
        Assert.Equal(0, first[0].Rank);
        Assert.Equal(1, first[2].Rank);
        Assert.Equal(0, first[0].File);
        Assert.Equal(1, first[1].File);
    }

    [Fact]
    public void M1FixtureCommitsWithStableCountsIdsSlotsAndDigest()
    {
        var first = BattleFixtureFactory.CreateM1MeleeFixture();
        var second = BattleFixtureFactory.CreateM1MeleeFixture();

        Assert.Equal(M1BattleSettings.SimulationVersion, first.SimulationVersion);
        Assert.Equal(M1BattleSettings.TicksPerSecond, 12);
        Assert.Equal(2, first.Sides.Count);
        Assert.Equal(128, first.TotalUnitCount);

        for (var sideIndex = 0; sideIndex < first.Sides.Count; sideIndex++)
        {
            var firstSquad = first.Sides[sideIndex].Squads[0];
            var secondSquad = second.Sides[sideIndex].Squads[0];
            Assert.Equal(M1BattleSettings.FixtureUnitsPerSide, firstSquad.Members.Count);
            Assert.Equal(M1BattleSettings.FixtureUnitsPerSide, firstSquad.Formation.Slots.Count);
            Assert.Equal(M1BattleSettings.FixtureFormationFileCount, firstSquad.Formation.FileCount);
            Assert.Equal(M1BattleSettings.FixtureFormationRankCount, firstSquad.Formation.RankCount);

            for (var memberIndex = 0; memberIndex < firstSquad.Members.Count; memberIndex++)
            {
                Assert.Equal(firstSquad.Members[memberIndex].Id, secondSquad.Members[memberIndex].Id);
                Assert.Equal(firstSquad.Formation.Slots[memberIndex], secondSquad.Formation.Slots[memberIndex]);
                Assert.Equal(firstSquad.Formation.Slots[memberIndex].UnitId, firstSquad.Members[memberIndex].Id);
                Assert.Equal(memberIndex / M1BattleSettings.FixtureFormationFileCount, firstSquad.Formation.Slots[memberIndex].Rank);
                Assert.Equal(memberIndex % M1BattleSettings.FixtureFormationFileCount, firstSquad.Formation.Slots[memberIndex].File);
            }
        }

        Assert.Equal(first.CanonicalInputDigest, second.CanonicalInputDigest);
        Assert.Equal(
            "65ff279dac01cc31305336485af41cb595c37137edd25e04753aa5c62ec84787",
            first.CanonicalInputDigest);
        Assert.NotEqual(first.CanonicalInputDigest, BattleFixtureFactory.CreateM1MeleeFixture(first.Seed + 1).CanonicalInputDigest);
        Assert.Equal("unit.a.000", first.OrderedUnits[0].Id.Value);
        Assert.Equal("unit.b.063", first.OrderedUnits[^1].Id.Value);
    }

    [Fact]
    public void CommittedDefinitionDoesNotRetainCallerOwnedMutableCollections()
    {
        var fixtureInput = CreateValidInput(2);
        var originalFormation = fixtureInput.Sides[0].Squads[0].Formation!;
        var fixtureDefinitions = fixtureInput.FixtureUnitDefinitions.ToList();
        var memberIds = originalFormation.MemberIds.ToList();
        var members = fixtureInput.Sides[0].Squads[0].Members.ToList();
        var squad = new BattleSquadInput(
            fixtureInput.Sides[0].Squads[0].Id,
            fixtureInput.Sides[0].Squads[0].SideId,
            members,
            new RectangularFormationInput(
                originalFormation.Anchor,
                originalFormation.Facing,
                originalFormation.FileCount,
                originalFormation.RankCount,
                originalFormation.FileSpacingUnits,
                originalFormation.RankSpacingUnits,
                memberIds),
            fixtureInput.Sides[0].Squads[0].AdvanceOrder);
        var sides = new List<BattleSideInput>
        {
            new(fixtureInput.Sides[0].Id, new[] { squad }),
            fixtureInput.Sides[1],
        };
        var input = new BattleDefinitionInput(
            fixtureInput.SimulationVersion,
            fixtureInput.BattleId,
            fixtureInput.Seed,
            fixtureDefinitions,
            sides);

        var committed = BattleDefinition.Commit(input);
        var originalDigest = committed.CanonicalInputDigest;

        fixtureDefinitions.Clear();
        memberIds[0] = new BattleUnitId("unit.changed");
        members[0] = new BattleUnitInput(
            new BattleUnitId("unit.changed"),
            new BattleSideId("side.a"),
            new BattleSquadId("squad.a.infantry"),
            new StableId("fixture.infantry.side-a"));
        sides.Clear();

        Assert.Equal(originalDigest, committed.CanonicalInputDigest);
        Assert.Equal(4, committed.TotalUnitCount);
        Assert.Equal("unit.a.000", committed.OrderedUnits[0].Id.Value);

        var committedSides = Assert.IsAssignableFrom<IList<BattleSide>>(committed.Sides);
        Assert.True(committedSides.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => committedSides.Clear());
    }

    [Fact]
    public void ValidationRejectsUnsupportedVersionAndInvalidFixtureReferences()
    {
        var unsupported = CreateValidInput(1, new SimulationVersion(9, 9));
        var unsupportedResult = BattleDefinition.Validate(unsupported);
        Assert.False(unsupportedResult.IsValid);
        Assert.True(unsupportedResult.HasCode(BattleValidationCode.UnsupportedSimulationVersion));
        Assert.False(BattleDefinition.TryCommit(unsupported, out var unsupportedDefinition, out var tryValidation));
        Assert.Null(unsupportedDefinition);
        Assert.True(tryValidation.HasCode(BattleValidationCode.UnsupportedSimulationVersion));
        var commitException = Assert.Throws<BattleDefinitionValidationException>(() => BattleDefinition.Commit(unsupported));
        Assert.Contains("UnsupportedSimulationVersion", commitException.Message, StringComparison.Ordinal);

        var missingReference = ReplaceUnit(
            CreateValidInput(2),
            0,
            0,
            0,
            new BattleUnitInput(
                new BattleUnitId("unit.a.000"),
                new BattleSideId("side.a"),
                new BattleSquadId("squad.a.infantry"),
                new StableId("fixture.missing")));
        var missingResult = BattleDefinition.Validate(missingReference);
        Assert.False(missingResult.IsValid);
        Assert.True(missingResult.HasCode(BattleValidationCode.MissingFixtureUnitReference));

        var invalidReference = ReplaceUnit(
            CreateValidInput(2),
            0,
            0,
            0,
            new BattleUnitInput(
                new BattleUnitId("unit.a.000"),
                new BattleSideId("side.a"),
                new BattleSquadId("squad.a.infantry"),
                default));
        var invalidResult = BattleDefinition.Validate(invalidReference);
        Assert.False(invalidResult.IsValid);
        Assert.True(invalidResult.HasCode(BattleValidationCode.InvalidFixtureUnitReference));
    }

    [Fact]
    public void ValidationRejectsDuplicateIdsAndMissingAssignments()
    {
        var valid = CreateValidInput(1);
        var sideA = valid.Sides[0];
        var duplicateSide = new BattleSideInput(sideA.Id, new[]
        {
            new BattleSquadInput(
                sideA.Squads[0].Id,
                sideA.Id,
                new[]
                {
                    new BattleUnitInput(
                        new BattleUnitId("unit.a.000"),
                        sideA.Id,
                        sideA.Squads[0].Id,
                        new StableId("fixture.infantry.side-a")),
                },
                sideA.Squads[0].Formation,
                sideA.Squads[0].AdvanceOrder),
        });
        var duplicateInput = new BattleDefinitionInput(
            valid.SimulationVersion,
            valid.BattleId,
            valid.Seed,
            valid.FixtureUnitDefinitions,
            new[] { duplicateSide, duplicateSide });
        var duplicateResult = BattleDefinition.Validate(duplicateInput);

        Assert.False(duplicateResult.IsValid);
        Assert.True(duplicateResult.HasCode(BattleValidationCode.DuplicateSideId));
        Assert.True(duplicateResult.HasCode(BattleValidationCode.DuplicateSquadId));
        Assert.True(duplicateResult.HasCode(BattleValidationCode.DuplicateUnitId));

        var missingAssignment = ReplaceUnit(
            CreateValidInput(1),
            0,
            0,
            0,
            new BattleUnitInput(
                new BattleUnitId("unit.a.000"),
                new BattleSideId("side.missing"),
                new BattleSquadId("squad.missing"),
                new StableId("fixture.infantry.side-a")));
        var assignmentResult = BattleDefinition.Validate(missingAssignment);
        Assert.False(assignmentResult.IsValid);
        Assert.True(assignmentResult.HasCode(BattleValidationCode.MissingSideReference));
        Assert.True(assignmentResult.HasCode(BattleValidationCode.MissingSquadReference));
    }

    [Fact]
    public void ValidationRejectsOrderingDimensionsSlotsDeploymentAndRanges()
    {
        var valid = CreateValidInput(2);
        var originalSquad = valid.Sides[0].Squads[0];
        var reversedMembers = originalSquad.Members.Reverse().ToArray();
        var reversedSlots = originalSquad.Formation!.MemberIds.Reverse().ToArray();
        var invalidOrder = ReplaceSquad(
            valid,
            0,
            new BattleSquadInput(
                originalSquad.Id,
                originalSquad.SideId,
                reversedMembers,
                new RectangularFormationInput(
                    originalSquad.Formation.Anchor,
                    originalSquad.Formation.Facing,
                    originalSquad.Formation.FileCount,
                    originalSquad.Formation.RankCount,
                    originalSquad.Formation.FileSpacingUnits,
                    originalSquad.Formation.RankSpacingUnits,
                    reversedSlots),
                originalSquad.AdvanceOrder));
        var orderingResult = BattleDefinition.Validate(invalidOrder);
        Assert.False(orderingResult.IsValid);
        Assert.True(orderingResult.HasCode(BattleValidationCode.InvalidMemberOrdering));

        var invalidDimensions = ReplaceFormation(
            valid,
            0,
            new RectangularFormationInput(
                originalSquad.Formation.Anchor,
                originalSquad.Formation.Facing,
                0,
                1,
                originalSquad.Formation.FileSpacingUnits,
                originalSquad.Formation.RankSpacingUnits,
                originalSquad.Formation.MemberIds));
        var dimensionResult = BattleDefinition.Validate(invalidDimensions);
        Assert.False(dimensionResult.IsValid);
        Assert.True(dimensionResult.HasCode(BattleValidationCode.InvalidFormationDimensions));

        var invalidSlots = ReplaceFormation(
            valid,
            0,
            new RectangularFormationInput(
                originalSquad.Formation.Anchor,
                originalSquad.Formation.Facing,
                2,
                1,
                originalSquad.Formation.FileSpacingUnits,
                originalSquad.Formation.RankSpacingUnits,
                new[] { originalSquad.Members[0].Id, originalSquad.Members[0].Id }));
        var slotResult = BattleDefinition.Validate(invalidSlots);
        Assert.False(slotResult.IsValid);
        Assert.True(slotResult.HasCode(BattleValidationCode.DuplicateFormationSlot));

        var invalidDeployment = ReplaceSquad(
            valid,
            0,
            new BattleSquadInput(
                originalSquad.Id,
                originalSquad.SideId,
                originalSquad.Members,
                originalSquad.Formation,
                new AdvanceOrderInput(originalSquad.Formation.Anchor, 0)));
        var deploymentResult = BattleDefinition.Validate(invalidDeployment);
        Assert.False(deploymentResult.IsValid);
        Assert.True(deploymentResult.HasCode(BattleValidationCode.InvalidDeployment));
        Assert.True(deploymentResult.HasCode(BattleValidationCode.InvalidRange));
    }

    private static BattleDefinitionInput CreateValidInput(int membersPerSide, SimulationVersion? version = null)
    {
        var definitionA = new FixtureUnitDefinition(new StableId("fixture.infantry.side-a"), 100, 12, 8, 100, 4, 1_500, 12);
        var definitionB = new FixtureUnitDefinition(new StableId("fixture.infantry.side-b"), 100, 12, 8, 100, 4, 1_500, 12);
        var sideAId = new BattleSideId("side.a");
        var sideBId = new BattleSideId("side.b");
        var squadAId = new BattleSquadId("squad.a.infantry");
        var squadBId = new BattleSquadId("squad.b.infantry");
        var sideAUnits = CreateUnits("unit.a", membersPerSide, sideAId, squadAId, definitionA.DefinitionId);
        var sideBUnits = CreateUnits("unit.b", membersPerSide, sideBId, squadBId, definitionB.DefinitionId);

        var sideAFormation = new RectangularFormationInput(new SimPosition(0, -5_000), FormationFacing.South, membersPerSide, 1, 1_000, 1_000, sideAUnits.Select(unit => unit.Id));
        var sideBFormation = new RectangularFormationInput(new SimPosition(0, 5_000), FormationFacing.North, membersPerSide, 1, 1_000, 1_000, sideBUnits.Select(unit => unit.Id));
        var sideASquad = new BattleSquadInput(squadAId, sideAId, sideAUnits, sideAFormation, new AdvanceOrderInput(new SimPosition(0, 5_000), 1_000));
        var sideBSquad = new BattleSquadInput(squadBId, sideBId, sideBUnits, sideBFormation, new AdvanceOrderInput(new SimPosition(0, -5_000), 1_000));

        return new BattleDefinitionInput(
            version ?? M1BattleSettings.SimulationVersion,
            new BattleId("battle.test.fixture"),
            82741,
            new[] { definitionA, definitionB },
            new[] { new BattleSideInput(sideAId, new[] { sideASquad }), new BattleSideInput(sideBId, new[] { sideBSquad }) });
    }

    private static BattleUnitInput[] CreateUnits(
        string prefix,
        int count,
        BattleSideId sideId,
        BattleSquadId squadId,
        StableId definitionId)
    {
        var units = new BattleUnitInput[count];
        for (var index = 0; index < count; index++)
        {
            units[index] = new BattleUnitInput(new BattleUnitId($"{prefix}.{index:000}"), sideId, squadId, definitionId);
        }

        return units;
    }

    private static BattleDefinitionInput ReplaceSquad(
        BattleDefinitionInput input,
        int sideIndex,
        BattleSquadInput replacement)
    {
        var sides = input.Sides.ToArray();
        var side = sides[sideIndex];
        sides[sideIndex] = new BattleSideInput(side.Id, new[] { replacement });
        return new BattleDefinitionInput(input.SimulationVersion, input.BattleId, input.Seed, input.FixtureUnitDefinitions, sides);
    }

    private static BattleDefinitionInput ReplaceUnit(
        BattleDefinitionInput input,
        int sideIndex,
        int squadIndex,
        int memberIndex,
        BattleUnitInput replacement)
    {
        var squad = input.Sides[sideIndex].Squads[squadIndex];
        var members = squad.Members.ToArray();
        members[memberIndex] = replacement;
        return ReplaceSquad(input, sideIndex, new BattleSquadInput(squad.Id, squad.SideId, members, squad.Formation, squad.AdvanceOrder));
    }

    private static BattleDefinitionInput ReplaceFormation(
        BattleDefinitionInput input,
        int sideIndex,
        RectangularFormationInput replacement)
    {
        var squad = input.Sides[sideIndex].Squads[0];
        return ReplaceSquad(input, sideIndex, new BattleSquadInput(squad.Id, squad.SideId, squad.Members, replacement, squad.AdvanceOrder));
    }
}
