using System;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;

namespace GritGud.Application.Gameplay
{
    public enum InventoryPowerAvailabilityFailure
    {
        None,
        ActorUnavailable,
        ActorNotActive,
        ActorIncapacitated,
        ActorPinned,
        OperationInProgress,
        ItemUnavailable,
        PowerUnavailable,
        Depleted,
        RequiresEquippedItem,
        TurnModeUnavailable,
        InsufficientActionPoints,
        InsufficientMovementOpportunity,
    }

    public sealed class InventoryPowerAvailability
    {
        internal InventoryPowerAvailability(
            InventoryItemDefinition item,
            ActionCost resolvedCost,
            InventoryPowerAvailabilityFailure failure,
            bool conditionalTurnCost = false)
        {
            if (!Enum.IsDefined(
                    typeof(InventoryPowerAvailabilityFailure),
                    failure))
            {
                throw new ArgumentOutOfRangeException(nameof(failure));
            }

            Item = item;
            ResolvedCost = resolvedCost;
            Failure = failure;
            ConditionalTurnCost = conditionalTurnCost;
        }

        public InventoryItemDefinition Item { get; }

        public ActionCost ResolvedCost { get; }

        public InventoryPowerAvailabilityFailure Failure { get; }

        public bool ConditionalTurnCost { get; }

        public bool IsAvailable =>
            Failure == InventoryPowerAvailabilityFailure.None;

        public string Requirement =>
            GameplayInventoryAvailabilitySession.Format(Failure);
    }

    public sealed class InventoryEquipmentAvailability
    {
        internal InventoryEquipmentAvailability(
            InventoryItemDefinition item,
            ActionCost resolvedCost,
            bool isSwitch,
            EquipmentChangeFailure failure)
        {
            if (!Enum.IsDefined(typeof(EquipmentChangeFailure), failure))
            {
                throw new ArgumentOutOfRangeException(nameof(failure));
            }

            Item = item;
            ResolvedCost = resolvedCost;
            IsSwitch = isSwitch;
            Failure = failure;
        }

        public InventoryItemDefinition Item { get; }

        public ActionCost ResolvedCost { get; }

        public bool IsSwitch { get; }

        public EquipmentChangeFailure Failure { get; }

        public bool IsAvailable => Failure == EquipmentChangeFailure.None;

        public string Requirement =>
            GameplayInventoryAvailabilitySession.Format(Failure);
    }

    public sealed class GameplayInventoryAvailabilitySession
    {
        private readonly GameplaySession gameplay;
        private readonly GameplayEquipmentSession equipment;

        public GameplayInventoryAvailabilitySession(
            GameplaySession gameplaySession)
        {
            gameplay = gameplaySession ?? throw new ArgumentNullException(
                nameof(gameplaySession));
            equipment = new GameplayEquipmentSession(gameplay);
        }

        public InventoryPowerAvailability EvaluatePower(
            string actorId,
            string itemId)
        {
            if (!GameplayActorActionAuthority.TryAuthorize(
                    gameplay,
                    actorId,
                    GameplayActionTiming.Immediate,
                    startsEncounter: false,
                    blocksPinnedActor: true,
                    out GameplayActorSnapshot actor,
                    out GameplayActorActionFailure authorizationFailure))
            {
                return Power(
                    null,
                    default,
                    ToInventoryFailure(authorizationFailure));
            }

            InventoryItemDefinition item = gameplay.GetInventoryItem(
                actorId,
                itemId);
            if (item == null)
            {
                return Power(
                    null,
                    default,
                    InventoryPowerAvailabilityFailure.ItemUnavailable);
            }

            ActionCost authoredCost = item.Attack?.TurnCost
                ?? item.ConsumablePower?.TurnCost
                ?? default;
            if (item.Attack == null && item.ConsumablePower == null)
            {
                return Power(
                    item,
                    authoredCost,
                    InventoryPowerAvailabilityFailure.PowerUnavailable);
            }

            if (item.ConsumablePower != null
                && gameplay.GetInventoryQuantity(actorId, item.Id) <= 0)
            {
                return Power(
                    item,
                    authoredCost,
                    InventoryPowerAvailabilityFailure.Depleted);
            }

            bool turnBased = gameplay.Mode == GameplaySessionMode.TurnBased;
            if (item.Attack != null
                && !string.Equals(
                    actor.EquippedItemId,
                    item.Id,
                    StringComparison.Ordinal))
            {
                return Power(
                    item,
                    authoredCost,
                    InventoryPowerAvailabilityFailure.RequiresEquippedItem);
            }

            // Exploration may begin a combat action at any time.  Whether the
            // selected target starts an encounter is decided only when that
            // action is resolved; the voluntary-turn cooldown must never
            // disable a weapon or its hotkey beforehand.
            bool immediateExplorationAttack = !turnBased
                && item.Attack != null;
            bool conditionalExplorationCost = !turnBased
                && item.ConsumablePower != null;

            ActionCost resolvedCost = immediateExplorationAttack
                ? new ActionCost(0, 0f, authoredCost.Mobility)
                : authoredCost;
            if (conditionalExplorationCost)
            {
                return new InventoryPowerAvailability(
                    item,
                    resolvedCost,
                    InventoryPowerAvailabilityFailure.None,
                    conditionalTurnCost: true);
            }

            return CanAfford(actor.TurnBudget, resolvedCost, out var failure)
                ? Power(
                    item,
                    resolvedCost,
                    InventoryPowerAvailabilityFailure.None)
                : Power(item, resolvedCost, failure);
        }

