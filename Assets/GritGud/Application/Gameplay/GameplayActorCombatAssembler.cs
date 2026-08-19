using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Levels;
using GritGud.Domain.Turns;

namespace GritGud.Application.Gameplay
{
    internal static class GameplayActorCombatAssembler
    {
        public static ActorCombatDefinition CreateCombatDefinition(
            ScenarioActorCombatData data)
        {
            if (data == null)
                return null;

            EnemyBehaviorDefinition behavior =
                !HasAuthoredEnemyBehavior(data.enemyBehavior)
                ? null
                : new EnemyBehaviorDefinition(
                    data.enemyBehavior.behaviorId,
                    data.enemyBehavior.perceptionRange,
                    data.enemyBehavior.viewAngleDegrees,
                    data.enemyBehavior.preferredEngagementRange,
                    data.enemyBehavior.movementSearchRadius,
                    data.enemyBehavior.maximumAttacksPerTurn,
                    data.enemyBehavior.minimumAttackHitChancePercent,
                    CreateAwarenessPolicy(data.enemyBehavior.awareness),
                    CreatePatrolRoute(data.enemyBehavior.patrol),
                    data.enemyBehavior.reinforcementActorIds);
            return new ActorCombatDefinition(
                data.allegianceId,
                data.hostileAllegianceIds,
                data.maximumWounds,
                behavior);
        }

        public static void ValidateCombat(ScenarioActorContentData actor)
        {
            ScenarioActorCombatData data = actor.combat;
            if (data == null)
                return;

            GameplayScenarioAssemblyValidation.RequireText(
                data.allegianceId,
                $"Actor '{actor.id}' combat allegiance");
            GameplayScenarioAssemblyValidation.Require(
                data.maximumWounds > 0,
                $"Actor '{actor.id}' maximum wounds must be greater than zero.");
            var hostileIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (string hostileId in data.hostileAllegianceIds
                ?? new List<string>())
            {
                GameplayScenarioAssemblyValidation.RequireText(
                    hostileId,
                    $"Actor '{actor.id}' hostile allegiance");
                GameplayScenarioAssemblyValidation.Require(
                    !string.Equals(
                        hostileId,
                        data.allegianceId,
                        StringComparison.Ordinal),
                    $"Actor '{actor.id}' cannot be hostile to its own allegiance.");
                GameplayScenarioAssemblyValidation.Require(
                    hostileIds.Add(hostileId),
                    $"Actor '{actor.id}' hostile allegiance '{hostileId}' is duplicated.");
            }

            if (HasAuthoredEnemyBehavior(data.enemyBehavior))
            {
                GameplayScenarioAssemblyValidation.RequireText(
                    data.enemyBehavior.behaviorId,
                    $"Actor '{actor.id}' enemy behavior ID");
                GameplayScenarioAssemblyValidation.RequireFinitePositive(
                    data.enemyBehavior.perceptionRange,
                    $"Actor '{actor.id}' perception range");
                GameplayScenarioAssemblyValidation.Require(
                    !float.IsNaN(data.enemyBehavior.viewAngleDegrees)
                        && !float.IsInfinity(
                            data.enemyBehavior.viewAngleDegrees)
                        && data.enemyBehavior.viewAngleDegrees > 0f
                        && data.enemyBehavior.viewAngleDegrees <= 360f,
                    $"Actor '{actor.id}' view angle must be greater than zero and no more than 360 degrees.");
                GameplayScenarioAssemblyValidation.RequireFinitePositive(
                    data.enemyBehavior.preferredEngagementRange,
                    $"Actor '{actor.id}' preferred engagement range");
                GameplayScenarioAssemblyValidation.Require(
                    data.enemyBehavior.preferredEngagementRange
                        <= data.enemyBehavior.perceptionRange,
                    $"Actor '{actor.id}' preferred engagement range cannot exceed perception range.");
                GameplayScenarioAssemblyValidation.RequireFinitePositive(
                    data.enemyBehavior.movementSearchRadius,
                    $"Actor '{actor.id}' movement search radius");
                GameplayScenarioAssemblyValidation.Require(
                    data.enemyBehavior.maximumAttacksPerTurn > 0,
                    $"Actor '{actor.id}' maximum attacks per turn must be greater than zero.");
                GameplayScenarioAssemblyValidation.Require(
                    data.enemyBehavior.minimumAttackHitChancePercent >= 0
                        && data.enemyBehavior.minimumAttackHitChancePercent
                            <= 100,
                    $"Actor '{actor.id}' minimum attack hit chance must be between 0 and 100.");
                ValidateAwareness(actor.id, data.enemyBehavior.awareness);
                ValidatePatrol(actor.id, data.enemyBehavior.patrol);
                ValidateReinforcements(
                    actor.id,
                    data.enemyBehavior.reinforcementActorIds);
                GameplayScenarioAssemblyValidation.Require(
                    HasImmediateEnemyAttack(actor),
                    $"Enemy actor '{actor.id}' requires an equipped immediate attack.");
                GameplayScenarioAssemblyValidation.Require(
                    hostileIds.Count > 0,
                    $"Enemy actor '{actor.id}' requires at least one hostile allegiance.");
            }

            _ = CreateCombatDefinition(data);
        }

