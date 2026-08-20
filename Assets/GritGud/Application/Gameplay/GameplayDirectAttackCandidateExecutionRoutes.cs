using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;

namespace GritGud.Application.Gameplay
{
    /// <summary>
    /// Pure preparation shared by live adapters and headless candidate routes.
    /// It owns immediate weapon discharges and their optional destructible
    /// consequence; neither caller mutates a session while resolving them.
    /// </summary>
    public static class GameplayDirectAttackPreparation
    {
        public static bool TryPrepareDischarge(
            GameplayCombatStateSnapshot state,
            ScenarioDefinition scenario,
            string actorId,
            string targetId,
            GameplayPosition aimPoint,
            DirectFireImpactRecord impact,
            bool canEnterTurnMode,
            out GameplayPreparedTransition<GameplayActionRecord> prepared,
            out AttackResolutionFailure failure)
        {
            prepared = null;
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (scenario == null)
                throw new ArgumentNullException(nameof(scenario));
            if (!string.Equals(
                    state.Session.ScenarioId,
                    scenario.Id,
                    StringComparison.Ordinal))
                throw new ArgumentException(
                    "Direct-fire rules and canonical state describe different scenarios.",
                    nameof(scenario));
            if (string.IsNullOrWhiteSpace(targetId))
                return Fail(
                    AttackResolutionFailure.TargetNotFound,
                    out failure);
            if (impact != null
                && (!string.Equals(
                        impact.TargetId,
                        targetId,
                        StringComparison.Ordinal)
                    || impact.Point.DistanceTo(aimPoint) > 0.0001f))
                return Fail(
                    AttackResolutionFailure.TargetNotFound,
                    out failure);

            GameplaySessionStateSnapshot session = state.Session;
            if (impact != null
                && impact.WorldStateRevision != session.JournalSequence)
                return Fail(
                    AttackResolutionFailure.WorldStateChanged,
                    out failure);
            if (session.Operation != GameplaySessionOperation.None)
                return Fail(
                    AttackResolutionFailure.OperationInProgress,
                    out failure);
            if (session.Mode == GameplaySessionMode.TurnBased
                && !string.Equals(
                    session.ActiveActorId,
                    actorId,
                    StringComparison.Ordinal))
                return Fail(
                    AttackResolutionFailure.ActorNotActive,
                    out failure);

            GameplayActorSnapshot actor;
            try
            {
                actor = session.GetActor(actorId);
            }
            catch (KeyNotFoundException)
            {
                return Fail(
                    AttackResolutionFailure.ActorNotActive,
                    out failure);
            }
            if (actor.IsIncapacitated)
                return Fail(
                    AttackResolutionFailure.ActorIncapacitated,
                    out failure);
            if (actor.IsPinned)
                return Fail(AttackResolutionFailure.ActorPinned, out failure);

            bool startsEncounter = scenario.TryGetAttackResponse(
                    targetId,
                    out AttackResponseDefinition response)
                && response.StartsEncounter;
            if (startsEncounter
                && !session.EncounterActive
                && session.Mode == GameplaySessionMode.Exploration
                && !canEnterTurnMode)
                return Fail(
                    AttackResolutionFailure.TurnModeRequired,
                    out failure);

            AttackDefinition attack = GetEquippedAttack(
                scenario,
                actor);
            if (attack == null || attack.Projectile != null)
                return Fail(
                    AttackResolutionFailure.AttackUnavailable,
                    out failure);
            if (!attack.CanTargetWorldPoint)
                return Fail(
                    AttackResolutionFailure.TargetRequired,
                    out failure);
            if (actor.Pose.Position.DistanceTo(aimPoint) <= 0f)
                return Fail(
                    AttackResolutionFailure.TargetNotFound,
                    out failure);

            ActionCost cost = session.Mode == GameplaySessionMode.TurnBased
                    || startsEncounter
                ? attack.TurnCost
                : new ActionCost(0, 0f, attack.TurnCost.Mobility);
            if (actor.TurnBudget.ActionPoints < cost.ActionPoints)
                return Fail(
                    AttackResolutionFailure.InsufficientActionPoints,
                    out failure);
            if (actor.TurnBudget.MovementOpportunity
                < cost.MovementOpportunity)
                return Fail(
                    AttackResolutionFailure.InsufficientMovementOpportunity,
                    out failure);

            long sequence = checked(session.LastActionSequence + 1L);
            DestructibleDamageRecord damage = PrepareDirectFireDamage(
                state,
                attack,
                targetId,
                impact,
                sequence);
            var discharge = new WeaponDischargeRecord(
                sequence,
                actorId,
                attack.ActionId,
                targetId,
                actor.Pose.Position,
                aimPoint,
                impact,
                damage);
            var action = new GameplayActionRecord(
                sequence,
                new GameplayActionRequest(
                    actorId,
                    attack.ActionId,
                    targetId),
                cost,
                actor.TurnBudget,
                actor.TurnBudget.SpendAction(cost),
                new[] { new WeaponDischargedActionOutcome(discharge) });
            prepared = new GameplayPreparedTransition<GameplayActionRecord>(
                action,
                state,
                GameplayWeaponActionStateProjector.Project(state, action));
            failure = AttackResolutionFailure.None;
            return true;
        }

