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
            migrated.playtest = migrated.playtest ?? new LevelPlaytestData();
            migrated.playtest.playerStart = new LevelTransformData(
                new Float3Data(
                    migrated.bounds.center.x,
                    migrated.bounds.center.y + (migrated.bounds.size.y * 0.5f),
                    migrated.bounds.center.z),
                0f);
            migrated.schemaVersion = TargetVersion;
            return migrated;
        }
    }
}
