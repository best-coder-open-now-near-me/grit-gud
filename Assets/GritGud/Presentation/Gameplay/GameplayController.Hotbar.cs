using System;
using System.Collections.Generic;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;

namespace GritGud.Presentation.Gameplay
{
    public sealed partial class GameplayController
    {
        private static IReadOnlyList<GameplayActorAbilityHotbarDefinition>
            CreateActorAbilityHotbarDefinitions(
                DisplacementAbilityDefinition displacementAbility,
                bool hasControlledDrone)
        {
            var definitions = new List<GameplayActorAbilityHotbarDefinition>
            {
                new GameplayActorAbilityHotbarDefinition(
                    GameplayCoreActorAbilities.StanceId,
                    "Crouch / Stand",
                    GameplayCoreActorAbilities.StanceHotbarSlot),
            };
            if (hasControlledDrone)
            {
                definitions.Add(new GameplayActorAbilityHotbarDefinition(
                    GameplayDroneController.AbilityId,
                    "Scout Drone",
                    GameplayDroneController.HotbarSlot,
                    new[]
                    {
                        new GameplayActorAbilityOptionDefinition(
                            GameplayDroneController.MoveOptionId,
                            "Move Drone"),
                        new GameplayActorAbilityOptionDefinition(
                            GameplayDroneController.AttackOptionId,
                            "Drone Attack"),
                    }));
            }
            if (displacementAbility == null) return definitions;
            var options = new List<GameplayActorAbilityOptionDefinition>(
                displacementAbility.Actions.Count);
            foreach (DisplacementActionDefinition action in
                displacementAbility.Actions)
                options.Add(new GameplayActorAbilityOptionDefinition(
                    action.Id,
                    action.DisplayName));
            definitions.Add(new GameplayActorAbilityHotbarDefinition(
                displacementAbility.Id,
                displacementAbility.DisplayName,
                displacementAbility.HotbarSlot,
                options));
            return definitions;
        }

        private bool HasControlledDrone(string actorId)
        {
            if (scenarioAssembly == null) return false;
            foreach (DroneDefinition drone in scenarioAssembly.Drones)
                if (string.Equals(
                    drone.ControllerActorId,
                    actorId,
                    StringComparison.Ordinal)) return true;
            return false;
        }
    }
}
