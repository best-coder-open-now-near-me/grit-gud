using System;
using GritGud.Domain.Gameplay;

namespace GritGud.Application.Gameplay
{
    internal sealed class GameplayThrownExplosiveOutcomeValidator :
        GameplayActionOutcomeValidator<ThrownExplosiveActionOutcome>
    {
        private readonly GameplaySession session;

        public GameplayThrownExplosiveOutcomeValidator(GameplaySession session)
        {
            this.session = session ?? throw new ArgumentNullException(
                nameof(session));
        }

        protected override void Validate(
            GameplayActionRecord action,
            ThrownExplosiveActionOutcome outcome)
        {
            ThrownExplosiveRecord thrown = outcome.Record;
            if (thrown == null
                || thrown.Definition == null
                || !string.Equals(
                    action.Request.ActorId,
                    thrown.ThrowerId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    action.Request.ActionId,
                    thrown.Definition.Id,
                    StringComparison.Ordinal)
                || !string.Equals(
                    action.Request.TargetId,
                    thrown.Definition.Id,
                    StringComparison.Ordinal)
                || !GameplayActionValidationRules.ActionCostsMatch(
                    action.Cost,
                    GameplayActionValidationRules.GetThrownExplosiveActionCost(
                        session,
                        thrown.Definition,
                        action)))
            {
                throw new InvalidOperationException(
                    "The thrown explosive does not match its action request.");
            }

            GameplayActorState actor = session.RequireActor(thrown.ThrowerId);
            InventoryItemDefinition item = session
                .RequireActorDefinition(thrown.ThrowerId)
                .GetInventoryItem(thrown.Definition.Id);
            if (!GameplayActionValidationRules.ThrownExplosiveDefinitionsMatch(
                    item?.ConsumablePower as ThrownExplosiveDefinition,
                    thrown.Definition))
            {
                throw new InvalidOperationException(
                    "The actor does not own the recorded thrown explosive.");
            }

            if (actor.Pose.Position.DistanceTo(thrown.Origin) > 0f)
            {
                throw new InvalidOperationException(
                    "The throw no longer starts at the actor's position.");
            }

            if (thrown.Definition.GetLaunchOrigin(actor.Pose)
                    .DistanceTo(thrown.LaunchOrigin) > 0f)
            {
                throw new InvalidOperationException(
                    "The throw no longer starts at its authored launch origin.");
            }

            InventoryQuantityChangeRecord quantity =
                GameplayInventoryQuantityOutcomeValidator
                    .FindInventoryQuantityChange(
                        action,
                        thrown.Definition.Id);
            if (quantity == null
                || !string.Equals(
                    quantity.ActorId,
                    thrown.ThrowerId,
                    StringComparison.Ordinal)
                || quantity.ConsumedQuantity != 1)
            {
                throw new InvalidOperationException(
                    "A thrown explosive must consume exactly one matching inventory item in the same action.");
            }
        }
    }

    internal sealed class GameplayInventoryQuantityOutcomeValidator :
        GameplayActionOutcomeValidator<InventoryQuantityChangedActionOutcome>
    {
        private readonly GameplaySession session;

        public GameplayInventoryQuantityOutcomeValidator(GameplaySession session)
        {
            this.session = session ?? throw new ArgumentNullException(
                nameof(session));
        }

        protected override void Validate(
            GameplayActionRecord action,
            InventoryQuantityChangedActionOutcome outcome)
        {
            InventoryQuantityChangeRecord change = outcome.Change;
            if (change == null
                || !string.Equals(
                    action.Request.ActorId,
                    change.ActorId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    action.Request.TargetId,
                    change.ItemId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "The inventory quantity change does not match its action request.");
            }

            InventoryItemDefinition item = session
                .RequireActorDefinition(change.ActorId)
                .GetInventoryItem(change.ItemId);
            GameplayActorState actor = session.RequireActor(change.ActorId);
            int pairedThrowCount = 0;
            foreach (GameplayActionOutcome pairedOutcome in action.Outcomes)
            {
                if (pairedOutcome is ThrownExplosiveActionOutcome thrown
                    && thrown.Record?.Definition != null
                    && string.Equals(
                        thrown.Record.Definition.Id,
                        change.ItemId,
                        StringComparison.Ordinal))
                {
                    pairedThrowCount++;
                }
            }

            if (item == null
                || item.Kind != InventoryItemKind.Consumable
                || pairedThrowCount != 1
                || change.ConsumedQuantity != 1
                || actor.GetInventoryQuantity(change.ItemId)
                    != change.PreviousQuantity)
            {
                throw new InvalidOperationException(
                    "The inventory quantity change is not valid for the actor's authoritative state.");
            }
        }

        internal static InventoryQuantityChangeRecord
            FindInventoryQuantityChange(
                GameplayActionRecord action,
                string itemId)
        {
            InventoryQuantityChangeRecord matched = null;
            foreach (GameplayActionOutcome outcome in action.Outcomes)
            {
                if (outcome is InventoryQuantityChangedActionOutcome inventory
                    && string.Equals(
                        inventory.Change?.ItemId,
                        itemId,
                        StringComparison.Ordinal))
                {
                    if (matched != null)
                    {
                        throw new InvalidOperationException(
                            "A thrown explosive action must contain exactly one matching inventory quantity change.");
                    }

                    matched = inventory.Change;
                }
            }

            return matched;
        }
    }
}
