using System.Collections.Generic;
using System.Linq;
using GritGud.Application.Levels;
using GritGud.Domain.Levels;
using GritGud.Presentation.Levels;
using NUnit.Framework;

namespace GritGud.Presentation.Tests
{
    public sealed class UnityLevelJsonSerializerTests
    {
        [Test]
        public void RoundTripPreservesPortableLevelData()
        {
            LevelDocument document = LevelDocumentFactory.CreateEmpty("Round Trip");
            document.entities.Add(new LevelEntity
            {
                id = "crate-1",
                archetypeId = "prop.crate.standard",
                groupId = "props",
                transform = new LevelTransformData(new Float3Data(2.5f, 0f, -5f), 90f),
                destructible = new DestructibleInstanceData
                {
                    enabled = true,
                    initialState = "intact",
                    integrity = 10f,
                },
                interactionPoints =
                {
                    new InteractionPointData
                    {
                        id = "entry",
                        type = "doorway",
                        localPosition = new Float3Data(1f, 0f, -0.5f),
                        radius = 1.25f,
                    },
                },
            });
            document.environment.practicalLights.Add(new LevelPracticalLightData
            {
                id = "loading-flood",
                displayName = "Loading Flood",
                position = new Float3Data(2f, 6f, -3f),
                target = new Float3Data(2f, 0f, 1f),
                color = new FloatColorData(1.2f, 0.4f, 0.1f),
                intensity = 4.5f,
                range = 17f,
                spotAngle = 52f,
                innerSpotFraction = 0.6f,
            });
            document.groups.Add(new LevelEntityGroupData
            {
                id = "props",
                displayName = "Gameplay Props",
                locked = true,
            });
            document.dressing.decals.Add(new LevelDecalData
            {
                id = "loading-arrow",
                displayName = "Loading Arrow",
                styleId = "arrow",
                position = new Float3Data(2f, 0.02f, 3f),
                size = new Float3Data(2f, 3f, 1f),
                color = new FloatColorData(0.2f, 0.8f, 1f, 0.65f),
            });
            document.dressing.audioZones.Add(new LevelAudioZoneData
            {
                id = "loading-hum",
                displayName = "Loading Hum",
                soundId = "industrial-hum",
                center = new Float3Data(2f, 2f, 3f),
                size = new Float3Data(8f, 4f, 8f),
                volume = 0.2f,
                fadeDistance = 3f,
            });
            var serializer = new UnityLevelJsonSerializer();

            string json = serializer.Serialize(document);
            LevelDocument result = serializer.Deserialize(json);

            Assert.That(result.schemaVersion, Is.EqualTo(LevelDocument.CurrentSchemaVersion));
            Assert.That(result.displayName, Is.EqualTo("Round Trip"));
            Assert.That(result.entities, Has.Count.EqualTo(1));
            Assert.That(result.entities[0].id, Is.EqualTo("crate-1"));
            Assert.That(result.entities[0].groupId, Is.EqualTo("props"));
            Assert.That(result.groups.Single().displayName, Is.EqualTo("Gameplay Props"));
            Assert.That(result.groups.Single().locked, Is.True);
            Assert.That(result.entities[0].transform.position.z, Is.EqualTo(-5f));
            Assert.That(result.entities[0].destructible.initialState, Is.EqualTo("intact"));
            Assert.That(result.entities[0].destructible.enabled, Is.True);
            Assert.That(result.entities[0].interactionPoints[0].type, Is.EqualTo("doorway"));
            Assert.That(result.entities[0].interactionPoints[0].radius, Is.EqualTo(1.25f));
            Assert.That(result.environment.practicalLights, Has.Count.EqualTo(1));
            Assert.That(result.environment.practicalLights[0].displayName,
                Is.EqualTo("Loading Flood"));
            Assert.That(result.environment.practicalLights[0].color.r, Is.EqualTo(1.2f));
            Assert.That(result.dressing.decals.Single().styleId, Is.EqualTo("arrow"));
            Assert.That(result.dressing.decals.Single().color.a, Is.EqualTo(0.65f));
            Assert.That(result.dressing.audioZones.Single().soundId,
                Is.EqualTo("industrial-hum"));
            Assert.That(
                result.scenario.FindInitiallySelectedPlayer().transform.position.y,
                Is.EqualTo(7.5f));
        }

