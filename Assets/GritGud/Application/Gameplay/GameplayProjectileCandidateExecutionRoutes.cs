using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;

namespace GritGud.Application.Gameplay
{
    public sealed class GameplayProjectileAdvanceIntent
    {
        public GameplayProjectileAdvanceIntent(float turnTime)
        {
            GameplayNumericPolicy.RequireFinite(turnTime, nameof(turnTime));
            if (turnTime <= 0f)
                throw new ArgumentOutOfRangeException(nameof(turnTime));
            TurnTime = turnTime;
        }

        public float TurnTime { get; }
    }

    public sealed class GameplayHeadlessProjectileSegmentQuery :
        IProjectileSegmentQuery
    {
        private readonly GameplayCombatStateSnapshot state;
        private readonly GameplayHeadlessSpatialEvidence spatial;

        public GameplayHeadlessProjectileSegmentQuery(
            GameplayCombatStateSnapshot canonicalState,
            GameplayHeadlessSpatialEvidence spatialEvidence)
        {
            state = canonicalState ?? throw new ArgumentNullException(
                nameof(canonicalState));
            spatial = spatialEvidence ?? throw new ArgumentNullException(
                nameof(spatialEvidence));
        }

        public ProjectileSegmentQueryResult Query(
            ProjectileSegmentQuery query) => spatial.CaptureProjectileSegment(
                state,
                query);
    }

    public static class GameplayProjectilePreparation
    {
        private const float ValueTolerance = 0.0001f;

        public static bool TryPrepareLaunch(
            GameplayCombatStateSnapshot state,
            ScenarioDefinition scenario,
            string actorId,
            string intendedTargetId,
            GameplayPosition aimPoint,
            bool canEnterTurnMode,
            out GameplayActionRecord action,
            out ProjectileLaunchFailure failure)
        {
            action = null;
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (scenario == null)
                throw new ArgumentNullException(nameof(scenario));
            string targetId;
            try
            {
                targetId = GameplayContentIdentity.RequireText(
                    intendedTargetId,
                    nameof(intendedTargetId));
            }
            catch (ArgumentException)
            {
                return Fail(ProjectileLaunchFailure.TargetNotFound, out failure);
            }
            if (!string.Equals(
                    state.Session.ScenarioId,
                    scenario.Id,
                    StringComparison.Ordinal))
                throw new ArgumentException(
                    "Projectile rules and canonical state describe different scenarios.",
                    nameof(scenario));
            if (string.Equals(actorId, targetId, StringComparison.Ordinal))
                return Fail(ProjectileLaunchFailure.TargetNotFound, out failure);

            GameplaySessionStateSnapshot session = state.Session;
            bool startsEncounter = scenario.TryGetAttackResponse(
                    targetId,
                    out AttackResponseDefinition response)
                && response.StartsEncounter;
            bool explorationOpeningAction =
                session.Mode == GameplaySessionMode.Exploration
                && startsEncounter;
            if (session.Mode != GameplaySessionMode.TurnBased
                && !explorationOpeningAction)
                return Fail(
                    ProjectileLaunchFailure.TurnModeRequired,
                    out failure);
            if (explorationOpeningAction && !canEnterTurnMode)
                return Fail(
                    ProjectileLaunchFailure.TurnModeRequired,
                    out failure);
            if (session.Operation != GameplaySessionOperation.None)
                return Fail(
                    ProjectileLaunchFailure.OperationInProgress,
                    out failure);

            GameplayActorSnapshot actor;
            try
            {
                actor = session.GetActor(actorId);
            }
            catch (KeyNotFoundException)
            {
                return Fail(
                    ProjectileLaunchFailure.ActorNotActive,
                    out failure);
            }
            if (session.Mode == GameplaySessionMode.TurnBased
                && !string.Equals(
                    session.ActiveActorId,
                    actorId,
                    StringComparison.Ordinal))
                return Fail(
                    ProjectileLaunchFailure.ActorNotActive,
                    out failure);
            if (actor.IsIncapacitated)
                return Fail(
                    ProjectileLaunchFailure.ActorIncapacitated,
                    out failure);
            if (actor.IsPinned)
                return Fail(ProjectileLaunchFailure.ActorPinned, out failure);

            AttackDefinition weapon = GameplayDirectAttackPreparation
                .GetEquippedAttack(scenario, actor);
            if (weapon == null)
                return Fail(
                    ProjectileLaunchFailure.WeaponUnavailable,
                    out failure);
            if (weapon.Projectile == null)
                return Fail(
                    ProjectileLaunchFailure.ProjectileUnavailable,
                    out failure);
            GameplayPosition origin = weapon.Projectile.GetLaunchOrigin(
                actor.Pose);
            if (origin.DistanceTo(aimPoint) <= ValueTolerance)
                return Fail(
                    ProjectileLaunchFailure.InvalidAimPoint,
                    out failure);
            if (actor.TurnBudget.ActionPoints
                < weapon.TurnCost.ActionPoints)
                return Fail(
                    ProjectileLaunchFailure.InsufficientActionPoints,
                    out failure);
            if (actor.TurnBudget.MovementOpportunity
                < weapon.TurnCost.MovementOpportunity)
                return Fail(
                    ProjectileLaunchFailure.InsufficientMovementOpportunity,
                    out failure);

            long sequence = checked(session.LastActionSequence + 1L);
            TurnBudget resultingBudget = actor.TurnBudget.SpendAction(
                weapon.TurnCost);
            var launch = new ProjectileLaunchRecord(
                sequence,
                "projectile." + sequence,
                actorId,
                targetId,
                weapon.ActionId,
                origin,
                aimPoint,
                weapon.Projectile,
                actor.ActionPointEconomy.IncomePerPersonalTurn,
                resultingBudget.ActionPoints);
            action = new GameplayActionRecord(
                sequence,
                new GameplayActionRequest(
                    actorId,
                    weapon.ActionId,
                    targetId),
                weapon.TurnCost,
                actor.TurnBudget,
                resultingBudget,
                new[] { new ProjectileLaunchedActionOutcome(launch) });
            failure = ProjectileLaunchFailure.None;
            return true;
        }

