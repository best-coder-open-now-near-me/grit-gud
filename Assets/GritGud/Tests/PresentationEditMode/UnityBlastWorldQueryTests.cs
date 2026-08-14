using System.Collections.Generic;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Presentation.Gameplay;
using GritGud.Presentation.Levels.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace GritGud.Presentation.Tests
{
    public sealed class UnityBlastWorldQueryTests
    {
        private readonly List<GameObject> createdObjects =
            new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject created in createdObjects)
            {
                if (created != null)
                {
                    Object.DestroyImmediate(created);
                }
            }

            createdObjects.Clear();
        }

        [Test]
        public void NearestExposedBodyRegionAndFalloffAreRecorded()
        {
            GameplayWorldRegistry registry = CreateRegistry();
            CreateActor(registry, "target", Vector3.zero);
            Physics.SyncTransforms();
            var query = new UnityBlastWorldQuery(
                registry,
                () => 12L,
                _ => false);

            BlastWorldQueryResult result = query.Query(
                new BlastWorldQuery(
                    new GameplayPosition(-2f, 1.2f, 0f),
                    5f));

            Assert.That(result.Effects, Has.Count.EqualTo(1));
            BlastEffectRecord effect = result.Effects[0];
            Assert.That(effect.EntityId, Is.EqualTo("target"));
            Assert.That(effect.SubjectKind, Is.EqualTo(BlastSubjectKind.Actor));
            Assert.That(effect.InjuryRegion, Is.EqualTo(TargetRegionId.LeftArm));
            Assert.That(effect.OcclusionExposure, Is.EqualTo(1f));
            Assert.That(effect.DistanceFalloff, Is.InRange(0f, 1f));
            Assert.That(effect.Exposure, Is.EqualTo(effect.DistanceFalloff));
        }

        [Test]
        public void OccludedActorIsRecordedWithoutAnInventedInjuryRegion()
        {
            GameplayWorldRegistry registry = CreateRegistry();
            CreateActor(registry, "target", Vector3.zero);
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Blast Occlusion Wall";
            wall.transform.position = new Vector3(-1f, 1f, 0f);
            wall.transform.localScale = new Vector3(0.2f, 4f, 4f);
            createdObjects.Add(wall);
            Physics.SyncTransforms();
            var query = new UnityBlastWorldQuery(
                registry,
                () => 13L,
                _ => false);

            BlastWorldQueryResult result = query.Query(
                new BlastWorldQuery(
                    new GameplayPosition(-2f, 1.2f, 0f),
                    5f));

            Assert.That(result.Effects, Has.Count.EqualTo(1));
            Assert.That(result.Effects[0].OcclusionExposure, Is.Zero);
            Assert.That(result.Effects[0].Exposure, Is.Zero);
            Assert.That(result.Effects[0].InjuryRegion, Is.Null);
        }

        private GameplayWorldRegistry CreateRegistry()
        {
            var root = new GameObject("Blast Query World");
            createdObjects.Add(root);
            return new GameplayWorldRegistry(new LevelWorld(
                root,
                new Dictionary<string, LevelEntityView>(),
                null));
        }

        private void CreateActor(
            GameplayWorldRegistry registry,
            string actorId,
            Vector3 position)
        {
            var actor = new GameObject(actorId);
            actor.transform.position = position;
            actor.AddComponent<ActorStancePresenter>();
            createdObjects.Add(actor);
            registry.RegisterActor(
                actorId,
                "test",
                targetable: true,
                actor);
        }
    }
}
