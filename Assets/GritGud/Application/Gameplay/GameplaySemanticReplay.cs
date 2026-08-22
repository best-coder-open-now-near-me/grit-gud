using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;

namespace GritGud.Application.Gameplay
{
    public sealed class GameplaySemanticReplayDivergenceException :
        InvalidOperationException
    {
        public GameplaySemanticReplayDivergenceException(
            int frameIndex,
            string reason,
            string expected,
            string actual)
            : base(
                $"Semantic replay diverged at frame {frameIndex} ({reason}).")
        {
            FrameIndex = frameIndex;
            Reason = reason ?? string.Empty;
            Expected = expected ?? string.Empty;
            Actual = actual ?? string.Empty;
        }

        public int FrameIndex { get; }
        public string Reason { get; }
        public string Expected { get; }
        public string Actual { get; }
    }

    public sealed class GameplaySemanticReplayFrame
    {
        internal GameplaySemanticReplayFrame(
            int index,
            GameplayTrajectoryStep step,
            GameplayReductionResult reduction,
            object semanticRecord)
        {
            Index = index;
            Step = step ?? throw new ArgumentNullException(nameof(step));
            Reduction = reduction ?? throw new ArgumentNullException(
                nameof(reduction));
            SemanticRecord = semanticRecord ?? throw new ArgumentNullException(
                nameof(semanticRecord));
        }

        public int Index { get; }
        public GameplayTrajectoryStep Step { get; }
        public GameplaySemanticTransition Transition => Step.Transition;
        public GameplayReductionResult Reduction { get; }
        public GameplayCombatStateSnapshot Previous => Reduction.Previous;
        public GameplayCombatStateSnapshot Resulting => Reduction.Resulting;
        public object SemanticRecord { get; }
    }

    public sealed class GameplaySemanticReplayTimeline
    {
        private readonly IReadOnlyList<GameplaySemanticReplayFrame> frames;

        public GameplaySemanticReplayTimeline(
            GameplayCombatStateSnapshot initialState,
            IEnumerable<GameplayTrajectoryStep> trajectory,
            GameplayTransitionReducerRegistry reducers)
            : this(
                initialState,
                trajectory,
                reducers,
                authoritativeResultingStates: null,
                authoritativeDomainEvents: null)
        {
        }

        internal static GameplaySemanticReplayTimeline FromRecordedArtifact(
            GameplayCombatStateSnapshot initialState,
            IEnumerable<GameplayTrajectoryStep> trajectory,
            IReadOnlyList<GameplayCombatStateSnapshot> resultingStates,
            IReadOnlyList<IReadOnlyList<GameplayDomainEvent>> domainEvents) =>
            new GameplaySemanticReplayTimeline(
                initialState,
                trajectory,
                reducers: null,
                authoritativeResultingStates: resultingStates,
                authoritativeDomainEvents: domainEvents);

