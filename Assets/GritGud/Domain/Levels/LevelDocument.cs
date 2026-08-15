using System;
using System.Collections.Generic;

namespace GritGud.Domain.Levels
{
    [Serializable]
    public struct Float3Data
    {
        public Float3Data(float x, float y, float z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public float x;
        public float y;
        public float z;
    }

    [Serializable]
    public struct LevelTransformData
    {
        public LevelTransformData(Float3Data position, float yawDegrees)
        {
            this.position = position;
            this.yawDegrees = yawDegrees;
        }

        public Float3Data position;
        public float yawDegrees;
    }

    [Serializable]
    public struct LevelBoundsData
    {
        public LevelBoundsData(Float3Data center, Float3Data size)
        {
            this.center = center;
            this.size = size;
        }

        public Float3Data center;
        public Float3Data size;
    }

    [Serializable]
    public sealed class CoverVolumeData
    {
        public string id = string.Empty;
        public Float3Data localCenter;
        public Float3Data size = new Float3Data(1f, 1f, 1f);

        public CoverVolumeData DeepCopy()
        {
            return new CoverVolumeData
            {
                id = id,
                localCenter = localCenter,
                size = size,
            };
        }
    }

    [Serializable]
    public sealed class InteractionPointData
    {
        public string id = string.Empty;
        public string type = string.Empty;
        public Float3Data localPosition;
        public float radius = 0.5f;

        public InteractionPointData DeepCopy()
        {
            return new InteractionPointData
            {
                id = id,
                type = type,
                localPosition = localPosition,
                radius = radius,
            };
        }
    }

    [Serializable]
    public sealed class DestructibleInstanceData
    {
        public bool enabled;
        public string initialState = string.Empty;
        public float integrity;

        public DestructibleInstanceData DeepCopy()
        {
            return new DestructibleInstanceData
            {
                enabled = enabled,
                initialState = initialState,
                integrity = integrity,
            };
        }
    }

    [Serializable]
    public sealed class LevelEntity
    {
        public string id = string.Empty;
        public string archetypeId = string.Empty;
        public LevelTransformData transform;
        public List<CoverVolumeData> coverVolumes = new List<CoverVolumeData>();
        public List<InteractionPointData> interactionPoints = new List<InteractionPointData>();
        public DestructibleInstanceData destructible;

        public void Normalize()
        {
            id = id ?? string.Empty;
            archetypeId = archetypeId ?? string.Empty;
            coverVolumes = coverVolumes ?? new List<CoverVolumeData>();
            interactionPoints = interactionPoints ?? new List<InteractionPointData>();
        }

        public LevelEntity DeepCopy()
        {
            var copy = new LevelEntity
            {
                id = id ?? string.Empty,
                archetypeId = archetypeId ?? string.Empty,
                transform = transform,
                destructible = destructible?.DeepCopy(),
            };

            if (coverVolumes != null)
            {
                foreach (CoverVolumeData volume in coverVolumes)
                {
                    copy.coverVolumes.Add(volume?.DeepCopy());
                }
            }

            if (interactionPoints != null)
            {
                foreach (InteractionPointData point in interactionPoints)
                {
                    copy.interactionPoints.Add(point?.DeepCopy());
                }
            }

            return copy;
        }
    }

    [Serializable]
    public sealed class TerrainSurfaceData
    {
        public string id = string.Empty;
        public Float3Data origin;
        public int sampleCountX;
        public int sampleCountZ;
        public float sampleSpacing = 1f;
        public float minimumElevation;
        public float elevationIncrement = 0.01f;
        public List<int> heightSamples = new List<int>();

        public void Normalize()
        {
            id = id ?? string.Empty;
            heightSamples = heightSamples ?? new List<int>();
        }

        public TerrainSurfaceData DeepCopy()
        {
            return new TerrainSurfaceData
            {
                id = id ?? string.Empty,
                origin = origin,
                sampleCountX = sampleCountX,
                sampleCountZ = sampleCountZ,
                sampleSpacing = sampleSpacing,
                minimumElevation = minimumElevation,
                elevationIncrement = elevationIncrement,
                heightSamples = heightSamples != null
                    ? new List<int>(heightSamples)
                    : new List<int>(),
            };
        }
    }

    [Serializable]
    public sealed class LevelPlaytestData
    {
        public LevelTransformData playerStart;

        public LevelPlaytestData DeepCopy()
        {
            return new LevelPlaytestData { playerStart = playerStart };
        }
    }

