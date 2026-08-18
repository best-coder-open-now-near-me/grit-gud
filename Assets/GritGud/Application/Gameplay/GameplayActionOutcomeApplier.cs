using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;

namespace GritGud.Application.Gameplay
{
    internal sealed class GameplayActionOutcomeApplier
    {
        private delegate void OutcomeApplier(
            GameplayActorState actingActor,
            GameplayActionOutcome outcome,
            GameplayNotificationBatch notifications,
            Action<string> actorCapabilityChanged,
            Action<EquipmentChangeRecord> equipmentChanged);

        private readonly GameplaySession session;
        private readonly IReadOnlyDictionary<Type, OutcomeApplier> appliers;
        private readonly IReadOnlyCollection<Type> supportedOutcomeTypes;

        public GameplayActionOutcomeApplier(GameplaySession session)
        {
            this.session = session ?? throw new ArgumentNullException(
                nameof(session));
            var registered = new Dictionary<Type, OutcomeApplier>
            {
                [typeof(ObjectiveCompletedActionOutcome)] = ApplyObjective,
                [typeof(AttackResolvedActionOutcome)] = ApplyAttack,
                [typeof(WeaponDischargedActionOutcome)] = ApplyWeaponDischarge,
                [typeof(ProjectileLaunchedActionOutcome)] = ApplyProjectileLaunch,
                [typeof(EquipmentChangedActionOutcome)] = ApplyEquipment,
                [typeof(ThrownExplosiveActionOutcome)] = ApplyThrownExplosive,
                [typeof(InventoryQuantityChangedActionOutcome)] =
                    ApplyInventoryQuantity,
                [typeof(DisplacementActionOutcome)] = ApplyDisplacement,
            };
            appliers = registered;
            supportedOutcomeTypes = Array.AsReadOnly(
                new List<Type>(registered.Keys).ToArray());
        }

        public IReadOnlyCollection<Type> SupportedOutcomeTypes =>
            supportedOutcomeTypes;

        public void Apply(
            GameplayActorState actingActor,
            GameplayActionOutcome outcome,
            GameplayNotificationBatch notifications,
            Action<string> actorCapabilityChanged,
            Action<EquipmentChangeRecord> equipmentChanged)
        {
            if (outcome == null)
                throw new ArgumentNullException(nameof(outcome));
            if (!appliers.TryGetValue(outcome.GetType(), out OutcomeApplier apply))
            {
                throw new InvalidOperationException(
                    $"Unsupported action outcome '{outcome.GetType().Name}'.");
            }

            apply(
                actingActor,
                outcome,
                notifications,
                actorCapabilityChanged,
                equipmentChanged);
        }

        private void ApplyObjective(
            GameplayActorState actingActor,
            GameplayActionOutcome outcome,
            GameplayNotificationBatch notifications,
            Action<string> actorCapabilityChanged,
            Action<EquipmentChangeRecord> equipmentChanged)
        {
            var completed = (ObjectiveCompletedActionOutcome)outcome;
            session.RequireObjective(completed.ObjectiveId).IsCompleted = true;
        }

        private void ApplyAttack(
            GameplayActorState actingActor,
            GameplayActionOutcome outcome,
            GameplayNotificationBatch notifications,
            Action<string> actorCapabilityChanged,
            Action<EquipmentChangeRecord> equipmentChanged)
        {
            var resolved = (AttackResolvedActionOutcome)outcome;
            GameplayActorState target = session.RequireActor(resolved.TargetId);
            actingActor.FaceToward(target.Pose.Position);
            target.ApplyAttack(resolved.Attack);
            notifications.Add(actorCapabilityChanged, resolved.TargetId);
        }

        private static void ApplyWeaponDischarge(
            GameplayActorState actingActor,
            GameplayActionOutcome outcome,
            GameplayNotificationBatch notifications,
            Action<string> actorCapabilityChanged,
            Action<EquipmentChangeRecord> equipmentChanged)
        {
            var discharged = (WeaponDischargedActionOutcome)outcome;
            actingActor.FaceToward(discharged.Discharge.AimPoint);
        }

        private static void ApplyProjectileLaunch(
            GameplayActorState actingActor,
            GameplayActionOutcome outcome,
            GameplayNotificationBatch notifications,
            Action<string> actorCapabilityChanged,
            Action<EquipmentChangeRecord> equipmentChanged)
        {
            var launched = (ProjectileLaunchedActionOutcome)outcome;
            actingActor.FaceToward(launched.Launch.AimPoint);
        }

        private void ApplyEquipment(
            GameplayActorState actingActor,
            GameplayActionOutcome outcome,
            GameplayNotificationBatch notifications,
            Action<string> actorCapabilityChanged,
            Action<EquipmentChangeRecord> equipmentChanged)
        {
            EquipmentChangeRecord change =
                ((EquipmentChangedActionOutcome)outcome).Change;
            GameplayActorState actor = session.RequireActor(change.ActorId);
            InventoryItemDefinition item = change.ResultingEquippedItemId == null
                ? null
                : session.RequireActorDefinition(change.ActorId)
                    .GetInventoryItem(change.ResultingEquippedItemId);
            actor.ApplyEquipment(item);
            notifications.Add(equipmentChanged, change);
        }

        private static void ApplyThrownExplosive(
            GameplayActorState actingActor,
            GameplayActionOutcome outcome,
            GameplayNotificationBatch notifications,
            Action<string> actorCapabilityChanged,
            Action<EquipmentChangeRecord> equipmentChanged)
        {
            var thrown = (ThrownExplosiveActionOutcome)outcome;
            actingActor.FaceToward(thrown.Record.IntendedLanding);
        }

        private void ApplyInventoryQuantity(
            GameplayActorState actingActor,
            GameplayActionOutcome outcome,
            GameplayNotificationBatch notifications,
            Action<string> actorCapabilityChanged,
            Action<EquipmentChangeRecord> equipmentChanged)
        {
            InventoryQuantityChangeRecord change =
                ((InventoryQuantityChangedActionOutcome)outcome).Change;
            session.RequireActor(change.ActorId).ApplyInventoryQuantity(change);
        }

        private static void ApplyDisplacement(
            GameplayActorState actingActor,
            GameplayActionOutcome outcome,
            GameplayNotificationBatch notifications,
            Action<string> actorCapabilityChanged,
            Action<EquipmentChangeRecord> equipmentChanged)
        {
            var displacement = (DisplacementActionOutcome)outcome;
            actingActor.FaceToward(
                displacement.Displacement.PreviousPosition);
        }
    }
}