        public static bool HasAuthoredEnemyBehavior(
            ScenarioEnemyBehaviorData behavior) =>
            behavior != null
            && (!string.IsNullOrWhiteSpace(behavior.behaviorId)
                || behavior.perceptionRange != 0f
                || behavior.viewAngleDegrees != 0f
                || behavior.preferredEngagementRange != 0f
                || behavior.movementSearchRadius != 0f
                || behavior.maximumAttacksPerTurn != 0);

        internal static void ValidateEncounterReferences(
            ScenarioActorContentData actor,
            IReadOnlyDictionary<string, ScenarioActorContentData> actors)
        {
            ScenarioEnemyBehaviorData behavior = actor.combat?.enemyBehavior;
            if (!HasAuthoredEnemyBehavior(behavior))
                return;
            foreach (string reinforcementId in behavior.reinforcementActorIds
                ?? new List<string>())
            {
                GameplayScenarioAssemblyValidation.Require(
                    !string.Equals(reinforcementId, actor.id,
                        StringComparison.Ordinal),
                    $"Enemy actor '{actor.id}' cannot reinforce itself.");
                GameplayScenarioAssemblyValidation.Require(
                    actors.TryGetValue(reinforcementId, out ScenarioActorContentData reinforcement),
                    $"Enemy actor '{actor.id}' references unknown reinforcement '{reinforcementId}'.");
                GameplayScenarioAssemblyValidation.Require(
                    HasAuthoredEnemyBehavior(reinforcement.combat?.enemyBehavior),
                    $"Enemy actor '{actor.id}' reinforcement '{reinforcementId}' must be AI-controlled.");
            }
        }

        private static EncounterAwarenessPolicyDefinition CreateAwarenessPolicy(
            ScenarioEncounterAwarenessData data) =>
            data == null
                ? null
                : new EncounterAwarenessPolicyDefinition(
                    data.hearingRange,
                    data.sightSuspicionGain,
                    data.soundSuspicionGain,
                    data.suspicionDecayPerTick,
                    data.alertThreshold);

        private static PatrolRouteDefinition CreatePatrolRoute(
            ScenarioPatrolRouteData data)
        {
            if (data == null || !data.enabled)
                return null;
            var points = new List<GameplayPosition>();
            foreach (Float3Data point in data.waypoints ?? new List<Float3Data>())
                points.Add(new GameplayPosition(point.x, point.y, point.z));
            return new PatrolRouteDefinition(points, data.loops);
        }

        private static void ValidateAwareness(
            string actorId,
            ScenarioEncounterAwarenessData data)
        {
            GameplayScenarioAssemblyValidation.Require(
                data != null,
                $"Enemy actor '{actorId}' requires an awareness policy.");
            GameplayScenarioAssemblyValidation.RequireFinitePositive(
                data.hearingRange,
                $"Enemy actor '{actorId}' hearing range");
            ValidateSuspicionValue(
                actorId, data.sightSuspicionGain, "sight suspicion gain", allowZero: false);
            ValidateSuspicionValue(
                actorId, data.soundSuspicionGain, "sound suspicion gain", allowZero: false);
            ValidateSuspicionValue(
                actorId, data.suspicionDecayPerTick, "suspicion decay", allowZero: true);
            GameplayScenarioAssemblyValidation.Require(
                data.alertThreshold > 0 && data.alertThreshold <= 100,
                $"Enemy actor '{actorId}' alert threshold must be between 1 and 100.");
        }

