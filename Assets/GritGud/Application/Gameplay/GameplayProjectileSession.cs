using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;

namespace GritGud.Application.Gameplay
{
    public enum ProjectileLaunchFailure
    {
        None,
        TurnModeRequired,
        ActorNotActive,
        ActorIncapacitated,
        ActorPinned,
        OperationInProgress,
        WeaponUnavailable,
        ProjectileUnavailable,
        TargetNotFound,
        InvalidAimPoint,
        InsufficientActionPoints,
        InsufficientMovementOpportunity,
    }

    public enum ProjectileLaunchModeRequirement
    {
        None,
        VoluntaryTurnMode,
        Encounter,
    }

    /// <summary>
    /// Side-effect-free evidence for one possible projectile interval. The
    /// world must be queried again when the interval is committed so reactions
    /// can change the eventual collision.
    /// </summary>
    public sealed class ProjectileAdvancePrediction
    {
        internal ProjectileAdvancePrediction(
            ProjectileFlightSnapshot previous,
            float requestedTurnTime,
            float segmentDistance,
            GameplayPosition segmentEnd,
            ProjectileSegmentQueryResult queryResult)
        {
            Previous = previous;
            RequestedTurnTime = requestedTurnTime;
            SegmentDistance = segmentDistance;
            SegmentEnd = segmentEnd;
            QueryResult = queryResult;
        }

        public string ProjectileId => Previous.ProjectileId;

        public ProjectileFlightSnapshot Previous { get; }

        public float RequestedTurnTime { get; }

        public float SegmentDistance { get; }

        public GameplayPosition SegmentEnd { get; }

        public long WorldStateRevision => QueryResult.WorldStateRevision;

        public bool HasCollision => QueryResult.HasCollision;

        public string HitEntityId => QueryResult.HitEntityId;

        public float CollisionFraction => QueryResult.CollisionFraction;

        public float CollisionTurnTime
        {
            get
            {
                if (!HasCollision)
                {
                    throw new InvalidOperationException(
                        "Clear projectile predictions have no collision time.");
                }

                float segmentTurnTime = SegmentDistance
                    / Previous.Launch.Definition.SpeedPerTurn;
                return segmentTurnTime * CollisionFraction;
            }
        }

        public GameplayPosition CollisionPosition
        {
            get
            {
                if (!HasCollision)
                {
                    throw new InvalidOperationException(
                        "Clear projectile predictions have no collision position.");
                }

                return Previous.Launch.GetPosition(
                    Previous.DistanceTraveled
                    + (SegmentDistance * CollisionFraction));
            }
        }

        internal ProjectileSegmentQueryResult QueryResult { get; }
    }

    public sealed class GameplayProjectileSession
    {
        private const float ValueTolerance = 0.0001f;

        private readonly GameplaySession gameplay;
        private readonly IProjectileSegmentQuery segmentQuery;
        private readonly GameplayBlastConsequenceResolver consequences;
        private readonly List<ProjectileLaunchRecord> launches =
            new List<ProjectileLaunchRecord>();
        private readonly List<ProjectileAdvanceRecord> advances =
            new List<ProjectileAdvanceRecord>();
        private readonly Dictionary<string, ProjectileFlightSnapshot> flights =
            new Dictionary<string, ProjectileFlightSnapshot>(StringComparer.Ordinal);
        private readonly IReadOnlyList<ProjectileLaunchRecord> readOnlyLaunches;
        private readonly IReadOnlyList<ProjectileAdvanceRecord> readOnlyAdvances;

        public GameplayProjectileSession(
            GameplaySession gameplaySession,
            IProjectileSegmentQuery query,
            GameplayBlastConsequenceResolver consequenceResolver)
        {
            gameplay = gameplaySession ?? throw new ArgumentNullException(
                nameof(gameplaySession));
            segmentQuery = query ?? throw new ArgumentNullException(nameof(query));
            consequences = consequenceResolver ??
                throw new ArgumentNullException(nameof(consequenceResolver));
            readOnlyLaunches = launches.AsReadOnly();
            readOnlyAdvances = advances.AsReadOnly();
        }

