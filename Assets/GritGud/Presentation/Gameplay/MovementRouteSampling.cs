using System.Collections.Generic;
using GritGud.Domain.Gameplay;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    internal static class MovementRouteSampling
    {
        private const float MinimumSegmentDistance = 0.0001f;

        public static bool TrySample(
            IReadOnlyList<GameplayPosition> points,
            float distance,
            out Vector3 position,
            out Vector3 direction)
        {
            position = default;
            direction = Vector3.forward;
            if (points == null || points.Count < 2)
            {
                return false;
            }

            float remaining = Mathf.Max(0f, distance);
            Vector3 lastDirection = Vector3.forward;
            for (int index = 1; index < points.Count; index++)
            {
                Vector3 from = ToVector3(points[index - 1]);
                Vector3 to = ToVector3(points[index]);
                Vector3 segment = to - from;
                float segmentDistance = segment.magnitude;
                if (segmentDistance <= MinimumSegmentDistance)
                {
                    continue;
                }

                lastDirection = segment / segmentDistance;
                if (remaining <= segmentDistance)
                {
                    position = Vector3.Lerp(
                        from,
                        to,
                        remaining / segmentDistance);
                    direction = lastDirection;
                    return true;
                }

                remaining -= segmentDistance;
            }

            position = ToVector3(points[points.Count - 1]);
            direction = lastDirection;
            return true;
        }

        public static bool TrySample(
            IReadOnlyList<MovementRouteSegmentRecord> segments,
            float elapsedSeconds,
            out Vector3 position,
            out Vector3 direction,
            out int segmentIndex,
            out float segmentProgress)
        {
            position = default;
            direction = Vector3.forward;
            segmentIndex = -1;
            segmentProgress = 0f;
            if (segments == null || segments.Count == 0)
                return false;

            float remaining = Mathf.Max(0f, elapsedSeconds);
            for (int index = 0; index < segments.Count; index++)
            {
                MovementRouteSegmentRecord segment = segments[index];
                float duration = Mathf.Max(
                    0.0001f,
                    segment.PlaybackDurationSeconds);
                if (remaining < duration || index == segments.Count - 1)
                {
                    float progress = Mathf.Clamp01(remaining / duration);
                    position = ToVector3(segment.Sample(progress));
                    direction = SampleDirection(segment, progress);
                    segmentIndex = index;
                    segmentProgress = progress;
                    return true;
                }
                remaining -= duration;
            }
            return false;
        }

        public static bool TrySample(
            MovementRouteRecord route,
            float elapsedSeconds,
            out Vector3 position,
            out Vector3 direction,
            out int segmentIndex,
            out float segmentProgress) => TrySample(
                route?.Segments,
                elapsedSeconds,
                out position,
                out direction,
                out segmentIndex,
                out segmentProgress);

        private static Vector3 SampleDirection(
            MovementRouteSegmentRecord segment,
            float progress)
        {
            const float sampleOffset = 0.01f;
            float fromProgress = Mathf.Max(0f, progress - sampleOffset);
            float toProgress = Mathf.Min(1f, progress + sampleOffset);
            Vector3 direction = ToVector3(segment.Sample(toProgress))
                - ToVector3(segment.Sample(fromProgress));
            if (direction.sqrMagnitude <= MinimumSegmentDistance
                    * MinimumSegmentDistance)
                direction = ToVector3(segment.To) - ToVector3(segment.From);
            return direction.sqrMagnitude > 0f
                ? direction.normalized
                : Vector3.forward;
        }

        public static Vector3 ToVector3(GameplayPosition position)
        {
            return new Vector3(position.X, position.Y, position.Z);
        }
    }
}
