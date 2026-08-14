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
            Normalize();
            var copy = new LevelEntity
            {
                id = id,
                archetypeId = archetypeId,
                transform = transform,
                destructible = destructible?.DeepCopy(),
            };

            foreach (CoverVolumeData volume in coverVolumes)
            {
                copy.coverVolumes.Add(volume?.DeepCopy());
            }

            foreach (InteractionPointData point in interactionPoints)
            {
                copy.interactionPoints.Add(point?.DeepCopy());
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
            Normalize();
            return new TerrainSurfaceData
            {
                id = id,
                origin = origin,
                sampleCountX = sampleCountX,
                sampleCountZ = sampleCountZ,
                sampleSpacing = sampleSpacing,
                minimumElevation = minimumElevation,
                elevationIncrement = elevationIncrement,
                heightSamples = new List<int>(heightSamples),
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
    public sealed class LevelDocument
    {
        public const int CurrentSchemaVersion = 3;

        public int schemaVersion = CurrentSchemaVersion;
        public string levelId = string.Empty;
        public string displayName = string.Empty;
        public LevelBoundsData bounds = new LevelBoundsData(
            new Float3Data(0f, 2.5f, 0f),
            new Float3Data(50f, 10f, 50f));
        public List<LevelEntity> entities = new List<LevelEntity>();
        public List<TerrainSurfaceData> terrainSurfaces = new List<TerrainSurfaceData>();
        public LevelPlaytestData playtest = new LevelPlaytestData();

        public void Normalize()
        {
            levelId = levelId ?? string.Empty;
            displayName = displayName ?? string.Empty;
            entities = entities ?? new List<LevelEntity>();
            terrainSurfaces = terrainSurfaces ?? new List<TerrainSurfaceData>();
            playtest = playtest ?? new LevelPlaytestData();

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
            Normalize();
            var copy = new LevelDocument
            {
                schemaVersion = schemaVersion,
                levelId = levelId,
                displayName = displayName,
                bounds = bounds,
                playtest = playtest?.DeepCopy(),
            };

            foreach (LevelEntity entity in entities)
            {
                copy.entities.Add(entity?.DeepCopy());
            }

            foreach (TerrainSurfaceData surface in terrainSurfaces)
            {
                copy.terrainSurfaces.Add(surface?.DeepCopy());
            }

            return copy;
        }
    }
}