        public static ProjectileAdvanceRecord PrepareAdvance(
            GameplayCombatStateSnapshot state,
            string projectileId,
            float turnTime,
            IProjectileSegmentQuery segmentQuery)
        {
            ProjectileAdvancePrediction prediction = PredictAdvance(
                state,
                projectileId,
                turnTime,
                segmentQuery);
            ProjectileFlightSnapshot previous = prediction.Previous;
            ProjectileFlightDefinition definition = previous.Launch.Definition;
            float segmentDistance = prediction.SegmentDistance;
            float segmentEndDistance = previous.DistanceTraveled
                + segmentDistance;
            GameplayPosition segmentEnd = prediction.SegmentEnd;
            ProjectileSegmentQueryResult queryResult = prediction.QueryResult;
            if (queryResult.WorldStateRevision
                != state.Session.JournalSequence)
                throw new InvalidOperationException(
                    "Projectile segment evidence is stale.");

            ProjectileFlightSnapshot resulting;
            float? collisionFraction = null;
            if (queryResult.HasCollision)
            {
                collisionFraction = queryResult.CollisionFraction;
                float impactDistance = previous.DistanceTraveled
                    + (segmentDistance * queryResult.CollisionFraction);
                GameplayPosition impactPosition = previous.Launch.GetPosition(
                    impactDistance);
                float arrivalTurnTime = impactDistance
                    / definition.SpeedPerTurn;
                var impact = new ProjectileImpactRecord(
                    projectileId,
                    queryResult.HitEntityId,
                    impactPosition,
                    arrivalTurnTime,
                    queryResult.WorldStateRevision,
                    queryResult.BlastEffects);
                resulting = new ProjectileFlightSnapshot(
                    previous.Launch,
                    impactPosition,
                    impactDistance,
                    arrivalTurnTime,
                    ProjectileFlightStatus.Impacted,
                    impact);
            }
            else
            {
                float elapsed = segmentEndDistance / definition.SpeedPerTurn;
                ProjectileFlightStatus status = Math.Abs(
                        segmentEndDistance - definition.MaximumRange)
                    <= ValueTolerance
                        ? ProjectileFlightStatus.Expired
                        : ProjectileFlightStatus.InFlight;
                resulting = new ProjectileFlightSnapshot(
                    previous.Launch,
                    segmentEnd,
                    segmentEndDistance,
                    elapsed,
                    status);
            }
            return new ProjectileAdvanceRecord(
                checked(state.Session.LastTransitionSequence + 1L),
                previous,
                resulting,
                turnTime,
                segmentEnd,
                queryResult.WorldStateRevision,
                collisionFraction);
        }

