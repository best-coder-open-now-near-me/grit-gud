using System;
using System.Globalization;

namespace GritGud.Application.Gameplay
{
    public sealed class GameplayContentIdentity
    {
        public const int CurrentSchemaVersion = 1;

        public GameplayContentIdentity(
            string scenarioId,
            int scenarioSchemaVersion,
            int rulesSchemaVersion,
            string definitionDigest)
        {
            ScenarioId = RequireText(scenarioId, nameof(scenarioId));
            if (scenarioSchemaVersion <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(scenarioSchemaVersion));
            if (rulesSchemaVersion <= 0)
                throw new ArgumentOutOfRangeException(nameof(rulesSchemaVersion));
            ScenarioSchemaVersion = scenarioSchemaVersion;
            RulesSchemaVersion = rulesSchemaVersion;
            DefinitionDigest = RequireDigest(
                definitionDigest,
                nameof(definitionDigest));
        }

        public int SchemaVersion => CurrentSchemaVersion;
        public string ScenarioId { get; }
        public int ScenarioSchemaVersion { get; }
        public int RulesSchemaVersion { get; }
        public string DefinitionDigest { get; }

        public bool HasSameIdentity(GameplayContentIdentity other) =>
            other != null
            && SchemaVersion == other.SchemaVersion
            && ScenarioSchemaVersion == other.ScenarioSchemaVersion
            && RulesSchemaVersion == other.RulesSchemaVersion
            && string.Equals(
                ScenarioId,
                other.ScenarioId,
                StringComparison.Ordinal)
            && string.Equals(
                DefinitionDigest,
                other.DefinitionDigest,
                StringComparison.Ordinal);

        internal static string RequireDigest(
            string value,
            string parameterName)
        {
            string digest = RequireText(value, parameterName);
            if (digest.Length != 64)
                throw new ArgumentException(
                    "Identity digests require 64 lowercase hexadecimal characters.",
                    parameterName);
            foreach (char character in digest)
                if (!((character >= '0' && character <= '9')
                    || (character >= 'a' && character <= 'f')))
                    throw new ArgumentException(
                        "Identity digests require 64 lowercase hexadecimal characters.",
                        parameterName);
            return digest;
        }

        internal static string RequireText(
            string value,
            string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException(
                    "Identity fields cannot be empty.",
                    parameterName);
            return value.Trim();
        }
    }

    public sealed class SpatialContentIdentity
    {
        public const int CurrentSchemaVersion = 1;

        public SpatialContentIdentity(
            string levelId,
            int levelSchemaVersion,
            int evidenceAlgorithmVersion,
            string staticSpatialDigest)
        {
            LevelId = GameplayContentIdentity.RequireText(
                levelId,
                nameof(levelId));
            if (levelSchemaVersion <= 0)
                throw new ArgumentOutOfRangeException(nameof(levelSchemaVersion));
            if (evidenceAlgorithmVersion <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(evidenceAlgorithmVersion));
            LevelSchemaVersion = levelSchemaVersion;
            EvidenceAlgorithmVersion = evidenceAlgorithmVersion;
            StaticSpatialDigest = GameplayContentIdentity.RequireDigest(
                staticSpatialDigest,
                nameof(staticSpatialDigest));
        }

        public int SchemaVersion => CurrentSchemaVersion;
        public string LevelId { get; }
        public int LevelSchemaVersion { get; }
        public int EvidenceAlgorithmVersion { get; }
        public string StaticSpatialDigest { get; }

        public bool HasSameIdentity(SpatialContentIdentity other) =>
            other != null
            && SchemaVersion == other.SchemaVersion
            && LevelSchemaVersion == other.LevelSchemaVersion
            && EvidenceAlgorithmVersion == other.EvidenceAlgorithmVersion
            && string.Equals(LevelId, other.LevelId, StringComparison.Ordinal)
            && string.Equals(
                StaticSpatialDigest,
                other.StaticSpatialDigest,
                StringComparison.Ordinal);
    }

    public sealed class ScenarioRunIdentity
    {
        public const int CurrentSchemaVersion = 1;
        public const int CurrentRandomSchemaVersion = 1;

