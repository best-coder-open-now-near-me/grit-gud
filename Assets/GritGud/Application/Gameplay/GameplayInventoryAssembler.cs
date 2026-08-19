using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;

namespace GritGud.Application.Gameplay
{
    internal static class GameplayInventoryAssembler
    {
        internal static IReadOnlyList<InventoryItemDefinition>
            CreateDefinitions(ScenarioActorContentData actor)
        {
            var definitions = new List<InventoryItemDefinition>();
            IReadOnlyList<ScenarioInventoryItemData> inventory = actor.inventory;
            if (inventory == null)
            {
                inventory = Array.Empty<ScenarioInventoryItemData>();
            }
            foreach (ScenarioInventoryItemData item in inventory)
            {
                ScenarioActionCostData cost = item.equipmentCost;
                EquipmentEffectSet effects = item.equippedEffects == null
                    ? EquipmentEffectSet.None
                    : new EquipmentEffectSet(
                        item.equippedEffects.movementSpeedMultiplier);
                definitions.Add(new InventoryItemDefinition(
                    item.id,
                    item.displayName,
                    item.hotbarSlot,
                    ParseInventoryItemKind(item.kind),
                    new ActionCost(
                        cost.actionPoints,
                        cost.movementOpportunity,
                        ParseMobility(cost.mobility)),
                    effects,
                    GameplayActorCombatAssembler.CreateAttackDefinition(
                        actor.id,
                        item.attackCapability),
                    CreateConsumablePowerDefinition(item),
                    ResolveOccupiedHands(item),
                    item.quantity));
            }

            return definitions.AsReadOnly();
        }

