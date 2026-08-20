using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;

namespace GritGud.Application.Gameplay
{
    public sealed class GameplayDroneSession
    {
        private readonly GameplaySession gameplay;
        private readonly DestructiblePropSession destructibles;
        private readonly Dictionary<string, DroneSnapshot> drones =
            new Dictionary<string, DroneSnapshot>(StringComparer.Ordinal);
        private bool canonicalProjectionBound;

        public GameplayDroneSession(
            GameplaySession gameplaySession,
            IEnumerable<DroneDefinition> definitions,
            DestructiblePropSession destructibleSession = null)
        {
            gameplay = gameplaySession ?? throw new ArgumentNullException(
                nameof(gameplaySession));
            destructibles = destructibleSession;
            foreach (DroneDefinition definition in definitions
                ?? throw new ArgumentNullException(nameof(definitions)))
            {
                if (definition == null || !drones.TryAdd(
                    definition.Id,
                    definition.CreateInitialSnapshot()))
                    throw new ArgumentException(
                        "Drone definitions must be non-null and unique.",
                        nameof(definitions));
                _ = gameplay.GetActor(definition.ControllerActorId);
            }
        }

        public IReadOnlyList<DroneSnapshot> CaptureDrones()
        {
            var result = new List<DroneSnapshot>(drones.Values);
            result.Sort((left, right) => StringComparer.Ordinal.Compare(
                left.DroneId, right.DroneId));
            return result.AsReadOnly();
        }

        internal void BindCanonicalProjection(
            IReadOnlyList<DroneSnapshot> snapshots)
        {
            if (canonicalProjectionBound)
                throw new InvalidOperationException(
                    "Drones already have a canonical runtime projection.");
            ValidateCanonicalProjection(snapshots);
            foreach (DroneSnapshot snapshot in snapshots)
                if (!StatesMatch(drones[snapshot.DroneId], snapshot))
                    throw new InvalidOperationException(
                        "Drone session does not match the initial canonical state.");
            canonicalProjectionBound = true;
        }

