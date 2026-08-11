using System;
using System.Collections.Generic;
using System.Linq;
using Warwrought.Battle.Model;
using Warwrought.Core;

namespace Warwrought.Battle.Transcript;

/// <summary>
/// Ordered final record for a unit that is not dead when resolution ends.
/// </summary>
public sealed record BattleSurvivorRecord
{
    public BattleSurvivorRecord(
        BattleUnitId unitId,
        BattleSideId sideId,
        BattleSquadId squadId,
        BattleUnitState state,
        int remainingHealth,
        int finalMorale)
    {
        if (!unitId.IsValid)
        {
            throw new ArgumentException("A survivor record requires a valid unit ID.", nameof(unitId));
        }

        if (!sideId.IsValid)
        {
            throw new ArgumentException("A survivor record requires a valid side ID.", nameof(sideId));
        }

        if (!squadId.IsValid)
        {
            throw new ArgumentException("A survivor record requires a valid squad ID.", nameof(squadId));
        }

        if (state == BattleUnitState.Dead)
        {
            throw new ArgumentException("Dead units belong in casualty records, not survivor records.", nameof(state));
        }

        UnitId = unitId;
        SideId = sideId;
        SquadId = squadId;
        State = state;
        RemainingHealth = remainingHealth;
        FinalMorale = finalMorale;
    }

    public BattleUnitId UnitId { get; }

    public BattleSideId SideId { get; }

    public BattleSquadId SquadId { get; }

    public BattleUnitState State { get; }

    public int RemainingHealth { get; }

    public int FinalMorale { get; }

    public bool IsRouted => State is BattleUnitState.Routed or BattleUnitState.Retreated;

    public bool IsRetreated => State == BattleUnitState.Retreated;
}

/// <summary>
/// Ordered final record for a dead unit.
/// </summary>
public sealed record BattleCasualtyRecord
{
    public BattleCasualtyRecord(
        BattleUnitId unitId,
        BattleSideId sideId,
        BattleSquadId squadId,
        SimulationTick killedAtTick,
        BattleUnitId? killedByUnitId)
    {
        if (!unitId.IsValid)
        {
            throw new ArgumentException("A casualty record requires a valid unit ID.", nameof(unitId));
        }

        if (!sideId.IsValid)
        {
            throw new ArgumentException("A casualty record requires a valid side ID.", nameof(sideId));
        }

        if (!squadId.IsValid)
        {
            throw new ArgumentException("A casualty record requires a valid squad ID.", nameof(squadId));
        }

        UnitId = unitId;
        SideId = sideId;
        SquadId = squadId;
        KilledAtTick = killedAtTick;
        KilledByUnitId = killedByUnitId;
    }

    public BattleUnitId UnitId { get; }

    public BattleSideId SideId { get; }

    public BattleSquadId SquadId { get; }

    public SimulationTick KilledAtTick { get; }

    public BattleUnitId? KilledByUnitId { get; }
}

/// <summary>
/// Authoritative campaign-facing result plus the canonical digest of the result/transcript
/// contract. M1 has no commander model, so no commander status is fabricated here.
/// </summary>
public sealed record BattleResult
{
    public const string CanonicalDigestDomain = "warwrought.battle-resolution.m1.v1";

    internal BattleResult(
        BattleId battleId,
        SimulationVersion simulationVersion,
        ulong seed,
        BattleResultType resultType,
        BattleSideId? winnerSideId,
        SimulationTick terminalTick,
        bool isTerminal,
        string diagnosticReason,
        IEnumerable<BattleSurvivorRecord> survivors,
        IEnumerable<BattleCasualtyRecord> casualties,
        string transcriptDigest)
    {
        ArgumentNullException.ThrowIfNull(diagnosticReason);
        ArgumentNullException.ThrowIfNull(survivors);
        ArgumentNullException.ThrowIfNull(casualties);
        ArgumentNullException.ThrowIfNull(transcriptDigest);

        BattleId = battleId;
        SimulationVersion = simulationVersion;
        Seed = seed;
        ResultType = resultType;
        WinnerSideId = winnerSideId;
        TerminalTick = terminalTick;
        IsTerminal = isTerminal;
        DiagnosticReason = diagnosticReason;
        Survivors = Array.AsReadOnly(survivors.ToArray());
        Casualties = Array.AsReadOnly(casualties.ToArray());
        TranscriptDigest = transcriptDigest;

        ValidateClassification();
        CanonicalDigest = ComputeCanonicalDigest();
    }

    public BattleId BattleId { get; }

    public SimulationVersion SimulationVersion { get; }

    public ulong Seed { get; }

    public BattleResultType ResultType { get; }

    public BattleSideId? WinnerSideId { get; }

    public BattleSideId? Winner => WinnerSideId;

    public SimulationTick TerminalTick { get; }

    public bool IsTerminal { get; }

