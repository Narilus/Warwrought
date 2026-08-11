using System;
using System.Collections.Generic;
using System.Linq;
using Warwrought.Battle.Model;
using Warwrought.Core;

namespace Warwrought.Battle.Transcript;

/// <summary>
/// The semantic event vocabulary required by the M1 authoritative fixture.
/// Events describe authoritative decisions and state changes; they are not render frames.
/// </summary>
public enum BattleEventType
{
    BattleStarted,
    FormationMoved,
    ContactStarted,
    ContactEnded,
    AttackResolved,
    DamageDealt,
    UnitKilled,
    MoraleChanged,
    RoutStarted,
    RetreatCompleted,
    BattleEnded,
}

/// <summary>
/// State carried by a unit in a transcript keyframe or final result.
/// Routed and retreated are deliberately not equivalent to dead.
/// </summary>
public enum BattleUnitState
{
    Active,
    Dead,
    Routed,
    Retreated,
}

/// <summary>
/// Formation state carried by a presentation keyframe.
/// </summary>
public enum BattleFormationState
{
    Advancing,
    Engaged,
    Routing,
    Retreated,
    Defeated,
}

/// <summary>
/// Result classification for the bounded M1 resolver.
/// </summary>
public enum BattleResultType
{
    SideAWin,
    SideBWin,
    Draw,
    NonTerminalFailure,
}

/// <summary>
/// Identity and fixed-format metadata for transcript v1.
/// </summary>
public sealed record BattleTranscriptHeader
{
    public const string TranscriptIdentity = "warwrought.battle-transcript.m1";
    public const int CurrentVersion = 1;

    public BattleTranscriptHeader(
        BattleId battleId,
        SimulationVersion simulationVersion,
        ulong seed,
        string canonicalInputDigest,
        int ticksPerSecond,
        int keyframeIntervalTicks)
    {
        if (!battleId.IsValid)
        {
            throw new ArgumentException("A transcript header requires a valid battle ID.", nameof(battleId));
        }

        ArgumentNullException.ThrowIfNull(canonicalInputDigest);
        if (string.IsNullOrWhiteSpace(canonicalInputDigest))
        {
            throw new ArgumentException("A transcript header requires the committed input digest.", nameof(canonicalInputDigest));
        }

        if (ticksPerSecond <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ticksPerSecond), ticksPerSecond, "Transcript tick frequency must be positive.");
        }

        if (keyframeIntervalTicks <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(keyframeIntervalTicks), keyframeIntervalTicks, "Transcript keyframe interval must be positive.");
        }

        Identity = TranscriptIdentity;
        Version = CurrentVersion;
        BattleId = battleId;
        SimulationVersion = simulationVersion;
        Seed = seed;
        CanonicalInputDigest = canonicalInputDigest;
        TicksPerSecond = ticksPerSecond;
        KeyframeIntervalTicks = keyframeIntervalTicks;
    }

    public string Identity { get; }

    public int Version { get; }

    public BattleId BattleId { get; }

    public SimulationVersion SimulationVersion { get; }

    public ulong Seed { get; }

    public string CanonicalInputDigest { get; }

    public int TicksPerSecond { get; }

    public int KeyframeIntervalTicks { get; }
}

/// <summary>
/// One ordered authoritative semantic event. Optional IDs identify the event's source,
/// target, or related formation. The meaning of Amount/Value/SecondaryValue is supplied by
/// the event family and reason string rather than hidden in presentation code.
/// </summary>
public sealed record BattleSemanticEvent
{
    public BattleSemanticEvent(
        int sequence,
        SimulationTick tick,
        BattleEventType type,
        BattleSideId? sideId = null,
        BattleSquadId? squadId = null,
        BattleSideId? otherSideId = null,
        BattleSquadId? otherSquadId = null,
        BattleUnitId? sourceUnitId = null,
        BattleUnitId? targetUnitId = null,
        int amount = 0,
        int value = 0,
        int secondaryValue = 0,
        string? reason = null,
        SimPosition? position = null,
        FormationFacing? facing = null,
        bool isTerminal = false)
    {
        if (sequence < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sequence), sequence, "Transcript event sequence cannot be negative.");
        }

        Sequence = sequence;
        Tick = tick;
        Type = type;
        SideId = sideId;
        SquadId = squadId;
        OtherSideId = otherSideId;
        OtherSquadId = otherSquadId;
        SourceUnitId = sourceUnitId;
        TargetUnitId = targetUnitId;
        Amount = amount;
        Value = value;
        SecondaryValue = secondaryValue;
        Reason = reason ?? string.Empty;
        Position = position;
        Facing = facing;
        IsTerminal = isTerminal;
    }

    public int Sequence { get; }

    public SimulationTick Tick { get; }

    public BattleEventType Type { get; }

    public BattleSideId? SideId { get; }

    public BattleSquadId? SquadId { get; }

    public BattleSideId? OtherSideId { get; }

    public BattleSquadId? OtherSquadId { get; }

    public BattleUnitId? SourceUnitId { get; }

    public BattleUnitId? TargetUnitId { get; }

    /// <summary>
    /// Convenience alias for consumers that treat the event's target as its subject.
    /// </summary>
    public BattleUnitId? SubjectUnitId => TargetUnitId ?? SourceUnitId;

    public int Amount { get; }

    public int Value { get; }

    public int SecondaryValue { get; }

    public string Reason { get; }

    public SimPosition? Position { get; }

    public FormationFacing? Facing { get; }

    public bool IsTerminal { get; }
}

