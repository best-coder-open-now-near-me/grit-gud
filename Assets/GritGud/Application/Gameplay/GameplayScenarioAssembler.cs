using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Levels;
using GritGud.Domain.Turns;

namespace GritGud.Application.Gameplay
{
    public sealed class GameplayScenarioAssembler
    {
        public GameplayScenarioAssembly Assemble(
            ScenarioContentDocument content,
            LevelDocument level)
        {
            if (content == null)
            {
                throw new ArgumentNullException(nameof(content));
            }

            if (level == null)
            {
                throw new ArgumentNullException(nameof(level));
            }

            content.Normalize();
            Require(
                content.schemaVersion == ScenarioContentDocument.CurrentSchemaVersion,
                $"Scenario schema {content.schemaVersion} is unsupported; expected "
                + $"{ScenarioContentDocument.CurrentSchemaVersion}.");
            RequireText(content.scenarioId, "Scenario ID");
            RequireText(content.displayName, "Scenario display name");
            RequireText(content.levelId, "Scenario level ID");
            RequireFinitePositive(
                content.timing.minimumVoluntaryTurnSeconds,
                "Minimum voluntary turn duration");
            Require(
                string.Equals(content.levelId, level.levelId, StringComparison.Ordinal),
                $"Scenario '{content.scenarioId}' requires level '{content.levelId}', "
                + $"not '{level.levelId}'.");

            Dictionary<string, ScenarioActorContentData> actorIndex =
                IndexActors(content.actors);
            PlayerPartyDefinition playerParty = CreatePlayerParty(
                content.playerParty,
                actorIndex);
            if (!string.IsNullOrWhiteSpace(content.primaryTargetActorId))
            {
                Require(
                    actorIndex.ContainsKey(content.primaryTargetActorId),
                    $"Primary target actor '{content.primaryTargetActorId}' is not defined.");
                Require(
                    !playerParty.Contains(content.primaryTargetActorId),
                    "A player party actor cannot also be the primary target.");
            }

            var actorDefinitions = new List<ScenarioActorDefinition>(
                actorIndex.Count);
            var actorRuntimeDefinitions =
                new Dictionary<string, ScenarioActorRuntimeDefinition>(
                    actorIndex.Count,
                    StringComparer.Ordinal);
            foreach (ScenarioActorContentData actor in content.actors)
            {
                ScenarioActorDefinition gameplayDefinition =
                    CreateActorDefinition(actor);
                actorDefinitions.Add(gameplayDefinition);
                actorRuntimeDefinitions.Add(
                    actor.id,
                    new ScenarioActorRuntimeDefinition(
                        actor.displayName,
                        actor.presentationId,
                        actor.characterId,
                        actor.targetable,
                        actor.mass,
                        gameplayDefinition,
                        CreateControlProfile(actor)));
            }

            var objectiveDefinitions = new List<ScenarioObjectiveDefinition>();
            var objectiveRuntimeDefinitions =
                new Dictionary<string, ScenarioObjectiveRuntimeDefinition>(
                    StringComparer.Ordinal);
            foreach (ScenarioObjectiveContentData objective in content.objectives)
            {
                Require(objective != null, "Scenario objectives cannot contain null entries.");
                RequireText(objective.id, "Objective ID");
                Require(
                    objectiveRuntimeDefinitions.TryAdd(
                        objective.id,
                        new ScenarioObjectiveRuntimeDefinition(
                            objective.id,
                            objective.activeHudText,
                            objective.completedHudText)),
                    $"Objective '{objective.id}' is defined more than once.");
                objectiveDefinitions.Add(CreateObjectiveDefinition(level, objective));
            }

            if (!string.IsNullOrWhiteSpace(content.primaryObjectiveId))
            {
                Require(
                    objectiveRuntimeDefinitions.ContainsKey(
                        content.primaryObjectiveId),
                    $"Primary objective '{content.primaryObjectiveId}' is not defined.");
            }

            Dictionary<string, ScenarioPropContentData> propIndex =
                IndexProps(content.props, level);
            Dictionary<string, ScenarioVehicleRuntimeDefinition> vehicleIndex =
                IndexVehicles(content.vehicles, level, actorIndex);
            var scenario = new ScenarioDefinition(
                content.scenarioId,
                new ScenarioTimingDefinition(
                    content.timing.minimumVoluntaryTurnSeconds),
                actorDefinitions,
                objectiveDefinitions,
                CreateAttackResponses(
                    content.actors,
                    content.props,
                    content.vehicles),
                playerParty);
            return new GameplayScenarioAssembly(
                content.displayName,
                content.primaryTargetActorId,
                content.primaryObjectiveId,
                content.randomSeed,
                scenario,
                actorRuntimeDefinitions,
                objectiveRuntimeDefinitions,
                vehicleIndex,
                CreateDisplacementSubjects(actorIndex, propIndex));
        }

