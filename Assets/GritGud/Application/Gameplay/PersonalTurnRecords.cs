using System;
using GritGud.Domain.Turns;

namespace GritGud.Application.Gameplay
{
    public sealed class PersonalTurnStartRecord
    {
        public PersonalTurnStartRecord(
            string actorId,
            PersonalTurnActionPointGrant actionPoints,
            float refreshedMovement)
        {
            if (string.IsNullOrWhiteSpace(actorId))
                throw new ArgumentException(
                    "Personal-turn starts require an actor ID.",
                    nameof(actorId));
            GameplayNumericPolicy.RequireFinite(
                refreshedMovement,
                nameof(refreshedMovement));
            if (refreshedMovement < 0f)
                throw new ArgumentOutOfRangeException(nameof(refreshedMovement));
            ActorId = actorId;
            ActionPoints = actionPoints;
            RefreshedMovement = refreshedMovement;
        }

        public string ActorId { get; }
        public PersonalTurnActionPointGrant ActionPoints { get; }
        public float RefreshedMovement { get; }
    }

    public sealed class TurnEndRecord
    {
        public TurnEndRecord(
            long sequence,
            string endingActorId,
            string nextActorId,
            GameplayTurnKind kind = GameplayTurnKind.Normal,
            string interruptedActorId = null,
            PersonalTurnStartRecord personalTurnStart = null)
        {
            if (sequence <= 0)
                throw new ArgumentOutOfRangeException(nameof(sequence));
            EndingActorId = RequireActorId(endingActorId, nameof(endingActorId));
            NextActorId = RequireActorId(nextActorId, nameof(nextActorId));
            if (!Enum.IsDefined(typeof(GameplayTurnKind), kind))
                throw new ArgumentOutOfRangeException(nameof(kind));
            if (kind == GameplayTurnKind.EmergencyReaction
                && string.IsNullOrWhiteSpace(interruptedActorId))
                throw new ArgumentException(
                    "Emergency turns require the interrupted actor identifier.",
                    nameof(interruptedActorId));
            if (personalTurnStart != null
                && !string.Equals(
                    personalTurnStart.ActorId,
                    NextActorId,
                    StringComparison.Ordinal))
                throw new ArgumentException(
                    "Personal-turn grants must belong to the next actor.",
                    nameof(personalTurnStart));
            Sequence = sequence;
            Kind = kind;
            InterruptedActorId = interruptedActorId ?? string.Empty;
            PersonalTurnStart = personalTurnStart;
        }

        public long Sequence { get; }
        public string EndingActorId { get; }
        public string NextActorId { get; }
        public GameplayTurnKind Kind { get; }
        public string InterruptedActorId { get; }
        public PersonalTurnStartRecord PersonalTurnStart { get; }

        private static string RequireActorId(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException(
                    "Turn records require actor identifiers.",
                    parameterName);
            return value;
        }
    }
}
