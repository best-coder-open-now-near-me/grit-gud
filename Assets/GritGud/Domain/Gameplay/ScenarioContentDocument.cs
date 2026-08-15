using System;
using System.Collections.Generic;
using GritGud.Domain.Levels;

namespace GritGud.Domain.Gameplay
{
    [Serializable]
    public sealed class ScenarioTurnBudgetData
    {
        public int actionPoints;
    }

    [Serializable]
    public sealed class ScenarioTimingData
    {
        public float minimumVoluntaryTurnSeconds;
    }

    [Serializable]
    public sealed class ScenarioControlProfileData
    {
        public string talentId = string.Empty;
        public int talentModifier;
    }

    [Serializable]
    public sealed class ScenarioDisplacementActionData
    {
        public string id = string.Empty;
        public string displayName = string.Empty;
        public string intent = string.Empty;
        public ScenarioActionCostData cost = new ScenarioActionCostData();
        public List<string> acceptedSubjectKinds = new List<string>();
        public float reach;
        public float maximumDistance;
        public float maximumSubjectMass;
        public string maximumSubjectSize = "huge";
        public ScenarioDisplacementDistanceDecayData distanceDecay;
        public string handRequirement = "none";
        public string autoStowPolicy = "never";
        public string contestPolicy = "none";
        public List<string> allowedResults = new List<string>();
    }

    [Serializable]
    public sealed class ScenarioDisplacementDistanceDecayData
    {
        public float fullDistanceMass;
        public float minimumDistance;
        public float exponent = 1f;
    }

    [Serializable]
    public sealed class ScenarioDisplacementAbilityData
    {
        public string id = string.Empty;
        public string displayName = string.Empty;
        public int hotbarSlot;
        public List<ScenarioDisplacementActionData> actions =
            new List<ScenarioDisplacementActionData>();
    }

    [Serializable]
    public sealed class ScenarioProjectileCapabilityData
    {
        public bool enabled;
        public string id = string.Empty;
        public float speedPerTurn;
        public float radius;
        public float maximumRange;
        public float standingLaunchHeight;
        public float crouchedLaunchHeight;
        public bool opensEmergencyReactionWindow;
        public float blastRadius;
        public float blastWoundMovementPenalty;
        public float blastIntegrityDamage;
    }

    [Serializable]
    public sealed class ScenarioAccuracyDecayData
    {
        public float halfLifeDistance;
        public float minimumAccuracyPercent;
    }

    [Serializable]
    public sealed class ScenarioContactAttackData
    {
        public bool enabled;
        public float maximumReach;
    }

    [Serializable]
    public sealed class ScenarioAttackCapabilityData
    {
        public bool enabled;
        public string actionId = string.Empty;
        public string displayName = string.Empty;
        public ScenarioActionCostData turnCost = new ScenarioActionCostData();
        public float woundMovementPenalty;
        public ScenarioAccuracyDecayData accuracyDecay;
        public ScenarioProjectileCapabilityData projectile;
        public ScenarioContactAttackData contact;
    }

    [Serializable]
    public sealed class ScenarioEquipmentEffectData
    {
        public float movementSpeedMultiplier = 1f;
    }

    [Serializable]
    public sealed class ScenarioInventoryItemData
    {
        public string id = string.Empty;
        public string displayName = string.Empty;
        public int hotbarSlot;
        public string kind = "weapon";
        public int occupiedHands = -1;
        public int quantity;
        public ScenarioActionCostData equipmentCost = new ScenarioActionCostData();
        public ScenarioEquipmentEffectData equippedEffects =
            new ScenarioEquipmentEffectData();
        public ScenarioAttackCapabilityData attackCapability;
        public ScenarioConsumablePowerData consumablePower;
    }

    [Serializable]
    public sealed class ScenarioConsumablePowerData
    {
        public string type = string.Empty;
        public ScenarioThrownExplosiveData thrownExplosive;
    }

    [Serializable]
    public sealed class ScenarioThrownExplosiveData
    {
        public ScenarioActionCostData turnCost = new ScenarioActionCostData();
        public float maximumRange;
        public float standingLaunchHeight;
        public float crouchedLaunchHeight;
        public float baseUncertaintyRadius;
        public float uncertaintyPerMeter;
        public float blastRadius;
        public float blastWoundMovementPenalty;
        public float blastIntegrityDamage;
        public ScenarioSmokeFieldData smokeField;
    }

    [Serializable]
    public sealed class ScenarioSmokeFieldData
    {
        public float radius;
        public float height;
        public float explorationDurationSeconds;
        public int durationTurnEnds;
        public float minimumObscuredPath;
    }

    [Serializable]
    public sealed class ScenarioAttackResponseData
    {
        public bool startsEncounter;
    }

    [Serializable]
    public sealed class ScenarioEnemyBehaviorData
    {
        public string behaviorId = string.Empty;
        public float perceptionRange;
        public float viewAngleDegrees;
        public float preferredEngagementRange;
        public float movementSearchRadius;
        public int maximumAttacksPerTurn;
    }

    [Serializable]
    public sealed class ScenarioActorCombatData
    {
        public string allegianceId = string.Empty;
        public List<string> hostileAllegianceIds = new List<string>();
        public int maximumWounds;
        public ScenarioEnemyBehaviorData enemyBehavior;
    }

