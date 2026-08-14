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

            document.playtest = document.playtest ?? new LevelPlaytestData();
            document.playtest.playerStart = value;
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

    public sealed class CompositeLevelEditCommand : ILevelEditCommand
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
