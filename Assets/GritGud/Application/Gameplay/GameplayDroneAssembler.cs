using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;

namespace GritGud.Application.Gameplay
{
    internal sealed class GameplayDroneAssemblyResult
    {
        public GameplayDroneAssemblyResult(
            Dictionary<string, DroneArchetypeDefinition> archetypes,
            Dictionary<string, ScenarioDroneSummonRuntimeDefinition> abilities)
        {
            Archetypes = archetypes ?? throw new ArgumentNullException(
                nameof(archetypes));
            Abilities = abilities ?? throw new ArgumentNullException(
                nameof(abilities));
        }

        public Dictionary<string, DroneArchetypeDefinition> Archetypes
        {
            get;
        }

        public Dictionary<string, ScenarioDroneSummonRuntimeDefinition>
            Abilities { get; }
    }

    internal static class GameplayDroneAssembler
    {
        internal static GameplayDroneAssemblyResult Assemble(
            IReadOnlyList<ScenarioDroneArchetypeContentData> archetypeContent,
            IReadOnlyList<ScenarioDroneSummonAbilityContentData> abilityContent,
            IReadOnlyDictionary<string, ScenarioActorContentData> actors)
        {
            var archetypes = new Dictionary<string, DroneArchetypeDefinition>(
                StringComparer.Ordinal);
            foreach (ScenarioDroneArchetypeContentData data in archetypeContent
                ?? Array.Empty<ScenarioDroneArchetypeContentData>())
            {
                Require(data != null,
                    "Scenario drone archetypes cannot contain null entries.");
                RequireText(data.archetypeId, "Drone archetype ID");
                RequireText(data.presentationId, "Drone presentation ID");
                Require(data.attackCapability?.enabled == true,
                    $"Drone archetype '{data.archetypeId}' requires an enabled attack capability.");
                ScenarioActionCostData moveCost = data.moveCost
                    ?? throw new InvalidOperationException(
                        $"Drone archetype '{data.archetypeId}' requires a movement cost.");
                ScenarioDroneCrashData crash = data.crash
                    ?? throw new InvalidOperationException(
                        $"Drone archetype '{data.archetypeId}' requires crash behavior.");
                var archetype = new DroneArchetypeDefinition(
                    data.archetypeId,
                    data.maximumIntegrity,
                    data.maximumMoveDistance,
                    CreateCost(moveCost),
                    new DroneSensorDefinition(
                        data.sensorRange,
                        data.sensorViewAngleDegrees),
                    GameplayActorCombatAssembler.CreateAttackDefinition(
                        data.archetypeId,
                        data.attackCapability),
                    data.presentationId,
                    new DroneCrashDefinition(
                        crash.impactRadius,
                        crash.injuryMovementPenalty,
                        crash.destructibleIntegrityDamage,
                        crash.maximumActionPointReduction,
                        crash.maximumDriftDistance,
                        crash.impactPlaybackSeconds));
                Require(archetypes.TryAdd(archetype.ArchetypeId, archetype),
                    $"Drone archetype '{archetype.ArchetypeId}' is defined more than once.");
            }

            var abilities = new Dictionary<
                string,
                ScenarioDroneSummonRuntimeDefinition>(StringComparer.Ordinal);
            foreach (ScenarioDroneSummonAbilityContentData data in abilityContent
                ?? Array.Empty<ScenarioDroneSummonAbilityContentData>())
            {
                Require(data != null,
                    "Scenario drone summon abilities cannot contain null entries.");
                RequireText(data.abilityId, "Drone summon ability ID");
                RequireText(data.summonerActorId, "Drone summoner actor ID");
                RequireText(data.droneArchetypeId, "Summoned drone archetype ID");
                Require(actors.ContainsKey(data.summonerActorId),
                    $"Drone ability '{data.abilityId}' references undefined summoner '{data.summonerActorId}'.");
                Require(archetypes.ContainsKey(data.droneArchetypeId),
                    $"Drone ability '{data.abilityId}' references undefined archetype '{data.droneArchetypeId}'.");
                ScenarioActionCostData summonCost = data.summonCost
                    ?? throw new InvalidOperationException(
                        $"Drone ability '{data.abilityId}' requires a summon cost.");
                var runtime = new ScenarioDroneSummonRuntimeDefinition(
                    data.summonerActorId,
                    new DroneSummonAbilityDefinition(
                        data.abilityId,
                        data.droneArchetypeId,
                        CreateCost(summonCost),
                        data.maximumSpawnDistance,
                        data.maximumActiveInstances,
                        data.durationTurns > 0
                            ? data.durationTurns
                            : (int?)null,
                        data.spawnHeight));
                Require(abilities.TryAdd(runtime.Ability.AbilityId, runtime),
                    $"Drone summon ability '{runtime.Ability.AbilityId}' is defined more than once.");
            }
            return new GameplayDroneAssemblyResult(archetypes, abilities);
        }

        private static ActionCost CreateCost(ScenarioActionCostData data) =>
            new ActionCost(
                data.actionPoints,
                data.movementOpportunity,
                GameplayScenarioAssemblyValidation.ParseMobility(
                    data.mobility));

        private static void RequireText(string value, string label) =>
            GameplayScenarioAssemblyValidation.RequireText(value, label);

        private static void Require(bool condition, string message) =>
            GameplayScenarioAssemblyValidation.Require(condition, message);
    }
}