        public static AttackDefinition GetEquippedAttack(
            ScenarioDefinition scenario,
            GameplayActorSnapshot actor)
        {
            if (scenario == null)
                throw new ArgumentNullException(nameof(scenario));
            ScenarioActorDefinition definition = scenario.GetActor(
                actor.ActorId);
            if (definition.Inventory.Count == 0) return definition.Attack;
            return actor.EquippedItemId == null
                ? null
                : definition.GetInventoryItem(actor.EquippedItemId)?.Attack;
        }

        private static DestructibleDamageRecord PrepareDirectFireDamage(
            GameplayCombatStateSnapshot state,
            AttackDefinition attack,
            string targetId,
            DirectFireImpactRecord impact,
            long sequence)
        {
            if (impact == null || attack.DirectFireDamage == null)
                return null;
            if (!state.Covers(GameplayCombatStateCoverage.Destructibles)
                || !TryFindProp(
                    state.Destructibles,
                    targetId,
                    out DestructiblePropSnapshot previous)
                || previous.State == DestructiblePropState.Destroyed)
                return null;
            float requested = attack.DirectFireDamage
                .EvaluateIntegrityDamage(impact.SurfaceId);
            if (requested <= 0f) return null;
            float applied = Math.Min(requested, previous.RemainingIntegrity);
            float remaining = Math.Max(
                0f,
                previous.RemainingIntegrity - applied);
            DestructiblePropState resultingState = remaining <= 0f
                ? DestructiblePropState.Destroyed
                : DestructiblePropState.Damaged;
            ulong detached = DestructibleFracture.CreateResultingMask(
                previous.PropId,
                previous.FractureChunkCount,
                previous.DetachedFractureChunks,
                previous.MaximumIntegrity,
                remaining,
                impact.PreferredFractureChunkIndex);
            var resulting = new DestructiblePropSnapshot(
                previous.PropId,
                resultingState,
                previous.MaximumIntegrity,
                remaining,
                previous.Pose,
                previous.Posture,
                previous.FractureChunkCount,
                detached);
            return new DestructibleDamageRecord(
                sequence,
                applied,
                previous,
                resulting,
                impact.PreferredFractureChunkIndex);
        }

        private static bool TryFindProp(
            IEnumerable<DestructiblePropSnapshot> props,
            string propId,
            out DestructiblePropSnapshot found)
        {
            foreach (DestructiblePropSnapshot prop in props)
                if (string.Equals(
                    prop.PropId,
                    propId,
                    StringComparison.Ordinal))
                {
                    found = prop;
                    return true;
                }
            found = default;
            return false;
        }

        private static bool Fail(
            AttackResolutionFailure value,
            out AttackResolutionFailure failure)
        {
            failure = value;
            return false;
        }
    }

    public sealed class GameplayWorldPositionIntent
    {
        public GameplayWorldPositionIntent(GameplayPosition position)
        {
            Position = position;
        }

        public GameplayPosition Position { get; }
    }

