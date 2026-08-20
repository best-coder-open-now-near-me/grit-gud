using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;

namespace GritGud.Application.Gameplay
{
    public sealed class GameplayHeadlessMovementRouteSegmentValidator :
        IMovementRouteSegmentValidator
    {
        private const float StepOffset = 0.35f;
        private const float SlopeLimitDegrees = 50f;
        private const float ProbePadding = 0.12f;
        private const float CapsuleCenterHeight = 0.9f;
        private const float CollisionRadius = 0.3f;
        private const float ActorSeparation = 0.7f;

        private readonly GameplayCombatStateSnapshot state;
        private readonly GameplayHeadlessSpatialEvidence spatial;

        public GameplayHeadlessMovementRouteSegmentValidator(
            GameplayCombatStateSnapshot canonicalState,
            GameplayHeadlessSpatialEvidence spatialEvidence)
        {
            state = canonicalState ?? throw new ArgumentNullException(
                nameof(canonicalState));
            spatial = spatialEvidence ?? throw new ArgumentNullException(
                nameof(spatialEvidence));
        }

        public MovementRouteSegmentValidation Validate(
            string actorId,
            GameplayPosition from,
            GameplayPosition requestedDestination)
        {
            GameplayActorSnapshot actor = state.Session.GetActor(actorId);
            if (actor.IsPinned)
                return MovementRouteSegmentValidation.Rejected(
                    "Pinned actors cannot move.");
            float horizontalDistance = HorizontalDistance(
                from,
                requestedDestination);
            if (horizontalDistance <= 0.0001f)
                return MovementRouteSegmentValidation.Rejected(
                    "Grounded movement requires horizontal displacement.");
            float maximumVerticalReach = Math.Max(
                StepOffset,
                horizontalDistance * (float)Math.Tan(
                    SlopeLimitDegrees * (Math.PI / 180d))) + ProbePadding;
            if (!spatial.TryResolveMovementPosition(
                    from,
                    requestedDestination,
                    maximumVerticalReach,
                    out GameplayPosition resolved))
                return MovementRouteSegmentValidation.Rejected(
                    "No reachable portable ground was found beneath the route.");
            float rise = Math.Abs(resolved.Y - from.Y);
            float slope = (float)(Math.Atan2(rise, horizontalDistance)
                * (180d / Math.PI));
            if (rise > StepOffset + ProbePadding
                && slope > SlopeLimitDegrees + 0.01f)
                return MovementRouteSegmentValidation.Rejected(
                    "The route exceeds the portable slope limit.");

            GameplayPosition fromCenter = AddHeight(
                from,
                CapsuleCenterHeight);
            GameplayPosition resolvedCenter = AddHeight(
                resolved,
                CapsuleCenterHeight);
            if (spatial.BlocksPath(
                    state,
                    fromCenter,
                    resolvedCenter,
                    CollisionRadius))
                return MovementRouteSegmentValidation.Rejected(
                    "Authored or destructible geometry blocks the route.");
            foreach (GameplayActorSnapshot other in state.Session.Actors)
            {
                if (string.Equals(
                        other.ActorId,
                        actorId,
                        StringComparison.Ordinal)
                    || other.IsIncapacitated)
                    continue;
                if (DistanceToSegment(
                        AddHeight(other.Pose.Position, CapsuleCenterHeight),
                        fromCenter,
                        resolvedCenter) < ActorSeparation)
                    return MovementRouteSegmentValidation.Rejected(
                        $"Actor '{other.ActorId}' blocks the route.");
            }
            return MovementRouteSegmentValidation.Accepted(resolved);
        }

        private static float HorizontalDistance(
            GameplayPosition left,
            GameplayPosition right)
        {
            double x = right.X - left.X;
            double z = right.Z - left.Z;
            return (float)Math.Sqrt((x * x) + (z * z));
        }

        private static GameplayPosition AddHeight(
            GameplayPosition position,
            float height) => new GameplayPosition(
                position.X,
                position.Y + height,
                position.Z);

        private static float DistanceToSegment(
            GameplayPosition point,
            GameplayPosition from,
            GameplayPosition to)
        {
            double x = to.X - from.X;
            double y = to.Y - from.Y;
            double z = to.Z - from.Z;
            double lengthSquared = (x * x) + (y * y) + (z * z);
            if (lengthSquared <= 0.00000001d) return point.DistanceTo(from);
            double projection = (
                ((point.X - from.X) * x)
                + ((point.Y - from.Y) * y)
                + ((point.Z - from.Z) * z)) / lengthSquared;
            projection = Math.Max(0d, Math.Min(1d, projection));
            return point.DistanceTo(new GameplayPosition(
                (float)(from.X + (x * projection)),
                (float)(from.Y + (y * projection)),
                (float)(from.Z + (z * projection))));
        }
    }

