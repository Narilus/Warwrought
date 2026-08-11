using System;
using System.Collections.Generic;
using System.Linq;
using Warwrought.Battle.Model;
using Warwrought.Battle.Transcript;
using Warwrought.Core;

namespace Warwrought.Battle.Simulation;

/// <summary>
/// The single public M1 authoritative resolution entry point. It accepts only the immutable
/// committed <see cref="BattleDefinition"/>; all mutable combat state is private to one run.
/// No Godot nodes, scene lifecycle, render timing, physics, or presentation state participate.
/// </summary>
public static class AuthoritativeBattleResolver
{
    /// <summary>
    /// Resolves one validated committed definition through the bounded fixed-tick M1 fixture.
    /// The returned result and transcript are produced together and share one canonical digest.
    /// </summary>
    public static BattleResolution Resolve(BattleDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        // BattleDefinition can only be constructed through its validating commitment boundary.
        // Keep this guard explicit so a future version cannot accidentally enter the M1 loop.
        if (definition.SimulationVersion != M1BattleSettings.SimulationVersion)
        {
            throw new ArgumentException(
                $"The M1 resolver accepts only simulation version {M1BattleSettings.SimulationVersion}; received {definition.SimulationVersion}.",
                nameof(definition));
        }

        return new ResolverRun(definition).Execute();
    }

    private sealed class ResolverRun
    {
        private const string AdvanceReason = "fixture.advance.integer_xz_step";
        private const string RetreatReason = "fixture.rout.retreat_away_from_deployment_centre";
        private const string ContactReason = "formation_footprint_and_melee_range";
        private const string AttackReason = "fixture.cooldown.melee.front_rank_pair";
        private const string DamageReason = "fixture.damage.attack_minus_defense_plus_seeded_roll";
        private const string CasualtyReason = "damage_reduced_health_to_zero";
        private const string MoraleReason = "fixture.morale.casualty_loss";
        private const string RoutReason = "fixture.morale.threshold_crossed";
        private const string SafetyCapReason = "maximum logical tick count reached before terminal resolution";

        private readonly BattleDefinition _definition;
        private readonly DeterministicRng _rng;
        private readonly Dictionary<string, FixtureUnitDefinition> _fixtureDefinitions;
        private readonly List<MutableFormationState> _formations = new();
        private readonly Dictionary<string, MutableUnitState> _unitsById = new(StringComparer.Ordinal);
        private readonly List<BattleSemanticEvent> _events = new();
        private readonly List<BattleFormationKeyframe> _keyframes = new();
        private readonly BattleTranscriptHeader _header;
        private bool _contactActive;
        private bool _keyframeRequested;
        private long _lastKeyframeTick = -1;

        public ResolverRun(BattleDefinition definition)
        {
            _definition = definition;
            _rng = new DeterministicRng(definition.Seed);
            _fixtureDefinitions = new Dictionary<string, FixtureUnitDefinition>(StringComparer.Ordinal);

            for (var index = 0; index < definition.FixtureUnitDefinitions.Count; index++)
            {
                var fixtureDefinition = definition.FixtureUnitDefinitions[index];
                _fixtureDefinitions.Add(fixtureDefinition.DefinitionId.Value, fixtureDefinition);
            }

            for (var sideIndex = 0; sideIndex < definition.Sides.Count; sideIndex++)
            {
                var side = definition.Sides[sideIndex];
                for (var squadIndex = 0; squadIndex < side.Squads.Count; squadIndex++)
                {
                    var squad = side.Squads[squadIndex];
                    var members = new List<MutableUnitState>(squad.Members.Count);
                    for (var memberIndex = 0; memberIndex < squad.Members.Count; memberIndex++)
                    {
                        var member = squad.Members[memberIndex];
                        var fixtureDefinition = _fixtureDefinitions[member.FixtureUnitDefinitionId.Value];
                        var slot = squad.Formation.Slots[memberIndex];
                        var unit = new MutableUnitState(
                            member,
                            fixtureDefinition,
                            BattleUnitState.Active,
                            fixtureDefinition.MaximumHealth,
                            slot.SlotIndex,
                            slot.Rank,
                            slot.File,
                            slot.Position);
                        members.Add(unit);
                        _unitsById.Add(member.Id.Value, unit);
                    }

                    _formations.Add(new MutableFormationState(side, squad, members));
                }
            }

            _header = new BattleTranscriptHeader(
                definition.BattleId,
                definition.SimulationVersion,
                definition.Seed,
                definition.CanonicalInputDigest,
                M1BattleSettings.TicksPerSecond,
                M1BattleSettings.TranscriptKeyframeIntervalTicks);
        }

