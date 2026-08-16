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
        private GameObject observer;
        private GameObject target;
        private readonly List<GameObject> traversalFixtures =
            new List<GameObject>();

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

            if (observer != null)
            {
                Object.DestroyImmediate(observer);
                observer = null;
            }

            if (target != null)
            {
                Object.DestroyImmediate(target);
                target = null;
            }

            foreach (GameObject fixture in traversalFixtures)
            {
                if (fixture != null)
                    Object.DestroyImmediate(fixture);
            }
            traversalFixtures.Clear();
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
            GameplayActorPose captured = GameplayPoseAdapter.FromTransform(
                actor.transform,
                ActorStance.Standing);
            Assert.That(captured.Position.Y, Is.EqualTo(2.02f).Within(0.001f));
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

        [Test]
        public void AuthoredJumpCrossesBarrierAndFreezesTraversalEvidence()
        {
            CreateFlatTerrain(1.5f);
            CharacterController controller = CreateTraversalActor();
            CreateBarrier(new Vector3(0.5f, 1.8f, 1f),
                new Vector3(0.4f, 0.55f, 0.25f));
            LevelTraversalLinkData link = CreateJumpLink();
            var validator = new UnityMovementRouteSegmentValidator(
                controller,
                new[] { link });

            MovementRouteSegmentValidation result = validator.Validate(
                "player",
                new GameplayPosition(0.5f, 1.52f, 0.5f),
                new GameplayPosition(0.5f, 1.52f, 0.75f));

            Assert.That(result.IsValid, Is.True, result.FailureReason);
            Assert.That(result.Segment, Is.Not.Null);
            Assert.That(result.Segment.Kind,
                Is.EqualTo(MovementRouteSegmentKind.Jump));
            Assert.That(result.Segment.TraversalLinkId,
                Is.EqualTo("jump.barrier"));
            Assert.That(result.Segment.ActionId,
                Is.EqualTo("traversal.jump"));
            Assert.That(result.Segment.MovementCost, Is.EqualTo(2f));
            Assert.That(result.Segment.ActionPointCost, Is.EqualTo(1));
            Assert.That(result.Segment.Sample(0.5f).Y,
                Is.EqualTo(2.52f).Within(0.001f));
        }

        [Test]
        public void BarrierIsDisconnectedWithoutAuthoredTraversalLink()
        {
            CreateFlatTerrain(1.5f);
            CharacterController controller = CreateTraversalActor();
            CreateBarrier(new Vector3(0.5f, 1.8f, 1f),
                new Vector3(0.4f, 0.55f, 0.25f));
            var validator = new UnityMovementRouteSegmentValidator(controller);

            MovementRouteSegmentValidation result = validator.Validate(
                "player",
                new GameplayPosition(0.5f, 1.52f, 0.5f),
                new GameplayPosition(0.5f, 1.52f, 1.5f));

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.FailureReason,
                Does.Contain("capsule path"));
        }

        [Test]
        public void AuthoredJumpRejectsBlockedCapsuleArc()
        {
            CreateFlatTerrain(1.5f);
            CharacterController controller = CreateTraversalActor();
            CreateBarrier(new Vector3(0.5f, 3.7f, 1f),
                new Vector3(0.6f, 0.5f, 0.5f));
            var validator = new UnityMovementRouteSegmentValidator(
                controller,
                new[] { CreateJumpLink() });

            MovementRouteSegmentValidation result = validator.Validate(
                "player",
                new GameplayPosition(0.5f, 1.52f, 0.5f),
                new GameplayPosition(0.5f, 1.52f, 0.75f));

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.FailureReason, Does.Contain("does not clear"));
        }

        [Test]
        public void OneWayTraversalLinkIsNotSelectedInReverse()
        {
            CreateFlatTerrain(1.5f);
            CharacterController controller = CreateTraversalActor();
            LevelTraversalLinkData link = CreateJumpLink();
            link.bidirectional = false;
            actor.transform.position = new Vector3(0.5f, 1.52f, 1.5f);
            Physics.SyncTransforms();
            var validator = new UnityMovementRouteSegmentValidator(
                controller,
                new[] { link });

            MovementRouteSegmentValidation result = validator.Validate(
                "player",
                new GameplayPosition(0.5f, 1.52f, 1.5f),
                new GameplayPosition(0.5f, 1.52f, 1.25f));

            Assert.That(result.IsValid, Is.True, result.FailureReason);
            Assert.That(result.Segment, Is.Null,
                "Reverse movement must remain grounded when the link is one-way.");
        }

        [Test]
        public void FrozenTrajectorySamplingUsesAuthoredArcAndDuration()
        {
            var segment = new MovementRouteSegmentRecord(
                new GameplayPosition(0f, 0f, 0f),
                new GameplayPosition(0f, 0f, 2f),
                MovementRouteSegmentKind.Jump,
                "jump.sample",
                "traversal.jump",
                2f,
                0,
                1.25f,
                0.8f);

            bool sampled = MovementRouteSampling.TrySample(
                new[] { segment },
                0.4f,
                out Vector3 position,
                out Vector3 direction,
                out int segmentIndex,
                out float progress);

            Assert.That(sampled, Is.True);
            Assert.That(segmentIndex, Is.Zero);
            Assert.That(progress, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(position.y, Is.EqualTo(1.25f).Within(0.001f));
            Assert.That(position.z, Is.EqualTo(1f).Within(0.001f));
            Assert.That(direction.z, Is.GreaterThan(0f));
        }

        [TestCase(45f, true)]
        [TestCase(50f, true)]
        [TestCase(55f, false)]
        public void GroundedRouteHonorsBelowAtAndAboveSlopeLimit(
            float slopeDegrees,
            bool expectedValid)
        {
            GameObject ramp = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ramp.name = $"{slopeDegrees:0} Degree Traversal Ramp";
            ramp.transform.rotation = Quaternion.Euler(
                slopeDegrees,
                0f,
                0f);
            ramp.transform.localScale = new Vector3(0.2f, 1f, 0.2f);
            traversalFixtures.Add(ramp);
            CharacterController controller = CreateSlopeActor();
            Physics.SyncTransforms();
            var validator = new UnityMovementRouteSegmentValidator(controller);

            MovementRouteSegmentValidation result = validator.Validate(
                "player",
                new GameplayPosition(0f, 0.02f, 0f),
                new GameplayPosition(0f, 0.02f, -0.25f));

            Assert.That(result.IsValid, Is.EqualTo(expectedValid),
                result.FailureReason);
            if (!expectedValid)
                Assert.That(result.FailureReason, Does.Contain("slope limit"));
        }

        [Test]
        public void ProjectedTerrainRidgeOccludesTargetExposure()
        {
            CreateTerrainRidge();
            observer = new GameObject("Terrain Exposure Observer");
            target = new GameObject("Terrain Exposure Target");
            observer.transform.position = new Vector3(2f, 0f, 0.25f);
            target.transform.position = new Vector3(2f, 0f, 3.75f);
            Physics.SyncTransforms();
            var query = new UnityTargetExposureQuery(
                observer.transform,
                target.transform);

            TargetExposureSnapshot result = query.Capture(
                "observer",
                new GameplayPosition(2f, 1.2f, 0.25f),
                "target",
                new[]
                {
                    new TargetRegionSample(
                        TargetRegionId.Torso,
                        new GameplayPosition(2f, 1.2f, 3.75f),
                        0.2f),
                });

            Assert.That(result.TotalSampleCount, Is.GreaterThan(0));
            Assert.That(result.VisibleSampleCount, Is.Zero);
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

        private CharacterController CreateTraversalActor()
        {
            actor = new GameObject("Authored Traversal Actor");
            CharacterController controller =
                actor.AddComponent<CharacterController>();
            controller.height = 2f;
            controller.center = new Vector3(0f, 1f, 0f);
            controller.radius = 0.25f;
            controller.stepOffset = 0.3f;
            controller.slopeLimit = 50f;
            actor.transform.position = new Vector3(0.5f, 1.52f, 0.5f);
            Physics.SyncTransforms();
            return controller;
        }

        private CharacterController CreateSlopeActor()
        {
            actor = new GameObject("Slope Limit Actor");
            CharacterController controller =
                actor.AddComponent<CharacterController>();
            controller.height = 2f;
            controller.center = new Vector3(0f, 1f, 0f);
            controller.radius = 0.25f;
            controller.stepOffset = 0.3f;
            controller.slopeLimit = 50f;
            actor.transform.position = new Vector3(0f, 0.02f, 0f);
            return controller;
        }

        private void CreateBarrier(Vector3 position, Vector3 scale)
        {
            GameObject barrier = GameObject.CreatePrimitive(
                PrimitiveType.Cube);
            barrier.name = "Traversal Barrier";
            barrier.transform.position = position;
            barrier.transform.localScale = scale;
            traversalFixtures.Add(barrier);
            Physics.SyncTransforms();
        }

        private static LevelTraversalLinkData CreateJumpLink() =>
            new LevelTraversalLinkData
            {
                id = "jump.barrier",
                actionId = "traversal.jump",
                kind = LevelTraversalLinkData.JumpKind,
                takeoff = new Float3Data(0.5f, 1.52f, 0.5f),
                landing = new Float3Data(0.5f, 1.52f, 1.5f),
                activationRadius = 0.45f,
                movementCost = 2f,
                actionPointCost = 1,
                arcHeight = 1f,
                playbackDurationSeconds = 0.8f,
                clearancePadding = 0.02f,
            };

        private void CreateTerrainRidge()
        {
            terrainRoot = new GameObject("Projected Terrain Ridge");
            terrainProjector = new TerrainWorldProjector(terrainRoot.transform);
            var heights = new List<int>(new int[25]);
            for (int x = 0; x < 5; x++)
            {
                heights[2 * 5 + x] = 12;
            }

            var document = new LevelDocument
            {
                schemaVersion = LevelDocument.CurrentSchemaVersion,
                levelId = "exposure-terrain-test",
                displayName = "Exposure Terrain Test",
                terrainSurfaces = new List<TerrainSurfaceData>
                {
                    new TerrainSurfaceData
                    {
                        id = "ridge",
                        origin = new Float3Data(0f, 0f, 0f),
                        sampleCountX = 5,
                        sampleCountZ = 5,
                        sampleSpacing = 1f,
                        elevationIncrement = 0.25f,
                        heightSamples = heights,
                    },
                },
            };
            terrainProjector.Replace(document);
            Physics.SyncTransforms();
        }
    }
}
