using System;
using System.Collections.Generic;
using System.Linq;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Levels;
using GritGud.Presentation.Levels;
using GritGud.Presentation.Levels.Runtime;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    [Serializable]
    internal sealed class GameplayContentManifestDocument
    {
        public const int CurrentSchemaVersion = 1;

        public int schemaVersion = CurrentSchemaVersion;
        public string scenarioResource = string.Empty;
        public string levelResource = string.Empty;
    }

    internal sealed class GameplayContentPackage
    {
        public GameplayContentPackage(
            GameplayContentManifestDocument manifest,
            ScenarioContentDocument scenario,
            LevelDocument level,
            LevelArchetypeCatalog archetypes,
            ActorPresentationCatalog actorPresentations,
            GameplayScenarioAssembly assembly,
            bool isSandbox = false)
        {
            Manifest = manifest;
            Scenario = scenario;
            Level = level;
            Archetypes = archetypes;
            ActorPresentations = actorPresentations;
            Assembly = assembly;
            ValidationContent = new LevelValidationContent(
                archetypes.CreateKnownIdSet(),
                scenario.actors
                    .Where(actor => actor != null && !string.IsNullOrWhiteSpace(actor.id))
                    .Select(actor => new KeyValuePair<string, string>(
                        actor.id,
                        actor.presentationId)),
                actorPresentations.CreateKnownIdSet());
            IsSandbox = isSandbox;
        }

        public GameplayContentManifestDocument Manifest { get; }

        public ScenarioContentDocument Scenario { get; }

        public LevelDocument Level { get; }

        public LevelArchetypeCatalog Archetypes { get; }

        public ActorPresentationCatalog ActorPresentations { get; }

        public GameplayScenarioAssembly Assembly { get; }

        public LevelValidationContent ValidationContent { get; }

        public bool IsSandbox { get; }
    }

    internal static class GameplayContentLoader
    {
        internal const string DefaultManifestResource =
            "Gameplay/gameplay-content-manifest";

        public static GameplayContentPackage LoadDefault()
        {
            TextAsset manifestAsset = Resources.Load<TextAsset>(
                DefaultManifestResource);
            if (manifestAsset == null)
            {
                throw new InvalidOperationException(
                    $"Gameplay content manifest '{DefaultManifestResource}' was not found.");
            }

            GameplayContentManifestDocument manifest =
                JsonUtility.FromJson<GameplayContentManifestDocument>(
                    manifestAsset.text);
            if (manifest == null
                || manifest.schemaVersion !=
                    GameplayContentManifestDocument.CurrentSchemaVersion)
            {
                throw new InvalidOperationException(
                    "The gameplay content manifest has an unsupported schema.");
            }

            TextAsset scenarioAsset = LoadRequiredText(
                manifest.scenarioResource,
                "scenario");
            ScenarioContentDocument scenario =
                JsonUtility.FromJson<ScenarioContentDocument>(scenarioAsset.text)
                ?? throw new InvalidOperationException(
                    $"Scenario resource '{manifest.scenarioResource}' is invalid JSON.");
            TextAsset levelAsset = LoadRequiredText(
                manifest.levelResource,
                "level");
            LevelDocument level = new UnityLevelJsonSerializer().Deserialize(
                levelAsset.text);
            return CreatePackage(manifest, scenario, level);
        }

        public static GameplayContentPackage LoadSandbox(LevelDocument source)
        {
            return LoadAuthored(source, isSandbox: true);
        }

        public static GameplayContentPackage LoadCommitted(LevelDocument source)
        {
            return LoadAuthored(source, isSandbox: false);
        }

        private static GameplayContentPackage LoadAuthored(
            LevelDocument source,
            bool isSandbox)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            LevelDocument level = source.DeepCopy();
            GameplayContentPackage defaults = LoadDefault();
            ScenarioContentDocument scenario = CreateAuthoredScenario(
                level,
                defaults.Scenario,
                isSandbox);
            return CreatePackage(
                new GameplayContentManifestDocument(),
                scenario,
                level,
                isSandbox);
        }

        private static ScenarioContentDocument CreateAuthoredScenario(
            LevelDocument level,
            ScenarioContentDocument templateSource,
            bool isSandbox)
        {
            LevelScenarioData authored = level.scenario
                ?? throw new InvalidOperationException(
                    "The level does not define authored scenario data.");
            var templates = templateSource.actors
                .Where(actor => actor != null && !string.IsNullOrWhiteSpace(actor.id))
                .GroupBy(actor => actor.id, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            var scenario = new ScenarioContentDocument
            {
                schemaVersion = ScenarioContentDocument.CurrentSchemaVersion,
                scenarioId = (isSandbox ? "playtest-" : "committed-") + level.levelId,
                displayName = isSandbox
                    ? "Playtest: " + level.displayName
                    : level.displayName,
                levelId = level.levelId,
                randomSeed = authored.randomSeed,
                timing = new ScenarioTimingData
                {
                    minimumVoluntaryTurnSeconds = authored.minimumVoluntaryTurnSeconds,
                },
            };

            foreach (LevelScenarioActorData instance in authored.actors)
            {
                if (instance == null || !templates.TryGetValue(
                        instance.templateId ?? string.Empty,
                        out ScenarioActorContentData template))
                {
                    throw new InvalidOperationException(
                        $"Scenario actor '{instance?.id}' references unavailable template "
                        + $"'{instance?.templateId}'.");
                }

                ScenarioActorContentData actor = Clone(template);
                actor.id = instance.id;
                actor.position = instance.transform.position;
                actor.facingDegrees = instance.transform.yawDegrees;
                scenario.actors.Add(actor);
                if (instance.playerControlled)
                    scenario.playerParty.actorIds.Add(instance.id);
                if (instance.initiallySelected)
                    scenario.playerParty.initiallySelectedActorId = instance.id;
                if (instance.primaryTarget)
                    scenario.primaryTargetActorId = instance.id;
            }

            foreach (LevelScenarioObjectiveData authoredObjective in authored.objectives)
            {
                LevelEntity entity = level.entities.FirstOrDefault(candidate =>
                    string.Equals(candidate?.id, authoredObjective?.entityId, StringComparison.Ordinal));
                InteractionPointData point = entity?.interactionPoints.FirstOrDefault(candidate =>
                    string.Equals(
                        candidate?.id,
                        authoredObjective?.interactionPointId,
                        StringComparison.Ordinal));
                if (authoredObjective == null || point == null)
                {
                    throw new InvalidOperationException(
                        $"Scenario objective '{authoredObjective?.id}' does not resolve to an interaction point.");
                }

                scenario.objectives.Add(new ScenarioObjectiveContentData
                {
                    id = authoredObjective.id,
                    levelInteractionPointId = authoredObjective.interactionPointId,
                    levelInteractionPointType = point.type,
                    actionId = authoredObjective.actionId,
                    displayName = authoredObjective.displayName,
                    activeHudText = authoredObjective.activeHudText,
                    completedHudText = authoredObjective.completedHudText,
                    turnCost = new ScenarioActionCostData
                    {
                        actionPoints = authoredObjective.actionPointCost,
                        movementOpportunity =
                            authoredObjective.movementOpportunityCost,
                        mobility = authoredObjective.mobility,
                    },
                });
            }

            if (scenario.objectives.Count > 0)
                scenario.primaryObjectiveId = scenario.objectives[0].id;

            foreach (LevelScenarioPropData prop in authored.props)
            {
                scenario.props.Add(new ScenarioPropContentData
                {
                    entityId = prop.entityId,
                    mass = prop.mass,
                    sizeClass = prop.sizeClass,
                    attackResponse = new ScenarioAttackResponseData
                    {
                        startsEncounter = prop.startsEncounterOnAttack,
                    },
                });
            }

            foreach (LevelScenarioVehicleData vehicle in authored.vehicles)
            {
                scenario.vehicles.Add(new ScenarioVehicleContentData
                {
                    entityId = vehicle.entityId,
                    maximumSpeed = vehicle.maximumSpeed,
                    accelerationPerTurn = vehicle.accelerationPerTurn,
                    brakingPerTurn = vehicle.brakingPerTurn,
                    lowSpeedTurnDegrees = vehicle.lowSpeedTurnDegrees,
                    highSpeedTurnDegrees = vehicle.highSpeedTurnDegrees,
                    baseTurningRadius = vehicle.baseTurningRadius,
                    speedTurningRadiusFactor = vehicle.speedTurningRadiusFactor,
                    startingSpeed = vehicle.startingSpeed,
                    startingOccupantActorId = vehicle.startingOccupantActorId,
                    attackResponse = new ScenarioAttackResponseData
                    {
                        startsEncounter = vehicle.startsEncounterOnAttack,
                    },
                });
            }

            scenario.Normalize();
            return scenario;
        }

        private static ScenarioActorContentData Clone(ScenarioActorContentData source)
        {
            return JsonUtility.FromJson<ScenarioActorContentData>(
                JsonUtility.ToJson(source));
        }

        private static GameplayContentPackage CreatePackage(
            GameplayContentManifestDocument manifest,
            ScenarioContentDocument scenario,
            LevelDocument level,
            bool isSandbox = false)
        {
            LevelArchetypeCatalog archetypes = LevelArchetypeCatalog.LoadDefault();
            GameplayScenarioAssembly assembly =
                new GameplayScenarioAssembler().Assemble(scenario, level);
            ActorPresentationCatalog actorPresentations =
                ActorPresentationCatalog.LoadDefault();
            foreach (ScenarioActorRuntimeDefinition actor in assembly.Actors)
            {
                _ = actorPresentations.Get(actor.PresentationId);
            }

            return new GameplayContentPackage(
                manifest,
                scenario,
                level,
                archetypes,
                actorPresentations,
                assembly,
                isSandbox);
        }

        private static TextAsset LoadRequiredText(string resource, string label)
        {
            if (string.IsNullOrWhiteSpace(resource))
            {
                throw new InvalidOperationException(
                    $"The gameplay manifest does not define its {label} resource.");
            }

            TextAsset asset = Resources.Load<TextAsset>(resource);
            return asset != null
                ? asset
                : throw new InvalidOperationException(
                    $"Gameplay {label} resource '{resource}' was not found.");
        }
    }
}