        public BattleResolution Execute()
        {
            AddEvent(
                SimulationTick.Zero,
                BattleEventType.BattleStarted,
                reason: "committed_definition_accepted_for_authoritative_resolution");
            CaptureKeyframes(SimulationTick.Zero, force: true);

            // The bound counts tick zero as the first logical tick. Every exit from this loop
            // is either a terminal result or the explicit safety-cap failure below.
            for (var tickValue = 0; tickValue < M1BattleSettings.MaximumSimulationTicks; tickValue++)
            {
                var tick = new SimulationTick(tickValue);
                _keyframeRequested = false;

                AdvanceFormations(tick);
                UpdateContactState(tick);

                if (_contactActive)
                {
                    ResolveMelee(tick);
                    UpdateContactState(tick);
                }

                var terminal = TryGetTerminalOutcome(out var resultType, out var winnerSideId, out var terminalReason);
                if (terminal)
                {
                    return Finish(tick, resultType, winnerSideId, isTerminal: true, terminalReason);
                }

                if (_keyframeRequested || tickValue % M1BattleSettings.TranscriptKeyframeIntervalTicks == 0)
                {
                    CaptureKeyframes(tick, force: true);
                }
            }

            var capTick = new SimulationTick(M1BattleSettings.MaximumSimulationTicks - 1L);
            UpdateContactState(capTick);
            return Finish(capTick, BattleResultType.NonTerminalFailure, winnerSideId: null, isTerminal: false, SafetyCapReason);
        }

        private void AdvanceFormations(SimulationTick tick)
        {
            for (var formationIndex = 0; formationIndex < _formations.Count; formationIndex++)
            {
                var formation = _formations[formationIndex];
                if (formation.State == BattleFormationState.Routing)
                {
                    MoveFormationToward(formation, formation.RetreatDestination, M1BattleSettings.FixtureRetreatStepUnits, tick, RetreatReason);
                    if (formation.Anchor == formation.RetreatDestination)
                    {
                        CompleteRetreat(formation, tick);
                    }

                    continue;
                }

                if (formation.State != BattleFormationState.Advancing || _contactActive)
                {
                    continue;
                }

                if (formation.Anchor.ManhattanDistanceTo(formation.Squad.AdvanceOrder.Destination) <= formation.Squad.AdvanceOrder.StopRangeUnits)
                {
                    continue;
                }

                MoveFormationToward(
                    formation,
                    formation.Squad.AdvanceOrder.Destination,
                    M1BattleSettings.FixtureAdvanceStepUnits,
                    tick,
                    AdvanceReason);
            }
        }

        private void MoveFormationToward(
            MutableFormationState formation,
            SimPosition destination,
            int stepUnits,
            SimulationTick tick,
            string reason)
        {
            var nextX = MoveCoordinate(formation.Anchor.X, destination.X, stepUnits);
            var nextZ = MoveCoordinate(formation.Anchor.Z, destination.Z, stepUnits);
            if (nextX == formation.Anchor.X && nextZ == formation.Anchor.Z)
            {
                return;
            }

            formation.Anchor = new SimPosition(nextX, nextZ);
            formation.UpdateLiveMemberPositions();
            AddEvent(
                tick,
                BattleEventType.FormationMoved,
                sideId: formation.Side.Id,
                squadId: formation.Squad.Id,
                amount: stepUnits,
                value: formation.Anchor.X,
                secondaryValue: formation.Anchor.Z,
                reason: reason,
                position: formation.Anchor,
                facing: formation.Squad.Formation.Facing);
        }

