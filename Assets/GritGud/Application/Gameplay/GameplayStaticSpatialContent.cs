using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Levels;

namespace GritGud.Application.Gameplay
{
    [Serializable]
    public sealed class GameplayFractureSpatialChunkData
    {
        public Float3Data center;
        public Float3Data size;

        public GameplayFractureSpatialChunkData DeepCopy() =>
            new GameplayFractureSpatialChunkData
            {
                center = center,
                size = size,
            };
    }

    [Serializable]
    public sealed class GameplayFractureSpatialProfileData
    {
        public string archetypeId = string.Empty;
        public string profileId = string.Empty;
        public List<GameplayFractureSpatialChunkData> chunks =
            new List<GameplayFractureSpatialChunkData>();

        public void Normalize()
        {
            archetypeId = archetypeId?.Trim() ?? string.Empty;
            profileId = profileId?.Trim() ?? string.Empty;
            chunks = chunks ?? new List<GameplayFractureSpatialChunkData>();
        }

        public GameplayFractureSpatialProfileData DeepCopy()
        {
            var copy = new GameplayFractureSpatialProfileData
            {
                archetypeId = archetypeId ?? string.Empty,
                profileId = profileId ?? string.Empty,
            };
            if (chunks != null)
                foreach (GameplayFractureSpatialChunkData chunk in chunks)
                    copy.chunks.Add(chunk?.DeepCopy());
            return copy;
        }
    }

    [Serializable]
    public sealed class GameplayFractureSpatialCatalogDocument
    {
        public const int CurrentSchemaVersion = 1;

        public int schemaVersion = CurrentSchemaVersion;
        public List<GameplayFractureSpatialProfileData> profiles =
            new List<GameplayFractureSpatialProfileData>();

        public void Normalize()
        {
            profiles = profiles
                ?? new List<GameplayFractureSpatialProfileData>();
            foreach (GameplayFractureSpatialProfileData profile in profiles)
                profile?.Normalize();
            profiles.Sort((left, right) => StringComparer.Ordinal.Compare(
                left?.archetypeId,
                right?.archetypeId));
        }

        public GameplayFractureSpatialCatalogDocument DeepCopy()
        {
            var copy = new GameplayFractureSpatialCatalogDocument
            {
                schemaVersion = schemaVersion,
            };
            if (profiles != null)
                foreach (GameplayFractureSpatialProfileData profile in profiles)
                    copy.profiles.Add(profile?.DeepCopy());
            return copy;
        }
    }

    /// <summary>
    /// Owns the complete portable simulation-spatial contract for a gameplay
    /// level. Both live Unity play and engine-free battle execution consume
    /// this object, so fracture topology cannot silently default on one path.
    /// </summary>
    public sealed class GameplayStaticSpatialContent
    {
        /// <summary>
        /// Version two narrows the fingerprint from the entire authoring
        /// document to the data that the simulation and replay viewer actually
        /// use as spatial evidence. Presentation-only data such as lighting,
        /// dressing, editor groups, and display labels must not invalidate a
        /// deterministic battle artifact.
        /// </summary>
        public const int CurrentEvidenceAlgorithmVersion = 2;

        private sealed class CanonicalDefinition
        {
            public CanonicalDefinition(
                List<CanonicalEntityDefinition> entities,
                List<CanonicalTerrainDefinition> terrainSurfaces,
                List<LevelTraversalLinkData> traversalLinks,
                GameplayFractureSpatialCatalogDocument fractureCatalog)
            {
                this.entities = entities;
                this.terrainSurfaces = terrainSurfaces;
                this.traversalLinks = traversalLinks;
                this.fractureCatalog = fractureCatalog;
            }

            public List<CanonicalEntityDefinition> entities;

            public List<CanonicalTerrainDefinition> terrainSurfaces;

            public List<LevelTraversalLinkData> traversalLinks;

            public GameplayFractureSpatialCatalogDocument fractureCatalog;
        }

        private sealed class CanonicalEntityDefinition
        {
            public CanonicalEntityDefinition(LevelEntity source)
            {
                id = source.id ?? string.Empty;
                archetypeId = source.archetypeId ?? string.Empty;
                transform = source.transform;
                destructible = source.destructible?.DeepCopy();
                placementSurface = source.placementSurface?.DeepCopy();
                coverVolumes = new List<CoverVolumeData>();
                if (source.coverVolumes != null)
                {
                    foreach (CoverVolumeData volume in source.coverVolumes)
                        coverVolumes.Add(volume?.DeepCopy());
                }
                interactionPoints = new List<InteractionPointData>();
                if (source.interactionPoints != null)
                {
                    foreach (InteractionPointData point in source.interactionPoints)
                        interactionPoints.Add(point?.DeepCopy());
                }
            }

            // This intentionally omits authoring-only entity data such as the
            // editor group and rotation pivot. The retained fields feed static
            // collision, placement, destructible, vehicle, objective, and
            // fracture evidence.
            public string id;

            public string archetypeId;

            public LevelTransformData transform;

            public List<CoverVolumeData> coverVolumes;

            public List<InteractionPointData> interactionPoints;

            public DestructibleInstanceData destructible;

            public LevelPlacementSurfaceData placementSurface;
        }

        private sealed class CanonicalTerrainDefinition
        {
            public CanonicalTerrainDefinition(TerrainSurfaceData source)
            {
                id = source.id ?? string.Empty;
                origin = source.origin;
                sampleCountX = source.sampleCountX;
                sampleCountZ = source.sampleCountZ;
                sampleSpacing = source.sampleSpacing;
                minimumElevation = source.minimumElevation;
                elevationIncrement = source.elevationIncrement;
                heightSamples = source.heightSamples != null
                    ? new List<int>(source.heightSamples)
                    : new List<int>();
            }

