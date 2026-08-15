using System;
using System.Collections.Generic;
using System.Linq;
using GritGud.Domain.Levels;

namespace GritGud.Application.Levels
{
    public interface ILevelEditCommand
    {
        string Description { get; }

        IReadOnlyCollection<string> AffectedEntityIds { get; }

        bool RequiresFullProjection { get; }

        void Apply(LevelDocument document);

        void Revert(LevelDocument document);
    }

    public interface ITerrainLevelEditCommand : ILevelEditCommand
    {
        string SurfaceId { get; }

        int StartX { get; }

        int StartZ { get; }

        int Width { get; }

        int Depth { get; }
    }

    public interface ILevelEditCommandGroup : ILevelEditCommand
    {
        IReadOnlyList<ILevelEditCommand> Commands { get; }
    }

    public interface ILevelEnvironmentEditCommand : ILevelEditCommand
    {
    }

    public interface ILevelBoundsEditCommand : ILevelEditCommand
    {
    }

    public interface ILevelOrganizationEditCommand : ILevelEditCommand
    {
    }

    public sealed class AddLevelGroupCommand : ILevelOrganizationEditCommand
    {
        private readonly LevelEntityGroupData group;

        public AddLevelGroupCommand(LevelEntityGroupData group)
        {
            this.group = group?.DeepCopy() ?? throw new ArgumentNullException(nameof(group));
        }

        public string Description => "Add entity group";
        public IReadOnlyCollection<string> AffectedEntityIds => Array.Empty<string>();
        public bool RequiresFullProjection => false;

        public void Apply(LevelDocument document)
        {
            AddEntityCommand.RequireDocument(document);
            if (document.groups.Any(candidate => string.Equals(
                    candidate?.id,
                    group.id,
                    StringComparison.Ordinal)))
            {
                throw new InvalidOperationException($"Entity group '{group.id}' already exists.");
            }
            document.groups.Add(group.DeepCopy());
        }

        public void Revert(LevelDocument document)
        {
            AddEntityCommand.RequireDocument(document);
            document.groups.RemoveAll(candidate => string.Equals(
                candidate?.id,
                group.id,
                StringComparison.Ordinal));
        }
    }

    public sealed class SetLevelGroupCommand : ILevelOrganizationEditCommand
    {
        private readonly string groupId;
        private readonly LevelEntityGroupData before;
        private readonly LevelEntityGroupData after;

        public SetLevelGroupCommand(
            string groupId,
            LevelEntityGroupData before,
            LevelEntityGroupData after)
        {
            this.groupId = string.IsNullOrWhiteSpace(groupId)
                ? throw new ArgumentException("A group ID is required.", nameof(groupId))
                : groupId;
            this.before = before?.DeepCopy() ?? throw new ArgumentNullException(nameof(before));
            this.after = after?.DeepCopy() ?? throw new ArgumentNullException(nameof(after));
        }

        public string Description => "Edit entity group";
        public IReadOnlyCollection<string> AffectedEntityIds => Array.Empty<string>();
        public bool RequiresFullProjection => false;
        public void Apply(LevelDocument document) => Set(document, after);
        public void Revert(LevelDocument document) => Set(document, before);

        private void Set(LevelDocument document, LevelEntityGroupData value)
        {
            AddEntityCommand.RequireDocument(document);
            int index = document.groups.FindIndex(candidate => string.Equals(
                candidate?.id,
                groupId,
                StringComparison.Ordinal));
            if (index < 0)
                throw new InvalidOperationException($"Entity group '{groupId}' does not exist.");
            document.groups[index] = value.DeepCopy();
        }
    }

    public sealed class SetEntityGroupCommand : ILevelOrganizationEditCommand
    {
        private readonly string entityId;
        private readonly string before;
        private readonly string after;

        public SetEntityGroupCommand(string entityId, string before, string after)
        {
            this.entityId = string.IsNullOrWhiteSpace(entityId)
                ? throw new ArgumentException("An entity ID is required.", nameof(entityId))
                : entityId;
            this.before = before ?? string.Empty;
            this.after = after ?? string.Empty;
        }

        public string Description => "Assign entity group";
        public IReadOnlyCollection<string> AffectedEntityIds => new[] { entityId };
        public bool RequiresFullProjection => false;
        public void Apply(LevelDocument document) => Set(document, after);
        public void Revert(LevelDocument document) => Set(document, before);

        private void Set(LevelDocument document, string groupId)
        {
            AddEntityCommand.RequireDocument(document);
            LevelEntity entity = document.entities.FirstOrDefault(candidate => string.Equals(
                candidate?.id,
                entityId,
                StringComparison.Ordinal));
            if (entity == null)
                throw new InvalidOperationException($"Entity '{entityId}' does not exist.");
            entity.groupId = groupId;
        }
    }