        private static void ValidatePatrol(
            string actorId,
            ScenarioPatrolRouteData data)
        {
            if (data == null || !data.enabled)
                return;
            GameplayScenarioAssemblyValidation.Require(
                data.waypoints != null && data.waypoints.Count >= 2,
                $"Enemy actor '{actorId}' patrol requires at least two waypoints.");
            try
            {
                _ = CreatePatrolRoute(data);
            }
            catch (ArgumentException exception)
            {
                throw new InvalidOperationException(
                    $"Enemy actor '{actorId}' patrol is invalid: "
                    + exception.Message,
                    exception);
            }
        }

        private static void ValidateReinforcements(
            string actorId,
            IEnumerable<string> values)
        {
            var unique = new HashSet<string>(StringComparer.Ordinal);
            foreach (string value in values ?? new List<string>())
            {
                GameplayScenarioAssemblyValidation.RequireText(
                    value,
                    $"Enemy actor '{actorId}' reinforcement ID");
                GameplayScenarioAssemblyValidation.Require(
                    unique.Add(value),
                    $"Enemy actor '{actorId}' reinforcement '{value}' is duplicated.");
            }
        }

        private static void ValidateSuspicionValue(
            string actorId,
            int value,
            string label,
            bool allowZero) => GameplayScenarioAssemblyValidation.Require(
                value >= (allowZero ? 0 : 1) && value <= 100,
                $"Enemy actor '{actorId}' {label} must be between "
                + (allowZero ? "0" : "1") + " and 100.");

