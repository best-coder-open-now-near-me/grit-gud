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

            Assert.That(result.schemaVersion, Is.EqualTo(LevelDocument.CurrentSchemaVersion));
            Assert.That(
                result.scenario.objectives[0].movementOpportunityCost,
                Is.Zero);
            Assert.That(result.scenario.objectives[0].mobility, Is.EqualTo("set"));
        }

        [Test]
        public void VersionFiveDocumentGainsPortableEnvironmentDefaults()
        {
            LevelDocument source = LevelDocumentFactory.CreateEmpty("Legacy Lighting");
            source.schemaVersion = 5;
            source.environment = null;

            LevelDocument result = new LevelDocumentMigrator().MigrateToCurrent(source);

            Assert.That(result.schemaVersion, Is.EqualTo(LevelDocument.CurrentSchemaVersion));
            Assert.That(result.environment, Is.Not.Null);
            Assert.That(result.environment.presetId, Is.EqualTo("depot-night"));
            Assert.That(result.environment.atmosphere.fogEnabled, Is.True);
            Assert.That(result.environment.keyLight.intensity, Is.GreaterThan(0f));
        }

        [Test]
        public void VersionSixDocumentGainsPortableOrganizationDefaults()
        {
            LevelDocument source = LevelDocumentFactory.CreateEmpty("Legacy Groups");
            source.schemaVersion = 6;
            source.groups = null;
            source.entities.Add(new LevelEntity
            {
                id = "entity",
                archetypeId = "prop.crate.standard",
                groupId = null,
            });

            LevelDocument result = new LevelDocumentMigrator().MigrateToCurrent(source);

            Assert.That(result.schemaVersion, Is.EqualTo(LevelDocument.CurrentSchemaVersion));
            Assert.That(result.groups, Is.Empty);
            Assert.That(result.entities[0].groupId, Is.Empty);
        }

        [Test]
        public void VersionSevenTerrainGainsPortableAppearanceDefaults()
        {
            LevelDocument source = LevelDocumentFactory.CreateNew("Legacy Terrain Appearance");
            source.schemaVersion = 7;
            source.terrainSurfaces[0].appearance = null;

            LevelDocument result = new LevelDocumentMigrator().MigrateToCurrent(source);

            Assert.That(result.schemaVersion, Is.EqualTo(LevelDocument.CurrentSchemaVersion));
            Assert.That(result.terrainSurfaces[0].appearance, Is.Not.Null);
            Assert.That(result.terrainSurfaces[0].appearance.presetId, Is.EqualTo("slate"));
            Assert.That(result.terrainSurfaces[0].appearance.slopeBlendEndDegrees,
                Is.GreaterThan(result.terrainSurfaces[0].appearance.slopeBlendStartDegrees));
        }

        [Test]
        public void VersionEightDocumentGainsPortableDressingDefaults()
        {
            LevelDocument source = LevelDocumentFactory.CreateEmpty("Legacy Dressing");
            source.schemaVersion = 8;
            source.dressing = null;

            LevelDocument result = new LevelDocumentMigrator().MigrateToCurrent(source);

            Assert.That(result.schemaVersion, Is.EqualTo(LevelDocument.CurrentSchemaVersion));
            Assert.That(result.dressing, Is.Not.Null);
            Assert.That(result.dressing.decals, Is.Empty);
            Assert.That(result.dressing.ambientVfx, Is.Empty);
            Assert.That(result.dressing.audioZones, Is.Empty);
        }

        [Test]
        public void VersionNineYawTransformMigratesToThreeAxisRotation()
        {
            LevelDocument source = LevelDocumentFactory.CreateEmpty("Legacy rotation");
            source.schemaVersion = 9;
            source.entities.Add(new LevelEntity
            {
                id = "crate",
                archetypeId = "crate",
                transform = new LevelTransformData(new Float3Data(1f, 2f, 3f), 45f),
            });

            LevelDocument result = new LevelDocumentMigrator().MigrateToCurrent(source);

            Assert.That(result.schemaVersion, Is.EqualTo(LevelDocument.CurrentSchemaVersion));
            Assert.That(result.entities[0].transform.pitchDegrees, Is.Zero);
            Assert.That(result.entities[0].transform.yawDegrees, Is.EqualTo(45f));
            Assert.That(result.entities[0].transform.rollDegrees, Is.Zero);
        }
        [Test]
        public void VersionTenTerrainGainsUnpaintedMaterialSamples()
        {
            LevelDocument source = LevelDocumentFactory.CreateEmpty("Legacy terrain paint");
            source.schemaVersion = 10;
            source.terrainSurfaces.Add(TerrainSurfaceAuthoring.CreateFlat(
                "ground",
                source.bounds,
                2f));
            source.terrainSurfaces[0].materialSamples.Clear();

            LevelDocument result = new LevelDocumentMigrator().MigrateToCurrent(source);

            Assert.That(result.schemaVersion, Is.EqualTo(LevelDocument.CurrentSchemaVersion));
            Assert.That(result.terrainSurfaces[0].materialSamples,
                Has.Count.EqualTo(result.terrainSurfaces[0].heightSamples.Count));
            Assert.That(result.terrainSurfaces[0].materialSamples, Is.All.Zero);
        }

        [Test]
        public void VersionElevenActorsGainOptionalCharacterReferences()
        {
            LevelDocument source = LevelDocumentFactory.CreateEmpty("Legacy characters");
            source.schemaVersion = 11;
            source.scenario.actors[0].characterId = null;

            LevelDocument result = new LevelDocumentMigrator().MigrateToCurrent(source);

            Assert.That(result.schemaVersion, Is.EqualTo(LevelDocument.CurrentSchemaVersion));
            Assert.That(result.scenario.actors[0].characterId, Is.Empty);
            Assert.That(result.scenario.actors[0].id, Is.EqualTo("player"));
        }

        [Test]
        public void VersionTwelvePropsGainDisabledTopplingDefaults()
        {
            LevelDocument source = LevelDocumentFactory.CreateEmpty("Legacy props");
            source.schemaVersion = 12;
            source.scenario.props.Add(new LevelScenarioPropData
            {
                entityId = "prop.one",
                toppling = null,
            });

            LevelDocument result = new LevelDocumentMigrator().MigrateToCurrent(source);

            Assert.That(result.schemaVersion, Is.EqualTo(LevelDocument.CurrentSchemaVersion));
            Assert.That(result.scenario.props[0].toppling, Is.Not.Null);
            Assert.That(result.scenario.props[0].toppling.enabled, Is.False);
        }

        [Test]
        public void VersionThirteenPropsGainDisabledPinningDefaults()
        {
            LevelDocument source = LevelDocumentFactory.CreateEmpty(
                "Legacy pinning");
            source.schemaVersion = 13;
            source.scenario.props.Add(new LevelScenarioPropData
            {
                entityId = "prop.one",
                pinning = null,
            });

            LevelDocument result = new LevelDocumentMigrator()
                .MigrateToCurrent(source);

            Assert.That(result.schemaVersion,
                Is.EqualTo(LevelDocument.CurrentSchemaVersion));
            Assert.That(result.scenario.props[0].pinning, Is.Not.Null);
            Assert.That(result.scenario.props[0].pinning.enabled, Is.False);
            Assert.That(result.scenario.props[0].pinning.maximumActorMass,
                Is.EqualTo(100f));
        }

        [Test]
        public void VersionFourteenLevelsGainEmptyTraversalLinks()
        {
            LevelDocument source = LevelDocumentFactory.CreateEmpty(
                "Legacy traversal");
            source.schemaVersion = 14;
            source.traversalLinks = null;

            LevelDocument result = new LevelDocumentMigrator()
                .MigrateToCurrent(source);

            Assert.That(result.schemaVersion,
                Is.EqualTo(LevelDocument.CurrentSchemaVersion));
            Assert.That(result.traversalLinks, Is.Not.Null.And.Empty);
        }

    }
}
