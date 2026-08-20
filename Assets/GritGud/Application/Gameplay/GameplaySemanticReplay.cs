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
        {
            InitialState = initialState ?? throw new ArgumentNullException(
                nameof(initialState));
            if (trajectory == null)
                throw new ArgumentNullException(nameof(trajectory));
            if (reducers == null)
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
                string payloadDigest =
                    GameplayTransitionPayloadDigest.Calculate(step.Transition);
                RequireEqual(
                    index,
                    "transition-payload",
                    step.TransitionPayloadDigest,
                    payloadDigest);
                GameplayReductionResult reduction = reducers.Reduce(
                    state,
                    step.Transition);
                RequireEqual(
                    index,
                    "state-hash",
                    step.ResultingStateHash,
                    reduction.Resulting.CanonicalHash);
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
        public GameplaySessionStateSnapshot Session => Frame.Resulting.Session;
        public IReadOnlyDictionary<string, GameplayActorSnapshot> Actors { get; }
        public IReadOnlyList<DestructiblePropSnapshot> Destructibles { get; }
        public IReadOnlyList<VehicleMomentumState> Vehicles { get; }
        public IReadOnlyList<ProjectileFlightSnapshot> Projectiles { get; }
        public IReadOnlyList<SmokeFieldSnapshot> SmokeFields { get; }
        public IReadOnlyList<FireFieldSnapshot> FireFields { get; }
        public IReadOnlyList<DroneSnapshot> Drones { get; }
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
            GameplayCombatStateSnapshot resulting = frame.Resulting;
            var actors = IndexActors(resulting.Session.Actors);
            var destructibles = new List<DestructiblePropSnapshot>(
                resulting.Destructibles);
            var vehicles = new List<VehicleMomentumState>(resulting.Vehicles);
            var projectiles = new List<ProjectileFlightSnapshot>(
                resulting.Projectiles);
            var drones = new List<DroneSnapshot>(resulting.Drones);

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
                            projectile.Previous,
                            projectile.Resulting,
                            progress));
                    break;
                case DroneMoveRecord drone:
                    ReplaceDrone(drones, SampleDrone(frame, drone, progress));
                    break;
            }

            return new GameplayPresentationWorldStateSample(
                frame,
                progress,
                actors,
                destructibles.AsReadOnly(),
                vehicles.AsReadOnly(),
                projectiles.AsReadOnly(),
                resulting.SmokeFields,
                resulting.FireFields,
                drones.AsReadOnly());
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
        }

        private static DestructiblePropSnapshot SampleDisplacedProp(
            GameplaySemanticReplayFrame frame,
            DisplacementRecord movement,
            float progress)
        {
            DestructiblePropSnapshot resulting = FindDestructible(
                frame.Resulting.Destructibles,
                movement.Request.SubjectId);
            PropDisplacementState previous = movement.PreviousPropState;
            PropDisplacementState next = movement.ResultingPropState;
            return new DestructiblePropSnapshot(
                resulting.PropId,
                resulting.State,
                resulting.MaximumIntegrity,
                resulting.RemainingIntegrity,
                new GameplayPropPose(
                    Lerp(previous.Pose.Position, next.Pose.Position, progress),
                    Lerp(previous.Pose.PitchDegrees,
                        next.Pose.PitchDegrees, progress),
                    Lerp(previous.Pose.YawDegrees,
                        next.Pose.YawDegrees, progress),
                    Lerp(previous.Pose.RollDegrees,
                        next.Pose.RollDegrees, progress)),
                progress >= 1f ? next.Posture : previous.Posture,
                resulting.FractureChunkCount,
                resulting.DetachedFractureChunks);
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
                actor.SuspendedTurnBudget);

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