        private GameplaySemanticReplayTimeline(
            GameplayCombatStateSnapshot initialState,
            IEnumerable<GameplayTrajectoryStep> trajectory,
            GameplayTransitionReducerRegistry reducers,
            IReadOnlyList<GameplayCombatStateSnapshot>
                authoritativeResultingStates,
            IReadOnlyList<IReadOnlyList<GameplayDomainEvent>>
                authoritativeDomainEvents)
        {
            InitialState = initialState ?? throw new ArgumentNullException(
                nameof(initialState));
            if (trajectory == null)
                throw new ArgumentNullException(nameof(trajectory));
            bool usesRecordedFrames = authoritativeResultingStates != null
                || authoritativeDomainEvents != null;
            if ((authoritativeResultingStates == null)
                != (authoritativeDomainEvents == null))
                throw new ArgumentException(
                    "Authoritative replay states and events must be supplied together.");
            if (reducers == null && !usesRecordedFrames)
                throw new ArgumentNullException(nameof(reducers));
            var built = new List<GameplaySemanticReplayFrame>();
            GameplayCombatStateSnapshot state = InitialState;
            int index = 0;
            foreach (GameplayTrajectoryStep step in trajectory)
            {
                if (step == null)
                    throw new ArgumentException(
                        "Semantic replay trajectories cannot contain null frames.",
                        nameof(trajectory));
                if (!usesRecordedFrames)
                {
                    string payloadDigest = GameplayTransitionPayloadDigest
                        .Calculate(step.Transition);
                    RequireEqual(
                        index,
                        "transition-payload",
                        step.TransitionPayloadDigest,
                        payloadDigest);
                }
                GameplayReductionResult reduction;
                if (usesRecordedFrames)
                {
                    // Portable artifacts round floating-point source values for
                    // canonical storage. Their independently digested states
                    // and events are the playback authority; exact reduction
                    // from unrounded values is proved when the artifact is
                    // generated and verified offline.
                    if (index >= authoritativeResultingStates.Count)
                        throw new ArgumentException(
                            "Authoritative replay states do not cover the trajectory.",
                            nameof(authoritativeResultingStates));
                    if (index >= authoritativeDomainEvents.Count)
                        throw new ArgumentException(
                            "Authoritative replay events do not cover the trajectory.",
                            nameof(authoritativeDomainEvents));
                    GameplayCombatStateSnapshot authoritative =
                        authoritativeResultingStates[index];
                    RequireEqual(
                        index,
                        "artifact-state-hash",
                        step.ResultingStateHash,
                        authoritative.CanonicalHash);
                    reduction = new GameplayReductionResult(
                        state,
                        authoritative,
                        authoritativeDomainEvents[index]);
                }
                else
                {
                    reduction = reducers.Reduce(state, step.Transition);
                    RequireEqual(
                        index,
                        "state-hash",
                        step.ResultingStateHash,
                        reduction.Resulting.CanonicalHash);
                }
                RequireEqual(
                    index,
                    "domain-events",
                    Join(step.DomainEventTypes),
                    Join(reduction.DomainEvents));
                object semanticRecord = RequireSemanticRecord(
                    index,
                    reduction.DomainEvents);
                built.Add(new GameplaySemanticReplayFrame(
                    index,
                    step,
                    reduction,
                    semanticRecord));
                state = reduction.Resulting;
                index++;
            }
            if (authoritativeResultingStates != null
                && authoritativeResultingStates.Count != index)
                throw new ArgumentException(
                    "Authoritative replay states exceed the trajectory.",
                    nameof(authoritativeResultingStates));
            if (authoritativeDomainEvents != null
                && authoritativeDomainEvents.Count != index)
                throw new ArgumentException(
                    "Authoritative replay events exceed the trajectory.",
                    nameof(authoritativeDomainEvents));
            frames = built.AsReadOnly();
            FinalState = state;
        }

        public GameplayCombatStateSnapshot InitialState { get; }
        public GameplayCombatStateSnapshot FinalState { get; }
        public IReadOnlyList<GameplaySemanticReplayFrame> Frames => frames;

        private static object RequireSemanticRecord(
            int frameIndex,
            IReadOnlyList<GameplayDomainEvent> domainEvents)
        {
            GameplayTransitionReducedEvent semantic = null;
            foreach (GameplayDomainEvent domainEvent in domainEvents)
            {
                if (!(domainEvent is GameplayTransitionReducedEvent reduced))
                    continue;
                if (semantic != null)
                    throw new GameplaySemanticReplayDivergenceException(
                        frameIndex,
                        "semantic-record-count",
                        "1",
                        ">1");
                semantic = reduced;
            }
            if (semantic == null)
                throw new GameplaySemanticReplayDivergenceException(
                    frameIndex,
                    "semantic-record-count",
                    "1",
                    "0");
            return semantic.SemanticRecord;
        }

        private static void RequireEqual(
            int frameIndex,
            string reason,
            string expected,
            string actual)
        {
            if (!string.Equals(expected, actual, StringComparison.Ordinal))
                throw new GameplaySemanticReplayDivergenceException(
                    frameIndex,
                    reason,
                    expected,
                    actual);
        }

        private static string Join(IEnumerable<string> values) =>
            string.Join("\n", values);

        private static string Join(
            IEnumerable<GameplayDomainEvent> events)
        {
            var values = new List<string>();
            foreach (GameplayDomainEvent domainEvent in events)
                values.Add(domainEvent.EventType);
            return Join(values);
        }
    }

    public sealed class GameplayPresentationWorldStateSample
    {
        internal GameplayPresentationWorldStateSample(
            GameplaySemanticReplayFrame frame,
            float progress,
            GameplaySessionStateSnapshot session,
            IReadOnlyDictionary<string, GameplayActorSnapshot> actors,
            IReadOnlyList<DestructiblePropSnapshot> destructibles,
            IReadOnlyList<VehicleMomentumState> vehicles,
            IReadOnlyList<ProjectileFlightSnapshot> projectiles,
            IReadOnlyList<SmokeFieldSnapshot> smokeFields,
            IReadOnlyList<FireFieldSnapshot> fireFields,
            IReadOnlyList<DroneSnapshot> drones)
        {
            Frame = frame ?? throw new ArgumentNullException(nameof(frame));
            Progress = progress;
            Session = session ?? throw new ArgumentNullException(
                nameof(session));
            Actors = actors;
            Destructibles = destructibles;
            Vehicles = vehicles;
            Projectiles = projectiles;
            SmokeFields = smokeFields;
            FireFields = fireFields;
            Drones = drones;
        }