        private static Dictionary<string, DisplacementSubjectDefinition>
            CreateDisplacementSubjects(
                IReadOnlyDictionary<string, ScenarioActorContentData> actors,
                IReadOnlyDictionary<string, ScenarioPropContentData> props)
        {
            var subjects =
                new Dictionary<string, DisplacementSubjectDefinition>(
                    StringComparer.Ordinal);
            foreach (ScenarioActorContentData actor in actors.Values)
            {
                subjects.Add(
                    actor.id,
                    new DisplacementSubjectDefinition(
                        actor.id,
                        DisplacementSubjectKind.Combatant,
                        actor.mass,
                        ParseDisplacementSize(actor.sizeClass)));
            }

            foreach (ScenarioPropContentData prop in props.Values)
            {
                Require(
                    !subjects.ContainsKey(prop.entityId),
                    $"Displacement subject '{prop.entityId}' is defined as both an actor and a prop.");
                subjects.Add(
                    prop.entityId,
                    new DisplacementSubjectDefinition(
                        prop.entityId,
                        DisplacementSubjectKind.Prop,
                        prop.mass,
                        ParseDisplacementSize(prop.sizeClass),
                        CreatePropToppling(prop.toppling),
                        CreatePropPinning(prop.pinning)));
            }

            return subjects;
        }

        private static PlayerPartyDefinition CreatePlayerParty(
            ScenarioPlayerPartyData data,
            IReadOnlyDictionary<string, ScenarioActorContentData> actors)
        {
            Require(data != null, "Scenario requires a player party.");
            Require(
                data.actorIds != null && data.actorIds.Count > 0,
                "Player party requires at least one controlled actor.");
            RequireText(
                data.initiallySelectedActorId,
                "Player party initially selected actor ID");

            var ids = new HashSet<string>(StringComparer.Ordinal);
            var characterIdentities = new HashSet<string>(StringComparer.Ordinal);
            foreach (string actorId in data.actorIds)
            {
                RequireText(actorId, "Player party actor ID");
                Require(
                    ids.Add(actorId),
                    $"Player party actor '{actorId}' is listed more than once.");
                Require(
                    actors.TryGetValue(actorId, out ScenarioActorContentData actor),
                    $"Player party actor '{actorId}' is not defined.");
                Require(
                    !GameplayActorCombatAssembler.HasAuthoredEnemyBehavior(
                        actor.combat?.enemyBehavior),
                    $"Player party actor '{actorId}' cannot own enemy behavior.");
                string identityId = actor.characterProfile?.identityId;
                RequireText(
                    identityId,
                    $"Player party actor '{actorId}' character identity ID");
                Require(
                    characterIdentities.Add(identityId),
                    $"Player party character identity '{identityId}' is used more than once.");
            }

            Require(
                ids.Contains(data.initiallySelectedActorId),
                $"Initially selected actor '{data.initiallySelectedActorId}' is not in the player party.");
            return new PlayerPartyDefinition(
                data.actorIds,
                data.initiallySelectedActorId);
        }

        private static Dictionary<string, ScenarioActorContentData> IndexActors(
            IReadOnlyList<ScenarioActorContentData> actors)
        {
            Require(actors.Count > 0, "A scenario requires at least one actor.");
            var index = new Dictionary<string, ScenarioActorContentData>(
                StringComparer.Ordinal);
            foreach (ScenarioActorContentData actor in actors)
            {
                Require(actor != null, "Scenario actors cannot contain null entries.");
                RequireText(actor.id, "Actor ID");
                RequireText(actor.displayName, $"Actor '{actor.id}' display name");
                RequireText(actor.presentationId, $"Actor '{actor.id}' presentation ID");
                Require(
                    index.TryAdd(actor.id, actor),
                    $"Actor '{actor.id}' is defined more than once.");
                RequireFinitePositive(actor.mass, $"Actor '{actor.id}' mass");
                ParseDisplacementSize(actor.sizeClass);
                ValidateCharacterProfile(actor);
                ValidateControl(actor);
                ValidateDisplacementActions(actor);

                GameplayActorCombatAssembler.ValidateAttack(
                    actor.id,
                    actor.attackCapability);
                ValidateInventory(actor);
                GameplayActorCombatAssembler.ValidateCombat(actor);
            }

            return index;
        }

