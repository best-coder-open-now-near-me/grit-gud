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
    public sealed class UnityProjectileSegmentQueryTests
    {
        private readonly List<GameObject> createdObjects = new List<GameObject>();
        private TerrainWorldProjector terrainProjector;

        [TearDown]
        public void TearDown()
        {
            terrainProjector?.Dispose();
            terrainProjector = null;
            foreach (GameObject createdObject in createdObjects)
            {
                if (createdObject != null)
                {
                    Object.DestroyImmediate(createdObject);
                }
            }

            createdObjects.Clear();
        }

        [Test]
        public void ProjectedTerrainRidgeStopsProjectileAsWorldGeometry()
        {
            CreateTerrainRidge();
            GameplayWorldRegistry registry = CreateRegistry();
            var origin = new Vector3(2f, 1.2f, 0.25f);
            var target = new Vector3(2f, 1.2f, 3.75f);
            CreateActor(registry, "attacker", origin);
            CreateActor(registry, "target", target);
            Physics.SyncTransforms();
            var adapter = new UnityProjectileSegmentQuery(
                registry,
                currentWorldStateRevision: () => 45L,
                blastQuery: new EmptyBlastWorldQuery(45L));

            ProjectileSegmentQueryResult result = adapter.Query(
                CreateQuery(origin, target, target));

            Assert.That(result.HasCollision, Is.True);
            Assert.That(
                result.HitEntityId,
                Is.EqualTo(UnityProjectileSegmentQuery.UnregisteredWorldGeometryId));
            Assert.That(result.CollisionFraction, Is.LessThan(1f));
        }

        [Test]
        public void SegmentQueryReturnsTheStableActorAtArrivalState()
        {
            GameplayWorldRegistry registry = CreateRegistry();
            CreateActor(registry, "attacker", Vector3.zero);
            CreateActor(registry, "target", new Vector3(0f, 0f, 6f));
            Physics.SyncTransforms();
            var adapter = new UnityProjectileSegmentQuery(
                registry,
                currentWorldStateRevision: () => 42,
                blastQuery: new EmptyBlastWorldQuery(42));

            ProjectileSegmentQueryResult result = adapter.Query(CreateQuery());

            Assert.That(result.HasCollision, Is.True);
            Assert.That(result.HitEntityId, Is.EqualTo("target"));
            Assert.That(result.WorldStateRevision, Is.EqualTo(42));
            Assert.That(result.CollisionFraction, Is.GreaterThan(0f));
            Assert.That(result.CollisionFraction, Is.LessThan(1f));
        }

        [Test]
        public void PinnedProjectileCollisionUsesHorizontalTargetProfile()
        {
            GameplayWorldRegistry registry = CreateRegistry();
            CreateActor(registry, "attacker", new Vector3(0f, 0f, -4f));
            CreateActor(registry, "target", Vector3.zero);
            GameplayActorView target = registry.GetActor("target");
            target.ReplayActions.PresentPinState(new ActorPinState(
                "target",
                "prop",
                displacementSequence: 1,
                new DisplacementContactEvidence(
                    "target",
                    new GameplayPosition(0f, 0f, 0f),
                    new GameplayPosition(0f, 1f, 0f),
                    overlapDepth: 0.2f)));
            Physics.SyncTransforms();
            var adapter = new UnityProjectileSegmentQuery(
                registry,
                currentWorldStateRevision: () => 46L,
                blastQuery: new EmptyBlastWorldQuery(46L));

            ProjectileSegmentQueryResult bodyHit = adapter.Query(CreateQuery(
                new Vector3(0f, 0.3f, -4f),
                new Vector3(0f, 0.3f, 0f),
                new Vector3(0f, 0.3f, 1f)));
            ProjectileSegmentQueryResult uprightOnly = adapter.Query(
                CreateQuery(
                    new Vector3(0f, 1.2f, -4f),
                    new Vector3(0f, 1.2f, 0f),
                    new Vector3(0f, 1.2f, 1f)));

            Assert.That(bodyHit.HasCollision, Is.True);
            Assert.That(bodyHit.HitEntityId, Is.EqualTo("target"));
            Assert.That(uprightOnly.HasCollision, Is.False,
                "The old upright movement volume must not catch projectiles.");
        }

        [Test]
        public void BlockingWorldGeometryWinsBeforeTheIntendedTarget()
        {
            GameplayWorldRegistry registry = CreateRegistry();
            CreateActor(registry, "attacker", Vector3.zero);
            CreateActor(registry, "target", new Vector3(0f, 0f, 6f));
            GameObject obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obstacle.name = "Unregistered Blocking Wall";
            obstacle.transform.position = new Vector3(0f, 0f, 3f);
            createdObjects.Add(obstacle);
            Physics.SyncTransforms();
            var adapter = new UnityProjectileSegmentQuery(
                registry,
                currentWorldStateRevision: () => 43,
                blastQuery: new EmptyBlastWorldQuery(43));

            ProjectileSegmentQueryResult result = adapter.Query(CreateQuery());

            Assert.That(result.HasCollision, Is.True);
            Assert.That(
                result.HitEntityId,
                Is.EqualTo(UnityProjectileSegmentQuery.UnregisteredWorldGeometryId));
            Assert.That(result.WorldStateRevision, Is.EqualTo(43));
        }

        [Test]
        public void ProjectileImpactUsesSharedBlastEvidenceWithoutRecalculation()
        {
            GameplayWorldRegistry registry = CreateRegistry();
            CreateActor(registry, "attacker", Vector3.zero);
            CreateActor(registry, "target", new Vector3(0f, 0f, 6f));
            Physics.SyncTransforms();
            var blast = new RecordingBlastWorldQuery(44L);
            var adapter = new UnityProjectileSegmentQuery(
                registry,
                currentWorldStateRevision: () => 44L,
                blastQuery: blast);

            ProjectileSegmentQueryResult result = adapter.Query(
                CreateQuery(withBlast: true));

            Assert.That(result.HasCollision, Is.True);
            Assert.That(blast.QueryCount, Is.EqualTo(1));
            Assert.That(blast.LastQuery.Radius, Is.EqualTo(5f));
            Assert.That(result.BlastEffects, Has.Count.EqualTo(1));
            Assert.That(
                result.BlastEffects[0].InjuryRegion,
                Is.EqualTo(TargetRegionId.RightArm));
            Assert.That(
                result.BlastEffects[0].Exposure,
                Is.EqualTo(0.4f));
        }

        private GameplayWorldRegistry CreateRegistry()
        {
            var root = new GameObject("Projectile Query World");
            createdObjects.Add(root);
            var world = new LevelWorld(
                root,
                new Dictionary<string, LevelEntityView>(),
                null);
            return new GameplayWorldRegistry(world);
        }

        private void CreateActor(
            GameplayWorldRegistry registry,
            string actorId,
            Vector3 position)
        {
            var actor = new GameObject(actorId);
            actor.transform.position = position;
            actor.AddComponent<SphereCollider>().radius = 0.45f;
            actor.AddComponent<ActorStancePresenter>();
            createdObjects.Add(actor);
            registry.RegisterActor(
                actorId,
                "test",
                targetable: true,
                actor);
        }

        private static ProjectileSegmentQuery CreateQuery(
            bool withBlast = false)
        {
            return CreateQuery(
                Vector3.zero,
                new Vector3(0f, 0f, 10f),
                new Vector3(0f, 0f, 8f),
                withBlast);
        }

        private static ProjectileSegmentQuery CreateQuery(
            Vector3 origin,
            Vector3 aimPoint,
            Vector3 segmentEnd,
            bool withBlast = false)
        {
            var definition = new ProjectileFlightDefinition(
                "projectile.query-test",
                speedPerTurn: 4f,
                radius: 0.1f,
                maximumRange: 12f,
                blastRadius: withBlast ? 5f : 0f,
                blastWoundMovementPenalty: withBlast ? 2f : 0f);
            var launch = new ProjectileLaunchRecord(
                sequence: 1,
                projectileId: "projectile.1",
                attackerId: "attacker",
                intendedTargetId: "target",
                actionId: "attack.projectile",
                origin: ToPosition(origin),
                aimPoint: ToPosition(aimPoint),
                definition: definition,
                turnActionPointAllowance: 4,
                remainingActionPointsAfterLaunch: 2);
            var flight = new ProjectileFlightSnapshot(
                launch,
                launch.Origin,
                distanceTraveled: 0f,
                elapsedTurnTime: 0f,
                status: ProjectileFlightStatus.InFlight);
            return new ProjectileSegmentQuery(
                flight,
                ToPosition(segmentEnd));
        }

        private void CreateTerrainRidge()
        {
            var root = new GameObject("Projectile Terrain Ridge");
            createdObjects.Add(root);
            terrainProjector = new TerrainWorldProjector(root.transform);
            var heights = new List<int>(new int[25]);
            for (int x = 0; x < 5; x++)
            {
                heights[2 * 5 + x] = 12;
            }

            var document = new LevelDocument
            {
                schemaVersion = LevelDocument.CurrentSchemaVersion,
                levelId = "projectile-terrain",
                displayName = "Projectile Terrain",
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
        }

        private static GameplayPosition ToPosition(Vector3 value) =>
            new GameplayPosition(value.x, value.y, value.z);

        private sealed class EmptyBlastWorldQuery : IBlastWorldQuery
        {
            private readonly long revision;

            public EmptyBlastWorldQuery(long worldStateRevision)
            {
                revision = worldStateRevision;
            }

            public BlastWorldQueryResult Query(BlastWorldQuery query) =>
                new BlastWorldQueryResult(
                    query,
                    revision,
                    System.Array.Empty<BlastEffectRecord>());
        }

        private sealed class RecordingBlastWorldQuery : IBlastWorldQuery
        {
            private readonly long revision;

            public RecordingBlastWorldQuery(long worldStateRevision)
            {
                revision = worldStateRevision;
            }

            public int QueryCount { get; private set; }

            public BlastWorldQuery LastQuery { get; private set; }

            public BlastWorldQueryResult Query(BlastWorldQuery query)
            {
                QueryCount++;
                LastQuery = query;
                return new BlastWorldQueryResult(
                    query,
                    revision,
                    new[]
                    {
                        new BlastEffectRecord(
                            "target",
                            BlastSubjectKind.Actor,
                            3f,
                            occlusionExposure: 0.5f,
                            distanceFalloff: 0.8f,
                            injuryRegion: TargetRegionId.RightArm),
                    });
            }
        }
    }
}
