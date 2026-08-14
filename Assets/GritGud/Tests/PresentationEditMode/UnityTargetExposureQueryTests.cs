using System.Collections.Generic;
using System.Linq;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Presentation.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace GritGud.Presentation.Tests
{
    public sealed class UnityTargetExposureQueryTests
    {
        private readonly List<GameObject> createdObjects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject createdObject in createdObjects)
            {
                Object.DestroyImmediate(createdObject);
            }

            createdObjects.Clear();
        }

        [Test]
        public void RealColliderBlocksAllRegionSamples()
        {
            GameObject observer = CreateActor("Observer", Vector3.zero);
            GameObject target = CreateActor("Target", new Vector3(0f, 0f, 5f));
            GameObject wall = CreatePrimitive(
                "Wall",
                PrimitiveType.Cube,
                new Vector3(0f, 1f, 2.5f),
                new Vector3(3f, 3f, 0.2f));
            Physics.SyncTransforms();
            var query = new UnityTargetExposureQuery(observer.transform, target.transform);

            TargetExposureSnapshot snapshot = query.Capture(
                "observer",
                new GameplayPosition(0f, 1.6f, 0f),
                "target",
                new[]
                {
                    new TargetRegionSample(
                        TargetRegionId.Torso,
                        new GameplayPosition(0f, 1.2f, 5f),
                        0.2f),
                });

            Assert.That(wall, Is.Not.Null);
            Assert.That(snapshot.VisibleSampleCount, Is.Zero);
            Assert.That(snapshot.TotalSampleCount, Is.GreaterThan(0));
        }

        [Test]
        public void ObserverAndTargetCollidersDoNotOccludeTheirOwnQuery()
        {
            GameObject observer = CreateActor("Observer", Vector3.zero);
            GameObject target = CreateActor("Target", new Vector3(0f, 0f, 5f));
            Physics.SyncTransforms();
            var query = new UnityTargetExposureQuery(observer.transform, target.transform);

            TargetExposureSnapshot snapshot = query.Capture(
                "observer",
                new GameplayPosition(0f, 1.6f, 0f),
                "target",
                new[]
                {
                    new TargetRegionSample(
                        TargetRegionId.Torso,
                        new GameplayPosition(0f, 1.2f, 5f),
                        0.2f),
                });

            Assert.That(snapshot.VisibleSampleCount,
                Is.EqualTo(snapshot.TotalSampleCount));
            Assert.That(snapshot.TotalSampleCount, Is.GreaterThan(0));
        }

        [Test]
        public void UnchangedExposureReusesRasterUntilWorldRevisionChanges()
        {
            GameObject observer = CreateActor("Observer", Vector3.zero);
            GameObject target = CreateActor("Target", new Vector3(0f, 0f, 5f));
            long revision = 4L;
            Physics.SyncTransforms();
            var query = new UnityTargetExposureQuery(
                observer.transform,
                target.transform,
                Physics.DefaultRaycastLayers,
                () => revision);
            var regions = new[]
            {
                new TargetRegionSample(
                    TargetRegionId.Torso,
                    new GameplayPosition(0f, 1.2f, 5f),
                    0.2f),
            };

            TargetExposureSnapshot first = query.Capture(
                "observer",
                new GameplayPosition(0f, 1.6f, 0f),
                "target",
                regions);
            TargetExposureSnapshot unchanged = query.Capture(
                "observer",
                new GameplayPosition(0f, 1.6f, 0f),
                "target",
                regions);

            Assert.That(unchanged, Is.SameAs(first));
            Assert.That(query.RasterEvaluationCount, Is.EqualTo(1));

            revision++;
            TargetExposureSnapshot changedWorld = query.Capture(
                "observer",
                new GameplayPosition(0f, 1.6f, 0f),
                "target",
                regions);

            Assert.That(changedWorld, Is.Not.SameAs(first));
            Assert.That(query.RasterEvaluationCount, Is.EqualTo(2));
        }

        [Test]
        public void SmokeObscuranceBlocksExposureAndInvalidatesCachedRaster()
        {
            GameObject observer = CreateActor("Observer", Vector3.zero);
            GameObject target = CreateActor("Target", new Vector3(0f, 0f, 5f));
            var obscurance = new MutableObscuranceQuery();
            Physics.SyncTransforms();
            var query = new UnityTargetExposureQuery(
                observer.transform,
                target.transform,
                Physics.DefaultRaycastLayers,
                obscuranceQuery: obscurance);
            var regions = new[]
            {
                new TargetRegionSample(
                    TargetRegionId.Torso,
                    new GameplayPosition(0f, 1.2f, 5f),
                    0.2f),
            };

            TargetExposureSnapshot clear = query.Capture(
                "observer",
                new GameplayPosition(0f, 1.6f, 0f),
                "target",
                regions);
            obscurance.SetBlocked(true);
            TargetExposureSnapshot obscured = query.Capture(
                "observer",
                new GameplayPosition(0f, 1.6f, 0f),
                "target",
                regions);

            Assert.That(clear.VisibleSampleCount,
                Is.EqualTo(clear.TotalSampleCount));
            Assert.That(obscured.VisibleSampleCount, Is.Zero);
            Assert.That(query.RasterEvaluationCount, Is.EqualTo(2));
        }

        [Test]
        public void NearBodyRegionOccludesFarBodyRegionFromHitLocationRoll()
        {
            GameObject observer = CreateActor("Observer", Vector3.zero);
            GameObject target = CreateActor("Target", new Vector3(0f, 0f, 5f));
            Physics.SyncTransforms();
            var query = new UnityTargetExposureQuery(
                observer.transform,
                target.transform);

            TargetExposureSnapshot snapshot = query.Capture(
                "observer",
                new GameplayPosition(0f, 1.2f, 0f),
                "target",
                new[]
                {
                    new TargetRegionSample(
                        TargetRegionId.Torso,
                        new GameplayPosition(0f, 1.2f, 4.5f),
                        0.3f),
                    new TargetRegionSample(
                        TargetRegionId.LeftArm,
                        new GameplayPosition(0f, 1.2f, 5f),
                        0.12f),
                });

            Assert.That(
                snapshot.GetRegion(TargetRegionId.Torso).VisibleSampleCount,
                Is.EqualTo(
                    snapshot.GetRegion(TargetRegionId.Torso).TotalSampleCount));
            Assert.That(
                snapshot.GetRegion(TargetRegionId.LeftArm).VisibleSampleCount,
                Is.Zero);
            Assert.That(
                snapshot.GetRegion(TargetRegionId.LeftArm).TotalSampleCount,
                Is.Zero);
        }

        [Test]
        public void FlattenedSilhouetteWeightsRegionsByPaintedArea()
        {
            GameObject observer = CreateActor("Observer", Vector3.zero);
            GameObject target = CreateActor("Target", new Vector3(0f, 0f, 5f));
            Physics.SyncTransforms();
            var query = new UnityTargetExposureQuery(
                observer.transform,
                target.transform);

            TargetExposureSnapshot snapshot = query.Capture(
                "observer",
                new GameplayPosition(0f, 1.2f, 0f),
                "target",
                new[]
                {
                    new TargetRegionSample(
                        TargetRegionId.Torso,
                        new GameplayPosition(-0.35f, 1.2f, 5f),
                        0.3f),
                    new TargetRegionSample(
                        TargetRegionId.LeftArm,
                        new GameplayPosition(0.35f, 1.2f, 5f),
                        0.12f),
                });

            TargetRegionExposure torso = snapshot.GetRegion(
                TargetRegionId.Torso);
            TargetRegionExposure arm = snapshot.GetRegion(
                TargetRegionId.LeftArm);
            Assert.That(torso.VisibleSampleCount,
                Is.EqualTo(torso.TotalSampleCount));
            Assert.That(arm.VisibleSampleCount,
                Is.EqualTo(arm.TotalSampleCount));
            Assert.That(torso.TotalSampleCount,
                Is.GreaterThan(arm.TotalSampleCount));
            Assert.That(
                TargetExposureRules.CalculateHitChancePercent(snapshot),
                Is.EqualTo(100));
        }

        [Test]
        public void CrouchingBehindLowGeometryReducesVisibleSamples()
        {
            GameObject observer = CreateActor("Observer", Vector3.zero);
            GameObject target = CreateActor("Target", new Vector3(0f, 0f, 5f));
            ActorStancePresenter observerStance =
                observer.GetComponent<ActorStancePresenter>();
            ActorStancePresenter targetStance =
                target.GetComponent<ActorStancePresenter>();
            CreatePrimitive(
                "Low Cover",
                PrimitiveType.Cube,
                new Vector3(0f, 0.7f, 2.5f),
                new Vector3(2f, 1.4f, 0.25f));
            Physics.SyncTransforms();
            var query = new UnityTargetExposureQuery(observer.transform, target.transform);

            TargetExposureSnapshot standing = Capture(
                query,
                observerStance,
                targetStance);
            targetStance.ApplyResolved(ActorStance.Crouched);
            Physics.SyncTransforms();
            TargetExposureSnapshot crouched = Capture(
                query,
                observerStance,
                targetStance);

            Assert.That(
                TargetExposureRules.CalculateHitChancePercent(crouched),
                Is.LessThan(
                    TargetExposureRules.CalculateHitChancePercent(standing)));
            Assert.That(
                crouched.GetRegion(TargetRegionId.Torso).VisibleFraction,
                Is.LessThan(
                    standing.GetRegion(TargetRegionId.Torso).VisibleFraction));
        }

        private static TargetExposureSnapshot Capture(
            UnityTargetExposureQuery query,
            ActorStancePresenter observer,
            ActorStancePresenter target)
        {
            TargetRegionSample[] regions = target.GetTargetRegionSamples()
                .Select(region => new TargetRegionSample(
                    region.Id,
                    new GameplayPosition(
                        region.WorldCenter.x,
                        region.WorldCenter.y,
                        region.WorldCenter.z),
                    region.Radius))
                .ToArray();
            Vector3 origin = observer.FirstPersonEyePosition;
            return query.Capture(
                "observer",
                new GameplayPosition(origin.x, origin.y, origin.z),
                "target",
                regions);
        }

        private GameObject CreateActor(string name, Vector3 position)
        {
            var actor = new GameObject(name);
            actor.transform.position = position;
            CharacterController controller = actor.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.radius = 0.35f;
            actor.AddComponent<ActorStancePresenter>();
            createdObjects.Add(actor);
            return actor;
        }

        private GameObject CreatePrimitive(
            string name,
            PrimitiveType type,
            Vector3 position,
            Vector3 scale)
        {
            GameObject primitive = GameObject.CreatePrimitive(type);
            primitive.name = name;
            primitive.transform.position = position;
            primitive.transform.localScale = scale;
            createdObjects.Add(primitive);
            return primitive;
        }

        private sealed class MutableObscuranceQuery : ISightObscuranceQuery
        {
            private bool blocked;

            public long Revision { get; private set; }

            public bool BlocksSight(
                GameplayPosition origin,
                GameplayPosition destination) => blocked;

            public void SetBlocked(bool value)
            {
                blocked = value;
                Revision++;
            }
        }
    }
}