            // Material and appearance samples are presentation data. Headless
            // movement, placement, and projectile evidence use terrain shape
            // only.
            public string id;

            public Float3Data origin;

            public int sampleCountX;

            public int sampleCountZ;

            public float sampleSpacing;

            public float minimumElevation;

            public float elevationIncrement;

            public List<int> heightSamples;
        }

        private readonly IReadOnlyDictionary<
            string,
            GameplayFractureSpatialProfile> fractureProfilesByArchetype;

        public GameplayStaticSpatialContent(
            LevelDocument level,
            GameplayFractureSpatialCatalogDocument fractureCatalog)
        {
            Level = level?.DeepCopy()
                ?? throw new ArgumentNullException(nameof(level));
            Level.Normalize();
            FractureCatalog = fractureCatalog?.DeepCopy()
                ?? throw new ArgumentNullException(nameof(fractureCatalog));
            FractureCatalog.Normalize();
            if (FractureCatalog.schemaVersion
                != GameplayFractureSpatialCatalogDocument.CurrentSchemaVersion)
            {
                throw new InvalidOperationException(
                    "The fracture spatial catalog has an unsupported schema.");
            }

            var profiles = new Dictionary<
                string,
                GameplayFractureSpatialProfile>(StringComparer.Ordinal);
            var profileIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (GameplayFractureSpatialProfileData authored in
                FractureCatalog.profiles)
            {
                if (authored == null)
                    throw new InvalidOperationException(
                        "The fracture spatial catalog contains an empty profile.");
                string archetypeId = GameplayContentIdentity.RequireText(
                    authored.archetypeId,
                    nameof(authored.archetypeId));
                string profileId = GameplayContentIdentity.RequireText(
                    authored.profileId,
                    nameof(authored.profileId));
                if (!profileIds.Add(profileId))
                    throw new InvalidOperationException(
                        $"Fracture spatial profile '{profileId}' is duplicated.");
                var chunks = new List<GameplayLocalSpatialVolume>();
                foreach (GameplayFractureSpatialChunkData chunk in
                    authored.chunks)
                {
                    if (chunk == null)
                        throw new InvalidOperationException(
                            $"Fracture spatial profile '{profileId}' contains "
                            + "an empty chunk.");
                    chunks.Add(new GameplayLocalSpatialVolume(
                        ToPosition(chunk.center),
                        ToPosition(chunk.size)));
                }
                if (!profiles.TryAdd(
                        archetypeId,
                        new GameplayFractureSpatialProfile(profileId, chunks)))
                {
                    throw new InvalidOperationException(
                        $"Archetype '{archetypeId}' has multiple fracture "
                        + "spatial profiles.");
                }
            }

            fractureProfilesByArchetype = new ReadOnlyDictionary<
                string,
                GameplayFractureSpatialProfile>(profiles);
            Identity = new SpatialContentIdentity(
                Level.levelId,
                Level.schemaVersion,
                evidenceAlgorithmVersion: CurrentEvidenceAlgorithmVersion,
                GameplayCanonicalValueDigest.CalculateSerializableFields(
                    CreateCanonicalDefinition(Level, FractureCatalog)));
        }

        public LevelDocument Level { get; }

        public GameplayFractureSpatialCatalogDocument FractureCatalog { get; }

        public IReadOnlyDictionary<string, GameplayFractureSpatialProfile>
            FractureProfilesByArchetype => fractureProfilesByArchetype;

        public SpatialContentIdentity Identity { get; }

        public GameplayHeadlessSpatialEvidence CreateEvidence() =>
            new GameplayHeadlessSpatialEvidence(
                Level,
                Identity,
                fractureProfilesByArchetype);

        public int ResolveFractureChunkCount(LevelEntity entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            return fractureProfilesByArchetype.TryGetValue(
                entity.archetypeId ?? string.Empty,
                out GameplayFractureSpatialProfile profile)
                    ? profile.ChunkCount
                    : 0;
        }

        private static GameplayPosition ToPosition(Float3Data value) =>
            new GameplayPosition(value.x, value.y, value.z);

        private static CanonicalDefinition CreateCanonicalDefinition(
            LevelDocument level,
            GameplayFractureSpatialCatalogDocument fractureCatalog)
        {
            var entities = new List<CanonicalEntityDefinition>();
            foreach (LevelEntity entity in level.entities)
            {
                if (entity != null)
                    entities.Add(new CanonicalEntityDefinition(entity));
            }
            entities.Sort((left, right) => StringComparer.Ordinal.Compare(
                left.id,
                right.id));

            var terrainSurfaces = new List<CanonicalTerrainDefinition>();
            foreach (TerrainSurfaceData terrain in level.terrainSurfaces)
            {
                if (terrain != null)
                    terrainSurfaces.Add(new CanonicalTerrainDefinition(terrain));
            }
            terrainSurfaces.Sort((left, right) => StringComparer.Ordinal.Compare(
                left.id,
                right.id));

            var traversalLinks = new List<LevelTraversalLinkData>();
            foreach (LevelTraversalLinkData link in level.traversalLinks)
            {
                if (link != null)
                    traversalLinks.Add(link.DeepCopy());
            }
            traversalLinks.Sort((left, right) => StringComparer.Ordinal.Compare(
                left.id,
                right.id));

            return new CanonicalDefinition(
                entities,
                terrainSurfaces,
                traversalLinks,
                fractureCatalog.DeepCopy());
        }
    }
}
