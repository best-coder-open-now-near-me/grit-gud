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

        public static Vector3 ToVector3(GameplayPosition position)
        {
            return new Vector3(position.X, position.Y, position.Z);
        }
    }
}
