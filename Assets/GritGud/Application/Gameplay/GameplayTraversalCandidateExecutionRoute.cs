using System;
using GritGud.Domain.Gameplay;

namespace GritGud.Application.Gameplay
{
    public sealed class GameplayHeadlessTraversalIntent
    {
        public GameplayHeadlessTraversalIntent(
            GameplayReachableInput input,
            string stateHash,
            MovementRouteRecord route,
            GameplayEvidenceRecord routeEvidence,
            float fireHazardTraversal)
        {
            Input = input ?? throw new ArgumentNullException(nameof(input));
            StateHash = GameplayContentIdentity.RequireDigest(
                stateHash,
                nameof(stateHash));
            Route = route ?? throw new ArgumentNullException(nameof(route));
            if (!route.HasTraversal)
                throw new ArgumentException(
                    "Traversal intents require an authored traversal segment.",
                    nameof(route));
            RouteEvidence = routeEvidence ?? throw new ArgumentNullException(
                nameof(routeEvidence));
            GameplayNumericPolicy.RequireFinite(
                fireHazardTraversal,
                nameof(fireHazardTraversal));
            if (fireHazardTraversal < 0f)
                throw new ArgumentOutOfRangeException(
                    nameof(fireHazardTraversal));
            FireHazardTraversal = fireHazardTraversal;
        }

        public GameplayReachableInput Input { get; }
        public string StateHash { get; }
        public MovementRouteRecord Route { get; }
        public GameplayEvidenceRecord RouteEvidence { get; }
        public float FireHazardTraversal { get; }
    }

