using System;
using System.Collections.Generic;

namespace GritGud.Domain.Gameplay
{
    public enum EmergencyReactionWindowStatus
    {
        Pending,
        Active,
        Completed,
    }

    public enum EmergencyReactionCompletionReason
    {
        ProjectileImpacted,
        ProjectileExpired,
        ResponsePassCompleted,
    }

    public sealed class EmergencyReactionWindowRecord
    {
        public EmergencyReactionWindowRecord(
            long sequence,
            string triggerType,
            string triggerId,
            string initiatorActorId,
            IEnumerable<string> responderIds,
            int actionPointAllowance,
            EmergencyReactionWindowStatus status)
        {
            if (sequence <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sequence));
            }
            if (string.IsNullOrWhiteSpace(triggerType))
            {
                throw new ArgumentException(
                    "Emergency windows require a trigger type.",
                    nameof(triggerType));
            }
            if (string.IsNullOrWhiteSpace(triggerId))
                throw new ArgumentException("Emergency windows require a trigger.", nameof(triggerId));
            if (string.IsNullOrWhiteSpace(initiatorActorId))
            {
                throw new ArgumentException(
                    "Emergency windows require an attacker.",
                    nameof(initiatorActorId));
            }
            if (responderIds == null)
            {
                throw new ArgumentNullException(nameof(responderIds));
            }
            if (actionPointAllowance <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(actionPointAllowance));
            }
            var responders = new List<string>(responderIds);
            if (responders.Count == 0)
            {
                throw new ArgumentException(
                    "Emergency windows require at least one responder.",
                    nameof(responderIds));
            }
            var unique = new HashSet<string>(StringComparer.Ordinal);
            foreach (string responderId in responders)
            {
                if (string.IsNullOrWhiteSpace(responderId)
                    || !unique.Add(responderId)
                    || string.Equals(
                        responderId,
                        initiatorActorId,
                        StringComparison.Ordinal))
                {
                    throw new ArgumentException(
                        "Emergency responders must be unique, defined, and different from the attacker.",
                        nameof(responderIds));
                }
            }
            if (!Enum.IsDefined(typeof(EmergencyReactionWindowStatus), status))
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }
            Sequence = sequence;
            TriggerType = triggerType;
            TriggerId = triggerId;
            InitiatorActorId = initiatorActorId;
            ResponderIds = responders.AsReadOnly();
            ActionPointAllowance = actionPointAllowance;
            Status = status;
        }

        public long Sequence { get; }
        public string TriggerType { get; }
        public string TriggerId { get; }
        public string InitiatorActorId { get; }
        public IReadOnlyList<string> ResponderIds { get; }
        public int ActionPointAllowance { get; }
        public EmergencyReactionWindowStatus Status { get; }

        public EmergencyReactionWindowRecord WithStatus(EmergencyReactionWindowStatus status) =>
            new EmergencyReactionWindowRecord(
                Sequence, TriggerType, TriggerId, InitiatorActorId,
                ResponderIds, ActionPointAllowance, status);
    }
}