    public sealed class GameplayHeadlessMovementIntent
    {
        public GameplayHeadlessMovementIntent(
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

    /// <summary>
    /// Expands the semantic Move input into stable route candidates. Direction
    /// sampling is world-relative and policy-neutral; valuation decides which,
    /// if any, route advances a tactic.
    /// </summary>
    public sealed class GameplayHeadlessCandidateBuilder
    {
        private static readonly GameplayPosition[] Directions =
        {
            new GameplayPosition(0f, 0f, 1f),
            new GameplayPosition(1f, 0f, 1f),
            new GameplayPosition(1f, 0f, 0f),
            new GameplayPosition(1f, 0f, -1f),
            new GameplayPosition(0f, 0f, -1f),
            new GameplayPosition(-1f, 0f, -1f),
            new GameplayPosition(-1f, 0f, 0f),
            new GameplayPosition(-1f, 0f, 1f),
        };

        private readonly GameplayReachableCandidateBuilder candidates;
        private readonly GameplayTacticalCandidateBuilder tacticalCandidates;
        private readonly GameplayHeadlessSpatialEvidence spatial;
        private readonly ScenarioDefinition scenario;
        private readonly float maximumCandidateDistance;

        public GameplayHeadlessCandidateBuilder(
            GameplayCapabilityRegistry capabilities,
            GameplayHeadlessSpatialEvidence spatialEvidence,
            float maximumMovementCandidateDistance = 6f,
            ScenarioDefinition scenarioDefinition = null)
        {
            candidates = new GameplayReachableCandidateBuilder(
                capabilities ?? throw new ArgumentNullException(
                    nameof(capabilities)));
            tacticalCandidates = new GameplayTacticalCandidateBuilder(
                capabilities);
            spatial = spatialEvidence ?? throw new ArgumentNullException(
                nameof(spatialEvidence));
            scenario = scenarioDefinition;
            GameplayNumericPolicy.RequireFinite(
                maximumMovementCandidateDistance,
                nameof(maximumMovementCandidateDistance));
            if (maximumMovementCandidateDistance <= 0f)
                throw new ArgumentOutOfRangeException(
                    nameof(maximumMovementCandidateDistance));
            maximumCandidateDistance = maximumMovementCandidateDistance;
        }

        public IReadOnlyList<GameplayCandidate> Build(
            GameplayCombatStateSnapshot state,
            IEnumerable<GameplayReachableInput> reachableInputs,
            string actorId)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            string decidingActorId = GameplayContentIdentity.RequireText(
                actorId,
                nameof(actorId));
            var result = new List<GameplayCandidate>();
            foreach (GameplayReachableInput input in reachableInputs
                ?? throw new ArgumentNullException(nameof(reachableInputs)))
            {
                if (!string.Equals(
                        input.ActorId,
                        decidingActorId,
                        StringComparison.Ordinal))
                    continue;
                if (input.Profile.Capability == GameplaySemanticCapability.Move)
                {
                    if (input.Profile.Equals(
                        GameplayCapabilityProfiles.GroundedMove()))
                        result.AddRange(BuildGroundedMoves(state, input));
                    else if (input.Profile.Equals(
                        GameplayCapabilityProfiles.AerialDroneMove()))
                        result.AddRange(BuildDroneMoves(state, input));
                    continue;
                }
                if (input.Profile.Equals(
                    GameplayCapabilityProfiles.AdvanceProjectile()))
                {
                    result.AddRange(BuildProjectileAdvances(state, input));
                    continue;
                }
                if (input.SubjectKind
                        == GameplaySemanticSubjectKind.WorldPosition
                    && scenario != null
                    && (input.Profile.Capability
                            == GameplaySemanticCapability.ThrowExplosive
                        || input.Profile.Capability
                            == GameplaySemanticCapability.LaunchProjectile
                        || input.Profile.Capability
                            == GameplaySemanticCapability.DirectAttack))
                {
                    result.AddRange(BuildWorldPositions(state, input));
                    continue;
                }
                result.AddRange(tacticalCandidates.Build(
                    state,
                    new[] { input }));
            }
            result.Sort((left, right) => StringComparer.Ordinal.Compare(
                left.CandidateId,
                right.CandidateId));
            return result.AsReadOnly();
        }

        private IEnumerable<GameplayCandidate> BuildProjectileAdvances(
            GameplayCombatStateSnapshot state,
            GameplayReachableInput input)
        {
            var ordered = new List<ProjectileFlightSnapshot>();
            foreach (ProjectileFlightSnapshot projectile in state.Projectiles)
                if (projectile.Status == ProjectileFlightStatus.InFlight)
                    ordered.Add(projectile);
            ordered.Sort((left, right) => StringComparer.Ordinal.Compare(
                left.ProjectileId,
                right.ProjectileId));
            foreach (ProjectileFlightSnapshot projectile in ordered)
            {
                float remainingTurnTime = (projectile.Launch.Definition
                        .MaximumRange - projectile.DistanceTraveled)
                    / projectile.Launch.Definition.SpeedPerTurn;
                float turnTime = Math.Min(1f, remainingTurnTime);
                if (turnTime <= 0f) continue;
                yield return candidates.Build(
                    input,
                    new GameplaySubjectReference(
                        GameplaySemanticSubjectKind.Projectile,
                        projectile.ProjectileId),
                    new GameplayProjectileAdvanceIntent(turnTime),
                    "advance." + projectile.ProjectileId + "."
                        + GameplayNumericPolicy.FormatCanonical(turnTime));
            }
        }

        private IEnumerable<GameplayCandidate> BuildWorldPositions(
            GameplayCombatStateSnapshot state,
            GameplayReachableInput input)
        {
            GameplayActorSnapshot actor = state.Session.GetActor(input.ActorId);
            if (actor.IsIncapacitated || actor.IsPinned) yield break;
            float maximumRange = ResolveMaximumRange(actor, input);
            if (maximumRange <= 0f) yield break;
            var requested = new List<GameplayPosition>();
            float[] distances = maximumRange <= 0.2f
                ? new[] { maximumRange }
                : new[] { maximumRange * 0.5f, maximumRange };
            foreach (float distance in distances)
            foreach (GameplayPosition direction in Directions)
            {
                double length = Math.Sqrt(
                    (direction.X * direction.X)
                    + (direction.Z * direction.Z));
                requested.Add(new GameplayPosition(
                    actor.Pose.Position.X
                        + (float)((direction.X / length) * distance),
                    actor.Pose.Position.Y,
                    actor.Pose.Position.Z
                        + (float)((direction.Z / length) * distance)));
            }
            if (input.Profile.Capability
                == GameplaySemanticCapability.ThrowExplosive)
                foreach (GameplayTacticalSubject subject in
                    GameplayTacticalSubjectCatalog.Discover(state))
                    if (!string.Equals(
                            subject.Subject.Id,
                            actor.ActorId,
                            StringComparison.Ordinal)
                        && actor.Pose.Position.DistanceTo(subject.Position)
                            <= maximumRange + 0.0001f)
                        requested.Add(subject.Position);

            var destinations = new HashSet<string>(StringComparer.Ordinal);
            foreach (GameplayPosition value in requested)
            {
                var atActorHeight = new GameplayPosition(
                    value.X,
                    actor.Pose.Position.Y,
                    value.Z);
                if (!spatial.TryResolveMovementPosition(
                    actor.Pose.Position,
                    atActorHeight,
                    maximumVerticalReach: 12f,
                    out GameplayPosition grounded))
                    continue;
                if (actor.Pose.Position.DistanceTo(grounded)
                    > maximumRange + 0.0001f)
                    continue;
                string key = Format(grounded);
                if (!destinations.Add(key)) continue;
                var intent = new GameplayWorldPositionIntent(grounded);
                yield return candidates.Build(
                    input,
                    new GameplaySubjectReference(
                        GameplaySemanticSubjectKind.WorldPosition,
                        "world." + key),
                    intent,
                    input.Profile.Capability.ToString().ToLowerInvariant()
                        + "." + actor.ActorId + "." + key);
            }
        }

        private float ResolveMaximumRange(
            GameplayActorSnapshot actor,
            GameplayReachableInput input)
        {
            ScenarioActorDefinition definition = scenario.GetActor(
                actor.ActorId);
            if (input.Profile.Capability
                == GameplaySemanticCapability.ThrowExplosive)
            {
                foreach (InventoryItemDefinition item in definition.Inventory)
                    if (item.ConsumablePower
                            is ThrownExplosiveDefinition explosive
                        && input.Profile.Equals(
                            GameplayCapabilityProfiles.ThrowExplosive(
                                explosive)))
                        return explosive.MaximumRange;
                return 0f;
            }
            AttackDefinition attack = definition.Inventory.Count == 0
                ? definition.Attack
                : actor.EquippedItemId == null
                    ? null
                    : definition.GetInventoryItem(actor.EquippedItemId)?.Attack;
            if (attack == null) return 0f;
            if (input.Profile.Capability
                == GameplaySemanticCapability.LaunchProjectile)
                return attack.Projectile?.MaximumRange ?? 0f;
            return 12f;
        }

        private IEnumerable<GameplayCandidate> BuildGroundedMoves(
            GameplayCombatStateSnapshot state,
            GameplayReachableInput input)
        {
            GameplayActorSnapshot actor = state.Session.GetActor(input.ActorId);
            if (actor.IsPinned
                || actor.IsIncapacitated
                || actor.TurnBudget.MovementOpportunity <= 0.0001f)
                yield break;
            float maximumDistance = Math.Min(
                maximumCandidateDistance,
                actor.TurnBudget.MovementOpportunity);
            float[] distances = maximumDistance <= 0.2f
                ? new[] { maximumDistance }
                : new[] { maximumDistance * 0.5f, maximumDistance };
            var destinations = new HashSet<string>(StringComparer.Ordinal);
            var validator = new GameplayHeadlessMovementRouteSegmentValidator(
                state,
                spatial);
            foreach (float distance in distances)
            foreach (GameplayPosition direction in Directions)
            {
                double length = Math.Sqrt(
                    (direction.X * direction.X)
                    + (direction.Z * direction.Z));
                var requested = new GameplayPosition(
                    actor.Pose.Position.X
                        + (float)((direction.X / length) * distance),
                    actor.Pose.Position.Y,
                    actor.Pose.Position.Z
                        + (float)((direction.Z / length) * distance));
                var planner = new MovementRoutePlanner(actor, validator);
                if (!planner.TryAppend(requested, out _)) continue;
                MovementRouteRecord route = planner.Confirm();
                string destinationKey = Format(route.Destination);
                if (!destinations.Add(destinationKey)) continue;
                GameplayEvidenceRecord evidence = spatial.CaptureEvidence(
                    "movement-route",
                    state,
                    actor.Pose.Position,
                    route.Destination,
                    clearanceRadius: 0.3f);
                float fireHazard = state.Covers(
                        GameplayCombatStateCoverage.FireFields)
                    ? spatial.EvaluateFireHazardTraversal(
                        state,
                        actor.Pose.Position,
                        route.Destination)
                    : 0f;
                var intent = new GameplayHeadlessMovementIntent(
                    input,
                    state.CanonicalHash,
                    route,
                    evidence,
                    fireHazard);
                yield return candidates.Build(
                    input,
                    new GameplaySubjectReference(
                        GameplaySemanticSubjectKind.Actor,
                        actor.ActorId),
                    intent,
                    "move." + actor.ActorId + "." + destinationKey);
            }
        }

        private IEnumerable<GameplayCandidate> BuildDroneMoves(
            GameplayCombatStateSnapshot state,
            GameplayReachableInput input)
        {
            state.RequireCoverage(GameplayCombatStateCoverage.Drones);
            DroneSnapshot drone = FindDrone(
                state.Drones,
                input.SourceSubjectId ?? input.SubjectIdHint);
            GameplayActorSnapshot controller = state.Session.GetActor(
                drone.Definition.ControllerActorId);
            if (!drone.IsOperational
                || controller.IsIncapacitated
                || controller.TurnBudget.ActionPoints
                    < drone.Definition.MoveCost.ActionPoints
                || controller.TurnBudget.MovementOpportunity
                    < drone.Definition.MoveCost.MovementOpportunity)
                yield break;
            float maximumDistance = Math.Min(
                maximumCandidateDistance,
                drone.Definition.MaximumMoveDistance);
            float[] distances = maximumDistance <= 0.2f
                ? new[] { maximumDistance }
                : new[] { maximumDistance * 0.5f, maximumDistance };
            foreach (float distance in distances)
            foreach (GameplayPosition direction in Directions)
            {
                double length = Math.Sqrt(
                    (direction.X * direction.X)
                    + (direction.Z * direction.Z));
                var destination = new GameplayPosition(
                    drone.Position.X
                        + (float)((direction.X / length) * distance),
                    drone.Position.Y,
                    drone.Position.Z
                        + (float)((direction.Z / length) * distance));
                if (spatial.BlocksPath(
                    state,
                    drone.Position,
                    destination,
                    clearanceRadius: 0.2f)) continue;
                float facing = (float)(Math.Atan2(
                    destination.X - drone.Position.X,
                    destination.Z - drone.Position.Z) * 180d / Math.PI);
                if (facing < 0f) facing += 360f;
                GameplayEvidenceRecord evidence = spatial.CaptureEvidence(
                    "drone-route",
                    state,
                    drone.Position,
                    destination,
                    clearanceRadius: 0.2f);
                var intent = new GameplayHeadlessDroneMoveIntent(
                    input,
                    state.CanonicalHash,
                    drone.DroneId,
                    drone.Position,
                    destination,
                    facing,
                    evidence);
                yield return candidates.Build(
                    input,
                    new GameplaySubjectReference(
                        GameplaySemanticSubjectKind.Vehicle,
                        drone.DroneId),
                    intent,
                    "drone-move." + drone.DroneId + "."
                        + Format(destination));
            }
        }

        private static DroneSnapshot FindDrone(
            IEnumerable<DroneSnapshot> drones,
            string droneId)
        {
            foreach (DroneSnapshot drone in drones)
                if (string.Equals(
                    drone.DroneId,
                    droneId,
                    StringComparison.Ordinal)) return drone;
            throw new KeyNotFoundException(
                $"Drone '{droneId}' is absent from canonical state.");
        }

        private static string Format(GameplayPosition position) =>
            GameplayNumericPolicy.FormatCanonical(position.X) + ","
            + GameplayNumericPolicy.FormatCanonical(position.Y) + ","
            + GameplayNumericPolicy.FormatCanonical(position.Z);
    }

