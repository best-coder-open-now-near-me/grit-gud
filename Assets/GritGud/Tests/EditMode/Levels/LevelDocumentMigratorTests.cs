using System;
using System.Linq;
using GritGud.Application.Levels;
using GritGud.Domain.Levels;
using NUnit.Framework;

namespace GritGud.Domain.Tests.Levels
{
    public sealed class LevelDocumentMigratorTests
    {
        [Test]
        public void CurrentDocumentPassesThroughAsDetachedCopy()
        {
            LevelDocument source = LevelDocumentFactory.CreateEmpty("Migration Test");
            var migrator = new LevelDocumentMigrator();

            LevelDocument result = migrator.MigrateToCurrent(source);
            result.displayName = "Changed";

            Assert.That(source.displayName, Is.EqualTo("Migration Test"));
            Assert.That(result.schemaVersion, Is.EqualTo(LevelDocument.CurrentSchemaVersion));
        }

        [Test]
        public void FutureSchemaIsRejectedExplicitly()
        {
            LevelDocument source = LevelDocumentFactory.CreateEmpty();
            source.schemaVersion = LevelDocument.CurrentSchemaVersion + 1;

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => new LevelDocumentMigrator().MigrateToCurrent(source));

            Assert.That(exception.Message, Does.Contain("newer"));
        }

        [Test]
        public void VersionOneDocumentMigratesWithTerrainAndPlaytestDefaults()
        {
            LevelDocument source = LevelDocumentFactory.CreateEmpty("Legacy Level");
            source.schemaVersion = 1;
            source.terrainSurfaces = null;
            source.entities.Add(new LevelEntity
            {
                id = "preserved",
                archetypeId = "prop.crate.standard",
            });

            LevelDocument result = new LevelDocumentMigrator().MigrateToCurrent(source);

            Assert.That(result.schemaVersion, Is.EqualTo(LevelDocument.CurrentSchemaVersion));
            Assert.That(result.terrainSurfaces, Is.Empty);
            Assert.That(result.playtest, Is.Not.Null);
            Assert.That(result.playtest.playerStart.position.y, Is.EqualTo(7.5f));
            Assert.That(result.entities.Single().id, Is.EqualTo("preserved"));
        }
    }
}