        private static ScenarioActorDefinition CreateActorDefinition(
            ScenarioActorContentData actor)
        {
            ScenarioTurnBudgetData budget = actor.turnBudget ??
                throw new InvalidOperationException(
                    $"Actor '{actor.id}' does not define a turn budget.");
            CharacterProfileDefinition characterProfile =
                CreateCharacterProfile(actor.characterProfile);
            CharacterDerivedStatistics derived =
                characterProfile.DerivedStatistics;
            var pose = new GameplayActorPose(
                ToPosition(actor.position),
                actor.facingDegrees,
                ParseStance(actor.stance));
            var startingBudget = new TurnBudget(
                budget.actionPoints,
                derived.MovementOpportunity);
            IReadOnlyList<InventoryItemDefinition> inventory =
                CreateInventoryDefinitions(actor);
            return inventory.Count == 0
                ? new ScenarioActorDefinition(
                    actor.id,
                    derived.Initiative,
                    pose,
                    startingBudget,
                    GameplayActorCombatAssembler.CreateAttackDefinition(
                        actor.id,
                        actor.attackCapability),
                    CreateDisplacementAbility(actor),
                    GameplayActorCombatAssembler.CreateCombatDefinition(
                        actor.combat),
                    characterProfile)
                : new ScenarioActorDefinition(
                    actor.id,
                    derived.Initiative,
                    pose,
                    startingBudget,
                    inventory,
                    NormalizeOptionalId(actor.initiallyEquippedItemId),
                    characterProfile,
                    CreateDisplacementAbility(actor),
                    GameplayActorCombatAssembler.CreateCombatDefinition(
                        actor.combat));
        }

        private static CharacterProfileDefinition CreateCharacterProfile(
            ScenarioCharacterProfileData data)
        {
            if (!HasAuthoredCharacterProfile(data))
            {
                throw new InvalidOperationException(
                    "Scenario actors require an authored character profile.");
            }
            var attributes = new List<CharacterRating>();
            foreach (ScenarioCharacterRatingData value in
                data.attributes ?? new List<ScenarioCharacterRatingData>())
                attributes.Add(new CharacterRating(value.id, value.rating));
            var skills = new List<CharacterRating>();
            foreach (ScenarioCharacterRatingData value in
                data.skills ?? new List<ScenarioCharacterRatingData>())
                skills.Add(new CharacterRating(value.id, value.rating));
            return new CharacterProfileDefinition(
                data.identityId, data.displayName, data.archetype,
                attributes, skills, data.talentIds ?? new List<string>());
        }

        private static void ValidateCharacterProfile(ScenarioActorContentData actor)
        {
            ScenarioCharacterProfileData data = actor.characterProfile;
            Require(
                HasAuthoredCharacterProfile(data),
                $"Actor '{actor.id}' requires a character profile.");
            RequireText(data.identityId, $"Actor '{actor.id}' character identity");
            RequireText(data.displayName, $"Actor '{actor.id}' character display name");
            RequireText(data.archetype, $"Actor '{actor.id}' archetype");
            try
            {
                _ = CreateCharacterProfile(data);
            }
            catch (ArgumentException exception)
            {
                throw new InvalidOperationException(
                    $"Actor '{actor.id}' character profile is invalid: "
                    + exception.Message,
                    exception);
            }
        }

        private static bool HasAuthoredCharacterProfile(
            ScenarioCharacterProfileData data) =>
            data != null
            && (!string.IsNullOrWhiteSpace(data.identityId)
                || !string.IsNullOrWhiteSpace(data.displayName)
                || !string.IsNullOrWhiteSpace(data.archetype)
                || (data.attributes != null && data.attributes.Count > 0)
                || (data.skills != null && data.skills.Count > 0)
                || (data.talentIds != null && data.talentIds.Count > 0));

        private static IReadOnlyList<InventoryItemDefinition>
            CreateInventoryDefinitions(ScenarioActorContentData actor)
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