        public static ProjectileAdvancePrediction PredictAdvance(
            GameplayCombatStateSnapshot state,
            string projectileId,
            float turnTime,
            IProjectileSegmentQuery segmentQuery)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (segmentQuery == null)
                throw new ArgumentNullException(nameof(segmentQuery));
            GameplayNumericPolicy.RequireFinite(turnTime, nameof(turnTime));
            if (turnTime <= 0f)
                throw new ArgumentOutOfRangeException(nameof(turnTime));
            state.RequireCoverage(GameplayCombatStateCoverage.Projectiles);
            ProjectileFlightSnapshot previous = FindProjectile(
                state.Projectiles,
                projectileId);
            if (previous.Status != ProjectileFlightStatus.InFlight)
                throw new InvalidOperationException(
                    $"Projectile '{projectileId}' is no longer in flight.");
            ProjectileFlightDefinition definition = previous.Launch.Definition;
            double requestedDistance = (double)definition.SpeedPerTurn
                * turnTime;
            float remainingDistance = definition.MaximumRange
                - previous.DistanceTraveled;
            float segmentDistance = (float)Math.Min(
                remainingDistance,
                requestedDistance);
            if (segmentDistance <= ValueTolerance)
                throw new InvalidOperationException(
                    "An in-flight projectile has no remaining segment.");
            float segmentEndDistance = previous.DistanceTraveled
                + segmentDistance;
            GameplayPosition segmentEnd = previous.Launch.GetPosition(
                segmentEndDistance);
            ProjectileSegmentQueryResult queryResult = segmentQuery.Query(
                new ProjectileSegmentQuery(previous, segmentEnd));
            if (!queryResult.IsDefined)
                throw new InvalidOperationException(
                    "Projectile segment queries must return an explicit result.");
            return new ProjectileAdvancePrediction(
                previous,
                turnTime,
                segmentDistance,
                segmentEnd,
                queryResult);
        }

        private static ProjectileFlightSnapshot FindProjectile(
            IEnumerable<ProjectileFlightSnapshot> projectiles,
            string projectileId)
        {
            string id = GameplayContentIdentity.RequireText(
                projectileId,
                nameof(projectileId));
            foreach (ProjectileFlightSnapshot projectile in projectiles)
                if (string.Equals(
                    projectile.ProjectileId,
                    id,
                    StringComparison.Ordinal))
                    return projectile;
            throw new KeyNotFoundException(
                $"Projectile '{id}' is absent from canonical state.");
        }

