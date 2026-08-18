using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;

namespace GritGud.Application.Gameplay
{
    internal sealed class GameplayCanonicalStateMutation
    {
        private readonly GameplayCombatStateSnapshot source;
        private readonly List<GameplayActorSnapshot> actors;
        private readonly List<GameplayObjectiveSnapshot> objectives;
        private readonly List<DestructiblePropSnapshot> destructibles;
        private readonly List<VehicleMomentumState> vehicles;
        private readonly List<ProjectileFlightSnapshot> projectiles;
        private readonly List<SmokeFieldSnapshot> smokeFields;

        public GameplayCanonicalStateMutation(
            GameplayCombatStateSnapshot canonicalState)
        {
            source = canonicalState ?? throw new ArgumentNullException(
                nameof(canonicalState));
            GameplaySessionStateSnapshot session = source.Session;
            actors = new List<GameplayActorSnapshot>(session.Actors);
            objectives = new List<GameplayObjectiveSnapshot>(session.Objectives);
            destructibles = new List<DestructiblePropSnapshot>(
                source.Destructibles);
            vehicles = new List<VehicleMomentumState>(source.Vehicles);
            projectiles = new List<ProjectileFlightSnapshot>(source.Projectiles);
            smokeFields = new List<SmokeFieldSnapshot>(source.SmokeFields);
            Mode = session.Mode;
            Operation = session.Operation;
            TurnContext = session.TurnContext;
            EncounterActive = session.EncounterActive;
            EncounterCompletionRequested =
                session.EncounterCompletionRequested;
            ActiveActorId = session.ActiveActorId;
            TurnPhase = session.TurnPhase;
            EmergencyResponders = session.EmergencyResponders;
            EmergencyResponderIndex = session.EmergencyResponderIndex;
            EmergencyResumeActorId = session.EmergencyResumeActorId;
            LastActionSequence = session.LastActionSequence;
            LastTurnSequence = session.LastTurnSequence;
            JournalSequence = session.JournalSequence;
            Revision = session.Revision;
            VoluntaryTurnReentrySecondsRemaining =
                session.VoluntaryTurnReentrySecondsRemaining;
            PendingMovementRoute = session.PendingMovementRoute;
            PendingVoluntaryTurnCycle = session.PendingVoluntaryTurnCycle;
            LastTransitionSequence = session.LastTransitionSequence;
            LastVoluntaryTurnCycleSequence =
                session.LastVoluntaryTurnCycleSequence;
        }

        public GameplaySessionMode Mode { get; set; }
        public GameplaySessionOperation Operation { get; set; }
        public TurnModeContext TurnContext { get; set; }
        public bool EncounterActive { get; set; }
        public bool EncounterCompletionRequested { get; set; }
        public string ActiveActorId { get; set; }
        public GameplayTurnPhase TurnPhase { get; set; }
        public IReadOnlyList<string> EmergencyResponders { get; set; }
        public int EmergencyResponderIndex { get; set; }
        public string EmergencyResumeActorId { get; set; }
        public long LastActionSequence { get; set; }
        public long LastTurnSequence { get; set; }
        public long JournalSequence { get; set; }
        public long Revision { get; set; }
        public float VoluntaryTurnReentrySecondsRemaining { get; set; }
        public MovementRouteRecord PendingMovementRoute { get; set; }
        public VoluntaryTurnCycleRecord PendingVoluntaryTurnCycle { get; set; }
        public long LastTransitionSequence { get; set; }
        public long LastVoluntaryTurnCycleSequence { get; set; }

        public GameplayActorSnapshot GetActor(string actorId) =>
            Find(actors, value => value.ActorId, actorId, "actor");

        public GameplayObjectiveSnapshot GetObjective(string objectiveId) =>
            Find(
                objectives,
                value => value.ObjectiveId,
                objectiveId,
                "objective");

        public DestructiblePropSnapshot GetDestructible(string propId) =>
            Find(destructibles, value => value.PropId, propId, "destructible");

        public void ReplaceActor(GameplayActorSnapshot actor) => Replace(
            actors,
            value => value.ActorId,
            actor.ActorId,
            actor,
            "actor");

        public void ReplaceObjective(GameplayObjectiveSnapshot objective) =>
            Replace(
                objectives,
                value => value.ObjectiveId,
                objective.ObjectiveId,
                objective,
                "objective");

        public void ReplaceDestructible(DestructiblePropSnapshot prop) => Replace(
            destructibles,
            value => value.PropId,
            prop.PropId,
            prop,
            "destructible");

