using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Warwrought.Battle.Simulation;
using Warwrought.Core;

namespace Warwrought.Battle.Model;

public enum BattleValidationCode
{
    NullInput,
    UnsupportedSimulationVersion,
    InvalidBattleId,
    InvalidSideCount,
    InvalidFixtureDefinition,
    DuplicateFixtureDefinitionId,
    InvalidFixtureStats,
    InvalidSideId,
    DuplicateSideId,
    InvalidSquadCount,
    InvalidSquadId,
    DuplicateSquadId,
    MissingSideReference,
    SideReferenceMismatch,
    InvalidUnitId,
    DuplicateUnitId,
    MissingSquadReference,
    SquadReferenceMismatch,
    InvalidFixtureUnitReference,
    MissingFixtureUnitReference,
    InvalidMemberOrdering,
    MissingFormation,
    InvalidFormationDimensions,
    InvalidFormationMemberCount,
    InvalidFormationFacing,
    InvalidFormationSpacing,
    InvalidFormationSlot,
    FormationSlotMemberMismatch,
    DuplicateFormationSlot,
    MissingAdvanceOrder,
    InvalidDeployment,
    InvalidRange,
}

public sealed record BattleValidationError
{
    public BattleValidationError(BattleValidationCode code, string path, string message)
    {
        Code = code;
        Path = path;
        Message = message;
    }

    public BattleValidationCode Code { get; }

    public string Path { get; }

    public string Message { get; }

    public override string ToString() => $"{Code} at {Path}: {Message}";
}

public sealed class BattleValidationResult
{
    private readonly ReadOnlyCollection<BattleValidationError> _errors;

    internal BattleValidationResult(IEnumerable<BattleValidationError> errors)
    {
        _errors = Array.AsReadOnly(errors.ToArray());
    }

    public bool IsValid => _errors.Count == 0;

    public IReadOnlyList<BattleValidationError> Errors => _errors;

    public bool HasCode(BattleValidationCode code)
    {
        for (var index = 0; index < _errors.Count; index++)
        {
            if (_errors[index].Code == code)
            {
                return true;
            }
        }

        return false;
    }

    public override string ToString() => IsValid ? "Battle definition is valid." : string.Join(Environment.NewLine, _errors);
}

public sealed class BattleDefinitionValidationException : InvalidOperationException
{
    public BattleDefinitionValidationException(BattleValidationResult validation)
        : base($"Battle definition validation failed:{Environment.NewLine}{validation}")
    {
        Validation = validation;
    }

    public BattleValidationResult Validation { get; }
}

/// <summary>
/// Immutable committed advance order for the M1 fixture.
/// </summary>
public sealed record AdvanceOrder
{
    internal AdvanceOrder(SimPosition destination, int stopRangeUnits)
    {
        Destination = destination;
        StopRangeUnits = stopRangeUnits;
    }

    public SimPosition Destination { get; }

    public int StopRangeUnits { get; }
}

