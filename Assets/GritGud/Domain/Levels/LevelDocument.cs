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
    public struct FloatColorData
    {
        public FloatColorData(float r, float g, float b, float a = 1f)
        {
            this.r = r;
            this.g = g;
            this.b = b;
            this.a = a;
        }

        public float r;
        public float g;
        public float b;
        public float a;
    }

    [Serializable]
    public struct LevelTransformData
    {
        public LevelTransformData(Float3Data position, float yawDegrees)
        {
            this.position = position;
            this.yawDegrees = yawDegrees;
            pitchDegrees = 0f;
            rollDegrees = 0f;
        }

        public LevelTransformData(
            Float3Data position,
            float pitchDegrees,
            float yawDegrees,
            float rollDegrees)
        {
            this.position = position;
            this.pitchDegrees = pitchDegrees;
            this.yawDegrees = yawDegrees;
            this.rollDegrees = rollDegrees;
        }

        public Float3Data position;
        public float pitchDegrees;
        public float yawDegrees;
        public float rollDegrees;
    }

    [Serializable]
    public sealed class LevelRotationPivotData
    {
        public string mode = "bounds";
        public Float3Data localPosition;

        public LevelRotationPivotData DeepCopy()
        {
            return new LevelRotationPivotData
            {
                mode = mode ?? string.Empty,
                localPosition = localPosition,
            };
        }
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
    public sealed class LevelAtmosphereData
    {
        public FloatColorData ambientSky = new FloatColorData(0.055f, 0.11f, 0.23f);
        public FloatColorData ambientEquator = new FloatColorData(0.028f, 0.075f, 0.17f);
        public FloatColorData ambientGround = new FloatColorData(0.012f, 0.026f, 0.065f);
        public float ambientIntensity = 0.76f;
        public float reflectionIntensity = 0.52f;
        public FloatColorData subtractiveShadow = new FloatColorData(0.01f, 0.02f, 0.052f);
        public bool fogEnabled = true;
        public FloatColorData fogColor = new FloatColorData(0.018f, 0.052f, 0.125f);
        public float fogStartDistance = 16f;
        public float fogEndDistance = 54f;

        public LevelAtmosphereData DeepCopy()
        {
            return new LevelAtmosphereData
            {
                ambientSky = ambientSky,
                ambientEquator = ambientEquator,
                ambientGround = ambientGround,
                ambientIntensity = ambientIntensity,
                reflectionIntensity = reflectionIntensity,
                subtractiveShadow = subtractiveShadow,
                fogEnabled = fogEnabled,
                fogColor = fogColor,
                fogStartDistance = fogStartDistance,
                fogEndDistance = fogEndDistance,
            };
        }
    }

    [Serializable]
    public sealed class LevelDirectionalLightData
    {
        public FloatColorData color = new FloatColorData(0.49f, 0.69f, 1f);
        public float intensity = 0.82f;
        public float bounceIntensity = 0.2f;
        public float shadowStrength = 0.9f;
        public float shadowBias = 0.07f;
        public float shadowNormalBias = 0.38f;
        public Float3Data rotationEuler = new Float3Data(42f, -28f, 0f);

        public LevelDirectionalLightData DeepCopy()
        {
            return new LevelDirectionalLightData
            {
                color = color,
                intensity = intensity,
                bounceIntensity = bounceIntensity,
                shadowStrength = shadowStrength,
                shadowBias = shadowBias,
                shadowNormalBias = shadowNormalBias,
                rotationEuler = rotationEuler,
            };
        }
    }

    [Serializable]
    public sealed class LevelPracticalLightData
    {
        public string id = string.Empty;
        public string displayName = "Practical Light";
        public Float3Data position;
        public Float3Data target;
        public FloatColorData color = new FloatColorData(1f, 0.8f, 0.55f);
        public float intensity = 3f;
        public float range = 14f;
        public float spotAngle = 55f;
        public float innerSpotFraction = 0.58f;
        public float baseHeight;

        public void Normalize()
        {
            id = id ?? string.Empty;
            displayName = displayName ?? string.Empty;
        }

        public LevelPracticalLightData DeepCopy()
        {
            return new LevelPracticalLightData
            {
                id = id ?? string.Empty,
                displayName = displayName ?? string.Empty,
                position = position,
                target = target,
                color = color,
                intensity = intensity,
                range = range,
                spotAngle = spotAngle,
                innerSpotFraction = innerSpotFraction,
                baseHeight = baseHeight,
            };
        }
    }

    [Serializable]
    public sealed class LevelEnvironmentData
    {
        public const int MaximumPracticalLights = 8;

        public string presetId = "depot-night";
        public LevelAtmosphereData atmosphere = new LevelAtmosphereData();
        public LevelDirectionalLightData keyLight = new LevelDirectionalLightData();
        public FloatColorData fixtureHousingColor =
            new FloatColorData(0.025f, 0.055f, 0.1f);
        public float lensEmissionIntensity = 5.5f;
        public List<LevelPracticalLightData> practicalLights =
            new List<LevelPracticalLightData>();

        public void Normalize()
        {
            presetId = presetId ?? string.Empty;
            atmosphere = atmosphere ?? new LevelAtmosphereData();
            keyLight = keyLight ?? new LevelDirectionalLightData();
            practicalLights = practicalLights ?? new List<LevelPracticalLightData>();
            foreach (LevelPracticalLightData light in practicalLights)
                light?.Normalize();
        }

        public LevelEnvironmentData DeepCopy()
        {
            var copy = new LevelEnvironmentData
            {
                presetId = presetId ?? string.Empty,
                atmosphere = atmosphere?.DeepCopy() ?? new LevelAtmosphereData(),
                keyLight = keyLight?.DeepCopy() ?? new LevelDirectionalLightData(),
                fixtureHousingColor = fixtureHousingColor,
                lensEmissionIntensity = lensEmissionIntensity,
            };
            if (practicalLights != null)
            {
                foreach (LevelPracticalLightData light in practicalLights)
                    copy.practicalLights.Add(light?.DeepCopy());
            }
            return copy;
        }
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
    public sealed class LevelEntityGroupData
    {
        public string id = string.Empty;
        public string displayName = "Group";
        public bool locked;
        public bool hidden;

        public void Normalize()
        {
            id = id ?? string.Empty;
            displayName = displayName ?? string.Empty;
        }

        public LevelEntityGroupData DeepCopy()
        {
            return new LevelEntityGroupData
            {
                id = id ?? string.Empty,
                displayName = displayName ?? string.Empty,
                locked = locked,
                hidden = hidden,
            };
        }
    }

    [Serializable]
    public sealed class LevelEntity
    {
        public string id = string.Empty;
        public string archetypeId = string.Empty;
        public string groupId = string.Empty;
        public LevelTransformData transform;
        public LevelRotationPivotData rotationPivot;
        public List<CoverVolumeData> coverVolumes = new List<CoverVolumeData>();
        public List<InteractionPointData> interactionPoints = new List<InteractionPointData>();
        public DestructibleInstanceData destructible;

        public void Normalize()
        {
            id = id ?? string.Empty;
            archetypeId = archetypeId ?? string.Empty;
            groupId = groupId ?? string.Empty;
            coverVolumes = coverVolumes ?? new List<CoverVolumeData>();
            interactionPoints = interactionPoints ?? new List<InteractionPointData>();
        }

        public LevelEntity DeepCopy()
        {
            var copy = new LevelEntity
            {
                id = id ?? string.Empty,
                archetypeId = archetypeId ?? string.Empty,
                groupId = groupId ?? string.Empty,
                transform = transform,
                rotationPivot = rotationPivot?.DeepCopy(),
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
    public sealed class TerrainAppearanceData
    {
        public string presetId = "slate";
        public FloatColorData baseColor = new FloatColorData(0.18f, 0.24f, 0.27f);
        public FloatColorData steepColor = new FloatColorData(0.11f, 0.14f, 0.16f);
        public float slopeBlendStartDegrees = 32f;
        public float slopeBlendEndDegrees = 58f;
        public float smoothness = 0.1f;
        public float specularStrength = 0.03f;

        public void Normalize()
        {
            presetId = presetId ?? string.Empty;
        }

        public TerrainAppearanceData DeepCopy()
        {
            return new TerrainAppearanceData
            {
                presetId = presetId ?? string.Empty,
                baseColor = baseColor,
                steepColor = steepColor,
                slopeBlendStartDegrees = slopeBlendStartDegrees,
                slopeBlendEndDegrees = slopeBlendEndDegrees,
                smoothness = smoothness,
                specularStrength = specularStrength,
            };
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
        public TerrainAppearanceData appearance = new TerrainAppearanceData();
        public List<int> heightSamples = new List<int>();

        public void Normalize()
        {
            id = id ?? string.Empty;
            appearance = appearance ?? new TerrainAppearanceData();
            appearance.Normalize();
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
                appearance = appearance?.DeepCopy() ?? new TerrainAppearanceData(),
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
        public const int CurrentSchemaVersion = 10;
        public const int MaximumEntityGroupCount = 64;

        public int schemaVersion = CurrentSchemaVersion;
        public string levelId = string.Empty;
        public string displayName = string.Empty;
        public LevelBoundsData bounds = new LevelBoundsData(
            new Float3Data(0f, 2.5f, 0f),
            new Float3Data(50f, 10f, 50f));
        public LevelEnvironmentData environment = new LevelEnvironmentData();
        public LevelDressingData dressing = new LevelDressingData();
        public List<LevelEntityGroupData> groups = new List<LevelEntityGroupData>();
        public List<LevelEntity> entities = new List<LevelEntity>();
        public List<TerrainSurfaceData> terrainSurfaces = new List<TerrainSurfaceData>();
        public LevelScenarioData scenario = new LevelScenarioData();

        [NonSerialized]
        public LevelPlaytestData legacyPlaytest;

        public void Normalize()
        {
            levelId = levelId ?? string.Empty;
            displayName = displayName ?? string.Empty;
            environment = environment ?? new LevelEnvironmentData();
            environment.Normalize();
            dressing = dressing ?? new LevelDressingData();
            dressing.Normalize();
            groups = groups ?? new List<LevelEntityGroupData>();
            foreach (LevelEntityGroupData group in groups)
                group?.Normalize();
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
                environment = environment?.DeepCopy() ?? new LevelEnvironmentData(),
                dressing = dressing?.DeepCopy() ?? new LevelDressingData(),
                scenario = scenario?.DeepCopy() ?? new LevelScenarioData(),
                legacyPlaytest = legacyPlaytest?.DeepCopy(),
            };

            if (groups != null)
            {
                foreach (LevelEntityGroupData group in groups)
                    copy.groups.Add(group?.DeepCopy());
            }

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
