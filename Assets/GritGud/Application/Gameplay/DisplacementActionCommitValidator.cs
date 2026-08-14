using System;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;

namespace GritGud.Application.Gameplay
{
    internal static class DisplacementActionCommitValidator
    {
        internal static void Validate(
            GameplayActionRecord action,
            DisplacementRecord displacement,
            DisplacementActionDefinition definition,
            InventoryItemDefinition equippedItem,
            bool chargesTurnCost)
        {
            if (action == null
                || displacement == null
                || definition == null
                || !string.Equals(
                    action.Request.ActorId,
                    displacement.Request.ActorId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    action.Request.ActionId,
                    displacement.Request.ActionId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    action.Request.TargetId,
                    displacement.Request.SubjectId,
                    StringComparison.Ordinal)
                || displacement.Request.ActionKind != definition.Intent
                || !definition.Accepts(displacement.Request.SubjectKind)
                || displacement.Request.SubjectMass
                    > definition.MaximumSubjectMass
                || displacement.Request.SubjectSize
                    > definition.MaximumSubjectSize
                || (displacement.AppliedResults & ~definition.AllowedResults)
                    != 0)
            {
                throw InvalidDisplacement();
            }

            EquipmentChangeRecord autoStow = null;
            int displacementOutcomeCount = 0;
            foreach (GameplayActionOutcome outcome in action.Outcomes)
            {
                if (outcome is DisplacementActionOutcome)
                {
                    displacementOutcomeCount++;
                }
                else if (outcome is EquipmentChangedActionOutcome equipment)
                {
                    if (autoStow != null)
                        throw InvalidDisplacement();
                    autoStow = equipment.Change;
                }
            }

            if (displacementOutcomeCount != 1)
                throw InvalidDisplacement();

            ActionCost expectedCost = ResolveCost(
                definition.Cost,
                chargesTurnCost);
            bool handsAvailable = definition.HasRequiredFreeHands(
                equippedItem?.OccupiedHands ?? 0);
            if (autoStow == null)
            {
                if (!handsAvailable
                    || !ActionCostsMatch(action.Cost, expectedCost))
                {
                    throw InvalidDisplacement();
                }

                return;
            }

            if (handsAvailable
                || equippedItem == null
                || !equippedItem.IsEquippable
                || definition.AutoStowPolicy
                    != DisplacementAutoStowPolicy.Allowed
                || autoStow.Kind != EquipmentChangeKind.Unequip
                || !string.Equals(
                    autoStow.ActorId,
                    action.Request.ActorId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    autoStow.ItemId,
                    equippedItem.Id,
                    StringComparison.Ordinal)
                || !string.Equals(
                    autoStow.PreviousEquippedItemId,
                    equippedItem.Id,
                    StringComparison.Ordinal)
                || autoStow.ResultingEquippedItemId != null)
            {
                throw InvalidDisplacement();
            }

            expectedCost = ActionCost.Combine(
                expectedCost,
                ResolveCost(
                    equippedItem.EquipmentCost,
                    chargesTurnCost));
            if (!ActionCostsMatch(action.Cost, expectedCost))
                throw InvalidDisplacement();
        }

        private static ActionCost ResolveCost(
            ActionCost authored,
            bool chargesTurnCost) =>
            chargesTurnCost
                ? authored
                : new ActionCost(0, 0f, authored.Mobility);

        private static bool ActionCostsMatch(ActionCost left, ActionCost right) =>
            left.ActionPoints == right.ActionPoints
            && left.MovementOpportunity == right.MovementOpportunity
            && left.Mobility == right.Mobility;

        private static InvalidOperationException InvalidDisplacement() =>
            new InvalidOperationException(
                "The displacement does not match its authored action request.");
    }
}
