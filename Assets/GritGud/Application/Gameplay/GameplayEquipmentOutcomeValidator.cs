using System;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;

namespace GritGud.Application.Gameplay
{
    internal sealed class GameplayEquipmentOutcomeValidator :
        GameplayActionOutcomeValidator<EquipmentChangedActionOutcome>
    {
        private readonly GameplaySession session;

        public GameplayEquipmentOutcomeValidator(GameplaySession session)
        {
            this.session = session ?? throw new ArgumentNullException(
                nameof(session));
        }

        protected override void Validate(
            GameplayActionRecord action,
            EquipmentChangedActionOutcome outcome)
        {
            foreach (GameplayActionOutcome pairedOutcome in action.Outcomes)
            {
                if (pairedOutcome is DisplacementActionOutcome)
                {
                    // The displacement validator owns its automatic equipment
                    // transition as part of the composite action.
                    return;
                }
            }

            EquipmentChangeRecord change = outcome.Change;
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
                    "The equipment change does not match its action request.");
            }

            GameplayActorState actor = session.RequireActor(change.ActorId);
            ScenarioActorDefinition definition = session
                .RequireActorDefinition(change.ActorId);
            InventoryItemDefinition item = definition.GetInventoryItem(
                change.ItemId);
            if (item == null)
            {
                throw new InvalidOperationException(
                    "The actor does not own the recorded equipment change.");
            }

            ActionCost expectedCost = session.Mode == GameplaySessionMode.TurnBased
                ? item.EquipmentCost
                : new ActionCost(0, 0f, item.EquipmentCost.Mobility);
            if (!item.IsEquippable
                || !string.Equals(
                    actor.EquippedItemId,
                    change.PreviousEquippedItemId,
                    StringComparison.Ordinal)
                || !GameplayActionValidationRules.ActionCostsMatch(
                    action.Cost,
                    expectedCost))
            {
                throw new InvalidOperationException(
                    "The actor does not own the recorded equipment change.");
            }

            string expectedActionId = change.Kind == EquipmentChangeKind.Equip
                ? EquipmentActionIds.Equip
                : EquipmentActionIds.Unequip;
            string expectedResult = change.Kind == EquipmentChangeKind.Equip
                ? item.Id
                : null;
            if (!string.Equals(
                    action.Request.ActionId,
                    expectedActionId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    change.ResultingEquippedItemId,
                    expectedResult,
                    StringComparison.Ordinal)
                || (change.Kind == EquipmentChangeKind.Equip
                    && change.PreviousEquippedItemId != null))
            {
                throw new InvalidOperationException(
                    "The recorded equipment transition is invalid.");
            }
        }
    }
}