        internal void ValidateCanonicalProjection(
            IReadOnlyList<DroneSnapshot> snapshots)
        {
            if (snapshots == null)
                throw new ArgumentNullException(nameof(snapshots));
            if (snapshots.Count != drones.Count)
                throw new InvalidOperationException(
                    "Canonical projection changed the drone set.");
            foreach (DroneSnapshot snapshot in snapshots)
            {
                if (!drones.TryGetValue(
                        snapshot.DroneId,
                        out DroneSnapshot current)
                    || !string.Equals(
                        GameplayCanonicalValueDigest.Calculate(
                            current.Definition),
                        GameplayCanonicalValueDigest.Calculate(
                            snapshot.Definition),
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Canonical drone '{snapshot.DroneId}' changed its definition.");
                }
            }
        }

        internal void InstallCanonicalProjection(
            IReadOnlyList<DroneSnapshot> snapshots)
        {
            if (!canonicalProjectionBound)
                throw new InvalidOperationException(
                    "Drones are not bound to a canonical runtime.");
            ValidateCanonicalProjection(snapshots);
            foreach (DroneSnapshot snapshot in snapshots)
                drones[snapshot.DroneId] = snapshot;
        }

        public DroneSnapshot GetDrone(string droneId) =>
            drones.TryGetValue(droneId ?? string.Empty, out DroneSnapshot drone)
                ? drone
                : throw new KeyNotFoundException(
                    $"Drone '{droneId}' is not active.");

        public DroneMoveRecord PrepareMove(
            string droneId,
            GameplayPosition destination,
            float facingDegrees)
        {
            DroneSnapshot drone = GetDrone(droneId);
            RequireControllerTurn(drone);
            if (!drone.IsOperational)
                throw new InvalidOperationException(
                    "Destroyed drones cannot move.");
            if (drone.Position.DistanceTo(destination)
                > drone.Definition.MaximumMoveDistance)
                throw new InvalidOperationException(
                    "Drone destination exceeds its movement range.");
            TurnBudget previous = gameplay.GetActor(
                drone.Definition.ControllerActorId).TurnBudget;
            return new DroneMoveRecord(
                drone.Definition.ControllerActorId,
                drone.DroneId,
                drone.Position,
                destination,
                facingDegrees,
                drone.Definition.MoveCost,
                previous,
                previous.SpendAction(drone.Definition.MoveCost));
        }

        public void CommitMove(DroneMoveRecord record)
        {
            RequireLegacyMutationAllowed(nameof(CommitMove));
            if (record == null) throw new ArgumentNullException(nameof(record));
            DroneSnapshot drone = GetDrone(record.DroneId);
            RequireControllerTurn(drone);
            gameplay.CommitDroneMoveBudget(record);
            drones[drone.DroneId] = new DroneSnapshot(
                drone.Definition,
                record.Destination,
                record.ResultingFacingDegrees,
                drone.RemainingIntegrity);
        }

        public DroneAttackRecord PrepareActorAttack(
            string droneId,
            AttackResolutionRecord resolution)
        {
            if (resolution == null) throw new ArgumentNullException(
                nameof(resolution));
            DroneSnapshot drone = GetDrone(droneId);
            RequireControllerTurn(drone);
            if (!drone.IsOperational)
                throw new InvalidOperationException(
                    "Destroyed drones cannot attack.");
            if (!string.Equals(
                    resolution.AttackerId,
                    droneId,
                    StringComparison.Ordinal))
                throw new ArgumentException(
                    "Drone attack evidence must originate from the firing drone.",
                    nameof(resolution));
            TurnBudget previous = gameplay.GetActor(
                drone.Definition.ControllerActorId).TurnBudget;
            ActionCost cost = drone.Definition.Attack.TurnCost;
            return new DroneAttackRecord(
                drone.Definition.ControllerActorId,
                drone.DroneId,
                resolution.TargetId,
                GameplaySemanticSubjectKind.Actor.ToString(),
                cost,
                previous,
                previous.SpendAction(cost),
                resolution);
        }

        public void CommitAttack(DroneAttackRecord record)
        {
            RequireLegacyMutationAllowed(nameof(CommitAttack));
            if (record == null) throw new ArgumentNullException(nameof(record));
            DroneSnapshot drone = GetDrone(record.DroneId);
            RequireControllerTurn(drone);
            if (!drone.IsOperational)
                throw new InvalidOperationException(
                    "Destroyed drones cannot attack.");
            switch (record.Consequence)
            {
                case AttackResolutionRecord resolution:
                    gameplay.CommitDroneActorAttack(record, resolution);
                    break;
                case DestructibleDamageRecord damage:
                    if (destructibles == null)
                        throw new InvalidOperationException(
                            "Drone destructible damage requires a bound prop session.");
                    destructibles.ValidateDamage(damage);
                    gameplay.CommitDroneAttackBudget(record);
                    destructibles.CommitDamage(damage);
                    break;
                case DroneIntegrityDamageRecord damage:
                    DroneSnapshot target = GetDrone(damage.DroneId);
                    if (!StatesMatch(target, damage.Previous))
                        throw new InvalidOperationException(
                            "Drone damage starts from stale integrity state.");
                    gameplay.CommitDroneAttackBudget(record);
                    drones[target.DroneId] = damage.Resulting;
                    break;
                default:
                    throw new NotSupportedException(
                        "Drone attack consequence is not live-installable.");
            }
        }

        public void ApplyIntegrityDamage(DroneIntegrityDamageRecord damage)
        {
            RequireLegacyMutationAllowed(nameof(ApplyIntegrityDamage));
            if (damage == null) throw new ArgumentNullException(nameof(damage));
            DroneSnapshot current = GetDrone(damage.DroneId);
            if (!StatesMatch(current, damage.Previous))
                throw new InvalidOperationException(
                    "Drone damage starts from stale integrity state.");
            drones[current.DroneId] = damage.Resulting;
        }

        public ActorDroneAttackRecord PrepareActorAttack(
            string attackerId,
            string droneId,
            DroneExposureSnapshot exposure,
            float distance,
            uint resolutionSeed)
        {
            DroneSnapshot drone = GetDrone(droneId);
            if (!drone.IsOperational)
                throw new InvalidOperationException(
                    "Destroyed drones cannot be attacked again.");
            if (gameplay.Mode != GameplaySessionMode.TurnBased
                || gameplay.Operation != GameplaySessionOperation.None
                || !string.Equals(gameplay.ActiveActorId, attackerId,
                    StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Only the idle active actor can attack a drone.");
            AttackDefinition attack = gameplay.GetEquippedAttack(attackerId)
                ?? throw new InvalidOperationException(
                    "Actor has no equipped attack.");
            return DroneDirectAttackRules.Resolve(
                gameplay.LastActionSequence + 1L,
                resolutionSeed,
                attackerId,
                attack,
                gameplay.GetActor(attackerId).TurnBudget,
                exposure,
                distance,
                drone);
        }

        public void CommitActorAttack(ActorDroneAttackRecord record)
        {
            RequireLegacyMutationAllowed(nameof(CommitActorAttack));
            if (record == null) throw new ArgumentNullException(nameof(record));
            DroneSnapshot drone = GetDrone(record.DroneId);
            if (record.Damage != null
                && !StatesMatch(drone, record.Damage.Previous))
                throw new InvalidOperationException(
                    "Actor-drone attack starts from stale drone state.");
            gameplay.CommitActorDroneAttack(record);
            if (record.Damage != null)
                drones[drone.DroneId] = record.Damage.Resulting;
        }

        private void RequireControllerTurn(DroneSnapshot drone)
        {
            if (gameplay.Mode != GameplaySessionMode.TurnBased
                || gameplay.Operation != GameplaySessionOperation.None
                || !string.Equals(
                    gameplay.ActiveActorId,
                    drone.Definition.ControllerActorId,
                    StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Drone commands require the controller's idle personal turn.");
        }

        private static bool StatesMatch(DroneSnapshot left, DroneSnapshot right) =>
            string.Equals(left.DroneId, right.DroneId, StringComparison.Ordinal)
            && left.Position.DistanceTo(right.Position) == 0f
            && left.FacingDegrees == right.FacingDegrees
            && left.RemainingIntegrity == right.RemainingIntegrity;

        private void RequireLegacyMutationAllowed(string operation)
        {
            if (canonicalProjectionBound)
                throw new InvalidOperationException(
                    $"Legacy drone mutation '{operation}' is disabled while the semantic runtime owns state.");
        }
    }
}
