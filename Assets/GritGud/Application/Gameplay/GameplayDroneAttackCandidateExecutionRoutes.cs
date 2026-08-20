using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;

namespace GritGud.Application.Gameplay
{
    public sealed class GameplayActorDroneAttackCandidateExecutionRoute :
        IGameplayCandidateExecutionRoute
    {
        public const string Id = "actor-drone-attack.v1";

        private readonly ScenarioDefinition scenario;
        private readonly GameplayHeadlessSpatialEvidence spatial;

        public GameplayActorDroneAttackCandidateExecutionRoute(
            GameplayScenarioAssembly assembly,
            GameplayHeadlessSpatialEvidence spatialEvidence)
            : this(
                (assembly
                    ?? throw new ArgumentNullException(nameof(assembly)))
                    .Scenario,
                spatialEvidence)
        {
        }

        public GameplayActorDroneAttackCandidateExecutionRoute(
            ScenarioDefinition scenarioDefinition,
            GameplayHeadlessSpatialEvidence spatialEvidence)
        {
            scenario = scenarioDefinition ?? throw new ArgumentNullException(
                nameof(scenarioDefinition));
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
                return GameplayCapabilityProfiles.GetSubjectKind(profile)
                        == GameplaySemanticSubjectKind.Vehicle
                    && profile.GetTrait("delivery") == "immediate-ranged"
                    && profile.GetTrait("targeting") == "semantic-subject"
                    && profile.GetTrait("resource") == "equipped-weapon"
                    && profile.GetTrait("consequence") == "drone-integrity";
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
            GameplayCombatStateSnapshot state = context.State;
            state.RequireCoverage(GameplayCombatStateCoverage.Drones);
            GameplaySessionStateSnapshot session = state.Session;
            GameplayActorSnapshot attacker = session.GetActor(
                candidate.ActorId);
            AttackDefinition attack = GameplayDirectAttackPreparation
                .GetEquippedAttack(scenario, attacker);
            if (attack == null
                || !candidate.Profile.Equals(
                    GameplayCapabilityProfiles.AttackDrone(attack)))
                return Illegal(
                    context,
                    candidate,
                    "equipped-profile-mismatch");
            if (session.Mode != GameplaySessionMode.TurnBased)
                return Illegal(context, candidate, "turn-mode-required");
            if (session.Operation != GameplaySessionOperation.None)
                return Illegal(context, candidate, "operation-in-progress");
            if (!string.Equals(
                session.ActiveActorId,
                attacker.ActorId,
                StringComparison.Ordinal))
                return Illegal(context, candidate, "actor-not-active");
            if (attacker.IsIncapacitated)
                return Illegal(context, candidate, "actor-incapacitated");
            if (attacker.IsPinned)
                return Illegal(context, candidate, "actor-pinned");
            if (attacker.TurnBudget.ActionPoints
                    < attack.TurnCost.ActionPoints
                || attacker.TurnBudget.MovementOpportunity
                    < attack.TurnCost.MovementOpportunity)
                return Illegal(context, candidate, "insufficient-budget");

            DroneSnapshot target;
            try
            {
                target = FindDrone(state.Drones, candidate.SubjectId);
            }
            catch (KeyNotFoundException)
            {
                return Illegal(context, candidate, "drone-not-found");
            }
            if (!target.IsOperational)
                return Illegal(context, candidate, "drone-destroyed");
            ScenarioActorDefinition targetController = scenario.GetActor(
                target.Definition.ControllerActorId);
            if (!scenario.GetActor(attacker.ActorId).Combat.IsHostileTo(
                targetController.Combat.AllegianceId))
                return Illegal(context, candidate, "target-not-hostile");

            DroneExposureSnapshot exposure = GameplayHeadlessEncounterEvidence
                .CaptureActorSightOfDrone(
                    state,
                    spatial,
                    attacker.ActorId,
                    target.DroneId);
            if (exposure.VisibleSampleCount == 0)
                return Illegal(context, candidate, "target-not-exposed");
            long actionSequence = checked(
                session.LastActionSequence + 1L);
            var randomIdentity = new GameplayTransitionIdentity(
                actionSequence,
                GameplaySemanticCapability.DirectAttack.ToString(),
                attacker.ActorId,
                target.DroneId);
            uint seed = GameplayAddressedRandom.SampleUInt32(
                session.RunIdentity,
                randomIdentity,
                "resolution");
            ActorDroneAttackRecord record = DroneDirectAttackRules.Resolve(
                actionSequence,
                seed,
                attacker.ActorId,
                attack,
                attacker.TurnBudget,
                exposure,
                attacker.Pose.Position.DistanceTo(target.Position),
                target);
            return new GameplayExecutableCandidateEvaluation(
                Id,
                candidate,
                state.CanonicalHash,
                isLegal: true,
                failureCode: string.Empty,
                new GameplayCandidateOutcomeEstimate(new[]
                {
                    new GameplayCandidateOutcomeFeature(
                        "attack.hit-probability",
                        record.HitChancePercent / 100f),
                    new GameplayCandidateOutcomeFeature(
                        "drone.integrity-damage",
                        record.Damage?.AppliedDamage ?? 0f),
                    new GameplayCandidateOutcomeFeature(
                        "cost.action-points",
                        record.Cost.ActionPoints),
                }),
                new[]
                {
                    spatial.CaptureEvidence(
                        "actor-drone-sight",
                        state,
                        attacker.Pose.Position,
                        target.Position),
                },
                new PreparedActorDroneAttack(attack, record));
        }

        public GameplayTransitionPayload PreparePayload(
            GameplayDecisionContext context,
            GameplayExecutableCandidateEvaluation evaluation)
        {
            var prepared = evaluation?.FrozenPreparation
                    as PreparedActorDroneAttack
                ?? throw new ArgumentException(
                    "Actor-drone attack preparation is missing.",
                    nameof(evaluation));
            return new GameplayActorDroneAttackTransitionPayload(
                prepared.Attack,
                prepared.Record);
        }

        private sealed class PreparedActorDroneAttack
        {
            public PreparedActorDroneAttack(
                AttackDefinition attack,
                ActorDroneAttackRecord record)
            {
                Attack = attack;
                Record = record;
            }

            public AttackDefinition Attack { get; }
            public ActorDroneAttackRecord Record { get; }
        }

        private static DroneSnapshot FindDrone(
            IEnumerable<DroneSnapshot> drones,
            string droneId)
        {
            foreach (DroneSnapshot drone in drones)
                if (string.Equals(
                    drone.DroneId,
                    droneId,
                    StringComparison.Ordinal))
                    return drone;
            throw new KeyNotFoundException(
                $"Drone '{droneId}' is absent from canonical state.");
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

    public sealed class GameplayDroneAttackCandidateExecutionRoute :
        IGameplayCandidateExecutionRoute
    {
        public const string Id = "drone-attack.v1";

        private readonly ScenarioDefinition scenario;
        private readonly GameplayHeadlessSpatialEvidence spatial;

        public GameplayDroneAttackCandidateExecutionRoute(
            GameplayScenarioAssembly assembly,
            GameplayHeadlessSpatialEvidence spatialEvidence)
            : this(
                (assembly
                    ?? throw new ArgumentNullException(nameof(assembly)))
                    .Scenario,
                spatialEvidence)
        {
        }

        public GameplayDroneAttackCandidateExecutionRoute(
            ScenarioDefinition scenarioDefinition,
            GameplayHeadlessSpatialEvidence spatialEvidence)
        {
            scenario = scenarioDefinition ?? throw new ArgumentNullException(
                nameof(scenarioDefinition));
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
                return (subject == GameplaySemanticSubjectKind.Actor
                        || subject
                            == GameplaySemanticSubjectKind.DestructibleProp)
                    && profile.GetTrait("delivery") == "immediate-ranged"
                    && profile.GetTrait("targeting") == "semantic-subject"
                    && profile.GetTrait("resource")
                        == "controller-drone-weapon"
                    && (profile.GetTrait("consequence") == "actor-wound"
                        || profile.GetTrait("consequence")
                            == "destructible-damage");
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
            GameplayCombatStateSnapshot state = context.State;
            state.RequireCoverage(GameplayCombatStateCoverage.Drones);
            string sourceDroneId = GetSourceDroneId(candidate);
            DroneSnapshot drone;
            try
            {
                drone = FindDrone(state.Drones, sourceDroneId);
            }
            catch (KeyNotFoundException)
            {
                return Illegal(context, candidate, "source-drone-not-found");
            }
            GameplayActorSnapshot controller = state.Session.GetActor(
                candidate.ActorId);
            if (!string.Equals(
                drone.Definition.ControllerActorId,
                controller.ActorId,
                StringComparison.Ordinal))
                return Illegal(context, candidate, "controller-mismatch");
            if (!candidate.Profile.Equals(
                GameplayCapabilityProfiles.DroneAttack(
                    drone.Definition.Attack,
                    candidate.SubjectKind)))
                return Illegal(context, candidate, "drone-profile-mismatch");
            string readiness = ValidateControllerReadiness(
                state.Session,
                controller,
                drone);
            if (readiness.Length > 0)
                return Illegal(context, candidate, readiness);

            object consequence;
            GameplayPosition targetPosition;
            float hitProbability;
            switch (candidate.SubjectKind)
            {
                case GameplaySemanticSubjectKind.Actor:
                    GameplayActorSnapshot target = state.Session.GetActor(
                        candidate.SubjectId);
                    if (target.IsIncapacitated)
                        return Illegal(
                            context,
                            candidate,
                            "target-incapacitated");
                    if (!scenario.GetActor(controller.ActorId).Combat.IsHostileTo(
                        scenario.GetActor(target.ActorId).Combat.AllegianceId))
                        return Illegal(
                            context,
                            candidate,
                            "target-not-hostile");
                    TargetExposureSnapshot exposure =
                        GameplayHeadlessEncounterEvidence.CaptureDroneSight(
                            state,
                            spatial,
                            drone.DroneId,
                            target.ActorId);
                    if (exposure.VisibleSampleCount == 0)
                        return Illegal(
                            context,
                            candidate,
                            "target-not-exposed");
                    long resolutionSequence = checked(
                        state.Session.LastTransitionSequence + 1L);
                    var identity = new GameplayTransitionIdentity(
                        resolutionSequence,
                        GameplaySemanticCapability.DirectAttack.ToString(),
                        controller.ActorId,
                        target.ActorId);
                    AttackResolutionRecord resolution = AttackResolutionRules
                        .Resolve(
                            resolutionSequence,
                            GameplayAddressedRandom.SampleUInt32(
                                state.Session.RunIdentity,
                                identity,
                                "resolution"),
                            exposure,
                            drone.Definition.Attack.AccuracyDecay,
                            drone.Position.DistanceTo(target.Pose.Position),
                            target.Wounds,
                            drone.Definition.Attack.WoundMovementPenalty);
                    consequence = resolution;
                    targetPosition = target.Pose.Position;
                    hitProbability = resolution.FinalHitChancePercent / 100f;
                    break;
                case GameplaySemanticSubjectKind.DestructibleProp:
                    if (!DroneSensorRules.CanObserve(
                        drone,
                        FindProp(state.Destructibles, candidate.SubjectId)
                            .Pose.Position))
                        return Illegal(
                            context,
                            candidate,
                            "target-outside-sensor");
                    if (!spatial.TryResolveDestructibleDirectFireImpact(
                        state,
                        drone.Position,
                        candidate.SubjectId,
                        out DirectFireImpactRecord impact))
                        return Illegal(
                            context,
                            candidate,
                            "target-not-exposed");
                    DestructibleDamageRecord damage =
                        GameplayDirectAttackPreparation
                            .PrepareDirectFireDamage(
                                state,
                                drone.Definition.Attack,
                                candidate.SubjectId,
                                impact,
                                checked(
                                    state.Session.LastTransitionSequence + 1L));
                    if (damage == null)
                        return Illegal(
                            context,
                            candidate,
                            "no-integrity-damage");
                    consequence = damage;
                    targetPosition = impact.Point;
                    hitProbability = 1f;
                    break;
                default:
                    return Illegal(
                        context,
                        candidate,
                        "unsupported-subject");
            }

            var record = new DroneAttackRecord(
                controller.ActorId,
                drone.DroneId,
                candidate.SubjectId,
                candidate.SubjectKind.ToString(),
                drone.Definition.Attack.TurnCost,
                controller.TurnBudget,
                controller.TurnBudget.SpendAction(
                    drone.Definition.Attack.TurnCost),
                consequence);
            return new GameplayExecutableCandidateEvaluation(
                Id,
                candidate,
                state.CanonicalHash,
                isLegal: true,
                failureCode: string.Empty,
                new GameplayCandidateOutcomeEstimate(new[]
                {
                    new GameplayCandidateOutcomeFeature(
                        "attack.hit-probability",
                        hitProbability),
                    new GameplayCandidateOutcomeFeature(
                        "cost.action-points",
                        record.Cost.ActionPoints),
                }),
                new[]
                {
                    spatial.CaptureEvidence(
                        "drone-direct-fire",
                        state,
                        drone.Position,
                        targetPosition),
                },
                new PreparedDroneAttack(
                    candidate.SubjectKind,
                    drone.Definition.Attack,
                    record));
        }

        public GameplayTransitionPayload PreparePayload(
            GameplayDecisionContext context,
            GameplayExecutableCandidateEvaluation evaluation)
        {
            var prepared = evaluation?.FrozenPreparation as PreparedDroneAttack
                ?? throw new ArgumentException(
                    "Drone attack preparation is missing.",
                    nameof(evaluation));
            return new GameplayDroneAttackTransitionPayload(
                prepared.SubjectKind,
                prepared.Attack,
                prepared.Record);
        }

        private static string ValidateControllerReadiness(
            GameplaySessionStateSnapshot session,
            GameplayActorSnapshot controller,
            DroneSnapshot drone)
        {
            if (!drone.IsOperational) return "source-drone-destroyed";
            if (session.Mode != GameplaySessionMode.TurnBased)
                return "turn-mode-required";
            if (session.Operation != GameplaySessionOperation.None)
                return "operation-in-progress";
            if (!string.Equals(
                session.ActiveActorId,
                controller.ActorId,
                StringComparison.Ordinal))
                return "controller-not-active";
            if (controller.IsIncapacitated)
                return "controller-incapacitated";
            if (controller.TurnBudget.ActionPoints
                    < drone.Definition.Attack.TurnCost.ActionPoints
                || controller.TurnBudget.MovementOpportunity
                    < drone.Definition.Attack.TurnCost.MovementOpportunity)
                return "insufficient-budget";
            return string.Empty;
        }

        private static string GetSourceDroneId(GameplayCandidate candidate)
        {
            GameplayReachableInput input = candidate.Intent switch
            {
                GameplayTacticalIntent tactical => tactical.Input,
                GameplayReachableIntent reachable => reachable.Input,
                _ => null,
            };
            return GameplayContentIdentity.RequireText(
                input?.SourceSubjectId,
                "source drone ID");
        }

        private static DroneSnapshot FindDrone(
            IEnumerable<DroneSnapshot> drones,
            string droneId)
        {
            foreach (DroneSnapshot drone in drones)
                if (string.Equals(
                    drone.DroneId,
                    droneId,
                    StringComparison.Ordinal))
                    return drone;
            throw new KeyNotFoundException(
                $"Drone '{droneId}' is absent from canonical state.");
        }

        private static DestructiblePropSnapshot FindProp(
            IEnumerable<DestructiblePropSnapshot> props,
            string propId)
        {
            foreach (DestructiblePropSnapshot prop in props)
                if (string.Equals(
                    prop.PropId,
                    propId,
                    StringComparison.Ordinal))
                    return prop;
            throw new KeyNotFoundException(
                $"Destructible prop '{propId}' is absent from canonical state.");
        }

        private sealed class PreparedDroneAttack
        {
            public PreparedDroneAttack(
                GameplaySemanticSubjectKind subjectKind,
                AttackDefinition attack,
                DroneAttackRecord record)
            {
                SubjectKind = subjectKind;
                Attack = attack;
                Record = record;
            }

            public GameplaySemanticSubjectKind SubjectKind { get; }
            public AttackDefinition Attack { get; }
            public DroneAttackRecord Record { get; }
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
