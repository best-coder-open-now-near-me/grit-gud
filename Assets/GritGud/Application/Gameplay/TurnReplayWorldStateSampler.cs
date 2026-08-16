using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;

namespace GritGud.Application.Gameplay
{
    public sealed class TurnReplayWorldStateSample
    {
        public TurnReplayWorldStateSample(
            IReadOnlyDictionary<string, GameplayActorSnapshot> actors,
            IReadOnlyList<DestructiblePropSnapshot> destructibles,
            IReadOnlyList<VehicleMomentumState> vehicles,
            IReadOnlyList<ProjectileFlightSnapshot> projectiles,
            IReadOnlyList<SmokeFieldSnapshot> smokeFields)
        {
            Actors = actors ?? throw new ArgumentNullException(nameof(actors));
            Destructibles = destructibles ?? throw new ArgumentNullException(
                nameof(destructibles));
            Vehicles = vehicles ?? throw new ArgumentNullException(nameof(vehicles));
            Projectiles = projectiles ?? throw new ArgumentNullException(
                nameof(projectiles));
            SmokeFields = smokeFields ?? throw new ArgumentNullException(
                nameof(smokeFields));
        }

        public IReadOnlyDictionary<string, GameplayActorSnapshot> Actors { get; }
        public IReadOnlyList<DestructiblePropSnapshot> Destructibles { get; }
        public IReadOnlyList<VehicleMomentumState> Vehicles { get; }
        public IReadOnlyList<ProjectileFlightSnapshot> Projectiles { get; }
        public IReadOnlyList<SmokeFieldSnapshot> SmokeFields { get; }
    }

    public static class TurnReplayWorldStateSampler
    {
        public static TurnReplayWorldStateSample Sample(
            TurnReplayStateWindow window,
            float playhead)
        {
            if (window == null) throw new ArgumentNullException(nameof(window));
            float clamped = Math.Max(
                0f,
                Math.Min(window.Replay.Segments.Count, playhead));
            int segmentIndex = Math.Min(
                window.Replay.Segments.Count - 1,
                (int)Math.Floor(clamped));
            float progress = clamped >= window.Replay.Segments.Count
                ? 1f
                : clamped - segmentIndex;
            GameplayCombatStateSnapshot before = segmentIndex == 0
                ? window.Start.State
                : window.SegmentEnds[segmentIndex - 1].State;
            GameplayCombatStateSnapshot after =
                window.SegmentEnds[segmentIndex].State;
            GameplayCombatStateSnapshot persistent = progress >= 1f
                ? after
                : before;

            var finalPoses = new Dictionary<string, GameplayActorPose>(
                StringComparer.Ordinal);
            foreach (GameplayActorSnapshot actor in window.End.State.Session.Actors)
                finalPoses.Add(actor.ActorId, actor.Pose);
            IReadOnlyDictionary<string, GameplayActorPose> poses =
                TurnReplayPoseProjector.Project(
                    window.Replay,
                    finalPoses,
                    clamped);
            var actors = new Dictionary<string, GameplayActorSnapshot>(
                StringComparer.Ordinal);
            foreach (GameplayActorSnapshot actor in persistent.Session.Actors)
                actors.Add(actor.ActorId, CopyActor(actor, poses[actor.ActorId]));

            return new TurnReplayWorldStateSample(
                actors,
                persistent.Destructibles,
                SampleVehicles(before.Vehicles, after.Vehicles, progress),
                SampleProjectiles(before.Projectiles, after.Projectiles, progress),
                persistent.SmokeFields);
        }

        public static TurnReplayWorldStateSample SampleAtTime(
            TurnReplayStateWindow window,
            TurnReplayEventTimeline timeline,
            float timeSeconds)
        {
            if (timeline == null) throw new ArgumentNullException(nameof(timeline));
            if (!ReferenceEquals(window?.Replay, timeline.Replay))
                throw new ArgumentException(
                    "The event timeline must describe the sampled replay window.",
                    nameof(timeline));
            return Sample(window, timeline.ToSegmentPlayhead(timeSeconds));
        }