        public GameplaySemanticReplayFrame Frame { get; }
        public float Progress { get; }
        public GameplaySessionStateSnapshot Session { get; }
        public IReadOnlyDictionary<string, GameplayActorSnapshot> Actors { get; }
        public IReadOnlyList<DestructiblePropSnapshot> Destructibles { get; }
        public IReadOnlyList<VehicleMomentumState> Vehicles { get; }
        public IReadOnlyList<ProjectileFlightSnapshot> Projectiles { get; }
        public IReadOnlyList<SmokeFieldSnapshot> SmokeFields { get; }
        public IReadOnlyList<FireFieldSnapshot> FireFields { get; }
        public IReadOnlyList<DroneSnapshot> Drones { get; }
    }

    public sealed class GameplaySemanticReplayPlaybackFrame
    {
        internal GameplaySemanticReplayPlaybackFrame(
            GameplaySemanticReplayFrame frame,
            float startSeconds,
            float durationSeconds)
        {
            Frame = frame ?? throw new ArgumentNullException(nameof(frame));
            GameplayNumericPolicy.RequireFinite(
                startSeconds,
                nameof(startSeconds));
            GameplayNumericPolicy.RequireFinite(
                durationSeconds,
                nameof(durationSeconds));
            if (startSeconds < 0f)
                throw new ArgumentOutOfRangeException(nameof(startSeconds));
            if (durationSeconds <= 0f)
                throw new ArgumentOutOfRangeException(nameof(durationSeconds));
            StartSeconds = startSeconds;
            DurationSeconds = durationSeconds;
        }

        public GameplaySemanticReplayFrame Frame { get; }
        public float StartSeconds { get; }
        public float DurationSeconds { get; }
        public float EndSeconds => StartSeconds + DurationSeconds;
    }

    public readonly struct GameplaySemanticReplayPlaybackPosition
    {
        internal GameplaySemanticReplayPlaybackPosition(
            GameplaySemanticReplayPlaybackFrame playbackFrame,
            float progress)
        {
            PlaybackFrame = playbackFrame ?? throw new ArgumentNullException(
                nameof(playbackFrame));
            GameplayNumericPolicy.RequireFinite(progress, nameof(progress));
            Progress = Math.Max(0f, Math.Min(1f, progress));
        }

        public GameplaySemanticReplayPlaybackFrame PlaybackFrame { get; }
        public GameplaySemanticReplayFrame Frame => PlaybackFrame.Frame;
        public float Progress { get; }
    }

    public sealed class GameplaySemanticReplayTurnGroup
    {
        internal GameplaySemanticReplayTurnGroup(
            int index,
            IEnumerable<GameplaySemanticReplayPlaybackFrame> groupedFrames)
        {
            var copied = new List<GameplaySemanticReplayPlaybackFrame>(
                groupedFrames ?? throw new ArgumentNullException(
                    nameof(groupedFrames)));
            if (copied.Count == 0)
                throw new ArgumentException(
                    "Replay turn groups cannot be empty.",
                    nameof(groupedFrames));
            Index = index;
            Frames = copied.AsReadOnly();
            StartSeconds = copied[0].StartSeconds;
            EndSeconds = copied[copied.Count - 1].EndSeconds;
            ActorId = ResolveActorId(copied);
            EndsWithTurnRecord = copied[copied.Count - 1].Frame
                .SemanticRecord is TurnEndRecord;
            ClosureReason = EndsWithTurnRecord
                ? GameplayReplayWindowClosureReason.TurnEnded
                : GameplayReplayWindowClosureReason.ArtifactTerminal;
        }

        public int Index { get; }
        public string ActorId { get; }
        public IReadOnlyList<GameplaySemanticReplayPlaybackFrame> Frames
        {
            get;
        }
        public float StartSeconds { get; }
        public float EndSeconds { get; }
        public float DurationSeconds => EndSeconds - StartSeconds;
        public bool EndsWithTurnRecord { get; }
        public GameplayReplayWindowClosureReason ClosureReason { get; }

