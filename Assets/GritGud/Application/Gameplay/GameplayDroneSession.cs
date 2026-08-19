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

        public void CommitAttack(DroneAttackRecord record)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));
            DroneSnapshot drone = GetDrone(record.DroneId);
            RequireControllerTurn(drone);
            if (!drone.IsOperational)
                throw new InvalidOperationException(
                    "Destroyed drones cannot attack.");
            switch (record.Consequence)
            {
                case ActorWoundRecord wound:
                    gameplay.CommitDroneActorAttack(record, wound);
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
            if (damage == null) throw new ArgumentNullException(nameof(damage));
            DroneSnapshot current = GetDrone(damage.DroneId);
            if (!StatesMatch(current, damage.Previous))
                throw new InvalidOperationException(
                    "Drone damage starts from stale integrity state.");
            drones[current.DroneId] = damage.Resulting;
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
    }
}