    public bool IsFailure => ResultType == BattleResultType.NonTerminalFailure;

    public string DiagnosticReason { get; }

    public IReadOnlyList<BattleSurvivorRecord> Survivors { get; }

    public IReadOnlyList<BattleSurvivorRecord> OrderedSurvivors => Survivors;

    public IReadOnlyList<BattleCasualtyRecord> Casualties { get; }

    public IReadOnlyList<BattleCasualtyRecord> OrderedCasualties => Casualties;

    /// <summary>
    /// Routed members remain survivors until they have reached their retreat destination;
    /// this list includes both routed and completed-retreat states.
    /// </summary>
    public IReadOnlyList<BattleSurvivorRecord> RoutedUnits => Survivors.Where(record => record.IsRouted).ToArray();

    public IReadOnlyList<BattleSurvivorRecord> OrderedRoutedUnits => RoutedUnits;

    public IReadOnlyList<BattleSurvivorRecord> RetreatedUnits => Survivors.Where(record => record.IsRetreated).ToArray();

    public IReadOnlyList<BattleSurvivorRecord> OrderedRetreatedUnits => RetreatedUnits;

    /// <summary>
    /// M1 contains no commander records. The property remains an explicit empty result rather
    /// than inventing commander mechanics before the model requires them.
    /// </summary>
    public IReadOnlyList<string> CommanderStatuses { get; } = Array.Empty<string>();

    public string TranscriptDigest { get; }

    public string CanonicalDigest { get; }

    public string Digest => CanonicalDigest;

    private void ValidateClassification()
    {
        if (IsTerminal && ResultType == BattleResultType.NonTerminalFailure)
        {
            throw new ArgumentException("A safety-cap failure cannot be marked terminal.", nameof(IsTerminal));
        }

        if (!IsTerminal && ResultType != BattleResultType.NonTerminalFailure)
        {
            throw new ArgumentException("A non-terminal result must use the explicit failure classification.", nameof(ResultType));
        }

        if (ResultType is BattleResultType.SideAWin or BattleResultType.SideBWin)
        {
            if (!WinnerSideId.HasValue || !WinnerSideId.Value.IsValid)
            {
                throw new ArgumentException("A side win result requires a valid winner side ID.", nameof(WinnerSideId));
            }
        }
        else if (WinnerSideId.HasValue)
        {
            throw new ArgumentException("Draw and non-terminal results must not declare a winner.", nameof(WinnerSideId));
        }
    }

    private string ComputeCanonicalDigest()
    {
        using var writer = new CanonicalDigestWriter();
        writer.WriteString(CanonicalDigestDomain);
        writer.WriteString(BattleId.Value);
        writer.WriteSimulationVersion(SimulationVersion);
        writer.WriteUInt64(Seed);
        writer.WriteInt32((int)ResultType);
        BattleTranscript.WriteOptionalId(writer, WinnerSideId?.Value);
        writer.WriteSimulationTick(TerminalTick);
        writer.WriteBoolean(IsTerminal);
        writer.WriteString(DiagnosticReason);
        writer.WriteString(TranscriptDigest);

        writer.WriteInt32(Survivors.Count);
        for (var index = 0; index < Survivors.Count; index++)
        {
            var survivor = Survivors[index];
            writer.WriteString(survivor.UnitId.Value);
            writer.WriteString(survivor.SideId.Value);
            writer.WriteString(survivor.SquadId.Value);
            writer.WriteInt32((int)survivor.State);
            writer.WriteInt32(survivor.RemainingHealth);
            writer.WriteInt32(survivor.FinalMorale);
        }

        writer.WriteInt32(Casualties.Count);
        for (var index = 0; index < Casualties.Count; index++)
        {
            var casualty = Casualties[index];
            writer.WriteString(casualty.UnitId.Value);
            writer.WriteString(casualty.SideId.Value);
            writer.WriteString(casualty.SquadId.Value);
            writer.WriteSimulationTick(casualty.KilledAtTick);
            BattleTranscript.WriteOptionalId(writer, casualty.KilledByUnitId?.Value);
        }

        return writer.ComputeSha256Hex();
    }
}

/// <summary>
/// The one authoritative resolver output. Watch and skip consumers receive this same pair;
/// neither path is allowed to reconstruct a result from presentation state.
/// </summary>
public sealed record BattleResolution
{
    public BattleResolution(BattleResult result, BattleTranscript transcript)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(transcript);
        if (!StringComparer.Ordinal.Equals(result.TranscriptDigest, transcript.CanonicalDigest))
        {
            throw new ArgumentException("The result and transcript do not belong to the same canonical resolution.", nameof(result));
        }

        Result = result;
        Transcript = transcript;
    }

    public BattleResult Result { get; }

    public BattleTranscript Transcript { get; }

    public string CanonicalDigest => Result.CanonicalDigest;

    public string Digest => CanonicalDigest;
}
