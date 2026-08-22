using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;

namespace GritGud.Application.Gameplay
{
    /// <summary>
    /// Builds portable encounter evidence from canonical state and the same
    /// destructible-aware spatial model used by headless attack and path work.
    /// Search branches therefore see a newly-opened line of sight after a
    /// tactical obstruction is moved, toppled, fractured, or destroyed.
    /// </summary>
    public static class GameplayHeadlessEncounterEvidence
    {
        public static TargetExposureSnapshot CaptureSight(
            GameplayCombatStateSnapshot state,
            GameplayHeadlessSpatialEvidence spatial,
            string observerId,
            string targetId)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (spatial == null) throw new ArgumentNullException(nameof(spatial));
            GameplayActorSnapshot observer = state.Session.GetActor(observerId);
            GameplayActorSnapshot target = state.Session.GetActor(targetId);
            TargetRegionSample observerHead = GetRegionSample(
                observer,
                TargetRegionId.Head);
            IReadOnlyList<TargetRegionSample> targetSamples =
                ActorTargetProfileCatalog.CreateWorldSamples(
                    target.Pose,
                    target.IsPinned);
            return GameplayTargetExposureRaster.Capture(
                observerId,
                observerHead.Center,
                targetId,
                targetSamples,
                new HeadlessExposureObstruction(
                    state,
                    spatial,
                    forceBlocked: false));
        }