        internal static void Validate(ScenarioActorContentData actor)
        {
            IReadOnlyList<ScenarioInventoryItemData> inventory = actor.inventory;
            if (inventory == null)
            {
                inventory = Array.Empty<ScenarioInventoryItemData>();
            }
            Require(
                inventory.Count == 0
                    || actor.attackCapability == null
                    || !actor.attackCapability.enabled,
                $"Actor '{actor.id}' cannot author both legacy attack capability and inventory weapons.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var slots = new HashSet<int>();
            foreach (ScenarioInventoryItemData item in inventory)
            {
                Require(
                    item != null,
                    $"Actor '{actor.id}' inventory cannot contain null entries.");
                RequireText(item.id, $"Actor '{actor.id}' inventory item ID");
                RequireText(
                    item.displayName,
                    $"Actor '{actor.id}' inventory item '{item.id}' display name");
                Require(
                    ids.Add(item.id),
                    $"Actor '{actor.id}' inventory item '{item.id}' is duplicated.");
                Require(
                    item.hotbarSlot > 0
                        && item.hotbarSlot <= GameplayHotbarRules.SlotCount,
                    $"Actor '{actor.id}' inventory item '{item.id}' hotbar slot must be between 1 and {GameplayHotbarRules.SlotCount}.");
                Require(
                    slots.Add(item.hotbarSlot),
                    $"Actor '{actor.id}' inventory hotbar slot {item.hotbarSlot} is duplicated.");
                InventoryItemKind kind = ParseInventoryItemKind(item.kind);
                int occupiedHands = ResolveOccupiedHands(item);
                Require(
                    item.equipmentCost != null,
                    $"Actor '{actor.id}' inventory item '{item.id}' requires an equipment cost.");
                ParseMobility(item.equipmentCost.mobility);
                Require(
                    item.equippedEffects != null,
                    $"Actor '{actor.id}' inventory item '{item.id}' requires equipped effects.");
                RequireFinitePositive(
                    item.equippedEffects.movementSpeedMultiplier,
                    $"Actor '{actor.id}' inventory item '{item.id}' movement speed multiplier");
                if (kind == InventoryItemKind.Weapon)
                {
                    Require(
                        item.quantity == 0,
                        $"Actor '{actor.id}' weapon '{item.id}' cannot author a consumable quantity.");
                    Require(
                        occupiedHands >= 1 && occupiedHands <= 2,
                        $"Actor '{actor.id}' weapon '{item.id}' must occupy one or two hands.");
                    Require(
                        item.attackCapability != null
                            && item.attackCapability.enabled,
                        $"Actor '{actor.id}' weapon '{item.id}' requires an enabled attack capability.");
                    Require(
                        string.IsNullOrWhiteSpace(
                            item.consumablePower?.type),
                        $"Actor '{actor.id}' weapon '{item.id}' cannot author a consumable power.");
                    GameplayActorCombatAssembler.ValidateAttack(
                        actor.id,
                        item.attackCapability);
                }
                else
                {
                    Require(
                        item.quantity > 0,
                        $"Actor '{actor.id}' consumable '{item.id}' quantity must be greater than zero.");
                    Require(
                        occupiedHands == 0,
                        $"Actor '{actor.id}' consumable '{item.id}' cannot occupy equipped hands.");
                    Require(
                        item.attackCapability == null
                            || !item.attackCapability.enabled,
                        $"Actor '{actor.id}' consumable '{item.id}' cannot author a weapon attack.");
                    ValidateConsumablePower(actor.id, item);
                }
            }

            string initiallyEquipped = NormalizeOptionalId(
                actor.initiallyEquippedItemId);
            if (initiallyEquipped == null)
            {
                return;
            }

            ScenarioInventoryItemData equipped = null;
            foreach (ScenarioInventoryItemData item in inventory)
            {
                if (string.Equals(
                    item.id,
                    initiallyEquipped,
                    StringComparison.Ordinal))
                {
                    equipped = item;
                    break;
                }
            }

            Require(
                equipped != null
                    && ParseInventoryItemKind(equipped.kind)
                        == InventoryItemKind.Weapon,
                $"Actor '{actor.id}' initially equipped item '{initiallyEquipped}' must be an inventory weapon.");
        }

        private static ConsumablePowerDefinition CreateConsumablePowerDefinition(
            ScenarioInventoryItemData item)
        {
            ScenarioConsumablePowerData power = item?.consumablePower;
            string powerType = NormalizeOptionalId(power?.type);
            if (powerType == null) return null;
            switch (powerType)
            {
                case ThrownExplosiveDefinition.TypeId:
                    ScenarioThrownExplosiveData data = power.thrownExplosive;
                    return new ThrownExplosiveDefinition(
                        item.id,
                        new ActionCost(
                            data.turnCost.actionPoints,
                            data.turnCost.movementOpportunity,
                            ParseMobility(data.turnCost.mobility)),
                        data.maximumRange,
                        data.standingLaunchHeight,
                        data.crouchedLaunchHeight,
                        data.baseUncertaintyRadius,
                        data.uncertaintyPerMeter,
                        data.blastRadius,
                        data.blastWoundMovementPenalty,
                        data.blastIntegrityDamage,
                        CreateSmokeFieldDefinition(data.smokeField),
                        CreateFireFieldDefinition(data.fireField));
                default:
                    throw new InvalidOperationException(
                        $"Consumable '{item.id}' has unsupported power type '{powerType}'.");
            }
        }

        private static void ValidateConsumablePower(
            string actorId,
            ScenarioInventoryItemData item)
        {
            ScenarioConsumablePowerData power = item.consumablePower;
            string powerType = NormalizeOptionalId(power?.type);
            Require(
                powerType != null,
                $"Actor '{actorId}' consumable '{item.id}' requires an authored power.");
            switch (powerType)
            {
                case ThrownExplosiveDefinition.TypeId:
                    Require(
                        power.thrownExplosive != null,
                        $"Actor '{actorId}' consumable '{item.id}' requires thrown-explosive data.");
                    ValidateThrownExplosive(
                        actorId,
                        item.id,
                        power.thrownExplosive);
                    break;
                default:
                    throw new InvalidOperationException(
                        $"Actor '{actorId}' consumable '{item.id}' has unsupported power type '{powerType}'.");
            }
        }

        private static void ValidateThrownExplosive(
            string actorId,
            string itemId,
            ScenarioThrownExplosiveData data)
        {
            Require(data.turnCost != null,
                $"Actor '{actorId}' consumable '{itemId}' requires a turn cost.");
            ParseMobility(data.turnCost.mobility);
            RequireFinitePositive(data.maximumRange,
                $"Actor '{actorId}' consumable '{itemId}' maximum range");
            RequireFiniteNonNegative(data.standingLaunchHeight,
                $"Actor '{actorId}' consumable '{itemId}' standing launch height");
            RequireFiniteNonNegative(data.crouchedLaunchHeight,
                $"Actor '{actorId}' consumable '{itemId}' crouched launch height");
            RequireFiniteNonNegative(data.baseUncertaintyRadius,
                $"Actor '{actorId}' consumable '{itemId}' base uncertainty");
            RequireFiniteNonNegative(data.uncertaintyPerMeter,
                $"Actor '{actorId}' consumable '{itemId}' uncertainty per meter");
            RequireFiniteNonNegative(data.blastRadius,
                $"Actor '{actorId}' consumable '{itemId}' blast radius");
            RequireFiniteNonNegative(data.blastWoundMovementPenalty,
                $"Actor '{actorId}' consumable '{itemId}' blast wound movement penalty");
            RequireFiniteNonNegative(data.blastIntegrityDamage,
                $"Actor '{actorId}' consumable '{itemId}' blast integrity damage");
            bool hasBlast = data.blastRadius > 0f;
            bool hasBlastConsequence = data.blastWoundMovementPenalty > 0f
                || data.blastIntegrityDamage > 0f;
            bool hasSmoke = IsAuthoredSmokeField(data.smokeField);
            bool hasFire = IsAuthoredFireField(data.fireField);
            Require(
                hasBlast == hasBlastConsequence,
                $"Actor '{actorId}' consumable '{itemId}' blast radius and consequences must be authored together.");
            Require(
                (hasBlast ? 1 : 0) + (hasSmoke ? 1 : 0)
                    + (hasFire ? 1 : 0) == 1,
                $"Actor '{actorId}' consumable '{itemId}' requires exactly one blast, smoke, or fire payload.");
            if (hasSmoke)
            {
                ValidateSmokeField(actorId, itemId, data.smokeField);
            }
            if (hasFire)
            {
                ValidateFireField(actorId, itemId, data.fireField);
            }
        }

        private static SmokeFieldDefinition CreateSmokeFieldDefinition(
            ScenarioSmokeFieldData data) =>
            !IsAuthoredSmokeField(data)
                ? null
                : new SmokeFieldDefinition(
                    data.radius,
                    data.height,
                    data.explorationDurationSeconds,
                    data.durationTurnEnds,
                    data.minimumObscuredPath);

        private static bool IsAuthoredSmokeField(
            ScenarioSmokeFieldData data) =>
            data != null
            && (data.radius != 0f
                || data.height != 0f
                || data.explorationDurationSeconds != 0f
                || data.durationTurnEnds != 0
                || data.minimumObscuredPath != 0f);

        private static FireFieldDefinition CreateFireFieldDefinition(
            ScenarioFireFieldData data) =>
            !IsAuthoredFireField(data)
                ? null
                : new FireFieldDefinition(
                    data.initialRadius,
                    data.maximumRadius,
                    data.height,
                    data.explorationDurationSeconds,
                    data.durationTurnEnds,
                    data.explorationPulseSeconds,
                    data.actorWoundMovementPenalty,
                    data.destructibleIntegrityDamage,
                    data.minimumHazardPath);

        private static bool IsAuthoredFireField(ScenarioFireFieldData data) =>
            data != null
            && (data.initialRadius != 0f
                || data.maximumRadius != 0f
                || data.height != 0f
                || data.explorationDurationSeconds != 0f
                || data.durationTurnEnds != 0
                || data.explorationPulseSeconds != 0f
                || data.actorWoundMovementPenalty != 0f
                || data.destructibleIntegrityDamage != 0f
                || data.minimumHazardPath != 0f);

        private static void ValidateSmokeField(
            string actorId,
            string itemId,
            ScenarioSmokeFieldData data)
        {
            RequireFinitePositive(
                data.radius,
                $"Actor '{actorId}' consumable '{itemId}' smoke radius");
            RequireFinitePositive(
                data.height,
                $"Actor '{actorId}' consumable '{itemId}' smoke height");
            RequireFinitePositive(
                data.explorationDurationSeconds,
                $"Actor '{actorId}' consumable '{itemId}' smoke exploration duration");
            Require(
                data.durationTurnEnds > 0,
                $"Actor '{actorId}' consumable '{itemId}' smoke turn duration must be positive.");
            RequireFinitePositive(
                data.minimumObscuredPath,
                $"Actor '{actorId}' consumable '{itemId}' minimum obscured path");
            Require(
                data.minimumObscuredPath <= data.radius * 2f,
                $"Actor '{actorId}' consumable '{itemId}' minimum obscured path cannot exceed its diameter.");
        }

        private static void ValidateFireField(
            string actorId,
            string itemId,
            ScenarioFireFieldData data)
        {
            string prefix = $"Actor '{actorId}' consumable '{itemId}' fire";
            RequireFinitePositive(data.initialRadius, prefix + " initial radius");
            RequireFinitePositive(data.maximumRadius, prefix + " maximum radius");
            Require(
                data.maximumRadius >= data.initialRadius,
                prefix + " maximum radius cannot be smaller than its initial radius.");
            RequireFinitePositive(data.height, prefix + " height");
            RequireFinitePositive(
                data.explorationDurationSeconds,
                prefix + " exploration duration");
            Require(
                data.durationTurnEnds > 0,
                prefix + " turn duration must be positive.");
            RequireFinitePositive(
                data.explorationPulseSeconds,
                prefix + " exploration pulse interval");
            RequireFiniteNonNegative(
                data.actorWoundMovementPenalty,
                prefix + " actor wound movement penalty");
            RequireFiniteNonNegative(
                data.destructibleIntegrityDamage,
                prefix + " destructible integrity damage");
            Require(
                data.actorWoundMovementPenalty > 0f
                    || data.destructibleIntegrityDamage > 0f,
                prefix + " requires at least one consequence.");
            RequireFinitePositive(
                data.minimumHazardPath,
                prefix + " minimum hazard path");
            Require(
                data.minimumHazardPath <= data.maximumRadius * 2f,
                prefix + " minimum hazard path cannot exceed its maximum diameter.");
        }

        private static InventoryItemKind ParseInventoryItemKind(string value)
        {
            if (string.Equals(value, "weapon", StringComparison.OrdinalIgnoreCase))
            {
                return InventoryItemKind.Weapon;
            }

            if (string.Equals(
                value,
                "consumable",
                StringComparison.OrdinalIgnoreCase))
            {
                return InventoryItemKind.Consumable;
            }

            throw new InvalidOperationException(
                $"Inventory item kind '{value}' is unsupported.");
        }

        internal static string NormalizeOptionalId(string value) =>
            string.IsNullOrWhiteSpace(value) ? null : value;


        private static int ResolveOccupiedHands(ScenarioInventoryItemData item)
        {
            InventoryItemKind kind = ParseInventoryItemKind(item.kind);
            return item.occupiedHands < 0
                ? kind == InventoryItemKind.Weapon ? 2 : 0
                : item.occupiedHands;
        }


        private static ActionMobility ParseMobility(string value) =>
            GameplayScenarioAssemblyValidation.ParseMobility(value);

        private static void RequireText(string value, string label) =>
            GameplayScenarioAssemblyValidation.RequireText(value, label);

        private static void RequireFinitePositive(float value, string label) =>
            GameplayScenarioAssemblyValidation.RequireFinitePositive(
                value,
                label);

        private static void RequireFiniteNonNegative(
            float value,
            string label) =>
            GameplayScenarioAssemblyValidation.RequireFiniteNonNegative(
                value,
                label);

        private static void Require(bool condition, string message) =>
            GameplayScenarioAssemblyValidation.Require(condition, message);
    }
}
