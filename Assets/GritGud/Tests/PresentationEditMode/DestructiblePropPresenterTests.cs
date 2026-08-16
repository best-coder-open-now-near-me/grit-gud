using GritGud.Domain.Gameplay;
using GritGud.Presentation.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace GritGud.Presentation.Tests
{
    public sealed class DestructiblePropPresenterTests
    {
        [Test]
        public void DestroyedPropStopsOccludingTargetRegions()
        {
            var observer = new GameObject("Observer");
            var target = new GameObject("Target");
            var prop = new GameObject("Destructible Prop");
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                observer.transform.position = Vector3.zero;
                target.transform.position = new Vector3(0f, 0f, 5f);
                visual.transform.SetParent(prop.transform, false);
                visual.transform.position = new Vector3(0f, 1f, 2.5f);
                visual.transform.localScale = new Vector3(2f, 2f, 0.4f);
                var presenter = prop.AddComponent<DestructiblePropPresenter>();
                var intact = new DestructiblePropSnapshot(
                    "cover",
                    DestructiblePropState.Intact,
                    10f,
                    10f);
                presenter.Bind(intact);
                Physics.SyncTransforms();
                var query = new UnityTargetExposureQuery(
                    observer.transform,
                    target.transform);
                var region = new TargetRegionSample(
                    TargetRegionId.Torso,
                    new GameplayPosition(0f, 1.2f, 5f),
                    0.2f);

                TargetExposureSnapshot before = query.Capture(
                    "observer",
                    new GameplayPosition(0f, 1.6f, 0f),
                    "target",
                    new[] { region });
                presenter.Present(new DestructiblePropSnapshot(
                    "cover",
                    DestructiblePropState.Destroyed,
                    10f,
                    0f));
                Physics.SyncTransforms();
                TargetExposureSnapshot after = query.Capture(
                    "observer",
                    new GameplayPosition(0f, 1.6f, 0f),
                    "target",
                    new[] { region });

                Assert.That(before.VisibleSampleCount, Is.Zero);
                Assert.That(after.VisibleSampleCount,
                    Is.EqualTo(after.TotalSampleCount));
                Assert.That(after.TotalSampleCount, Is.GreaterThan(0));
                Assert.That(visual.GetComponent<Collider>().enabled, Is.False);
                Assert.That(visual.GetComponent<Renderer>().enabled, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(prop);
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(observer);
            }
        }

        [Test]
        public void DamagedPropChangesItsRealCollisionHeight()
        {
            var prop = new GameObject("Destructible Prop");
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                visual.transform.SetParent(prop.transform, false);
                visual.transform.localScale = new Vector3(1f, 2f, 1f);
                var presenter = prop.AddComponent<DestructiblePropPresenter>();
                presenter.Bind(new DestructiblePropSnapshot(
                    "cover",
                    DestructiblePropState.Intact,
                    10f,
                    10f));
                Physics.SyncTransforms();
                float intactHeight = visual.GetComponent<Collider>().bounds.size.y;

                presenter.Present(new DestructiblePropSnapshot(
                    "cover",
                    DestructiblePropState.Damaged,
                    10f,
                    5f));
                Physics.SyncTransforms();

                Assert.That(visual.GetComponent<Collider>().bounds.size.y,
                    Is.EqualTo(intactHeight * DestructiblePropPresenter.DamagedHeightFraction)
                        .Within(0.001f));
                Assert.That(visual.GetComponent<Collider>().enabled, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(prop);
            }
        }

        [Test]
        public void ToppledPoseChangesRealCollisionAndExposure()
        {
            var observer = new GameObject("Observer");
            var target = new GameObject("Target");
            var prop = new GameObject("Toppling Prop");
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                observer.transform.position = new Vector3(0f, 1.2f, 0f);
                target.transform.position = new Vector3(0f, 1.2f, 5f);
                prop.transform.position = new Vector3(0f, 0f, 2.5f);
                visual.transform.SetParent(prop.transform, false);
                visual.transform.localPosition = Vector3.up;
                visual.transform.localScale = new Vector3(0.5f, 2f, 0.5f);
                var presenter = prop.AddComponent<DestructiblePropPresenter>();
                var upright = new DestructiblePropSnapshot(
                    "cover",
                    DestructiblePropState.Intact,
                    10f,
                    10f,
                    new GameplayPropPose(
                        new GameplayPosition(0f, 0f, 2.5f),
                        0f,
                        0f,
                        0f),
                    DestructiblePropPosture.Upright);
                presenter.Bind(upright);
                Physics.SyncTransforms();
                var query = new UnityTargetExposureQuery(
                    observer.transform,
                    target.transform);
                var region = new TargetRegionSample(
                    TargetRegionId.Torso,
                    new GameplayPosition(0f, 1.2f, 5f),
                    0.1f);

                TargetExposureSnapshot before = query.Capture(
                    "observer",
                    new GameplayPosition(0f, 1.2f, 0f),
                    "target",
                    new[] { region });
                presenter.Present(new DestructiblePropSnapshot(
                    "cover",
                    DestructiblePropState.Intact,
                    10f,
                    10f,
                    new GameplayPropPose(
                        new GameplayPosition(0f, 0.25f, 2.5f),
                        0f,
                        0f,
                        90f),
                    DestructiblePropPosture.Toppled));
                Physics.SyncTransforms();
                TargetExposureSnapshot after = query.Capture(
                    "observer",
                    new GameplayPosition(0f, 1.2f, 0f),
                    "target",
                    new[] { region });

                Assert.That(before.VisibleSampleCount, Is.Zero);
                Assert.That(after.VisibleSampleCount,
                    Is.EqualTo(after.TotalSampleCount));
                Assert.That(prop.transform.position.y, Is.EqualTo(0.25f));
                Assert.That(Quaternion.Angle(
                    prop.transform.rotation,
                    Quaternion.Euler(0f, 0f, 90f)), Is.LessThan(0.01f));
                Assert.That(visual.GetComponent<Collider>().enabled, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(prop);
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(observer);
            }
        }
    }
}