        public static TargetExposureSnapshot CaptureDroneSight(
            GameplayCombatStateSnapshot state,
            GameplayHeadlessSpatialEvidence spatial,
            string droneId,
            string targetActorId)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (spatial == null) throw new ArgumentNullException(nameof(spatial));
            state.RequireCoverage(GameplayCombatStateCoverage.Drones);
            SummonedDroneSnapshot drone = FindDrone(state.Drones, droneId);
            GameplayActorSnapshot target = state.Session.GetActor(targetActorId);
            IReadOnlyList<TargetRegionSample> samples =
                ActorTargetProfileCatalog.CreateWorldSamples(
                    target.Pose,
                    target.IsPinned);
            bool withinSensor = DroneSensorRules.CanObserve(
                drone,
                target.Pose.Position);
            return GameplayTargetExposureRaster.Capture(
                droneId,
                drone.Position,
                targetActorId,
                samples,
                new HeadlessExposureObstruction(
                    state,
                    spatial,
                    forceBlocked: !withinSensor));
        }

        public static DroneExposureSnapshot CaptureActorSightOfDrone(
            GameplayCombatStateSnapshot state,
            GameplayHeadlessSpatialEvidence spatial,
            string observerActorId,
            string droneId)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (spatial == null) throw new ArgumentNullException(nameof(spatial));
            state.RequireCoverage(GameplayCombatStateCoverage.Drones);
            GameplayActorSnapshot observer = state.Session.GetActor(
                observerActorId);
            TargetRegionSample observerHead = GetRegionSample(
                observer,
                TargetRegionId.Head);
            SummonedDroneSnapshot drone = FindDrone(state.Drones, droneId);
            TargetExposureSnapshot raster = GameplayTargetExposureRaster.Capture(
                observerActorId,
                observerHead.Center,
                droneId,
                GameplayDroneTargetProfile.CreateWorldSamples(drone),
                new HeadlessExposureObstruction(
                    state,
                    spatial,
                    forceBlocked: false));
            return new DroneExposureSnapshot(
                observerActorId,
                droneId,
                raster.VisibleSampleCount,
                raster.TotalSampleCount);
        }

        private static bool IsObscuredBySmoke(
            GameplayCombatStateSnapshot state,
            GameplayPosition origin,
            GameplayPosition destination)
        {
            if (!state.Covers(GameplayCombatStateCoverage.SmokeFields))
                return false;
            foreach (SmokeFieldSnapshot smoke in state.SmokeFields)
            {
                SmokeFieldRecord field = smoke.Field;
                if (GameplaySmokeFieldSession.CalculateTraversalLength(
                        origin,
                        destination,
                        field.Origin,
                        field.Definition.Radius,
                        field.Definition.Height)
                    >= field.Definition.MinimumObscuredPath)
                    return true;
            }
            return false;
        }

        public static EncounterSoundEvidence CaptureSound(
            GameplayCombatStateSnapshot state,
            GameplayHeadlessSpatialEvidence spatial,
            string observerId,
            string sourceId,
            float loudness,
            float hearingRange)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (spatial == null) throw new ArgumentNullException(nameof(spatial));
            GameplayActorSnapshot observer = state.Session.GetActor(observerId);
            GameplayActorSnapshot source = state.Session.GetActor(sourceId);
            bool obstructed = spatial.BlocksLineOfSight(
                state,
                observer.Pose.Position,
                source.Pose.Position);
            return GameplaySoundEvidenceRules.Capture(
                observerId,
                observer.Pose.Position,
                sourceId,
                source.Pose.Position,
                loudness,
                hearingRange,
                obstructed);
        }

        public static EncounterObservation CaptureObservation(
            GameplayCombatStateSnapshot state,
            GameplayHeadlessSpatialEvidence spatial,
            string observerId,
            string targetId,
            string soundSourceId = null,
            float soundLoudness = 0f,
            float hearingRange = 0f)
        {
            TargetExposureSnapshot sight = CaptureSight(
                state,
                spatial,
                observerId,
                targetId);
            EncounterSoundEvidence sound = string.IsNullOrWhiteSpace(
                soundSourceId)
                ? null
                : CaptureSound(
                    state,
                    spatial,
                    observerId,
                    soundSourceId,
                    soundLoudness,
                    hearingRange);
            return new EncounterObservation(
                observerId,
                sight,
                state.Session.GetActor(targetId).Pose.Position,
                sound);
        }

        private static TargetRegionSample GetRegionSample(
            GameplayActorSnapshot actor,
            TargetRegionId regionId)
        {
            foreach (TargetRegionSample sample in
                ActorTargetProfileCatalog.CreateWorldSamples(
                    actor.Pose,
                    actor.IsPinned))
            {
                if (sample.Id == regionId)
                    return sample;
            }
            throw new InvalidOperationException(
                $"Actor target profile does not contain region '{regionId}'.");
        }

        private static SummonedDroneSnapshot FindDrone(
            IReadOnlyList<SummonedDroneSnapshot> drones,
            string droneId)
        {
            foreach (SummonedDroneSnapshot drone in drones)
                if (string.Equals(drone.DroneId, droneId,
                    StringComparison.Ordinal)) return drone;
            throw new KeyNotFoundException(
                $"Drone '{droneId}' is absent from canonical state.");
        }

        private sealed class HeadlessExposureObstruction :
            ITargetExposureObstructionQuery
        {
            private readonly GameplayCombatStateSnapshot state;
            private readonly GameplayHeadlessSpatialEvidence spatial;
            private readonly bool forceBlocked;

            public HeadlessExposureObstruction(
                GameplayCombatStateSnapshot canonicalState,
                GameplayHeadlessSpatialEvidence spatialEvidence,
                bool forceBlocked)
            {
                state = canonicalState;
                spatial = spatialEvidence;
                this.forceBlocked = forceBlocked;
            }

            public bool Blocks(
                GameplayPosition origin,
                GameplayPosition targetSurface) => forceBlocked
                || spatial.BlocksLineOfSight(state, origin, targetSurface)
                || IsObscuredBySmoke(state, origin, targetSurface);
        }

    }

    public static class GameplayDroneTargetProfile
    {
        private const float SampleRadius = 0.15f;

        public static IReadOnlyList<TargetRegionSample> CreateWorldSamples(
            SummonedDroneSnapshot drone)
        {
            var result = new List<TargetRegionSample>(6)
            {
                Sample(drone, TargetRegionId.Head,
                    new GameplayPosition(0f, 0.2f, 0f)),
                Sample(drone, TargetRegionId.Torso,
                    new GameplayPosition(0f, 0f, 0f)),
                Sample(drone, TargetRegionId.LeftArm,
                    new GameplayPosition(-0.55f, 0f, 0f)),
                Sample(drone, TargetRegionId.RightArm,
                    new GameplayPosition(0.55f, 0f, 0f)),
                Sample(drone, TargetRegionId.LeftLeg,
                    new GameplayPosition(0f, 0f, -0.55f)),
                Sample(drone, TargetRegionId.RightLeg,
                    new GameplayPosition(0f, 0f, 0.55f)),
            };
            return result.AsReadOnly();
        }

        private static TargetRegionSample Sample(
            SummonedDroneSnapshot drone,
            TargetRegionId id,
            GameplayPosition local)
        {
            double radians = drone.FacingDegrees * Math.PI / 180d;
            double cosine = Math.Cos(radians);
            double sine = Math.Sin(radians);
            float x = (float)((cosine * local.X) + (sine * local.Z));
            float z = (float)((-sine * local.X) + (cosine * local.Z));
            return new TargetRegionSample(
                id,
                new GameplayPosition(
                    drone.Position.X + x,
                    drone.Position.Y + local.Y,
                    drone.Position.Z + z),
                SampleRadius);
        }
    }
}
