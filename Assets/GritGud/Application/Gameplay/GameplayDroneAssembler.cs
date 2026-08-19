using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Levels;
using GritGud.Domain.Turns;

namespace GritGud.Application.Gameplay
{
    internal static class GameplayDroneAssembler
    {
        internal static Dictionary<string, DroneDefinition> Index(
            IReadOnlyList<ScenarioDroneContentData> drones,
            LevelDocument level,
            IReadOnlyDictionary<string, ScenarioActorContentData> actors)
        {
            var entities = new Dictionary<string, LevelEntity>(
                StringComparer.Ordinal);
            foreach (LevelEntity entity in level.entities)
                entities.Add(entity.id, entity);
            var result = new Dictionary<string, DroneDefinition>(
                StringComparer.Ordinal);
            foreach (ScenarioDroneContentData drone in drones
                ?? Array.Empty<ScenarioDroneContentData>())
            {
                Require(drone != null, "Scenario drones cannot contain null entries.");
                RequireText(drone.entityId, "Scenario drone entity ID");
                RequireText(drone.controllerActorId, "Scenario drone controller actor ID");
                Require(entities.TryGetValue(drone.entityId, out LevelEntity entity),
                    $"Drone '{drone.entityId}' is missing from level '{level.levelId}'.");
                Require(actors.ContainsKey(drone.controllerActorId),
                    $"Drone '{drone.entityId}' references undefined controller '{drone.controllerActorId}'.");
                Require(drone.attackCapability?.enabled == true,
                    $"Drone '{drone.entityId}' requires an enabled attack capability.");
                ScenarioActionCostData cost = drone.moveCost
                    ?? throw new InvalidOperationException(
                        $"Drone '{drone.entityId}' requires a movement cost.");
                Float3Data position = entity.transform.position;
                var definition = new DroneDefinition(
                    drone.entityId,
                    drone.controllerActorId,
                    new GameplayPosition(position.x, position.y, position.z),
                    entity.transform.yawDegrees,
                    drone.maximumIntegrity,
                    drone.maximumMoveDistance,
                    new ActionCost(
                        cost.actionPoints,
                        cost.movementOpportunity,
                        GameplayScenarioAssemblyValidation.ParseMobility(
                            cost.mobility)),
                    new DroneSensorDefinition(
                        drone.sensorRange,
                        drone.sensorViewAngleDegrees),
                    GameplayActorCombatAssembler.CreateAttackDefinition(
                        drone.entityId,
                        drone.attackCapability));
                Require(result.TryAdd(definition.Id, definition),
                    $"Drone '{definition.Id}' is defined more than once.");
            }
            return result;
        }

        private static void RequireText(string value, string label) =>
            GameplayScenarioAssemblyValidation.RequireText(value, label);

        private static void Require(bool condition, string message) =>
            GameplayScenarioAssemblyValidation.Require(condition, message);
    }
}
