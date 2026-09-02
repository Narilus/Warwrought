using System;
using System.Collections.Generic;
using Warwrought.Battle.Model;
using Warwrought.Battle.Simulation;
using Warwrought.Battle.Transcript;
using Warwrought.Core;

namespace Warwrought.Presentation.Battle;

/// <summary>
/// Creates deterministic presentation-only transcript sources for the M2.3 scale stress cases.
/// This type deliberately does not create a BattleDefinition or call any authoritative resolver:
/// it only supplies ordered records to the existing production playback controller.
/// </summary>
public static class BattleScalePresentationStressFactory
{
    public const string SourceClassification = "presentation.synthetic-deterministic";
    public const string Presentation300Scenario = "battlescalelab.m2.300v300";
    public const string Presentation500Scenario = "battlescalelab.m2.500v500";

    private const int DeploymentHalfDistanceUnits = 20_000;
    private const int FileSpacingUnits = 1_000;
    private const int RankSpacingUnits = 1_000;
    private const int TerminalTick = 480;
    private const int ContactTick = 150;
    private const int DeathTick = 240;

    public static BattleScalePresentationStressData Create300v300()
    {
        return Create(
            Presentation300Scenario,
            battleId: "battle.m2.scale.300v300.presentation",
            seed: 83_003,
            unitsPerSide: 300,
            fileCount: 20,
            rankCount: 15);
    }

    public static BattleScalePresentationStressData Create500v500()
    {
        return Create(
            Presentation500Scenario,
            battleId: "battle.m2.scale.500v500.presentation",
            seed: 83_005,
            unitsPerSide: 500,
            fileCount: 25,
            rankCount: 20);
    }

    private static BattleScalePresentationStressData Create(
        string scenarioId,
        string battleId,
        ulong seed,
        int unitsPerSide,
        int fileCount,
        int rankCount)
    {
        var sideAId = new BattleSideId("side.a");
        var sideBId = new BattleSideId("side.b");
        var squadAId = new BattleSquadId("squad.scale.synthetic.a");
        var squadBId = new BattleSquadId("squad.scale.synthetic.b");
        var sideAUnitIds = CreateUnitIds("unit.synthetic.a", unitsPerSide);
        var sideBUnitIds = CreateUnitIds("unit.synthetic.b", unitsPerSide);
        var sideAInitialAnchor = new SimPosition(0, -DeploymentHalfDistanceUnits);
        var sideBInitialAnchor = new SimPosition(0, DeploymentHalfDistanceUnits);
        var sideASlots = RectangularFormationLayout.GenerateSlots(
            sideAInitialAnchor,
            FormationFacing.South,
            fileCount,
            rankCount,
            FileSpacingUnits,
            RankSpacingUnits,
            sideAUnitIds);
        var sideBSlots = RectangularFormationLayout.GenerateSlots(
            sideBInitialAnchor,
            FormationFacing.North,
            fileCount,
            rankCount,
            FileSpacingUnits,
            RankSpacingUnits,
            sideBUnitIds);

        var sourceIdentityDigest = ComputeSourceIdentityDigest(
            scenarioId,
            battleId,
            seed,
            unitsPerSide,
            fileCount,
            rankCount);
        var header = new BattleTranscriptHeader(
            new BattleId(battleId),
            M1BattleSettings.SimulationVersion,
            seed,
            sourceIdentityDigest,
            M1BattleSettings.TicksPerSecond,
            M1BattleSettings.TranscriptKeyframeIntervalTicks);
        var events = CreateEvents(
            sideAId,
            sideBId,
            squadAId,
            squadBId,
            sideAUnitIds,
            sideBUnitIds);
        var keyframes = CreateKeyframes(
            sideAId,
            sideBId,
            squadAId,
            squadBId,
            sideAUnitIds,
            sideBUnitIds,
            sideAInitialAnchor,
            sideBInitialAnchor,
            sideASlots,
            sideBSlots);
        var transcript = new BattleTranscript(header, events, keyframes);
        var casualties = CreateCasualties(sideAId, sideBId, squadAId, squadBId, sideAUnitIds, sideBUnitIds);
        var survivors = CreateSurvivors(sideAId, sideBId, squadAId, squadBId, sideAUnitIds, sideBUnitIds);
        var result = new BattleResult(
            new BattleId(battleId),
            M1BattleSettings.SimulationVersion,
            seed,
            BattleResultType.NonTerminalFailure,
            winnerSideId: null,
            new SimulationTick(TerminalTick),
            isTerminal: false,
            diagnosticReason: "presentation.synthetic-deterministic.stress-transcript-complete",
            survivors,
            casualties,
            transcript.CanonicalDigest);
        var resolution = new BattleResolution(result, transcript);
        return new BattleScalePresentationStressData(
            scenarioId,
            SourceClassification,
            sourceIdentityDigest,
            resolution,
            unitsPerSide,
            fileCount,
            rankCount);
    }

