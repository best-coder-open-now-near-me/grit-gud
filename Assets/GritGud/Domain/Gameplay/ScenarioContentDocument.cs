using System;
using System.Collections.Generic;
using GritGud.Domain.Levels;
using GritGud.Domain.Turns;

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
        public int startingActionPoints = 4;
        public int actionPointIncome = 4;
        public int maximumHeldActionPoints = 6;
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
    public sealed class ScenarioSurfaceDamageModifierData
    {
        public string surfaceId = string.Empty;
        public float multiplier = 1f;
    }

    [Serializable]
    public sealed class ScenarioDirectFireDamageData
    {
        public string damageTypeId = string.Empty;
        public float baseIntegrityDamage;
        public List<ScenarioSurfaceDamageModifierData> surfaceModifiers =
            new List<ScenarioSurfaceDamageModifierData>();
    }

    [Serializable]
    public sealed class ScenarioWeaponDamageRangeData
    {
        public float halfLifeDistance;
        public int minimumTransferPercent = 100;
    }

    [Serializable]
    public sealed class ScenarioRegionConsequenceData
    {
        public string region = string.Empty;
        public int systemicPerHundred;
        public int structuralPerHundred;
        public int motorPerHundred;
        public int sensoryPerHundred;
        public int bleedPerHundred;
        public int consciousnessPerHundred;
        public int respirationPerHundred;
        public int criticalIncapacitationImpact;
        public int vitalImpact;
    }

    [Serializable]
    public sealed class ScenarioWeaponDamageProfileData
    {
        public int schemaVersion = 1;
        public string damageProfileId = string.Empty;
        public string mechanism = string.Empty;
        public int baseImpact;
        public int penetration;
        public ScenarioWeaponDamageRangeData range =
            new ScenarioWeaponDamageRangeData();
        public List<ScenarioRegionConsequenceData> regions =
            new List<ScenarioRegionConsequenceData>();
        public ScenarioDirectFireDamageData directFireDamage;
    }

    [Serializable]
    public sealed class ScenarioWeaponHandlingProfileData
    {
        public int schemaVersion;
        public int requiredHands;
        public string primaryHand = "right";
        public int minimumPrimaryGrip;
        public int minimumSupportGrip;
        public int minimumAimStability;
        public int minimumReloadCapacity;
        public bool canBraceWithOneHand;
        public bool canFireProne = true;
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
        public ScenarioDirectFireDamageData directFireDamage;
        public ScenarioWeaponDamageProfileData damageProfile;
        public ScenarioWeaponHandlingProfileData handlingProfile;
        public float soundSignature = 1f;
        public float directVehicleIntegrityDamage;
    }

    [Serializable]
    public sealed class ScenarioTacticalPredicateData
    {
        public string feature = string.Empty;
        public string comparison = "equal";
        public string value = string.Empty;
    }

    [Serializable]
    public sealed class ScenarioTacticalConsequencesData
    {
        public int accuracyDeltaPercent;
        public int woundDelta;
        public bool hasReactionsAllowed;
        public bool reactionsAllowed;
        public float soundMultiplier = 1f;
        public int actionPointCostDelta;
    }

    [Serializable]
    public sealed class ScenarioTacticalRuleData
    {
        public string id = string.Empty;
        public string displayName = string.Empty;
        public int order;
        public string capability = string.Empty;
        public List<string> subjectKinds = new List<string>();
        public List<ScenarioTacticalPredicateData> predicates =
            new List<ScenarioTacticalPredicateData>();
        public ScenarioTacticalConsequencesData consequences =
            new ScenarioTacticalConsequencesData();
        public List<string> outcomeFeatureIds = new List<string>();
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
        public ScenarioWeaponAmmunitionData ammunition;
        public ScenarioConsumablePowerData consumablePower;
    }

    [Serializable]
    public sealed class ScenarioWeaponAmmunitionData
    {
        // JsonUtility materializes omitted/null nested objects. This explicit
        // discriminator keeps ammunition-free items portable across serializers.
        public bool enabled;
        public string ammoTypeId = string.Empty;
        public int magazineCapacity;
        public int initialLoadedRounds;
        public int roundsPerUse = 1;
        public ScenarioActionCostData reloadCost = new ScenarioActionCostData();
        public bool consumesRemainingMovement = true;
        public int reloadPolicyVersion = 1;
    }

    [Serializable]
    public sealed class ScenarioAmmunitionReserveData
    {
        public string ammoTypeId = string.Empty;
        public int rounds;
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
        public int blastActionPointReduction;
        public ScenarioSmokeFieldData smokeField;
        public ScenarioFireFieldData fireField;
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
    public sealed class ScenarioFireFieldData
    {
        public float initialRadius;
        public float maximumRadius;
        public float height;
        public float explorationDurationSeconds;
        public int durationTurnEnds;
        public float explorationPulseSeconds;
        public float actorWoundMovementPenalty;
        public float destructibleIntegrityDamage;
        public float minimumHazardPath;
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
        public int minimumAttackHitChancePercent = 25;
        public ScenarioEncounterAwarenessData awareness;
        public ScenarioPatrolRouteData patrol;
        public List<string> reinforcementActorIds = new List<string>();
    }

    [Serializable]
    public sealed class ScenarioEncounterAwarenessData
    {
        public float hearingRange;
        public int sightSuspicionGain;
        public int soundSuspicionGain;
        public int suspicionDecayPerTick;
        public int alertThreshold;
    }

    [Serializable]
    public sealed class ScenarioPatrolRouteData
    {
        // JsonUtility materializes omitted/null nested objects. This explicit
        // discriminator keeps stationary enemies portable across serializers.
        public bool enabled;
        public bool loops = true;
        public List<Float3Data> waypoints = new List<Float3Data>();
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
        public List<ScenarioAmmunitionReserveData> ammunitionReserves =
            new List<ScenarioAmmunitionReserveData>();
        public ScenarioCharacterProfileData characterProfile;
    }

    [Serializable]
    public sealed class ScenarioCharacterRatingData
    {
        public string id = string.Empty;
        public int rating;
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
    }

    [Serializable]
    public sealed class ScenarioActionCostData
    {
        public int actionPoints;
        public float movementOpportunity;
        public string mobility = ActionMobilityCodec.SetValue;
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
    public sealed class ScenarioPropTopplingData
    {
        public bool enabled;
        public float pitchOffsetDegrees;
        public float rollOffsetDegrees = 90f;
        public float elevationOffset;
    }

    [Serializable]
    public sealed class ScenarioPropPinningData
    {
        public bool enabled;
        public float maximumActorMass;
        public float minimumContactDepth;
    }

    [Serializable]
    public sealed class ScenarioPropContentData
    {
        public string entityId = string.Empty;
        public float mass;
        public string sizeClass = "medium";
        public ScenarioPropTopplingData toppling =
            new ScenarioPropTopplingData();
        public ScenarioPropPinningData pinning =
            new ScenarioPropPinningData();
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
    public sealed class ScenarioDroneContentData
    {
        public string entityId = string.Empty;
        public string controllerActorId = string.Empty;
        public float maximumIntegrity;
        public float maximumMoveDistance;
        public ScenarioActionCostData moveCost = new ScenarioActionCostData();
        public float sensorRange;
        public float sensorViewAngleDegrees;
        public ScenarioAttackCapabilityData attackCapability;
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
        public const int CurrentSchemaVersion = 21;

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
        public List<ScenarioDroneContentData> drones =
            new List<ScenarioDroneContentData>();
        public List<ScenarioTacticalRuleData> tacticalRules =
            new List<ScenarioTacticalRuleData>();

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
            drones = drones ?? new List<ScenarioDroneContentData>();
            tacticalRules = tacticalRules
                ?? new List<ScenarioTacticalRuleData>();
        }
    }
}
