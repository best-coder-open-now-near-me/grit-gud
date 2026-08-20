using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;

namespace GritGud.Application.Gameplay
{
    internal sealed class GameplayActionCommitValidator
    {
        private readonly GameplaySession session;
        private readonly IReadOnlyDictionary<Type, IGameplayActionOutcomeValidator>
            validators;
        private readonly IReadOnlyCollection<Type> supportedOutcomeTypes;

        public GameplayActionCommitValidator(GameplaySession session)
        {
            this.session = session ?? throw new ArgumentNullException(
                nameof(session));
            IGameplayActionOutcomeValidator[] registered =
            {
                new GameplayObjectiveOutcomeValidator(session),
                new GameplayAttackOutcomeValidator(session),
                new GameplayWeaponDischargeOutcomeValidator(session),
                new GameplayProjectileLaunchOutcomeValidator(session),
                new GameplayAmmunitionSpentOutcomeValidator(session),
                new GameplayEquipmentOutcomeValidator(session),
                new GameplayThrownExplosiveOutcomeValidator(session),
                new GameplayInventoryQuantityOutcomeValidator(session),
                new GameplayDisplacementOutcomeValidator(session),
            };
            var index = new Dictionary<Type, IGameplayActionOutcomeValidator>();
            foreach (IGameplayActionOutcomeValidator validator in registered)
            {
                if (!index.TryAdd(validator.OutcomeType, validator))
                {
                    throw new InvalidOperationException(
                        $"Outcome validator '{validator.OutcomeType.Name}' is registered more than once.");
                }
            }

            validators = index;
            supportedOutcomeTypes = Array.AsReadOnly(
                new List<Type>(index.Keys).ToArray());
        }

        public IReadOnlyCollection<Type> SupportedOutcomeTypes =>
            supportedOutcomeTypes;

        public void Validate(GameplayActionRecord record)
        {
            if (record == null)
                throw new ArgumentNullException(nameof(record));

            GameplayActorState actor = session.RequireActionActor(
                record.Request.ActorId);
            if (record.Sequence != session.NextActionSequence)
            {
                throw new InvalidOperationException(
                    "The action record is not the next authoritative sequence.");
            }

            if (!GameplayActionValidationRules.TurnBudgetsMatch(
                    actor.TurnBudget,
                    record.PreviousBudget))
            {
                throw new InvalidOperationException(
                    "The action no longer begins at the actor's authoritative budget.");
            }

            TurnBudget expectedBudget = actor.TurnBudget.SpendAction(record.Cost);
            if (!GameplayActionValidationRules.TurnBudgetsMatch(
                    expectedBudget,
                    record.ResultingBudget))
            {
                throw new InvalidOperationException(
                    "The action record's resulting budget does not match its cost.");
            }

            var outcomeKeys = new HashSet<string>(StringComparer.Ordinal);
            bool hasFiringPrimary = false;
            bool hasAmmunitionSpend = false;
            foreach (GameplayActionOutcome outcome in record.Outcomes)
            {
                if (outcome == null)
                {
                    throw new InvalidOperationException(
                        "An action record cannot contain a null outcome.");
                }

                string outcomeKey = outcome.GetType().FullName
                    + ":"
                    + (outcome.TargetId ?? string.Empty);
                if (!outcomeKeys.Add(outcomeKey))
                {
                    throw new InvalidOperationException(
                        "An action record cannot repeat the same authoritative outcome.");
                }

                if (!validators.TryGetValue(
                        outcome.GetType(),
                        out IGameplayActionOutcomeValidator validator))
                {
                    throw new InvalidOperationException(
                        $"Unsupported action outcome '{outcome.GetType().Name}'.");
                }

                validator.Validate(record, outcome);
                hasFiringPrimary |= outcome is AttackResolvedActionOutcome
                    || outcome is WeaponDischargedActionOutcome
                    || outcome is ProjectileLaunchedActionOutcome;
                hasAmmunitionSpend |=
                    outcome is AmmunitionSpentActionOutcome;
            }
            if (hasFiringPrimary)
                GameplayWeaponActionOutcomes.ValidateFiringGrammar(
                    record,
                    actor.CreateSnapshot());
            else if (hasAmmunitionSpend)
                throw new InvalidOperationException(
                    "Ammunition cannot be spent without a firing outcome.");
        }
    }
}
