using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;

namespace GritGud.Application.Gameplay
{
    public enum RoutePlanFailure
    {
        None,
        ZeroLengthSegment,
        ExceedsMovementBudget,
        ExceedsActionPointBudget,
        SegmentBlocked,
    }

    public readonly struct MovementRouteSegmentValidation
    {
        private MovementRouteSegmentValidation(
            bool isValid,
            GameplayPosition resolvedPosition,
            MovementRouteSegmentRecord segment,
            string failureReason)
        {
            IsValid = isValid;
            ResolvedPosition = resolvedPosition;
            Segment = segment;
            FailureReason = failureReason;
        }

        public bool IsValid { get; }

        public GameplayPosition ResolvedPosition { get; }

        public MovementRouteSegmentRecord Segment { get; }

        public string FailureReason { get; }

        public static MovementRouteSegmentValidation Accepted(
            GameplayPosition resolvedPosition)
        {
            return new MovementRouteSegmentValidation(
                true,
                resolvedPosition,
                null,
                string.Empty);
        }

        public static MovementRouteSegmentValidation Accepted(
            MovementRouteSegmentRecord segment)
        {
            if (segment == null)
                throw new ArgumentNullException(nameof(segment));
            return new MovementRouteSegmentValidation(
                true,
                segment.To,
                segment,
                string.Empty);
        }

        public static MovementRouteSegmentValidation Rejected(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new ArgumentException(
                    "Rejected route segments require a reason.",
                    nameof(reason));
            }

            return new MovementRouteSegmentValidation(
                false,
                default(GameplayPosition),
                null,
                reason);
        }
    }

    public interface IMovementRouteSegmentValidator
    {
        MovementRouteSegmentValidation Validate(
            string actorId,
            GameplayPosition from,
            GameplayPosition requestedDestination);
    }

    /// <summary>
    /// Builds a provisional route without mutating authoritative session state.
    /// </summary>
    public sealed class MovementRoutePlanner
    {
        private const float MeaningfulSegmentDistance = 0.0001f;
        private readonly GameplayActorSnapshot actor;
        private readonly IMovementRouteSegmentValidator segmentValidator;
        private readonly List<GameplayPosition> points;
        private readonly List<MovementRouteSegmentRecord> segments;
        private readonly IReadOnlyList<GameplayPosition> readOnlyPoints;
        private readonly IReadOnlyList<MovementRouteSegmentRecord>
            readOnlySegments;
        private float totalCost;
        private int totalActionPointCost;
        private float totalPlaybackDurationSeconds;

        public MovementRoutePlanner(
            GameplayActorSnapshot actor,
            IMovementRouteSegmentValidator segmentValidator)
        {
            if (string.IsNullOrWhiteSpace(actor.ActorId))
            {
                throw new ArgumentException(
                    "A route planner requires a valid actor snapshot.",
                    nameof(actor));
            }

            this.actor = actor;
            this.segmentValidator = segmentValidator
                ?? throw new ArgumentNullException(nameof(segmentValidator));
            points = new List<GameplayPosition> { actor.Pose.Position };
            segments = new List<MovementRouteSegmentRecord>();
            readOnlyPoints = points.AsReadOnly();
            readOnlySegments = segments.AsReadOnly();
        }

        public string ActorId => actor.ActorId;

        public GameplayActorPose OriginPose => actor.Pose;

        public int MaximumActionPoints => actor.TurnBudget.ActionPoints;

        public float MaximumCost => actor.TurnBudget.MovementOpportunity;

        public IReadOnlyList<GameplayPosition> Points => readOnlyPoints;

        public IReadOnlyList<MovementRouteSegmentRecord> Segments =>
            readOnlySegments;

        public GameplayPosition Destination => points[points.Count - 1];

        public float TotalCost => totalCost;

        public int TotalActionPointCost => totalActionPointCost;

        public float TotalPlaybackDurationSeconds =>
            totalPlaybackDurationSeconds;

        public bool CanConfirm => points.Count > 1;

        public string LastFailureReason { get; private set; } = string.Empty;

        public bool TryAppend(
            GameplayPosition requestedDestination,
            out RoutePlanFailure failure)
        {
            GameplayPosition from = Destination;
            MovementRouteSegmentValidation validation = segmentValidator.Validate(
                actor.ActorId,
                from,
                requestedDestination);
            if (!validation.IsValid)
            {
                failure = RoutePlanFailure.SegmentBlocked;
                LastFailureReason = string.IsNullOrWhiteSpace(validation.FailureReason)
                    ? "The route segment was rejected."
                    : validation.FailureReason;
                return false;
            }

            MovementRouteSegmentRecord segment = validation.Segment
                ?? new MovementRouteSegmentRecord(
                    from,
                    validation.ResolvedPosition);
            if (from.DistanceTo(segment.From) > MeaningfulSegmentDistance)
            {
                failure = RoutePlanFailure.SegmentBlocked;
                LastFailureReason =
                    "The resolved route segment does not begin at the planned destination.";
                return false;
            }
            float segmentCost = segment.MovementCost;
            if (segmentCost <= MeaningfulSegmentDistance)
            {
                failure = RoutePlanFailure.ZeroLengthSegment;
                LastFailureReason = "The route segment is too short.";
                return false;
            }

            if (segmentCost > MaximumCost - totalCost)
            {
                failure = RoutePlanFailure.ExceedsMovementBudget;
                LastFailureReason = "The route exceeds the actor's remaining movement.";
                return false;
            }

            if (segment.ActionPointCost
                > actor.TurnBudget.ActionPoints - totalActionPointCost)
            {
                failure = RoutePlanFailure.ExceedsActionPointBudget;
                LastFailureReason =
                    "The route exceeds the actor's remaining action points.";
                return false;
            }

            segments.Add(segment);
            points.Add(segment.To);
            totalCost += segmentCost;
            totalActionPointCost += segment.ActionPointCost;
            totalPlaybackDurationSeconds +=
                segment.PlaybackDurationSeconds;
            failure = RoutePlanFailure.None;
            LastFailureReason = string.Empty;
            return true;
        }

        public bool UndoLastSegment()
        {
            if (points.Count == 1)
            {
                return false;
            }

            points.RemoveAt(points.Count - 1);
            segments.RemoveAt(segments.Count - 1);
            RecalculateCost();
            LastFailureReason = string.Empty;
            return true;
        }

        public void Cancel()
        {
            if (points.Count > 1)
            {
                points.RemoveRange(1, points.Count - 1);
            }

            segments.Clear();

            totalCost = 0f;
            totalActionPointCost = 0;
            totalPlaybackDurationSeconds = 0f;
            LastFailureReason = string.Empty;
        }

        public MovementRouteRecord Confirm()
        {
            if (!CanConfirm)
            {
                throw new InvalidOperationException(
                    "A route must contain movement before it can be confirmed.");
            }

            return new MovementRouteRecord(
                actor.ActorId,
                actor.Pose,
                actor.TurnBudget,
                segments);
        }

        private void RecalculateCost()
        {
            totalCost = 0f;
            totalActionPointCost = 0;
            totalPlaybackDurationSeconds = 0f;
            foreach (MovementRouteSegmentRecord segment in segments)
            {
                totalCost += segment.MovementCost;
                totalActionPointCost += segment.ActionPointCost;
                totalPlaybackDurationSeconds +=
                    segment.PlaybackDurationSeconds;
            }
        }
    }
}