        private static string ResolveActorId(
            IReadOnlyList<GameplaySemanticReplayPlaybackFrame> groupedFrames)
        {
            for (int index = groupedFrames.Count - 1; index >= 0; index--)
            {
                GameplaySemanticReplayPlaybackFrame frame =
                    groupedFrames[index];
                if (frame.Frame.SemanticRecord is TurnEndRecord ended)
                    return ended.EndingActorId;
            }
            return groupedFrames[0].Frame.Transition.Identity.ActorId;
        }
    }

    /// <summary>
    /// Presentation-only clock over an exact semantic trajectory. Every
    /// canonical transition retains one ordered frame; only its display
    /// duration is compressed. Durations never enter state hashes or replay
    /// equality.
    /// </summary>
    public sealed class GameplaySemanticReplayPlaybackTimeline
    {
        private readonly IReadOnlyList<GameplaySemanticReplayPlaybackFrame>
            frames;
        private readonly IReadOnlyList<GameplaySemanticReplayTurnGroup>
            turnGroups;

        public GameplaySemanticReplayPlaybackTimeline(
            GameplaySemanticReplayTimeline replay)
        {
            Replay = replay ?? throw new ArgumentNullException(nameof(replay));
            var built = new List<GameplaySemanticReplayPlaybackFrame>(
                replay.Frames.Count);
            float cursor = 0f;
            foreach (GameplaySemanticReplayFrame frame in replay.Frames)
            {
                float duration = GameplaySemanticReplayPresentationTiming
                    .GetDurationSeconds(frame);
                built.Add(new GameplaySemanticReplayPlaybackFrame(
                    frame,
                    cursor,
                    duration));
                cursor += duration;
            }
            frames = built.AsReadOnly();
            turnGroups = BuildTurnGroups(built);
            TotalDurationSeconds = cursor;
        }

        public GameplaySemanticReplayTimeline Replay { get; }
        public IReadOnlyList<GameplaySemanticReplayPlaybackFrame> Frames =>
            frames;
        public IReadOnlyList<GameplaySemanticReplayTurnGroup> TurnGroups =>
            turnGroups;
        public float TotalDurationSeconds { get; }

        public GameplaySemanticReplayPlaybackPosition Locate(
            float timeSeconds)
        {
            if (frames.Count == 0)
                throw new InvalidOperationException(
                    "An empty semantic trajectory has no playback position.");
            GameplayNumericPolicy.RequireFinite(
                timeSeconds,
                nameof(timeSeconds));
            float time = Math.Max(
                0f,
                Math.Min(TotalDurationSeconds, timeSeconds));
            if (time >= TotalDurationSeconds)
                return new GameplaySemanticReplayPlaybackPosition(
                    frames[frames.Count - 1],
                    1f);
            for (int index = frames.Count - 1; index >= 0; index--)
            {
                GameplaySemanticReplayPlaybackFrame frame = frames[index];
                if (time < frame.StartSeconds) continue;
                return new GameplaySemanticReplayPlaybackPosition(
                    frame,
                    (time - frame.StartSeconds) / frame.DurationSeconds);
            }
            return new GameplaySemanticReplayPlaybackPosition(frames[0], 0f);
        }

        public float GetFrameStartSeconds(int index) => frames[index]
            .StartSeconds;

        public float GetFrameEndSeconds(int index) => frames[index]
            .EndSeconds;

        public int LocateTurnGroupIndex(float timeSeconds)
        {
            if (turnGroups.Count == 0)
                throw new InvalidOperationException(
                    "An empty semantic trajectory has no replay turn group.");
            float time = Math.Max(
                0f,
                Math.Min(TotalDurationSeconds, timeSeconds));
            for (int index = turnGroups.Count - 1; index >= 0; index--)
                if (time >= turnGroups[index].StartSeconds)
                    return index;
            return 0;
        }

        private static IReadOnlyList<GameplaySemanticReplayTurnGroup>
            BuildTurnGroups(
                IReadOnlyList<GameplaySemanticReplayPlaybackFrame> playbackFrames)
        {
            var result = new List<GameplaySemanticReplayTurnGroup>();
            var current = new List<GameplaySemanticReplayPlaybackFrame>();
            foreach (GameplaySemanticReplayPlaybackFrame frame in playbackFrames)
            {
                current.Add(frame);
                if (!(frame.Frame.SemanticRecord is TurnEndRecord)) continue;
                result.Add(new GameplaySemanticReplayTurnGroup(
                    result.Count,
                    current));
                current = new List<GameplaySemanticReplayPlaybackFrame>();
            }
            if (current.Count > 0)
                result.Add(new GameplaySemanticReplayTurnGroup(
                    result.Count,
                    current));
            return result.AsReadOnly();
        }
    }