        private static int MoveCoordinate(int current, int destination, int stepUnits)
        {
            var difference = (long)destination - current;
            if (difference == 0 || Math.Abs(difference) <= stepUnits)
            {
                return destination;
            }

            return checked(current + (difference > 0 ? stepUnits : -stepUnits));
        }

        private void UpdateContactState(SimulationTick tick)
        {
            var shouldBeInContact = _formations.Count == 2 && AreFormationFootprintsInContact(_formations[0], _formations[1]);
            if (!_contactActive && shouldBeInContact)
            {
                _contactActive = true;
                for (var index = 0; index < _formations.Count; index++)
                {
                    if (_formations[index].State == BattleFormationState.Advancing)
                    {
                        _formations[index].State = BattleFormationState.Engaged;
                    }
                }

                AddEvent(
                    tick,
                    BattleEventType.ContactStarted,
                    sideId: _formations[0].Side.Id,
                    squadId: _formations[0].Squad.Id,
                    otherSideId: _formations[1].Side.Id,
                    otherSquadId: _formations[1].Squad.Id,
                    reason: ContactReason);
                _keyframeRequested = true;
            }
            else if (_contactActive && !shouldBeInContact)
            {
                EndContact(tick, "formation_left_contact_footprint_or_became_inactive");
            }
        }

        private void EndContact(SimulationTick tick, string reason)
        {
            if (!_contactActive)
            {
                return;
            }

            _contactActive = false;
            AddEvent(
                tick,
                BattleEventType.ContactEnded,
                sideId: _formations[0].Side.Id,
                squadId: _formations[0].Squad.Id,
                otherSideId: _formations[1].Side.Id,
                otherSquadId: _formations[1].Squad.Id,
                reason: reason);
            _keyframeRequested = true;
        }

        private void ResolveMelee(SimulationTick tick)
        {
            var left = _formations[0];
            var right = _formations[1];
            var pairs = BuildFrontRankPairs(left, right);

            for (var pairIndex = 0; pairIndex < pairs.Count; pairIndex++)
            {
                var pair = pairs[pairIndex];
                ResolveAttack(pair.Left, pair.Right, tick);
                ResolveAttack(pair.Right, pair.Left, tick);
            }
        }

        private List<FrontRankPair> BuildFrontRankPairs(MutableFormationState left, MutableFormationState right)
        {
            var leftFront = GetFrontRankMembers(left);
            var rightFront = GetFrontRankMembers(right);
            var pairCount = Math.Min(leftFront.Count, rightFront.Count);
            var pairs = new List<FrontRankPair>(pairCount);
            for (var index = 0; index < pairCount; index++)
            {
                pairs.Add(new FrontRankPair(leftFront[index], rightFront[index]));
            }

            return pairs;
        }

        private List<MutableUnitState> GetFrontRankMembers(MutableFormationState formation)
        {
            var members = new List<MutableUnitState>();
            for (var index = 0; index < formation.Members.Count; index++)
            {
                var member = formation.Members[index];
                if (member.State == BattleUnitState.Active && member.Rank == 0)
                {
                    members.Add(member);
                }
            }

            // Sort by the world-space lateral coordinate, then stable ID. This pairs opposing
            // facings by the same physical file rather than by each formation's local file index.
            members.Sort((first, second) =>
            {
                var lateralComparison = GetLateralCoordinate(first.Position, _currentOpposingAxis()).CompareTo(
                    GetLateralCoordinate(second.Position, _currentOpposingAxis()));
                return lateralComparison != 0
                    ? lateralComparison
                    : first.Definition.Id.CompareTo(second.Definition.Id);
            });
            return members;
        }

