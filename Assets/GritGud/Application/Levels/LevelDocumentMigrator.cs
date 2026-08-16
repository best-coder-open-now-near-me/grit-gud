using System;
using System.Collections.Generic;
using GritGud.Domain.Levels;

namespace GritGud.Application.Levels
{
    public interface ILevelDocumentMigration
    {
        int SourceVersion { get; }

        int TargetVersion { get; }

        LevelDocument Migrate(LevelDocument source);
    }

    public sealed class LevelDocumentMigrator
    {
        private readonly Dictionary<int, ILevelDocumentMigration> migrations;

        public LevelDocumentMigrator(IEnumerable<ILevelDocumentMigration> migrations = null)
        {
            this.migrations = new Dictionary<int, ILevelDocumentMigration>();
            if (migrations == null)
            {
                migrations = new ILevelDocumentMigration[]
                {
                    new LevelDocumentV1ToV2Migration(),
                    new LevelDocumentV2ToV3Migration(),
                    new LevelDocumentV3ToV4Migration(),
                    new LevelDocumentV4ToV5Migration(),
                    new LevelDocumentV5ToV6Migration(),
                    new LevelDocumentV6ToV7Migration(),
                    new LevelDocumentV7ToV8Migration(),
                    new LevelDocumentV8ToV9Migration(),
                    new LevelDocumentV9ToV10Migration(),
                    new LevelDocumentV10ToV11Migration(),
                    new LevelDocumentV11ToV12Migration(),
                    new LevelDocumentV12ToV13Migration(),
                    new LevelDocumentV13ToV14Migration(),
                    new LevelDocumentV14ToV15Migration(),
                };
            }

            foreach (ILevelDocumentMigration migration in migrations)
            {
                if (migration == null)
                {
                    continue;
                }

                if (migration.TargetVersion <= migration.SourceVersion)
                {
                    throw new ArgumentException(
                        "Level migrations must advance the schema version.",
                        nameof(migrations));
                }

                if (!this.migrations.TryAdd(migration.SourceVersion, migration))
                {
                    throw new ArgumentException(
                        $"More than one migration starts at schema {migration.SourceVersion}.",
                        nameof(migrations));
                }
            }
        }

        public LevelDocument MigrateToCurrent(LevelDocument source)
        {
            LevelDocument document = source?.DeepCopy()
                ?? throw new ArgumentNullException(nameof(source));
            if (document.schemaVersion > LevelDocument.CurrentSchemaVersion)
            {
                throw new InvalidOperationException(
                    $"Schema version {document.schemaVersion} is newer than the supported version "
                    + $"{LevelDocument.CurrentSchemaVersion}.");
            }

            var visitedVersions = new HashSet<int>();
            while (document.schemaVersion < LevelDocument.CurrentSchemaVersion)
            {
                if (!visitedVersions.Add(document.schemaVersion))
                {
                    throw new InvalidOperationException("The level migration chain contains a cycle.");
                }

                if (!migrations.TryGetValue(document.schemaVersion, out ILevelDocumentMigration migration))
                {
                    throw new InvalidOperationException(
                        $"No migration is registered for schema version {document.schemaVersion}.");
                }

                document = migration.Migrate(document)
                    ?? throw new InvalidOperationException(
                        $"The schema {migration.SourceVersion} migration returned no document.");
                if (document.schemaVersion != migration.TargetVersion)
                {
                    throw new InvalidOperationException(
                        $"The schema {migration.SourceVersion} migration did not produce "
                        + $"schema {migration.TargetVersion}.");
                }
            }

            document.Normalize();
            return document;
        }
    }

    public sealed class LevelDocumentV1ToV2Migration : ILevelDocumentMigration
    {
        public int SourceVersion => 1;

        public int TargetVersion => 2;

        public LevelDocument Migrate(LevelDocument source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            LevelDocument migrated = source.DeepCopy();
            migrated.terrainSurfaces = migrated.terrainSurfaces
                ?? new List<TerrainSurfaceData>();
            migrated.schemaVersion = TargetVersion;
            return migrated;
        }
    }

