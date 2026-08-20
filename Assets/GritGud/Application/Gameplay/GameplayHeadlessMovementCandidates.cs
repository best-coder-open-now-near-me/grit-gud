using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Levels;

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
        private readonly IReadOnlyList<LevelTraversalLinkData> traversalLinks;
        private readonly float maximumCandidateDistance;

        public GameplayHeadlessCandidateBuilder(
            GameplayCapabilityRegistry capabilities,
            GameplayHeadlessSpatialEvidence spatialEvidence,
            float maximumMovementCandidateDistance = 6f,
            ScenarioDefinition scenarioDefinition = null,
            IEnumerable<LevelTraversalLinkData> authoredTraversalLinks = null)
        {
            candidates = new GameplayReachableCandidateBuilder(
                capabilities ?? throw new ArgumentNullException(
                    nameof(capabilities)));
            tacticalCandidates = new GameplayTacticalCandidateBuilder(
                capabilities);
            spatial = spatialEvidence ?? throw new ArgumentNullException(
                nameof(spatialEvidence));
            scenario = scenarioDefinition;
            var links = new List<LevelTraversalLinkData>();
            foreach (LevelTraversalLinkData link in authoredTraversalLinks
                ?? Array.Empty<LevelTraversalLinkData>())
            {
                if (link == null)
                    throw new ArgumentException(
                        "Traversal links cannot contain null entries.",
                        nameof(authoredTraversalLinks));
                links.Add(link.DeepCopy());
            }
            links.Sort((left, right) => StringComparer.Ordinal.Compare(
                left.id,
                right.id));
            traversalLinks = links.AsReadOnly();
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
                // Encounter onset/completion requires an external causal fact
                // (detection, committed hostile action, or no capable hostile).
                // A generic actor policy may never manufacture that fact from
                // the mere existence of a system control input.
                if (input.Kind
                        == GameplayReachableInputKind.SystemContinuation
                    && input.Profile.Capability
                        == GameplaySemanticCapability.ChangeEncounter)
                    continue;
                GameplayReachableInput effectiveInput = input;
                if (!string.Equals(
                        input.ActorId,
                        decidingActorId,
                        StringComparison.Ordinal))
                {
                    if (input.Kind
                        != GameplayReachableInputKind.SystemContinuation)
                        continue;
                    effectiveInput = new GameplayReachableInput(
                        input.Kind,
                        input.SourceId,
                        decidingActorId,
                        input.Profile,
                        input.SubjectIdHint,
                        input.SourceSubjectId);
                }
                if (HasReachedAuthoredAttackLimit(state, effectiveInput))
                    continue;
                if (effectiveInput.Profile.Capability
                    == GameplaySemanticCapability.Move)
                {
                    if (effectiveInput.Profile.Equals(
                        GameplayCapabilityProfiles.GroundedMove()))
                        result.AddRange(BuildGroundedMoves(
                            state,
                            effectiveInput));
                    else if (effectiveInput.Profile.Equals(
                        GameplayCapabilityProfiles.TraversalMove()))
                        result.AddRange(BuildTraversals(
                            state,
                            effectiveInput));
                    else if (effectiveInput.Profile.Equals(
                        GameplayCapabilityProfiles.AerialDroneMove()))
                        result.AddRange(BuildDroneMoves(
                            state,
                            effectiveInput));
                    continue;
                }
                if (effectiveInput.Profile.Equals(
                    GameplayCapabilityProfiles.AdvanceProjectile()))
                {
                    result.AddRange(BuildProjectileAdvances(
                        state,
                        effectiveInput));
                    continue;
                }
                if (effectiveInput.Profile.Capability
                        == GameplaySemanticCapability.Displace
                    && scenario != null)
                {
                    result.AddRange(BuildDisplacements(
                        state,
                        effectiveInput));
                    continue;
                }
                if (effectiveInput.SubjectKind
                        == GameplaySemanticSubjectKind.WorldPosition
                    && scenario != null
                    && (effectiveInput.Profile.Capability
                            == GameplaySemanticCapability.ThrowExplosive
                        || effectiveInput.Profile.Capability
                            == GameplaySemanticCapability.LaunchProjectile
                        || effectiveInput.Profile.Capability
                            == GameplaySemanticCapability.DirectAttack))
                {
                    result.AddRange(BuildWorldPositions(
                        state,
                        effectiveInput));
                    continue;
                }
                result.AddRange(tacticalCandidates.Build(
                    state,
                    new[] { effectiveInput }));
            }
            result.Sort((left, right) => StringComparer.Ordinal.Compare(
                left.CandidateId,
                right.CandidateId));
            return result.AsReadOnly();
        }

        private IEnumerable<GameplayCandidate> BuildTraversals(
            GameplayCombatStateSnapshot state,
            GameplayReachableInput input)
        {
            GameplayActorSnapshot actor = state.Session.GetActor(input.ActorId);
            if (actor.IsPinned || actor.IsIncapacitated) yield break;
            foreach (LevelTraversalLinkData link in traversalLinks)
            {
                foreach (bool reverse in link.bidirectional
                    ? new[] { false, true }
                    : new[] { false })
                {
                    GameplayPosition takeoff = ToPosition(
                        reverse ? link.landing : link.takeoff);
                    GameplayPosition landing = ToPosition(
                        reverse ? link.takeoff : link.landing);
                    if (actor.Pose.Position.DistanceTo(takeoff)
                        > link.activationRadius + 0.0001f)
                        continue;
                    if (!spatial.TryResolveMovementPosition(
                            takeoff,
                            landing,
                            maximumVerticalReach: 1.5f,
                            out GameplayPosition supportedLanding)
                        || supportedLanding.DistanceTo(landing) > 0.15f)
                        continue;
                    var segment = new MovementRouteSegmentRecord(
                        actor.Pose.Position,
                        landing,
                        ParseTraversalKind(link.kind),
                        link.id,
                        link.actionId,
                        link.movementCost,
                        link.actionPointCost,
                        link.arcHeight,
                        link.playbackDurationSeconds);
                    var route = new MovementRouteRecord(
                        actor.ActorId,
                        actor.Pose,
                        actor.TurnBudget,
                        new[] { segment });
                    if (route.TotalActionPointCost
                            > actor.TurnBudget.ActionPoints
                        || route.TotalCost
                            > actor.TurnBudget.MovementOpportunity + 0.0001f
                        || !TraversalClears(state, segment, link))
                        continue;
                    bool actorBlocked = false;
                    foreach (GameplayActorSnapshot other in
                        state.Session.Actors)
                        if (!string.Equals(
                                other.ActorId,
                                actor.ActorId,
                                StringComparison.Ordinal)
                            && !other.IsIncapacitated
                            && other.Pose.Position.DistanceTo(landing) < 0.7f)
                        {
                            actorBlocked = true;
                            break;
                        }
                    if (actorBlocked) continue;
                    GameplayEvidenceRecord evidence = spatial.CaptureEvidence(
                        "authored-traversal",
                        state,
                        actor.Pose.Position,
                        landing,
                        clearanceRadius: 0.3f + link.clearancePadding);
                    float fireHazard = state.Covers(
                            GameplayCombatStateCoverage.FireFields)
                        ? spatial.EvaluateFireHazardTraversal(
                            state,
                            actor.Pose.Position,
                            landing)
                        : 0f;
                    var intent = new GameplayHeadlessTraversalIntent(
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
                        "traverse." + link.id + "."
                            + (reverse ? "reverse" : "forward"));
                }
            }
        }

        private bool TraversalClears(
            GameplayCombatStateSnapshot state,
            MovementRouteSegmentRecord segment,
            LevelTraversalLinkData link)
        {
            const int samples = 12;
            GameplayPosition previous = segment.Sample(0f);
            for (int index = 1; index <= samples; index++)
            {
                GameplayPosition current = segment.Sample(
                    index / (float)samples);
                if (spatial.BlocksPath(
                    state,
                    previous,
                    current,
                    0.3f + link.clearancePadding))
                    return false;
                previous = current;
            }
            return true;
        }

        private static MovementRouteSegmentKind ParseTraversalKind(
            string kind)
        {
            switch (kind?.Trim().ToLowerInvariant())
            {
                case LevelTraversalLinkData.VaultKind:
                    return MovementRouteSegmentKind.Vault;
                case LevelTraversalLinkData.MantleKind:
                    return MovementRouteSegmentKind.Mantle;
                default:
                    return MovementRouteSegmentKind.Jump;
            }
        }

        private static GameplayPosition ToPosition(Float3Data value) =>
            new GameplayPosition(value.x, value.y, value.z);

        private IEnumerable<GameplayCandidate> BuildDisplacements(
            GameplayCombatStateSnapshot state,
            GameplayReachableInput input)
        {
            GameplayActorSnapshot actor = state.Session.GetActor(input.ActorId);
            if (actor.IsIncapacitated) yield break;
            GameplaySemanticSubjectKind requiredKind =
                GameplayCapabilityProfiles.GetSubjectKind(input.Profile);
            DisplacementActionDefinition action = null;
            foreach (DisplacementActionDefinition candidate in scenario
                .GetActor(input.ActorId).DisplacementActions)
                if (input.Profile.Equals(
                    GameplayCapabilityProfiles.Displace(
                        candidate,
                        requiredKind)))
                {
                    action = candidate;
                    break;
                }
            if (action == null) yield break;

            float minimumDistance = action.DistanceDecay?.MinimumDistance
                ?? action.MaximumDistance * 0.5f;
            float[] distances = Math.Abs(
                    minimumDistance - action.MaximumDistance)
                    <= 0.0001f
                ? new[] { action.MaximumDistance }
                : new[]
                {
                    minimumDistance,
                    action.MaximumDistance * 0.5f,
                    action.MaximumDistance,
                };
            var emitted = new HashSet<string>(StringComparer.Ordinal);
            foreach (GameplayTacticalSubject subject in
                GameplayTacticalSubjectCatalog.Discover(state))
            {
                if (subject.Subject.Kind != requiredKind
                    || !subject.Affords(GameplayTacticalAffordance.Displace)
                    || string.Equals(
                        subject.Subject.Id,
                        actor.ActorId,
                        StringComparison.Ordinal)
                    || (input.SubjectIdHint != null
                        && !string.Equals(
                            input.SubjectIdHint,
                            subject.Subject.Id,
                            StringComparison.Ordinal)))
                    continue;
                foreach (float distance in distances)
                foreach (GameplayPosition direction in Directions)
                {
                    double length = Math.Sqrt(
                        (direction.X * direction.X)
                        + (direction.Z * direction.Z));
                    var destination = new GameplayPosition(
                        subject.Position.X
                            + (float)((direction.X / length) * distance),
                        subject.Position.Y,
                        subject.Position.Z
                            + (float)((direction.Z / length) * distance));
                    string key = subject.Subject.Id + "." + Format(destination);
                    if (!emitted.Add(key)) continue;
                    var intent = new GameplayDisplacementIntent(
                        input,
                        state.CanonicalHash,
                        subject.Position,
                        destination);
                    yield return candidates.Build(
                        input,
                        subject.Subject,
                        intent,
                        "displace." + action.Id + "." + key);
                }
            }
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
            {
                ThrownExplosiveDefinition explosive = ResolveThrownExplosive(
                    actor,
                    input);
                foreach (GameplayTacticalSubject subject in
                    GameplayTacticalSubjectCatalog.Discover(state))
                    if (!string.Equals(
                            subject.Subject.Id,
                            actor.ActorId,
                            StringComparison.Ordinal)
                        && actor.Pose.Position.DistanceTo(subject.Position)
                            <= maximumRange + 0.0001f)
                    {
                        requested.Add(subject.Position);
                        float tacticalOffset = ResolveTacticalThrowOffset(
                            explosive);
                        if (tacticalOffset <= 0f) continue;
                        foreach (GameplayPosition direction in Directions)
                        {
                            double length = Math.Sqrt(
                                (direction.X * direction.X)
                                + (direction.Z * direction.Z));
                            requested.Add(new GameplayPosition(
                                subject.Position.X + (float)(
                                    (direction.X / length) * tacticalOffset),
                                subject.Position.Y,
                                subject.Position.Z + (float)(
                                    (direction.Z / length) * tacticalOffset)));
                        }
                    }
            }

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
                        + "." + actor.ActorId + "." + input.SourceId
                        + "." + key);
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
                return ResolveThrownExplosive(actor, input)?.MaximumRange
                    ?? 0f;
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

        private ThrownExplosiveDefinition ResolveThrownExplosive(
            GameplayActorSnapshot actor,
            GameplayReachableInput input)
        {
            foreach (InventoryItemDefinition item in scenario.GetActor(
                actor.ActorId).Inventory)
                if (item.ConsumablePower
                        is ThrownExplosiveDefinition explosive
                    && input.Profile.Equals(
                        GameplayCapabilityProfiles.ThrowExplosive(explosive)))
                    return explosive;
            return null;
        }

        private static float ResolveTacticalThrowOffset(
            ThrownExplosiveDefinition explosive)
        {
            if (explosive == null) return 0f;
            float effectRadius = explosive.BlastRadius;
            if (explosive.SmokeField != null)
                effectRadius = Math.Max(
                    effectRadius,
                    explosive.SmokeField.Radius);
            if (explosive.FireField != null)
                effectRadius = Math.Max(
                    effectRadius,
                    explosive.FireField.MaximumRadius);
            return Math.Min(2f, effectRadius * 0.5f);
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
            EnemyBehaviorDefinition behavior = scenario?.GetActor(
                actor.ActorId).Combat.EnemyBehavior;
            if (behavior != null)
                maximumDistance = Math.Min(
                    maximumDistance,
                    behavior.MovementSearchRadius);
            float[] distances = maximumDistance <= 0.2f
                ? new[] { maximumDistance }
                : new[] { maximumDistance * 0.5f, maximumDistance };
            var destinations = new HashSet<string>(StringComparer.Ordinal);
            var validator = new GameplayHeadlessMovementRouteSegmentValidator(
                state,
                spatial);
            foreach (float distance in distances)
            foreach (GameplayPosition direction in EnumerateTacticalDirections(
                state,
                actor.ActorId,
                actor.Pose.Position))
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
            var destinations = new HashSet<string>(StringComparer.Ordinal);
            foreach (float distance in distances)
            foreach (GameplayPosition direction in EnumerateTacticalDirections(
                state,
                drone.Definition.ControllerActorId,
                drone.Position))
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
                string destinationKey = Format(destination);
                if (!destinations.Add(destinationKey)) continue;
                if (spatial.BlocksPath(
                    state,
                    drone.Position,
                    destination,
                    clearanceRadius: 0.2f)
                    || !DroneRouteClearsActors(
                        state,
                        drone.Position,
                        destination))
                    continue;
                float travelFacing = NormalizeFacing((float)(Math.Atan2(
                    destination.X - drone.Position.X,
                    destination.Z - drone.Position.Z) * 180d / Math.PI));
                GameplayEvidenceRecord evidence = spatial.CaptureEvidence(
                    "drone-route",
                    state,
                    drone.Position,
                    destination,
                    clearanceRadius: 0.2f);
                var facings = new HashSet<string>(StringComparer.Ordinal);
                foreach (float facing in EnumerateDroneFacings(
                    state,
                    drone,
                    destination,
                    travelFacing))
                {
                    string facingKey = GameplayNumericPolicy.FormatCanonical(
                        facing);
                    if (!facings.Add(facingKey)) continue;
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
                            + destinationKey + ".f" + facingKey);
                }
            }
        }

        private IEnumerable<float> EnumerateDroneFacings(
            GameplayCombatStateSnapshot state,
            DroneSnapshot drone,
            GameplayPosition destination,
            float travelFacing)
        {
            yield return travelFacing;
            if (scenario == null) yield break;
            ScenarioActorDefinition controller = scenario.GetActor(
                drone.Definition.ControllerActorId);
            foreach (GameplayActorSnapshot actor in state.Session.Actors)
            {
                if (actor.IsIncapacitated) continue;
                ScenarioActorDefinition target = scenario.GetActor(
                    actor.ActorId);
                if (!controller.Combat.IsHostileTo(
                        target.Combat.AllegianceId))
                    continue;
                yield return NormalizeFacing((float)(Math.Atan2(
                    actor.Pose.Position.X - destination.X,
                    actor.Pose.Position.Z - destination.Z)
                    * 180d / Math.PI));
            }
        }

        private static float NormalizeFacing(float facing)
        {
            float normalized = facing % 360f;
            if (normalized < 0f) normalized += 360f;
            return GameplayNumericPolicy.Normalize(normalized);
        }

        private IEnumerable<GameplayPosition> EnumerateTacticalDirections(
            GameplayCombatStateSnapshot state,
            string actorId,
            GameplayPosition origin)
        {
            var result = new List<GameplayPosition>(Directions);
            if (scenario != null)
            {
                ScenarioActorDefinition observer = scenario.GetActor(actorId);
                foreach (GameplayActorSnapshot actor in state.Session.Actors)
                {
                    if (actor.IsIncapacitated
                        || string.Equals(
                            actor.ActorId,
                            actorId,
                            StringComparison.Ordinal))
                        continue;
                    ScenarioActorDefinition target = scenario.GetActor(
                        actor.ActorId);
                    if (!observer.Combat.IsHostileTo(
                            target.Combat.AllegianceId))
                        continue;
                    float x = actor.Pose.Position.X - origin.X;
                    float z = actor.Pose.Position.Z - origin.Z;
                    double length = Math.Sqrt((x * x) + (z * z));
                    if (length <= 0.0001d) continue;
                    var toward = new GameplayPosition(
                        (float)(x / length),
                        0f,
                        (float)(z / length));
                    var left = new GameplayPosition(
                        -toward.Z,
                        0f,
                        toward.X);
                    var right = new GameplayPosition(
                        toward.Z,
                        0f,
                        -toward.X);
                    result.Add(toward);
                    result.Add(left);
                    result.Add(right);
                    result.Add(NormalizeDirection(toward, left));
                    result.Add(NormalizeDirection(toward, right));
                }
            }
            var unique = new HashSet<string>(StringComparer.Ordinal);
            foreach (GameplayPosition direction in result)
            {
                string key = GameplayNumericPolicy.FormatCanonical(
                        direction.X)
                    + "," + GameplayNumericPolicy.FormatCanonical(direction.Z);
                if (unique.Add(key)) yield return direction;
            }
        }

        private static GameplayPosition NormalizeDirection(
            GameplayPosition first,
            GameplayPosition second)
        {
            float x = first.X + second.X;
            float z = first.Z + second.Z;
            double length = Math.Sqrt((x * x) + (z * z));
            return length <= 0.0001d
                ? first
                : new GameplayPosition(
                    (float)(x / length),
                    0f,
                    (float)(z / length));
        }

        private static bool DroneRouteClearsActors(
            GameplayCombatStateSnapshot state,
            GameplayPosition origin,
            GameplayPosition destination)
        {
            const float separation = 0.65f;
            foreach (GameplayActorSnapshot actor in state.Session.Actors)
            {
                if (actor.IsIncapacitated) continue;
                GameplayPosition position = actor.Pose.Position;
                double x = destination.X - origin.X;
                double y = destination.Y - origin.Y;
                double z = destination.Z - origin.Z;
                double lengthSquared = (x * x) + (y * y) + (z * z);
                double projection = lengthSquared <= 0.00000001d
                    ? 0d
                    : (((position.X - origin.X) * x)
                        + ((position.Y - origin.Y) * y)
                        + ((position.Z - origin.Z) * z)) / lengthSquared;
                projection = Math.Max(0d, Math.Min(1d, projection));
                var nearest = new GameplayPosition(
                    (float)(origin.X + (x * projection)),
                    (float)(origin.Y + (y * projection)),
                    (float)(origin.Z + (z * projection)));
                if (position.DistanceTo(nearest) < separation)
                    return false;
            }
            return true;
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

        private bool HasReachedAuthoredAttackLimit(
            GameplayCombatStateSnapshot state,
            GameplayReachableInput input)
        {
            if (scenario == null
                || (input.Profile.Capability
                        != GameplaySemanticCapability.DirectAttack
                    && input.Profile.Capability
                        != GameplaySemanticCapability.LaunchProjectile))
                return false;
            EnemyBehaviorDefinition behavior = scenario.GetActor(
                input.ActorId).Combat.EnemyBehavior;
            if (behavior == null)
                return false;
            try
            {
                if (!string.Equals(
                        input.Profile.GetTrait("resource"),
                        "equipped-weapon",
                        StringComparison.Ordinal))
                    return false;
            }
            catch (KeyNotFoundException)
            {
                return false;
            }
            return state.Session.GetActor(input.ActorId)
                .AttacksCommittedThisTurn
                >= behavior.MaximumAttacksPerTurn;
        }
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
        private readonly GameplayHeadlessSpatialEvidence spatial;

        public GameplayGroundedMoveCandidateExecutionRoute(
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
            return GameplayMoveCandidateExecutionRouteUtility.Evaluate(
                Id,
                scenario,
                context,
                candidate,
                intent.StateHash,
                intent.Route,
                intent.RouteEvidence,
                intent.FireHazardTraversal,
                requiresTraversal: false,
                spatial);
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

    }
}