        private int _currentOpposingAxis()
        {
            // M1's two formations advance on the axis separating their anchors. The resolver
            // uses Z for the canonical fixture and retains the X alternative for valid narrow
            // test definitions without introducing a general spatial framework.
            return _formations.Count == 2 &&
                   Math.Abs((long)_formations[1].Anchor.X - _formations[0].Anchor.X) >
                   Math.Abs((long)_formations[1].Anchor.Z - _formations[0].Anchor.Z)
                ? 1
                : 0;
        }

        private static long GetLateralCoordinate(SimPosition position, int opposingAxis)
        {
            return opposingAxis == 0 ? position.X : position.Z;
        }

        private void ResolveAttack(MutableUnitState attacker, MutableUnitState target, SimulationTick tick)
        {
            if (attacker.State != BattleUnitState.Active || target.State != BattleUnitState.Active || attacker.NextAttackTick > tick.Value)
            {
                return;
            }

            var rangeSquared = (long)attacker.Statistics.MeleeRangeUnits * attacker.Statistics.MeleeRangeUnits;
            if (attacker.Position.DistanceSquaredTo(target.Position) > rangeSquared)
            {
                return;
            }

            attacker.NextAttackTick = checked(tick.Value + attacker.Statistics.AttackCooldownTicks);
            var seededRoll = _rng.NextInt(0, M1BattleSettings.FixtureDamageRollMaxExclusive);
            var baseDamage = attacker.Statistics.MeleeAttack - target.Statistics.MeleeDefense;
            var rawDamage = (long)baseDamage + seededRoll;
            var damage = rawDamage <= 0 ? 0 : checked((int)rawDamage);
            var targetHealthBefore = target.Health;

            AddEvent(
                tick,
                BattleEventType.AttackResolved,
                sideId: attacker.Definition.SideId,
                squadId: attacker.Definition.SquadId,
                sourceUnitId: attacker.Definition.Id,
                targetUnitId: target.Definition.Id,
                amount: damage,
                value: seededRoll,
                secondaryValue: targetHealthBefore,
                reason: AttackReason,
                position: attacker.Position,
                facing: FindFormation(attacker.Definition.SquadId).Squad.Formation.Facing);

            target.Health = Math.Max(0, target.Health - damage);
            AddEvent(
                tick,
                BattleEventType.DamageDealt,
                sideId: attacker.Definition.SideId,
                squadId: attacker.Definition.SquadId,
                sourceUnitId: attacker.Definition.Id,
                targetUnitId: target.Definition.Id,
                amount: damage,
                value: target.Health,
                secondaryValue: targetHealthBefore,
                reason: DamageReason,
                position: target.Position);

            if (target.Health == 0 && target.State == BattleUnitState.Active)
            {
                KillUnit(attacker, target, tick);
            }
        }

        private void KillUnit(MutableUnitState attacker, MutableUnitState target, SimulationTick tick)
        {
            target.State = BattleUnitState.Dead;
            target.SlotIndex = -1;
            target.Rank = -1;
            target.File = -1;
            target.KilledAtTick = tick;
            target.KilledByUnitId = attacker.Definition.Id;

            AddEvent(
                tick,
                BattleEventType.UnitKilled,
                sideId: target.Definition.SideId,
                squadId: target.Definition.SquadId,
                sourceUnitId: attacker.Definition.Id,
                targetUnitId: target.Definition.Id,
                reason: CasualtyReason,
                position: target.Position);

            var targetFormation = FindFormation(target.Definition.SquadId);
            targetFormation.ReformActiveMembers();
            _keyframeRequested = true;
            var previousMorale = targetFormation.Morale;
            var newMorale = Math.Max(0, previousMorale - target.Statistics.MoraleLossPerCasualty);
            targetFormation.Morale = newMorale;
            AddEvent(
                tick,
                BattleEventType.MoraleChanged,
                sideId: targetFormation.Side.Id,
                squadId: targetFormation.Squad.Id,
                amount: newMorale - previousMorale,
                value: newMorale,
                secondaryValue: previousMorale,
                reason: MoraleReason);

            if (targetFormation.ActiveMemberCount == 0)
            {
                targetFormation.State = BattleFormationState.Defeated;
                _keyframeRequested = true;
            }
            else if (newMorale <= M1BattleSettings.FixtureRoutMoraleThreshold)
            {
                StartRout(targetFormation, tick);
            }
        }