/// <summary>
/// Immutable committed unit membership and fixture reference.
/// </summary>
public sealed record BattleUnit
{
    internal BattleUnit(
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
/// Immutable committed squad and its one rectangular formation.
/// </summary>
public sealed record BattleSquad
{
    internal BattleSquad(
        BattleSquadId id,
        BattleSideId sideId,
        IEnumerable<BattleUnit> members,
        RectangularFormationLayout formation,
        AdvanceOrder advanceOrder)
    {
        Id = id;
        SideId = sideId;
        Members = RectangularFormationInput.CopyReadOnly(members, nameof(members));
        Formation = formation;
        AdvanceOrder = advanceOrder;
    }

    public BattleSquadId Id { get; }

    public BattleSideId SideId { get; }

    public IReadOnlyList<BattleUnit> Members { get; }

    public RectangularFormationLayout Formation { get; }

    public AdvanceOrder AdvanceOrder { get; }
}

/// <summary>
/// Immutable committed side. M1.1 permits exactly one squad per side.
/// </summary>
public sealed record BattleSide
{
    internal BattleSide(BattleSideId id, IEnumerable<BattleSquad> squads)
    {
        Id = id;
        Squads = RectangularFormationInput.CopyReadOnly(squads, nameof(squads));
    }

    public BattleSideId Id { get; }

    public IReadOnlyList<BattleSquad> Squads { get; }
}

/// <summary>
/// Immutable authoritative input boundary for the narrow M1 fixture.
/// It is committed only after all cross-references, ordered members, deployment, and layout invariants pass.
/// </summary>
public sealed record BattleDefinition
{
    public const string CanonicalDigestDomain = "warwrought.battle-definition.m1.input.v1";

    private BattleDefinition(
        SimulationVersion simulationVersion,
        BattleId battleId,
        ulong seed,
        IEnumerable<FixtureUnitDefinition> fixtureUnitDefinitions,
        IEnumerable<BattleSide> sides)
    {
        SimulationVersion = simulationVersion;
        BattleId = battleId;
        Seed = seed;
        FixtureUnitDefinitions = RectangularFormationInput.CopyReadOnly(fixtureUnitDefinitions, nameof(fixtureUnitDefinitions));
        Sides = RectangularFormationInput.CopyReadOnly(sides, nameof(sides));

        var orderedUnits = new List<BattleUnit>();
        for (var sideIndex = 0; sideIndex < Sides.Count; sideIndex++)
        {
            var side = Sides[sideIndex];
            for (var squadIndex = 0; squadIndex < side.Squads.Count; squadIndex++)
            {
                orderedUnits.AddRange(side.Squads[squadIndex].Members);
            }
        }

        OrderedUnits = Array.AsReadOnly(orderedUnits.ToArray());
        CanonicalInputDigest = ComputeCanonicalInputDigest();
    }

    public SimulationVersion SimulationVersion { get; }

    public BattleId BattleId { get; }

    public ulong Seed { get; }

    public IReadOnlyList<FixtureUnitDefinition> FixtureUnitDefinitions { get; }

    /// <summary>
    /// Sides are stored in ordinal ID order regardless of lookup/input insertion order.
    /// </summary>
    public IReadOnlyList<BattleSide> Sides { get; }

    /// <summary>
    /// Stable flattened view for later simulation code; it is ordered by side, squad, then member order.
    /// </summary>
    public IReadOnlyList<BattleUnit> OrderedUnits { get; }

    public int TotalUnitCount => OrderedUnits.Count;

    public string CanonicalInputDigest { get; }

    public static BattleValidationResult Validate(BattleDefinitionInput? input)
    {
        var errors = new List<BattleValidationError>();
        if (input is null)
        {
            Add(errors, BattleValidationCode.NullInput, "battle", "A battle definition input is required.");
            return new BattleValidationResult(errors);
        }

        if (input.SimulationVersion != M1BattleSettings.SimulationVersion)
        {
            Add(
                errors,
                BattleValidationCode.UnsupportedSimulationVersion,
                "simulationVersion",
                $"Simulation version {input.SimulationVersion} is unsupported; M1 accepts only {M1BattleSettings.SimulationVersion}.");
        }

        if (!input.BattleId.IsValid)
        {
            Add(errors, BattleValidationCode.InvalidBattleId, "battleId", "Battle ID must contain a non-whitespace stable value.");
        }

        var fixtureDefinitionIds = new HashSet<string>(StringComparer.Ordinal);
        ValidateFixtureDefinitions(input.FixtureUnitDefinitions, fixtureDefinitionIds, errors);

        if (input.Sides.Count != 2)
        {
            Add(errors, BattleValidationCode.InvalidSideCount, "sides", $"M1 requires exactly two sides; received {input.Sides.Count}.");
        }

        var sideIds = new HashSet<string>(StringComparer.Ordinal);
        var squadIds = new HashSet<string>(StringComparer.Ordinal);
        ValidateTopLevelIds(input.Sides, sideIds, squadIds, errors);

        var unitIds = new HashSet<string>(StringComparer.Ordinal);
        for (var sideIndex = 0; sideIndex < input.Sides.Count; sideIndex++)
        {
            var side = input.Sides[sideIndex];
            if (side is null)
            {
                Add(errors, BattleValidationCode.InvalidSideId, $"sides[{sideIndex}]", "Side record cannot be null.");
                continue;
            }

            if (side.Squads.Count != 1)
            {
                Add(
                    errors,
                    BattleValidationCode.InvalidSquadCount,
                    $"sides[{sideIndex}].squads",
                    $"M1 supports exactly one squad/formation per side; received {side.Squads.Count}.");
            }

            for (var squadIndex = 0; squadIndex < side.Squads.Count; squadIndex++)
            {
                var squad = side.Squads[squadIndex];
                var squadPath = $"sides[{sideIndex}].squads[{squadIndex}]";
                if (squad is null)
                {
                    Add(errors, BattleValidationCode.InvalidSquadId, squadPath, "Squad record cannot be null.");
                    continue;
                }

                if (!squad.SideId.IsValid || !sideIds.Contains(squad.SideId.Value))
                {
                    Add(
                        errors,
                        BattleValidationCode.MissingSideReference,
                        $"{squadPath}.sideId",
                        $"Squad references missing side '{squad.SideId.Value}'.");
                }
                else if (squad.SideId != side.Id)
                {
                    Add(
                        errors,
                        BattleValidationCode.SideReferenceMismatch,
                        $"{squadPath}.sideId",
                        $"Squad side '{squad.SideId.Value}' does not match containing side '{side.Id.Value}'.");
                }

                var memberPath = $"{squadPath}.members";
                for (var memberIndex = 0; memberIndex < squad.Members.Count; memberIndex++)
                {
                    var member = squad.Members[memberIndex];
                    var unitPath = $"{memberPath}[{memberIndex}]";
                    if (member is null)
                    {
                        Add(errors, BattleValidationCode.InvalidUnitId, unitPath, "Unit record cannot be null.");
                        continue;
                    }

                    if (!member.Id.IsValid)
                    {
                        Add(errors, BattleValidationCode.InvalidUnitId, $"{unitPath}.id", "Unit ID must contain a non-whitespace stable value.");
                    }
                    else if (!unitIds.Add(member.Id.Value))
                    {
                        Add(errors, BattleValidationCode.DuplicateUnitId, $"{unitPath}.id", $"Unit ID '{member.Id.Value}' is duplicated.");
                    }

                    if (!member.SideId.IsValid || !sideIds.Contains(member.SideId.Value))
                    {
                        Add(
                            errors,
                            BattleValidationCode.MissingSideReference,
                            $"{unitPath}.sideId",
                            $"Unit references missing side '{member.SideId.Value}'.");
                    }
                    else if (member.SideId != side.Id)
                    {
                        Add(
                            errors,
                            BattleValidationCode.SideReferenceMismatch,
                            $"{unitPath}.sideId",
                            $"Unit side '{member.SideId.Value}' does not match containing side '{side.Id.Value}'.");
                    }

                    if (!member.SquadId.IsValid || !squadIds.Contains(member.SquadId.Value))
                    {
                        Add(
                            errors,
                            BattleValidationCode.MissingSquadReference,
                            $"{unitPath}.squadId",
                            $"Unit references missing squad '{member.SquadId.Value}'.");
                    }
                    else if (member.SquadId != squad.Id)
                    {
                        Add(
                            errors,
                            BattleValidationCode.SquadReferenceMismatch,
                            $"{unitPath}.squadId",
                            $"Unit squad '{member.SquadId.Value}' does not match containing squad '{squad.Id.Value}'.");
                    }

                    var fixtureReference = member.FixtureUnitDefinitionId.Value;
                    if (string.IsNullOrWhiteSpace(fixtureReference))
                    {
                        Add(
                            errors,
                            BattleValidationCode.InvalidFixtureUnitReference,
                            $"{unitPath}.fixtureUnitDefinitionId",
                            "Fixture unit definition reference must contain a stable value.");
                    }
                    else if (!fixtureDefinitionIds.Contains(fixtureReference))
                    {
                        Add(
                            errors,
                            BattleValidationCode.MissingFixtureUnitReference,
                            $"{unitPath}.fixtureUnitDefinitionId",
                            $"Fixture unit definition '{fixtureReference}' is not present in the committed fixture definitions.");
                    }
                }

                ValidateMemberOrdering(squad.Members, memberPath, errors);
                ValidateFormation(squad.Formation, squad.Members, squadPath, errors);
                ValidateAdvanceOrder(squad.AdvanceOrder, squad.Formation, squadPath, errors);
            }
        }

        return new BattleValidationResult(errors);
    }

    public static BattleDefinition Commit(BattleDefinitionInput input)
    {
        var validation = Validate(input);
        if (!validation.IsValid)
        {
            throw new BattleDefinitionValidationException(validation);
        }

        return Build(input);
    }

    public static bool TryCommit(
        BattleDefinitionInput? input,
        out BattleDefinition? definition,
        out BattleValidationResult validation)
    {
        validation = Validate(input);
        if (!validation.IsValid)
        {
            definition = null;
            return false;
        }

        definition = Build(input!);
        return true;
    }

    public BattleSide GetSide(BattleSideId sideId)
    {
        for (var index = 0; index < Sides.Count; index++)
        {
            if (Sides[index].Id == sideId)
            {
                return Sides[index];
            }
        }

        throw new KeyNotFoundException($"Committed battle does not contain side '{sideId.Value}'.");
    }

    private static BattleDefinition Build(BattleDefinitionInput input)
    {
        var definitions = input.FixtureUnitDefinitions
            .OrderBy(definition => definition.DefinitionId.Value, StringComparer.Ordinal)
            .Select(definition => new FixtureUnitDefinition(
                definition.DefinitionId,
                definition.MaximumHealth,
                definition.MeleeAttack,
                definition.MeleeDefense,
                definition.StartingMorale,
                definition.MoraleLossPerCasualty,
                definition.MeleeRangeUnits,
                definition.AttackCooldownTicks))
            .ToArray();

        var sides = input.Sides
            .OrderBy(side => side.Id.Value, StringComparer.Ordinal)
            .Select(side =>
            {
                var squads = side.Squads
                    .OrderBy(squad => squad.Id.Value, StringComparer.Ordinal)
                    .Select(squad =>
                    {
                        var members = squad.Members
                            .Select(member => new BattleUnit(
                                member.Id,
                                member.SideId,
                                member.SquadId,
                                member.FixtureUnitDefinitionId))
                            .ToArray();
                        var formation = RectangularFormationLayout.Create(squad.Formation!);
                        var advanceOrder = new AdvanceOrder(squad.AdvanceOrder!.Destination, squad.AdvanceOrder.StopRangeUnits);
                        return new BattleSquad(squad.Id, squad.SideId, members, formation, advanceOrder);
                    })
                    .ToArray();

                return new BattleSide(side.Id, squads);
            })
            .ToArray();

        return new BattleDefinition(input.SimulationVersion, input.BattleId, input.Seed, definitions, sides);
    }

    private static void ValidateFixtureDefinitions(
        IReadOnlyList<FixtureUnitDefinition> definitions,
        HashSet<string> definitionIds,
        List<BattleValidationError> errors)
    {
        for (var index = 0; index < definitions.Count; index++)
        {
            var definition = definitions[index];
            var path = $"fixtureUnitDefinitions[{index}]";
            if (definition is null)
            {
                Add(errors, BattleValidationCode.InvalidFixtureDefinition, path, "Fixture unit definition cannot be null.");
                continue;
            }

            var id = definition.DefinitionId.Value;
            if (string.IsNullOrWhiteSpace(id))
            {
                Add(errors, BattleValidationCode.InvalidFixtureDefinition, $"{path}.definitionId", "Fixture definition ID must contain a non-whitespace stable value.");
            }
            else if (!definitionIds.Add(id))
            {
                Add(errors, BattleValidationCode.DuplicateFixtureDefinitionId, $"{path}.definitionId", $"Fixture definition ID '{id}' is duplicated.");
            }

            if (definition.MaximumHealth <= 0 || definition.MaximumHealth > 10_000 ||
                definition.MeleeAttack <= 0 || definition.MeleeAttack > 10_000 ||
                definition.MeleeDefense < 0 || definition.MeleeDefense > 10_000 ||
                definition.StartingMorale <= 0 || definition.StartingMorale > 10_000 ||
                definition.MoraleLossPerCasualty <= 0 || definition.MoraleLossPerCasualty > 10_000 ||
                definition.MeleeRangeUnits <= 0 || definition.MeleeRangeUnits > SimPosition.MaxCoordinateUnits ||
                definition.AttackCooldownTicks <= 0 || definition.AttackCooldownTicks > 1_000_000)
            {
                Add(
                    errors,
                    BattleValidationCode.InvalidFixtureStats,
                    path,
                    "Fixture combat/morale values must be positive where required and within the bounded M1 integer ranges.");
            }
        }
    }

    private static void ValidateTopLevelIds(
        IReadOnlyList<BattleSideInput> sides,
        HashSet<string> sideIds,
        HashSet<string> squadIds,
        List<BattleValidationError> errors)
    {
        for (var sideIndex = 0; sideIndex < sides.Count; sideIndex++)
        {
            var side = sides[sideIndex];
            var sidePath = $"sides[{sideIndex}]";
            if (side is null)
            {
                continue;
            }

            var sideId = side.Id.Value;
            if (string.IsNullOrWhiteSpace(sideId))
            {
                Add(errors, BattleValidationCode.InvalidSideId, $"{sidePath}.id", "Side ID must contain a non-whitespace stable value.");
            }
            else if (!sideIds.Add(sideId))
            {
                Add(errors, BattleValidationCode.DuplicateSideId, $"{sidePath}.id", $"Side ID '{sideId}' is duplicated.");
            }

            for (var squadIndex = 0; squadIndex < side.Squads.Count; squadIndex++)
            {
                var squad = side.Squads[squadIndex];
                if (squad is null)
                {
                    continue;
                }

                var squadId = squad.Id.Value;
                var squadPath = $"{sidePath}.squads[{squadIndex}]";
                if (string.IsNullOrWhiteSpace(squadId))
                {
                    Add(errors, BattleValidationCode.InvalidSquadId, $"{squadPath}.id", "Squad ID must contain a non-whitespace stable value.");
                }
                else if (!squadIds.Add(squadId))
                {
                    Add(errors, BattleValidationCode.DuplicateSquadId, $"{squadPath}.id", $"Squad ID '{squadId}' is duplicated.");
                }
            }
        }
    }

    private static void ValidateMemberOrdering(
        IReadOnlyList<BattleUnitInput> members,
        string path,
        List<BattleValidationError> errors)
    {
        for (var index = 1; index < members.Count; index++)
        {
            var previous = members[index - 1];
            var current = members[index];
            if (previous is not null && current is not null && previous.Id.CompareTo(current.Id) >= 0)
            {
                Add(
                    errors,
                    BattleValidationCode.InvalidMemberOrdering,
                    path,
                    $"Member IDs must be strictly ordinal and increasing; '{previous.Id.Value}' does not precede '{current.Id.Value}'.");
            }
        }
    }

    private static void ValidateFormation(
        RectangularFormationInput? formation,
        IReadOnlyList<BattleUnitInput> members,
        string squadPath,
        List<BattleValidationError> errors)
    {
        if (formation is null)
        {
            Add(errors, BattleValidationCode.MissingFormation, $"{squadPath}.formation", "A rectangular formation deployment is required.");
            return;
        }

        var formationPath = $"{squadPath}.formation";
        var dimensionsValid = true;
        if (formation.FileCount < 1 || formation.FileCount > RectangularFormationLayout.MaxDimension)
        {
            Add(errors, BattleValidationCode.InvalidFormationDimensions, $"{formationPath}.fileCount", $"File count must be between 1 and {RectangularFormationLayout.MaxDimension}; received {formation.FileCount}.");
            dimensionsValid = false;
        }

        if (formation.RankCount < 1 || formation.RankCount > RectangularFormationLayout.MaxDimension)
        {
            Add(errors, BattleValidationCode.InvalidFormationDimensions, $"{formationPath}.rankCount", $"Rank count must be between 1 and {RectangularFormationLayout.MaxDimension}; received {formation.RankCount}.");
            dimensionsValid = false;
        }

        if (dimensionsValid)
        {
            var expectedSlotCount = (long)formation.FileCount * formation.RankCount;
            if (expectedSlotCount != members.Count)
            {
                Add(errors, BattleValidationCode.InvalidFormationMemberCount, formationPath, $"Formation dimensions require {expectedSlotCount} members, but the squad contains {members.Count}.");
            }

            if (expectedSlotCount != formation.MemberIds.Count)
            {
                Add(errors, BattleValidationCode.InvalidFormationMemberCount, $"{formationPath}.memberIds", $"Formation dimensions require {expectedSlotCount} slot IDs, but {formation.MemberIds.Count} were supplied.");
            }
        }

        if (!Enum.IsDefined(formation.Facing))
        {
            Add(errors, BattleValidationCode.InvalidFormationFacing, $"{formationPath}.facing", $"Facing value {(int)formation.Facing} is unsupported.");
        }

        var spacingValid = true;
        if (formation.FileSpacingUnits <= 0 || formation.FileSpacingUnits > SimPosition.MaxCoordinateUnits)
        {
            Add(errors, BattleValidationCode.InvalidFormationSpacing, $"{formationPath}.fileSpacingUnits", "File spacing must be a positive scaled integer within simulation bounds.");
            spacingValid = false;
        }

        if (formation.RankSpacingUnits <= 0 || formation.RankSpacingUnits > SimPosition.MaxCoordinateUnits)
        {
            Add(errors, BattleValidationCode.InvalidFormationSpacing, $"{formationPath}.rankSpacingUnits", "Rank spacing must be a positive scaled integer within simulation bounds.");
            spacingValid = false;
        }

        if (formation.FileCount > 0 && formation.FileCount % 2 == 0 && formation.FileSpacingUnits % 2 != 0)
        {
            Add(errors, BattleValidationCode.InvalidFormationSlot, formationPath, "An even-width formation needs even file spacing for centred integer slots.");
            spacingValid = false;
        }

        var slotIds = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < formation.MemberIds.Count; index++)
        {
            var slotId = formation.MemberIds[index];
            if (!slotId.IsValid)
            {
                Add(errors, BattleValidationCode.InvalidFormationSlot, $"{formationPath}.memberIds[{index}]", "Formation slot unit ID is invalid.");
            }
            else if (!slotIds.Add(slotId.Value))
            {
                Add(errors, BattleValidationCode.DuplicateFormationSlot, $"{formationPath}.memberIds[{index}]", $"Formation slot unit ID '{slotId.Value}' is duplicated.");
            }

            if (index > 0 && formation.MemberIds[index - 1].CompareTo(slotId) >= 0)
            {
                Add(errors, BattleValidationCode.InvalidMemberOrdering, $"{formationPath}.memberIds", "Formation member IDs must be strictly ordinal and increasing.");
            }

            if (index < members.Count && members[index] is not null && members[index].Id != slotId)
            {
                Add(
                    errors,
                    BattleValidationCode.FormationSlotMemberMismatch,
                    $"{formationPath}.memberIds[{index}]",
                    $"Slot ID '{slotId.Value}' does not match ordered member ID '{members[index].Id.Value}'.");
            }
        }

        if (dimensionsValid && spacingValid && formation.MemberIds.Count == members.Count && formation.MemberIds.Count > 0 &&
            RectangularFormationLayout.TryCreate(formation, out _, out var generationError) == false)
        {
            Add(errors, BattleValidationCode.InvalidFormationSlot, formationPath, generationError ?? "Formation slot generation failed.");
        }
    }

    private static void ValidateAdvanceOrder(
        AdvanceOrderInput? advanceOrder,
        RectangularFormationInput? formation,
        string squadPath,
        List<BattleValidationError> errors)
    {
        if (advanceOrder is null)
        {
            Add(errors, BattleValidationCode.MissingAdvanceOrder, $"{squadPath}.advanceOrder", "The M1 fixture requires an advance order.");
            return;
        }

        if (!advanceOrder.Destination.IsValid || (formation is not null && advanceOrder.Destination == formation.Anchor))
        {
            Add(errors, BattleValidationCode.InvalidDeployment, $"{squadPath}.advanceOrder.destination", "Advance destination must be a valid position different from the deployment anchor.");
        }

        if (advanceOrder.StopRangeUnits <= 0 || advanceOrder.StopRangeUnits > SimPosition.MaxCoordinateUnits)
        {
            Add(errors, BattleValidationCode.InvalidRange, $"{squadPath}.advanceOrder.stopRangeUnits", "Advance stop range must be positive and use the bounded scaled integer position convention.");
        }
    }

    private string ComputeCanonicalInputDigest()
    {
        using var writer = new CanonicalDigestWriter();
        writer.WriteString(CanonicalDigestDomain);
        writer.WriteSimulationVersion(SimulationVersion);
        writer.WriteString(BattleId.Value);
        writer.WriteUInt64(Seed);
        writer.WriteInt32(M1BattleSettings.PositionUnitsPerMetre);
        writer.WriteInt32(M1BattleSettings.TicksPerSecond);

        writer.WriteInt32(FixtureUnitDefinitions.Count);
        for (var definitionIndex = 0; definitionIndex < FixtureUnitDefinitions.Count; definitionIndex++)
        {
            var definition = FixtureUnitDefinitions[definitionIndex];
            writer.WriteString(definition.DefinitionId.Value);
            writer.WriteInt32(definition.MaximumHealth);
            writer.WriteInt32(definition.MeleeAttack);
            writer.WriteInt32(definition.MeleeDefense);
            writer.WriteInt32(definition.StartingMorale);
            writer.WriteInt32(definition.MoraleLossPerCasualty);
            writer.WriteInt32(definition.MeleeRangeUnits);
            writer.WriteInt32(definition.AttackCooldownTicks);
        }

        writer.WriteInt32(Sides.Count);
        for (var sideIndex = 0; sideIndex < Sides.Count; sideIndex++)
        {
            var side = Sides[sideIndex];
            writer.WriteString(side.Id.Value);
            writer.WriteInt32(side.Squads.Count);

            for (var squadIndex = 0; squadIndex < side.Squads.Count; squadIndex++)
            {
                var squad = side.Squads[squadIndex];
                writer.WriteString(squad.Id.Value);
                writer.WriteString(squad.SideId.Value);
                writer.WriteInt32(squad.Members.Count);

                for (var memberIndex = 0; memberIndex < squad.Members.Count; memberIndex++)
                {
                    var member = squad.Members[memberIndex];
                    writer.WriteString(member.Id.Value);
                    writer.WriteString(member.SideId.Value);
                    writer.WriteString(member.SquadId.Value);
                    writer.WriteString(member.FixtureUnitDefinitionId.Value);
                }

                var formation = squad.Formation;
                writer.WriteSimPosition(formation.Anchor);
                writer.WriteInt32((int)formation.Facing);
                writer.WriteInt32(formation.FileCount);
                writer.WriteInt32(formation.RankCount);
                writer.WriteInt32(formation.FileSpacingUnits);
                writer.WriteInt32(formation.RankSpacingUnits);
                writer.WriteInt32(formation.OrderedMemberIds.Count);
                for (var memberIndex = 0; memberIndex < formation.OrderedMemberIds.Count; memberIndex++)
                {
                    writer.WriteString(formation.OrderedMemberIds[memberIndex].Value);
                }

                writer.WriteInt32(formation.Slots.Count);
                for (var slotIndex = 0; slotIndex < formation.Slots.Count; slotIndex++)
                {
                    var slot = formation.Slots[slotIndex];
                    writer.WriteInt32(slot.SlotIndex);
                    writer.WriteInt32(slot.Rank);
                    writer.WriteInt32(slot.File);
                    writer.WriteString(slot.UnitId.Value);
                    writer.WriteSimPosition(slot.Position);
                }

                writer.WriteSimPosition(squad.AdvanceOrder.Destination);
                writer.WriteInt32(squad.AdvanceOrder.StopRangeUnits);
            }
        }

        return writer.ComputeSha256Hex();
    }

    private static void Add(List<BattleValidationError> errors, BattleValidationCode code, string path, string message)
    {
        errors.Add(new BattleValidationError(code, path, message));
    }
}