        private static IReadOnlyList<ProjectileFlightSnapshot> SampleProjectiles(
            IReadOnlyList<ProjectileFlightSnapshot> before,
            IReadOnlyList<ProjectileFlightSnapshot> after,
            float progress)
        {
            if (progress >= 1f) return after;
            var beforeIndex = new Dictionary<string, ProjectileFlightSnapshot>(
                StringComparer.Ordinal);
            foreach (ProjectileFlightSnapshot flight in before)
                beforeIndex.Add(flight.ProjectileId, flight);
            var result = new List<ProjectileFlightSnapshot>();
            foreach (ProjectileFlightSnapshot target in after)
            {
                ProjectileFlightSnapshot source;
                if (!beforeIndex.TryGetValue(target.ProjectileId, out source))
                {
                    if (progress <= 0f) continue;
                    source = new ProjectileFlightSnapshot(
                        target.Launch,
                        target.Launch.Origin,
                        0f,
                        0f,
                        ProjectileFlightStatus.InFlight);
                }
                else if (source.Status != ProjectileFlightStatus.InFlight)
                {
                    result.Add(source);
                    beforeIndex.Remove(target.ProjectileId);
                    continue;
                }
                float distance = source.DistanceTraveled
                    + ((target.DistanceTraveled - source.DistanceTraveled)
                        * progress);
                ProjectileFlightStatus status = progress >= 1f
                    ? target.Status
                    : ProjectileFlightStatus.InFlight;
                result.Add(new ProjectileFlightSnapshot(
                    target.Launch,
                    target.Launch.GetPosition(distance),
                    distance,
                    distance / target.Launch.Definition.SpeedPerTurn,
                    status));
                beforeIndex.Remove(target.ProjectileId);
            }
            foreach (ProjectileFlightSnapshot unchanged in beforeIndex.Values)
                result.Add(unchanged);
            result.Sort((left, right) => StringComparer.Ordinal.Compare(
                left.ProjectileId,
                right.ProjectileId));
            return result.AsReadOnly();
        }

        private static IReadOnlyList<VehicleMomentumState> SampleVehicles(
            IReadOnlyList<VehicleMomentumState> before,
            IReadOnlyList<VehicleMomentumState> after,
            float progress)
        {
            if (progress <= 0f) return before;
            if (progress >= 1f) return after;
            var previous = new Dictionary<string, VehicleMomentumState>(
                StringComparer.Ordinal);
            foreach (VehicleMomentumState vehicle in before)
                previous.Add(vehicle.VehicleId, vehicle);
            var result = new List<VehicleMomentumState>(after.Count);
            foreach (VehicleMomentumState target in after)
            {
                if (!previous.TryGetValue(target.VehicleId, out VehicleMomentumState source))
                {
                    result.Add(target);
                    continue;
                }
                result.Add(new VehicleMomentumState(
                    target.VehicleId,
                    Lerp(source.Position, target.Position, progress),
                    LerpAngle(source.ForwardDegrees, target.ForwardDegrees, progress),
                    source.Speed + ((target.Speed - source.Speed) * progress)));
            }
            return result.AsReadOnly();
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
                actor.TurnActionPointAllowance,
                actor.TurnMovementAllowance);

        private static GameplayPosition Lerp(
            GameplayPosition from,
            GameplayPosition to,
            float progress) => new GameplayPosition(
                from.X + ((to.X - from.X) * progress),
                from.Y + ((to.Y - from.Y) * progress),
                from.Z + ((to.Z - from.Z) * progress));

        private static float LerpAngle(float from, float to, float progress)
        {
            float delta = ((to - from + 540f) % 360f) - 180f;
            return from + (delta * progress);
        }
    }
}
