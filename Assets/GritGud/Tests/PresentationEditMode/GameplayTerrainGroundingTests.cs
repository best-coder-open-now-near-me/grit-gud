using System.Collections.Generic;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Levels;
using GritGud.Presentation.Gameplay;
using GritGud.Presentation.Levels.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace GritGud.Presentation.Tests
{
    public sealed class GameplayTerrainGroundingTests
    {
        private GameObject terrainRoot;
        private TerrainWorldProjector terrainProjector;
        private GameObject actor;

        [TearDown]
        public void TearDown()
        {
            terrainProjector?.Dispose();
            terrainProjector = null;
            if (terrainRoot != null)
            {
                Object.DestroyImmediate(terrainRoot);
                terrainRoot = null;
            }

            if (actor != null)
            {
                Object.DestroyImmediate(actor);
                actor = null;
            }
        }

        [Test]
        public void PlaceOnGroundOffsetsCharacterControllerAboveProjectedTerrain()
        {
            CreateFlatTerrain(1.5f);
            actor = new GameObject("Terrain Grounding Actor");
            CharacterController controller = actor.AddComponent<CharacterController>();
            controller.height = 2f;
            controller.center = new Vector3(0f, 0.5f, 0f);

            GameplayGroundPlacement.PlaceOnGround(
                actor.transform,
                new Vector3(1f, 4f, 1f));

            Assert.That(actor.transform.position.x, Is.EqualTo(1f).Within(0.001f));
            Assert.That(actor.transform.position.y, Is.EqualTo(2.02f).Within(0.001f));
            Assert.That(actor.transform.position.z, Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void RouteSegmentResolvesDestinationOntoProjectedTerrain()
        {
            CreateFlatTerrain(1.5f);
            actor = new GameObject("Terrain Route Actor");
            CharacterController controller = actor.AddComponent<CharacterController>();
            controller.height = 2f;
            controller.center = new Vector3(0f, 1f, 0f);
            controller.radius = 0.25f;
            controller.stepOffset = 0.3f;
            controller.slopeLimit = 50f;
            actor.transform.position = new Vector3(0.5f, 1.52f, 0.5f);
            Physics.SyncTransforms();
            var validator = new UnityMovementRouteSegmentValidator(controller);

            MovementRouteSegmentValidation result = validator.Validate(
                "player",
                new GameplayPosition(0.5f, 1.52f, 0.5f),
                new GameplayPosition(0.75f, 1.52f, 0.5f));

            Assert.That(result.IsValid, Is.True, result.FailureReason);
            Assert.That(result.ResolvedPosition.Y, Is.EqualTo(1.52f).Within(0.001f));
        }

        private void CreateFlatTerrain(float elevation)
        {
            terrainRoot = new GameObject("Projected Gameplay Terrain");
            terrainProjector = new TerrainWorldProjector(terrainRoot.transform);
            var document = new LevelDocument
            {
                schemaVersion = LevelDocument.CurrentSchemaVersion,
                levelId = "gameplay-terrain-test",
                displayName = "Gameplay Terrain Test",
                terrainSurfaces = new List<TerrainSurfaceData>
                {
                    new TerrainSurfaceData
                    {
                        id = "gameplay-ground",
                        origin = new Float3Data(0f, elevation, 0f),
                        sampleCountX = 3,
                        sampleCountZ = 3,
                        sampleSpacing = 1f,
                        elevationIncrement = 0.25f,
                        minimumElevation = 0f,
                        heightSamples = new List<int>(new int[9]),
                    },
                },
            };
            terrainProjector.Replace(document);
            Physics.SyncTransforms();
        }
    }
}
