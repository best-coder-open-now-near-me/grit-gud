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
            IsSandbox = isSandbox;
        }

        public GameplayContentManifestDocument Manifest { get; }

        public ScenarioContentDocument Scenario { get; }

        public LevelDocument Level { get; }

        public LevelArchetypeCatalog Archetypes { get; }

        public ActorPresentationCatalog ActorPresentations { get; }

        public GameplayScenarioAssembly Assembly { get; }
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
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            GameplayContentPackage defaults = LoadDefault();
            ScenarioContentDocument scenario = JsonUtility.FromJson<ScenarioContentDocument>(
                JsonUtility.ToJson(defaults.Scenario));
            LevelDocument level = source.DeepCopy();
            scenario.scenarioId = "playtest-" + level.levelId;
            scenario.displayName = "Playtest: " + level.displayName;
            scenario.levelId = level.levelId;
            scenario.primaryTargetActorId = string.Empty;
            scenario.primaryObjectiveId = string.Empty;
            scenario.objectives.Clear();
            scenario.props.Clear();
            scenario.vehicles.Clear();
            string selectedActorId = scenario.playerParty.initiallySelectedActorId;
            if (string.IsNullOrWhiteSpace(selectedActorId))
            {
                throw new InvalidOperationException(
                    "The default scenario must define an initially selected player actor for test play.");
            }

            scenario.playerParty = new ScenarioPlayerPartyData
            {
                actorIds = new List<string> { selectedActorId },
                initiallySelectedActorId = selectedActorId,
            };
            scenario.actors = scenario.actors
                .Where(actor => actor != null && string.Equals(actor.id, selectedActorId, StringComparison.Ordinal))
                .ToList();
            if (scenario.actors.Count != 1)
            {
                throw new InvalidOperationException(
                    $"The default scenario does not define selected player actor '{selectedActorId}' for test play.");
            }

            Float3Data playerStart = level.playtest.playerStart.position;
            float startX = playerStart.x - ((scenario.actors.Count - 1) * 0.75f);
            float startY = playerStart.y;
            for (int index = 0; index < scenario.actors.Count; index++)
            {
                scenario.actors[index].position = new Float3Data(
                    startX + (index * 1.5f),
                    startY,
                    playerStart.z);
                scenario.actors[index].facingDegrees = level.playtest.playerStart.yawDegrees;
            }

            return CreatePackage(new GameplayContentManifestDocument(), scenario, level, true);
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