    public static class GameplaySemanticReplayPresentationTiming
    {
        public const float ContactResolutionProgress = 0.4f;
        public const float ActionResolutionProgress = 0.65f;

        public static float GetProjectileImpactProgress(
            ProjectileAdvanceRecord advance)
        {
            if (advance == null) throw new ArgumentNullException(nameof(advance));
            ProjectileImpactRecord impact = advance.Resulting.Impact;
            if (impact == null) return 1f;
            float arrivalWithinAdvance = Math.Max(
                0f,
                impact.ArrivalTurnTime - advance.Previous.ElapsedTurnTime);
            return Clamp(
                arrivalWithinAdvance / advance.RequestedTurnTime,
                0f,
                1f);
        }

        public static float GetActionResolutionProgress(
            GameplayActionRecord action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            foreach (GameplayActionOutcome outcome in action.Outcomes)
                if (outcome is ThrownExplosiveActionOutcome)
                    return GameplayThrownExplosivePresentationTiming
                        .ImpactNormalizedTime;
            foreach (GameplayActionOutcome outcome in action.Outcomes)
                if (outcome is AttackResolvedActionOutcome resolved
                    && resolved.Attack.IsContactAttack)
                    return ContactResolutionProgress;
            return ActionResolutionProgress;
        }

        public static float GetDurationSeconds(
            GameplaySemanticReplayFrame frame)
        {
            if (frame == null) throw new ArgumentNullException(nameof(frame));
            switch (frame.SemanticRecord)
            {
                case MovementRouteRecord movement:
                    return Clamp(
                        movement.TotalPlaybackDurationSeconds,
                        0.3f,
                        4f);
                case ProjectileAdvanceRecord projectile:
                    return Clamp(
                        projectile.RequestedTurnTime * 0.65f,
                        0.2f,
                        1.2f);
                case GameplayActionRecord action:
                    return GetActionDuration(action);
                case VehicleMomentumRecord _:
                    return 0.65f;
                case DroneMoveRecord movement:
                    return Clamp(
                        movement.Origin.DistanceTo(movement.Destination) / 6f,
                        0.3f,
                        1.2f);
                case DroneAttackRecord _:
                case ActorDroneAttackRecord _:
                    return 0.8f;
                case StanceChangeRecord _:
                    return 0.3f;
                case TurnEndRecord _:
                    return 0.15f;
                case EnemyAwarenessTransitionRecord _:
                    return 0.35f;
                case PatrolAdvanceRecord _:
                    return 0.3f;
                case GameplayWorldAdvanceTransitionPayload world:
                    return world.ExplorationPose == null
                        ? 0.08f
                        : Clamp(world.ElapsedSeconds * 0.35f, 0.02f, 0.08f);
                case GameplaySessionControlTransitionPayload _:
                case GameplayEmergencyReactionTransitionPayload _:
                    return 0.2f;
                default:
                    return 0.2f;
            }
        }

        private static float GetActionDuration(GameplayActionRecord action)
        {
            foreach (GameplayActionOutcome outcome in action.Outcomes)
            {
                if (outcome is DisplacementActionOutcome displacement)
                    return Math.Max(
                        0.25f,
                        GameplayDisplacementPresentationTiming
                            .GetDurationSeconds(displacement.Displacement));
            }
            foreach (GameplayActionOutcome outcome in action.Outcomes)
            {
                if (outcome is ProjectileLaunchedActionOutcome) return 0.65f;
                if (outcome is WeaponDischargedActionOutcome) return 0.65f;
                if (outcome is AttackResolvedActionOutcome) return 0.8f;
                if (outcome is ThrownExplosiveActionOutcome)
                    return GameplayThrownExplosivePresentationTiming
                        .TotalSequenceSeconds;
                if (outcome is EquipmentChangedActionOutcome) return 0.4f;
            }
            return 0.35f;
        }

        private static float Clamp(float value, float minimum, float maximum)
            => Math.Max(minimum, Math.Min(maximum, value));
    }