    internal static class GameplayMoveCandidateExecutionRouteUtility
    {
        public static GameplayExecutableCandidateEvaluation Evaluate(
            string routeId,
            ScenarioDefinition scenario,
            GameplayDecisionContext context,
            GameplayCandidate candidate,
            string stateHash,
            MovementRouteRecord route,
            GameplayEvidenceRecord evidence,
            float fireHazardTraversal,
            bool requiresTraversal,
            GameplayHeadlessSpatialEvidence spatial)
        {
            if (spatial == null) throw new ArgumentNullException(
                nameof(spatial));
            GameplaySessionStateSnapshot session = context.State.Session;
            GameplayActorSnapshot actor = session.GetActor(candidate.ActorId);
            string failure = !string.Equals(
                    stateHash,
                    context.State.CanonicalHash,
                    StringComparison.Ordinal)
                ? "movement-evidence-stale"
                : !string.Equals(
                    route.ActorId,
                    candidate.ActorId,
                    StringComparison.Ordinal)
                    ? "movement-actor-mismatch"
                    : route.HasTraversal != requiresTraversal
                        ? "movement-path-kind-mismatch"
                        : session.Mode != GameplaySessionMode.TurnBased
                            ? "turn-mode-required"
                            : session.Operation
                                != GameplaySessionOperation.None
                                ? "operation-in-progress"
                                : !string.Equals(
                                    session.ActiveActorId,
                                    candidate.ActorId,
                                    StringComparison.Ordinal)
                                    ? "actor-not-active"
                                    : actor.IsIncapacitated
                                        ? "actor-incapacitated"
                                        : actor.IsPinned
                                            ? "actor-pinned"
                                            : !PosesMatch(
                                                actor.Pose,
                                                route.OriginPose)
                                                ? "movement-origin-stale"
                                                : route.TotalActionPointCost
                                                    > actor.TurnBudget
                                                        .ActionPoints
                                                    ? "movement-ap-unaffordable"
                                                    : route.TotalCost
                                                        > actor.TurnBudget
                                                            .MovementOpportunity
                                                            + 0.0001f
                                                        ? "movement-unaffordable"
                                                        : string.Empty;
            bool legal = failure.Length == 0;
            float beforeDistance = NearestHostileDistance(
                scenario,
                session,
                candidate.ActorId,
                actor.Pose.Position);
            float afterDistance = NearestHostileDistance(
                scenario,
                session,
                candidate.ActorId,
                route.Destination);
            float concussiveRange = MaximumAvailableConcussiveRange(
                scenario,
                actor);
            int concussiveHostilesBefore = CountHostilesWithinRange(
                scenario,
                session,
                candidate.ActorId,
                actor.Pose.Position,
                concussiveRange);
            int concussiveHostilesAfter = CountHostilesWithinRange(
                scenario,
                session,
                candidate.ActorId,
                route.Destination,
                concussiveRange);
            float visibilityBefore = CaptureHostileVisibility(
                scenario,
                context.State,
                spatial,
                candidate.ActorId);
            var movedState = new GameplayCanonicalStateMutation(context.State);
            movedState.ReplaceActor(GameplayCanonicalStateMutation.CopyActor(
                actor,
                pose: new GameplayActorPose(
                    route.Destination,
                    route.FinalFacingDegrees,
                    actor.Pose.Stance)));
            float visibilityAfter = CaptureHostileVisibility(
                scenario,
                movedState.Build(),
                spatial,
                candidate.ActorId);
            return new GameplayExecutableCandidateEvaluation(
                routeId,
                candidate,
                context.State.CanonicalHash,
                legal,
                failure,
                new GameplayCandidateOutcomeEstimate(new[]
                {
                    new GameplayCandidateOutcomeFeature(
                        "move.distance",
                        route.TotalCost),
                    new GameplayCandidateOutcomeFeature(
                        "move.traversal",
                        route.HasTraversal ? 1f : 0f),
                    new GameplayCandidateOutcomeFeature(
                        "cost.action-points",
                        route.TotalActionPointCost),
                    new GameplayCandidateOutcomeFeature(
                        "cost.movement-opportunity",
                        route.TotalCost),
                    new GameplayCandidateOutcomeFeature(
                        "hazard.fire-traversal",
                        fireHazardTraversal),
                    new GameplayCandidateOutcomeFeature(
                        "hostile.distance-before",
                        beforeDistance),
                    new GameplayCandidateOutcomeFeature(
                        "hostile.distance-after",
                        afterDistance),
                    new GameplayCandidateOutcomeFeature(
                        "hostile.distance-improvement",
                        beforeDistance - afterDistance),
                    new GameplayCandidateOutcomeFeature(
                        "concussive.hostiles-in-range-before",
                        concussiveHostilesBefore),
                    new GameplayCandidateOutcomeFeature(
                        "concussive.hostiles-in-range-after",
                        concussiveHostilesAfter),
                    new GameplayCandidateOutcomeFeature(
                        "concussive.hostile-range-gain",
                        Math.Max(
                            0,
                            concussiveHostilesAfter
                                - concussiveHostilesBefore)),
                    new GameplayCandidateOutcomeFeature(
                        "hostile.visibility-before",
                        visibilityBefore),
                    new GameplayCandidateOutcomeFeature(
                        "hostile.visibility-after",
                        visibilityAfter),
                    new GameplayCandidateOutcomeFeature(
                        "hostile.visibility-gain",
                        Math.Max(0f, visibilityAfter - visibilityBefore)),
                }),
                new[] { evidence },
                legal ? route : null);
        }

        private static float NearestHostileDistance(
            ScenarioDefinition scenario,
            GameplaySessionStateSnapshot session,
            string actorId,
            GameplayPosition position)
        {
            ScenarioActorDefinition observer = scenario.GetActor(actorId);
            float nearest = 100000f;
            foreach (GameplayActorSnapshot candidate in session.Actors)
            {
                if (candidate.IsIncapacitated
                    || string.Equals(
                        candidate.ActorId,
                        actorId,
                        StringComparison.Ordinal))
                    continue;
                ScenarioActorDefinition target = scenario.GetActor(
                    candidate.ActorId);
                if (!observer.Combat.IsHostileTo(target.Combat.AllegianceId))
                    continue;
                nearest = Math.Min(
                    nearest,
                    position.DistanceTo(candidate.Pose.Position));
            }
            return nearest;
        }

        private static float MaximumAvailableConcussiveRange(
            ScenarioDefinition scenario,
            GameplayActorSnapshot actor)
        {
            float maximum = 0f;
            foreach (InventoryItemDefinition item in scenario.GetActor(
                actor.ActorId).Inventory)
            {
                if (!(item.ConsumablePower
                        is ThrownExplosiveDefinition explosive)
                    || !explosive.IsConcussive
                    || !actor.Inventory.TryGetQuantity(
                        item.Id,
                        out int quantity)
                    || quantity <= 0)
                    continue;
                maximum = Math.Max(maximum, explosive.MaximumRange);
            }
            return maximum;
        }

