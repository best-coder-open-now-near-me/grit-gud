using System;
using GritGud.Domain.Gameplay;

namespace GritGud.Application.Gameplay
{
    internal sealed class DisplacementRecordCommitValidator
    {
        private readonly GameplaySession gameplay;
        private readonly DestructiblePropSession destructibles;

        public DisplacementRecordCommitValidator(
            GameplaySession gameplaySession,
            DestructiblePropSession destructibleSession)
        {
            gameplay = gameplaySession ??
                throw new ArgumentNullException(nameof(gameplaySession));
            destructibles = destructibleSession ??
                throw new ArgumentNullException(nameof(destructibleSession));
        }

        public void Validate(
            DisplacementRecord record,
            long expectedSequence)
        {
            if (record == null)
                throw new ArgumentNullException(nameof(record));
            if (expectedSequence <= 0L)
                throw new ArgumentOutOfRangeException(nameof(expectedSequence));
            if (record.Sequence != expectedSequence)
            {
                throw new InvalidOperationException(
                    "The displacement record is not the next authoritative sequence.");
            }

            if (record.Request.SubjectKind == DisplacementSubjectKind.Prop)
            {
                ValidateProp(record);
                return;
            }

            if (gameplay.GetActor(record.Request.SubjectId).Pose.Position
                .DistanceTo(record.PreviousPosition) > 0f)
            {
                throw new InvalidOperationException(
                    "The displacement record no longer starts at authoritative position.");
            }
        }

        private void ValidateProp(DisplacementRecord record)
        {
            DestructiblePropSnapshot current = destructibles.GetProp(
                record.Request.SubjectId);
            if (record.PreviousPropState == null
                || current.Posture != record.PreviousPropState.Posture
                || !current.Pose.HasSameState(record.PreviousPropState.Pose))
            {
                throw new InvalidOperationException(
                    "The displacement record no longer starts from authoritative prop state.");
            }

            gameplay.ValidatePinTransition(record.PinTransition);
        }
    }
}
