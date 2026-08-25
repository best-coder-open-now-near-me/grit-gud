using System;

namespace GritGud.Application.Gameplay
{
    public enum ActorActionPresentationPhase
    {
        WindUp = 0,
        Release = 1,
        Recovery = 2,
        Complete = 3,
    }

    /// <summary>
    /// Seekable presentation plan for an action whose authoritative result
    /// includes a new actor facing. Simulation supplies both facings;
    /// presentation only samples the transition between them.
    /// </summary>
    public sealed class ActorTargetFacingActionPhase
    {
        public ActorTargetFacingActionPhase(
            float startingFacingDegrees,
            float targetFacingDegrees,
            float windUpStartNormalizedTime,
            float releaseNormalizedTime,
            float recoveryEndNormalizedTime)
        {
            RequireFinite(startingFacingDegrees, nameof(startingFacingDegrees));
            RequireFinite(targetFacingDegrees, nameof(targetFacingDegrees));
            RequireNormalized(
                windUpStartNormalizedTime,
                nameof(windUpStartNormalizedTime));
            RequireNormalized(
                releaseNormalizedTime,
                nameof(releaseNormalizedTime));
            RequireNormalized(
                recoveryEndNormalizedTime,
                nameof(recoveryEndNormalizedTime));
            if (releaseNormalizedTime <= windUpStartNormalizedTime)
                throw new ArgumentException(
                    "Action release must follow the start of wind-up.",
                    nameof(releaseNormalizedTime));
            if (recoveryEndNormalizedTime < releaseNormalizedTime)
                throw new ArgumentException(
                    "Action recovery cannot end before release.",
                    nameof(recoveryEndNormalizedTime));

            StartingFacingDegrees = Normalize(startingFacingDegrees);
            TargetFacingDegrees = Normalize(targetFacingDegrees);
            WindUpStartNormalizedTime = windUpStartNormalizedTime;
            ReleaseNormalizedTime = releaseNormalizedTime;
            RecoveryEndNormalizedTime = recoveryEndNormalizedTime;
        }

        public float StartingFacingDegrees { get; }

        public float TargetFacingDegrees { get; }

        public float WindUpStartNormalizedTime { get; }

        public float ReleaseNormalizedTime { get; }

        public float RecoveryStartNormalizedTime => ReleaseNormalizedTime;

        public float RecoveryEndNormalizedTime { get; }

        public float SampleFacingDegrees(float normalizedProgress)
        {
            float progress = Clamp01(normalizedProgress);
            float blend = Clamp01(
                (progress - WindUpStartNormalizedTime)
                / (ReleaseNormalizedTime - WindUpStartNormalizedTime));
            float smoothBlend = blend * blend * (3f - 2f * blend);
            float delta = DeltaDegrees(
                StartingFacingDegrees,
                TargetFacingDegrees);
            return Normalize(
                StartingFacingDegrees + (delta * smoothBlend));
        }

        public float SampleActionProgress(float normalizedProgress) =>
            RecoveryEndNormalizedTime <= 0f
                ? 1f
                : Clamp01(normalizedProgress / RecoveryEndNormalizedTime);

        public ActorActionPresentationPhase GetPhase(
            float normalizedProgress)
        {
            float progress = Clamp01(normalizedProgress);
            if (progress < ReleaseNormalizedTime)
                return ActorActionPresentationPhase.WindUp;
            if (progress == ReleaseNormalizedTime)
                return ActorActionPresentationPhase.Release;
            return progress < RecoveryEndNormalizedTime
                ? ActorActionPresentationPhase.Recovery
                : ActorActionPresentationPhase.Complete;
        }

        private static float DeltaDegrees(float current, float target)
        {
            float delta = Normalize(target - current);
            return delta > 180f ? delta - 360f : delta;
        }

        private static float Normalize(float value)
        {
            float normalized = value % 360f;
            return normalized < 0f ? normalized + 360f : normalized;
        }

        private static float Clamp01(float value)
        {
            RequireFinite(value, nameof(value));
            return Math.Max(0f, Math.Min(1f, value));
        }

        private static void RequireNormalized(float value, string parameter)
        {
            RequireFinite(value, parameter);
            if (value < 0f || value > 1f)
                throw new ArgumentOutOfRangeException(parameter);
        }

        private static void RequireFinite(float value, string parameter)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                throw new ArgumentOutOfRangeException(parameter);
        }
    }

    public static class GameplayThrownExplosivePresentationTiming
    {
        public const float ReleaseSeconds = 0.45f;
        public const float FlightSeconds = 0.55f;
        public const float RecoveryEndSeconds = 0.8f;
        public const float PostImpactSeconds = 0.65f;
        public const float DefaultImpactEffectSeconds = 0.65f;
        public const float ImpactSeconds = ReleaseSeconds + FlightSeconds;
        public const float TotalSequenceSeconds =
            ImpactSeconds + PostImpactSeconds;
        public const float ReleaseNormalizedTime =
            ReleaseSeconds / TotalSequenceSeconds;
        public const float RecoveryEndNormalizedTime =
            RecoveryEndSeconds / TotalSequenceSeconds;
        public const float ImpactNormalizedTime =
            ImpactSeconds / TotalSequenceSeconds;

        public static ActorTargetFacingActionPhase CreateFacingPhase(
            float startingFacingDegrees,
            float targetFacingDegrees) => new ActorTargetFacingActionPhase(
                startingFacingDegrees,
                targetFacingDegrees,
                windUpStartNormalizedTime: 0f,
                ReleaseNormalizedTime,
                RecoveryEndNormalizedTime);

        public static string GetProjectileId(string actorId, long sequence)
        {
            if (string.IsNullOrWhiteSpace(actorId))
                throw new ArgumentException(
                    "Thrown presentation projectiles require an actor.",
                    nameof(actorId));
            if (sequence <= 0)
                throw new ArgumentOutOfRangeException(nameof(sequence));
            return "thrown-explosive:" + actorId + ":" + sequence;
        }
    }
}