    [Serializable]
    public sealed class ScenarioActorContentData
    {
        public string id = string.Empty;
        public string displayName = string.Empty;
        public string presentationId = string.Empty;
        public string characterId = string.Empty;
        public bool targetable = true;
        public Float3Data position;
        public float facingDegrees;
        public string stance = "standing";
        public ScenarioTurnBudgetData turnBudget = new ScenarioTurnBudgetData();
        public float mass;
        public string sizeClass = "medium";
        public ScenarioControlProfileData control = new ScenarioControlProfileData();
        public ScenarioDisplacementAbilityData displacementAbility;
        public ScenarioAttackCapabilityData attackCapability;
        public ScenarioAttackResponseData attackResponse;
        public ScenarioActorCombatData combat;
        public string initiallyEquippedItemId = string.Empty;
        public List<ScenarioInventoryItemData> inventory =
            new List<ScenarioInventoryItemData>();
        public ScenarioCharacterProfileData characterProfile;
    }

    [Serializable]
    public sealed class ScenarioCharacterRatingData
    {
        public string id = string.Empty;
        public int rating;
    }

    [Serializable]
    public sealed class ScenarioAdvancementOptionData
    {
        public string id = string.Empty;
        public string skillId = string.Empty;
        public int pointCost;
        public int maximumBonus;
    }

    [Serializable]
    public sealed class ScenarioCharacterProfileData
    {
        public string identityId = string.Empty;
        public string displayName = string.Empty;
        public string archetype = string.Empty;
        public List<ScenarioCharacterRatingData> attributes = new List<ScenarioCharacterRatingData>();
        public List<ScenarioCharacterRatingData> skills = new List<ScenarioCharacterRatingData>();
        public List<string> talentIds = new List<string>();
        public int startingProgressionPoints;
        public List<ScenarioAdvancementOptionData> advancementOptions = new List<ScenarioAdvancementOptionData>();
    }

    [Serializable]
    public sealed class ScenarioActionCostData
    {
        public int actionPoints;
        public float movementOpportunity;
        public string mobility = "set";
    }

    [Serializable]
    public sealed class ScenarioObjectiveContentData
    {
        public string id = string.Empty;
        public string levelInteractionPointId = string.Empty;
        public string levelInteractionPointType = string.Empty;
        public string actionId = string.Empty;
        public string displayName = string.Empty;
        public string activeHudText = string.Empty;
        public string completedHudText = string.Empty;
        public ScenarioActionCostData turnCost = new ScenarioActionCostData();
    }

    [Serializable]
    public sealed class ScenarioPropContentData
    {
        public string entityId = string.Empty;
        public float mass;
        public string sizeClass = "medium";
        public ScenarioAttackResponseData attackResponse;
    }

    [Serializable]
    public sealed class ScenarioVehicleContentData
    {
        public string entityId = string.Empty;
        public float maximumSpeed;
        public float accelerationPerTurn;
        public float brakingPerTurn;
        public float lowSpeedTurnDegrees;
        public float highSpeedTurnDegrees;
        public float baseTurningRadius;
        public float speedTurningRadiusFactor;
        public float startingSpeed;
        public string startingOccupantActorId = string.Empty;
        public ScenarioAttackResponseData attackResponse;
    }

    [Serializable]
    public sealed class ScenarioPlayerPartyData
    {
        public List<string> actorIds = new List<string>();
        public string initiallySelectedActorId = string.Empty;

        public void Normalize()
        {
            actorIds = actorIds ?? new List<string>();
            for (int index = 0; index < actorIds.Count; index++)
                actorIds[index] = actorIds[index] ?? string.Empty;
            initiallySelectedActorId = initiallySelectedActorId ?? string.Empty;
        }
    }

    [Serializable]
    public sealed class ScenarioContentDocument
    {
        public const int CurrentSchemaVersion = 12;

        public int schemaVersion = CurrentSchemaVersion;
        public string scenarioId = string.Empty;
        public string displayName = string.Empty;
        public string levelId = string.Empty;
        public ScenarioPlayerPartyData playerParty =
            new ScenarioPlayerPartyData();
        public string primaryTargetActorId = string.Empty;
        public string primaryObjectiveId = string.Empty;
        public uint randomSeed;
        public ScenarioTimingData timing = new ScenarioTimingData();
        public List<ScenarioActorContentData> actors =
            new List<ScenarioActorContentData>();
        public List<ScenarioObjectiveContentData> objectives =
            new List<ScenarioObjectiveContentData>();
        public List<ScenarioPropContentData> props =
            new List<ScenarioPropContentData>();
        public List<ScenarioVehicleContentData> vehicles =
            new List<ScenarioVehicleContentData>();

        public void Normalize()
        {
            scenarioId = scenarioId ?? string.Empty;
            displayName = string.IsNullOrWhiteSpace(displayName)
                ? scenarioId
                : displayName;
            levelId = levelId ?? string.Empty;
            playerParty = playerParty ?? new ScenarioPlayerPartyData();
            playerParty.Normalize();
            primaryTargetActorId = primaryTargetActorId ?? string.Empty;
            primaryObjectiveId = primaryObjectiveId ?? string.Empty;
            timing = timing ?? new ScenarioTimingData();
            actors = actors ?? new List<ScenarioActorContentData>();
            objectives = objectives ?? new List<ScenarioObjectiveContentData>();
            props = props ?? new List<ScenarioPropContentData>();
            vehicles = vehicles ?? new List<ScenarioVehicleContentData>();
        }
    }
}