        private void StartRout(MutableFormationState formation, SimulationTick tick)
        {
            if (formation.State is BattleFormationState.Routing or BattleFormationState.Retreated or BattleFormationState.Defeated)
            {
                return;
            }

            formation.State = BattleFormationState.Routing;
            for (var index = 0; index < formation.Members.Count; index++)
            {
                var member = formation.Members[index];
                if (member.State == BattleUnitState.Active)
                {
                    member.State = BattleUnitState.Routed;
                }
            }

            AddEvent(
                tick,
                BattleEventType.RoutStarted,
                sideId: formation.Side.Id,
                squadId: formation.Squad.Id,
                value: formation.Morale,
                reason: RoutReason,
                position: formation.Anchor,
                facing: formation.Squad.Formation.Facing);
            _keyframeRequested = true;
        }

        private void CompleteRetreat(MutableFormationState formation, SimulationTick tick)
        {
            if (formation.State != BattleFormationState.Routing)
            {
                return;
            }

            for (var index = 0; index < formation.Members.Count; index++)
            {
                var member = formation.Members[index];
                if (member.State == BattleUnitState.Routed)
                {
                    member.State = BattleUnitState.Retreated;
                }
            }

            formation.State = BattleFormationState.Retreated;
            AddEvent(
                tick,
                BattleEventType.RetreatCompleted,
                sideId: formation.Side.Id,
                squadId: formation.Squad.Id,
                value: formation.Morale,
                reason: RetreatReason,
                position: formation.Anchor,
                facing: formation.Squad.Formation.Facing);
            _keyframeRequested = true;
        }

        private bool TryGetTerminalOutcome(out BattleResultType resultType, out BattleSideId? winnerSideId, out string reason)
        {
            var first = _formations[0];
            var second = _formations[1];
            var firstResolved = first.State is BattleFormationState.Defeated or BattleFormationState.Retreated;
            var secondResolved = second.State is BattleFormationState.Defeated or BattleFormationState.Retreated;

            if (!firstResolved && !secondResolved)
            {
                resultType = default;
                winnerSideId = null;
                reason = string.Empty;
                return false;
            }

            if (firstResolved && secondResolved)
            {
                resultType = BattleResultType.Draw;
                winnerSideId = null;
                reason = "both formations were defeated or completed retreat";
                return true;
            }

            if (firstResolved)
            {
                resultType = BattleResultType.SideBWin;
                winnerSideId = second.Side.Id;
                reason = first.State == BattleFormationState.Defeated
                    ? "side.a formation was defeated"
                    : "side.a formation completed retreat";
                return true;
            }

            resultType = BattleResultType.SideAWin;
            winnerSideId = first.Side.Id;
            reason = second.State == BattleFormationState.Defeated
                ? "side.b formation was defeated"
                : "side.b formation completed retreat";
            return true;
        }

        private BattleResolution Finish(
            SimulationTick tick,
            BattleResultType resultType,
            BattleSideId? winnerSideId,
            bool isTerminal,
            string reason)
        {
            if (_contactActive)
            {
                EndContact(tick, isTerminal ? "battle_terminal_state_left_contact" : "safety_cap_ended_contact_tracking");
            }

            AddEvent(
                tick,
                BattleEventType.BattleEnded,
                sideId: winnerSideId,
                amount: (int)resultType,
                value: isTerminal ? 1 : 0,
                reason: reason,
                isTerminal: isTerminal);
            CaptureKeyframes(tick, force: true);

            var transcript = new BattleTranscript(_header, _events, _keyframes);
            var result = new BattleResult(
                _definition.BattleId,
                _definition.SimulationVersion,
                _definition.Seed,
                resultType,
                winnerSideId,
                tick,
                isTerminal,
                reason,
                BuildSurvivors(),
                BuildCasualties(),
                transcript.CanonicalDigest);
            return new BattleResolution(result, transcript);
        }

