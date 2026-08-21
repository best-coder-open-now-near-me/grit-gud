using System;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;

namespace GritGud.Application.Gameplay
{
    public readonly struct GameplayActorSnapshot
    {
        public GameplayActorSnapshot(
            string actorId,
            GameplayActorPose pose,
            TurnBudget turnBudget)
            : this(
                actorId,
                pose,
                turnBudget,
                new ActorWoundSnapshot(actorId, 0, 0f))
        {
        }

        public GameplayActorSnapshot(
            string actorId,
            GameplayActorPose pose,
            TurnBudget turnBudget,
            ActorWoundSnapshot wounds)
            : this(
                actorId,
                pose,
                turnBudget,
                wounds,
                equippedItemId: null,
                EquipmentEffectSet.None)
        {
        }

        public GameplayActorSnapshot(
            string actorId,
            GameplayActorPose pose,
            TurnBudget turnBudget,
            ActorWoundSnapshot wounds,
            string equippedItemId,
            EquipmentEffectSet equipmentEffects,
            int maximumWounds = int.MaxValue,
            ActorInventorySnapshot inventory = null,
            TurnActionPointEconomy? actionPointEconomy = null,
            float turnMovementAllowance = -1f,
            ActorPinState pinState = null,
            int emergencyActionPointAllowance = 0,
            TurnBudget? suspendedTurnBudget = null,
            int attacksCommittedThisTurn = 0,
            ActorAmmunitionSnapshot ammunition = null)
        {
            if (!string.Equals(actorId, wounds.ActorId, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Actor snapshots and wound state must share an identifier.",
                    nameof(wounds));
            }
            if (maximumWounds <= 0)
                throw new ArgumentOutOfRangeException(nameof(maximumWounds));
            if (emergencyActionPointAllowance < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(emergencyActionPointAllowance));
            if (attacksCommittedThisTurn < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(attacksCommittedThisTurn));
            ActorInventorySnapshot resolvedInventory = inventory
                ?? new ActorInventorySnapshot(
                    actorId,
                    Array.Empty<InventoryQuantitySnapshot>());
            if (!string.Equals(
                    actorId,
                    resolvedInventory.ActorId,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Actor snapshots and inventory state must share an identifier.",
                    nameof(inventory));
            }
            ActorAmmunitionSnapshot resolvedAmmunition = ammunition
                ?? new ActorAmmunitionSnapshot(
                    actorId,
                    Array.Empty<WeaponMagazineSnapshot>(),
                    Array.Empty<AmmunitionReserveSnapshot>());
            if (!string.Equals(
                    actorId,
                    resolvedAmmunition.ActorId,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Actor snapshots and ammunition state must share an identifier.",
                    nameof(ammunition));
            }
            if (pinState != null
                && !string.Equals(
                    actorId,
                    pinState.ActorId,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Actor snapshots and pin state must share an identifier.",
                    nameof(pinState));
            }

            ActorId = actorId;
            Pose = pose;
            TurnBudget = turnBudget;
            Wounds = wounds;
            EquippedItemId = equippedItemId;
            EquipmentEffects = equipmentEffects;
            MaximumWounds = maximumWounds;
            Inventory = resolvedInventory;
            ActionPointEconomy = actionPointEconomy
                ?? new TurnActionPointEconomy(
                    turnBudget.ActionPoints,
                    turnBudget.ActionPoints,
                    Math.Max(1, turnBudget.ActionPoints));
            TurnMovementAllowance = turnMovementAllowance < 0f
                ? turnBudget.MovementOpportunity + wounds.MovementPenalty
                : turnMovementAllowance;
            PinState = pinState;
            EmergencyActionPointAllowance = emergencyActionPointAllowance;
            SuspendedTurnBudget = suspendedTurnBudget;
            AttacksCommittedThisTurn = attacksCommittedThisTurn;
            Ammunition = resolvedAmmunition;
            if (float.IsNaN(TurnMovementAllowance)
                || float.IsInfinity(TurnMovementAllowance)
                || ActionPointEconomy.MaximumHeldActionPoints
                    < turnBudget.ActionPoints
                || TurnMovementAllowance + 0.0001f
                    < turnBudget.MovementOpportunity + wounds.MovementPenalty)
                throw new ArgumentException(
                    "Actor allowances cannot be below the represented state.");
        }

        public string ActorId { get; }

        public GameplayActorPose Pose { get; }

        public TurnBudget TurnBudget { get; }

        public ActorWoundSnapshot Wounds { get; }

        public string EquippedItemId { get; }

        public EquipmentEffectSet EquipmentEffects { get; }

        public int MaximumWounds { get; }

        public ActorInventorySnapshot Inventory { get; }

        public TurnActionPointEconomy ActionPointEconomy { get; }

        public float TurnMovementAllowance { get; }

        public ActorPinState PinState { get; }

        public int EmergencyActionPointAllowance { get; }

        public TurnBudget? SuspendedTurnBudget { get; }

        public int AttacksCommittedThisTurn { get; }

        public ActorAmmunitionSnapshot Ammunition { get; }

        public bool IsPinned => PinState != null;

        public bool IsIncapacitated => Wounds.WoundCount >= MaximumWounds;
    }
}