    public static class GameplaySemanticReplaySampler
    {
        public static GameplayPresentationWorldStateSample Sample(
            GameplaySemanticReplayFrame frame,
            float linearProgress)
        {
            if (frame == null) throw new ArgumentNullException(nameof(frame));
            GameplayNumericPolicy.RequireFinite(
                linearProgress,
                nameof(linearProgress));
            float progress = Clamp01(linearProgress);
            bool projectileImpactResolved =
                frame.SemanticRecord is ProjectileAdvanceRecord impactAdvance
                && impactAdvance.Resulting.Impact != null
                && progress >= GameplaySemanticReplayPresentationTiming
                    .GetProjectileImpactProgress(impactAdvance);
            bool droneAttackResolved =
                (frame.SemanticRecord is DroneAttackRecord
                    || frame.SemanticRecord is ActorDroneAttackRecord)
                && progress >= GameplaySemanticReplayPresentationTiming
                    .ActionResolutionProgress;
            GameplayCombatStateSnapshot baseState = progress >= 1f
                    || projectileImpactResolved
                    || droneAttackResolved
                ? frame.Resulting
                : frame.Previous;
            if (frame.SemanticRecord is GameplayActionRecord resolvingAction
                && progress >= GameplaySemanticReplayPresentationTiming
                    .GetActionResolutionProgress(resolvingAction))
            {
                baseState = frame.Resulting;
            }
            var actors = IndexActors(baseState.Session.Actors);
            var destructibles = new List<DestructiblePropSnapshot>(
                baseState.Destructibles);
            var vehicles = new List<VehicleMomentumState>(baseState.Vehicles);
            var projectiles = new List<ProjectileFlightSnapshot>(
                baseState.Projectiles);
            var drones = new List<DroneSnapshot>(baseState.Drones);

            switch (frame.SemanticRecord)
            {
                case MovementRouteRecord movement:
                    SampleMovement(frame, movement, progress, actors);
                    break;
                case GameplayActionRecord action:
                    SampleAction(frame, action, progress, actors, destructibles);
                    break;
                case VehicleMomentumRecord vehicle:
                    ReplaceVehicle(
                        vehicles,
                        SampleVehicle(vehicle, progress));
                    break;
                case ProjectileAdvanceRecord projectile:
                    ReplaceProjectile(
                        projectiles,
                        GameplayProjectilePresentationSampler.Sample(
                            projectile,
                            progress));
                    break;
                case DroneMoveRecord drone:
                    ReplaceDrone(drones, SampleDrone(frame, drone, progress));
                    break;
                case GameplayWorldAdvanceTransitionPayload world
                    when world.ExplorationPose != null:
                    SampleExplorationPose(
                        frame,
                        world.ExplorationPose,
                        progress,
                        actors);
                    break;
            }

            return new GameplayPresentationWorldStateSample(
                frame,
                progress,
                baseState.Session,
                actors,
                destructibles.AsReadOnly(),
                vehicles.AsReadOnly(),
                projectiles.AsReadOnly(),
                baseState.SmokeFields,
                baseState.FireFields,
                drones.AsReadOnly());
        }

        private static void SampleExplorationPose(
            GameplaySemanticReplayFrame frame,
            ExplorationPoseRecord movement,
            float progress,
            IDictionary<string, GameplayActorSnapshot> actors)
        {
            GameplayActorSnapshot actor = frame.Resulting.Session.GetActor(
                movement.ActorId);
            actors[movement.ActorId] = CopyActor(
                actor,
                new GameplayActorPose(
                    Lerp(
                        movement.PreviousPose.Position,
                        movement.ResultingPose.Position,
                        progress),
                    Lerp(
                        movement.PreviousPose.FacingDegrees,
                        movement.ResultingPose.FacingDegrees,
                        progress),
                    progress >= 1f
                        ? movement.ResultingPose.Stance
                        : movement.PreviousPose.Stance));
        }

        private static void SampleMovement(
            GameplaySemanticReplayFrame frame,
            MovementRouteRecord movement,
            float progress,
            IDictionary<string, GameplayActorSnapshot> actors)
        {
            GameplayMovementPresentationSampler.TrySample(
                movement,
                movement.TotalPlaybackDurationSeconds * progress,
                out GameplayMovementPresentationSample sampled);
            GameplayActorSnapshot actor = frame.Resulting.Session.GetActor(
                movement.ActorId);
            actors[movement.ActorId] = CopyActor(
                actor,
                new GameplayActorPose(
                    sampled.Position,
                    sampled.FacingDegrees,
                    actor.Pose.Stance));
        }

