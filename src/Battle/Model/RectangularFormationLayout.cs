using System;
using System.Collections.Generic;
using Warwrought.Core;

namespace Warwrought.Battle.Model;

/// <summary>
/// The four cardinal facings supported by the M1 rectangular line layout.
/// </summary>
public enum FormationFacing
{
    North = 0,
    East = 1,
    South = 2,
    West = 3,
}

/// <summary>
/// One immutable rank/file assignment generated from a rectangular formation input.
/// </summary>
public sealed record FormationSlot
{
    public FormationSlot(BattleUnitId unitId, int slotIndex, int rank, int file, SimPosition position)
    {
        UnitId = unitId;
        SlotIndex = slotIndex;
        Rank = rank;
        File = file;
        Position = position;
    }

    public BattleUnitId UnitId { get; }

    public int SlotIndex { get; }

    /// <summary>
    /// Rank zero is the front rank; ranks increase away from the facing direction.
    /// </summary>
    public int Rank { get; }

    public int File { get; }

    public SimPosition Position { get; }
}

/// <summary>
/// The one supported M1 rectangular rank/file layout.
/// Its anchor is the centre of the front rank and its slots are ordered rank-major, then file.
/// </summary>
public sealed record RectangularFormationLayout
{
    public const int MaxDimension = 64;

    private RectangularFormationLayout(RectangularFormationInput input, FormationSlot[] slots)
    {
        Anchor = input.Anchor;
        Facing = input.Facing;
        FileCount = input.FileCount;
        RankCount = input.RankCount;
        FileSpacingUnits = input.FileSpacingUnits;
        RankSpacingUnits = input.RankSpacingUnits;
        OrderedMemberIds = Array.AsReadOnly((BattleUnitId[])input.MemberIds.ToArray());
        Slots = Array.AsReadOnly(slots);
    }

    public SimPosition Anchor { get; }

    public FormationFacing Facing { get; }

    public int FileCount { get; }

    public int RankCount { get; }

    public int FileSpacingUnits { get; }

    public int RankSpacingUnits { get; }

    public IReadOnlyList<BattleUnitId> OrderedMemberIds { get; }

    public IReadOnlyList<FormationSlot> Slots { get; }

    public int SlotCount => Slots.Count;

    public static RectangularFormationLayout Create(RectangularFormationInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (!TryCreate(input, out var layout, out var error))
        {
            throw new ArgumentException(error, nameof(input));
        }

        return layout!;
    }

    /// <summary>
    /// Generates deterministic rank-major slots without consulting a scene, dictionary, or floating-point transform.
    /// </summary>
    public static IReadOnlyList<FormationSlot> GenerateSlots(
        SimPosition anchor,
        FormationFacing facing,
        int fileCount,
        int rankCount,
        int fileSpacingUnits,
        int rankSpacingUnits,
        IEnumerable<BattleUnitId> orderedMemberIds)
    {
        var input = new RectangularFormationInput(
            anchor,
            facing,
            fileCount,
            rankCount,
            fileSpacingUnits,
            rankSpacingUnits,
            orderedMemberIds);

        return Create(input).Slots;
    }

    public bool TryGetSlot(BattleUnitId unitId, out FormationSlot? slot)
    {
        for (var index = 0; index < Slots.Count; index++)
        {
            if (Slots[index].UnitId == unitId)
            {
                slot = Slots[index];
                return true;
            }
        }

        slot = null;
        return false;
    }

