using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Levels;
using GritGud.Domain.Turns;

namespace GritGud.Application.Gameplay
{
    public sealed class GameplayScenarioAssembler
    {
        public GameplayScenarioAssembly Assemble(
            ScenarioContentDocument content,
            LevelDocument level)
        {
            if (content == null)
            {
                throw new ArgumentNullException(nameof(content));
            }

            if (level == null)
            {
                throw new ArgumentNullException(nameof(level));
            }

            ScenarioContentMigrator.Migrate(content);
            Require(
                content.schemaVersion == ScenarioContentDocument.CurrentSchemaVersion,
                $"Scenario schema {content.schemaVersion} is unsupported; expected "
                + $"{ScenarioContentDocument.CurrentSchemaVersion}.");
            RequireText(content.scenarioId, "Scenario ID");
            RequireText(content.displayName, "Scenario display name");
            RequireText(content.levelId, "Scenario level ID");
            RequireFinitePositive(
                content.timing.minimumVoluntaryTurnSeconds,
                "Minimum voluntary turn duration");
            Require(content.timing.startingActionPoints >= 0,
                "Starting action points cannot be negative.");
            Require(content.timing.actionPointIncome >= 0,
                "Action-point income cannot be negative.");
            Require(content.timing.maximumHeldActionPoints
                    >= Math.Max(
                        content.timing.actionPointIncome,
                        content.timing.startingActionPoints),
                "Maximum held action points cannot be below starting AP or income.");
            Require(
                string.Equals(content.levelId, level.levelId, StringComparison.Ordinal),
                $"Scenario '{content.scenarioId}' requires level '{content.levelId}', "
                + $"not '{level.levelId}'.");

            Dictionary<string, ScenarioActorContentData> actorIndex =
                GameplayActorAssembler.IndexActors(content.actors);
            PlayerPartyDefinition playerParty =
                GameplayActorAssembler.CreatePlayerParty(
                    content.playerParty,
                    actorIndex);
            if (!string.IsNullOrWhiteSpace(content.primaryTargetActorId))
            {
                Require(
                    actorIndex.ContainsKey(content.primaryTargetActorId),
                    $"Primary target actor '{content.primaryTargetActorId}' is not defined.");
                Require(
                    !playerParty.Contains(content.primaryTargetActorId),
                    "A player party actor cannot also be the primary target.");
            }

            var actorDefinitions = new List<ScenarioActorDefinition>(
                actorIndex.Count);
            var actorRuntimeDefinitions =
                new Dictionary<string, ScenarioActorRuntimeDefinition>(
                    actorIndex.Count,
                    StringComparer.Ordinal);
            foreach (ScenarioActorContentData actor in content.actors)
            {
                ScenarioActorDefinition gameplayDefinition =
                    GameplayActorAssembler.CreateActorDefinition(actor);
                Require(
                    gameplayDefinition.StartingTurnBudget.ActionPoints
                        == content.timing.startingActionPoints,
                    $"Actor '{actor.id}' starting AP must equal scenario economy starting AP {content.timing.startingActionPoints}.");
                actorDefinitions.Add(gameplayDefinition);
                actorRuntimeDefinitions.Add(
                    actor.id,
                    new ScenarioActorRuntimeDefinition(
                        actor.displayName,
                        actor.presentationId,
                        actor.characterId,
                        actor.targetable,
                        actor.mass,
                        gameplayDefinition,
                        GameplayActorAssembler.CreateControlProfile(actor)));
            }

            var objectiveDefinitions = new List<ScenarioObjectiveDefinition>();
            var objectiveRuntimeDefinitions =
                new Dictionary<string, ScenarioObjectiveRuntimeDefinition>(
                    StringComparer.Ordinal);
            foreach (ScenarioObjectiveContentData objective in content.objectives)
            {
                Require(objective != null, "Scenario objectives cannot contain null entries.");
                RequireText(objective.id, "Objective ID");
                Require(
                    objectiveRuntimeDefinitions.TryAdd(
                        objective.id,
                        new ScenarioObjectiveRuntimeDefinition(
                            objective.id,
                            objective.activeHudText,
                            objective.completedHudText)),
                    $"Objective '{objective.id}' is defined more than once.");
                objectiveDefinitions.Add(
                    GameplayObjectiveAssembler.Create(level, objective));
            }

            if (!string.IsNullOrWhiteSpace(content.primaryObjectiveId))
            {
                Require(
                    objectiveRuntimeDefinitions.ContainsKey(
                        content.primaryObjectiveId),
                    $"Primary objective '{content.primaryObjectiveId}' is not defined.");
            }

            Dictionary<string, ScenarioPropContentData> propIndex =
                GameplayPropAssembler.Index(content.props, level);
            Dictionary<string, ScenarioVehicleRuntimeDefinition> vehicleIndex =
                GameplayVehicleAssembler.Index(
                    content.vehicles,
                    level,
                    actorIndex);
            GameplayDroneAssemblyResult droneAssembly =
                GameplayDroneAssembler.Assemble(
                    content.droneArchetypes,
                    content.droneSummonAbilities,
                    actorIndex);
            var scenario = new ScenarioDefinition(
                content.scenarioId,
                new ScenarioTimingDefinition(
                    content.timing.minimumVoluntaryTurnSeconds,
                    content.timing.startingActionPoints,
                    content.timing.actionPointIncome,
                    content.timing.maximumHeldActionPoints),
                actorDefinitions,
                objectiveDefinitions,
                GameplayAttackResponseAssembler.Create(
                    content.actors,
                    content.props,
                    content.vehicles),
                playerParty);
            IReadOnlyList<TacticalContextRuleDefinition> tacticalRules =
                GameplayTacticalRuleAssembler.Create(
                    content.tacticalRules,
                    scenario,
                    level);
            var assembly = new GameplayScenarioAssembly(
                content.displayName,
                content.primaryTargetActorId,
                content.primaryObjectiveId,
                content.randomSeed,
                scenario,
                actorRuntimeDefinitions,
                objectiveRuntimeDefinitions,
                vehicleIndex,
                GameplayDisplacementAssembler.CreateSubjects(
                    actorIndex,
                    propIndex),
                tacticalRules,
                droneAssembly.Archetypes,
                droneAssembly.Abilities);
            GameplayCapabilityCoverageGate.RequireCurrent(assembly, level);
            return assembly;
        }

        private static void RequireText(string value, string label) =>
            GameplayScenarioAssemblyValidation.RequireText(value, label);

        private static void RequireFinitePositive(float value, string label) =>
            GameplayScenarioAssemblyValidation.RequireFinitePositive(
                value,
                label);

        private static void Require(bool condition, string message) =>
            GameplayScenarioAssemblyValidation.Require(condition, message);
    }
}
