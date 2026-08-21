using System;
using GritGud.Domain.Gameplay;

namespace GritGud.Application.Gameplay
{
    public enum StanceChangeFailure
    {
        None,
        AlreadyInStance,
        ActorNotActive,
        OperationInProgress,
        SpatiallyBlocked,
        ActorPinned,
    }

    public readonly struct StanceTransitionValidation
    {
        private StanceTransitionValidation(bool accepted, string failureCode)
        {
            Accepted = accepted;
            FailureCode = failureCode ?? string.Empty;
        }

        public bool Accepted { get; }
        public string FailureCode { get; }

        public static StanceTransitionValidation Allowed() =>
            new StanceTransitionValidation(true, string.Empty);

        public static StanceTransitionValidation Blocked(string failureCode)
        {
            if (string.IsNullOrWhiteSpace(failureCode))
            {
                throw new ArgumentException(
                    "Blocked stance transitions require a stable failure code.",
                    nameof(failureCode));
            }

            return new StanceTransitionValidation(false, failureCode);
        }
    }

    public interface IStanceTransitionValidator
    {
        StanceTransitionValidation Validate(
            GameplayActorSnapshot actor,
            ActorStance requestedStance);
    }

    public sealed class StanceChangeResolver
    {
        private readonly GameplaySession session;
        private readonly IStanceTransitionValidator validator;

        public StanceChangeResolver(
            GameplaySession session,
            IStanceTransitionValidator validator)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.validator = validator ?? throw new ArgumentNullException(nameof(validator));
        }

        public bool TryResolve(
            string actorId,
            ActorStance requestedStance,
            out StanceChangeRecord record,
            out StanceChangeFailure failure,
            out string failureCode)
        {
            if (string.IsNullOrWhiteSpace(actorId))
            {
                throw new ArgumentException(
                    "Actor identifiers cannot be empty.",
                    nameof(actorId));
            }

            if (!Enum.IsDefined(typeof(ActorStance), requestedStance))
            {
                throw new ArgumentOutOfRangeException(nameof(requestedStance));
            }

            record = null;
            failureCode = string.Empty;
            if (session.Mode == GameplaySessionMode.TurnBased)
            {
                if (session.Operation != GameplaySessionOperation.None)
                {
                    failure = StanceChangeFailure.OperationInProgress;
                    return false;
                }

                if (!string.Equals(session.ActiveActorId, actorId, StringComparison.Ordinal))
                {
                    failure = StanceChangeFailure.ActorNotActive;
                    return false;
                }
            }

            GameplayActorSnapshot actor = session.GetActor(actorId);
            if (actor.IsPinned)
            {
                failure = StanceChangeFailure.ActorPinned;
                failureCode = "actor.pinned";
                return false;
            }
            if (actor.Pose.Stance == requestedStance)
            {
                failure = StanceChangeFailure.AlreadyInStance;
                return false;
            }

            StanceTransitionValidation validation = validator.Validate(actor, requestedStance);
            if (!validation.Accepted)
            {
                failure = StanceChangeFailure.SpatiallyBlocked;
                failureCode = validation.FailureCode;
                return false;
            }

            var resultingPose = new GameplayActorPose(
                actor.Pose.Position,
                actor.Pose.FacingDegrees,
                requestedStance);
            record = new StanceChangeRecord(actorId, actor.Pose, resultingPose);
            session.CommitStanceChange(record);
            failure = StanceChangeFailure.None;
            return true;
        }
    }
}