        private List<BattleSurvivorRecord> BuildSurvivors()
        {
            var survivors = new List<BattleSurvivorRecord>();
            for (var index = 0; index < _definition.OrderedUnits.Count; index++)
            {
                var definitionUnit = _definition.OrderedUnits[index];
                var unit = _unitsById[definitionUnit.Id.Value];
                if (unit.State == BattleUnitState.Dead)
                {
                    continue;
                }

                survivors.Add(new BattleSurvivorRecord(
                    unit.Definition.Id,
                    unit.Definition.SideId,
                    unit.Definition.SquadId,
                    unit.State,
                    unit.Health,
                    FindFormation(unit.Definition.SquadId).Morale));
            }

            return survivors;
        }

        private List<BattleCasualtyRecord> BuildCasualties()
        {
            var casualties = new List<BattleCasualtyRecord>();
            for (var index = 0; index < _definition.OrderedUnits.Count; index++)
            {
                var definitionUnit = _definition.OrderedUnits[index];
                var unit = _unitsById[definitionUnit.Id.Value];
                if (unit.State != BattleUnitState.Dead || !unit.KilledAtTick.HasValue)
                {
                    continue;
                }

                casualties.Add(new BattleCasualtyRecord(
                    unit.Definition.Id,
                    unit.Definition.SideId,
                    unit.Definition.SquadId,
                    unit.KilledAtTick.Value,
                    unit.KilledByUnitId));
            }

            return casualties;
        }

        private MutableFormationState FindFormation(BattleSquadId squadId)
        {
            for (var index = 0; index < _formations.Count; index++)
            {
                if (_formations[index].Squad.Id == squadId)
                {
                    return _formations[index];
                }
            }

            throw new InvalidOperationException($"Committed resolver state is missing squad '{squadId.Value}'.");
        }

        private void CaptureKeyframes(SimulationTick tick, bool force)
        {
            if (!force || _lastKeyframeTick == tick.Value)
            {
                return;
            }

            for (var formationIndex = 0; formationIndex < _formations.Count; formationIndex++)
            {
                var formation = _formations[formationIndex];
                var members = new List<BattleMemberKeyframe>(formation.Members.Count);
                for (var memberIndex = 0; memberIndex < formation.Members.Count; memberIndex++)
                {
                    var member = formation.Members[memberIndex];
                    members.Add(new BattleMemberKeyframe(
                        member.Definition.Id,
                        member.State == BattleUnitState.Dead ? -1 : member.SlotIndex,
                        member.State == BattleUnitState.Dead ? -1 : member.Rank,
                        member.State == BattleUnitState.Dead ? -1 : member.File,
                        member.Position,
                        member.State));
                }

                _keyframes.Add(new BattleFormationKeyframe(
                    tick,
                    formation.Side.Id,
                    formation.Squad.Id,
                    formation.Anchor,
                    formation.Squad.Formation.Facing,
                    formation.State,
                    formation.Morale,
                    members));
            }

            _lastKeyframeTick = tick.Value;
            _keyframeRequested = false;
        }

        private void AddEvent(
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
            _events.Add(new BattleSemanticEvent(
                _events.Count,
                tick,
                type,
                sideId,
                squadId,
                otherSideId,
                otherSquadId,
                sourceUnitId,
                targetUnitId,
                amount,
                value,
                secondaryValue,
                reason,
                position,
                facing,
                isTerminal));
        }