        public void ReplaceVehicle(VehicleMomentumState vehicle) => Replace(
            vehicles,
            value => value.VehicleId,
            vehicle.VehicleId,
            vehicle,
            "vehicle");

        public void ReplaceProjectile(ProjectileFlightSnapshot projectile) =>
            Replace(
                projectiles,
                value => value.ProjectileId,
                projectile.ProjectileId,
                projectile,
                "projectile");

        public void AddProjectile(ProjectileFlightSnapshot projectile)
        {
            EnsureMissing(
                projectiles,
                value => value.ProjectileId,
                projectile.ProjectileId,
                "projectile");
            projectiles.Add(projectile);
        }

        public void AddSmokeField(SmokeFieldSnapshot smoke)
        {
            EnsureMissing(
                smokeFields,
                value => value.Field.Id,
                smoke.Field.Id,
                "smoke field");
            smokeFields.Add(smoke);
        }

        public void ReplaceSmokeFields(IEnumerable<SmokeFieldSnapshot> values)
        {
            smokeFields.Clear();
            smokeFields.AddRange(values ?? throw new ArgumentNullException(
                nameof(values)));
        }

        public GameplayCombatStateSnapshot Build()
        {
            GameplaySessionStateSnapshot original = source.Session;
            var session = new GameplaySessionStateSnapshot(
                original.ScenarioId,
                Mode,
                Operation,
                TurnContext,
                EncounterActive,
                EncounterCompletionRequested,
                ActiveActorId,
                TurnPhase,
                actors,
                original.InitiativeOrder,
                objectives,
                EmergencyResponders,
                EmergencyResponderIndex,
                EmergencyResumeActorId,
                LastActionSequence,
                LastTurnSequence,
                JournalSequence,
                original.RunIdentity,
                Revision,
                VoluntaryTurnReentrySecondsRemaining,
                PendingMovementRoute,
                PendingVoluntaryTurnCycle,
                LastTransitionSequence,
                LastVoluntaryTurnCycleSequence);
            return new GameplayCombatStateSnapshot(
                session,
                destructibles,
                vehicles,
                projectiles,
                smokeFields,
                source.Coverage);
        }

        public static GameplayActorSnapshot CopyActor(
            GameplayActorSnapshot actor,
            GameplayActorPose? pose = null,
            GritGud.Domain.Turns.TurnBudget? budget = null,
            ActorWoundSnapshot? wounds = null,
            string equippedItemId = null,
            EquipmentEffectSet? equipmentEffects = null,
            ActorInventorySnapshot inventory = null,
            ActorPinState pinState = null,
            bool replaceEquipment = false,
            bool replacePin = false,
            int? emergencyActionPointAllowance = null) =>
            new GameplayActorSnapshot(
                actor.ActorId,
                pose ?? actor.Pose,
                budget ?? actor.TurnBudget,
                wounds ?? actor.Wounds,
                replaceEquipment ? equippedItemId : actor.EquippedItemId,
                replaceEquipment
                    ? equipmentEffects ?? EquipmentEffectSet.None
                    : actor.EquipmentEffects,
                actor.MaximumWounds,
                inventory ?? actor.Inventory,
                actor.TurnActionPointAllowance,
                actor.TurnMovementAllowance,
                replacePin ? pinState : actor.PinState,
                emergencyActionPointAllowance
                    ?? actor.EmergencyActionPointAllowance);

        private static T Find<T>(
            IList<T> values,
            Func<T, string> getId,
            string id,
            string label)
        {
            for (int index = 0; index < values.Count; index++)
                if (string.Equals(
                    getId(values[index]),
                    id,
                    StringComparison.Ordinal))
                    return values[index];
            throw new KeyNotFoundException(
                $"Canonical {label} '{id}' was not found.");
        }

        private static void Replace<T>(
            IList<T> values,
            Func<T, string> getId,
            string id,
            T replacement,
            string label)
        {
            for (int index = 0; index < values.Count; index++)
            {
                if (!string.Equals(
                    getId(values[index]),
                    id,
                    StringComparison.Ordinal)) continue;
                values[index] = replacement;
                return;
            }
            throw new KeyNotFoundException(
                $"Canonical {label} '{id}' was not found.");
        }

        private static void EnsureMissing<T>(
            IEnumerable<T> values,
            Func<T, string> getId,
            string id,
            string label)
        {
            foreach (T value in values)
                if (string.Equals(getId(value), id, StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        $"Canonical {label} '{id}' already exists.");
        }
    }
}
