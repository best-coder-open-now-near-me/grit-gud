using System;
using System.Collections.Generic;
using System.Linq;

namespace GritGud.Domain.Levels
{
    public enum LevelValidationSeverity
    {
        Warning,
        Error,
    }

    public enum LevelValidationProfile
    {
        Authoring,
        Publish,
        Runtime,
    }

    public sealed class LevelValidationIssue
    {
        public LevelValidationIssue(
            LevelValidationSeverity severity,
            string code,
            string message,
            string entityId = null)
        {
            Severity = severity;
            Code = code ?? throw new ArgumentNullException(nameof(code));
            Message = message ?? throw new ArgumentNullException(nameof(message));
            EntityId = entityId;
        }

        public LevelValidationSeverity Severity { get; }

        public string Code { get; }

        public string Message { get; }

        public string EntityId { get; }
    }

    public sealed class LevelValidationContext
    {
        private readonly ICollection<LevelValidationIssue> issues;

        internal LevelValidationContext(
            LevelDocument document,
            ISet<string> knownArchetypeIds,
            LevelValidationProfile profile,
            ICollection<LevelValidationIssue> issues)
        {
            Document = document ?? throw new ArgumentNullException(nameof(document));
            KnownArchetypeIds = knownArchetypeIds;
            Profile = profile;
            this.issues = issues ?? throw new ArgumentNullException(nameof(issues));
        }

        public LevelDocument Document { get; }

        public ISet<string> KnownArchetypeIds { get; }

        public LevelValidationProfile Profile { get; }

        public void Report(
            LevelValidationSeverity severity,
            string code,
            string message,
            string entityId = null)
        {
            issues.Add(new LevelValidationIssue(severity, code, message, entityId));
        }

        public void Error(string code, string message, string entityId = null)
        {
            Report(LevelValidationSeverity.Error, code, message, entityId);
        }

        public void Warning(string code, string message, string entityId = null)
        {
            Report(LevelValidationSeverity.Warning, code, message, entityId);
        }
    }

    public interface ILevelValidationRule
    {
        void Evaluate(LevelValidationContext context);
    }

    public sealed class LevelValidationService
    {
        private readonly ILevelValidationRule[] rules;

        public LevelValidationService(IEnumerable<ILevelValidationRule> rules)
        {
            this.rules = rules?.Where(rule => rule != null).ToArray()
                ?? throw new ArgumentNullException(nameof(rules));
        }

        public IReadOnlyList<LevelValidationIssue> Validate(
            LevelDocument source,
            ISet<string> knownArchetypeIds = null,
            LevelValidationProfile profile = LevelValidationProfile.Authoring)
        {
            var issues = new List<LevelValidationIssue>();
            if (source == null)
            {
                issues.Add(new LevelValidationIssue(
                    LevelValidationSeverity.Error,
                    "document.missing",
                    "The level document is missing."));
                return issues;
            }

            LevelDocument document = source.DeepCopy();
            document.Normalize();
            var context = new LevelValidationContext(document, knownArchetypeIds, profile, issues);
            foreach (ILevelValidationRule rule in rules)
            {
                rule.Evaluate(context);
            }

            return issues;
        }
    }

    public static class LevelValidator
    {
        public const int MaximumEntityCount = 2048;

        private static readonly LevelValidationService DefaultService = new LevelValidationService(
            new ILevelValidationRule[]
            {
                new LevelDocumentValidationRule(),
                new LevelEntityValidationRule(),
                new LevelGameplayMetadataValidationRule(),
                new LevelPlaytestValidationRule(),
                new LevelTerrainValidationRule(),
            });

        public static IReadOnlyList<LevelValidationIssue> Validate(
            LevelDocument document,
            ISet<string> knownArchetypeIds = null,
            LevelValidationProfile profile = LevelValidationProfile.Authoring)
        {
            return DefaultService.Validate(document, knownArchetypeIds, profile);
        }

        public static bool HasErrors(IReadOnlyList<LevelValidationIssue> issues)
        {
            return issues != null
                && issues.Any(issue => issue?.Severity == LevelValidationSeverity.Error);
        }
    }

    public sealed class LevelTerrainValidationRule : ILevelValidationRule
    {
        public const int MaximumSamplesPerAxis = 257;
        public const int MaximumSamplesPerSurface = 66049;
        public const int MinimumQuantizedHeight = -1000000;
        public const int MaximumQuantizedHeight = 1000000;

