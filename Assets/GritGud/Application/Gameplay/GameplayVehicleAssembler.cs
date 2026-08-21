using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Levels;

namespace GritGud.Application.Gameplay
{
    internal static class GameplayVehicleAssembler
    {
        internal static Dictionary<string, ScenarioVehicleRuntimeDefinition> Index(
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
                if (!string.IsNullOrWhiteSpace(vehicle.startingOccupantActorId))
                {
                    Require(
                        actors.ContainsKey(vehicle.startingOccupantActorId),
                        $"Vehicle '{vehicle.entityId}' starts with undefined "
                        + $"actor '{vehicle.startingOccupantActorId}'.");
                }

                var definition = new ScenarioVehicleRuntimeDefinition(
                    vehicle.entityId,
                    CreateProfile(vehicle),
                    vehicle.startingSpeed,
                    vehicle.startingOccupantActorId);
                Require(
                    index.TryAdd(vehicle.entityId, definition),
                    $"Vehicle '{vehicle.entityId}' is defined more than once.");
            }

            return index;
        }

        private static VehicleMomentumProfile CreateProfile(
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

        private static void RequireText(string value, string label) =>
            GameplayScenarioAssemblyValidation.RequireText(value, label);

        private static void Require(bool condition, string message) =>
            GameplayScenarioAssemblyValidation.Require(condition, message);
    }
}