        private static bool AreFormationFootprintsInContact(MutableFormationState first, MutableFormationState second)
        {
            if (!first.CanFight || !second.CanFight)
            {
                return false;
            }

            GetFootprintBounds(first, out var firstMinX, out var firstMaxX, out var firstMinZ, out var firstMaxZ);
            GetFootprintBounds(second, out var secondMinX, out var secondMaxX, out var secondMinZ, out var secondMaxZ);

            var xOverlap = firstMaxX >= secondMinX && secondMaxX >= firstMinX;
            var zOverlap = firstMaxZ >= secondMinZ && secondMaxZ >= firstMinZ;
            var anchorDifferenceX = Math.Abs((long)second.Anchor.X - first.Anchor.X);
            var anchorDifferenceZ = Math.Abs((long)second.Anchor.Z - first.Anchor.Z);

            long footprintGap;
            long lateralOverlapLength;
            int maxRange;
            if (anchorDifferenceZ >= anchorDifferenceX)
            {
                footprintGap = GetIntervalGap(firstMinZ, firstMaxZ, secondMinZ, secondMaxZ);
                lateralOverlapLength = GetIntervalOverlapLength(firstMinX, firstMaxX, secondMinX, secondMaxX);
                maxRange = Math.Max(first.MaximumActiveMeleeRange, second.MaximumActiveMeleeRange);
            }
            else
            {
                footprintGap = GetIntervalGap(firstMinX, firstMaxX, secondMinX, secondMaxX);
                lateralOverlapLength = GetIntervalOverlapLength(firstMinZ, firstMaxZ, secondMinZ, secondMaxZ);
                maxRange = Math.Max(first.MaximumActiveMeleeRange, second.MaximumActiveMeleeRange);
            }

            return (anchorDifferenceZ >= anchorDifferenceX ? xOverlap : zOverlap) &&
                   lateralOverlapLength >= 0 &&
                   footprintGap <= maxRange;
        }

        private static void GetFootprintBounds(
            MutableFormationState formation,
            out long minX,
            out long maxX,
            out long minZ,
            out long maxZ)
        {
            minX = long.MaxValue;
            maxX = long.MinValue;
            minZ = long.MaxValue;
            maxZ = long.MinValue;
            var slots = formation.Squad.Formation.Slots;
            for (var index = 0; index < slots.Count; index++)
            {
                var slot = slots[index];
                var position = TranslateTemplateSlot(formation.Squad.Formation, formation.Anchor, slot.SlotIndex);
                minX = Math.Min(minX, position.X);
                maxX = Math.Max(maxX, position.X);
                minZ = Math.Min(minZ, position.Z);
                maxZ = Math.Max(maxZ, position.Z);
            }
        }

        private static long GetIntervalGap(long firstMin, long firstMax, long secondMin, long secondMax)
        {
            if (firstMax < secondMin)
            {
                return secondMin - firstMax;
            }

            if (secondMax < firstMin)
            {
                return firstMin - secondMax;
            }

            return 0;
        }

        private static long GetIntervalOverlapLength(long firstMin, long firstMax, long secondMin, long secondMax)
        {
            return Math.Min(firstMax, secondMax) - Math.Max(firstMin, secondMin);
        }

        private static SimPosition TranslateTemplateSlot(RectangularFormationLayout layout, SimPosition anchor, int slotIndex)
        {
            var template = layout.Slots[slotIndex].Position;
            return anchor.Translate(template.X - layout.Anchor.X, template.Z - layout.Anchor.Z);
        }

        private sealed class MutableFormationState
        {
            public MutableFormationState(BattleSide side, BattleSquad squad, List<MutableUnitState> members)
            {
                Side = side;
                Squad = squad;
                Members = members;
                Anchor = squad.Formation.Anchor;
                Morale = members.Count == 0 ? 0 : members[0].Statistics.StartingMorale;
                RetreatDestination = CalculateRetreatDestination(squad.Formation.Anchor, squad.Formation.Facing);
            }

            public BattleSide Side { get; }

            public BattleSquad Squad { get; }

            public List<MutableUnitState> Members { get; }