/// <summary>
/// One unit's authoritative state in a formation keyframe.
/// Dead members retain their last authoritative position and use -1 for slot fields.
/// </summary>
public sealed record BattleMemberKeyframe
{
    public BattleMemberKeyframe(
        BattleUnitId unitId,
        int slotIndex,
        int rank,
        int file,
        SimPosition position,
        BattleUnitState state)
    {
        if (!unitId.IsValid)
        {
            throw new ArgumentException("A keyframe member requires a valid unit ID.", nameof(unitId));
        }

        if (state != BattleUnitState.Dead && (slotIndex < 0 || rank < 0 || file < 0))
        {
            throw new ArgumentOutOfRangeException(nameof(slotIndex), "Active, routed, and retreated members require a valid formation slot.");
        }

        if (state == BattleUnitState.Dead && (slotIndex >= 0 || rank >= 0 || file >= 0))
        {
            throw new ArgumentOutOfRangeException(nameof(slotIndex), "Dead members must not occupy an active formation slot.");
        }

        UnitId = unitId;
        SlotIndex = slotIndex;
        Rank = rank;
        File = file;
        Position = position;
        State = state;
    }

    public BattleUnitId UnitId { get; }

    public int SlotIndex { get; }

    public int Rank { get; }

    public int File { get; }

    public SimPosition Position { get; }

    public BattleUnitState State { get; }

    public bool IsAlive => State != BattleUnitState.Dead;
}

/// <summary>
/// Periodic formation state sufficient for M1 playback to present movement, facing,
/// reform, death/remains, and rout without re-running combat decisions.
/// </summary>
public sealed record BattleFormationKeyframe
{
    public BattleFormationKeyframe(
        SimulationTick tick,
        BattleSideId sideId,
        BattleSquadId squadId,
        SimPosition anchor,
        FormationFacing facing,
        BattleFormationState state,
        int morale,
        IEnumerable<BattleMemberKeyframe> members)
    {
        if (!sideId.IsValid)
        {
            throw new ArgumentException("A keyframe requires a valid side ID.", nameof(sideId));
        }

        if (!squadId.IsValid)
        {
            throw new ArgumentException("A keyframe requires a valid squad ID.", nameof(squadId));
        }

        ArgumentNullException.ThrowIfNull(members);
        Tick = tick;
        SideId = sideId;
        SquadId = squadId;
        Anchor = anchor;
        Facing = facing;
        State = state;
        Morale = morale;
        Members = Array.AsReadOnly(members.ToArray());
    }

    public SimulationTick Tick { get; }

    public BattleSideId SideId { get; }

    public BattleSquadId SquadId { get; }

    public SimPosition Anchor { get; }

    public FormationFacing Facing { get; }

    public BattleFormationState State { get; }

    public int Morale { get; }

    public IReadOnlyList<BattleMemberKeyframe> Members { get; }

    public IReadOnlyList<BattleMemberKeyframe> MemberStates => Members;
}

/// <summary>
/// Immutable transcript v1. Events and keyframes are copied into ordered read-only lists,
/// then hashed through the explicit transcript digest contract.
/// </summary>
public sealed record BattleTranscript
{
    public const string CanonicalDigestDomain = "warwrought.battle-transcript.m1.v1";

    public BattleTranscript(
        BattleTranscriptHeader header,
        IEnumerable<BattleSemanticEvent> events,
        IEnumerable<BattleFormationKeyframe> keyframes)
    {
        ArgumentNullException.ThrowIfNull(header);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(keyframes);

        Header = header;
        Events = Array.AsReadOnly(events.ToArray());
        Keyframes = Array.AsReadOnly(keyframes.ToArray());
        ValidateOrdering();
        CanonicalDigest = ComputeCanonicalDigest();
    }

    public BattleTranscriptHeader Header { get; }

    public IReadOnlyList<BattleSemanticEvent> Events { get; }

    public IReadOnlyList<BattleSemanticEvent> SemanticEvents => Events;

    public IReadOnlyList<BattleFormationKeyframe> Keyframes { get; }

    public int EventCount => Events.Count;

