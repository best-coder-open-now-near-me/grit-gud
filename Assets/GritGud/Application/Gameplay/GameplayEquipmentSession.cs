using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;

namespace GritGud.Application.Gameplay
{
    public enum EquipmentChangeFailure
    {
        None,
        ActorNotActive,
        OperationInProgress,
        ItemNotFound,
        ItemNotEquippable,
        MustUnequipCurrentItem,
        AlreadyInRequestedState,
        InsufficientActionPoints,
        InsufficientMovementOpportunity,
    }

    public sealed class GameplayEquipmentSession
    {
        private readonly GameplaySession gameplay;
        private readonly List<EquipmentChangeRecord> records =
            new List<EquipmentChangeRecord>();
        private readonly IReadOnlyList<EquipmentChangeRecord> readOnlyRecords;

        public GameplayEquipmentSession(GameplaySession gameplaySession)
        {
            gameplay = gameplaySession ??
                throw new ArgumentNullException(nameof(gameplaySession));
            readOnlyRecords = records.AsReadOnly();
        }

        public IReadOnlyList<EquipmentChangeRecord> Records => readOnlyRecords;

        public InventoryEquipmentAvailability EvaluateAvailability(
            string actorId,
            string itemId)
        {
            if (gameplay.Operation != GameplaySessionOperation.None)
            {
                return Availability(
                    null,
                    default,
                    isSwitch: false,
                    EquipmentChangeFailure.OperationInProgress);
            }

            if (!gameplay.TryGetActor(
                    actorId,
                    out GameplayActorSnapshot actor)
                || (gameplay.Mode == GameplaySessionMode.TurnBased
                    && !string.Equals(
                        gameplay.ActiveActorId,
                        actorId,
                        StringComparison.Ordinal)))
            {
                return Availability(
                    null,
                    default,
                    isSwitch: false,
                    EquipmentChangeFailure.ActorNotActive);
            }

            InventoryItemDefinition requested = gameplay.GetInventoryItem(
                actorId,
                itemId);
            if (requested == null)
            {
                return Availability(
                    null,
                    default,
                    isSwitch: false,
                    EquipmentChangeFailure.ItemNotFound);
            }

            if (!requested.IsEquippable)
            {
                return Availability(
                    requested,
                    default,
                    isSwitch: false,
                    EquipmentChangeFailure.ItemNotEquippable);
            }

            bool isEquipped = string.Equals(
                actor.EquippedItemId,
                requested.Id,
                StringComparison.Ordinal);
            InventoryItemDefinition current = isEquipped
                || actor.EquippedItemId == null
                    ? null
                    : gameplay.GetInventoryItem(
                        actorId,
                        actor.EquippedItemId);
            bool isSwitch = current != null;
            ActionCost cost = gameplay.Mode == GameplaySessionMode.TurnBased
                ? isSwitch
                    ? Combine(current.EquipmentCost, requested.EquipmentCost)
                    : requested.EquipmentCost
                : new ActionCost(0, 0f, requested.EquipmentCost.Mobility);
            EquipmentChangeFailure failure = !actor.TurnBudget.CanAfford(cost)
                ? actor.TurnBudget.ActionPoints < cost.ActionPoints
                    ? EquipmentChangeFailure.InsufficientActionPoints
                    : EquipmentChangeFailure.InsufficientMovementOpportunity
                : EquipmentChangeFailure.None;
            return Availability(requested, cost, isSwitch, failure);
        }

        public bool TryResolve(
            string actorId,
            string itemId,
            bool equip,
            out GameplayActionRecord action,
            out EquipmentChangeFailure failure)
        {
            var notifications = new GameplayNotificationBatch();
            bool resolved = TryResolve(
                actorId,
                itemId,
                equip,
                notifications,
                out action,
                out failure);
            if (resolved)
            {
                notifications.Publish();
            }

            return resolved;
        }

        private bool TryResolve(
            string actorId,
            string itemId,
            bool equip,
            GameplayNotificationBatch notifications,
            out GameplayActionRecord action,
            out EquipmentChangeFailure failure)
        {
            action = null;
            if (!TryPrepare(
                    actorId,
                    itemId,
                    equip,
                    out InventoryItemDefinition item,
                    out GameplayActorSnapshot actor,
                    out EquipmentChangeRecord change,
                    out failure))
            {
                return false;
            }

            ActionCost cost = GetActionCost(item);
            TurnBudget resultingBudget = actor.TurnBudget.SpendAction(cost);
            long sequence = gameplay.LastResolvedAction == null
                ? 1L
                : gameplay.LastResolvedAction.Sequence + 1L;
            action = new GameplayActionRecord(
                sequence,
                new GameplayActionRequest(
                    actorId,
                    equip ? EquipmentActionIds.Equip : EquipmentActionIds.Unequip,
                    itemId),
                cost,
                actor.TurnBudget,
                resultingBudget,
                new[] { new EquipmentChangedActionOutcome(change) });
            Commit(action, notifications);
            failure = EquipmentChangeFailure.None;
            return true;
        }

