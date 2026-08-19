using System;
using System.Collections.Generic;
using System.Linq;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Levels;
using GritGud.Presentation.Levels;
using GritGud.Presentation.Levels.Runtime;
using UnityEngine;
using GritGud.Presentation.Characters;
using GritGud.Domain.Characters;

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
            CharacterAppearanceCatalog characterAppearances,
            UnityCharacterLibrary characters,
            GameplayScenarioAssembly assembly,
            IReadOnlyDictionary<string, GameplayFractureSpatialProfile>
                fractureSpatialProfiles,
            bool isSandbox = false)
        {
            Manifest = manifest;
            Scenario = scenario;
            Level = level;
            Archetypes = archetypes;
            ActorPresentations = actorPresentations;
            CharacterAppearances = characterAppearances;
            Characters = characters;
            Assembly = assembly;
            FractureSpatialProfiles = fractureSpatialProfiles
                ?? throw new ArgumentNullException(
                    nameof(fractureSpatialProfiles));
            ValidationContent = new LevelValidationContent(
                archetypes.CreateKnownIdSet(),
                scenario.actors
                    .Where(actor => actor != null && !string.IsNullOrWhiteSpace(actor.id))
                    .Select(actor => new KeyValuePair<string, string>(
                        actor.id,
                        actor.presentationId)),
                actorPresentations.CreateKnownIdSet(),
                characters.CreateKnownIdSet());
            IsSandbox = isSandbox;
        }

        public GameplayContentManifestDocument Manifest { get; }

        public ScenarioContentDocument Scenario { get; }

        public LevelDocument Level { get; }

        public LevelArchetypeCatalog Archetypes { get; }

        public ActorPresentationCatalog ActorPresentations { get; }

        public CharacterAppearanceCatalog CharacterAppearances { get; }

        public UnityCharacterLibrary Characters { get; }

        public GameplayScenarioAssembly Assembly { get; }

        public IReadOnlyDictionary<string, GameplayFractureSpatialProfile>
            FractureSpatialProfiles { get; }

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
                actor.characterId = instance.characterId;
                actor.position = instance.transform.position;
                actor.facingDegrees = instance.transform.yawDegrees;
                if (actor.combat?.enemyBehavior != null)
                {
                    actor.combat.enemyBehavior.reinforcementActorIds =
                        new List<string>(instance.reinforcementActorIds
                            ?? new List<string>());
                }
                else if ((instance.reinforcementActorIds?.Count ?? 0) > 0)
                {
                    throw new InvalidOperationException(
                        $"Scenario actor '{instance.id}' authors reinforcements "
                        + "but its template has no enemy behavior.");
                }
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
                    toppling = new ScenarioPropTopplingData
                    {
                        enabled = prop.toppling != null
                            && prop.toppling.enabled,
                        pitchOffsetDegrees = prop.toppling?.pitchOffsetDegrees
                            ?? 0f,
                        rollOffsetDegrees = prop.toppling?.rollOffsetDegrees
                            ?? 90f,
                        elevationOffset = prop.toppling?.elevationOffset
                            ?? 0f,
                    },
                    pinning = new ScenarioPropPinningData
                    {
                        enabled = prop.pinning != null
                            && prop.pinning.enabled,
                        maximumActorMass = prop.pinning?.maximumActorMass
                            ?? 0f,
                        minimumContactDepth = prop.pinning?.minimumContactDepth
                            ?? 0f,
                    },
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
            ActorPresentationCatalog actorPresentations =
                ActorPresentationCatalog.LoadDefault();
            CharacterAppearanceCatalog characterAppearances =
                CharacterAppearanceCatalog.LoadDefault();
            UnityCharacterLibrary characters = UnityCharacterLibrary.LoadDefault(
                characterAppearances);
            ApplyCharacterAuthoring(scenario, characters);
            GameplayScenarioAssembly assembly =
                new GameplayScenarioAssembler().Assemble(scenario, level);
            IReadOnlyDictionary<string, GameplayFractureSpatialProfile>
                fractureSpatialProfiles =
                    GameplaySpatialContentAssembler.AssembleFractureProfiles(
                        level,
                        archetypes);
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
                characterAppearances,
                characters,
                assembly,
                fractureSpatialProfiles,
                isSandbox);
        }

        private static void ApplyCharacterAuthoring(
            ScenarioContentDocument scenario,
            UnityCharacterLibrary characters)
        {
            foreach (ScenarioActorContentData actor in scenario.actors)
            {
                if (actor == null || string.IsNullOrWhiteSpace(actor.characterId))
                    continue;
                CharacterDocument character = characters.Find(actor.characterId)?.CreateSnapshot()
                    ?? throw new InvalidOperationException(
                        $"Character '{actor.characterId}' is unavailable.");
                actor.displayName = character.displayName;
                actor.characterProfile = CreateCharacterProfile(character);
                ApplyStartingLoadout(actor, character.startingLoadout);
            }
        }

        private static ScenarioCharacterProfileData CreateCharacterProfile(
            CharacterDocument character)
        {
            var result = new ScenarioCharacterProfileData
            {
                identityId = character.characterId,
                displayName = character.displayName,
                archetype = character.build.archetype,
            };
            foreach (CharacterRatingData attribute in character.build.attributes)
            {
                result.attributes.Add(new ScenarioCharacterRatingData
                {
                    id = attribute.id,
                    rating = attribute.rating,
                });
            }
            foreach (CharacterRatingData skill in character.build.skills)
            {
                result.skills.Add(new ScenarioCharacterRatingData
                {
                    id = skill.id,
                    rating = skill.rating,
                });
            }
            result.talentIds.AddRange(character.build.talentIds);
            return result;
        }

        private static void ApplyStartingLoadout(
            ScenarioActorContentData actor,
            CharacterLoadoutData loadout)
        {
            if (loadout == null || loadout.items.Count == 0)
                return;
            Dictionary<string, ScenarioInventoryItemData> catalog = actor.inventory
                .Where(item => item != null && !string.IsNullOrWhiteSpace(item.id))
                .ToDictionary(item => item.id, StringComparer.Ordinal);
            var selected = new List<ScenarioInventoryItemData>();
            foreach (CharacterLoadoutItemData authored in loadout.items)
            {
                if (!catalog.TryGetValue(authored.itemId, out ScenarioInventoryItemData definition))
                {
                    throw new InvalidOperationException(
                        $"Character '{actor.characterId}' starting item '{authored.itemId}' "
                        + $"is unavailable to actor template '{actor.id}'.");
                }
                ScenarioInventoryItemData item = JsonUtility.FromJson<ScenarioInventoryItemData>(
                    JsonUtility.ToJson(definition));
                item.quantity = authored.quantity;
                item.hotbarSlot = authored.hotbarSlot;
                selected.Add(item);
            }
            actor.inventory = selected;
            actor.initiallyEquippedItemId = loadout.initiallyEquippedItemId;
            actor.attackCapability = null;
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