        public ScenarioRunIdentity(
            string runId,
            uint scenarioSeed,
            int randomSchemaVersion = CurrentRandomSchemaVersion)
        {
            RunId = GameplayContentIdentity.RequireText(runId, nameof(runId));
            if (randomSchemaVersion <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(randomSchemaVersion));
            ScenarioSeed = scenarioSeed;
            RandomSchemaVersion = randomSchemaVersion;
        }

        public int SchemaVersion => CurrentSchemaVersion;
        public string RunId { get; }
        public uint ScenarioSeed { get; }
        public int RandomSchemaVersion { get; }

        public bool HasSameIdentity(ScenarioRunIdentity other) =>
            other != null
            && SchemaVersion == other.SchemaVersion
            && RandomSchemaVersion == other.RandomSchemaVersion
            && ScenarioSeed == other.ScenarioSeed
            && string.Equals(RunId, other.RunId, StringComparison.Ordinal);
    }

    public sealed class GameplayExecutionIdentity
    {
        public GameplayExecutionIdentity(
            GameplayContentIdentity gameplay,
            SpatialContentIdentity spatial,
            ScenarioRunIdentity run)
        {
            Gameplay = gameplay ?? throw new ArgumentNullException(
                nameof(gameplay));
            Spatial = spatial ?? throw new ArgumentNullException(nameof(spatial));
            Run = run ?? throw new ArgumentNullException(nameof(run));
        }

        public GameplayContentIdentity Gameplay { get; }
        public SpatialContentIdentity Spatial { get; }
        public ScenarioRunIdentity Run { get; }

        public bool HasSameIdentity(GameplayExecutionIdentity other) =>
            other != null
            && Gameplay.HasSameIdentity(other.Gameplay)
            && Spatial.HasSameIdentity(other.Spatial)
            && Run.HasSameIdentity(other.Run);
    }

    public readonly struct GameplayTransitionIdentity : IEquatable<
        GameplayTransitionIdentity>
    {
        public GameplayTransitionIdentity(
            long sequence,
            string kind,
            string actorId,
            string subjectId)
        {
            if (sequence <= 0)
                throw new ArgumentOutOfRangeException(nameof(sequence));
            Sequence = sequence;
            Kind = GameplayContentIdentity.RequireText(kind, nameof(kind));
            ActorId = GameplayContentIdentity.RequireText(
                actorId,
                nameof(actorId));
            SubjectId = GameplayContentIdentity.RequireText(
                subjectId,
                nameof(subjectId));
        }

        public long Sequence { get; }
        public string Kind { get; }
        public string ActorId { get; }
        public string SubjectId { get; }

        public bool Equals(GameplayTransitionIdentity other) =>
            Sequence == other.Sequence
            && string.Equals(Kind, other.Kind, StringComparison.Ordinal)
            && string.Equals(ActorId, other.ActorId, StringComparison.Ordinal)
            && string.Equals(SubjectId, other.SubjectId, StringComparison.Ordinal);

        public override bool Equals(object obj) =>
            obj is GameplayTransitionIdentity other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Sequence.GetHashCode();
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(Kind);
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(ActorId);
                return (hash * 397)
                    ^ StringComparer.Ordinal.GetHashCode(SubjectId);
            }
        }

        public static bool operator ==(
            GameplayTransitionIdentity left,
            GameplayTransitionIdentity right) => left.Equals(right);

        public static bool operator !=(
            GameplayTransitionIdentity left,
            GameplayTransitionIdentity right) => !left.Equals(right);
    }

    public static class GameplayNumericPolicy
    {
        public const int CurrentVersion = 1;
        public const int CanonicalDecimalPlaces = 5;
        public const float ComparisonTolerance = 0.0001f;

        public static float Normalize(float value)
        {
            RequireFinite(value, nameof(value));
            return (float)Math.Round(
                value,
                CanonicalDecimalPlaces,
                MidpointRounding.AwayFromZero);
        }

        public static string FormatCanonical(float value) =>
            Normalize(value).ToString("0.#####", CultureInfo.InvariantCulture);

        public static bool AreEquivalent(float left, float right)
        {
            RequireFinite(left, nameof(left));
            RequireFinite(right, nameof(right));
            return Math.Abs(left - right) <= ComparisonTolerance;
        }

        public static void RequireFinite(float value, string parameterName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    "Deterministic numeric values must be finite.");
        }
    }
}
