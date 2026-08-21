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
        InsufficientLoadedAmmunition,
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
        private bool canonicalProjectionBound;
        private Func<
            GameplayTransitionPayload,
            IEnumerable<GameplayEvidenceRecord>,
            GameplayReductionResult> canonicalExecutor;

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

        internal void BindCanonicalExecutor(
            Func<
                GameplayTransitionPayload,
                IEnumerable<GameplayEvidenceRecord>,
                GameplayReductionResult> executor)
        {
            if (executor == null) throw new ArgumentNullException(nameof(executor));
            if (canonicalExecutor != null || canonicalProjectionBound)
                throw new InvalidOperationException(
                    "Projectile semantic executor is already bound or projection binding has started.");
            canonicalExecutor = executor;
        }

        internal void BindCanonicalProjection(
            IReadOnlyList<ProjectileFlightSnapshot> snapshots)
        {
            if (canonicalProjectionBound)
                throw new InvalidOperationException(
                    "Projectiles already have a canonical runtime projection.");
            ValidateCanonicalProjection(snapshots);
            if (snapshots.Count != flights.Count)
                throw new InvalidOperationException(
                    "Projectile session does not match the initial canonical state.");
            foreach (ProjectileFlightSnapshot snapshot in snapshots)
                if (!SnapshotsMatch(flights[snapshot.ProjectileId], snapshot))
                    throw new InvalidOperationException(
                        "Projectile session does not match the initial canonical state.");
            canonicalProjectionBound = true;
        }

        internal void ValidateCanonicalProjection(
            IReadOnlyList<ProjectileFlightSnapshot> snapshots)
        {
            if (snapshots == null)
                throw new ArgumentNullException(nameof(snapshots));
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (ProjectileFlightSnapshot snapshot in snapshots)
            {
                if (!ids.Add(snapshot.ProjectileId))
                    throw new InvalidOperationException(
                        $"Canonical projectile '{snapshot.ProjectileId}' is duplicated.");
                if (flights.TryGetValue(
                        snapshot.ProjectileId,
                        out ProjectileFlightSnapshot current)
                    && !string.Equals(
                        GameplayCanonicalValueDigest.Calculate(current.Launch),
                        GameplayCanonicalValueDigest.Calculate(snapshot.Launch),
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Canonical projectile '{snapshot.ProjectileId}' changed its launch identity.");
                }
            }
            foreach (string projectileId in flights.Keys)
                if (!ids.Contains(projectileId))
                    throw new InvalidOperationException(
                        $"Canonical projection removed projectile '{projectileId}'.");
        }

        internal void ValidateCanonicalProjection(
            IReadOnlyList<ProjectileFlightSnapshot> snapshots,
            object semanticRecord)
        {
            ValidateCanonicalProjection(snapshots);
            foreach (ProjectileFlightSnapshot snapshot in snapshots)
            {
                if (!flights.TryGetValue(
                        snapshot.ProjectileId,
                        out ProjectileFlightSnapshot previous))
                {
                    if (!TryGetLaunch(semanticRecord, out ProjectileLaunchRecord launch)
                        || !string.Equals(
                            GameplayCanonicalValueDigest.Calculate(launch),
                            GameplayCanonicalValueDigest.Calculate(snapshot.Launch),
                            StringComparison.Ordinal))
                        throw new InvalidOperationException(
                            "A new canonical projectile requires its exact launch action.");
                    continue;
                }
                if (SnapshotsMatch(previous, snapshot)) continue;
                if (!(semanticRecord is ProjectileAdvanceRecord advance)
                    || !string.Equals(
                        advance.ProjectileId,
                        snapshot.ProjectileId,
                        StringComparison.Ordinal)
                    || !SnapshotsMatch(advance.Previous, previous)
                    || !SnapshotsMatch(advance.Resulting, snapshot))
                    throw new InvalidOperationException(
                        "A changed canonical projectile requires its exact advance record.");
            }
        }

        internal void InstallCanonicalProjection(
            IReadOnlyList<ProjectileFlightSnapshot> snapshots,
            object semanticRecord)
        {
            if (!canonicalProjectionBound)
                throw new InvalidOperationException(
                    "Projectiles are not bound to a canonical runtime.");
            ValidateCanonicalProjection(snapshots, semanticRecord);
            foreach (ProjectileFlightSnapshot snapshot in snapshots)
            {
                if (!flights.TryGetValue(
                        snapshot.ProjectileId,
                        out ProjectileFlightSnapshot previous))
                {
                    flights.Add(snapshot.ProjectileId, snapshot);
                    launches.Add(snapshot.Launch);
                    continue;
                }
                if (SnapshotsMatch(previous, snapshot)) continue;
                var advance = (ProjectileAdvanceRecord)semanticRecord;
                flights[snapshot.ProjectileId] = snapshot;
                advances.Add(advance);
            }
        }

        private static bool TryGetLaunch(
            object semanticRecord,
            out ProjectileLaunchRecord launch)
        {
            if (semanticRecord is GameplayActionRecord action
                && GameplayWeaponActionOutcomes.TryGetPrimary(
                    action,
                    out ProjectileLaunchedActionOutcome launched))
            {
                launch = launched.Launch;
                return true;
            }
            launch = null;
            return false;
        }

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
            GameplayCombatStateSnapshot previous = CaptureCombatState();
            if (!GameplayProjectilePreparation.TryPrepareLaunch(
                    previous,
                    gameplay.Scenario,
                    actorId,
                    intendedTargetId,
                    aimPoint,
                    out GameplayActionRecord action,
                    out failure))
                return false;
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
            if (!canonicalProjectionBound)
                RequireLegacyMutationAllowed(nameof(CommitLaunch));
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            ProjectileLaunchedActionOutcome outcome =
                GameplayWeaponActionOutcomes
                    .RequirePrimary<ProjectileLaunchedActionOutcome>(action);

            ProjectileLaunchRecord launch = outcome.Launch;
            if (launch.Sequence != action.Sequence
                || !string.Equals(
                    launch.ProjectileId,
                    CreateProjectileId(action.Sequence),
                    StringComparison.Ordinal)
                || flights.ContainsKey(launch.ProjectileId))
            {
                throw new InvalidOperationException(
                    "The projectile launch does not share its canonical action identity.");
            }

            var notifications = new GameplayNotificationBatch();
            gameplay.CommitAction(action, notifications);
            if (canonicalProjectionBound)
            {
                notifications.Publish();
                return;
            }
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
            ProjectileAdvanceRecord record = GameplayProjectilePreparation
                .PrepareAdvance(
                    previousState,
                    projectileId,
                    turnTime,
                    segmentQuery);
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
            float turnTime) => GameplayProjectilePreparation.PredictAdvance(
                CaptureCombatState(),
                projectileId,
                turnTime,
                segmentQuery);

        public void CommitAdvance(ProjectileAdvanceRecord record)
        {
            if (record == null)
            {
                throw new ArgumentNullException(nameof(record));
            }
            if (canonicalProjectionBound)
            {
                canonicalExecutor(new GameplayProjectileAdvanceTransitionPayload(
                        !string.IsNullOrWhiteSpace(gameplay.ActiveActorId)
                            ? gameplay.ActiveActorId
                            : record.Previous.Launch.AttackerId,
                        record,
                        consequences.Destructibles.Journal
                            == gameplay.Journal),
                    null);
                return;
            }
            RequireLegacyMutationAllowed(nameof(CommitAdvance));

            if (record.Sequence != gameplay.LastTransitionSequence + 1L)
            {
                throw new InvalidOperationException(
                    "The projectile advance is not the next canonical transition.");
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

        private void RequireLegacyMutationAllowed(string operation)
        {
            if (canonicalProjectionBound)
                throw new InvalidOperationException(
                    $"Legacy projectile mutation '{operation}' is disabled while the semantic runtime owns state.");
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