        public InventoryEquipmentAvailability EvaluateEquipment(
            string actorId,
            string itemId) =>
            equipment.EvaluateAvailability(actorId, itemId);

        internal static string Format(
            InventoryPowerAvailabilityFailure failure)
        {
            switch (failure)
            {
                case InventoryPowerAvailabilityFailure.None:
                    return string.Empty;
                case InventoryPowerAvailabilityFailure.ActorUnavailable:
                    return "ACTOR UNAVAILABLE";
                case InventoryPowerAvailabilityFailure.ActorNotActive:
                    return "REQUIRES ACTIVE TURN";
                case InventoryPowerAvailabilityFailure.ActorIncapacitated:
                    return "ACTOR INCAPACITATED";
                case InventoryPowerAvailabilityFailure.ActorPinned:
                    return "PUSH OFF THE PINNING PROP FIRST";
                case InventoryPowerAvailabilityFailure.OperationInProgress:
                    return "WAIT FOR CURRENT ACTION";
                case InventoryPowerAvailabilityFailure.ItemUnavailable:
                    return "ITEM UNAVAILABLE";
                case InventoryPowerAvailabilityFailure.PowerUnavailable:
                    return "NO ITEM POWER";
                case InventoryPowerAvailabilityFailure.Depleted:
                    return "NO QUANTITY REMAINING";
                case InventoryPowerAvailabilityFailure.RequiresEquippedItem:
                    return "REQUIRES EQUIPPED ITEM";
                case InventoryPowerAvailabilityFailure.TurnModeUnavailable:
                    return "TURN MODE COOLDOWN ACTIVE";
                case InventoryPowerAvailabilityFailure.InsufficientActionPoints:
                    return "INSUFFICIENT AP";
                case InventoryPowerAvailabilityFailure.InsufficientMovementOpportunity:
                    return "INSUFFICIENT MOVEMENT";
                default:
                    throw new ArgumentOutOfRangeException(nameof(failure));
            }
        }

        internal static string Format(EquipmentChangeFailure failure)
        {
            switch (failure)
            {
                case EquipmentChangeFailure.None:
                    return string.Empty;
                case EquipmentChangeFailure.ActorNotActive:
                    return "REQUIRES ACTIVE TURN";
                case EquipmentChangeFailure.ActorIncapacitated:
                    return "ACTOR INCAPACITATED";
                case EquipmentChangeFailure.OperationInProgress:
                    return "WAIT FOR CURRENT ACTION";
                case EquipmentChangeFailure.ItemNotFound:
                    return "ITEM UNAVAILABLE";
                case EquipmentChangeFailure.ItemNotEquippable:
                    return "ITEM CANNOT BE EQUIPPED";
                case EquipmentChangeFailure.InsufficientActionPoints:
                    return "INSUFFICIENT AP";
                case EquipmentChangeFailure.InsufficientMovementOpportunity:
                    return "INSUFFICIENT MOVEMENT";
                case EquipmentChangeFailure.MustUnequipCurrentItem:
                    return "CURRENT ITEM MUST BE UNEQUIPPED";
                case EquipmentChangeFailure.AlreadyInRequestedState:
                    return "ITEM ALREADY IN REQUESTED STATE";
                case EquipmentChangeFailure.ActorPinned:
                    return "PUSH OFF THE PINNING PROP FIRST";
                default:
                    throw new ArgumentOutOfRangeException(nameof(failure));
            }
        }

        private static InventoryPowerAvailabilityFailure ToInventoryFailure(
            GameplayActorActionFailure failure)
        {
            switch (failure)
            {
                case GameplayActorActionFailure.ActorUnavailable:
                    return InventoryPowerAvailabilityFailure.ActorUnavailable;
                case GameplayActorActionFailure.ActorNotActive:
                    return InventoryPowerAvailabilityFailure.ActorNotActive;
                case GameplayActorActionFailure.ActorIncapacitated:
                    return InventoryPowerAvailabilityFailure.ActorIncapacitated;
                case GameplayActorActionFailure.ActorPinned:
                    return InventoryPowerAvailabilityFailure.ActorPinned;
                case GameplayActorActionFailure.OperationInProgress:
                    return InventoryPowerAvailabilityFailure.OperationInProgress;
                case GameplayActorActionFailure.TurnModeRequired:
                    return InventoryPowerAvailabilityFailure.TurnModeUnavailable;
                default:
                    throw new ArgumentOutOfRangeException(nameof(failure));
            }
        }

        private static bool CanAfford(
            TurnBudget budget,
            ActionCost cost,
            out InventoryPowerAvailabilityFailure failure)
        {
            if (budget.ActionPoints < cost.ActionPoints)
            {
                failure = InventoryPowerAvailabilityFailure
                    .InsufficientActionPoints;
                return false;
            }

            if (budget.MovementOpportunity < cost.MovementOpportunity)
            {
                failure = InventoryPowerAvailabilityFailure
                    .InsufficientMovementOpportunity;
                return false;
            }

            failure = InventoryPowerAvailabilityFailure.None;
            return true;
        }

        private static InventoryPowerAvailability Power(
            InventoryItemDefinition item,
            ActionCost cost,
            InventoryPowerAvailabilityFailure failure) =>
            new InventoryPowerAvailability(item, cost, failure);

    }
}