    private static List<BattleSemanticEvent> CreateEvents(
        BattleSideId sideAId,
        BattleSideId sideBId,
        BattleSquadId squadAId,
        BattleSquadId squadBId,
        BattleUnitId[] sideAUnitIds,
        BattleUnitId[] sideBUnitIds)
    {
        var events = new List<BattleSemanticEvent>();
        AddEvent(events, SimulationTick.Zero, BattleEventType.BattleStarted, reason: "presentation.synthetic-deterministic.start");
        AddEvent(
            events,
            new SimulationTick(120),
            BattleEventType.FormationMoved,
            sideId: sideAId,
            squadId: squadAId,
            position: new SimPosition(0, -4_000),
            facing: FormationFacing.South,
            reason: "presentation.synthetic-deterministic.formation-advance");
        AddEvent(
            events,
            new SimulationTick(120),
            BattleEventType.FormationMoved,
            sideId: sideBId,
            squadId: squadBId,
            position: new SimPosition(0, 4_000),
            facing: FormationFacing.North,
            reason: "presentation.synthetic-deterministic.formation-advance");
        AddEvent(
            events,
            new SimulationTick(ContactTick),
            BattleEventType.ContactStarted,
            sideId: sideAId,
            squadId: squadAId,
            otherSideId: sideBId,
            otherSquadId: squadBId,
            position: new SimPosition(0, 0),
            reason: "presentation.synthetic-deterministic.contact-marker");

        for (var index = 0; index < 12; index++)
        {
            var tick = new SimulationTick(170 + index * 4L);
            AddEvent(
                events,
                tick,
                BattleEventType.AttackResolved,
                sideId: sideAId,
                squadId: squadAId,
                otherSideId: sideBId,
                otherSquadId: squadBId,
                sourceUnitId: sideAUnitIds[index],
                targetUnitId: sideBUnitIds[index],
                reason: "presentation.synthetic-deterministic.attack-cue");
            AddEvent(
                events,
                tick,
                BattleEventType.DamageDealt,
                sideId: sideAId,
                squadId: squadAId,
                otherSideId: sideBId,
                otherSquadId: squadBId,
                sourceUnitId: sideAUnitIds[index],
                targetUnitId: sideBUnitIds[index],
                amount: 1,
                reason: "presentation.synthetic-deterministic.damage-cue");
        }

        for (var index = 0; index < 30; index++)
        {
            AddEvent(
                events,
                new SimulationTick(DeathTick),
                BattleEventType.UnitKilled,
                sideId: sideBId,
                squadId: squadBId,
                sourceUnitId: sideAUnitIds[index % sideAUnitIds.Length],
                targetUnitId: sideBUnitIds[index],
                reason: "presentation.synthetic-deterministic.death-cue");
        }

        AddEvent(
            events,
            new SimulationTick(280),
            BattleEventType.ContactEnded,
            sideId: sideAId,
            squadId: squadAId,
            otherSideId: sideBId,
            otherSquadId: squadBId,
            reason: "presentation.synthetic-deterministic.contact-end-cue");
        AddEvent(
            events,
            new SimulationTick(330),
            BattleEventType.RoutStarted,
            sideId: sideBId,
            squadId: squadBId,
            position: new SimPosition(0, 7_000),
            facing: FormationFacing.North,
            reason: "presentation.synthetic-deterministic.rout-cue");
        AddEvent(
            events,
            new SimulationTick(450),
            BattleEventType.RetreatCompleted,
            sideId: sideBId,
            squadId: squadBId,
            position: new SimPosition(0, 10_000),
            facing: FormationFacing.North,
            reason: "presentation.synthetic-deterministic.retreat-cue");
        AddEvent(
            events,
            new SimulationTick(TerminalTick),
            BattleEventType.BattleEnded,
            position: new SimPosition(0, 0),
            reason: "presentation.synthetic-deterministic.observation-complete");
        return events;
    }

