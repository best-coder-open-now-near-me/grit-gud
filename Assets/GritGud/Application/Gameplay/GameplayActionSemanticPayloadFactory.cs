using System;
using GritGud.Domain.Gameplay;

namespace GritGud.Application.Gameplay
{
    /// <summary>
    /// The sole live adapter from an already prepared authoritative action
    /// record to its exact semantic reducer payload. Subject resolution uses
    /// the complete immutable combat root for canonical subjects; stable level
    /// geometry that is intentionally absent from combat state uses the
    /// world-position route.
    /// </summary>
    public static class GameplayActionSemanticPayloadFactory
    {
        public static GameplayTransitionPayload Create(
            ScenarioDefinition scenario,
            GameplayCombatStateSnapshot state,
            GameplayActionRecord action)
        {
            if (scenario == null) throw new ArgumentNullException(nameof(scenario));
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (action == null) throw new ArgumentNullException(nameof(action));
            if (!string.Equals(
                    scenario.Id,
                    state.Session.ScenarioId,
                    StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Action payload content does not match canonical scenario state.");

            ScenarioActorDefinition actorDefinition = scenario.GetActor(
                action.Request.ActorId);
            GameplayActorSnapshot actor = state.Session.GetActor(
                action.Request.ActorId);

            AttackResolvedActionOutcome attack = null;
            WeaponDischargedActionOutcome discharge = null;
            ProjectileLaunchedActionOutcome projectile = null;
            ThrownExplosiveActionOutcome explosive = null;
            DisplacementActionOutcome displacement = null;
            EquipmentChangedActionOutcome equipment = null;
            ObjectiveCompletedActionOutcome interaction = null;
            foreach (GameplayActionOutcome outcome in action.Outcomes)
            {
                switch (outcome)
                {
                    case AttackResolvedActionOutcome value:
                        RequireSingle(attack, nameof(AttackResolvedActionOutcome));
                        attack = value;
                        break;
                    case WeaponDischargedActionOutcome value:
                        RequireSingle(discharge, nameof(WeaponDischargedActionOutcome));
                        discharge = value;
                        break;
                    case ProjectileLaunchedActionOutcome value:
                        RequireSingle(projectile, nameof(ProjectileLaunchedActionOutcome));
                        projectile = value;
                        break;
                    case ThrownExplosiveActionOutcome value:
                        RequireSingle(explosive, nameof(ThrownExplosiveActionOutcome));
                        explosive = value;
                        break;
                    case DisplacementActionOutcome value:
                        RequireSingle(displacement, nameof(DisplacementActionOutcome));
                        displacement = value;
                        break;
                    case EquipmentChangedActionOutcome value:
                        RequireSingle(equipment, nameof(EquipmentChangedActionOutcome));
                        equipment = value;
                        break;
                    case ObjectiveCompletedActionOutcome value:
                        RequireSingle(interaction, nameof(ObjectiveCompletedActionOutcome));
                        interaction = value;
                        break;
                }
            }

            AttackDefinition equippedAttack = GetEquippedAttack(
                actorDefinition,
                actor);
            if (attack != null)
            {
                RequireOnlyOutcome(
                    action,
                    attack,
                    "Actor attacks");
                return new GameplayWeaponTransitionPayload(
                    GameplayCapabilityProfiles.Attack(
                        equippedAttack,
                        GameplaySemanticSubjectKind.Actor),
                    action);
            }

            if (discharge != null)
            {
                RequireOnlyOutcome(
                    action,
                    discharge,
                    "Direct-fire discharges");
                GameplaySemanticSubjectKind subject = ResolveSubjectKind(
                    state,
                    discharge.Discharge.TargetId);
                return new GameplayWeaponTransitionPayload(
                    GameplayCapabilityProfiles.Attack(equippedAttack, subject),
                    action);
            }

            if (projectile != null)
            {
                RequireOnlyOutcome(
                    action,
                    projectile,
                    "Projectile launches");
                GameplaySemanticSubjectKind subject = ResolveSubjectKind(
                    state,
                    projectile.Launch.IntendedTargetId);
                return new GameplayWeaponTransitionPayload(
                    GameplayCapabilityProfiles.Attack(equippedAttack, subject),
                    action);
            }

            if (explosive != null)
            {
                if (displacement != null || interaction != null
                    || attack != null || discharge != null || projectile != null)
                    throw new InvalidOperationException(
                        "Thrown explosive actions cannot contain another primary semantic outcome.");
                return new GameplayResolvedActionTransitionPayload(
                    GameplayCapabilityProfiles.ThrowExplosive(
                        explosive.Record.Definition),
                    action);
            }

            if (displacement != null)
            {
                if (interaction != null || attack != null || discharge != null
                    || projectile != null || explosive != null)
                    throw new InvalidOperationException(
                        "Displacement actions cannot contain another primary semantic outcome.");
                DisplacementActionDefinition definition =
                    actorDefinition.GetDisplacementAction(
                        action.Request.ActionId)
                    ?? throw new InvalidOperationException(
                        $"Actor '{actor.ActorId}' has no displacement action '{action.Request.ActionId}'.");
                GameplaySemanticSubjectKind subject =
                    displacement.Displacement.Request.SubjectKind
                        == DisplacementSubjectKind.Prop
                            ? GameplaySemanticSubjectKind.DestructibleProp
                            : GameplaySemanticSubjectKind.Actor;
                return new GameplayResolvedActionTransitionPayload(
                    GameplayCapabilityProfiles.Displace(definition, subject),
                    action);
            }

            if (equipment != null)
            {
                RequireOnlyOutcome(
                    action,
                    equipment,
                    "Equipment actions");
                EquipmentEffectSet? effects = null;
                if (equipment.Change.ResultingEquippedItemId != null)
                {
                    InventoryItemDefinition item = actorDefinition
                        .GetInventoryItem(
                            equipment.Change.ResultingEquippedItemId)
                        ?? throw new InvalidOperationException(
                            "Equipment outcome references an unknown resulting item.");
                    effects = item.EquippedEffects;
                }
                return new GameplayResolvedActionTransitionPayload(
                    GameplayCapabilityProfiles.Equip(),
                    action,
                    effects);
            }

            if (interaction != null)
            {
                RequireOnlyOutcome(
                    action,
                    interaction,
                    "Interaction actions");
                return new GameplayResolvedActionTransitionPayload(
                    GameplayCapabilityProfiles.Interact(),
                    action);
            }

            throw new NotSupportedException(
                $"Action '{action.Request.ActionId}' has no registered semantic payload route.");
        }

        private static AttackDefinition GetEquippedAttack(
            ScenarioActorDefinition definition,
            GameplayActorSnapshot actor)
        {
            AttackDefinition attack = definition.Inventory.Count == 0
                ? definition.Attack
                : actor.EquippedItemId == null
                    ? null
                    : definition.GetInventoryItem(actor.EquippedItemId)?.Attack;
            return attack ?? throw new InvalidOperationException(
                $"Actor '{actor.ActorId}' has no equipped attack for this action.");
        }

        internal static GameplaySemanticSubjectKind ResolveSubjectKind(
            GameplayCombatStateSnapshot state,
            string subjectId)
        {
            if (string.Equals(
                    subjectId,
                    GameplayTargetIds.WorldAimPoint,
                    StringComparison.Ordinal))
                return GameplaySemanticSubjectKind.WorldPosition;
            foreach (GameplayActorSnapshot actor in state.Session.Actors)
                if (string.Equals(actor.ActorId, subjectId, StringComparison.Ordinal))
                    return GameplaySemanticSubjectKind.Actor;
            foreach (DestructiblePropSnapshot prop in state.Destructibles)
                if (string.Equals(prop.PropId, subjectId, StringComparison.Ordinal))
                    return GameplaySemanticSubjectKind.DestructibleProp;
            foreach (VehicleMomentumState vehicle in state.Vehicles)
                if (string.Equals(vehicle.VehicleId, subjectId, StringComparison.Ordinal))
                    return GameplaySemanticSubjectKind.Vehicle;
            foreach (DroneSnapshot drone in state.Drones)
                if (string.Equals(drone.DroneId, subjectId, StringComparison.Ordinal))
                    return GameplaySemanticSubjectKind.Vehicle;
            return GameplaySemanticSubjectKind.WorldPosition;
        }

        private static void RequireSingle(object current, string label)
        {
            if (current != null)
                throw new InvalidOperationException(
                    $"An action cannot contain multiple {label} outcomes.");
        }

        private static void RequireOnlyOutcome(
            GameplayActionRecord action,
            GameplayActionOutcome primary,
            string label)
        {
            if (action.Outcomes.Count != 1
                || !ReferenceEquals(action.Outcomes[0], primary))
                throw new InvalidOperationException(
                    $"{label} contain unsupported additional outcomes.");
        }
    }
}
