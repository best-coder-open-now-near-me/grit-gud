using System;
using System.Collections.Generic;

namespace GritGud.Domain.Gameplay
{
    /// <summary>
    /// Immutable authoritative data for an accepted movement route.
    /// </summary>
    public sealed class MovementRouteRecord
    {
        private const float MeaningfulSegmentDistance = 0.0001f;
        private readonly IReadOnlyList<GameplayPosition> points;

        public MovementRouteRecord(
            string actorId,
            GameplayActorPose originPose,
            IEnumerable<GameplayPosition> waypoints)
        {
            if (string.IsNullOrWhiteSpace(actorId))
            {
                throw new ArgumentException(
                    "Actor identifiers cannot be empty.",
                    nameof(actorId));
            }

            if (waypoints == null)
            {
                throw new ArgumentNullException(nameof(waypoints));
            }

            ActorId = actorId;
            OriginPose = originPose;

            var copiedPoints = new List<GameplayPosition> { originPose.Position };
            float totalCost = 0f;
            GameplayPosition previous = originPose.Position;
            foreach (GameplayPosition waypoint in waypoints)
            {
                float segmentCost = previous.DistanceTo(waypoint);
                if (segmentCost <= MeaningfulSegmentDistance)
                {
                    throw new ArgumentException(
                        "Movement routes cannot contain zero-length segments.",
                        nameof(waypoints));
                }

                copiedPoints.Add(waypoint);
                totalCost += segmentCost;
                if (float.IsInfinity(totalCost))
                {
                    throw new ArgumentException(
                        "The movement route cost exceeds the supported range.",
                        nameof(waypoints));
                }

                previous = waypoint;
            }

            if (copiedPoints.Count == 1)
            {
                throw new ArgumentException(
                    "Movement routes must contain at least one waypoint.",
                    nameof(waypoints));
            }

            points = copiedPoints.AsReadOnly();
            TotalCost = totalCost;
            Destination = copiedPoints[copiedPoints.Count - 1];
            FinalFacingDegrees = CalculateFinalFacing(copiedPoints, originPose.FacingDegrees);
        }

        public string ActorId { get; }

        public GameplayActorPose OriginPose { get; }

        public IReadOnlyList<GameplayPosition> Points => points;

        public float TotalCost { get; }

        public GameplayPosition Destination { get; }

        public float FinalFacingDegrees { get; }

        private static float CalculateFinalFacing(
            IReadOnlyList<GameplayPosition> routePoints,
            float fallbackFacingDegrees)
        {
            float thresholdSquared =
                MeaningfulSegmentDistance * MeaningfulSegmentDistance;
            for (int index = routePoints.Count - 1; index > 0; index--)
            {
                GameplayPosition from = routePoints[index - 1];
                GameplayPosition to = routePoints[index];
                float deltaX = to.X - from.X;
                float deltaZ = to.Z - from.Z;
                if ((deltaX * deltaX) + (deltaZ * deltaZ) <= thresholdSquared)
                {
                    continue;
                }

                float facing = (float)(
                    Math.Atan2(deltaX, deltaZ) * (180d / Math.PI));
                return facing < 0f ? facing + 360f : facing;
            }

            return fallbackFacingDegrees;
        }
    }
}
