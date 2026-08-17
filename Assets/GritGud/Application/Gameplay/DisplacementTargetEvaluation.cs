using System;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;

namespace GritGud.Application.Gameplay
{
    public enum DisplacementActionAvailabilityFailure
    {
        None,
        ActorUnavailable,
        ActionUnavailable,
        OperationInProgress,
        ActorNotActive,
        HandsOccupied,
        InsufficientTurnBudget,
        ActorPinned,
        ActorNotPinned,
    }

    public sealed class DisplacementActionAvailability
    {
        internal DisplacementActionAvailability(
            string actorId,
            string actionId,
            DisplacementActionAvailabilityFailure failure,
            DisplacementActionDefinition action,
            ActionCost resolvedCost,
            string autoStowItemId)
        {
            if (string.IsNullOrWhiteSpace(actorId))
                throw new ArgumentException(
                    "Displacement availability requires an actor ID.",
                    nameof(actorId));
            if (string.IsNullOrWhiteSpace(actionId))
                throw new ArgumentException(
                    "Displacement availability requires an action ID.",
                    nameof(actionId));
            if (!Enum.IsDefined(
                    typeof(DisplacementActionAvailabilityFailure),
                    failure))
                throw new ArgumentOutOfRangeException(nameof(failure));
            if (failure == DisplacementActionAvailabilityFailure.None
                && action == null)
                throw new ArgumentException(
                    "Available displacement actions require a definition.",
                    nameof(action));

            ActorId = actorId;
            ActionId = actionId;
            Failure = failure;
            Action = action;
            ResolvedCost = resolvedCost;
            AutoStowItemId = string.IsNullOrWhiteSpace(autoStowItemId)
                ? null
                : autoStowItemId;
        }

        public string ActorId { get; }

        public string ActionId { get; }

        public DisplacementActionAvailabilityFailure Failure { get; }

        public DisplacementActionDefinition Action { get; }

        public ActionCost ResolvedCost { get; }

        public string AutoStowItemId { get; }

        public bool RequiresAutoStow => AutoStowItemId != null;

        public bool IsAvailable =>
            Failure == DisplacementActionAvailabilityFailure.None;
    }

    public enum DisplacementTargetFailure
    {
        None,
        ActorUnavailable,
        ActionUnavailable,
        CandidateUnavailable,
        SelfTarget,
        SubjectKindNotAccepted,
        SubjectTooHeavy,
        SubjectTooLarge,
        SubjectOutOfReach,
        NotPinningActor,
        SubjectPinned,
    }

    public sealed class DisplacementTargetEvaluation
    {
        internal DisplacementTargetEvaluation(
            string actorId,
            string actionId,
            string candidateId,
            DisplacementTargetFailure failure,
            DisplacementSubjectDefinition subject,
            float distance,
            DisplacementActionDefinition action)
        {
            if (string.IsNullOrWhiteSpace(actorId))
                throw new ArgumentException(
                    "Displacement evaluations require an actor ID.",
                    nameof(actorId));
            if (string.IsNullOrWhiteSpace(actionId))
                throw new ArgumentException(
                    "Displacement evaluations require an action ID.",
                    nameof(actionId));
            if (string.IsNullOrWhiteSpace(candidateId))
                throw new ArgumentException(
                    "Displacement evaluations require a candidate ID.",
                    nameof(candidateId));
            if (!Enum.IsDefined(typeof(DisplacementTargetFailure), failure))
                throw new ArgumentOutOfRangeException(nameof(failure));
            if (float.IsNaN(distance)
                || float.IsInfinity(distance)
                || distance < 0f)
                throw new ArgumentOutOfRangeException(nameof(distance));
            if (failure == DisplacementTargetFailure.None
                && (subject == null || action == null))
            {
                throw new ArgumentException(
                    "Eligible displacement targets require action and subject definitions.",
                    nameof(failure));
            }

            ActorId = actorId;
            ActionId = actionId;
            CandidateId = candidateId;
            Failure = failure;
            Subject = subject;
            Distance = distance;
            Action = action;
        }

        public string ActorId { get; }

        public string ActionId { get; }

        public string CandidateId { get; }

        public DisplacementTargetFailure Failure { get; }

        public DisplacementSubjectDefinition Subject { get; }

        public float Distance { get; }

        public DisplacementActionDefinition Action { get; }

        public bool IsEligible => Failure == DisplacementTargetFailure.None;
    }

    public sealed class DisplacementDestinationEvaluation
    {
        internal DisplacementDestinationEvaluation(
            string actorId,
            string actionId,
            string subjectId,
            GameplayPosition origin,
            GameplayPosition destination,
            DisplacementResolutionFailure failure,
            DisplacementActionDefinition action)
        {
            if (string.IsNullOrWhiteSpace(actorId))
                throw new ArgumentException(
                    "Displacement destination evaluations require an actor ID.",
                    nameof(actorId));
            if (string.IsNullOrWhiteSpace(actionId))
                throw new ArgumentException(
                    "Displacement destination evaluations require an action ID.",
                    nameof(actionId));
            if (string.IsNullOrWhiteSpace(subjectId))
                throw new ArgumentException(
                    "Displacement destination evaluations require a subject ID.",
                    nameof(subjectId));
            if (!Enum.IsDefined(typeof(DisplacementResolutionFailure), failure))
                throw new ArgumentOutOfRangeException(nameof(failure));
            if (failure == DisplacementResolutionFailure.None && action == null)
                throw new ArgumentException(
                    "Eligible displacement destinations require an action definition.",
                    nameof(action));

            ActorId = actorId;
            ActionId = actionId;
            SubjectId = subjectId;
            Origin = origin;
            Destination = destination;
            Failure = failure;
            Action = action;
        }

        public string ActorId { get; }

        public string ActionId { get; }

        public string SubjectId { get; }

        public GameplayPosition Origin { get; }

        public GameplayPosition Destination { get; }

        public DisplacementResolutionFailure Failure { get; }

        public DisplacementActionDefinition Action { get; }

        public float Distance => Origin.DistanceTo(Destination);

        public bool IsEligible => Failure == DisplacementResolutionFailure.None;
    }
}
