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
            var regions = new List<TargetRegionExposure>(targetSamples.Count);
            foreach (TargetRegionSample sample in targetSamples)
            {
                bool blocked = spatial.BlocksLineOfSight(
                        state,
                        observerHead.Center,
                        sample.Center)
                    || IsObscuredBySmoke(
                        state,
                        observerHead.Center,
                        sample.Center);
                regions.Add(new TargetRegionExposure(
                    sample.Id,
                    blocked ? 0 : 1,
                    totalSampleCount: 1));
            }
            return new TargetExposureSnapshot(
                observerId,
                targetId,
                regions);
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
            DroneSnapshot drone = FindDrone(state.Drones, droneId);
            GameplayActorSnapshot target = state.Session.GetActor(targetActorId);
            IReadOnlyList<TargetRegionSample> samples =
                ActorTargetProfileCatalog.CreateWorldSamples(
                    target.Pose,
                    target.IsPinned);
            var regions = new List<TargetRegionExposure>(samples.Count);
            bool withinSensor = DroneSensorRules.CanObserve(
                drone,
                target.Pose.Position);
            foreach (TargetRegionSample sample in samples)
            {
                bool visible = withinSensor
                    && !spatial.BlocksLineOfSight(
                        state,
                        drone.Position,
                        sample.Center)
                    && !IsObscuredBySmoke(
                        state,
                        drone.Position,
                        sample.Center);
                regions.Add(new TargetRegionExposure(
                    sample.Id,
                    visible ? 1 : 0,
                    totalSampleCount: 1));
            }
            return new TargetExposureSnapshot(droneId, targetActorId, regions);
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
            float loudness)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (spatial == null) throw new ArgumentNullException(nameof(spatial));
            if (float.IsNaN(loudness) || float.IsInfinity(loudness)
                || loudness < 0f || loudness > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(loudness));
            }

            GameplayActorSnapshot observer = state.Session.GetActor(observerId);
            GameplayActorSnapshot source = state.Session.GetActor(sourceId);
            bool obstructed = spatial.BlocksLineOfSight(
                state,
                observer.Pose.Position,
                source.Pose.Position);
            return new EncounterSoundEvidence(
                sourceId,
                source.Pose.Position,
                obstructed ? loudness * 0.5f : loudness);
        }

        public static EncounterObservation CaptureObservation(
            GameplayCombatStateSnapshot state,
            GameplayHeadlessSpatialEvidence spatial,
            string observerId,
            string targetId,
            string soundSourceId = null,
            float soundLoudness = 0f)
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
                    soundLoudness);
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

        private static DroneSnapshot FindDrone(
            IReadOnlyList<DroneSnapshot> drones,
            string droneId)
        {
            foreach (DroneSnapshot drone in drones)
                if (string.Equals(drone.DroneId, droneId,
                    StringComparison.Ordinal)) return drone;
            throw new KeyNotFoundException(
                $"Drone '{droneId}' is absent from canonical state.");
        }

    }
}