    public sealed class LevelDocumentV2ToV3Migration : ILevelDocumentMigration
    {
        public int SourceVersion => 2;

        public int TargetVersion => 3;

        public LevelDocument Migrate(LevelDocument source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            LevelDocument migrated = source.DeepCopy();
            migrated.legacyPlaytest = migrated.legacyPlaytest ?? new LevelPlaytestData();
            migrated.legacyPlaytest.playerStart = new LevelTransformData(
                new Float3Data(
                    migrated.bounds.center.x,
                    migrated.bounds.center.y + (migrated.bounds.size.y * 0.5f),
                    migrated.bounds.center.z),
                0f);
            migrated.schemaVersion = TargetVersion;
            return migrated;
        }
    }

    public sealed class LevelDocumentV3ToV4Migration : ILevelDocumentMigration
    {
        public int SourceVersion => 3;

        public int TargetVersion => 4;

        public LevelDocument Migrate(LevelDocument source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            LevelDocument migrated = source.DeepCopy();
            LevelTransformData playerStart = migrated.legacyPlaytest != null
                ? migrated.legacyPlaytest.playerStart
                : new LevelTransformData(
                    new Float3Data(
                        migrated.bounds.center.x,
                        migrated.bounds.center.y + (migrated.bounds.size.y * 0.5f),
                        migrated.bounds.center.z),
                    0f);
            migrated.scenario = new LevelScenarioData
            {
                actors = new List<LevelScenarioActorData>
                {
                    new LevelScenarioActorData
                    {
                        id = "player",
                        templateId = "player",
                        transform = playerStart,
                        playerControlled = true,
                        initiallySelected = true,
                    },
                },
            };
            migrated.legacyPlaytest = null;
            migrated.schemaVersion = TargetVersion;
            return migrated;
        }
    }

    public sealed class LevelDocumentV4ToV5Migration : ILevelDocumentMigration
    {
        public int SourceVersion => 4;

        public int TargetVersion => 5;

        public LevelDocument Migrate(LevelDocument source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            LevelDocument migrated = source.DeepCopy();
            foreach (LevelScenarioObjectiveData objective in
                migrated.scenario.objectives)
            {
                if (objective != null && string.IsNullOrWhiteSpace(objective.mobility))
                {
                    objective.mobility = "set";
                }
            }

            migrated.schemaVersion = TargetVersion;
            return migrated;
        }
    }

    public sealed class LevelDocumentV5ToV6Migration : ILevelDocumentMigration
    {
        public int SourceVersion => 5;

        public int TargetVersion => 6;

        public LevelDocument Migrate(LevelDocument source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            LevelDocument migrated = source.DeepCopy();
            migrated.environment = source.environment?.DeepCopy()
                ?? new LevelEnvironmentData();
            migrated.schemaVersion = TargetVersion;
            return migrated;
        }
    }

    public sealed class LevelDocumentV6ToV7Migration : ILevelDocumentMigration
    {
        public int SourceVersion => 6;

        public int TargetVersion => 7;

        public LevelDocument Migrate(LevelDocument source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            LevelDocument migrated = source.DeepCopy();
            migrated.groups = migrated.groups ?? new List<LevelEntityGroupData>();
            foreach (LevelEntity entity in migrated.entities)
            {
                if (entity != null)
                    entity.groupId = entity.groupId ?? string.Empty;
            }
            migrated.schemaVersion = TargetVersion;
            return migrated;
        }
    }

    public sealed class LevelDocumentV7ToV8Migration : ILevelDocumentMigration
    {
        public int SourceVersion => 7;

        public int TargetVersion => 8;

        public LevelDocument Migrate(LevelDocument source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            LevelDocument migrated = source.DeepCopy();
            foreach (TerrainSurfaceData surface in migrated.terrainSurfaces)
            {
                if (surface != null)
                    surface.appearance = surface.appearance ?? new TerrainAppearanceData();
            }
            migrated.schemaVersion = TargetVersion;
            return migrated;
        }
    }