        private static void SampleAction(
            GameplaySemanticReplayFrame frame,
            GameplayActionRecord action,
            float progress,
            IDictionary<string, GameplayActorSnapshot> actors,
            IList<DestructiblePropSnapshot> destructibles)
        {
            foreach (GameplayActionOutcome outcome in action.Outcomes)
            {
                if (!(outcome is DisplacementActionOutcome displacement)
                    || !displacement.Displacement.Succeeded)
                    continue;
                DisplacementRecord record = displacement.Displacement;
                if (record.Request.SubjectKind
                    == DisplacementSubjectKind.Combatant)
                {
                    GameplayActorSnapshot actor = actors[
                        record.Request.SubjectId];
                    actors[actor.ActorId] = CopyActor(
                        actor,
                        new GameplayActorPose(
                            Lerp(
                                record.PreviousPosition,
                                record.ResultingPosition,
                                progress),
                            actor.Pose.FacingDegrees,
                            actor.Pose.Stance));
                }
                else
                {
                    ReplaceDestructible(
                        destructibles,
                        SampleDisplacedProp(frame, record, progress));
                }
            }

            foreach (GameplayActionOutcome outcome in action.Outcomes)
            {
                if (!(outcome is ThrownExplosiveActionOutcome thrown))
                    continue;
                ThrownExplosiveRecord record = thrown.Record;
                GameplayActorSnapshot previous = frame.Previous.Session
                    .GetActor(record.ThrowerId);
                GameplayActorSnapshot resulting = frame.Resulting.Session
                    .GetActor(record.ThrowerId);
                ActorTargetFacingActionPhase phase =
                    GameplayThrownExplosivePresentationTiming
                        .CreateFacingPhase(
                            previous.Pose.FacingDegrees,
                            resulting.Pose.FacingDegrees);
                GameplayActorSnapshot actor = actors[record.ThrowerId];
                actors[record.ThrowerId] = CopyActor(
                    actor,
                    new GameplayActorPose(
                        actor.Pose.Position,
                        phase.SampleFacingDegrees(progress),
                        actor.Pose.Stance));
                break;
            }
        }

        private static DestructiblePropSnapshot SampleDisplacedProp(
            GameplaySemanticReplayFrame frame,
            DisplacementRecord movement,
            float progress)
        {
            DestructiblePropSnapshot authoritative = FindDestructible(
                progress >= GameplaySemanticReplayPresentationTiming
                    .ActionResolutionProgress
                    ? frame.Resulting.Destructibles
                    : frame.Previous.Destructibles,
                movement.Request.SubjectId);
            PropDisplacementState previous = movement.PreviousPropState;
            PropDisplacementState next = movement.ResultingPropState;
            return new DestructiblePropSnapshot(
                authoritative.PropId,
                authoritative.State,
                authoritative.MaximumIntegrity,
                authoritative.RemainingIntegrity,
                new GameplayPropPose(
                    Lerp(previous.Pose.Position, next.Pose.Position, progress),
                    Lerp(previous.Pose.PitchDegrees,
                        next.Pose.PitchDegrees, progress),
                    Lerp(previous.Pose.YawDegrees,
                        next.Pose.YawDegrees, progress),
                    Lerp(previous.Pose.RollDegrees,
                        next.Pose.RollDegrees, progress)),
                progress >= GameplaySemanticReplayPresentationTiming
                    .ActionResolutionProgress
                    ? next.Posture
                    : previous.Posture,
                authoritative.FractureChunkCount,
                authoritative.DetachedFractureChunks);
        }

        private static VehicleMomentumState SampleVehicle(
            VehicleMomentumRecord movement,
            float progress) => new VehicleMomentumState(
                movement.Resulting.VehicleId,
                Lerp(
                    movement.Previous.Position,
                    movement.Resulting.Position,
                    progress),
                Lerp(
                    movement.Previous.ForwardDegrees,
                    movement.Resulting.ForwardDegrees,
                    progress),
                Lerp(
                    movement.Previous.Speed,
                    movement.Resulting.Speed,
                    progress));

        private static DroneSnapshot SampleDrone(
            GameplaySemanticReplayFrame frame,
            DroneMoveRecord movement,
            float progress)
        {
            DroneSnapshot previous = FindDrone(
                frame.Previous.Drones,
                movement.DroneId);
            DroneSnapshot resulting = FindDrone(
                frame.Resulting.Drones,
                movement.DroneId);
            return new DroneSnapshot(
                resulting.Definition,
                Lerp(movement.Origin, movement.Destination, progress),
                Lerp(
                    previous.FacingDegrees,
                    movement.ResultingFacingDegrees,
                    progress),
                resulting.RemainingIntegrity);
        }

