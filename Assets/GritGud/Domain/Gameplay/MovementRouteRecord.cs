using System;
using System.Collections.Generic;
using GritGud.Domain.Turns;

namespace GritGud.Domain.Gameplay
{
    public enum MovementRouteSegmentKind
    {
        Grounded = 0,
        Jump = 1,
        Vault = 2,
        Mantle = 3,
    }

    public sealed class MovementRouteSegmentRecord
    {
        private const float MinimumDistance = 0.0001f;

        public MovementRouteSegmentRecord(
            GameplayPosition from,
            GameplayPosition to,
            MovementRouteSegmentKind kind = MovementRouteSegmentKind.Grounded,
            string traversalLinkId = null,
            string actionId = null,
            float movementCost = -1f,
            int actionPointCost = 0,
            float arcHeight = 0f,
            float playbackDurationSeconds = 0f)
        {
            if (!Enum.IsDefined(typeof(MovementRouteSegmentKind), kind))
                throw new ArgumentOutOfRangeException(nameof(kind));
            float distance = from.DistanceTo(to);
            if (distance <= MinimumDistance)
                throw new ArgumentException(
                    "Movement route segments cannot have identical endpoints.",
                    nameof(to));
            float resolvedCost = movementCost < 0f ? distance : movementCost;
            if (!FinitePositive(resolvedCost))
                throw new ArgumentOutOfRangeException(nameof(movementCost));
            if (actionPointCost < 0)
                throw new ArgumentOutOfRangeException(nameof(actionPointCost));
            if (!FiniteNonNegative(arcHeight))
                throw new ArgumentOutOfRangeException(nameof(arcHeight));
            if (playbackDurationSeconds < 0f
                || float.IsNaN(playbackDurationSeconds)
                || float.IsInfinity(playbackDurationSeconds))
                throw new ArgumentOutOfRangeException(
                    nameof(playbackDurationSeconds));
            if (kind != MovementRouteSegmentKind.Grounded
                && (string.IsNullOrWhiteSpace(traversalLinkId)
                    || string.IsNullOrWhiteSpace(actionId)))
            {
                throw new ArgumentException(
                    "Traversal segments require stable link and action IDs.",
                    nameof(traversalLinkId));
            }

            From = from;
            To = to;
            Kind = kind;
            TraversalLinkId = traversalLinkId?.Trim() ?? string.Empty;
            ActionId = actionId?.Trim() ?? string.Empty;
            MovementCost = resolvedCost;
            ActionPointCost = actionPointCost;
            ArcHeight = arcHeight;
            PlaybackDurationSeconds = playbackDurationSeconds > 0f
                ? playbackDurationSeconds
                : distance / 4f;
        }

        public GameplayPosition From { get; }
        public GameplayPosition To { get; }
        public MovementRouteSegmentKind Kind { get; }
        public string TraversalLinkId { get; }
        public string ActionId { get; }
        public float MovementCost { get; }
        public int ActionPointCost { get; }
        public float ArcHeight { get; }
        public float PlaybackDurationSeconds { get; }
        public bool IsTraversal => Kind != MovementRouteSegmentKind.Grounded;

        public GameplayPosition Sample(float normalizedProgress)
        {
            float progress = Math.Max(0f, Math.Min(1f, normalizedProgress));
            float lift = IsTraversal
                ? 4f * ArcHeight * progress * (1f - progress)
                : 0f;
            return new GameplayPosition(
                From.X + ((To.X - From.X) * progress),
                From.Y + ((To.Y - From.Y) * progress) + lift,
                From.Z + ((To.Z - From.Z) * progress));
        }