    public sealed class LevelDocumentV8ToV9Migration : ILevelDocumentMigration
    {
        public int SourceVersion => 8;

        public int TargetVersion => 9;

        public LevelDocument Migrate(LevelDocument source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            LevelDocument migrated = source.DeepCopy();
            migrated.dressing = migrated.dressing ?? new LevelDressingData();
            migrated.schemaVersion = TargetVersion;
            return migrated;
        }
    }

    public sealed class LevelDocumentV9ToV10Migration : ILevelDocumentMigration
    {
        public int SourceVersion => 9;

        public int TargetVersion => 10;

        public LevelDocument Migrate(LevelDocument source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            LevelDocument migrated = source.DeepCopy();
            // Missing pitch and roll fields deserialize as zero, preserving the
            // previous yaw-only transform exactly.
            migrated.schemaVersion = TargetVersion;
            return migrated;
        }
    }

    public sealed class LevelDocumentV10ToV11Migration : ILevelDocumentMigration
    {
        public int SourceVersion => 10;
        public int TargetVersion => 11;

        public LevelDocument Migrate(LevelDocument source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            LevelDocument migrated = source.DeepCopy();
            foreach (TerrainSurfaceData surface in migrated.terrainSurfaces ?? new List<TerrainSurfaceData>())
            {
                if (surface == null)
                    continue;
                int count = Math.Max(0, surface.sampleCountX * surface.sampleCountZ);
                surface.materialSamples = new List<int>(count);
                for (int index = 0; index < count; index++)
                    surface.materialSamples.Add(0);
            }
            migrated.schemaVersion = TargetVersion;
            return migrated;
        }
    }

    public sealed class LevelDocumentV11ToV12Migration : ILevelDocumentMigration
    {
        public int SourceVersion => 11;
        public int TargetVersion => 12;

        public LevelDocument Migrate(LevelDocument source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            LevelDocument migrated = source.DeepCopy();
            foreach (LevelScenarioActorData actor in migrated.scenario?.actors
                ?? new List<LevelScenarioActorData>())
            {
                if (actor != null)
                    actor.characterId = actor.characterId ?? string.Empty;
            }
            migrated.schemaVersion = TargetVersion;
            return migrated;
        }
    }

    public sealed class LevelDocumentV12ToV13Migration : ILevelDocumentMigration
    {
        public int SourceVersion => 12;
        public int TargetVersion => 13;

        public LevelDocument Migrate(LevelDocument source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            LevelDocument migrated = source.DeepCopy();
            foreach (LevelScenarioPropData prop in migrated.scenario?.props
                ?? new List<LevelScenarioPropData>())
            {
                if (prop != null)
                {
                    prop.toppling = prop.toppling
                        ?? new LevelScenarioPropTopplingData();
                }
            }
            migrated.schemaVersion = TargetVersion;
            return migrated;
        }
    }

    public sealed class LevelDocumentV13ToV14Migration : ILevelDocumentMigration
    {
        public int SourceVersion => 13;
        public int TargetVersion => 14;

        public LevelDocument Migrate(LevelDocument source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            LevelDocument migrated = source.DeepCopy();
            foreach (LevelScenarioPropData prop in migrated.scenario?.props
                ?? new List<LevelScenarioPropData>())
            {
                if (prop != null)
                {
                    prop.pinning = prop.pinning
                        ?? new LevelScenarioPropPinningData();
                }
            }
            migrated.schemaVersion = TargetVersion;
            return migrated;
        }
    }

    public sealed class LevelDocumentV14ToV15Migration : ILevelDocumentMigration
    {
        public int SourceVersion => 14;
        public int TargetVersion => 15;

        public LevelDocument Migrate(LevelDocument source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            LevelDocument migrated = source.DeepCopy();
            migrated.traversalLinks = migrated.traversalLinks
                ?? new List<LevelTraversalLinkData>();
            migrated.schemaVersion = TargetVersion;
            return migrated;
        }
    }

}
