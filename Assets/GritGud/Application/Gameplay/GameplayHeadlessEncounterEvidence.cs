using System;
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
            bool blocked = spatial.BlocksLineOfSight(
                state,
                observer.Pose.Position,
                target.Pose.Position);
            return new TargetExposureSnapshot(
                observerId,
                targetId,
                new[]
                {
                    new TargetRegionExposure(
                        TargetRegionId.Torso,
                        blocked ? 0 : 1,
                        totalSampleCount: 1),
                });
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
    }
}