    [Serializable]
    public sealed class LevelScenarioActorData
    {
        public string id = string.Empty;
        public string templateId = string.Empty;
        public LevelTransformData transform;
        public bool playerControlled;
        public bool initiallySelected;
        public bool primaryTarget;

        public void Normalize()
        {
            id = id ?? string.Empty;
            templateId = templateId ?? string.Empty;
        }

        public LevelScenarioActorData DeepCopy()
        {
            return new LevelScenarioActorData
            {
                id = id ?? string.Empty,
                templateId = templateId ?? string.Empty,
                transform = transform,
                playerControlled = playerControlled,
                initiallySelected = initiallySelected,
                primaryTarget = primaryTarget,
            };
        }
    }

    [Serializable]
    public sealed class LevelScenarioObjectiveData
    {
        public string id = string.Empty;
        public string entityId = string.Empty;
        public string interactionPointId = string.Empty;
        public string actionId = "interact";
        public string displayName = "Objective";
        public string activeHudText = string.Empty;
        public string completedHudText = string.Empty;
        public int actionPointCost = 1;
        public float movementOpportunityCost;
        public string mobility = "set";

        public void Normalize()
        {
            id = id ?? string.Empty;
            entityId = entityId ?? string.Empty;
            interactionPointId = interactionPointId ?? string.Empty;
            actionId = actionId ?? string.Empty;
            displayName = displayName ?? string.Empty;
            activeHudText = activeHudText ?? string.Empty;
            completedHudText = completedHudText ?? string.Empty;
            mobility = mobility ?? string.Empty;
        }

        public LevelScenarioObjectiveData DeepCopy()
        {
            return new LevelScenarioObjectiveData
            {
                id = id ?? string.Empty,
                entityId = entityId ?? string.Empty,
                interactionPointId = interactionPointId ?? string.Empty,
                actionId = actionId ?? string.Empty,
                displayName = displayName ?? string.Empty,
                activeHudText = activeHudText ?? string.Empty,
                completedHudText = completedHudText ?? string.Empty,
                actionPointCost = actionPointCost,
                movementOpportunityCost = movementOpportunityCost,
                mobility = mobility ?? string.Empty,
            };
        }
    }

    [Serializable]
    public sealed class LevelScenarioPropData
    {
        public string entityId = string.Empty;
        public float mass = 25f;
        public string sizeClass = "medium";
        public bool startsEncounterOnAttack;

        public void Normalize()
        {
            entityId = entityId ?? string.Empty;
            sizeClass = sizeClass ?? string.Empty;
        }

        public LevelScenarioPropData DeepCopy()
        {
            return new LevelScenarioPropData
            {
                entityId = entityId ?? string.Empty,
                mass = mass,
                sizeClass = sizeClass ?? string.Empty,
                startsEncounterOnAttack = startsEncounterOnAttack,
            };
        }
    }

    [Serializable]
    public sealed class LevelScenarioVehicleData
    {
        public string entityId = string.Empty;
        public float maximumSpeed = 12f;
        public float accelerationPerTurn = 3f;
        public float brakingPerTurn = 4f;
        public float lowSpeedTurnDegrees = 45f;
        public float highSpeedTurnDegrees = 15f;
        public float baseTurningRadius = 2f;
        public float speedTurningRadiusFactor = 0.25f;
        public float startingSpeed;
        public string startingOccupantActorId = string.Empty;
        public bool startsEncounterOnAttack;

        public void Normalize()
        {
            entityId = entityId ?? string.Empty;
            startingOccupantActorId = startingOccupantActorId ?? string.Empty;
        }

        public LevelScenarioVehicleData DeepCopy()
        {
            return new LevelScenarioVehicleData
            {
                entityId = entityId ?? string.Empty,
                maximumSpeed = maximumSpeed,
                accelerationPerTurn = accelerationPerTurn,
                brakingPerTurn = brakingPerTurn,
                lowSpeedTurnDegrees = lowSpeedTurnDegrees,
                highSpeedTurnDegrees = highSpeedTurnDegrees,
                baseTurningRadius = baseTurningRadius,
                speedTurningRadiusFactor = speedTurningRadiusFactor,
                startingSpeed = startingSpeed,
                startingOccupantActorId = startingOccupantActorId ?? string.Empty,
                startsEncounterOnAttack = startsEncounterOnAttack,
            };
        }
    }