    public sealed class DeleteLevelGroupCommand : ILevelOrganizationEditCommand
    {
        private readonly string groupId;
        private LevelEntityGroupData removed;
        private int removedIndex;

        public DeleteLevelGroupCommand(string groupId)
        {
            this.groupId = string.IsNullOrWhiteSpace(groupId)
                ? throw new ArgumentException("A group ID is required.", nameof(groupId))
                : groupId;
        }

        public string Description => "Delete entity group";
        public IReadOnlyCollection<string> AffectedEntityIds => Array.Empty<string>();
        public bool RequiresFullProjection => false;

        public void Apply(LevelDocument document)
        {
            AddEntityCommand.RequireDocument(document);
            if (document.entities.Any(entity => string.Equals(
                    entity?.groupId,
                    groupId,
                    StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    $"Entity group '{groupId}' must be empty before deletion.");
            }
            removedIndex = document.groups.FindIndex(candidate => string.Equals(
                candidate?.id,
                groupId,
                StringComparison.Ordinal));
            if (removedIndex < 0)
                throw new InvalidOperationException($"Entity group '{groupId}' does not exist.");
            removed = document.groups[removedIndex].DeepCopy();
            document.groups.RemoveAt(removedIndex);
        }

        public void Revert(LevelDocument document)
        {
            AddEntityCommand.RequireDocument(document);
            document.groups.Insert(Math.Min(removedIndex, document.groups.Count), removed.DeepCopy());
        }
    }

    public sealed class SetLevelBoundsCommand : ILevelBoundsEditCommand
    {
        private readonly LevelBoundsData before;
        private readonly LevelBoundsData after;

        public SetLevelBoundsCommand(LevelBoundsData before, LevelBoundsData after)
        {
            this.before = before;
            this.after = after;
        }

        public string Description => "Edit level bounds";

        public IReadOnlyCollection<string> AffectedEntityIds => Array.Empty<string>();

        public bool RequiresFullProjection => false;

        public void Apply(LevelDocument document) => Set(document, after);

        public void Revert(LevelDocument document) => Set(document, before);

        private static void Set(LevelDocument document, LevelBoundsData value)
        {
            AddEntityCommand.RequireDocument(document);
            document.bounds = value;
        }
    }

    public sealed class SetLevelEnvironmentCommand : ILevelEnvironmentEditCommand
    {
        private readonly LevelEnvironmentData before;
        private readonly LevelEnvironmentData after;

        public SetLevelEnvironmentCommand(
            LevelEnvironmentData before,
            LevelEnvironmentData after,
            string description = "Edit level environment")
        {
            this.before = before?.DeepCopy() ?? throw new ArgumentNullException(nameof(before));
            this.after = after?.DeepCopy() ?? throw new ArgumentNullException(nameof(after));
            Description = string.IsNullOrWhiteSpace(description)
                ? "Edit level environment"
                : description.Trim();
        }

        public string Description { get; }

        public IReadOnlyCollection<string> AffectedEntityIds => Array.Empty<string>();

        public bool RequiresFullProjection => false;

        public void Apply(LevelDocument document) => Set(document, after);

        public void Revert(LevelDocument document) => Set(document, before);

        private static void Set(LevelDocument document, LevelEnvironmentData environment)
        {
            AddEntityCommand.RequireDocument(document);
            document.environment = environment.DeepCopy();
        }
    }

    public sealed class SetLevelDisplayNameCommand : ILevelEditCommand
    {
        private readonly string before;
        private readonly string after;

        public SetLevelDisplayNameCommand(string before, string after)
        {
            this.before = before ?? string.Empty;
            this.after = string.IsNullOrWhiteSpace(after)
                ? throw new ArgumentException("A level display name is required.", nameof(after))
                : after.Trim();
        }

        public string Description => "Rename level";

        public IReadOnlyCollection<string> AffectedEntityIds => Array.Empty<string>();

        public bool RequiresFullProjection => false;

        public void Apply(LevelDocument document) => Set(document, after);

        public void Revert(LevelDocument document) => Set(document, before);

        private static void Set(LevelDocument document, string value)
        {
            AddEntityCommand.RequireDocument(document);
            document.displayName = value;
        }
    }

    public sealed class SetPlayerStartCommand : ILevelEditCommand
    {
        private readonly LevelTransformData before;
        private readonly LevelTransformData after;

        public SetPlayerStartCommand(LevelTransformData before, LevelTransformData after)
        {
            this.before = before;
            this.after = after;
        }

        public string Description => "Set player start";

        public IReadOnlyCollection<string> AffectedEntityIds => Array.Empty<string>();

        public bool RequiresFullProjection => false;

        public void Apply(LevelDocument document)
        {
            Set(document, after);
        }

        public void Revert(LevelDocument document)
        {
            Set(document, before);
        }

