using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;

namespace GritGud.Application.Gameplay
{
    public sealed class GameplayEncounterObservationCandidateExecutionRoute :
        IGameplayCandidateExecutionRoute
    {
        public const string Id = "encounter-observation.v1";
        private readonly ScenarioDefinition scenario;
        private readonly GameplayHeadlessSpatialEvidence spatial;

        public GameplayEncounterObservationCandidateExecutionRoute(
            ScenarioDefinition scenarioDefinition,
            GameplayHeadlessSpatialEvidence spatialEvidence)
        {
            scenario = scenarioDefinition ?? throw new ArgumentNullException(
                nameof(scenarioDefinition));
            spatial = spatialEvidence ?? throw new ArgumentNullException(
                nameof(spatialEvidence));
        }

        public string RouteId => Id;

        public bool Supports(GameplayCapabilityProfile profile) =>
            profile != null
            && profile.Equals(GameplayCapabilityProfiles.ObserveEncounter());

        public GameplayExecutableCandidateEvaluation Evaluate(
            GameplayDecisionContext context,
            GameplayCandidate candidate)
        {
            GameplayBasicCandidateRouteUtility.Require(
                context,
                candidate,
                Supports,
                Id);
            GameplaySessionStateSnapshot session = context.State.Session;
            ScenarioActorDefinition observerDefinition = scenario.GetActor(
                candidate.ActorId);
            EnemyBehaviorDefinition behavior = observerDefinition.Combat
                .EnemyBehavior;
            GameplayActorSnapshot observer = session.GetActor(
                candidate.ActorId);
            string failure = behavior == null
                ? "enemy-behavior-required"
                : observer.IsIncapacitated
                    ? "observer-incapacitated"
                    : !session.EncounterState.TryGetAwareness(
                        observer.ActorId,
                        out _)
                        ? "awareness-state-required"
                        : string.Empty;
            bool legal = failure.Length == 0;
            GameplayEncounterObservationTransitionPayload payload = null;
            GameplayEvidenceRecord evidence = null;
            float suspicionDelta = 0f;
            if (legal)
            {
                EncounterObservation observation = CaptureBestObservation(
                    context.State,
                    observerDefinition,
                    observer.ActorId);
                EnemyAwarenessSnapshot previous = session.EncounterState
                    .GetAwareness(observer.ActorId);
                EnemyAwarenessSnapshot resulting = EncounterAwarenessRules
                    .Evaluate(
                        behavior,
                        observer.Pose,
                        previous,
                        observation);
                suspicionDelta = resulting.Suspicion - previous.Suspicion;
                payload = new GameplayEncounterObservationTransitionPayload(
                    observer.ActorId,
                    behavior,
                    observation);
                evidence = new GameplayEvidenceRecord(
                    "encounter.observation",
                    session.Revision,
                    GameplayCanonicalValueDigest.Calculate(observation));
            }
            return new GameplayExecutableCandidateEvaluation(
                Id,
                candidate,
                context.State.CanonicalHash,
                legal,
                failure,
                new GameplayCandidateOutcomeEstimate(new[]
                {
                    new GameplayCandidateOutcomeFeature(
                        "awareness.suspicion-delta",
                        suspicionDelta),
                    new GameplayCandidateOutcomeFeature(
                        "awareness.visible-hostile",
                        payload?.Observation.HasVisibleSight == true ? 1f : 0f),
                }),
                evidence == null
                    ? null
                    : new[] { evidence },
                payload);
        }

        public GameplayTransitionPayload PreparePayload(
            GameplayDecisionContext context,
            GameplayExecutableCandidateEvaluation evaluation) =>
            evaluation?.FrozenPreparation
                as GameplayEncounterObservationTransitionPayload
            ?? throw new ArgumentException(
                "Encounter observation preparation is missing.",
                nameof(evaluation));

        private EncounterObservation CaptureBestObservation(
            GameplayCombatStateSnapshot state,
            ScenarioActorDefinition observer,
            string observerId)
        {
            TargetExposureSnapshot best = null;
            GameplayPosition? bestPosition = null;
            foreach (GameplayActorSnapshot target in state.Session.Actors)
            {
                if (target.IsIncapacitated
                    || string.Equals(
                        target.ActorId,
                        observerId,
                        StringComparison.Ordinal)) continue;
                ScenarioActorDefinition targetDefinition = scenario.GetActor(
                    target.ActorId);
                if (!observer.Combat.IsHostileTo(
                    targetDefinition.Combat.AllegianceId)) continue;
                TargetExposureSnapshot exposure =
                    GameplayHeadlessEncounterEvidence.CaptureSight(
                        state,
                        spatial,
                        observerId,
                        target.ActorId);
                if (exposure.VisibleSampleCount == 0) continue;
                if (best != null
                    && (exposure.VisibleFraction < best.VisibleFraction
                        || (exposure.VisibleFraction == best.VisibleFraction
                            && StringComparer.Ordinal.Compare(
                                exposure.TargetId,
                                best.TargetId) >= 0))) continue;
                best = exposure;
                bestPosition = target.Pose.Position;
            }
            return new EncounterObservation(
                observerId,
                best,
                bestPosition,
                sound: null);
        }
    }