        private static bool FinitePositive(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;

        private static bool FiniteNonNegative(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
    }

    /// <summary>
    /// Immutable authoritative data for an accepted movement route.
    /// </summary>
    public sealed class MovementRouteRecord
    {
        private const float MeaningfulSegmentDistance = 0.0001f;
        private IReadOnlyList<GameplayPosition> points;
        private IReadOnlyList<MovementRouteSegmentRecord> segments;

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

            var copiedSegments = new List<MovementRouteSegmentRecord>();
            GameplayPosition previous = originPose.Position;
            foreach (GameplayPosition waypoint in waypoints)
            {
                copiedSegments.Add(new MovementRouteSegmentRecord(
                    previous,
                    waypoint));
                previous = waypoint;
            }
            Initialize(actorId, originPose, copiedSegments, default, false);
        }

        public MovementRouteRecord(
            string actorId,
            GameplayActorPose originPose,
            TurnBudget previousBudget,
            IEnumerable<MovementRouteSegmentRecord> routeSegments)
        {
            Initialize(actorId, originPose, routeSegments, previousBudget, true);
        }

        public string ActorId { get; private set; }

        public GameplayActorPose OriginPose { get; private set; }

        public IReadOnlyList<GameplayPosition> Points => points;

        public IReadOnlyList<MovementRouteSegmentRecord> Segments => segments;

        public float TotalCost { get; private set; }

        public int TotalActionPointCost { get; private set; }

        public float TotalPlaybackDurationSeconds { get; private set; }

        public TurnBudget PreviousBudget { get; private set; }

        public bool HasFrozenBudget { get; private set; }

        public bool HasTraversal { get; private set; }

        public GameplayPosition Destination { get; private set; }

        public float FinalFacingDegrees { get; private set; }

        private void Initialize(
            string actorId,
            GameplayActorPose originPose,
            IEnumerable<MovementRouteSegmentRecord> routeSegments,
            TurnBudget previousBudget,
            bool hasFrozenBudget)
        {
            if (string.IsNullOrWhiteSpace(actorId))
                throw new ArgumentException(
                    "Actor identifiers cannot be empty.",
                    nameof(actorId));
            if (routeSegments == null)
                throw new ArgumentNullException(nameof(routeSegments));

            var copiedSegments = new List<MovementRouteSegmentRecord>();
            var copiedPoints = new List<GameplayPosition> { originPose.Position };
            GameplayPosition expectedFrom = originPose.Position;
            float totalCost = 0f;
            float totalDuration = 0f;
            int totalActionPoints = 0;
            bool hasTraversal = false;
            foreach (MovementRouteSegmentRecord segment in routeSegments)
            {
                if (segment == null)
                    throw new ArgumentException(
                        "Movement routes cannot contain empty segments.",
                        nameof(routeSegments));
                if (expectedFrom.DistanceTo(segment.From)
                    > MeaningfulSegmentDistance)
                    throw new ArgumentException(
                        "Movement route segments must form a continuous chain.",
                        nameof(routeSegments));
                copiedSegments.Add(segment);
                copiedPoints.Add(segment.To);
                expectedFrom = segment.To;
                totalCost += segment.MovementCost;
                totalDuration += segment.PlaybackDurationSeconds;
                checked { totalActionPoints += segment.ActionPointCost; }
                hasTraversal |= segment.IsTraversal;
            }
            if (copiedSegments.Count == 0)
                throw new ArgumentException(
                    "Movement routes must contain at least one segment.",
                    nameof(routeSegments));
            if (float.IsInfinity(totalCost) || float.IsInfinity(totalDuration))
                throw new ArgumentException(
                    "The movement route exceeds the supported range.",
                    nameof(routeSegments));

            ActorId = actorId;
            OriginPose = originPose;
            segments = copiedSegments.AsReadOnly();
            points = copiedPoints.AsReadOnly();
            TotalCost = totalCost;
            TotalActionPointCost = totalActionPoints;
            TotalPlaybackDurationSeconds = totalDuration;
            PreviousBudget = previousBudget;
            HasFrozenBudget = hasFrozenBudget;
            HasTraversal = hasTraversal;
            Destination = copiedPoints[copiedPoints.Count - 1];
            FinalFacingDegrees = CalculateFinalFacing(
                copiedPoints,
                originPose.FacingDegrees);
        }

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