        public void Evaluate(LevelValidationContext context)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (TerrainSurfaceData surface in context.Document.terrainSurfaces)
            {
                if (surface == null)
                {
                    context.Error("terrain.missing", "The terrain surface list contains an empty entry.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(surface.id) || !ids.Add(surface.id))
                {
                    context.Error("terrain.id", "Terrain surface IDs must be present and unique.");
                }

                bool dimensionsValid = surface.sampleCountX >= 2
                    && surface.sampleCountZ >= 2
                    && surface.sampleCountX <= MaximumSamplesPerAxis
                    && surface.sampleCountZ <= MaximumSamplesPerAxis
                    && (long)surface.sampleCountX * surface.sampleCountZ
                        <= MaximumSamplesPerSurface;
                if (!dimensionsValid)
                {
                    context.Error(
                        "terrain.dimensions",
                        $"Terrain '{surface.id}' needs between 2 and {MaximumSamplesPerAxis} "
                        + "samples per axis within the total sample limit.");
                }

                int expectedSamples = dimensionsValid
                    ? surface.sampleCountX * surface.sampleCountZ
                    : -1;
                if (surface.heightSamples.Count != expectedSamples)
                {
                    context.Error(
                        "terrain.samples",
                        $"Terrain '{surface.id}' has {surface.heightSamples.Count} height samples; "
                        + $"expected {expectedSamples}.");
                }

                if (!LevelValidationMath.IsFinite(surface.origin)
                    || !LevelValidationMath.IsFinite(surface.sampleSpacing)
                    || !LevelValidationMath.IsFinite(surface.minimumElevation)
                    || !LevelValidationMath.IsFinite(surface.elevationIncrement)
                    || surface.sampleSpacing <= 0f
                    || surface.elevationIncrement <= 0f)
                {
                    context.Error(
                        "terrain.scale",
                        $"Terrain '{surface.id}' needs finite origins and positive sample scales.");
                }

                if (surface.heightSamples.Any(value =>
                    value < MinimumQuantizedHeight || value > MaximumQuantizedHeight))
                {
                    context.Error(
                        "terrain.height-range",
                        $"Terrain '{surface.id}' contains a height outside the quantized range.");
                }
            }
        }
    }

    public sealed class LevelDocumentValidationRule : ILevelValidationRule
    {
        public void Evaluate(LevelValidationContext context)
        {
            LevelDocument document = context.Document;
            if (document.schemaVersion != LevelDocument.CurrentSchemaVersion)
            {
                context.Error(
                    "schema.unsupported",
                    $"Schema version {document.schemaVersion} is not supported; expected "
                    + $"{LevelDocument.CurrentSchemaVersion}.");
            }

            if (string.IsNullOrWhiteSpace(document.levelId))
            {
                context.Error("level.id.missing", "The level needs a stable ID.");
            }

            if (string.IsNullOrWhiteSpace(document.displayName))
            {
                context.Error("level.name.missing", "The level needs a display name.");
            }

            if (!LevelValidationMath.IsFinite(document.bounds.center)
                || !LevelValidationMath.IsFinite(document.bounds.size))
            {
                context.Error("bounds.not-finite", "Level bounds must contain finite coordinates.");
            }
            else if (document.bounds.size.x <= 0f
                || document.bounds.size.y <= 0f
                || document.bounds.size.z <= 0f)
            {
                context.Error("bounds.size", "Every level-bounds dimension must be greater than zero.");
            }

            if (document.entities.Count > LevelValidator.MaximumEntityCount)
            {
                context.Error(
                    "entities.limit",
                    $"The level contains {document.entities.Count} entities; the limit is "
                    + $"{LevelValidator.MaximumEntityCount}.");
            }
        }
    }

    public sealed class LevelPlaytestValidationRule : ILevelValidationRule
    {
        public void Evaluate(LevelValidationContext context)
        {
            LevelPlaytestData playtest = context.Document.playtest;
            if (playtest == null)
            {
                context.Error("playtest.missing", "The level needs playtest settings.");
                return;
            }

            if (!LevelValidationMath.IsFinite(playtest.playerStart.position)
                || !LevelValidationMath.IsFinite(playtest.playerStart.yawDegrees))
            {
                context.Error("playtest.start.not-finite", "The player start must be finite.");
            }
            else if (!LevelValidationMath.Contains(
                         context.Document.bounds,
                         playtest.playerStart.position))
            {
                context.Warning(
                    "playtest.start.outside-bounds",
                    "The player start is outside the authored level bounds.");
            }
        }
    }

    public sealed class LevelEntityValidationRule : ILevelValidationRule
    {
        public void Evaluate(LevelValidationContext context)
        {
            var entityIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (LevelEntity entity in context.Document.entities)
            {
                if (entity == null)
                {
                    context.Error("entity.missing", "The entity list contains an empty entry.");
                    continue;
                }

                string entityId = entity.id;
                if (string.IsNullOrWhiteSpace(entityId))
                {
                    context.Error("entity.id.missing", "An entity needs a stable ID.");
                }
                else if (!entityIds.Add(entityId))
                {
                    context.Error(
                        "entity.id.duplicate",
                        $"Entity ID '{entityId}' is duplicated.",
                        entityId);
                }

                if (string.IsNullOrWhiteSpace(entity.archetypeId))
                {
                    context.Error(
                        "entity.archetype.missing",
                        "The entity needs an archetype ID.",
                        entityId);
                }
                else if (context.KnownArchetypeIds != null
                    && !context.KnownArchetypeIds.Contains(entity.archetypeId))
                {
                    context.Error(
                        "entity.archetype.unknown",
                        $"Archetype '{entity.archetypeId}' is not in the active catalog.",
                        entityId);
                }

                if (!LevelValidationMath.IsFinite(entity.transform.position)
                    || !LevelValidationMath.IsFinite(entity.transform.yawDegrees))
                {
                    context.Error(
                        "entity.transform.not-finite",
                        "Entity transforms must be finite.",
                        entityId);
                }
                else if (!LevelValidationMath.Contains(
                    context.Document.bounds,
                    entity.transform.position))
                {
                    context.Warning(
                        "entity.outside-bounds",
                        "The entity origin is outside the authored level bounds.",
                        entityId);
                }
            }
        }
    }