        private static int CountHostilesWithinRange(
            ScenarioDefinition scenario,
            GameplaySessionStateSnapshot session,
            string actorId,
            GameplayPosition position,
            float maximumRange)
        {
            if (maximumRange <= 0f) return 0;
            ScenarioActorDefinition observer = scenario.GetActor(actorId);
            int count = 0;
            foreach (GameplayActorSnapshot candidate in session.Actors)
            {
                if (candidate.IsIncapacitated
                    || string.Equals(
                        candidate.ActorId,
                        actorId,
                        StringComparison.Ordinal))
                    continue;
                ScenarioActorDefinition target = scenario.GetActor(
                    candidate.ActorId);
                if (observer.Combat.IsHostileTo(
                        target.Combat.AllegianceId)
                    && position.DistanceTo(candidate.Pose.Position)
                        <= maximumRange + 0.0001f)
                    count++;
            }
            return count;
        }

        private static float CaptureHostileVisibility(
            ScenarioDefinition scenario,
            GameplayCombatStateSnapshot state,
            GameplayHeadlessSpatialEvidence spatial,
            string actorId)
        {
            ScenarioActorDefinition observer = scenario.GetActor(actorId);
            float visibility = 0f;
            foreach (GameplayActorSnapshot candidate in state.Session.Actors)
            {
                if (candidate.IsIncapacitated
                    || string.Equals(
                        candidate.ActorId,
                        actorId,
                        StringComparison.Ordinal))
                    continue;
                ScenarioActorDefinition target = scenario.GetActor(
                    candidate.ActorId);
                if (!observer.Combat.IsHostileTo(
                        target.Combat.AllegianceId))
                    continue;
                visibility += GameplayHeadlessEncounterEvidence.CaptureSight(
                    state,
                    spatial,
                    actorId,
                    candidate.ActorId).VisibleFraction;
            }
            return GameplayNumericPolicy.Normalize(visibility);
        }

        private static bool PosesMatch(
            GameplayActorPose left,
            GameplayActorPose right) => left.Stance == right.Stance
            && GameplayNumericPolicy.AreEquivalent(
                left.FacingDegrees,
                right.FacingDegrees)
            && left.Position.DistanceTo(right.Position) <= 0.0001f;
    }

    public sealed class GameplayTraversalCandidateExecutionRoute :
        IGameplayCandidateExecutionRoute
    {
        public const string Id = "authored-traversal.v1";
        private readonly ScenarioDefinition scenario;
        private readonly GameplayHeadlessSpatialEvidence spatial;

        public GameplayTraversalCandidateExecutionRoute(
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
            && profile.Equals(GameplayCapabilityProfiles.TraversalMove());

        public GameplayExecutableCandidateEvaluation Evaluate(
            GameplayDecisionContext context,
            GameplayCandidate candidate)
        {
            GameplayBasicCandidateRouteUtility.Require(
                context,
                candidate,
                Supports,
                Id);
            GameplayHeadlessTraversalIntent intent = candidate.Intent
                    as GameplayHeadlessTraversalIntent
                ?? throw new ArgumentException(
                    "Traversal candidates require a frozen traversal intent.",
                    nameof(candidate));
            return GameplayMoveCandidateExecutionRouteUtility.Evaluate(
                Id,
                scenario,
                context,
                candidate,
                intent.StateHash,
                intent.Route,
                intent.RouteEvidence,
                intent.FireHazardTraversal,
                requiresTraversal: true,
                spatial);
        }

        public GameplayTransitionPayload PreparePayload(
            GameplayDecisionContext context,
            GameplayExecutableCandidateEvaluation evaluation) =>
            new GameplayMoveTransitionPayload(
                evaluation.Candidate.Profile,
                evaluation.FrozenPreparation as MovementRouteRecord
                    ?? throw new ArgumentException(
                        "Traversal route preparation is missing.",
                        nameof(evaluation)));
    }
}