    private static List<BattleFormationKeyframe> CreateKeyframes(
        BattleSideId sideAId,
        BattleSideId sideBId,
        BattleSquadId squadAId,
        BattleSquadId squadBId,
        BattleUnitId[] sideAUnitIds,
        BattleUnitId[] sideBUnitIds,
        SimPosition sideAInitialAnchor,
        SimPosition sideBInitialAnchor,
        IReadOnlyList<FormationSlot> sideASlots,
        IReadOnlyList<FormationSlot> sideBSlots)
    {
        var keyframes = new List<BattleFormationKeyframe>();
        AddKeyframePair(keyframes, new SimulationTick(0), BattleFormationState.Advancing, BattleFormationState.Advancing, new SimPosition(0, -20_000), new SimPosition(0, 20_000));
        AddKeyframePair(keyframes, new SimulationTick(120), BattleFormationState.Advancing, BattleFormationState.Advancing, new SimPosition(0, -4_000), new SimPosition(0, 4_000));
        AddKeyframePair(keyframes, new SimulationTick(240), BattleFormationState.Engaged, BattleFormationState.Engaged, new SimPosition(0, -4_000), new SimPosition(0, 4_000));
        AddKeyframePair(keyframes, new SimulationTick(360), BattleFormationState.Engaged, BattleFormationState.Routing, new SimPosition(0, -1_500), new SimPosition(0, 7_000));
        AddKeyframePair(keyframes, new SimulationTick(480), BattleFormationState.Engaged, BattleFormationState.Retreated, new SimPosition(0, 0), new SimPosition(0, 10_000));
        return keyframes;

        void AddKeyframePair(
            List<BattleFormationKeyframe> destination,
            SimulationTick tick,
            BattleFormationState sideAState,
            BattleFormationState sideBState,
            SimPosition sideAAnchor,
            SimPosition sideBAnchor)
        {
            destination.Add(CreateKeyframe(
                tick,
                sideAId,
                squadAId,
                sideAAnchor,
                FormationFacing.South,
                sideAState,
                sideAUnitIds,
                sideAInitialAnchor,
                sideASlots));
            destination.Add(CreateKeyframe(
                tick,
                sideBId,
                squadBId,
                sideBAnchor,
                FormationFacing.North,
                sideBState,
                sideBUnitIds,
                sideBInitialAnchor,
                sideBSlots));
        }
    }

    private static BattleFormationKeyframe CreateKeyframe(
        SimulationTick tick,
        BattleSideId sideId,
        BattleSquadId squadId,
        SimPosition anchor,
        FormationFacing facing,
        BattleFormationState formationState,
        BattleUnitId[] unitIds,
        SimPosition initialAnchor,
        IReadOnlyList<FormationSlot> slots)
    {
        var members = new List<BattleMemberKeyframe>(unitIds.Length);
        var isSideB = sideId.Value == "side.b";
        for (var index = 0; index < unitIds.Length; index++)
        {
            var isCasualty = isSideB && index < 30 && tick.Value >= DeathTick;
            var position = new SimPosition(
                checked(anchor.X + slots[index].Position.X - initialAnchor.X),
                checked(anchor.Z + slots[index].Position.Z - initialAnchor.Z));
            if (isCasualty)
            {
                members.Add(new BattleMemberKeyframe(
                    unitIds[index],
                    slotIndex: -1,
                    rank: -1,
                    file: -1,
                    position,
                    BattleUnitState.Dead));
                continue;
            }

            var unitState = isSideB && tick.Value >= 480
                ? BattleUnitState.Retreated
                : isSideB && tick.Value >= 360
                    ? BattleUnitState.Routed
                    : BattleUnitState.Active;
            members.Add(new BattleMemberKeyframe(
                unitIds[index],
                slots[index].SlotIndex,
                slots[index].Rank,
                slots[index].File,
                position,
                unitState));
        }

        return new BattleFormationKeyframe(
            tick,
            sideId,
            squadId,
            anchor,
            facing,
            formationState,
            morale: isSideB && tick.Value >= 360 ? 30 : 100,
            members);
    }

