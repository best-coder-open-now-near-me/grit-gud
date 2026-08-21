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
    /// Owns the complete portable static-spatial contract for a gameplay
    /// level. Both live Unity play and engine-free battle execution consume
    /// this object, so fracture topology cannot silently default on one path.
    /// </summary>
    public sealed class GameplayStaticSpatialContent
    {
        private sealed class CanonicalDefinition
        {
            public CanonicalDefinition(
                string levelDigest,
                string fractureCatalogDigest)
            {
                LevelDigest = levelDigest;
                FractureCatalogDigest = fractureCatalogDigest;
            }

            public string LevelDigest { get; }

            public string FractureCatalogDigest { get; }
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
                evidenceAlgorithmVersion: 1,
                GameplayCanonicalValueDigest.Calculate(
                    new CanonicalDefinition(
                        GameplayCanonicalValueDigest
                            .CalculateSerializableFields(Level),
                        GameplayCanonicalValueDigest
                            .CalculateSerializableFields(FractureCatalog))));
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
    }
}