    public sealed class GameplayDirectAttackCandidateExecutionRoute :
        IGameplayCandidateExecutionRoute
    {
        public const string Id = "direct-fire.v1";

        private readonly ScenarioDefinition scenario;
        private readonly GameplayHeadlessSpatialEvidence spatial;

        public GameplayDirectAttackCandidateExecutionRoute(
            GameplayScenarioAssembly assembly,
            GameplayHeadlessSpatialEvidence spatialEvidence)
        {
            scenario = (assembly
                    ?? throw new ArgumentNullException(nameof(assembly)))
                .Scenario;
            spatial = spatialEvidence ?? throw new ArgumentNullException(
                nameof(spatialEvidence));
        }

        public string RouteId => Id;

        public bool Supports(GameplayCapabilityProfile profile)
        {
            if (profile == null
                || profile.Capability != GameplaySemanticCapability.DirectAttack)
                return false;
            try
            {
                GameplaySemanticSubjectKind subject =
                    GameplayCapabilityProfiles.GetSubjectKind(profile);
                return subject != GameplaySemanticSubjectKind.Actor
                    && profile.GetTrait("delivery") == "immediate-ranged"
                    && profile.GetTrait("targeting") == "semantic-subject"
                    && profile.GetTrait("resource") == "equipped-weapon"
                    && (profile.GetTrait("consequence") == "destructible-damage"
                        || profile.GetTrait("consequence")
                            == "discharge-only");
            }
            catch (KeyNotFoundException)
            {
                return false;
            }
        }

        public GameplayExecutableCandidateEvaluation Evaluate(
            GameplayDecisionContext context,
            GameplayCandidate candidate)
        {
            GameplayBasicCandidateRouteUtility.Require(
                context,
                candidate,
                Supports,
                Id);
            GameplayActorSnapshot actor = context.State.Session.GetActor(
                candidate.ActorId);
            AttackDefinition attack = GameplayDirectAttackPreparation
                .GetEquippedAttack(scenario, actor);
            if (attack == null
                || !candidate.Profile.Equals(
                    GameplayCapabilityProfiles.Attack(
                        attack,
                        candidate.SubjectKind)))
                return Illegal(
                    context,
                    candidate,
                    "equipped-profile-mismatch");

            if (!TryResolveAim(
                    context,
                    candidate,
                    actor.Pose.Position,
                    out GameplayPosition aimPoint,
                    out DirectFireImpactRecord impact,
                    out string failure))
                return Illegal(context, candidate, failure);
            if (!GameplayDirectAttackPreparation.TryPrepareDischarge(
                    context.State,
                    scenario,
                    candidate.ActorId,
                    candidate.SubjectId,
                    aimPoint,
                    impact,
                    canEnterTurnMode: false,
                    out GameplayPreparedTransition<GameplayActionRecord> prepared,
                    out AttackResolutionFailure attackFailure))
                return Illegal(
                    context,
                    candidate,
                    "attack." + attackFailure);
            WeaponDischargeRecord discharge =
                ((WeaponDischargedActionOutcome)prepared.Record.Outcomes[0])
                .Discharge;
            var features = new List<GameplayCandidateOutcomeFeature>
            {
                new GameplayCandidateOutcomeFeature(
                    "cost.action-points",
                    prepared.Record.Cost.ActionPoints),
                new GameplayCandidateOutcomeFeature(
                    "cost.movement-opportunity",
                    prepared.Record.Cost.MovementOpportunity),
                new GameplayCandidateOutcomeFeature("attack.discharge", 1f),
            };
            if (discharge.Damage != null)
                features.Add(new GameplayCandidateOutcomeFeature(
                    "destructible.integrity-damage",
                    discharge.Damage.AppliedDamage));
            return new GameplayExecutableCandidateEvaluation(
                Id,
                candidate,
                context.State.CanonicalHash,
                isLegal: true,
                failureCode: string.Empty,
                new GameplayCandidateOutcomeEstimate(features),
                new[]
                {
                    spatial.CaptureEvidence(
                        "direct-fire",
                        context.State,
                        actor.Pose.Position,
                        aimPoint),
                },
                prepared);
        }

        public GameplayTransitionPayload PreparePayload(
            GameplayDecisionContext context,
            GameplayExecutableCandidateEvaluation evaluation)
        {
            var prepared = evaluation?.FrozenPreparation
                    as GameplayPreparedTransition<GameplayActionRecord>
                ?? throw new ArgumentException(
                    "Direct-fire preparation is missing.",
                    nameof(evaluation));
            return new GameplayWeaponTransitionPayload(
                evaluation.Candidate.Profile,
                prepared.Record);
        }

        private bool TryResolveAim(
            GameplayDecisionContext context,
            GameplayCandidate candidate,
            GameplayPosition origin,
            out GameplayPosition aimPoint,
            out DirectFireImpactRecord impact,
            out string failure)
        {
            impact = null;
            switch (candidate.SubjectKind)
            {
                case GameplaySemanticSubjectKind.DestructibleProp:
                    if (!spatial.TryResolveDestructibleDirectFireImpact(
                        context.State,
                        origin,
                        candidate.SubjectId,
                        out impact))
                    {
                        aimPoint = default;
                        failure = "target-not-exposed";
                        return false;
                    }
                    aimPoint = impact.Point;
                    failure = string.Empty;
                    return true;
                case GameplaySemanticSubjectKind.Vehicle:
                    if (!TryFindVehiclePosition(
                        context.State.Vehicles,
                        candidate.SubjectId,
                        out aimPoint))
                    {
                        failure = "vehicle-not-found";
                        return false;
                    }
                    if (spatial.BlocksLineOfSight(
                        context.State,
                        origin,
                        aimPoint))
                    {
                        failure = "target-not-exposed";
                        return false;
                    }
                    failure = string.Empty;
                    return true;
                case GameplaySemanticSubjectKind.WorldPosition:
                    if (candidate.Intent is not GameplayWorldPositionIntent world)
                    {
                        aimPoint = default;
                        failure = "world-position-intent-required";
                        return false;
                    }
                    aimPoint = world.Position;
                    failure = string.Empty;
                    return true;
                default:
                    aimPoint = default;
                    failure = "unsupported-subject";
                    return false;
            }
        }

        private static bool TryFindVehiclePosition(
            IEnumerable<VehicleMomentumState> vehicles,
            string vehicleId,
            out GameplayPosition position)
        {
            foreach (VehicleMomentumState vehicle in vehicles)
                if (string.Equals(
                    vehicle.VehicleId,
                    vehicleId,
                    StringComparison.Ordinal))
                {
                    position = vehicle.Position;
                    return true;
                }
            position = default;
            return false;
        }

        private static GameplayExecutableCandidateEvaluation Illegal(
            GameplayDecisionContext context,
            GameplayCandidate candidate,
            string failure) => GameplayBasicCandidateRouteUtility.Result(
                Id,
                context,
                candidate,
                legal: false,
                failure,
                outcome: null,
                preparation: null);
    }
}
