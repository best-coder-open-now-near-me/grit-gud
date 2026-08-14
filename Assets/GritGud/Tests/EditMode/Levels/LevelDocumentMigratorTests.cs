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
        public void VersionOneDocumentMigratesWithTerrainAndScenarioDefaults()
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
            Assert.That(result.scenario, Is.Not.Null);
            Assert.That(result.scenario.actors, Has.Count.EqualTo(1));
            Assert.That(
                result.scenario.FindInitiallySelectedPlayer().transform.position.y,
                Is.EqualTo(7.5f));
            Assert.That(result.entities.Single().id, Is.EqualTo("preserved"));
        }

        [Test]
        public void VersionThreePlayerStartMigratesIntoSelectedScenarioActor()
        {
            LevelDocument source = LevelDocumentFactory.CreateEmpty("Legacy Playtest");
            source.schemaVersion = 3;
            source.scenario = null;
            source.legacyPlaytest = new LevelPlaytestData
            {
                playerStart = new LevelTransformData(
                    new Float3Data(4f, 2f, -6f),
                    135f),
            };

            LevelDocument result = new LevelDocumentMigrator().MigrateToCurrent(source);

            LevelScenarioActorData player = result.scenario.FindInitiallySelectedPlayer();
            Assert.That(player.id, Is.EqualTo("player"));
            Assert.That(player.templateId, Is.EqualTo("player"));
            Assert.That(player.transform.position.x, Is.EqualTo(4f));
            Assert.That(player.transform.position.z, Is.EqualTo(-6f));
            Assert.That(player.transform.yawDegrees, Is.EqualTo(135f));
            Assert.That(result.legacyPlaytest, Is.Null);
        }

        [Test]
        public void VersionFourObjectiveGainsCompleteActionCostDefaults()
        {
            LevelDocument source = LevelDocumentFactory.CreateEmpty("Legacy Objective");
            source.schemaVersion = 4;
            source.scenario.objectives.Add(new LevelScenarioObjectiveData
            {
                id = "objective",
                mobility = null,
            });

            LevelDocument result = new LevelDocumentMigrator().MigrateToCurrent(source);

            Assert.That(result.schemaVersion, Is.EqualTo(5));
            Assert.That(
                result.scenario.objectives[0].movementOpportunityCost,
                Is.Zero);
            Assert.That(result.scenario.objectives[0].mobility, Is.EqualTo("set"));
        }
    }
}
