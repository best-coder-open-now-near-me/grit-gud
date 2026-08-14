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
        SegmentBlocked,
    }

    public readonly struct MovementRouteSegmentValidation
    {
        private MovementRouteSegmentValidation(
            bool isValid,
            GameplayPosition resolvedPosition,
            string failureReason)
        {
            IsValid = isValid;
            ResolvedPosition = resolvedPosition;
            FailureReason = failureReason;
        }

        public bool IsValid { get; }

        public GameplayPosition ResolvedPosition { get; }

        public string FailureReason { get; }

        public static MovementRouteSegmentValidation Accepted(
            GameplayPosition resolvedPosition)
        {
            return new MovementRouteSegmentValidation(
                true,
                resolvedPosition,
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
        private readonly IReadOnlyList<GameplayPosition> readOnlyPoints;
        private float totalCost;

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
            readOnlyPoints = points.AsReadOnly();
        }

        public string ActorId => actor.ActorId;

        public GameplayActorPose OriginPose => actor.Pose;

        public float MaximumCost => actor.TurnBudget.MovementOpportunity;

        public IReadOnlyList<GameplayPosition> Points => readOnlyPoints;

        public GameplayPosition Destination => points[points.Count - 1];

        public float TotalCost => totalCost;

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

            float segmentCost = from.DistanceTo(validation.ResolvedPosition);
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

            points.Add(validation.ResolvedPosition);
            totalCost += segmentCost;
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

            totalCost = 0f;
            LastFailureReason = string.Empty;
        }

        public MovementRouteRecord Confirm()
        {
            if (!CanConfirm)
            {
                throw new InvalidOperationException(
                    "A route must contain movement before it can be confirmed.");
            }

            var waypoints = new List<GameplayPosition>(points.Count - 1);
            for (int index = 1; index < points.Count; index++)
            {
                waypoints.Add(points[index]);
            }

            return new MovementRouteRecord(actor.ActorId, actor.Pose, waypoints);
        }

        private void RecalculateCost()
        {
            totalCost = 0f;
            for (int index = 1; index < points.Count; index++)
            {
                totalCost += points[index - 1].DistanceTo(points[index]);
            }
        }
    }
}