        [Test]
        public void EmptyImportReturnsActionableFailure()
        {
            var serializer = new UnityLevelJsonSerializer();

            LevelSerializationException exception = Assert.Throws<LevelSerializationException>(
                () => serializer.Deserialize(string.Empty));

            Assert.That(exception.Message, Does.Contain("empty"));
        }

        [Test]
        public void VersionThreeJsonMigratesLegacyPlaytestWithoutReexportingIt()
        {
            const string json = "{\"schemaVersion\":3,\"levelId\":\"legacy\","
                + "\"displayName\":\"Legacy\",\"bounds\":{\"center\":{\"x\":0,\"y\":2.5,\"z\":0},"
                + "\"size\":{\"x\":50,\"y\":10,\"z\":50}},\"entities\":[],"
                + "\"terrainSurfaces\":[],\"playtest\":{\"playerStart\":{\"position\":"
                + "{\"x\":3,\"y\":4,\"z\":5},\"yawDegrees\":90}}}";
            var serializer = new UnityLevelJsonSerializer();

            LevelDocument result = serializer.Deserialize(json);
            string currentJson = serializer.Serialize(result);

            LevelScenarioActorData player = result.scenario.FindInitiallySelectedPlayer();
            Assert.That(player.transform.position.x, Is.EqualTo(3f));
            Assert.That(player.transform.position.y, Is.EqualTo(4f));
            Assert.That(player.transform.position.z, Is.EqualTo(5f));
            Assert.That(player.transform.yawDegrees, Is.EqualTo(90f));
            Assert.That(currentJson, Does.Contain("\"scenario\""));
            Assert.That(currentJson, Does.Not.Contain("\"playtest\""));
        }

        [Test]
        public void RoundTripPreservesQuantizedTerrainSamples()
        {
            LevelDocument document = LevelDocumentFactory.CreateEmpty("Terrain Round Trip");
            document.terrainSurfaces.Add(new TerrainSurfaceData
            {
                id = "ground",
                sampleCountX = 2,
                sampleCountZ = 2,
                sampleSpacing = 2.5f,
                elevationIncrement = 0.1f,
                appearance = new TerrainAppearanceData
                {
                    presetId = "grass",
                    baseColor = new FloatColorData(0.2f, 0.4f, 0.1f),
                    steepColor = new FloatColorData(0.3f, 0.2f, 0.1f),
                    slopeBlendStartDegrees = 28f,
                    slopeBlendEndDegrees = 54f,
                    smoothness = 0.04f,
                    specularStrength = 0.015f,
                },
                heightSamples = new List<int> { 0, 1, -2, 3 },
            });
            var serializer = new UnityLevelJsonSerializer();

            LevelDocument result = serializer.Deserialize(serializer.Serialize(document));

            Assert.That(result.terrainSurfaces, Has.Count.EqualTo(1));
            Assert.That(result.terrainSurfaces[0].id, Is.EqualTo("ground"));
            Assert.That(result.terrainSurfaces[0].heightSamples,
                Is.EqualTo(new[] { 0, 1, -2, 3 }));
            Assert.That(result.terrainSurfaces[0].appearance.presetId, Is.EqualTo("grass"));
            Assert.That(result.terrainSurfaces[0].appearance.steepColor.g, Is.EqualTo(0.2f));
            Assert.That(result.terrainSurfaces[0].appearance.slopeBlendEndDegrees,
                Is.EqualTo(54f));
        }
    }
}