    internal static bool TryCreate(
        RectangularFormationInput input,
        out RectangularFormationLayout? layout,
        out string? error)
    {
        layout = null;
        error = null;

        if (input.FileCount < 1 || input.FileCount > MaxDimension)
        {
            error = $"File count must be between 1 and {MaxDimension}; received {input.FileCount}.";
            return false;
        }

        if (input.RankCount < 1 || input.RankCount > MaxDimension)
        {
            error = $"Rank count must be between 1 and {MaxDimension}; received {input.RankCount}.";
            return false;
        }

        var expectedSlotCount = (long)input.FileCount * input.RankCount;
        if (expectedSlotCount != input.MemberIds.Count)
        {
            error = $"Formation dimensions require {expectedSlotCount} members, but {input.MemberIds.Count} member IDs were supplied.";
            return false;
        }

        if (!Enum.IsDefined(input.Facing))
        {
            error = $"Formation facing value {(int)input.Facing} is unsupported.";
            return false;
        }

        if (input.FileSpacingUnits <= 0 || input.FileSpacingUnits > SimPosition.MaxCoordinateUnits)
        {
            error = $"File spacing must be between 1 and {SimPosition.MaxCoordinateUnits} scaled units; received {input.FileSpacingUnits}.";
            return false;
        }

        if (input.RankSpacingUnits <= 0 || input.RankSpacingUnits > SimPosition.MaxCoordinateUnits)
        {
            error = $"Rank spacing must be between 1 and {SimPosition.MaxCoordinateUnits} scaled units; received {input.RankSpacingUnits}.";
            return false;
        }

        if (input.FileCount % 2 == 0 && input.FileSpacingUnits % 2 != 0)
        {
            error = "An even-width formation needs an even file spacing so its centred slots remain integer coordinates.";
            return false;
        }

        for (var index = 0; index < input.MemberIds.Count; index++)
        {
            var memberId = input.MemberIds[index];
            if (!memberId.IsValid)
            {
                error = $"Formation slot {index} has an invalid unit ID.";
                return false;
            }

            if (index > 0 && input.MemberIds[index - 1].CompareTo(memberId) >= 0)
            {
                error = $"Formation member IDs must be strictly ordinal and increasing; slot {index - 1} precedes slot {index}.";
                return false;
            }
        }

        GetAxes(input.Facing, out var forwardX, out var forwardZ, out var rightX, out var rightZ);
        var slots = new FormationSlot[input.MemberIds.Count];

        for (var rank = 0; rank < input.RankCount; rank++)
        {
            var rankOffset = (long)rank * input.RankSpacingUnits;

            for (var file = 0; file < input.FileCount; file++)
            {
                var slotIndex = (rank * input.FileCount) + file;
                var lateralNumerator = checked((2L * file - (input.FileCount - 1L)) * input.FileSpacingUnits);
                var lateralOffset = lateralNumerator / 2L;
                var x = checked((long)input.Anchor.X + (rightX * lateralOffset) - (forwardX * rankOffset));
                var z = checked((long)input.Anchor.Z + (rightZ * lateralOffset) - (forwardZ * rankOffset));

                if (x < -SimPosition.MaxCoordinateUnits || x > SimPosition.MaxCoordinateUnits ||
                    z < -SimPosition.MaxCoordinateUnits || z > SimPosition.MaxCoordinateUnits)
                {
                    error = $"Formation slot {slotIndex} would leave the valid simulation coordinate bounds at ({x}, {z}).";
                    return false;
                }

                var position = new SimPosition((int)x, (int)z);
                slots[slotIndex] = new FormationSlot(input.MemberIds[slotIndex], slotIndex, rank, file, position);
            }
        }

        layout = new RectangularFormationLayout(input, slots);
        return true;
    }

    private static void GetAxes(
        FormationFacing facing,
        out int forwardX,
        out int forwardZ,
        out int rightX,
        out int rightZ)
    {
        switch (facing)
        {
            case FormationFacing.North:
                forwardX = 0;
                forwardZ = -1;
                rightX = 1;
                rightZ = 0;
                return;
            case FormationFacing.East:
                forwardX = 1;
                forwardZ = 0;
                rightX = 0;
                rightZ = 1;
                return;
            case FormationFacing.South:
                forwardX = 0;
                forwardZ = 1;
                rightX = -1;
                rightZ = 0;
                return;
            case FormationFacing.West:
                forwardX = -1;
                forwardZ = 0;
                rightX = 0;
                rightZ = -1;
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(facing), facing, "The M1 formation facing is unsupported.");
        }
    }
}