        public bool TryResolveSwitch(
            string actorId,
            string itemId,
            out GameplayActionRecord unequipAction,
            out GameplayActionRecord equipAction,
            out EquipmentChangeFailure failure)
        {
            unequipAction = null;
            equipAction = null;
            InventoryEquipmentAvailability availability =
                EvaluateAvailability(actorId, itemId);
            if (!availability.IsAvailable)
            {
                failure = availability.Failure;
                return false;
            }

            InventoryItemDefinition target = availability.Item;
            GameplayActorSnapshot actor = gameplay.GetActor(actorId);

            if (actor.EquippedItemId == null)
            {
                return TryResolve(
                    actorId,
                    itemId,
                    equip: true,
                    out equipAction,
                    out failure);
            }

            if (string.Equals(
                    actor.EquippedItemId,
                    itemId,
                    StringComparison.Ordinal))
            {
                failure = EquipmentChangeFailure.AlreadyInRequestedState;
                return false;
            }

            InventoryItemDefinition current = gameplay.GetInventoryItem(
                actorId,
                actor.EquippedItemId);
            if (current == null || !current.IsEquippable)
            {
                failure = EquipmentChangeFailure.ItemNotEquippable;
                return false;
            }

            var notifications = new GameplayNotificationBatch();
            if (!TryResolve(
                    actorId,
                    current.Id,
                    equip: false,
                    notifications,
                    out unequipAction,
                    out failure))
            {
                return false;
            }

            if (!TryResolve(
                    actorId,
                    target.Id,
                    equip: true,
                    notifications,
                    out equipAction,
                    out failure))
            {
                throw new InvalidOperationException(
                    "A prevalidated equipment switch failed after unequipping.");
            }

            notifications.Publish();
            failure = EquipmentChangeFailure.None;
            return true;
        }

        public void Commit(GameplayActionRecord action)
        {
            var notifications = new GameplayNotificationBatch();
            Commit(action, notifications);
            notifications.Publish();
        }

        private void Commit(
            GameplayActionRecord action,
            GameplayNotificationBatch notifications)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            if (action.Outcomes.Count != 1
                || !(action.Outcomes[0] is EquipmentChangedActionOutcome outcome))
            {
                throw new ArgumentException(
                    "Equipment actions require exactly one equipment outcome.",
                    nameof(action));
            }

            gameplay.CommitAction(action, notifications);
            records.Add(outcome.Change);
        }

        private bool TryPrepare(
            string actorId,
            string itemId,
            bool equip,
            out InventoryItemDefinition item,
            out GameplayActorSnapshot actor,
            out EquipmentChangeRecord change,
            out EquipmentChangeFailure failure)
        {
            item = null;
            actor = default;
            change = null;
            if (gameplay.Operation != GameplaySessionOperation.None)
            {
                failure = EquipmentChangeFailure.OperationInProgress;
                return false;
            }

            if (!gameplay.TryGetActor(actorId, out actor)
                || (gameplay.Mode == GameplaySessionMode.TurnBased
                    && !string.Equals(
                        gameplay.ActiveActorId,
                        actorId,
                        StringComparison.Ordinal)))
            {
                failure = EquipmentChangeFailure.ActorNotActive;
                return false;
            }

            item = gameplay.GetInventoryItem(actorId, itemId);
            if (item == null)
            {
                failure = EquipmentChangeFailure.ItemNotFound;
                return false;
            }

            if (!item.IsEquippable)
            {
                failure = EquipmentChangeFailure.ItemNotEquippable;
                return false;
            }

            bool isEquipped = string.Equals(
                actor.EquippedItemId,
                itemId,
                StringComparison.Ordinal);
            if (equip == isEquipped)
            {
                failure = EquipmentChangeFailure.AlreadyInRequestedState;
                return false;
            }

            if (equip && actor.EquippedItemId != null)
            {
                failure = EquipmentChangeFailure.MustUnequipCurrentItem;
                return false;
            }

            if (!equip && !isEquipped)
            {
                failure = EquipmentChangeFailure.AlreadyInRequestedState;
                return false;
            }

            ActionCost cost = GetActionCost(item);
            if (actor.TurnBudget.ActionPoints < cost.ActionPoints)
            {
                failure = EquipmentChangeFailure.InsufficientActionPoints;
                return false;
            }

            if (actor.TurnBudget.MovementOpportunity < cost.MovementOpportunity)
            {
                failure =
                    EquipmentChangeFailure.InsufficientMovementOpportunity;
                return false;
            }

            change = new EquipmentChangeRecord(
                actorId,
                itemId,
                equip ? EquipmentChangeKind.Equip : EquipmentChangeKind.Unequip,
                actor.EquippedItemId,
                equip ? itemId : null);
            failure = EquipmentChangeFailure.None;
            return true;
        }

        private ActionCost GetActionCost(InventoryItemDefinition item)
        {
            return gameplay.Mode == GameplaySessionMode.TurnBased
                ? item.EquipmentCost
                : new ActionCost(
                    0,
                    0f,
                    item.EquipmentCost.Mobility);
        }

        private static ActionCost Combine(
            ActionCost first,
            ActionCost second) =>
            new ActionCost(
                checked(first.ActionPoints + second.ActionPoints),
                first.MovementOpportunity + second.MovementOpportunity,
                first.Mobility == ActionMobility.Set
                    || second.Mobility == ActionMobility.Set
                        ? ActionMobility.Set
                        : ActionMobility.Mobile);

        private static InventoryEquipmentAvailability Availability(
            InventoryItemDefinition item,
            ActionCost cost,
            bool isSwitch,
            EquipmentChangeFailure failure) =>
            new InventoryEquipmentAvailability(
                item,
                cost,
                isSwitch,
                failure);
    }
}