    private static List<BattleSurvivorRecord> CreateSurvivors(
        BattleSideId sideAId,
        BattleSideId sideBId,
        BattleSquadId squadAId,
        BattleSquadId squadBId,
        BattleUnitId[] sideAUnitIds,
        BattleUnitId[] sideBUnitIds)
    {
        var survivors = new List<BattleSurvivorRecord>(sideAUnitIds.Length + sideBUnitIds.Length - 30);
        for (var index = 0; index < sideAUnitIds.Length; index++)
        {
            survivors.Add(new BattleSurvivorRecord(
                sideAUnitIds[index],
                sideAId,
                squadAId,
                BattleUnitState.Active,
                remainingHealth: 100,
                finalMorale: 100));
        }

        for (var index = 30; index < sideBUnitIds.Length; index++)
        {
            survivors.Add(new BattleSurvivorRecord(
                sideBUnitIds[index],
                sideBId,
                squadBId,
                BattleUnitState.Retreated,
                remainingHealth: 100,
                finalMorale: 30));
        }

        return survivors;
    }

    private static List<BattleCasualtyRecord> CreateCasualties(
        BattleSideId sideAId,
        BattleSideId sideBId,
        BattleSquadId squadAId,
        BattleSquadId squadBId,
        BattleUnitId[] sideAUnitIds,
        BattleUnitId[] sideBUnitIds)
    {
        var casualties = new List<BattleCasualtyRecord>(30);
        for (var index = 0; index < 30; index++)
        {
            casualties.Add(new BattleCasualtyRecord(
                sideBUnitIds[index],
                sideBId,
                squadBId,
                new SimulationTick(DeathTick),
                sideAUnitIds[index % sideAUnitIds.Length]));
        }

        return casualties;
    }

    private static void AddEvent(
        List<BattleSemanticEvent> events,
        SimulationTick tick,
        BattleEventType type,
        BattleSideId? sideId = null,
        BattleSquadId? squadId = null,
        BattleSideId? otherSideId = null,
        BattleSquadId? otherSquadId = null,
        BattleUnitId? sourceUnitId = null,
        BattleUnitId? targetUnitId = null,
        int amount = 0,
        string? reason = null,
        SimPosition? position = null,
        FormationFacing? facing = null)
    {
        events.Add(new BattleSemanticEvent(
            events.Count,
            tick,
            type,
            sideId,
            squadId,
            otherSideId,
            otherSquadId,
            sourceUnitId,
            targetUnitId,
            amount,
            reason: reason,
            position: position,
            facing: facing));
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

    private static string ComputeSourceIdentityDigest(
        string scenarioId,
        string battleId,
        ulong seed,
        int unitsPerSide,
        int fileCount,
        int rankCount)
    {
        using var writer = new CanonicalDigestWriter();
        writer.WriteString("warwrought.scale.presentation.synthetic.v1");
        writer.WriteString(SourceClassification);
        writer.WriteString(scenarioId);
        writer.WriteString(battleId);
        writer.WriteUInt64(seed);
        writer.WriteInt32(unitsPerSide);
        writer.WriteInt32(fileCount);
        writer.WriteInt32(rankCount);
        writer.WriteInt32(FileSpacingUnits);
        writer.WriteInt32(RankSpacingUnits);
        writer.WriteInt32(TerminalTick);
        writer.WriteInt32(ContactTick);
        writer.WriteInt32(DeathTick);
        return writer.ComputeSha256Hex();
    }
}

/// <summary>
/// Immutable metadata plus one synthetic resolution used only to load the existing production
/// presentation path for 300v300/500v500 stress observation.
/// </summary>
public sealed class BattleScalePresentationStressData
{
    internal BattleScalePresentationStressData(
        string scenarioId,
        string sourceClassification,
        string sourceIdentityDigest,
        BattleResolution resolution,
        int requestedUnitsPerSide,
        int formationFileCount,
        int formationRankCount)
    {
        ScenarioId = scenarioId;
        SourceClassification = sourceClassification;
        SourceIdentityDigest = sourceIdentityDigest;
        Resolution = resolution;
        RequestedUnitsPerSide = requestedUnitsPerSide;
        FormationFileCount = formationFileCount;
        FormationRankCount = formationRankCount;
    }

    public string ScenarioId { get; }

    public string SourceClassification { get; }

    public string SourceIdentityDigest { get; }

    public BattleResolution Resolution { get; }

    public int RequestedUnitsPerSide { get; }

    public int ExpectedTotalUnitCount => RequestedUnitsPerSide * 2;

    public int FormationFileCount { get; }

    public int FormationRankCount { get; }
}
