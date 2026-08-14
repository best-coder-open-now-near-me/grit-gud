using System;

namespace GritGud.Domain.Gameplay
{
    public sealed class StanceChangeRecord
    {
        public StanceChangeRecord(
            string actorId,
            GameplayActorPose previousPose,
            GameplayActorPose resultingPose)
        {
            if (string.IsNullOrWhiteSpace(actorId))
            {
                throw new ArgumentException(
                    "Actor identifiers cannot be empty.",
                    nameof(actorId));
            }

            if (previousPose.Stance == resultingPose.Stance)
            {
                throw new ArgumentException(
                    "A stance-change record must change the actor's stance.",
                    nameof(resultingPose));
            }

            if (previousPose.Position.X != resultingPose.Position.X
                || previousPose.Position.Y != resultingPose.Position.Y
                || previousPose.Position.Z != resultingPose.Position.Z
                || previousPose.FacingDegrees != resultingPose.FacingDegrees)
            {
                throw new ArgumentException(
                    "A stance change cannot move or rotate the actor.",
                    nameof(resultingPose));
            }

            ActorId = actorId;
            PreviousPose = previousPose;
            ResultingPose = resultingPose;
        }

        public string ActorId { get; }
        public GameplayActorPose PreviousPose { get; }
        public GameplayActorPose ResultingPose { get; }
    }
}