    public int KeyframeCount => Keyframes.Count;

    public string CanonicalDigest { get; }

    public string Digest => CanonicalDigest;

    private void ValidateOrdering()
    {
        for (var index = 0; index < Events.Count; index++)
        {
            if (Events[index] is null)
            {
                throw new ArgumentException($"Transcript event {index} cannot be null.", nameof(Events));
            }

            if (Events[index].Sequence != index)
            {
                throw new ArgumentException($"Transcript event sequence {Events[index].Sequence} does not match ordered index {index}.", nameof(Events));
            }

            if (index > 0 && Events[index - 1].Tick > Events[index].Tick)
            {
                throw new ArgumentException("Transcript events must be ordered by non-decreasing authoritative tick.", nameof(Events));
            }
        }

        for (var index = 0; index < Keyframes.Count; index++)
        {
            if (Keyframes[index] is null)
            {
                throw new ArgumentException($"Transcript keyframe {index} cannot be null.", nameof(Keyframes));
            }

            if (index > 0 && Keyframes[index - 1].Tick > Keyframes[index].Tick)
            {
                throw new ArgumentException("Transcript keyframes must be ordered by non-decreasing authoritative tick.", nameof(Keyframes));
            }
        }
    }

    private string ComputeCanonicalDigest()
    {
        using var writer = new CanonicalDigestWriter();
        writer.WriteString(CanonicalDigestDomain);
        writer.WriteString(Header.Identity);
        writer.WriteInt32(Header.Version);
        writer.WriteString(Header.BattleId.Value);
        writer.WriteSimulationVersion(Header.SimulationVersion);
        writer.WriteUInt64(Header.Seed);
        writer.WriteString(Header.CanonicalInputDigest);
        writer.WriteInt32(Header.TicksPerSecond);
        writer.WriteInt32(Header.KeyframeIntervalTicks);

        writer.WriteInt32(Events.Count);
        for (var index = 0; index < Events.Count; index++)
        {
            var @event = Events[index];
            writer.WriteInt32(@event.Sequence);
            writer.WriteSimulationTick(@event.Tick);
            writer.WriteInt32((int)@event.Type);
            WriteOptionalId(writer, @event.SideId?.Value);
            WriteOptionalId(writer, @event.SquadId?.Value);
            WriteOptionalId(writer, @event.OtherSideId?.Value);
            WriteOptionalId(writer, @event.OtherSquadId?.Value);
            WriteOptionalId(writer, @event.SourceUnitId?.Value);
            WriteOptionalId(writer, @event.TargetUnitId?.Value);
            writer.WriteInt32(@event.Amount);
            writer.WriteInt32(@event.Value);
            writer.WriteInt32(@event.SecondaryValue);
            writer.WriteString(@event.Reason);
            WriteOptionalPosition(writer, @event.Position);
            WriteOptionalFacing(writer, @event.Facing);
            writer.WriteBoolean(@event.IsTerminal);
        }

        writer.WriteInt32(Keyframes.Count);
        for (var index = 0; index < Keyframes.Count; index++)
        {
            var keyframe = Keyframes[index];
            writer.WriteSimulationTick(keyframe.Tick);
            writer.WriteString(keyframe.SideId.Value);
            writer.WriteString(keyframe.SquadId.Value);
            writer.WriteSimPosition(keyframe.Anchor);
            writer.WriteInt32((int)keyframe.Facing);
            writer.WriteInt32((int)keyframe.State);
            writer.WriteInt32(keyframe.Morale);
            writer.WriteInt32(keyframe.Members.Count);

            for (var memberIndex = 0; memberIndex < keyframe.Members.Count; memberIndex++)
            {
                var member = keyframe.Members[memberIndex];
                writer.WriteString(member.UnitId.Value);
                writer.WriteInt32(member.SlotIndex);
                writer.WriteInt32(member.Rank);
                writer.WriteInt32(member.File);
                writer.WriteSimPosition(member.Position);
                writer.WriteInt32((int)member.State);
                writer.WriteBoolean(member.IsAlive);
            }
        }

        return writer.ComputeSha256Hex();
    }

    internal static void WriteOptionalId(CanonicalDigestWriter writer, string? value)
    {
        writer.WriteBoolean(value is not null);
        if (value is not null)
        {
            writer.WriteString(value);
        }
    }

    internal static void WriteOptionalPosition(CanonicalDigestWriter writer, SimPosition? value)
    {
        writer.WriteBoolean(value.HasValue);
        if (value.HasValue)
        {
            writer.WriteSimPosition(value.Value);
        }
    }

    internal static void WriteOptionalFacing(CanonicalDigestWriter writer, FormationFacing? value)
    {
        writer.WriteBoolean(value.HasValue);
        if (value.HasValue)
        {
            writer.WriteInt32((int)value.Value);
        }
    }
}
