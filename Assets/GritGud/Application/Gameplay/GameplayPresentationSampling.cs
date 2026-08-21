using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;

namespace GritGud.Application.Gameplay
{
    public readonly struct GameplayMovementPresentationSample
    {
        public GameplayMovementPresentationSample(
            GameplayPosition position,
            GameplayPosition direction,
            float facingDegrees,
            int segmentIndex,
            float segmentProgress)
        {
            Position = position;
            Direction = direction;
            FacingDegrees = facingDegrees;
            SegmentIndex = segmentIndex;
            SegmentProgress = segmentProgress;
        }

        public GameplayPosition Position { get; }
        public GameplayPosition Direction { get; }
        public float FacingDegrees { get; }
        public int SegmentIndex { get; }
        public float SegmentProgress { get; }
    }

    public static class GameplayMovementPresentationSampler
    {
        private const float MinimumDirectionLength = 0.0001f;

        public static bool TrySample(
            MovementRouteRecord route,
            float elapsedSeconds,
            out GameplayMovementPresentationSample sample)
        {
            sample = default;
            if (route == null || route.Segments.Count == 0)
                return false;
            if (!TrySample(
                    route.Segments,
                    elapsedSeconds,
                    route.OriginPose.FacingDegrees,
                    out sample))
                return false;
            if (elapsedSeconds >= route.TotalPlaybackDurationSeconds)
            {
                sample = new GameplayMovementPresentationSample(
                    route.Destination,
                    sample.Direction,
                    route.FinalFacingDegrees,
                    route.Segments.Count - 1,
                    1f);
            }
            return true;
        }

        public static bool TrySample(
            IReadOnlyList<MovementRouteSegmentRecord> segments,
            float elapsedSeconds,
            float fallbackFacingDegrees,
            out GameplayMovementPresentationSample sample)
        {
            sample = default;
            if (segments == null || segments.Count == 0)
                return false;
            GameplayNumericPolicy.RequireFinite(
                elapsedSeconds,
                nameof(elapsedSeconds));
            float remaining = Math.Max(0f, elapsedSeconds);
            for (int index = 0; index < segments.Count; index++)
            {
                MovementRouteSegmentRecord segment = segments[index];
                float duration = Math.Max(
                    0.0001f,
                    segment.PlaybackDurationSeconds);
                if (remaining >= duration
                    && index < segments.Count - 1)
                {
                    remaining -= duration;
                    continue;
                }
                float progress = Clamp01(remaining / duration);
                GameplayPosition direction = SampleDirection(
                    segment,
                    progress);
                sample = new GameplayMovementPresentationSample(
                    segment.Sample(progress),
                    direction,
                    CalculateFacing(
                        direction,
                        fallbackFacingDegrees),
                    index,
                    progress);
                return true;
            }
            return false;
        }

        private static GameplayPosition SampleDirection(
            MovementRouteSegmentRecord segment,
            float progress)
        {
            const float offset = 0.01f;
            GameplayPosition from = segment.Sample(Math.Max(
                0f,
                progress - offset));
            GameplayPosition to = segment.Sample(Math.Min(
                1f,
                progress + offset));
            GameplayPosition direction = NormalizeDirection(from, to);
            return Length(direction) > 0f
                ? direction
                : NormalizeDirection(segment.From, segment.To);
        }

        private static GameplayPosition NormalizeDirection(
            GameplayPosition from,
            GameplayPosition to)
        {
            float x = to.X - from.X;
            float y = to.Y - from.Y;
            float z = to.Z - from.Z;
            float length = (float)Math.Sqrt((x * x) + (y * y) + (z * z));
            return length <= MinimumDirectionLength
                ? new GameplayPosition(0f, 0f, 0f)
                : new GameplayPosition(x / length, y / length, z / length);
        }

        private static float Length(GameplayPosition value) =>
            (float)Math.Sqrt(
                (value.X * value.X)
                + (value.Y * value.Y)
                + (value.Z * value.Z));

        private static float CalculateFacing(
            GameplayPosition direction,
            float fallback)
        {
            if (Math.Abs(direction.X) <= MinimumDirectionLength
                && Math.Abs(direction.Z) <= MinimumDirectionLength)
                return fallback;
            float degrees = (float)(Math.Atan2(
                direction.X,
                direction.Z) * (180d / Math.PI));
            return degrees < 0f ? degrees + 360f : degrees;
        }

        private static float Clamp01(float value) =>
            Math.Max(0f, Math.Min(1f, value));
    }

    public static class GameplayProjectilePresentationSampler
    {
        public const float DefaultAccelerationFraction = 0.28f;

        public static float EvaluateProgress(
            float linearProgress,
            float accelerationFraction)
        {
            GameplayNumericPolicy.RequireFinite(
                linearProgress,
                nameof(linearProgress));
            GameplayNumericPolicy.RequireFinite(
                accelerationFraction,
                nameof(accelerationFraction));
            float progress = Clamp01(linearProgress);
            float acceleration = Clamp01(accelerationFraction);
            if (acceleration <= 0.0001f) return progress;
            float normalizedDistance = 1f - (acceleration * 0.5f);
            return progress < acceleration
                ? (0.5f * progress * progress / acceleration)
                    / normalizedDistance
                : (progress - (acceleration * 0.5f))
                    / normalizedDistance;
        }

        public static ProjectileFlightSnapshot Sample(
            ProjectileFlightSnapshot previous,
            ProjectileFlightSnapshot resulting,
            float linearProgress,
            float accelerationFraction = DefaultAccelerationFraction)
        {
            if (!string.Equals(
                    previous.ProjectileId,
                    resulting.ProjectileId,
                    StringComparison.Ordinal))
                throw new ArgumentException(
                    "Projectile presentation endpoints must share an identity.",
                    nameof(resulting));
            float linear = Clamp01(linearProgress);
            if (linear >= 1f) return resulting;
            float progress = EvaluateProgress(linear, accelerationFraction);
            float distance = previous.DistanceTraveled
                + ((resulting.DistanceTraveled - previous.DistanceTraveled)
                    * progress);
            return new ProjectileFlightSnapshot(
                resulting.Launch,
                resulting.Launch.GetPosition(distance),
                distance,
                distance / resulting.Launch.Definition.SpeedPerTurn,
                ProjectileFlightStatus.InFlight);
        }

        private static float Clamp01(float value) =>
            Math.Max(0f, Math.Min(1f, value));
    }
}
