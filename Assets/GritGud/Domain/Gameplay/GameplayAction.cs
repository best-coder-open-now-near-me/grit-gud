using System;
using System.Collections.Generic;
using GritGud.Domain.Turns;

namespace GritGud.Domain.Gameplay
{
    public sealed class GameplayInteractionDefinition
    {
        public GameplayInteractionDefinition(
            string id,
            string displayName,
            ActionCost turnCost)
        {
            Id = RequireText(id, nameof(id));
            DisplayName = RequireText(displayName, nameof(displayName));
            TurnCost = turnCost;
        }

        public string Id { get; }

        public string DisplayName { get; }

        public ActionCost TurnCost { get; }

        private static string RequireText(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    "Interaction fields cannot be empty.",
                    parameterName);
            }

            return value;
        }
    }

    public readonly struct GameplayActionRequest
    {
        public GameplayActionRequest(
            string actorId,
            string actionId,
            string targetId)
        {
            if (string.IsNullOrWhiteSpace(actorId))
            {
                throw new ArgumentException(
                    "Actor identifiers cannot be empty.",
                    nameof(actorId));
            }

            if (string.IsNullOrWhiteSpace(actionId))
            {
                throw new ArgumentException(
                    "Action identifiers cannot be empty.",
                    nameof(actionId));
            }

            if (string.IsNullOrWhiteSpace(targetId))
            {
                throw new ArgumentException(
                    "Targeted actions require a target identifier.",
                    nameof(targetId));
            }

            ActorId = actorId;
            ActionId = actionId;
            TargetId = targetId;
        }

        public string ActorId { get; }

        public string ActionId { get; }

        public string TargetId { get; }
    }

    public abstract class GameplayActionOutcome
    {
        protected GameplayActionOutcome(string targetId)
        {
            if (string.IsNullOrWhiteSpace(targetId))
            {
                throw new ArgumentException(
                    "Action outcomes require a target identifier.",
                    nameof(targetId));
            }

            TargetId = targetId;
        }

        public string TargetId { get; }
    }

    public sealed class ObjectiveCompletedActionOutcome : GameplayActionOutcome
    {
        public ObjectiveCompletedActionOutcome(string objectiveId)
            : base(objectiveId)
        {
        }

        public string ObjectiveId => TargetId;
    }

    public sealed class AttackResolvedActionOutcome : GameplayActionOutcome
    {
        public AttackResolvedActionOutcome(AttackResolutionRecord attack)
            : base((attack ?? throw new ArgumentNullException(nameof(attack))).TargetId)
        {
            Attack = attack;
        }

        public AttackResolutionRecord Attack { get; }
    }

    public sealed class WeaponDischargedActionOutcome : GameplayActionOutcome
    {
        public WeaponDischargedActionOutcome(WeaponDischargeRecord discharge)
            : base((discharge ?? throw new ArgumentNullException(
                nameof(discharge))).TargetId)
        {
            Discharge = discharge;
        }

        public WeaponDischargeRecord Discharge { get; }
    }

    public sealed class ProjectileLaunchedActionOutcome : GameplayActionOutcome
    {
        public ProjectileLaunchedActionOutcome(ProjectileLaunchRecord launch)
            : base((launch ?? throw new ArgumentNullException(nameof(launch)))
                .IntendedTargetId)
        {
            Launch = launch;
        }

        public ProjectileLaunchRecord Launch { get; }
    }

    public sealed class EquipmentChangedActionOutcome : GameplayActionOutcome
    {
        public EquipmentChangedActionOutcome(EquipmentChangeRecord change)
            : base((change ?? throw new ArgumentNullException(nameof(change))).ItemId)
        {
            Change = change;
        }

        public EquipmentChangeRecord Change { get; }
    }

    public sealed class GameplayActionRecord
    {
        private readonly IReadOnlyList<GameplayActionOutcome> outcomes;

        public GameplayActionRecord(
            long sequence,
            GameplayActionRequest request,
            ActionCost cost,
            TurnBudget previousBudget,
            TurnBudget resultingBudget,
            IEnumerable<GameplayActionOutcome> outcomes)
        {
            if (sequence <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sequence));
            }

            if (outcomes == null)
            {
                throw new ArgumentNullException(nameof(outcomes));
            }

            var copiedOutcomes = new List<GameplayActionOutcome>();
            foreach (GameplayActionOutcome outcome in outcomes)
            {
                if (outcome == null)
                {
                    throw new ArgumentException(
                        "Action outcomes cannot contain null entries.",
                        nameof(outcomes));
                }

                copiedOutcomes.Add(outcome);
            }

            if (copiedOutcomes.Count == 0)
            {
                throw new ArgumentException(
                    "A resolved action requires at least one explicit outcome.",
                    nameof(outcomes));
            }

            Sequence = sequence;
            Request = request;
            Cost = cost;
            PreviousBudget = previousBudget;
            ResultingBudget = resultingBudget;
            this.outcomes = copiedOutcomes.AsReadOnly();
        }

        public long Sequence { get; }

        public GameplayActionRequest Request { get; }

        public ActionCost Cost { get; }

        public TurnBudget PreviousBudget { get; }

        public TurnBudget ResultingBudget { get; }

        public IReadOnlyList<GameplayActionOutcome> Outcomes => outcomes;
    }
}