        public static void ValidateAttack(
            string actorId,
            ScenarioAttackCapabilityData attack)
        {
            if (attack == null || !attack.enabled)
                return;

            GameplayScenarioAssemblyValidation.RequireText(
                attack.actionId,
                $"Actor '{actorId}' attack ID");
            GameplayScenarioAssemblyValidation.RequireText(
                attack.displayName,
                $"Actor '{actorId}' attack display name");
            GameplayScenarioAssemblyValidation.Require(
                attack.turnCost != null,
                $"Actor '{actorId}' attack requires a turn cost.");
            GameplayScenarioAssemblyValidation.RequireFinitePositive(
                attack.woundMovementPenalty,
                $"Actor '{actorId}' wound movement penalty");
            GameplayScenarioAssemblyValidation.Require(
                !float.IsNaN(attack.soundSignature)
                    && !float.IsInfinity(attack.soundSignature)
                    && attack.soundSignature >= 0f
                    && attack.soundSignature <= 1f,
                $"Actor '{actorId}' attack sound signature must be between zero and one.");
            GameplayScenarioAssemblyValidation.ParseMobility(
                attack.turnCost.mobility);

            ScenarioProjectileCapabilityData projectile = attack.projectile;
            ScenarioContactAttackData contact = attack.contact;
            ScenarioDirectFireDamageData directFireDamage =
                HasAuthoredDirectFireDamage(attack.directFireDamage)
                    ? attack.directFireDamage
                    : null;
            bool contactEnabled = contact != null && contact.enabled;
            GameplayScenarioAssemblyValidation.Require(
                !contactEnabled || projectile == null || !projectile.enabled,
                $"Actor '{actorId}' attack cannot be both contact and projectile-delivered.");
            GameplayScenarioAssemblyValidation.Require(
                !contactEnabled
                    || attack.accuracyDecay == null
                    || (attack.accuracyDecay.halfLifeDistance == 0f
                        && attack.accuracyDecay.minimumAccuracyPercent == 0f),
                $"Actor '{actorId}' contact attack cannot author ranged accuracy decay.");
            if (contactEnabled)
            {
                GameplayScenarioAssemblyValidation.Require(
                    directFireDamage == null,
                    $"Actor '{actorId}' contact attack cannot author direct-fire prop damage.");
                GameplayScenarioAssemblyValidation.RequireFinitePositive(
                    contact.maximumReach,
                    $"Actor '{actorId}' contact attack maximum reach");
                return;
            }

            if (projectile == null || !projectile.enabled)
            {
                GameplayScenarioAssemblyValidation.Require(
                    attack.accuracyDecay != null,
                    $"Actor '{actorId}' ranged immediate attack requires an accuracy-decay function.");
                GameplayScenarioAssemblyValidation.RequireFinitePositive(
                    attack.accuracyDecay.halfLifeDistance,
                    $"Actor '{actorId}' attack accuracy half-life distance");
                GameplayScenarioAssemblyValidation.Require(
                    !float.IsNaN(
                        attack.accuracyDecay.minimumAccuracyPercent)
                        && !float.IsInfinity(
                            attack.accuracyDecay.minimumAccuracyPercent)
                        && attack.accuracyDecay.minimumAccuracyPercent > 0f
                        && attack.accuracyDecay.minimumAccuracyPercent <= 100f,
                    $"Actor '{actorId}' attack minimum accuracy must be greater than zero and no more than 100 percent.");
                ValidateDirectFireDamage(actorId, directFireDamage);
                return;
            }

            GameplayScenarioAssemblyValidation.Require(
                directFireDamage == null,
                $"Actor '{actorId}' projectile attack cannot author immediate direct-fire prop damage.");
            GameplayScenarioAssemblyValidation.RequireText(
                projectile.id,
                $"Actor '{actorId}' projectile ID");
            GameplayScenarioAssemblyValidation.RequireFinitePositive(
                projectile.speedPerTurn,
                $"Actor '{actorId}' projectile speed per turn");
            GameplayScenarioAssemblyValidation.RequireFinitePositive(
                projectile.radius,
                $"Actor '{actorId}' projectile radius");
            GameplayScenarioAssemblyValidation.RequireFinitePositive(
                projectile.maximumRange,
                $"Actor '{actorId}' projectile maximum range");
            GameplayScenarioAssemblyValidation.RequireFiniteNonNegative(
                projectile.standingLaunchHeight,
                $"Actor '{actorId}' standing projectile launch height");
            GameplayScenarioAssemblyValidation.RequireFiniteNonNegative(
                projectile.crouchedLaunchHeight,
                $"Actor '{actorId}' crouched projectile launch height");
            GameplayScenarioAssemblyValidation.RequireFiniteNonNegative(
                projectile.blastRadius,
                $"Actor '{actorId}' projectile blast radius");
            GameplayScenarioAssemblyValidation.RequireFiniteNonNegative(
                projectile.blastWoundMovementPenalty,
                $"Actor '{actorId}' projectile blast wound penalty");
            GameplayScenarioAssemblyValidation.RequireFiniteNonNegative(
                projectile.blastIntegrityDamage,
                $"Actor '{actorId}' projectile blast integrity damage");
            GameplayScenarioAssemblyValidation.Require(
                (projectile.blastRadius == 0f)
                    == (projectile.blastWoundMovementPenalty == 0f
                        && projectile.blastIntegrityDamage == 0f),
                $"Actor '{actorId}' projectile blast radius and consequences must be authored together.");
        }

        public static AttackDefinition CreateAttackDefinition(
            string actorId,
            ScenarioAttackCapabilityData attack)
        {
            if (attack == null || !attack.enabled)
                return null;

            ScenarioActionCostData cost = attack.turnCost
                ?? throw new InvalidOperationException(
                    $"Actor '{actorId}' attack requires a turn cost.");
            ScenarioProjectileCapabilityData projectile = attack.projectile;
            ProjectileFlightDefinition projectileDefinition =
                projectile != null && projectile.enabled
                    ? new ProjectileFlightDefinition(
                        projectile.id,
                        projectile.speedPerTurn,
                        projectile.radius,
                        projectile.maximumRange,
                        projectile.standingLaunchHeight,
                        projectile.crouchedLaunchHeight,
                        projectile.opensEmergencyReactionWindow,
                        projectile.blastRadius,
                        projectile.blastWoundMovementPenalty,
                        projectile.blastIntegrityDamage)
                    : null;
            AccuracyDecayDefinition accuracyDecayDefinition =
                projectileDefinition != null
                    || (attack.contact != null && attack.contact.enabled)
                    ? null
                    : new AccuracyDecayDefinition(
                        attack.accuracyDecay.halfLifeDistance,
                        attack.accuracyDecay.minimumAccuracyPercent);
            ContactAttackDefinition contactDefinition = attack.contact == null
                || !attack.contact.enabled
                ? null
                : new ContactAttackDefinition(attack.contact.maximumReach);
            DirectFireDamageDefinition directFireDamageDefinition =
                CreateDirectFireDamageDefinition(attack.directFireDamage);
            return new AttackDefinition(
                attack.actionId,
                attack.displayName,
                new ActionCost(
                    cost.actionPoints,
                    cost.movementOpportunity,
                    GameplayScenarioAssemblyValidation.ParseMobility(
                        cost.mobility)),
                attack.woundMovementPenalty,
                projectileDefinition,
                accuracyDecayDefinition,
                contactDefinition,
                directFireDamageDefinition,
                attack.soundSignature);
        }