        public GameplayJournal Journal => gameplay.Journal;

        public IReadOnlyList<ProjectileLaunchRecord> Launches => readOnlyLaunches;

        public IReadOnlyList<ProjectileAdvanceRecord> Advances => readOnlyAdvances;

        public IReadOnlyList<string> ProjectileIds
        {
            get
            {
                var ids = new List<string>(flights.Keys);
                ids.Sort(StringComparer.Ordinal);
                return ids.AsReadOnly();
            }
        }

        public bool HasActiveProjectiles
        {
            get
            {
                foreach (ProjectileFlightSnapshot flight in flights.Values)
                {
                    if (flight.Status == ProjectileFlightStatus.InFlight)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public ProjectileLaunchModeRequirement GetLaunchModeRequirement(
            string intendedTargetId)
        {
            if (gameplay.EncounterActive)
            {
                return ProjectileLaunchModeRequirement.None;
            }

            if (gameplay.AttackStartsEncounter(intendedTargetId))
            {
                return ProjectileLaunchModeRequirement.Encounter;
            }

            return gameplay.Mode == GameplaySessionMode.TurnBased
                ? ProjectileLaunchModeRequirement.None
                : ProjectileLaunchModeRequirement.VoluntaryTurnMode;
        }

        public bool TryLaunch(
            string actorId,
            string intendedTargetId,
            GameplayPosition aimPoint,
            out GameplayActionRecord action,
            out ProjectileLaunchFailure failure)
        {
            action = null;
            if (!TryPrepareLaunch(
                    actorId,
                    intendedTargetId,
                    aimPoint,
                    out GameplayPreparedTransition<GameplayActionRecord> prepared,
                    out failure))
                return false;
            action = prepared.Record;
            CommitPreparedLaunch(prepared);
            return true;
        }

        public bool TryPrepareLaunch(
            string actorId,
            string intendedTargetId,
            GameplayPosition aimPoint,
            out GameplayPreparedTransition<GameplayActionRecord> prepared,
            out ProjectileLaunchFailure failure)
        {
            prepared = null;
            if (!TryPrepareLaunch(
                    actorId,
                    intendedTargetId,
                    aimPoint,
                    out AttackDefinition weapon,
                    out GameplayActorSnapshot actor,
                    out failure))
            {
                return false;
            }

            GameplayCombatStateSnapshot previous =
                CaptureCombatState();
            long launchSequence = launches.Count + 1L;
            string projectileId = CreateProjectileId(launchSequence);
            GameplayPosition launchOrigin = weapon.Projectile.GetLaunchOrigin(
                actor.Pose);
            TurnBudget resultingBudget = actor.TurnBudget.SpendAction(
                weapon.TurnCost);
            var launch = new ProjectileLaunchRecord(
                launchSequence,
                projectileId,
                actorId,
                intendedTargetId,
                weapon.ActionId,
                launchOrigin,
                aimPoint,
                weapon.Projectile,
                gameplay.GetTurnActionPointAllowance(actorId),
                resultingBudget.ActionPoints);
            long actionSequence = gameplay.LastResolvedAction == null
                ? 1L
                : gameplay.LastResolvedAction.Sequence + 1L;
            var action = new GameplayActionRecord(
                actionSequence,
                new GameplayActionRequest(
                    actorId,
                    weapon.ActionId,
                    intendedTargetId),
                weapon.TurnCost,
                actor.TurnBudget,
                resultingBudget,
                new[] { new ProjectileLaunchedActionOutcome(launch) });
            prepared = new GameplayPreparedTransition<GameplayActionRecord>(
                action,
                previous,
                GameplayWeaponActionStateProjector.Project(previous, action));
            failure = ProjectileLaunchFailure.None;
            return true;
        }

        public GameplayTransitionCommitResult CommitPreparedLaunch(
            GameplayPreparedTransition<GameplayActionRecord> prepared) =>
            GameplayTransitionCoordinator.Commit(
                prepared,
                CaptureCombatState,
                CommitLaunch);

        public void CommitLaunch(GameplayActionRecord action)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            if (action.Outcomes.Count != 1
                || !(action.Outcomes[0] is ProjectileLaunchedActionOutcome outcome))
            {
                throw new ArgumentException(
                    "Projectile launches require exactly one launch outcome.",
                    nameof(action));
            }

            ProjectileLaunchRecord launch = outcome.Launch;
            long expectedSequence = launches.Count + 1L;
            if (launch.Sequence != expectedSequence
                || !string.Equals(
                    launch.ProjectileId,
                    CreateProjectileId(expectedSequence),
                    StringComparison.Ordinal)
                || flights.ContainsKey(launch.ProjectileId))
            {
                throw new InvalidOperationException(
                    "The projectile launch is not the next authoritative launch.");
            }

            var notifications = new GameplayNotificationBatch();
            gameplay.CommitAction(action, notifications);
            launches.Add(launch);
            flights.Add(
                launch.ProjectileId,
                new ProjectileFlightSnapshot(
                    launch,
                    launch.Origin,
                    distanceTraveled: 0f,
                    elapsedTurnTime: 0f,
                    ProjectileFlightStatus.InFlight));
            notifications.Publish();
        }

        public ProjectileAdvanceRecord Advance(
            string projectileId,
            float turnTime)
        {
            GameplayPreparedTransition<ProjectileAdvanceRecord> prepared =
                PrepareAdvance(projectileId, turnTime);
            CommitPreparedAdvance(prepared);
            return prepared.Record;
        }

        public GameplayPreparedTransition<ProjectileAdvanceRecord> PrepareAdvance(
            string projectileId,
            float turnTime)
        {
            GameplayCombatStateSnapshot previousState = CaptureCombatState();
            ProjectileAdvancePrediction prediction = PredictAdvance(
                projectileId,
                turnTime);
            ProjectileFlightSnapshot previous = prediction.Previous;
            ProjectileFlightDefinition definition = previous.Launch.Definition;
            float segmentDistance = prediction.SegmentDistance;
            float segmentEndDistance = previous.DistanceTraveled
                + segmentDistance;
            ProjectileSegmentQueryResult queryResult = prediction.QueryResult;

            ProjectileFlightSnapshot resulting;
            float? collisionFraction = null;
            if (queryResult.HasCollision)
            {
                collisionFraction = queryResult.CollisionFraction;
                float impactDistance = previous.DistanceTraveled
                    + (segmentDistance * queryResult.CollisionFraction);
                GameplayPosition impactPosition = previous.Launch.GetPosition(
                    impactDistance);
                float arrivalTurnTime = impactDistance / definition.SpeedPerTurn;
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
                float elapsedTurnTime = segmentEndDistance
                    / definition.SpeedPerTurn;
                ProjectileFlightStatus status = Math.Abs(
                    segmentEndDistance - definition.MaximumRange)
                    <= ValueTolerance
                        ? ProjectileFlightStatus.Expired
                        : ProjectileFlightStatus.InFlight;
                resulting = new ProjectileFlightSnapshot(
                    previous.Launch,
                    prediction.SegmentEnd,
                    segmentEndDistance,
                    elapsedTurnTime,
                    status);
            }

            var record = new ProjectileAdvanceRecord(
                advances.Count + 1L,
                previous,
                resulting,
                turnTime,
                prediction.SegmentEnd,
                queryResult.WorldStateRevision,
                collisionFraction);
            return new GameplayPreparedTransition<ProjectileAdvanceRecord>(
                record,
                previousState,
                GameplayProjectileAdvanceStateProjector.Project(
                    previousState,
                    record,
                    consequences.Destructibles.Journal == gameplay.Journal));
        }

        public GameplayTransitionCommitResult CommitPreparedAdvance(
            GameplayPreparedTransition<ProjectileAdvanceRecord> prepared) =>
            GameplayTransitionCoordinator.Commit(
                prepared,
                CaptureCombatState,
                CommitAdvance);

        public ProjectileAdvancePrediction PredictAdvance(
            string projectileId,
            float turnTime)
        {
            if (float.IsNaN(turnTime)
                || float.IsInfinity(turnTime)
                || turnTime <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(turnTime));
            }

            ProjectileFlightSnapshot previous = GetProjectile(projectileId);
            if (previous.Status != ProjectileFlightStatus.InFlight)
            {
                throw new InvalidOperationException(
                    $"Projectile '{projectileId}' is no longer in flight.");
            }

            ProjectileFlightDefinition definition = previous.Launch.Definition;
            double requestedDistance = (double)definition.SpeedPerTurn * turnTime;
            float remainingDistance = definition.MaximumRange
                - previous.DistanceTraveled;
            float segmentDistance = (float)Math.Min(
                remainingDistance,
                requestedDistance);
            float segmentEndDistance = previous.DistanceTraveled + segmentDistance;
            GameplayPosition segmentEnd = previous.Launch.GetPosition(
                segmentEndDistance);
            var query = new ProjectileSegmentQuery(previous, segmentEnd);
            ProjectileSegmentQueryResult queryResult = segmentQuery.Query(query);
            if (!queryResult.IsDefined)
            {
                throw new InvalidOperationException(
                    "Projectile segment queries must return an explicit result.");
            }

            return new ProjectileAdvancePrediction(
                previous,
                turnTime,
                segmentDistance,
                segmentEnd,
                queryResult);
        }

        public void CommitAdvance(ProjectileAdvanceRecord record)
        {
            if (record == null)
            {
                throw new ArgumentNullException(nameof(record));
            }

            if (record.Sequence != advances.Count + 1L)
            {
                throw new InvalidOperationException(
                    "The projectile advance is not the next authoritative advance.");
            }

            ProjectileFlightSnapshot current = GetProjectile(record.ProjectileId);
            if (!SnapshotsMatch(current, record.Previous))
            {
                throw new InvalidOperationException(
                    "The projectile advance no longer starts at authoritative state.");
            }

            ProjectileImpactRecord impact = record.Resulting.Impact;
            if (impact != null)
            {
                consequences.Validate(
                    impact.BlastEffects,
                    record.Resulting.Launch.Definition
                        .BlastWoundMovementPenalty,
                    record.Resulting.Launch.Definition.BlastIntegrityDamage);
            }

            flights[record.ProjectileId] = record.Resulting;
            advances.Add(record);
            gameplay.Journal.RecordProjectileAdvanced(record);
            if (impact != null)
            {
                consequences.Apply(
                    impact.BlastEffects,
                    record.Resulting.Launch.Definition
                        .BlastWoundMovementPenalty,
                    record.Resulting.Launch.Definition.BlastIntegrityDamage);
            }
        }

        public ProjectileFlightSnapshot GetProjectile(string projectileId)
        {
            if (string.IsNullOrWhiteSpace(projectileId))
            {
                throw new ArgumentException(
                    "Projectile identifiers cannot be empty.",
                    nameof(projectileId));
            }

            if (!flights.TryGetValue(projectileId, out ProjectileFlightSnapshot flight))
            {
                throw new KeyNotFoundException(
                    $"Projectile '{projectileId}' has not been launched.");
            }

            return flight;
        }

        private GameplayCombatStateSnapshot CaptureCombatState() =>
            GameplayCombatStateCapture.Capture(
                gameplay,
                consequences.Destructibles,
                projectiles: this);

        private bool TryPrepareLaunch(
            string actorId,
            string intendedTargetId,
            GameplayPosition aimPoint,
            out AttackDefinition weapon,
            out GameplayActorSnapshot actor,
            out ProjectileLaunchFailure failure)
        {
            weapon = null;
            actor = default;
            if (string.IsNullOrWhiteSpace(intendedTargetId)
                || string.Equals(
                    actorId,
                    intendedTargetId,
                    StringComparison.Ordinal))
            {
                failure = ProjectileLaunchFailure.TargetNotFound;
                return false;
            }

            bool startsEncounter = gameplay.AttackStartsEncounter(
                intendedTargetId);
            bool explorationOpeningAction =
                gameplay.Mode == GameplaySessionMode.Exploration
                && startsEncounter;
            if (gameplay.Mode != GameplaySessionMode.TurnBased
                && !explorationOpeningAction)
            {
                failure = ProjectileLaunchFailure.TurnModeRequired;
                return false;
            }

            if (explorationOpeningAction && !gameplay.CanEnterTurnMode)
            {
                failure = ProjectileLaunchFailure.TurnModeRequired;
                return false;
            }

            if (gameplay.Operation != GameplaySessionOperation.None)
            {
                failure = ProjectileLaunchFailure.OperationInProgress;
                return false;
            }

            if ((gameplay.Mode == GameplaySessionMode.TurnBased
                    && !string.Equals(
                        gameplay.ActiveActorId,
                        actorId,
                        StringComparison.Ordinal))
                || !gameplay.TryGetActor(actorId, out actor))
            {
                failure = ProjectileLaunchFailure.ActorNotActive;
                return false;
            }

            if (gameplay.IsActorIncapacitated(actorId))
            {
                failure = ProjectileLaunchFailure.ActorIncapacitated;
                return false;
            }
            if (actor.IsPinned)
            {
                failure = ProjectileLaunchFailure.ActorPinned;
                return false;
            }

            weapon = gameplay.GetEquippedAttack(actorId);
            if (weapon == null)
            {
                failure = ProjectileLaunchFailure.WeaponUnavailable;
                return false;
            }

            if (weapon.Projectile == null)
            {
                failure = ProjectileLaunchFailure.ProjectileUnavailable;
                return false;
            }

            GameplayPosition launchOrigin = weapon.Projectile
                .GetLaunchOrigin(actor.Pose);
            if (launchOrigin.DistanceTo(aimPoint) <= ValueTolerance)
            {
                failure = ProjectileLaunchFailure.InvalidAimPoint;
                return false;
            }

            ActionCost cost = weapon.TurnCost;
            if (actor.TurnBudget.ActionPoints < cost.ActionPoints)
            {
                failure = ProjectileLaunchFailure.InsufficientActionPoints;
                return false;
            }

            if (actor.TurnBudget.MovementOpportunity < cost.MovementOpportunity)
            {
                failure = ProjectileLaunchFailure.InsufficientMovementOpportunity;
                return false;
            }

            failure = ProjectileLaunchFailure.None;
            return true;
        }

        private static string CreateProjectileId(long sequence) =>
            "projectile." + sequence;

        private static bool SnapshotsMatch(
            ProjectileFlightSnapshot left,
            ProjectileFlightSnapshot right)
        {
            if (left.Launch == null
                || right.Launch == null
                || left.Launch.Sequence != right.Launch.Sequence
                || !string.Equals(
                    left.ProjectileId,
                    right.ProjectileId,
                    StringComparison.Ordinal)
                || left.Position.DistanceTo(right.Position) > ValueTolerance
                || Math.Abs(left.DistanceTraveled - right.DistanceTraveled)
                    > ValueTolerance
                || Math.Abs(left.ElapsedTurnTime - right.ElapsedTurnTime)
                    > ValueTolerance
                || left.Status != right.Status)
            {
                return false;
            }

            if (left.Impact == null || right.Impact == null)
            {
                return left.Impact == null && right.Impact == null;
            }

            return string.Equals(
                    left.Impact.HitEntityId,
                    right.Impact.HitEntityId,
                    StringComparison.Ordinal)
                && left.Impact.WorldStateRevision
                    == right.Impact.WorldStateRevision;
        }
    }
}