        private static bool Fail(
            ProjectileLaunchFailure value,
            out ProjectileLaunchFailure failure)
        {
            failure = value;
            return false;
        }
    }

    public sealed class GameplayProjectileLaunchCandidateExecutionRoute :
        IGameplayCandidateExecutionRoute
    {
        public const string Id = "projectile-launch.v1";

        private readonly ScenarioDefinition scenario;
        private readonly GameplayHeadlessSpatialEvidence spatial;

        public GameplayProjectileLaunchCandidateExecutionRoute(
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
                || profile.Capability
                    != GameplaySemanticCapability.LaunchProjectile)
                return false;
            try
            {
                GameplaySemanticSubjectKind subject =
                    GameplayCapabilityProfiles.GetSubjectKind(profile);
                return (subject == GameplaySemanticSubjectKind.Actor
                        || subject
                            == GameplaySemanticSubjectKind.DestructibleProp
                        || subject
                            == GameplaySemanticSubjectKind.WorldPosition)
                    && profile.GetTrait("delivery") == "turn-flight"
                    && profile.GetTrait("targeting") == "semantic-subject"
                    && profile.GetTrait("resource") == "equipped-weapon"
                    && profile.GetTrait("consequence")
                        == "blast-actor-and-destructible"
                    && (profile.GetTrait("emergency") == "opens"
                        || profile.GetTrait("emergency") == "none");
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
            if (attack?.Projectile == null
                || !candidate.Profile.Equals(
                    GameplayCapabilityProfiles.Attack(
                        attack,
                        candidate.SubjectKind)))
                return Illegal(
                    context,
                    candidate,
                    "equipped-profile-mismatch");
            GameplayPosition origin = attack.Projectile.GetLaunchOrigin(
                actor.Pose);
            if (!TryResolveAim(
                    context,
                    candidate,
                    origin,
                    out GameplayPosition aimPoint,
                    out string failure))
                return Illegal(context, candidate, failure);
            if (!GameplayProjectilePreparation.TryPrepareLaunch(
                    context.State,
                    scenario,
                    candidate.ActorId,
                    candidate.SubjectId,
                    aimPoint,
                    canEnterTurnMode: false,
                    out GameplayActionRecord action,
                    out ProjectileLaunchFailure launchFailure))
                return Illegal(
                    context,
                    candidate,
                    "launch." + launchFailure);
            return new GameplayExecutableCandidateEvaluation(
                Id,
                candidate,
                context.State.CanonicalHash,
                isLegal: true,
                failureCode: string.Empty,
                new GameplayCandidateOutcomeEstimate(new[]
                {
                    new GameplayCandidateOutcomeFeature(
                        "cost.action-points",
                        action.Cost.ActionPoints),
                    new GameplayCandidateOutcomeFeature(
                        "cost.movement-opportunity",
                        action.Cost.MovementOpportunity),
                    new GameplayCandidateOutcomeFeature(
                        "projectile.blast-radius",
                        attack.Projectile.BlastRadius),
                    new GameplayCandidateOutcomeFeature(
                        "projectile.launch",
                        1f),
                }),
                new[]
                {
                    spatial.CaptureEvidence(
                        "projectile-launch",
                        context.State,
                        origin,
                        aimPoint),
                },
                action);
        }

        public GameplayTransitionPayload PreparePayload(
            GameplayDecisionContext context,
            GameplayExecutableCandidateEvaluation evaluation) =>
            new GameplayWeaponTransitionPayload(
                evaluation.Candidate.Profile,
                evaluation?.FrozenPreparation as GameplayActionRecord
                    ?? throw new ArgumentException(
                        "Projectile launch preparation is missing.",
                        nameof(evaluation)));

        private bool TryResolveAim(
            GameplayDecisionContext context,
            GameplayCandidate candidate,
            GameplayPosition origin,
            out GameplayPosition aimPoint,
            out string failure)
        {
            switch (candidate.SubjectKind)
            {
                case GameplaySemanticSubjectKind.Actor:
                    if (!context.Observation.ObservesActor(
                        candidate.SubjectId))
                    {
                        aimPoint = default;
                        failure = "target-unobserved";
                        return false;
                    }
                    ScenarioActorDefinition attacker = scenario.GetActor(
                        candidate.ActorId);
                    ScenarioActorDefinition targetDefinition = scenario
                        .GetActor(candidate.SubjectId);
                    if (!attacker.Combat.IsHostileTo(
                        targetDefinition.Combat.AllegianceId))
                    {
                        aimPoint = default;
                        failure = "target-not-hostile";
                        return false;
                    }
                    GameplayActorSnapshot target = context.State.Session
                        .GetActor(candidate.SubjectId);
                    TargetExposureSnapshot exposure =
                        GameplayHeadlessEncounterEvidence.CaptureSight(
                            context.State,
                            spatial,
                            candidate.ActorId,
                            candidate.SubjectId);
                    if (exposure.VisibleSampleCount == 0)
                    {
                        aimPoint = default;
                        failure = "target-not-exposed";
                        return false;
                    }
                    TargetRegionId region = SelectAimRegion(exposure);
                    foreach (TargetRegionSample sample in
                        ActorTargetProfileCatalog.CreateWorldSamples(
                            target.Pose,
                            target.IsPinned))
                        if (sample.Id == region)
                        {
                            aimPoint = sample.Center;
                            failure = string.Empty;
                            return true;
                        }
                    throw new InvalidOperationException(
                        "Projectile target profile omitted the selected region.");
                case GameplaySemanticSubjectKind.DestructibleProp:
                    if (!spatial.TryResolveDestructibleDirectFireImpact(
                        context.State,
                        origin,
                        candidate.SubjectId,
                        out DirectFireImpactRecord impact))
                    {
                        aimPoint = default;
                        failure = "target-not-exposed";
                        return false;
                    }
                    aimPoint = impact.Point;
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

        private static TargetRegionId SelectAimRegion(
            TargetExposureSnapshot exposure)
        {
            TargetRegionExposure best = default;
            bool found = false;
            foreach (TargetRegionExposure region in exposure.Regions)
            {
                if (!region.IsExposed) continue;
                if (!found
                    || region.VisibleFraction > best.VisibleFraction
                    || (region.VisibleFraction == best.VisibleFraction
                        && region.Id == TargetRegionId.Torso))
                {
                    best = region;
                    found = true;
                }
            }
            if (!found)
                throw new InvalidOperationException(
                    "Visible projectile exposure contains no exposed region.");
            return best.Id;
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

    public sealed class GameplayProjectileAdvanceCandidateExecutionRoute :
        IGameplayCandidateExecutionRoute
    {
        public const string Id = "projectile-advance.v1";

        private readonly GameplayHeadlessSpatialEvidence spatial;

        public GameplayProjectileAdvanceCandidateExecutionRoute(
            GameplayHeadlessSpatialEvidence spatialEvidence)
        {
            spatial = spatialEvidence ?? throw new ArgumentNullException(
                nameof(spatialEvidence));
        }

        public string RouteId => Id;

        public bool Supports(GameplayCapabilityProfile profile) =>
            profile != null
            && profile.Equals(GameplayCapabilityProfiles.AdvanceProjectile());

        public GameplayExecutableCandidateEvaluation Evaluate(
            GameplayDecisionContext context,
            GameplayCandidate candidate)
        {
            GameplayBasicCandidateRouteUtility.Require(
                context,
                candidate,
                Supports,
                Id);
            if (candidate.Intent is not GameplayProjectileAdvanceIntent intent)
                return Illegal(
                    context,
                    candidate,
                    "projectile-advance-intent-required");
            if (!IsInFlight(
                context.State.Projectiles,
                candidate.SubjectId))
                return Illegal(
                    context,
                    candidate,
                    "projectile-not-advanceable");
            ProjectileAdvanceRecord record = GameplayProjectilePreparation
                .PrepareAdvance(
                    context.State,
                    candidate.SubjectId,
                    intent.TurnTime,
                    new GameplayHeadlessProjectileSegmentQuery(
                        context.State,
                        spatial));
            int affectedActors = 0;
            int affectedProps = 0;
            foreach (BlastEffectRecord effect in record.Resulting.Impact
                ?.BlastEffects ?? Array.Empty<BlastEffectRecord>())
            {
                if (effect.Exposure <= 0f) continue;
                if (effect.SubjectKind == BlastSubjectKind.Actor)
                    affectedActors++;
                else if (effect.SubjectKind
                    == BlastSubjectKind.DestructibleProp)
                    affectedProps++;
            }
            return new GameplayExecutableCandidateEvaluation(
                Id,
                candidate,
                context.State.CanonicalHash,
                isLegal: true,
                failureCode: string.Empty,
                new GameplayCandidateOutcomeEstimate(new[]
                {
                    new GameplayCandidateOutcomeFeature(
                        "projectile.collision",
                        record.CollisionFraction.HasValue ? 1f : 0f),
                    new GameplayCandidateOutcomeFeature(
                        "projectile.distance",
                        record.Resulting.DistanceTraveled
                            - record.Previous.DistanceTraveled),
                    new GameplayCandidateOutcomeFeature(
                        "blast.affected-actors",
                        affectedActors),
                    new GameplayCandidateOutcomeFeature(
                        "blast.affected-destructibles",
                        affectedProps),
                    new GameplayCandidateOutcomeFeature(
                        "lifecycle.mandatory",
                        1f),
                }),
                new[]
                {
                    spatial.CaptureEvidence(
                        "projectile-segment",
                        context.State,
                        record.SegmentStart,
                        record.SegmentEnd,
                        record.Previous.Launch.Definition.Radius),
                },
                record);
        }

        public GameplayTransitionPayload PreparePayload(
            GameplayDecisionContext context,
            GameplayExecutableCandidateEvaluation evaluation) =>
            new GameplayProjectileAdvanceTransitionPayload(
                evaluation.Candidate.ActorId,
                evaluation?.FrozenPreparation as ProjectileAdvanceRecord
                    ?? throw new ArgumentException(
                        "Projectile advance preparation is missing.",
                        nameof(evaluation)),
                destructiblesShareGameplayJournal: true);

        private static bool IsInFlight(
            IEnumerable<ProjectileFlightSnapshot> projectiles,
            string projectileId)
        {
            foreach (ProjectileFlightSnapshot projectile in projectiles)
                if (string.Equals(
                    projectile.ProjectileId,
                    projectileId,
                    StringComparison.Ordinal))
                    return projectile.Status
                        == ProjectileFlightStatus.InFlight;
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