        private static bool HasImmediateEnemyAttack(
            ScenarioActorContentData actor)
        {
            if (actor.attackCapability?.enabled == true
                && (actor.attackCapability.projectile == null
                    || !actor.attackCapability.projectile.enabled))
            {
                return true;
            }
            if (string.IsNullOrWhiteSpace(actor.initiallyEquippedItemId))
                return false;
            foreach (ScenarioInventoryItemData item in actor.inventory
                ?? new List<ScenarioInventoryItemData>())
            {
                if (item != null
                    && string.Equals(
                        item.id,
                        actor.initiallyEquippedItemId,
                        StringComparison.Ordinal)
                    && item.attackCapability?.enabled == true
                    && (item.attackCapability.projectile == null
                        || !item.attackCapability.projectile.enabled))
                {
                    return true;
                }
            }
            return false;
        }

        private static void ValidateDirectFireDamage(
            string actorId,
            ScenarioDirectFireDamageData damage)
        {
            if (damage == null)
                return;

            GameplayScenarioAssemblyValidation.RequireText(
                damage.damageTypeId,
                $"Actor '{actorId}' direct-fire damage type");
            GameplayScenarioAssemblyValidation.RequireFinitePositive(
                damage.baseIntegrityDamage,
                $"Actor '{actorId}' direct-fire base integrity damage");
            var surfaceIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (ScenarioSurfaceDamageModifierData modifier in
                damage.surfaceModifiers
                    ?? new List<ScenarioSurfaceDamageModifierData>())
            {
                GameplayScenarioAssemblyValidation.Require(
                    modifier != null,
                    $"Actor '{actorId}' direct-fire surface modifiers cannot contain null entries.");
                GameplayScenarioAssemblyValidation.RequireText(
                    modifier.surfaceId,
                    $"Actor '{actorId}' direct-fire surface ID");
                GameplayScenarioAssemblyValidation.Require(
                    surfaceIds.Add(modifier.surfaceId),
                    $"Actor '{actorId}' direct-fire surface '{modifier.surfaceId}' is duplicated.");
                GameplayScenarioAssemblyValidation.RequireFiniteNonNegative(
                    modifier.multiplier,
                    $"Actor '{actorId}' direct-fire surface multiplier");
            }
        }

        private static DirectFireDamageDefinition
            CreateDirectFireDamageDefinition(ScenarioDirectFireDamageData data)
        {
            if (!HasAuthoredDirectFireDamage(data))
                return null;

            var modifiers = new List<SurfaceIntegrityDamageModifier>();
            foreach (ScenarioSurfaceDamageModifierData modifier in
                data.surfaceModifiers
                    ?? new List<ScenarioSurfaceDamageModifierData>())
            {
                modifiers.Add(new SurfaceIntegrityDamageModifier(
                    modifier.surfaceId,
                    modifier.multiplier));
            }

            return new DirectFireDamageDefinition(
                data.damageTypeId,
                data.baseIntegrityDamage,
                modifiers);
        }

        private static bool HasAuthoredDirectFireDamage(
            ScenarioDirectFireDamageData data) =>
            data != null
            && (!string.IsNullOrWhiteSpace(data.damageTypeId)
                || data.baseIntegrityDamage != 0f
                || (data.surfaceModifiers?.Count ?? 0) > 0);
    }
}
