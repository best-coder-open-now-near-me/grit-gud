using System;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class GameplayEquipmentController : MonoBehaviour,
        IGameplayWarningHintSource
    {
        private const int ConfirmationWarningPriority = 100;
        private GameplayEquipmentSession equipment;
        private GameplayInventoryAvailabilitySession availability;
        private string actorId;
        private Action<string> powerRequested;
        private Func<string, bool> powerRequestAllowed;
        private bool pendingEquip;
        private int pendingActivationSlot;

        public GameplaySession Session { get; private set; }

        public string PendingItemId { get; private set; }

        public bool HasPendingConfirmation => PendingItemId != null;

        public EquipmentChangeFailure LastFailure { get; private set; }

        public string StatusMessage { get; private set; } = string.Empty;

        public GameplayWarningHintModel CurrentWarningHint { get; private set; }

        internal GameplayEquipmentSession EquipmentSession => equipment;

        public void Bind(
            GameplaySession session,
            string authoritativeActorId,
            Action<string> onPowerRequested,
            Func<string, bool> canRequestPower = null)
        {
            Unbind();
            Session = session ?? throw new ArgumentNullException(nameof(session));
            if (string.IsNullOrWhiteSpace(authoritativeActorId))
            {
                throw new ArgumentException(
                    "Equipment-controller actor identifiers cannot be empty.",
                    nameof(authoritativeActorId));
            }

            powerRequested = onPowerRequested ??
                throw new ArgumentNullException(nameof(onPowerRequested));
            powerRequestAllowed = canRequestPower ?? (_ => true);
            equipment = new GameplayEquipmentSession(Session);
            availability = new GameplayInventoryAvailabilitySession(Session);
            LastFailure = EquipmentChangeFailure.None;
            StatusMessage = string.Empty;
            CurrentWarningHint = null;
            enabled = true;
            SetActor(authoritativeActorId);
        }

        public void SetActor(string authoritativeActorId)
        {
            if (Session == null || equipment == null || availability == null)
            {
                throw new InvalidOperationException(
                    "Bind gameplay equipment before changing actors.");
            }
            if (string.IsNullOrWhiteSpace(authoritativeActorId))
            {
                throw new ArgumentException(
                    "Equipment-controller actor identifiers cannot be empty.",
                    nameof(authoritativeActorId));
            }

            Session.GetActor(authoritativeActorId);
            CancelPending();
            actorId = authoritativeActorId;
            LastFailure = EquipmentChangeFailure.None;
            StatusMessage = string.Empty;
            CurrentWarningHint = null;
        }

        public void Unbind()
        {
            Session = null;
            equipment = null;
            availability = null;
            actorId = null;
            powerRequested = null;
            powerRequestAllowed = null;
            PendingItemId = null;
            pendingEquip = false;
            pendingActivationSlot = 0;
            LastFailure = EquipmentChangeFailure.None;
            StatusMessage = string.Empty;
            CurrentWarningHint = null;
            enabled = false;
        }

        public bool TryActivateItem(string itemId, int activationSlot = 0)
        {
            InventoryItemDefinition item = Session?.GetInventoryItem(actorId, itemId);
            if (item == null)
            {
                return false;
            }

            bool equipped = string.Equals(
                Session.GetActor(actorId).EquippedItemId,
                item.Id,
                StringComparison.Ordinal);
            if (item.Kind == InventoryItemKind.Weapon && !equipped)
            {
                return RequestEquipmentChange(
                    item,
                    equip: true,
                    activationSlot: activationSlot);
            }

            if (powerRequestAllowed == null
                || !powerRequestAllowed(item.Id))
            {
                return false;
            }

            InventoryPowerAvailability powerAvailability =
                availability.EvaluatePower(actorId, item.Id);
            if (!powerAvailability.IsAvailable)
            {
                StatusMessage = powerAvailability.Requirement;
                return false;
            }

            CancelPending(clearStatus: true);
            powerRequested(item.Id);
            return true;
        }

        public bool TryToggleEquipment(
            string itemId,
            int activationSlot = 0)
        {
            InventoryItemDefinition item = Session?.GetInventoryItem(
                actorId,
                itemId);
            if (item == null || !item.IsEquippable)
            {
                return Fail(EquipmentChangeFailure.ItemNotEquippable);
            }

            bool equipped = string.Equals(
                Session.GetActor(actorId).EquippedItemId,
                item.Id,
                StringComparison.Ordinal);
            return RequestEquipmentChange(
                item,
                equip: !equipped,
                activationSlot: activationSlot);
        }

        public bool CancelPending()
        {
            return CancelPending(clearStatus: false);
        }

        public void ClearStatus()
        {
            if (!HasPendingConfirmation)
            {
                StatusMessage = string.Empty;
                CurrentWarningHint = null;
            }
        }

        private bool RequestEquipmentChange(
            InventoryItemDefinition item,
            bool equip,
            int activationSlot)
        {
            InventoryEquipmentAvailability readiness =
                availability.EvaluateEquipment(
                actorId,
                item.Id);
            if (!readiness.IsAvailable)
            {
                return Fail(readiness.Failure);
            }

            if (!string.Equals(PendingItemId, item.Id, StringComparison.Ordinal)
                || pendingEquip != equip)
            {
                PendingItemId = item.Id;
                pendingEquip = equip;
                pendingActivationSlot = activationSlot > 0
                    ? activationSlot
                    : item.HotbarSlot;
                LastFailure = EquipmentChangeFailure.None;
                StatusMessage = BuildConfirmationMessage(
                    item,
                    equip,
                    readiness.ResolvedCost);
                CurrentWarningHint = BuildConfirmationWarning(
                    item,
                    equip,
                    pendingActivationSlot);
                return true;
            }

            bool resolved;
            if (equip
                && Session.GetActor(actorId).EquippedItemId != null)
            {
                resolved = equipment.TryResolveSwitch(
                    actorId,
                    item.Id,
                    out _,
                    out _,
                    out EquipmentChangeFailure failure);
                LastFailure = failure;
            }
            else
            {
                resolved = equipment.TryResolve(
                    actorId,
                    item.Id,
                    equip,
                    out _,
                    out EquipmentChangeFailure failure);
                LastFailure = failure;
            }

            if (!resolved)
            {
                StatusMessage = DescribeFailure(LastFailure);
                PendingItemId = null;
                pendingEquip = false;
                pendingActivationSlot = 0;
                CurrentWarningHint = null;
                return false;
            }

            PendingItemId = null;
            pendingEquip = false;
            pendingActivationSlot = 0;
            CurrentWarningHint = null;
            LastFailure = EquipmentChangeFailure.None;
            StatusMessage = equip
                ? item.DisplayName + " equipped."
                : item.DisplayName + " unequipped; hands empty.";
            return true;
        }

        private bool CancelPending(bool clearStatus)
        {
            if (!HasPendingConfirmation)
            {
                if (clearStatus)
                {
                    StatusMessage = string.Empty;
                    CurrentWarningHint = null;
                }

                return false;
            }

            PendingItemId = null;
            pendingEquip = false;
            pendingActivationSlot = 0;
            CurrentWarningHint = null;
            LastFailure = EquipmentChangeFailure.None;
            StatusMessage = clearStatus
                ? string.Empty
                : "Equipment change canceled.";
            return true;
        }

        private string BuildConfirmationMessage(
            InventoryItemDefinition item,
            bool equip,
            GritGud.Domain.Turns.ActionCost resolvedCost)
        {
            string operation = ResolveConfirmationOperation(equip);

            string cost = Session.Mode == GameplaySessionMode.TurnBased
                ? resolvedCost.ActionPoints + " AP"
                : "FREE OUT OF TURN MODE";
            return $"CONFIRM {operation} {item.DisplayName.ToUpperInvariant()} - "
                + $"{cost} - PRESS AGAIN; ESC CANCEL";
        }

        private GameplayWarningHintModel BuildConfirmationWarning(
            InventoryItemDefinition item,
            bool equip,
            int activationSlot)
        {
            string operation = ResolveConfirmationOperation(equip);
            string action = string.Equals(
                operation,
                "SWITCH",
                StringComparison.Ordinal)
                    ? "SWITCH TO "
                    : operation + " ";
            string text = "CONFIRM "
                + action
                + item.DisplayName.ToUpperInvariant()
                + " - CLICK THE ORANGE ARROW OR PRESS ["
                + activationSlot
                + "] AGAIN - ESC TO CANCEL";
            return new GameplayWarningHintModel(
                "equipment.confirmation",
                text,
                ConfirmationWarningPriority);
        }

        private string ResolveConfirmationOperation(bool equip)
        {
            if (!equip)
            {
                return "UNEQUIP";
            }

            return Session.GetActor(actorId).EquippedItemId == null
                ? "EQUIP"
                : "SWITCH";
        }

        private bool Fail(EquipmentChangeFailure failure)
        {
            LastFailure = failure;
            StatusMessage = DescribeFailure(failure);
            return false;
        }

        private static string DescribeFailure(EquipmentChangeFailure failure)
        {
            switch (failure)
            {
                case EquipmentChangeFailure.ActorNotActive:
                    return "Only the active actor can change equipment.";
                case EquipmentChangeFailure.OperationInProgress:
                    return "Wait for the current movement to resolve.";
                case EquipmentChangeFailure.ItemNotFound:
                    return "That inventory item is unavailable.";
                case EquipmentChangeFailure.ItemNotEquippable:
                    return "That item cannot be equipped.";
                case EquipmentChangeFailure.MustUnequipCurrentItem:
                    return "The current weapon must be unequipped first.";
                case EquipmentChangeFailure.AlreadyInRequestedState:
                    return "That equipment state is already active.";
                case EquipmentChangeFailure.InsufficientActionPoints:
                    return "Not enough AP remains for that equipment change.";
                case EquipmentChangeFailure.InsufficientMovementOpportunity:
                    return "Not enough movement remains for that equipment change.";
                case EquipmentChangeFailure.ActorPinned:
                    return "Push off the pinning prop before changing equipment.";
                case EquipmentChangeFailure.None:
                    return string.Empty;
                default:
                    throw new ArgumentOutOfRangeException(nameof(failure));
            }
        }

        private void OnDestroy()
        {
            Unbind();
        }
    }
}