        private static Dictionary<string, GameplayActorSnapshot> IndexActors(
            IEnumerable<GameplayActorSnapshot> actors)
        {
            var result = new Dictionary<string, GameplayActorSnapshot>(
                StringComparer.Ordinal);
            foreach (GameplayActorSnapshot actor in actors)
                result.Add(actor.ActorId, actor);
            return result;
        }

        private static GameplayActorSnapshot CopyActor(
            GameplayActorSnapshot actor,
            GameplayActorPose pose) => new GameplayActorSnapshot(
                actor.ActorId,
                pose,
                actor.TurnBudget,
                actor.Wounds,
                actor.EquippedItemId,
                actor.EquipmentEffects,
                actor.MaximumWounds,
                actor.Inventory,
                actor.ActionPointEconomy,
                actor.TurnMovementAllowance,
                actor.PinState,
                actor.EmergencyActionPointAllowance,
                actor.SuspendedTurnBudget,
                actor.AttacksCommittedThisTurn,
                actor.Ammunition,
                actor.Injuries);

        private static DestructiblePropSnapshot FindDestructible(
            IEnumerable<DestructiblePropSnapshot> values,
            string id)
        {
            foreach (DestructiblePropSnapshot value in values)
                if (string.Equals(value.PropId, id, StringComparison.Ordinal))
                    return value;
            throw new KeyNotFoundException(
                $"Replay destructible '{id}' is missing.");
        }

        private static DroneSnapshot FindDrone(
            IEnumerable<DroneSnapshot> values,
            string id)
        {
            foreach (DroneSnapshot value in values)
                if (string.Equals(value.DroneId, id, StringComparison.Ordinal))
                    return value;
            throw new KeyNotFoundException($"Replay drone '{id}' is missing.");
        }

        private static void ReplaceDestructible(
            IList<DestructiblePropSnapshot> values,
            DestructiblePropSnapshot replacement)
        {
            for (int index = 0; index < values.Count; index++)
                if (string.Equals(
                        values[index].PropId,
                        replacement.PropId,
                        StringComparison.Ordinal))
                {
                    values[index] = replacement;
                    return;
                }
            throw new KeyNotFoundException(
                $"Replay destructible '{replacement.PropId}' is missing.");
        }

        private static void ReplaceVehicle(
            IList<VehicleMomentumState> values,
            VehicleMomentumState replacement)
        {
            for (int index = 0; index < values.Count; index++)
                if (string.Equals(
                        values[index].VehicleId,
                        replacement.VehicleId,
                        StringComparison.Ordinal))
                {
                    values[index] = replacement;
                    return;
                }
            throw new KeyNotFoundException(
                $"Replay vehicle '{replacement.VehicleId}' is missing.");
        }

        private static void ReplaceProjectile(
            IList<ProjectileFlightSnapshot> values,
            ProjectileFlightSnapshot replacement)
        {
            for (int index = 0; index < values.Count; index++)
                if (string.Equals(
                        values[index].ProjectileId,
                        replacement.ProjectileId,
                        StringComparison.Ordinal))
                {
                    values[index] = replacement;
                    return;
                }
            throw new KeyNotFoundException(
                $"Replay projectile '{replacement.ProjectileId}' is missing.");
        }

        private static void ReplaceDrone(
            IList<DroneSnapshot> values,
            DroneSnapshot replacement)
        {
            for (int index = 0; index < values.Count; index++)
                if (string.Equals(
                        values[index].DroneId,
                        replacement.DroneId,
                        StringComparison.Ordinal))
                {
                    values[index] = replacement;
                    return;
                }
            throw new KeyNotFoundException(
                $"Replay drone '{replacement.DroneId}' is missing.");
        }

        private static GameplayPosition Lerp(
            GameplayPosition from,
            GameplayPosition to,
            float progress) => new GameplayPosition(
                Lerp(from.X, to.X, progress),
                Lerp(from.Y, to.Y, progress),
                Lerp(from.Z, to.Z, progress));

        private static float Lerp(float from, float to, float progress) =>
            from + ((to - from) * progress);

        private static float Clamp01(float value) =>
            Math.Max(0f, Math.Min(1f, value));
    }
}
