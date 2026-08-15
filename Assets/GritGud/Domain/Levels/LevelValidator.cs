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
            LevelValidationContent content,
            LevelValidationProfile profile,
            ICollection<LevelValidationIssue> issues)
        {
            Document = document ?? throw new ArgumentNullException(nameof(document));
            Content = content;
            Profile = profile;
            this.issues = issues ?? throw new ArgumentNullException(nameof(issues));
        }

        public LevelDocument Document { get; }

        public LevelValidationContent Content { get; }

        public ISet<string> KnownArchetypeIds => Content?.KnownArchetypeIds;

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
            return Validate(
                source,
                new LevelValidationContent(knownArchetypeIds),
                profile);
        }

        public IReadOnlyList<LevelValidationIssue> Validate(
            LevelDocument source,
            LevelValidationContent content,
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
            var context = new LevelValidationContext(document, content, profile, issues);
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
                new LevelScenarioValidationRule(),
                new LevelTerrainValidationRule(),
            });

        public static IReadOnlyList<LevelValidationIssue> Validate(
            LevelDocument document,
            ISet<string> knownArchetypeIds = null,
            LevelValidationProfile profile = LevelValidationProfile.Authoring)
        {
            return DefaultService.Validate(document, knownArchetypeIds, profile);
        }

        public static IReadOnlyList<LevelValidationIssue> Validate(
            LevelDocument document,
            LevelValidationContent content,
            LevelValidationProfile profile = LevelValidationProfile.Authoring)
        {
            return DefaultService.Validate(document, content, profile);
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
        public const int MaximumSurfaceCount = 16;
        public const int MaximumSamplesPerDocument = 262144;
        public const int MinimumQuantizedHeight = -1000000;
        public const int MaximumQuantizedHeight = 1000000;

        public void Evaluate(LevelValidationContext context)
        {
            if (context.Document.terrainSurfaces.Count > MaximumSurfaceCount)
            {
                context.Error(
                    "terrain.surface-limit",
                    $"The level contains {context.Document.terrainSurfaces.Count} terrain "
                    + $"surfaces; the limit is {MaximumSurfaceCount}.");
            }

            long totalSampleCount = context.Document.terrainSurfaces
                .Where(surface => surface != null
                    && surface.sampleCountX > 0
                    && surface.sampleCountZ > 0)
                .Sum(surface => (long)surface.sampleCountX * surface.sampleCountZ);
            if (totalSampleCount > MaximumSamplesPerDocument)
            {
                context.Error(
                    "terrain.document-sample-limit",
                    $"The level contains {totalSampleCount} terrain samples; the total limit "
                    + $"is {MaximumSamplesPerDocument}.");
            }

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

    public sealed class LevelScenarioValidationRule : ILevelValidationRule
    {
        public void Evaluate(LevelValidationContext context)
        {
            LevelScenarioData scenario = context.Document.scenario;
            if (scenario == null)
            {
                context.Error("scenario.missing", "The level needs scenario settings.");
                return;
            }

            if (!LevelValidationMath.IsFinite(scenario.minimumVoluntaryTurnSeconds)
                || scenario.minimumVoluntaryTurnSeconds < 0f)
            {
                context.Error(
                    "scenario.timing.invalid",
                    "The scenario minimum turn duration must be finite and non-negative.");
            }

            var actorIds = new HashSet<string>(StringComparer.Ordinal);
            int playerCount = 0;
            int selectedPlayerCount = 0;
            int primaryTargetCount = 0;
            foreach (LevelScenarioActorData actor in scenario.actors)
            {
                if (actor == null)
                {
                    context.Error("scenario.actor.missing", "The scenario contains an empty actor.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(actor.id))
                {
                    context.Error("scenario.actor.id.missing", "A scenario actor needs a stable ID.");
                }
                else if (!actorIds.Add(actor.id))
                {
                    context.Error(
                        "scenario.actor.id.duplicate",
                        $"Scenario actor ID '{actor.id}' is duplicated.");
                }

                if (string.IsNullOrWhiteSpace(actor.templateId))
                {
                    context.Error(
                        "scenario.actor.template.missing",
                        $"Scenario actor '{actor.id}' needs an actor template.");
                }
                else
                {
                    ValidateActorTemplate(context, actor);
                }

                if (!LevelValidationMath.IsFinite(actor.transform.position)
                    || !LevelValidationMath.IsFinite(actor.transform.yawDegrees))
                {
                    context.Error(
                        "scenario.actor.transform.not-finite",
                        $"Scenario actor '{actor.id}' must have a finite transform.");
                }
                else if (!LevelValidationMath.Contains(
                             context.Document.bounds,
                             actor.transform.position))
                {
                    context.Warning(
                        "scenario.actor.outside-bounds",
                        $"Scenario actor '{actor.id}' is outside the authored level bounds.");
                }

                if (actor.playerControlled)
                    playerCount++;
                if (actor.initiallySelected)
                {
                    selectedPlayerCount++;
                    if (!actor.playerControlled)
                    {
                        context.Error(
                            "scenario.actor.selection.not-player",
                            $"Initially selected actor '{actor.id}' must be player controlled.");
                    }
                }

                if (actor.primaryTarget)
                {
                    primaryTargetCount++;
                    if (actor.playerControlled)
                    {
                        context.Error(
                            "scenario.actor.target.player",
                            $"Player actor '{actor.id}' cannot be the primary target.");
                    }
                }
            }

            if (playerCount == 0)
            {
                context.Error(
                    "scenario.party.empty",
                    "The scenario needs at least one player-controlled actor.");
            }

            if (selectedPlayerCount != 1)
            {
                context.Error(
                    "scenario.party.selection",
                    "The scenario needs exactly one initially selected player actor.");
            }

            if (primaryTargetCount > 1)
            {
                context.Error(
                    "scenario.target.multiple",
                    "The scenario can define at most one primary target actor.");
            }

            ValidateEntityLinks(context, scenario, actorIds);
        }

        private static void ValidateActorTemplate(
            LevelValidationContext context,
            LevelScenarioActorData actor)
        {
            LevelValidationContent content = context.Content;
            if (content?.HasActorTemplateCatalog != true)
            {
                return;
            }

            if (!content.TryGetActorPresentationId(
                    actor.templateId,
                    out string presentationId))
            {
                ReportRuntimeContentIssue(
                    context,
                    "scenario.actor.template.unknown",
                    $"Scenario actor '{actor.id}' references unavailable template "
                    + $"'{actor.templateId}'.",
                    actor.id);
                return;
            }

            if (string.IsNullOrWhiteSpace(presentationId))
            {
                ReportRuntimeContentIssue(
                    context,
                    "scenario.actor.presentation.missing",
                    $"Actor template '{actor.templateId}' does not define a presentation.",
                    actor.id);
            }
            else if (content.HasActorPresentationCatalog
                && !content.KnownActorPresentationIds.Contains(presentationId))
            {
                ReportRuntimeContentIssue(
                    context,
                    "scenario.actor.presentation.unknown",
                    $"Actor template '{actor.templateId}' references unavailable presentation "
                    + $"'{presentationId}'.",
                    actor.id);
            }
        }

        private static void ReportRuntimeContentIssue(
            LevelValidationContext context,
            string code,
            string message,
            string actorId)
        {
            if (context.Profile == LevelValidationProfile.Authoring)
            {
                context.Warning(code, message, actorId);
            }
            else
            {
                context.Error(code, message, actorId);
            }
        }

        private static void ValidateEntityLinks(
            LevelValidationContext context,
            LevelScenarioData scenario,
            ISet<string> actorIds)
        {
            var entities = context.Document.entities
                .Where(entity => entity != null && !string.IsNullOrWhiteSpace(entity.id))
                .GroupBy(entity => entity.id, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            var linkedProps = new HashSet<string>(StringComparer.Ordinal);
            foreach (LevelScenarioPropData prop in scenario.props)
            {
                if (prop == null || string.IsNullOrWhiteSpace(prop.entityId))
                {
                    context.Error("scenario.prop.entity.missing", "A scenario prop needs an entity link.");
                    continue;
                }

                if (!entities.ContainsKey(prop.entityId))
                {
                    context.Error(
                        "scenario.prop.entity.unknown",
                        $"Scenario prop entity '{prop.entityId}' does not exist.",
                        prop.entityId);
                }
                else if (!linkedProps.Add(prop.entityId))
                {
                    context.Error(
                        "scenario.prop.entity.duplicate",
                        $"Entity '{prop.entityId}' is linked as a scenario prop more than once.",
                        prop.entityId);
                }

                if (!LevelValidationMath.IsFinite(prop.mass) || prop.mass <= 0f)
                {
                    context.Error(
                        "scenario.prop.mass",
                        $"Scenario prop '{prop.entityId}' needs a positive finite mass.",
                        prop.entityId);
                }
                if (!IsSupportedSizeClass(prop.sizeClass))
                {
                    context.Error(
                        "scenario.prop.size",
                        $"Scenario prop '{prop.entityId}' has unsupported size "
                        + $"'{prop.sizeClass}'.",
                        prop.entityId);
                }
            }

            var linkedVehicles = new HashSet<string>(StringComparer.Ordinal);
            foreach (LevelScenarioVehicleData vehicle in scenario.vehicles)
            {
                if (vehicle == null || string.IsNullOrWhiteSpace(vehicle.entityId))
                {
                    context.Error(
                        "scenario.vehicle.entity.missing",
                        "A scenario vehicle needs an entity link.");
                    continue;
                }

                if (!entities.ContainsKey(vehicle.entityId))
                {
                    context.Error(
                        "scenario.vehicle.entity.unknown",
                        $"Scenario vehicle entity '{vehicle.entityId}' does not exist.",
                        vehicle.entityId);
                }
                else if (!linkedVehicles.Add(vehicle.entityId))
                {
                    context.Error(
                        "scenario.vehicle.entity.duplicate",
                        $"Entity '{vehicle.entityId}' is linked as a vehicle more than once.",
                        vehicle.entityId);
                }

                if (!string.IsNullOrWhiteSpace(vehicle.startingOccupantActorId)
                    && !actorIds.Contains(vehicle.startingOccupantActorId))
                {
                    context.Error(
                        "scenario.vehicle.occupant.unknown",
                        $"Vehicle '{vehicle.entityId}' references unknown actor "
                        + $"'{vehicle.startingOccupantActorId}'.",
                        vehicle.entityId);
                }

                if (!LevelValidationMath.IsFinite(vehicle.maximumSpeed)
                    || !LevelValidationMath.IsFinite(vehicle.accelerationPerTurn)
                    || !LevelValidationMath.IsFinite(vehicle.brakingPerTurn)
                    || !LevelValidationMath.IsFinite(vehicle.lowSpeedTurnDegrees)
                    || !LevelValidationMath.IsFinite(vehicle.highSpeedTurnDegrees)
                    || !LevelValidationMath.IsFinite(vehicle.baseTurningRadius)
                    || !LevelValidationMath.IsFinite(vehicle.speedTurningRadiusFactor)
                    || !LevelValidationMath.IsFinite(vehicle.startingSpeed)
                    || vehicle.maximumSpeed <= 0f
                    || vehicle.accelerationPerTurn < 0f
                    || vehicle.brakingPerTurn < 0f
                    || vehicle.lowSpeedTurnDegrees < 0f
                    || vehicle.highSpeedTurnDegrees < 0f
                    || vehicle.baseTurningRadius <= 0f
                    || vehicle.speedTurningRadiusFactor < 0f
                    || vehicle.startingSpeed < 0f
                    || vehicle.startingSpeed > vehicle.maximumSpeed)
                {
                    context.Error(
                        "scenario.vehicle.motion",
                        $"Scenario vehicle '{vehicle.entityId}' has invalid motion settings.",
                        vehicle.entityId);
                }
            }

            var objectiveIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (LevelScenarioObjectiveData objective in scenario.objectives)
            {
                if (objective == null || string.IsNullOrWhiteSpace(objective.id))
                {
                    context.Error("scenario.objective.id.missing", "A scenario objective needs a stable ID.");
                    continue;
                }

                if (!objectiveIds.Add(objective.id))
                {
                    context.Error(
                        "scenario.objective.id.duplicate",
                        $"Scenario objective ID '{objective.id}' is duplicated.");
                }

                if (!entities.TryGetValue(objective.entityId ?? string.Empty, out LevelEntity entity))
                {
                    context.Error(
                        "scenario.objective.entity.unknown",
                        $"Objective '{objective.id}' references an unknown entity "
                        + $"'{objective.entityId}'.",
                        objective.entityId);
                    continue;
                }

                bool pointExists = entity.interactionPoints.Any(point =>
                    point != null
                    && string.Equals(
                        point.id,
                        objective.interactionPointId,
                        StringComparison.Ordinal));
                if (!pointExists)
                {
                    context.Error(
                        "scenario.objective.interaction.unknown",
                        $"Objective '{objective.id}' references unknown interaction point "
                        + $"'{objective.interactionPointId}'.",
                        objective.entityId);
                }

                if (objective.actionPointCost < 0
                    || !LevelValidationMath.IsFinite(
                        objective.movementOpportunityCost)
                    || objective.movementOpportunityCost < 0f
                    || !IsSupportedMobility(objective.mobility))
                {
                    context.Error(
                        "scenario.objective.cost",
                        $"Objective '{objective.id}' has an invalid action cost.",
                        objective.entityId);
                }
                if (string.IsNullOrWhiteSpace(objective.actionId)
                    || string.IsNullOrWhiteSpace(objective.displayName))
                {
                    context.Error(
                        "scenario.objective.presentation",
                        $"Objective '{objective.id}' needs an action and display name.",
                        objective.entityId);
                }
            }
        }

        private static bool IsSupportedSizeClass(string value)
        {
            return string.Equals(value, "small", StringComparison.Ordinal)
                || string.Equals(value, "medium", StringComparison.Ordinal)
                || string.Equals(value, "large", StringComparison.Ordinal)
                || string.Equals(value, "huge", StringComparison.Ordinal);
        }

        private static bool IsSupportedMobility(string value)
        {
            return string.Equals(value, "mobile", StringComparison.Ordinal)
                || string.Equals(value, "momentum", StringComparison.Ordinal)
                || string.Equals(value, "set", StringComparison.Ordinal);
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