    [Serializable]
    public sealed class LevelScenarioData
    {
        public uint randomSeed = 12648430;
        public float minimumVoluntaryTurnSeconds = 1.25f;
        public List<LevelScenarioActorData> actors = new List<LevelScenarioActorData>();
        public List<LevelScenarioObjectiveData> objectives =
            new List<LevelScenarioObjectiveData>();
        public List<LevelScenarioPropData> props = new List<LevelScenarioPropData>();
        public List<LevelScenarioVehicleData> vehicles =
            new List<LevelScenarioVehicleData>();

        public void Normalize()
        {
            actors = actors ?? new List<LevelScenarioActorData>();
            objectives = objectives ?? new List<LevelScenarioObjectiveData>();
            props = props ?? new List<LevelScenarioPropData>();
            vehicles = vehicles ?? new List<LevelScenarioVehicleData>();

            foreach (LevelScenarioActorData actor in actors)
                actor?.Normalize();
            foreach (LevelScenarioObjectiveData objective in objectives)
                objective?.Normalize();
            foreach (LevelScenarioPropData prop in props)
                prop?.Normalize();
            foreach (LevelScenarioVehicleData vehicle in vehicles)
                vehicle?.Normalize();
        }

        public LevelScenarioData DeepCopy()
        {
            var copy = new LevelScenarioData
            {
                randomSeed = randomSeed,
                minimumVoluntaryTurnSeconds = minimumVoluntaryTurnSeconds,
            };
            if (actors != null)
            {
                foreach (LevelScenarioActorData actor in actors)
                    copy.actors.Add(actor?.DeepCopy());
            }
            if (objectives != null)
            {
                foreach (LevelScenarioObjectiveData objective in objectives)
                    copy.objectives.Add(objective?.DeepCopy());
            }
            if (props != null)
            {
                foreach (LevelScenarioPropData prop in props)
                    copy.props.Add(prop?.DeepCopy());
            }
            if (vehicles != null)
            {
                foreach (LevelScenarioVehicleData vehicle in vehicles)
                    copy.vehicles.Add(vehicle?.DeepCopy());
            }
            return copy;
        }

        public LevelScenarioActorData FindInitiallySelectedPlayer()
        {
            if (actors == null)
            {
                return null;
            }

            foreach (LevelScenarioActorData actor in actors)
            {
                if (actor != null && actor.playerControlled && actor.initiallySelected)
                    return actor;
            }

            return null;
        }
    }

    [Serializable]
    public sealed class LevelDocument
    {
        public const int CurrentSchemaVersion = 5;

        public int schemaVersion = CurrentSchemaVersion;
        public string levelId = string.Empty;
        public string displayName = string.Empty;
        public LevelBoundsData bounds = new LevelBoundsData(
            new Float3Data(0f, 2.5f, 0f),
            new Float3Data(50f, 10f, 50f));
        public List<LevelEntity> entities = new List<LevelEntity>();
        public List<TerrainSurfaceData> terrainSurfaces = new List<TerrainSurfaceData>();
        public LevelScenarioData scenario = new LevelScenarioData();

        [NonSerialized]
        public LevelPlaytestData legacyPlaytest;

        public void Normalize()
        {
            levelId = levelId ?? string.Empty;
            displayName = displayName ?? string.Empty;
            entities = entities ?? new List<LevelEntity>();
            terrainSurfaces = terrainSurfaces ?? new List<TerrainSurfaceData>();
            scenario = scenario ?? new LevelScenarioData();
            scenario.Normalize();

            foreach (LevelEntity entity in entities)
            {
                entity?.Normalize();
            }

            foreach (TerrainSurfaceData surface in terrainSurfaces)
            {
                surface?.Normalize();
            }
        }

        public LevelDocument DeepCopy()
        {
            var copy = new LevelDocument
            {
                schemaVersion = schemaVersion,
                levelId = levelId ?? string.Empty,
                displayName = displayName ?? string.Empty,
                bounds = bounds,
                scenario = scenario?.DeepCopy() ?? new LevelScenarioData(),
                legacyPlaytest = legacyPlaytest?.DeepCopy(),
            };

            if (entities != null)
            {
                foreach (LevelEntity entity in entities)
                {
                    copy.entities.Add(entity?.DeepCopy());
                }
            }

            if (terrainSurfaces != null)
            {
                foreach (TerrainSurfaceData surface in terrainSurfaces)
                {
                    copy.terrainSurfaces.Add(surface?.DeepCopy());
                }
            }

            return copy;
        }
    }
}