    public sealed class GameplayPatrolCandidateExecutionRoute :
        IGameplayCandidateExecutionRoute
    {
        public const string Id = "patrol.v1";
        private readonly ScenarioDefinition scenario;
        private readonly GameplayHeadlessSpatialEvidence spatial;

        public GameplayPatrolCandidateExecutionRoute(
            ScenarioDefinition scenarioDefinition,
            GameplayHeadlessSpatialEvidence spatialEvidence)
        {
            scenario = scenarioDefinition ?? throw new ArgumentNullException(
                nameof(scenarioDefinition));
            spatial = spatialEvidence ?? throw new ArgumentNullException(
                nameof(spatialEvidence));
        }

        public string RouteId => Id;

        public bool Supports(GameplayCapabilityProfile profile) =>
            profile != null
            && profile.Equals(GameplayCapabilityProfiles.Patrol());

        public GameplayExecutableCandidateEvaluation Evaluate(
            GameplayDecisionContext context,
            GameplayCandidate candidate)
        {
            GameplayBasicCandidateRouteUtility.Require(
                context,
                candidate,
                Supports,
                Id);
            GameplaySessionStateSnapshot session = context.State.Session;
            ScenarioActorDefinition definition = scenario.GetActor(
                candidate.ActorId);
            EnemyBehaviorDefinition behavior = definition.Combat.EnemyBehavior;
            PatrolRouteDefinition patrol = behavior?.PatrolRoute;
            GameplayActorSnapshot actor = session.GetActor(candidate.ActorId);
            EnemyAwarenessSnapshot awareness = behavior == null
                || !session.EncounterState.TryGetAwareness(
                    candidate.ActorId,
                    out EnemyAwarenessSnapshot found)
                ? null
                : found;
            int nextIndex = patrol == null || awareness == null
                ? -1
                : patrol.GetNextWaypointIndex(awareness.PatrolWaypointIndex);
            MovementRouteRecord route = null;
            string failure = session.Mode != GameplaySessionMode.Exploration
                    || session.EncounterActive
                ? "continuous-exploration-required"
                : actor.IsIncapacitated
                    ? "actor-incapacitated"
                    : patrol == null
                        ? "patrol-route-required"
                        : awareness == null
                            ? "awareness-state-required"
                            : awareness.State
                                != EncounterAwarenessState.Unaware
                                ? "actor-aware"
                                : nextIndex == awareness.PatrolWaypointIndex
                                    ? "patrol-complete"
                                    : TryBuildRoute(
                                        context.State,
                                        actor,
                                        patrol.GetWaypoint(nextIndex),
                                        out route)
                                        ? string.Empty
                                        : "patrol-route-blocked";
            bool legal = failure.Length == 0;
            GameplayPatrolTransitionPayload payload = null;
            GameplayEvidenceRecord evidence = null;
            if (legal)
            {
                var advance = new PatrolAdvanceRecord(
                    checked(session.EncounterState.LastTransitionSequence + 1L),
                    actor.ActorId,
                    route,
                    awareness.PatrolWaypointIndex,
                    nextIndex);
                payload = new GameplayPatrolTransitionPayload(
                    actor.ActorId,
                    behavior,
                    advance);
                evidence = spatial.CaptureEvidence(
                    "patrol-route",
                    context.State,
                    route.OriginPose.Position,
                    route.Destination,
                    clearanceRadius: 0.3f);
            }
            return new GameplayExecutableCandidateEvaluation(
                Id,
                candidate,
                context.State.CanonicalHash,
                legal,
                failure,
                new GameplayCandidateOutcomeEstimate(new[]
                {
                    new GameplayCandidateOutcomeFeature(
                        "patrol.distance",
                        route?.TotalCost ?? 0f),
                    new GameplayCandidateOutcomeFeature(
                        "patrol.waypoint-index",
                        nextIndex),
                }),
                evidence == null ? null : new[] { evidence },
                payload);
        }

        public GameplayTransitionPayload PreparePayload(
            GameplayDecisionContext context,
            GameplayExecutableCandidateEvaluation evaluation) =>
            evaluation?.FrozenPreparation as GameplayPatrolTransitionPayload
            ?? throw new ArgumentException(
                "Patrol preparation is missing.",
                nameof(evaluation));

        private bool TryBuildRoute(
            GameplayCombatStateSnapshot state,
            GameplayActorSnapshot actor,
            GameplayPosition destination,
            out MovementRouteRecord route)
        {
            var planner = new MovementRoutePlanner(
                actor,
                new AuthoredPatrolSegmentValidator(
                    state,
                    spatial));
            if (!planner.TryAppend(destination, out _))
            {
                route = null;
                return false;
            }
            route = planner.Confirm();
            return true;
        }

        private sealed class AuthoredPatrolSegmentValidator :
            IMovementRouteSegmentValidator
        {
            private readonly GameplayCombatStateSnapshot state;
            private readonly GameplayHeadlessSpatialEvidence spatial;

            public AuthoredPatrolSegmentValidator(
                GameplayCombatStateSnapshot canonicalState,
                GameplayHeadlessSpatialEvidence spatialEvidence)
            {
                state = canonicalState;
                spatial = spatialEvidence;
            }

            public MovementRouteSegmentValidation Validate(
                string actorId,
                GameplayPosition from,
                GameplayPosition requestedDestination) => spatial.BlocksPath(
                    state,
                    from,
                    requestedDestination,
                    clearanceRadius: 0.3f)
                    ? MovementRouteSegmentValidation.Rejected(
                        "The authored patrol segment is blocked.")
                    : MovementRouteSegmentValidation.Accepted(
                        requestedDestination);
        }
    }
}