    public sealed class GameplayHeadlessDroneMoveIntent
    {
        public GameplayHeadlessDroneMoveIntent(
            GameplayReachableInput input,
            string stateHash,
            string droneId,
            GameplayPosition origin,
            GameplayPosition destination,
            float facingDegrees,
            GameplayEvidenceRecord routeEvidence)
        {
            Input = input ?? throw new ArgumentNullException(nameof(input));
            StateHash = GameplayContentIdentity.RequireDigest(
                stateHash,
                nameof(stateHash));
            DroneId = GameplayContentIdentity.RequireText(
                droneId,
                nameof(droneId));
            GameplayNumericPolicy.RequireFinite(
                facingDegrees,
                nameof(facingDegrees));
            Origin = origin;
            Destination = destination;
            FacingDegrees = GameplayNumericPolicy.Normalize(facingDegrees);
            RouteEvidence = routeEvidence ?? throw new ArgumentNullException(
                nameof(routeEvidence));
        }

        public GameplayReachableInput Input { get; }
        public string StateHash { get; }
        public string DroneId { get; }
        public GameplayPosition Origin { get; }
        public GameplayPosition Destination { get; }
        public float FacingDegrees { get; }
        public GameplayEvidenceRecord RouteEvidence { get; }
    }

    public sealed class GameplayGroundedMoveCandidateExecutionRoute :
        IGameplayCandidateExecutionRoute
    {
        public const string Id = "grounded-move.v1";

        private readonly ScenarioDefinition scenario;

        public GameplayGroundedMoveCandidateExecutionRoute(
            ScenarioDefinition scenarioDefinition)
        {
            scenario = scenarioDefinition ?? throw new ArgumentNullException(
                nameof(scenarioDefinition));
        }

        public string RouteId => Id;

        public bool Supports(GameplayCapabilityProfile profile) =>
            profile != null
            && profile.Equals(GameplayCapabilityProfiles.GroundedMove());

        public GameplayExecutableCandidateEvaluation Evaluate(
            GameplayDecisionContext context,
            GameplayCandidate candidate)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            GameplayHeadlessMovementIntent intent = candidate?.Intent
                    as GameplayHeadlessMovementIntent
                ?? throw new ArgumentException(
                    "Grounded movement candidates require a frozen route intent.",
                    nameof(candidate));
            GameplaySessionStateSnapshot session = context.State.Session;
            GameplayActorSnapshot actor = session.GetActor(candidate.ActorId);
            string failure = !string.Equals(
                    intent.StateHash,
                    context.State.CanonicalHash,
                    StringComparison.Ordinal)
                ? "movement-evidence-stale"
                : session.Mode != GameplaySessionMode.TurnBased
                    ? "turn-mode-required"
                    : session.Operation != GameplaySessionOperation.None
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
                                        intent.Route.OriginPose)
                                        ? "movement-origin-stale"
                                        : intent.Route.TotalCost
                                            > actor.TurnBudget
                                                .MovementOpportunity + 0.0001f
                                            ? "movement-unaffordable"
                                            : string.Empty;
            bool legal = failure.Length == 0;
            float beforeDistance = NearestHostileDistance(
                session,
                candidate.ActorId,
                actor.Pose.Position);
            float afterDistance = NearestHostileDistance(
                session,
                candidate.ActorId,
                intent.Route.Destination);
            return new GameplayExecutableCandidateEvaluation(
                Id,
                candidate,
                context.State.CanonicalHash,
                legal,
                failure,
                new GameplayCandidateOutcomeEstimate(new[]
                {
                    new GameplayCandidateOutcomeFeature(
                        "move.distance",
                        intent.Route.TotalCost),
                    new GameplayCandidateOutcomeFeature(
                        "cost.action-points",
                        intent.Route.TotalActionPointCost),
                    new GameplayCandidateOutcomeFeature(
                        "cost.movement-opportunity",
                        intent.Route.TotalCost),
                    new GameplayCandidateOutcomeFeature(
                        "hazard.fire-traversal",
                        intent.FireHazardTraversal),
                    new GameplayCandidateOutcomeFeature(
                        "hostile.distance-before",
                        beforeDistance),
                    new GameplayCandidateOutcomeFeature(
                        "hostile.distance-after",
                        afterDistance),
                    new GameplayCandidateOutcomeFeature(
                        "hostile.distance-improvement",
                        beforeDistance - afterDistance),
                }),
                new[] { intent.RouteEvidence },
                legal ? intent.Route : null);
        }

        public GameplayTransitionPayload PreparePayload(
            GameplayDecisionContext context,
            GameplayExecutableCandidateEvaluation evaluation) =>
            new GameplayMoveTransitionPayload(
                evaluation.Candidate.Profile,
                evaluation.FrozenPreparation as MovementRouteRecord
                    ?? throw new ArgumentException(
                        "Grounded movement route preparation is missing.",
                        nameof(evaluation)));

        private float NearestHostileDistance(
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

        private static bool PosesMatch(
            GameplayActorPose left,
            GameplayActorPose right) => left.Stance == right.Stance
            && GameplayNumericPolicy.AreEquivalent(
                left.FacingDegrees,
                right.FacingDegrees)
            && left.Position.DistanceTo(right.Position) <= 0.0001f;
    }
}