            public SimPosition Anchor { get; set; }

            public int Morale { get; set; }

            public BattleFormationState State { get; set; } = BattleFormationState.Advancing;

            public SimPosition RetreatDestination { get; }

            public bool CanFight => (State == BattleFormationState.Advancing || State == BattleFormationState.Engaged) && ActiveMemberCount > 0;

            public int ActiveMemberCount => Members.Count(member => member.State == BattleUnitState.Active);

            public int MaximumActiveMeleeRange
            {
                get
                {
                    var maximum = 0;
                    for (var index = 0; index < Members.Count; index++)
                    {
                        if (Members[index].State == BattleUnitState.Active)
                        {
                            maximum = Math.Max(maximum, Members[index].Statistics.MeleeRangeUnits);
                        }
                    }

                    return maximum;
                }
            }

            public void UpdateLiveMemberPositions()
            {
                for (var index = 0; index < Members.Count; index++)
                {
                    var member = Members[index];
                    if (member.State == BattleUnitState.Dead || member.SlotIndex < 0)
                    {
                        continue;
                    }

                    member.Position = TranslateTemplateSlot(Squad.Formation, Anchor, member.SlotIndex);
                }
            }

            public void ReformActiveMembers()
            {
                var nextSlot = 0;
                for (var index = 0; index < Members.Count; index++)
                {
                    var member = Members[index];
                    if (member.State != BattleUnitState.Active)
                    {
                        member.SlotIndex = -1;
                        member.Rank = -1;
                        member.File = -1;
                        continue;
                    }

                    var slot = Squad.Formation.Slots[nextSlot];
                    member.SlotIndex = slot.SlotIndex;
                    member.Rank = slot.Rank;
                    member.File = slot.File;
                    member.Position = slot.Position;
                    nextSlot++;
                }

                UpdateLiveMemberPositions();
            }

            private static SimPosition CalculateRetreatDestination(SimPosition anchor, FormationFacing facing)
            {
                GetForward(facing, out var forwardX, out var forwardZ);
                return anchor.Translate(
                    checked(-forwardX * M1BattleSettings.FixtureRetreatDistanceUnits),
                    checked(-forwardZ * M1BattleSettings.FixtureRetreatDistanceUnits));
            }
        }

        private sealed class MutableUnitState
        {
            public MutableUnitState(
                BattleUnit definition,
                FixtureUnitDefinition statistics,
                BattleUnitState state,
                int health,
                int slotIndex,
                int rank,
                int file,
                SimPosition position)
            {
                Definition = definition;
                Statistics = statistics;
                State = state;
                Health = health;
                SlotIndex = slotIndex;
                Rank = rank;
                File = file;
                Position = position;
            }

            public BattleUnit Definition { get; }

            public FixtureUnitDefinition Statistics { get; }

            public BattleUnitState State { get; set; }

            public int Health { get; set; }

            public int SlotIndex { get; set; }

            public int Rank { get; set; }

            public int File { get; set; }

            public SimPosition Position { get; set; }

            public long NextAttackTick { get; set; }

            public SimulationTick? KilledAtTick { get; set; }

            public BattleUnitId? KilledByUnitId { get; set; }

        }

        private readonly record struct FrontRankPair(MutableUnitState Left, MutableUnitState Right);

        private static void GetForward(FormationFacing facing, out int forwardX, out int forwardZ)
        {
            switch (facing)
            {
                case FormationFacing.North:
                    forwardX = 0;
                    forwardZ = -1;
                    return;
                case FormationFacing.East:
                    forwardX = 1;
                    forwardZ = 0;
                    return;
                case FormationFacing.South:
                    forwardX = 0;
                    forwardZ = 1;
                    return;
                case FormationFacing.West:
                    forwardX = -1;
                    forwardZ = 0;
                    return;
                default:
                    throw new ArgumentOutOfRangeException(nameof(facing), facing, "The M1 formation facing is unsupported.");
            }
        }
    }
}
