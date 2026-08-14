using System;
using GritGud.Domain.Turns;

namespace GritGud.Domain.Gameplay
{
    public enum InventoryItemKind
    {
        Weapon,
        Consumable,
    }

    public readonly struct EquipmentEffectSet
    {
        public static EquipmentEffectSet None => new EquipmentEffectSet(1f);

        public EquipmentEffectSet(float movementSpeedMultiplier)
        {
            if (float.IsNaN(movementSpeedMultiplier)
                || float.IsInfinity(movementSpeedMultiplier)
                || movementSpeedMultiplier <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(movementSpeedMultiplier));
            }

            MovementSpeedMultiplier = movementSpeedMultiplier;
        }

        public float MovementSpeedMultiplier { get; }
    }

    public abstract class ConsumablePowerDefinition
    {
        protected ConsumablePowerDefinition(string id, ActionCost turnCost)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException(
                    "Consumable powers require an identifier.",
                    nameof(id));
            }

            Id = id;
            TurnCost = turnCost;
        }

        public string Id { get; }

        public ActionCost TurnCost { get; }

        public abstract string PowerTypeId { get; }
    }

    public sealed class InventoryItemDefinition
    {
        public InventoryItemDefinition(
            string id,
            string displayName,
            int hotbarSlot,
            InventoryItemKind kind,
            ActionCost equipmentCost,
            EquipmentEffectSet equippedEffects,
            AttackDefinition attack = null,
            ConsumablePowerDefinition consumablePower = null,
            int occupiedHands = -1,
            int initialQuantity = 0)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException(
                    "Inventory item identifiers cannot be empty.",
                    nameof(id));
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                throw new ArgumentException(
                    "Inventory item display names cannot be empty.",
                    nameof(displayName));
            }

            if (hotbarSlot <= 0
                || hotbarSlot > GameplayHotbarRules.SlotCount)
            {
                throw new ArgumentOutOfRangeException(nameof(hotbarSlot));
            }

            if (!Enum.IsDefined(typeof(InventoryItemKind), kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind));
            }

            if (kind == InventoryItemKind.Weapon && attack == null)
            {
                throw new ArgumentException(
                    "Weapon inventory items require an attack definition.",
                    nameof(attack));
            }
            if (kind == InventoryItemKind.Weapon && consumablePower != null)
            {
                throw new ArgumentException(
                    "Weapon inventory items cannot author consumable powers.",
                    nameof(consumablePower));
            }
            if (kind == InventoryItemKind.Consumable && consumablePower == null)
            {
                throw new ArgumentException(
                    "Consumable inventory items require an authored power.",
                    nameof(consumablePower));
            }
            if (kind == InventoryItemKind.Consumable && attack != null)
            {
                throw new ArgumentException(
                    "Consumable inventory items cannot author weapon attacks.",
                    nameof(attack));
            }
            if (consumablePower != null
                && !string.Equals(
                    id,
                    consumablePower.Id,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Consumable power identifiers must match their inventory item.",
                    nameof(consumablePower));
            }

            int resolvedOccupiedHands = occupiedHands < 0
                ? kind == InventoryItemKind.Weapon ? 2 : 0
                : occupiedHands;
            if (resolvedOccupiedHands < 0 || resolvedOccupiedHands > 2)
            {
                throw new ArgumentOutOfRangeException(nameof(occupiedHands));
            }
            if (kind == InventoryItemKind.Weapon
                && resolvedOccupiedHands == 0)
            {
                throw new ArgumentException(
                    "Equippable weapons must occupy at least one hand.",
                    nameof(occupiedHands));
            }
            if (kind == InventoryItemKind.Consumable
                && resolvedOccupiedHands != 0)
            {
                throw new ArgumentException(
                    "Consumables do not occupy equipped hands.",
                    nameof(occupiedHands));
            }
            if (kind == InventoryItemKind.Consumable && initialQuantity <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(initialQuantity),
                    "Consumable inventory items require a positive starting quantity.");
            }
            if (kind != InventoryItemKind.Consumable && initialQuantity != 0)
            {
                throw new ArgumentException(
                    "Only consumable inventory items can author a finite quantity.",
                    nameof(initialQuantity));
            }

            Id = id;
            DisplayName = displayName;
            HotbarSlot = hotbarSlot;
            Kind = kind;
            EquipmentCost = equipmentCost;
            EquippedEffects = equippedEffects;
            Attack = attack;
            ConsumablePower = consumablePower;
            OccupiedHands = resolvedOccupiedHands;
            InitialQuantity = initialQuantity;
        }

        public string Id { get; }

        public string DisplayName { get; }

        public int HotbarSlot { get; }

        public InventoryItemKind Kind { get; }

        public ActionCost EquipmentCost { get; }

        public EquipmentEffectSet EquippedEffects { get; }

        public AttackDefinition Attack { get; }

        public ConsumablePowerDefinition ConsumablePower { get; }

        public int OccupiedHands { get; }

        public int InitialQuantity { get; }

        public bool HasPower => Attack != null || ConsumablePower != null;

        public bool IsEquippable => Kind == InventoryItemKind.Weapon;
    }

    public enum EquipmentChangeKind
    {
        Equip,
        Unequip,
    }

    public sealed class EquipmentChangeRecord
    {
        public EquipmentChangeRecord(
            string actorId,
            string itemId,
            EquipmentChangeKind kind,
            string previousEquippedItemId,
            string resultingEquippedItemId)
        {
            if (string.IsNullOrWhiteSpace(actorId))
            {
                throw new ArgumentException(
                    "Equipment changes require an actor identifier.",
                    nameof(actorId));
            }

            if (string.IsNullOrWhiteSpace(itemId))
            {
                throw new ArgumentException(
                    "Equipment changes require an item identifier.",
                    nameof(itemId));
            }

            if (!Enum.IsDefined(typeof(EquipmentChangeKind), kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind));
            }

            if (kind == EquipmentChangeKind.Equip
                && (previousEquippedItemId != null
                    || !string.Equals(
                        itemId,
                        resultingEquippedItemId,
                        StringComparison.Ordinal)))
            {
                throw new ArgumentException(
                    "Equip changes must begin empty-handed and equip the requested item.",
                    nameof(resultingEquippedItemId));
            }

            if (kind == EquipmentChangeKind.Unequip
                && (!string.Equals(
                        itemId,
                        previousEquippedItemId,
                        StringComparison.Ordinal)
                    || resultingEquippedItemId != null))
            {
                throw new ArgumentException(
                    "Unequip changes must remove the requested equipped item.",
                    nameof(resultingEquippedItemId));
            }

            ActorId = actorId;
            ItemId = itemId;
            Kind = kind;
            PreviousEquippedItemId = previousEquippedItemId;
            ResultingEquippedItemId = resultingEquippedItemId;
        }

        public string ActorId { get; }

        public string ItemId { get; }

        public EquipmentChangeKind Kind { get; }

        public string PreviousEquippedItemId { get; }

        public string ResultingEquippedItemId { get; }
    }

    public static class EquipmentActionIds
    {
        public const string Equip = "equipment.equip";
        public const string Unequip = "equipment.unequip";
    }
}