        private static void ValidateInventory(ScenarioActorContentData actor)
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
                        CreateSmokeFieldDefinition(data.smokeField));
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
            Require(
                hasBlast == hasBlastConsequence,
                $"Actor '{actorId}' consumable '{itemId}' blast radius and consequences must be authored together.");
            Require(
                hasBlast != hasSmoke,
                $"Actor '{actorId}' consumable '{itemId}' requires exactly one blast or smoke payload.");
            if (hasSmoke)
            {
                ValidateSmokeField(actorId, itemId, data.smokeField);
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

        private static string NormalizeOptionalId(string value) =>
            string.IsNullOrWhiteSpace(value) ? null : value;

        private static ScenarioObjectiveDefinition CreateObjectiveDefinition(
            LevelDocument level,
            ScenarioObjectiveContentData objective)
        {
            RequireText(
                objective.levelInteractionPointId,
                $"Objective '{objective.id}' interaction-point ID");
            RequireText(
                objective.levelInteractionPointType,
                $"Objective '{objective.id}' interaction-point type");
            RequireText(objective.actionId, $"Objective '{objective.id}' action ID");
            RequireText(
                objective.displayName,
                $"Objective '{objective.id}' display name");
            RequireText(
                objective.activeHudText,
                $"Objective '{objective.id}' active HUD text");
            RequireText(
                objective.completedHudText,
                $"Objective '{objective.id}' completed HUD text");

            LevelEntity matchedEntity = null;
            InteractionPointData matchedPoint = null;
            foreach (LevelEntity entity in level.entities)
            {
                foreach (InteractionPointData point in entity.interactionPoints)
                {
                    if (!string.Equals(
                            point.id,
                            objective.levelInteractionPointId,
                            StringComparison.Ordinal)
                        || !string.Equals(
                            point.type,
                            objective.levelInteractionPointType,
                            StringComparison.Ordinal))
                    {
                        continue;
                    }

                    Require(
                        matchedPoint == null,
                        $"Level '{level.levelId}' defines interaction point "
                        + $"'{point.id}' of type '{point.type}' more than once.");
                    matchedEntity = entity;
                    matchedPoint = point;
                }
            }

            Require(
                matchedPoint != null,
                $"Level '{level.levelId}' does not define interaction point "
                + $"'{objective.levelInteractionPointId}' of type "
                + $"'{objective.levelInteractionPointType}'.");
            GameplayPosition worldPosition = TransformPoint(
                matchedEntity.transform,
                matchedPoint.localPosition);
            ScenarioActionCostData cost = objective.turnCost ??
                throw new InvalidOperationException(
                    $"Objective '{objective.id}' does not define an action cost.");
            return new ScenarioObjectiveDefinition(
                objective.id,
                worldPosition,
                matchedPoint.radius,
                new GameplayInteractionDefinition(
                    objective.actionId,
                    objective.displayName,
                    new ActionCost(
                        cost.actionPoints,
                        cost.movementOpportunity,
                        ParseMobility(cost.mobility))));
        }

        private static Dictionary<string, ScenarioPropContentData> IndexProps(
            IReadOnlyList<ScenarioPropContentData> props,
            LevelDocument level)
        {
            var levelEntityIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (LevelEntity entity in level.entities)
            {
                levelEntityIds.Add(entity.id);
            }

            var index = new Dictionary<string, ScenarioPropContentData>(
                StringComparer.Ordinal);
            foreach (ScenarioPropContentData prop in props)
            {
                Require(prop != null, "Scenario props cannot contain null entries.");
                RequireText(prop.entityId, "Scenario prop entity ID");
                Require(
                    levelEntityIds.Contains(prop.entityId),
                    $"Prop '{prop.entityId}' is missing from level '{level.levelId}'.");
                RequireFinitePositive(prop.mass, $"Prop '{prop.entityId}' mass");
                ParseDisplacementSize(prop.sizeClass);
                ValidatePropToppling(prop.entityId, prop.toppling);
                ValidatePropPinning(
                    prop.entityId,
                    prop.toppling,
                    prop.pinning);
                Require(
                    index.TryAdd(prop.entityId, prop),
                    $"Prop '{prop.entityId}' is defined more than once.");
            }

            return index;
        }

        private static void ValidatePropToppling(
            string propId,
            ScenarioPropTopplingData toppling)
        {
            if (toppling == null)
                return;

            Require(
                !float.IsNaN(toppling.pitchOffsetDegrees)
                    && !float.IsInfinity(toppling.pitchOffsetDegrees),
                $"Prop '{propId}' toppling pitch offset must be finite.");
            Require(
                !float.IsNaN(toppling.rollOffsetDegrees)
                    && !float.IsInfinity(toppling.rollOffsetDegrees),
                $"Prop '{propId}' toppling roll offset must be finite.");
            RequireFiniteNonNegative(
                toppling.elevationOffset,
                $"Prop '{propId}' toppling elevation offset");
            if (toppling.enabled)
            {
                Require(
                    toppling.pitchOffsetDegrees != 0f
                        || toppling.rollOffsetDegrees != 0f,
                    $"Prop '{propId}' enabled toppling requires a non-zero pitch or roll offset.");
            }
        }

        private static PropTopplingDefinition CreatePropToppling(
            ScenarioPropTopplingData toppling) =>
            toppling != null && toppling.enabled
                ? new PropTopplingDefinition(
                    toppling.pitchOffsetDegrees,
                    toppling.rollOffsetDegrees,
                    toppling.elevationOffset)
                : null;

        private static void ValidatePropPinning(
            string propId,
            ScenarioPropTopplingData toppling,
            ScenarioPropPinningData pinning)
        {
            if (pinning == null)
                return;

            RequireFiniteNonNegative(
                pinning.maximumActorMass,
                $"Prop '{propId}' maximum pinned actor mass");
            RequireFiniteNonNegative(
                pinning.minimumContactDepth,
                $"Prop '{propId}' minimum pin contact depth");
            if (pinning.enabled)
            {
                Require(
                    toppling != null && toppling.enabled,
                    $"Prop '{propId}' pinning requires enabled toppling.");
                RequireFinitePositive(
                    pinning.maximumActorMass,
                    $"Prop '{propId}' maximum pinned actor mass");
            }
        }

        private static PropPinningDefinition CreatePropPinning(
            ScenarioPropPinningData pinning) =>
            pinning != null && pinning.enabled
                ? new PropPinningDefinition(
                    pinning.maximumActorMass,
                    pinning.minimumContactDepth)
                : null;

        private static IReadOnlyList<AttackResponseDefinition>
            CreateAttackResponses(
                IReadOnlyList<ScenarioActorContentData> actors,
                IReadOnlyList<ScenarioPropContentData> props,
                IReadOnlyList<ScenarioVehicleContentData> vehicles)
        {
            var responses = new List<AttackResponseDefinition>();
            foreach (ScenarioActorContentData actor in actors)
            {
                AddAttackResponse(responses, actor.id, actor.attackResponse);
            }

            foreach (ScenarioPropContentData prop in props)
            {
                AddAttackResponse(
                    responses,
                    prop.entityId,
                    prop.attackResponse);
            }

            foreach (ScenarioVehicleContentData vehicle in vehicles)
            {
                AddAttackResponse(
                    responses,
                    vehicle.entityId,
                    vehicle.attackResponse);
            }

            return responses;
        }

        private static void AddAttackResponse(
            ICollection<AttackResponseDefinition> responses,
            string targetId,
            ScenarioAttackResponseData response)
        {
            if (response != null)
            {
                responses.Add(new AttackResponseDefinition(
                    targetId,
                    response.startsEncounter));
            }
        }

        private static Dictionary<string, ScenarioVehicleRuntimeDefinition>
            IndexVehicles(
            IReadOnlyList<ScenarioVehicleContentData> vehicles,
            LevelDocument level,
            IReadOnlyDictionary<string, ScenarioActorContentData> actors)
        {
            var levelEntityIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (LevelEntity entity in level.entities)
            {
                levelEntityIds.Add(entity.id);
            }

            var index =
                new Dictionary<string, ScenarioVehicleRuntimeDefinition>(
                    StringComparer.Ordinal);
            foreach (ScenarioVehicleContentData vehicle in vehicles)
            {
                Require(vehicle != null, "Scenario vehicles cannot contain null entries.");
                RequireText(vehicle.entityId, "Scenario vehicle entity ID");
                Require(
                    levelEntityIds.Contains(vehicle.entityId),
                    $"Vehicle '{vehicle.entityId}' is missing from level '{level.levelId}'.");
                if (!string.IsNullOrWhiteSpace(
                        vehicle.startingOccupantActorId))
                {
                    Require(
                        actors.ContainsKey(vehicle.startingOccupantActorId),
                        $"Vehicle '{vehicle.entityId}' starts with undefined "
                        + $"actor '{vehicle.startingOccupantActorId}'.");
                }

                var definition = new ScenarioVehicleRuntimeDefinition(
                    vehicle.entityId,
                    CreateVehicleProfile(vehicle),
                    vehicle.startingSpeed,
                    vehicle.startingOccupantActorId);
                Require(
                    index.TryAdd(vehicle.entityId, definition),
                    $"Vehicle '{vehicle.entityId}' is defined more than once.");
            }

            return index;
        }

        private static VehicleMomentumProfile CreateVehicleProfile(
            ScenarioVehicleContentData vehicle)
        {
            if (vehicle == null)
            {
                throw new ArgumentNullException(nameof(vehicle));
            }

            return new VehicleMomentumProfile(
                vehicle.maximumSpeed,
                vehicle.accelerationPerTurn,
                vehicle.brakingPerTurn,
                vehicle.lowSpeedTurnDegrees,
                vehicle.highSpeedTurnDegrees,
                vehicle.baseTurningRadius,
                vehicle.speedTurningRadiusFactor);
        }

        private static DisplacementAbilityDefinition
            CreateDisplacementAbility(ScenarioActorContentData actor)
        {
            ScenarioDisplacementAbilityData ability =
                actor?.displacementAbility;
            if (!HasAuthoredDisplacementAbility(ability))
            {
                return null;
            }

            var definitions = new List<DisplacementActionDefinition>();
            foreach (ScenarioDisplacementActionData action in
                ability.actions
                    ?? new List<ScenarioDisplacementActionData>())
            {
                DisplacementActionKind intent = ParseDisplacementIntent(
                    action.intent);
                definitions.Add(new DisplacementActionDefinition(
                    action.id,
                    action.displayName,
                    intent,
                    new ActionCost(
                        action.cost.actionPoints,
                        action.cost.movementOpportunity,
                        ParseMobility(action.cost.mobility)),
                    ParseAcceptedSubjects(action.acceptedSubjectKinds),
                    action.reach,
                    action.maximumDistance,
                    action.maximumSubjectMass,
                    ParseHandRequirement(action.handRequirement),
                    ParseAutoStowPolicy(action.autoStowPolicy),
                    ParseContestPolicy(action.contestPolicy),
                    ParseAllowedResults(action.allowedResults),
                    ParseDisplacementSize(action.maximumSubjectSize),
                    intent == DisplacementActionKind.Throw
                        ? CreateDistanceDecay(action.distanceDecay)
                        : null));
            }

            return new DisplacementAbilityDefinition(
                ability.id,
                ability.displayName,
                ability.hotbarSlot,
                definitions);
        }

        private static void ValidateDisplacementActions(
            ScenarioActorContentData actor)
        {
            ScenarioDisplacementAbilityData ability = actor.displacementAbility;
            if (!HasAuthoredDisplacementAbility(ability))
            {
                return;
            }

            RequireText(
                ability.id,
                $"Actor '{actor.id}' displacement ability ID");
            RequireText(
                ability.displayName,
                $"Actor '{actor.id}' displacement ability display name");
            Require(
                ability.hotbarSlot >= 1
                    && ability.hotbarSlot <= GameplayHotbarRules.SlotCount,
                $"Actor '{actor.id}' displacement ability hotbar slot must be between 1 and {GameplayHotbarRules.SlotCount}.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var hotbarSlots = new HashSet<int>();
            foreach (ScenarioInventoryItemData item in
                actor.inventory ?? new List<ScenarioInventoryItemData>())
            {
                if (item != null && item.hotbarSlot > 0)
                {
                    hotbarSlots.Add(item.hotbarSlot);
                }
            }
            Require(
                hotbarSlots.Add(ability.hotbarSlot),
                $"Actor '{actor.id}' hotbar slot {ability.hotbarSlot} is assigned more than once.");
            Require(
                ability.actions != null && ability.actions.Count > 0,
                $"Actor '{actor.id}' displacement ability requires at least one action.");
            foreach (ScenarioDisplacementActionData action in
                ability.actions
                    ?? new List<ScenarioDisplacementActionData>())
            {
                Require(action != null,
                    $"Actor '{actor.id}' displacement actions cannot contain null entries.");
                RequireText(action.id,
                    $"Actor '{actor.id}' displacement action ID");
                Require(ids.Add(action.id),
                    $"Actor '{actor.id}' displacement action '{action.id}' is duplicated.");
                RequireText(action.displayName,
                    $"Actor '{actor.id}' displacement action '{action.id}' display name");
                Require(action.cost != null,
                    $"Actor '{actor.id}' displacement action '{action.id}' requires a cost.");
                Require(action.cost.actionPoints > 0,
                    $"Actor '{actor.id}' displacement action '{action.id}' must cost at least one AP.");
                ParseMobility(action.cost.mobility);
                DisplacementActionKind intent = ParseDisplacementIntent(
                    action.intent);
                ParseAcceptedSubjects(action.acceptedSubjectKinds);
                RequireFinitePositive(action.reach,
                    $"Actor '{actor.id}' displacement action '{action.id}' reach");
                RequireFinitePositive(action.maximumDistance,
                    $"Actor '{actor.id}' displacement action '{action.id}' maximum distance");
                RequireFinitePositive(action.maximumSubjectMass,
                    $"Actor '{actor.id}' displacement action '{action.id}' maximum subject mass");
                ParseDisplacementSize(action.maximumSubjectSize);
                if (intent == DisplacementActionKind.Throw)
                {
                    Require(action.distanceDecay != null,
                        $"Actor '{actor.id}' throw action '{action.id}' requires distance decay.");
                    RequireFinitePositive(
                        action.distanceDecay.fullDistanceMass,
                        $"Actor '{actor.id}' displacement action '{action.id}' full-distance mass");
                    Require(
                        action.distanceDecay.fullDistanceMass
                            < action.maximumSubjectMass,
                        $"Actor '{actor.id}' displacement action '{action.id}' full-distance mass must be below its maximum mass.");
                    RequireFinitePositive(
                        action.distanceDecay.minimumDistance,
                        $"Actor '{actor.id}' displacement action '{action.id}' minimum distance");
                    Require(
                        action.distanceDecay.minimumDistance
                            <= action.maximumDistance,
                        $"Actor '{actor.id}' displacement action '{action.id}' minimum distance cannot exceed its maximum distance.");
                    RequireFinitePositive(
                        action.distanceDecay.exponent,
                        $"Actor '{actor.id}' displacement action '{action.id}' distance-decay exponent");
                }
                ParseHandRequirement(action.handRequirement);
                ParseAutoStowPolicy(action.autoStowPolicy);
                ParseContestPolicy(action.contestPolicy);
                ParseAllowedResults(action.allowedResults);
            }

            _ = CreateDisplacementAbility(actor);
        }

        private static bool HasAuthoredDisplacementAbility(
            ScenarioDisplacementAbilityData ability) =>
            ability != null
            && (!string.IsNullOrWhiteSpace(ability.id)
                || !string.IsNullOrWhiteSpace(ability.displayName)
                || ability.hotbarSlot != 0
                || (ability.actions != null && ability.actions.Count > 0));

        private static CloseQuartersControlProfile CreateControlProfile(
            ScenarioActorContentData actor)
        {
            if (actor == null)
            {
                throw new ArgumentNullException(nameof(actor));
            }

            CharacterProfileDefinition characterProfile =
                CreateCharacterProfile(actor.characterProfile);
            ValidateControl(actor);
            CharacterRating controlSkill = characterProfile.GetSkill(
                CharacterSkillIds.CloseQuarters);
            return new CloseQuartersControlProfile(
                characterProfile.CoreAttributes.Strength,
                controlSkill.Rating,
                string.IsNullOrWhiteSpace(actor.control.talentId)
                    ? null
                    : actor.control.talentId,
                actor.control.talentModifier);
        }

        private static void ValidateControl(ScenarioActorContentData actor)
        {
            ScenarioControlProfileData control = actor.control;
            Require(control != null, $"Actor '{actor.id}' has no control profile.");
            CharacterProfileDefinition characterProfile =
                CreateCharacterProfile(actor.characterProfile);
            Require(
                characterProfile.GetSkill(CharacterSkillIds.CloseQuarters)
                    != null,
                $"Actor '{actor.id}' requires skill '{CharacterSkillIds.CloseQuarters}'.");
            Require(
                control.talentModifier == 0
                || !string.IsNullOrWhiteSpace(control.talentId),
                $"Actor '{actor.id}' cannot have a talent modifier without a talent ID.");
            Require(
                string.IsNullOrWhiteSpace(control.talentId)
                || ContainsId(
                    characterProfile.TalentIds,
                    control.talentId),
                $"Actor '{actor.id}' control talent '{control.talentId}' is not owned by its character profile.");
        }

        private static bool ContainsId(
            IReadOnlyList<string> values,
            string expected)
        {
            foreach (string value in values)
            {
                if (string.Equals(value, expected, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static GameplayPosition TransformPoint(
            LevelTransformData transform,
            Float3Data local)
        {
            double radians = transform.yawDegrees * (Math.PI / 180d);
            double cosine = Math.Cos(radians);
            double sine = Math.Sin(radians);
            return new GameplayPosition(
                transform.position.x + (float)((local.x * cosine) + (local.z * sine)),
                transform.position.y + local.y,
                transform.position.z + (float)((-local.x * sine) + (local.z * cosine)));
        }

        private static GameplayPosition ToPosition(Float3Data position)
        {
            return new GameplayPosition(position.x, position.y, position.z);
        }

        private static DisplacementActionKind ParseDisplacementIntent(
            string value)
        {
            if (string.Equals(value, "push", StringComparison.OrdinalIgnoreCase))
                return DisplacementActionKind.Push;
            if (string.Equals(value, "lift", StringComparison.OrdinalIgnoreCase))
                return DisplacementActionKind.Lift;
            if (string.Equals(value, "throw", StringComparison.OrdinalIgnoreCase))
                return DisplacementActionKind.Throw;
            if (string.Equals(
                value,
                "push-off",
                StringComparison.OrdinalIgnoreCase))
                return DisplacementActionKind.PushOff;
            throw new InvalidOperationException(
                $"Unknown displacement intent '{value}'.");
        }

        private static DisplacementSizeClass ParseDisplacementSize(
            string value)
        {
            if (string.Equals(value, "tiny", StringComparison.OrdinalIgnoreCase))
                return DisplacementSizeClass.Tiny;
            if (string.Equals(value, "small", StringComparison.OrdinalIgnoreCase))
                return DisplacementSizeClass.Small;
            if (string.Equals(value, "medium", StringComparison.OrdinalIgnoreCase))
                return DisplacementSizeClass.Medium;
            if (string.Equals(value, "large", StringComparison.OrdinalIgnoreCase))
                return DisplacementSizeClass.Large;
            if (string.Equals(value, "huge", StringComparison.OrdinalIgnoreCase))
                return DisplacementSizeClass.Huge;
            throw new InvalidOperationException(
                $"Unknown displacement size class '{value}'.");
        }

        private static DisplacementDistanceDecayDefinition CreateDistanceDecay(
            ScenarioDisplacementDistanceDecayData data) =>
            data == null
                ? null
                : new DisplacementDistanceDecayDefinition(
                    data.fullDistanceMass,
                    data.minimumDistance,
                    data.exponent);

        private static int ResolveOccupiedHands(ScenarioInventoryItemData item)
        {
            InventoryItemKind kind = ParseInventoryItemKind(item.kind);
            return item.occupiedHands < 0
                ? kind == InventoryItemKind.Weapon ? 2 : 0
                : item.occupiedHands;
        }

        private static DisplacementSubjectKinds ParseAcceptedSubjects(
            IEnumerable<string> values)
        {
            DisplacementSubjectKinds result = DisplacementSubjectKinds.None;
            foreach (string value in values ?? Array.Empty<string>())
            {
                if (string.Equals(
                    value,
                    "prop",
                    StringComparison.OrdinalIgnoreCase))
                {
                    result |= DisplacementSubjectKinds.Prop;
                }
                else if (string.Equals(
                    value,
                    "combatant",
                    StringComparison.OrdinalIgnoreCase))
                {
                    result |= DisplacementSubjectKinds.Combatant;
                }
                else
                {
                    throw new InvalidOperationException(
                        $"Unknown displacement subject kind '{value}'.");
                }
            }

            if (result == DisplacementSubjectKinds.None)
            {
                throw new InvalidOperationException(
                    "Displacement actions require at least one accepted subject kind.");
            }

            return result;
        }

        private static DisplacementHandRequirement ParseHandRequirement(
            string value)
        {
            if (string.Equals(value, "none", StringComparison.OrdinalIgnoreCase))
                return DisplacementHandRequirement.None;
            if (string.Equals(
                value,
                "one-hand-free",
                StringComparison.OrdinalIgnoreCase))
                return DisplacementHandRequirement.OneHandFree;
            if (string.Equals(
                value,
                "both-hands-free",
                StringComparison.OrdinalIgnoreCase))
                return DisplacementHandRequirement.BothHandsFree;
            throw new InvalidOperationException(
                $"Unknown displacement hand requirement '{value}'.");
        }

        private static DisplacementAutoStowPolicy ParseAutoStowPolicy(
            string value)
        {
            if (string.Equals(value, "never", StringComparison.OrdinalIgnoreCase))
                return DisplacementAutoStowPolicy.Never;
            if (string.Equals(value, "allowed", StringComparison.OrdinalIgnoreCase))
                return DisplacementAutoStowPolicy.Allowed;
            throw new InvalidOperationException(
                $"Unknown displacement auto-stow policy '{value}'.");
        }

        private static DisplacementContestPolicy ParseContestPolicy(
            string value)
        {
            if (string.Equals(value, "none", StringComparison.OrdinalIgnoreCase))
                return DisplacementContestPolicy.None;
            if (string.Equals(
                value,
                "close-quarters-control",
                StringComparison.OrdinalIgnoreCase))
                return DisplacementContestPolicy.CloseQuartersControl;
            throw new InvalidOperationException(
                $"Unknown displacement contest policy '{value}'.");
        }

        private static DisplacementResultPolicies ParseAllowedResults(
            IEnumerable<string> values)
        {
            DisplacementResultPolicies result =
                DisplacementResultPolicies.None;
            foreach (string value in values ?? Array.Empty<string>())
            {
                if (string.Equals(
                    value,
                    "topple",
                    StringComparison.OrdinalIgnoreCase))
                    result |= DisplacementResultPolicies.Topple;
                else if (string.Equals(
                    value,
                    "release",
                    StringComparison.OrdinalIgnoreCase))
                    result |= DisplacementResultPolicies.Release;
                else if (string.Equals(
                    value,
                    "collision-damage",
                    StringComparison.OrdinalIgnoreCase))
                    result |= DisplacementResultPolicies.CollisionDamage;
                else if (string.Equals(
                    value,
                    "pin",
                    StringComparison.OrdinalIgnoreCase))
                    result |= DisplacementResultPolicies.Pin;
                else
                    throw new InvalidOperationException(
                        $"Unknown displacement result policy '{value}'.");
            }

            return result;
        }

        private static ActorStance ParseStance(string value)
        {
            if (string.Equals(value, "standing", StringComparison.OrdinalIgnoreCase))
            {
                return ActorStance.Standing;
            }

            if (string.Equals(value, "crouched", StringComparison.OrdinalIgnoreCase))
            {
                return ActorStance.Crouched;
            }

            throw new InvalidOperationException($"Unknown actor stance '{value}'.");
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