    public sealed class LevelGameplayMetadataValidationRule : ILevelValidationRule
    {
        public void Evaluate(LevelValidationContext context)
        {
            foreach (LevelEntity entity in context.Document.entities)
            {
                if (entity == null)
                {
                    continue;
                }

                ValidateCoverVolumes(context, entity);
                ValidateInteractionPoints(context, entity);
                ValidateDestructible(context, entity);
            }
        }

        private static void ValidateCoverVolumes(LevelValidationContext context, LevelEntity entity)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (CoverVolumeData volume in entity.coverVolumes)
            {
                if (volume == null)
                {
                    context.Error("cover.missing", "A cover-volume entry is empty.", entity.id);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(volume.id) || !ids.Add(volume.id))
                {
                    context.Error(
                        "cover.id",
                        "Cover-volume IDs must be present and unique within an entity.",
                        entity.id);
                }

                if (!LevelValidationMath.IsFinite(volume.localCenter)
                    || !LevelValidationMath.IsFinite(volume.size)
                    || volume.size.x <= 0f
                    || volume.size.y <= 0f
                    || volume.size.z <= 0f)
                {
                    context.Error(
                        "cover.volume",
                        "Cover volumes need finite centers and positive dimensions.",
                        entity.id);
                }
            }
        }

        private static void ValidateInteractionPoints(
            LevelValidationContext context,
            LevelEntity entity)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (InteractionPointData point in entity.interactionPoints)
            {
                if (point == null)
                {
                    context.Error(
                        "interaction.missing",
                        "An interaction-point entry is empty.",
                        entity.id);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(point.id) || !ids.Add(point.id))
                {
                    context.Error(
                        "interaction.id",
                        "Interaction-point IDs must be present and unique within an entity.",
                        entity.id);
                }

                if (string.IsNullOrWhiteSpace(point.type))
                {
                    context.Error(
                        "interaction.type",
                        "Every interaction point needs a type.",
                        entity.id);
                }

                if (!LevelValidationMath.IsFinite(point.localPosition)
                    || !LevelValidationMath.IsFinite(point.radius)
                    || point.radius <= 0f)
                {
                    context.Error(
                        "interaction.radius",
                        "Interaction points need finite positions and a positive radius.",
                        entity.id);
                }
            }
        }

        private static void ValidateDestructible(
            LevelValidationContext context,
            LevelEntity entity)
        {
            if (entity.destructible == null || !entity.destructible.enabled)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(entity.destructible.initialState))
            {
                context.Error(
                    "destructible.state",
                    "A destructible entity needs an initial state.",
                    entity.id);
            }
            else if (!string.Equals(
                         entity.destructible.initialState,
                         "intact",
                         StringComparison.OrdinalIgnoreCase)
                     && !string.Equals(
                         entity.destructible.initialState,
                         "damaged",
                         StringComparison.OrdinalIgnoreCase)
                     && !string.Equals(
                         entity.destructible.initialState,
                         "destroyed",
                         StringComparison.OrdinalIgnoreCase))
            {
                context.Error(
                    "destructible.state",
                    "Destructible state must be intact, damaged, or destroyed.",
                    entity.id);
            }

            if (!LevelValidationMath.IsFinite(entity.destructible.integrity)
                || entity.destructible.integrity <= 0f)
            {
                context.Error(
                    "destructible.integrity",
                    "Destructible integrity must be finite and positive.",
                    entity.id);
            }
        }
    }

    internal static class LevelValidationMath
    {
        public static bool Contains(LevelBoundsData bounds, Float3Data point)
        {
            float halfX = bounds.size.x * 0.5f;
            float halfY = bounds.size.y * 0.5f;
            float halfZ = bounds.size.z * 0.5f;
            return point.x >= bounds.center.x - halfX
                && point.x <= bounds.center.x + halfX
                && point.y >= bounds.center.y - halfY
                && point.y <= bounds.center.y + halfY
                && point.z >= bounds.center.z - halfZ
                && point.z <= bounds.center.z + halfZ;
        }

        public static bool IsFinite(Float3Data value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        public static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