        private static void Set(LevelDocument document, LevelTransformData value)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            LevelScenarioActorData player = document.scenario?
                .FindInitiallySelectedPlayer();
            if (player == null)
            {
                throw new InvalidOperationException(
                    "The scenario does not define an initially selected player actor.");
            }

            player.transform = value;
        }
    }

    public sealed class SetTerrainHeightsCommand : ITerrainLevelEditCommand
    {
        private readonly int[] after;
        private int[] before;

        public SetTerrainHeightsCommand(
            string surfaceId,
            int startX,
            int startZ,
            int width,
            int depth,
            IEnumerable<int> heightSamples)
        {
            SurfaceId = string.IsNullOrWhiteSpace(surfaceId)
                ? throw new ArgumentException("A terrain surface ID is required.", nameof(surfaceId))
                : surfaceId;
            if (startX < 0 || startZ < 0 || width <= 0 || depth <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(width), "The terrain patch must be positive.");
            }

            StartX = startX;
            StartZ = startZ;
            Width = width;
            Depth = depth;
            long expectedSampleCount = (long)width * depth;
            if (expectedSampleCount > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(width),
                    "The terrain patch contains too many samples.");
            }

            after = heightSamples?.ToArray() ?? throw new ArgumentNullException(nameof(heightSamples));
            if (after.Length != (int)expectedSampleCount)
            {
                throw new ArgumentException("The terrain patch sample count does not match its size.", nameof(heightSamples));
            }
        }

        public string Description => "Edit terrain heights";

        public IReadOnlyCollection<string> AffectedEntityIds => Array.Empty<string>();

        public bool RequiresFullProjection => false;

        public string SurfaceId { get; }

        public int StartX { get; }

        public int StartZ { get; }

        public int Width { get; }

        public int Depth { get; }

        public void Apply(LevelDocument document)
        {
            TerrainSurfaceData surface = RequireSurface(document);
            if (before == null)
            {
                before = ReadPatch(surface);
            }

            WritePatch(surface, after);
        }

        public void Revert(LevelDocument document)
        {
            if (before == null)
            {
                throw new InvalidOperationException("The terrain command has not been applied.");
            }

            WritePatch(RequireSurface(document), before);
        }

        private TerrainSurfaceData RequireSurface(LevelDocument document)
        {
            AddEntityCommand.RequireDocument(document);
            TerrainSurfaceData surface = document.terrainSurfaces.FirstOrDefault(candidate =>
                string.Equals(candidate?.id, SurfaceId, StringComparison.Ordinal));
            if (surface == null)
            {
                throw new InvalidOperationException($"Terrain surface '{SurfaceId}' does not exist.");
            }

            if (StartX + Width > surface.sampleCountX || StartZ + Depth > surface.sampleCountZ)
            {
                throw new InvalidOperationException("The terrain patch extends outside the surface.");
            }

            if (surface.heightSamples.Count != surface.sampleCountX * surface.sampleCountZ)
            {
                throw new InvalidOperationException(
                    $"Terrain surface '{SurfaceId}' does not have a complete sample array.");
            }

            return surface;
        }

        private int[] ReadPatch(TerrainSurfaceData surface)
        {
            var result = new int[after.Length];
            for (int z = 0; z < Depth; z++)
            {
                for (int x = 0; x < Width; x++)
                {
                    result[z * Width + x] = surface.heightSamples[
                        (StartZ + z) * surface.sampleCountX + StartX + x];
                }
            }

            return result;
        }

        private void WritePatch(TerrainSurfaceData surface, IReadOnlyList<int> values)
        {
            for (int z = 0; z < Depth; z++)
            {
                for (int x = 0; x < Width; x++)
                {
                    surface.heightSamples[(StartZ + z) * surface.sampleCountX + StartX + x] =
                        values[z * Width + x];
                }
            }
        }
    }

    public sealed class AddTerrainSurfaceCommand : ILevelEditCommand
    {
        private readonly TerrainSurfaceData surface;
        private int insertionIndex = -1;

        public AddTerrainSurfaceCommand(TerrainSurfaceData surface)
        {
            this.surface = surface?.DeepCopy() ?? throw new ArgumentNullException(nameof(surface));
            if (string.IsNullOrWhiteSpace(this.surface.id))
            {
                throw new ArgumentException("The terrain surface needs a stable ID.", nameof(surface));
            }
        }

        public string Description => "Add terrain surface";

        public IReadOnlyCollection<string> AffectedEntityIds => Array.Empty<string>();

        public bool RequiresFullProjection => true;

        public void Apply(LevelDocument document)
        {
            AddEntityCommand.RequireDocument(document);
            if (FindSurfaceIndex(document, surface.id) >= 0)
            {
                throw new InvalidOperationException(
                    $"Terrain surface '{surface.id}' already exists.");
            }

            if (insertionIndex < 0 || insertionIndex > document.terrainSurfaces.Count)
            {
                insertionIndex = document.terrainSurfaces.Count;
            }

            document.terrainSurfaces.Insert(insertionIndex, surface.DeepCopy());
        }

        public void Revert(LevelDocument document)
        {
            int index = RequireSurfaceIndex(document, surface.id);
            insertionIndex = index;
            document.terrainSurfaces.RemoveAt(index);
        }

        internal static int FindSurfaceIndex(LevelDocument document, string surfaceId)
        {
            for (int index = 0; index < document.terrainSurfaces.Count; index++)
            {
                if (string.Equals(
                    document.terrainSurfaces[index]?.id,
                    surfaceId,
                    StringComparison.Ordinal))
                {
                    return index;
                }
            }

            return -1;
        }

        internal static int RequireSurfaceIndex(LevelDocument document, string surfaceId)
        {
            AddEntityCommand.RequireDocument(document);
            int index = FindSurfaceIndex(document, surfaceId);
            if (index < 0)
            {
                throw new InvalidOperationException(
                    $"Terrain surface '{surfaceId}' does not exist.");
            }

            return index;
        }
    }

    public sealed class SetTerrainSurfaceCommand : ILevelEditCommand
    {
        private readonly string surfaceId;
        private readonly TerrainSurfaceData before;
        private readonly TerrainSurfaceData after;

        public SetTerrainSurfaceCommand(
            string surfaceId,
            TerrainSurfaceData before,
            TerrainSurfaceData after)
        {
            this.surfaceId = string.IsNullOrWhiteSpace(surfaceId)
                ? throw new ArgumentException("A terrain surface ID is required.", nameof(surfaceId))
                : surfaceId;
            this.before = RequireMatchingSurface(before, nameof(before));
            this.after = RequireMatchingSurface(after, nameof(after));
        }

        public string Description => "Resize terrain surface";

        public IReadOnlyCollection<string> AffectedEntityIds => Array.Empty<string>();

        public bool RequiresFullProjection => true;

        public void Apply(LevelDocument document)
        {
            Replace(document, after);
        }

        public void Revert(LevelDocument document)
        {
            Replace(document, before);
        }

        private TerrainSurfaceData RequireMatchingSurface(
            TerrainSurfaceData value,
            string parameterName)
        {
            TerrainSurfaceData copy = value?.DeepCopy()
                ?? throw new ArgumentNullException(parameterName);
            if (!string.Equals(copy.id, surfaceId, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    $"Terrain surface '{copy.id}' does not match '{surfaceId}'.",
                    parameterName);
            }

            return copy;
        }

        private void Replace(LevelDocument document, TerrainSurfaceData replacement)
        {
            int index = AddTerrainSurfaceCommand.RequireSurfaceIndex(document, surfaceId);
            document.terrainSurfaces[index] = replacement.DeepCopy();
        }
    }

    public sealed class AddEntityCommand : ILevelEditCommand
    {
        private readonly LevelEntity entity;
        private int insertionIndex = -1;

        public AddEntityCommand(LevelEntity entity)
        {
            this.entity = entity?.DeepCopy() ?? throw new ArgumentNullException(nameof(entity));
            if (string.IsNullOrWhiteSpace(this.entity.id))
            {
                throw new ArgumentException("The entity needs a stable ID.", nameof(entity));
            }
        }

        public string Description => "Place entity";

        public IReadOnlyCollection<string> AffectedEntityIds => new[] { entity.id };

        public bool RequiresFullProjection => false;

        public void Apply(LevelDocument document)
        {
            RequireDocument(document);
            if (FindEntityIndex(document, entity.id) >= 0)
            {
                throw new InvalidOperationException($"Entity '{entity.id}' already exists.");
            }

            if (insertionIndex < 0 || insertionIndex > document.entities.Count)
            {
                insertionIndex = document.entities.Count;
            }

            document.entities.Insert(insertionIndex, entity.DeepCopy());
        }

        public void Revert(LevelDocument document)
        {
            int index = RequireEntityIndex(document, entity.id);
            insertionIndex = index;
            document.entities.RemoveAt(index);
        }

        internal static int FindEntityIndex(LevelDocument document, string entityId)
        {
            for (int index = 0; index < document.entities.Count; index++)
            {
                if (string.Equals(document.entities[index]?.id, entityId, StringComparison.Ordinal))
                {
                    return index;
                }
            }

            return -1;
        }

        internal static int RequireEntityIndex(LevelDocument document, string entityId)
        {
            RequireDocument(document);
            int index = FindEntityIndex(document, entityId);
            if (index < 0)
            {
                throw new InvalidOperationException($"Entity '{entityId}' does not exist.");
            }

            return index;
        }

        internal static void RequireDocument(LevelDocument document)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            document.Normalize();
        }
    }

    public sealed class DeleteEntityCommand : ILevelEditCommand
    {
        private readonly string entityId;
        private LevelEntity deletedEntity;
        private LevelScenarioData scenarioBeforeDeletion;
        private int deletedIndex = -1;

        public DeleteEntityCommand(string entityId)
        {
            this.entityId = string.IsNullOrWhiteSpace(entityId)
                ? throw new ArgumentException("An entity ID is required.", nameof(entityId))
                : entityId;
        }

        public string Description => "Delete entity";

        public IReadOnlyCollection<string> AffectedEntityIds => new[] { entityId };

        public bool RequiresFullProjection => false;

        public void Apply(LevelDocument document)
        {
            deletedIndex = AddEntityCommand.RequireEntityIndex(document, entityId);
            deletedEntity = document.entities[deletedIndex].DeepCopy();
            scenarioBeforeDeletion = document.scenario.DeepCopy();
            document.scenario.objectives.RemoveAll(objective =>
                string.Equals(objective?.entityId, entityId, StringComparison.Ordinal));
            document.scenario.props.RemoveAll(prop =>
                string.Equals(prop?.entityId, entityId, StringComparison.Ordinal));
            document.scenario.vehicles.RemoveAll(vehicle =>
                string.Equals(vehicle?.entityId, entityId, StringComparison.Ordinal));
            document.entities.RemoveAt(deletedIndex);
        }

        public void Revert(LevelDocument document)
        {
            AddEntityCommand.RequireDocument(document);
            if (deletedEntity == null || deletedIndex < 0)
            {
                throw new InvalidOperationException("The delete command has not been applied.");
            }

            if (AddEntityCommand.FindEntityIndex(document, entityId) >= 0)
            {
                throw new InvalidOperationException($"Entity '{entityId}' already exists.");
            }

            document.entities.Insert(Math.Min(deletedIndex, document.entities.Count), deletedEntity.DeepCopy());
            document.scenario = scenarioBeforeDeletion?.DeepCopy()
                ?? throw new InvalidOperationException(
                    "The deleted entity did not capture its scenario links.");
        }
    }

    public sealed class SetEntityTransformCommand : ILevelEditCommand
    {
        private readonly string entityId;
        private readonly LevelTransformData before;
        private readonly LevelTransformData after;

        public SetEntityTransformCommand(
            string entityId,
            LevelTransformData before,
            LevelTransformData after)
        {
            this.entityId = string.IsNullOrWhiteSpace(entityId)
                ? throw new ArgumentException("An entity ID is required.", nameof(entityId))
                : entityId;
            this.before = before;
            this.after = after;
        }

        public string Description => "Transform entity";

        public IReadOnlyCollection<string> AffectedEntityIds => new[] { entityId };

        public bool RequiresFullProjection => false;

        public void Apply(LevelDocument document)
        {
            SetTransform(document, after);
        }

        public void Revert(LevelDocument document)
        {
            SetTransform(document, before);
        }

        private void SetTransform(LevelDocument document, LevelTransformData value)
        {
            int index = AddEntityCommand.RequireEntityIndex(document, entityId);
            document.entities[index].transform = value;
        }
    }

    public sealed class AddInteractionPointCommand : ILevelEditCommand
    {
        private readonly string entityId;
        private readonly InteractionPointData point;
        private int insertionIndex = -1;

        public AddInteractionPointCommand(string entityId, InteractionPointData point)
        {
            this.entityId = RequireEntityId(entityId);
            this.point = point?.DeepCopy() ?? throw new ArgumentNullException(nameof(point));
            if (string.IsNullOrWhiteSpace(this.point.id))
            {
                throw new ArgumentException("An interaction point needs a stable ID.", nameof(point));
            }
        }

        public string Description => "Add interaction point";

        public IReadOnlyCollection<string> AffectedEntityIds => new[] { entityId };

        public bool RequiresFullProjection => false;

        public void Apply(LevelDocument document)
        {
            LevelEntity entity = RequireEntity(document, entityId);
            if (FindPointIndex(entity, point.id) >= 0)
            {
                throw new InvalidOperationException(
                    $"Interaction point '{point.id}' already exists on entity '{entityId}'.");
            }

            if (insertionIndex < 0 || insertionIndex > entity.interactionPoints.Count)
            {
                insertionIndex = entity.interactionPoints.Count;
            }

            entity.interactionPoints.Insert(insertionIndex, point.DeepCopy());
        }

        public void Revert(LevelDocument document)
        {
            LevelEntity entity = RequireEntity(document, entityId);
            int index = RequirePointIndex(entity, point.id);
            insertionIndex = index;
            entity.interactionPoints.RemoveAt(index);
        }

        internal static string RequireEntityId(string value) => string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("An entity ID is required.", nameof(value))
            : value;

        internal static LevelEntity RequireEntity(LevelDocument document, string entityId)
        {
            int index = AddEntityCommand.RequireEntityIndex(document, entityId);
            return document.entities[index];
        }

        internal static int FindPointIndex(LevelEntity entity, string pointId)
        {
            for (int index = 0; index < entity.interactionPoints.Count; index++)
            {
                if (string.Equals(entity.interactionPoints[index]?.id, pointId, StringComparison.Ordinal))
                {
                    return index;
                }
            }

            return -1;
        }

        internal static int RequirePointIndex(LevelEntity entity, string pointId)
        {
            int index = FindPointIndex(entity, pointId);
            if (index < 0)
            {
                throw new InvalidOperationException(
                    $"Interaction point '{pointId}' does not exist on entity '{entity.id}'.");
            }

            return index;
        }
    }

    public sealed class SetInteractionPointCommand : ILevelEditCommand
    {
        private readonly string entityId;
        private readonly string pointId;
        private readonly InteractionPointData before;
        private readonly InteractionPointData after;

        public SetInteractionPointCommand(
            string entityId,
            string pointId,
            InteractionPointData before,
            InteractionPointData after)
        {
            this.entityId = AddInteractionPointCommand.RequireEntityId(entityId);
            this.pointId = string.IsNullOrWhiteSpace(pointId)
                ? throw new ArgumentException("An interaction point ID is required.", nameof(pointId))
                : pointId;
            this.before = before?.DeepCopy() ?? throw new ArgumentNullException(nameof(before));
            this.after = after?.DeepCopy() ?? throw new ArgumentNullException(nameof(after));
            if (!string.Equals(this.before.id, this.pointId, StringComparison.Ordinal)
                || !string.Equals(this.after.id, this.pointId, StringComparison.Ordinal))
            {
                throw new ArgumentException("Interaction point commands cannot change point identity.");
            }
        }

        public string Description => "Edit interaction point";

        public IReadOnlyCollection<string> AffectedEntityIds => new[] { entityId };

        public bool RequiresFullProjection => false;

        public void Apply(LevelDocument document) => Set(document, after);

        public void Revert(LevelDocument document) => Set(document, before);

        private void Set(LevelDocument document, InteractionPointData value)
        {
            LevelEntity entity = AddInteractionPointCommand.RequireEntity(document, entityId);
            entity.interactionPoints[AddInteractionPointCommand.RequirePointIndex(entity, pointId)] =
                value.DeepCopy();
        }
    }

    public sealed class DeleteInteractionPointCommand : ILevelEditCommand
    {
        private readonly string entityId;
        private readonly string pointId;
        private InteractionPointData deletedPoint;
        private LevelScenarioData scenarioBeforeDeletion;
        private int deletedIndex = -1;

        public DeleteInteractionPointCommand(string entityId, string pointId)
        {
            this.entityId = AddInteractionPointCommand.RequireEntityId(entityId);
            this.pointId = string.IsNullOrWhiteSpace(pointId)
                ? throw new ArgumentException("An interaction point ID is required.", nameof(pointId))
                : pointId;
        }

        public string Description => "Delete interaction point";

        public IReadOnlyCollection<string> AffectedEntityIds => new[] { entityId };

        public bool RequiresFullProjection => false;

        public void Apply(LevelDocument document)
        {
            LevelEntity entity = AddInteractionPointCommand.RequireEntity(document, entityId);
            deletedIndex = AddInteractionPointCommand.RequirePointIndex(entity, pointId);
            deletedPoint = entity.interactionPoints[deletedIndex].DeepCopy();
            scenarioBeforeDeletion = document.scenario.DeepCopy();
            document.scenario.objectives.RemoveAll(objective =>
                string.Equals(objective?.entityId, entityId, StringComparison.Ordinal)
                && string.Equals(
                    objective?.interactionPointId,
                    pointId,
                    StringComparison.Ordinal));
            entity.interactionPoints.RemoveAt(deletedIndex);
        }

        public void Revert(LevelDocument document)
        {
            LevelEntity entity = AddInteractionPointCommand.RequireEntity(document, entityId);
            if (deletedPoint == null || deletedIndex < 0)
            {
                throw new InvalidOperationException("The interaction-point delete command has not been applied.");
            }

            if (AddInteractionPointCommand.FindPointIndex(entity, pointId) >= 0)
            {
                throw new InvalidOperationException(
                    $"Interaction point '{pointId}' already exists on entity '{entityId}'.");
            }

            entity.interactionPoints.Insert(
                Math.Min(deletedIndex, entity.interactionPoints.Count),
                deletedPoint.DeepCopy());
            document.scenario = scenarioBeforeDeletion?.DeepCopy()
                ?? throw new InvalidOperationException(
                    "The deleted interaction point did not capture its scenario links.");
        }
    }

    public sealed class SetDestructibleInstanceCommand : ILevelEditCommand
    {
        private readonly string entityId;
        private readonly DestructibleInstanceData before;
        private readonly DestructibleInstanceData after;

        public SetDestructibleInstanceCommand(
            string entityId,
            DestructibleInstanceData before,
            DestructibleInstanceData after)
        {
            this.entityId = AddInteractionPointCommand.RequireEntityId(entityId);
            this.before = before?.DeepCopy();
            this.after = after?.DeepCopy();
        }

        public string Description => "Edit destructible defaults";

        public IReadOnlyCollection<string> AffectedEntityIds => new[] { entityId };

        public bool RequiresFullProjection => false;

        public void Apply(LevelDocument document) => Set(document, after);

        public void Revert(LevelDocument document) => Set(document, before);

        private void Set(LevelDocument document, DestructibleInstanceData value)
        {
            AddInteractionPointCommand.RequireEntity(document, entityId).destructible = value?.DeepCopy();
        }
    }

    public sealed class AddScenarioActorCommand : ILevelEditCommand
    {
        private readonly LevelScenarioActorData actor;
        private int insertionIndex = -1;

        public AddScenarioActorCommand(LevelScenarioActorData actor)
        {
            this.actor = actor?.DeepCopy() ?? throw new ArgumentNullException(nameof(actor));
            if (string.IsNullOrWhiteSpace(this.actor.id))
                throw new ArgumentException("A scenario actor ID is required.", nameof(actor));
        }

        public string Description => "Add scenario actor";

        public IReadOnlyCollection<string> AffectedEntityIds => Array.Empty<string>();

        public bool RequiresFullProjection => false;

        public void Apply(LevelDocument document)
        {
            LevelScenarioData scenario = RequireScenario(document);
            if (FindActorIndex(scenario, actor.id) >= 0)
                throw new InvalidOperationException($"Scenario actor '{actor.id}' already exists.");
            if (insertionIndex < 0 || insertionIndex > scenario.actors.Count)
                insertionIndex = scenario.actors.Count;
            scenario.actors.Insert(insertionIndex, actor.DeepCopy());
        }

        public void Revert(LevelDocument document)
        {
            LevelScenarioData scenario = RequireScenario(document);
            insertionIndex = RequireActorIndex(scenario, actor.id);
            scenario.actors.RemoveAt(insertionIndex);
        }

        internal static LevelScenarioData RequireScenario(LevelDocument document)
        {
            AddEntityCommand.RequireDocument(document);
            return document.scenario ?? throw new InvalidOperationException(
                "The level does not define scenario data.");
        }

        internal static int FindActorIndex(LevelScenarioData scenario, string actorId)
        {
            for (int index = 0; index < scenario.actors.Count; index++)
            {
                if (string.Equals(scenario.actors[index]?.id, actorId, StringComparison.Ordinal))
                    return index;
            }

            return -1;
        }

        internal static int RequireActorIndex(LevelScenarioData scenario, string actorId)
        {
            int index = FindActorIndex(scenario, actorId);
            if (index < 0)
                throw new InvalidOperationException($"Scenario actor '{actorId}' does not exist.");
            return index;
        }
    }

    public sealed class SetScenarioActorCommand : ILevelEditCommand
    {
        private readonly string actorId;
        private readonly LevelScenarioActorData before;
        private readonly LevelScenarioActorData after;

        public SetScenarioActorCommand(
            string actorId,
            LevelScenarioActorData before,
            LevelScenarioActorData after)
        {
            this.actorId = string.IsNullOrWhiteSpace(actorId)
                ? throw new ArgumentException("A scenario actor ID is required.", nameof(actorId))
                : actorId;
            this.before = before?.DeepCopy() ?? throw new ArgumentNullException(nameof(before));
            this.after = after?.DeepCopy() ?? throw new ArgumentNullException(nameof(after));
            if (!string.Equals(this.before.id, actorId, StringComparison.Ordinal)
                || !string.Equals(this.after.id, actorId, StringComparison.Ordinal))
            {
                throw new ArgumentException("Scenario actor edits cannot change actor identity.");
            }
        }

        public string Description => "Edit scenario actor";

        public IReadOnlyCollection<string> AffectedEntityIds => Array.Empty<string>();

        public bool RequiresFullProjection => false;

        public void Apply(LevelDocument document) => Set(document, after);

        public void Revert(LevelDocument document) => Set(document, before);

        private void Set(LevelDocument document, LevelScenarioActorData value)
        {
            LevelScenarioData scenario = AddScenarioActorCommand.RequireScenario(document);
            scenario.actors[AddScenarioActorCommand.RequireActorIndex(scenario, actorId)] =
                value.DeepCopy();
        }
    }

    public sealed class DeleteScenarioActorCommand : ILevelEditCommand
    {
        private readonly string actorId;
        private LevelScenarioActorData deletedActor;
        private int deletedIndex = -1;

        public DeleteScenarioActorCommand(string actorId)
        {
            this.actorId = string.IsNullOrWhiteSpace(actorId)
                ? throw new ArgumentException("A scenario actor ID is required.", nameof(actorId))
                : actorId;
        }

        public string Description => "Delete scenario actor";

        public IReadOnlyCollection<string> AffectedEntityIds => Array.Empty<string>();

        public bool RequiresFullProjection => false;

        public void Apply(LevelDocument document)
        {
            LevelScenarioData scenario = AddScenarioActorCommand.RequireScenario(document);
            deletedIndex = AddScenarioActorCommand.RequireActorIndex(scenario, actorId);
            deletedActor = scenario.actors[deletedIndex].DeepCopy();
            scenario.actors.RemoveAt(deletedIndex);
        }

        public void Revert(LevelDocument document)
        {
            LevelScenarioData scenario = AddScenarioActorCommand.RequireScenario(document);
            if (deletedActor == null || deletedIndex < 0)
                throw new InvalidOperationException("The scenario actor delete has not been applied.");
            if (AddScenarioActorCommand.FindActorIndex(scenario, actorId) >= 0)
                throw new InvalidOperationException($"Scenario actor '{actorId}' already exists.");
            scenario.actors.Insert(
                Math.Min(deletedIndex, scenario.actors.Count),
                deletedActor.DeepCopy());
        }
    }

    public sealed class SetScenarioConfigurationCommand : ILevelEditCommand
    {
        private readonly LevelScenarioData before;
        private readonly LevelScenarioData after;
        private readonly string[] affectedEntityIds;

        public SetScenarioConfigurationCommand(
            string description,
            LevelScenarioData before,
            LevelScenarioData after,
            IEnumerable<string> affectedEntityIds = null)
        {
            Description = string.IsNullOrWhiteSpace(description)
                ? throw new ArgumentException("A scenario edit description is required.", nameof(description))
                : description;
            this.before = before?.DeepCopy() ?? throw new ArgumentNullException(nameof(before));
            this.after = after?.DeepCopy() ?? throw new ArgumentNullException(nameof(after));
            this.affectedEntityIds = affectedEntityIds?
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .ToArray() ?? Array.Empty<string>();
        }

        public string Description { get; }

        public IReadOnlyCollection<string> AffectedEntityIds => affectedEntityIds;

        public bool RequiresFullProjection => false;

        public void Apply(LevelDocument document) => Set(document, after);

        public void Revert(LevelDocument document) => Set(document, before);

        private static void Set(LevelDocument document, LevelScenarioData value)
        {
            AddEntityCommand.RequireDocument(document);
            document.scenario = value.DeepCopy();
        }
    }

    public sealed class CompositeLevelEditCommand : ILevelEditCommandGroup
    {
        private readonly ILevelEditCommand[] commands;
        private readonly string[] affectedEntityIds;

        public CompositeLevelEditCommand(string description, IEnumerable<ILevelEditCommand> commands)
        {
            Description = string.IsNullOrWhiteSpace(description)
                ? throw new ArgumentException("A transaction description is required.", nameof(description))
                : description;
            this.commands = commands?.Where(command => command != null).ToArray()
                ?? throw new ArgumentNullException(nameof(commands));
            if (this.commands.Length == 0)
            {
                throw new ArgumentException("A transaction needs at least one command.", nameof(commands));
            }

            affectedEntityIds = this.commands
                .SelectMany(command => command.AffectedEntityIds)
                .Where(entityId => !string.IsNullOrWhiteSpace(entityId))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        public string Description { get; }

        public IReadOnlyCollection<string> AffectedEntityIds => affectedEntityIds;

        public IReadOnlyList<ILevelEditCommand> Commands => commands;

        public bool RequiresFullProjection => commands.Any(command => command.RequiresFullProjection);

        public void Apply(LevelDocument document)
        {
            int appliedCount = 0;
            try
            {
                for (; appliedCount < commands.Length; appliedCount++)
                {
                    commands[appliedCount].Apply(document);
                }
            }
            catch
            {
                for (int index = appliedCount - 1; index >= 0; index--)
                {
                    commands[index].Revert(document);
                }

                throw;
            }
        }

        public void Revert(LevelDocument document)
        {
            int revertedCount = 0;
            try
            {
                for (int index = commands.Length - 1; index >= 0; index--)
                {
                    commands[index].Revert(document);
                    revertedCount++;
                }
            }
            catch
            {
                int firstRevertedIndex = commands.Length - revertedCount;
                for (int index = firstRevertedIndex; index < commands.Length; index++)
                {
                    commands[index].Apply(document);
                }

                throw;
            }
        }
    }
}
