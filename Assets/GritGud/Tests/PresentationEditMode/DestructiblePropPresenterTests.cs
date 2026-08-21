using GritGud.Application.Gameplay;
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
        public void DamagedPropWithoutFractureProfileRetainsItsAuthoredCollision()
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
                    Is.EqualTo(intactHeight).Within(0.001f));
                Assert.That(visual.GetComponent<Collider>().enabled, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(prop);
            }
        }

        [Test]
        public void BakedFractureReplacesOriginalAndClearsTransientDebrisOnSeek()
        {
            var prop = new GameObject("Destructible Prop");
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var fracturePrefab = new GameObject("Fracture Prefab");
            var profile = ScriptableObject.CreateInstance<
                DestructibleFractureProfile>();
            try
            {
                visual.transform.SetParent(prop.transform, false);
                var centers = new[]
                {
                    new Vector3(-0.25f, 0.25f, 0f),
                    new Vector3(0.25f, 0.25f, 0f),
                    new Vector3(0f, 0.75f, 0f),
                };
                for (int index = 0; index < centers.Length; index++)
                {
                    GameObject chunk = GameObject.CreatePrimitive(
                        PrimitiveType.Cube);
                    chunk.name = $"Chunk {index}";
                    chunk.transform.SetParent(fracturePrefab.transform, false);
                    chunk.transform.localPosition = centers[index];
                    chunk.transform.localScale = Vector3.one * 0.4f;
                    chunk.AddComponent<DestructibleFractureChunk>()
                        .Configure(index);
                }
                profile.Configure(
                    "test.fracture",
                    fracturePrefab,
                    centers,
                    impulse: 1f,
                    lifetime: 2f);
                var presenter = prop.AddComponent<DestructiblePropPresenter>();
                var intact = new DestructiblePropSnapshot(
                    "cover",
                    DestructiblePropState.Intact,
                    10f,
                    10f,
                    new GameplayPropPose(
                        new GameplayPosition(0f, 0f, 0f),
                        0f,
                        0f,
                        0f),
                    DestructiblePropPosture.Upright,
                    fractureChunkCount: 3,
                    detachedFractureChunks: 0UL);
                presenter.Bind(intact, profile);
                var resulting = new DestructiblePropSnapshot(
                    "cover",
                    DestructiblePropState.Damaged,
                    10f,
                    5f,
                    intact.Pose,
                    intact.Posture,
                    fractureChunkCount: 3,
                    detachedFractureChunks: 0b011UL);
                var record = new DestructibleDamageRecord(
                    1L,
                    5f,
                    intact,
                    resulting,
                    preferredFractureChunkIndex: 1);

                presenter.PresentDamage(record, spawnTransientDebris: true);

                Assert.That(visual.GetComponent<Renderer>().enabled, Is.False);
                Assert.That(visual.GetComponent<Collider>().enabled, Is.False);
                DestructibleFractureChunk[] presented = prop
                    .GetComponentsInChildren<DestructibleFractureChunk>(true);
                Assert.That(
                    presented[0].gameObject.activeSelf,
                    Is.False);
                Assert.That(
                    presented[1].gameObject.activeSelf,
                    Is.False);
                Assert.That(
                    presented[2].gameObject.activeSelf,
                    Is.True);
                Assert.That(presenter.ActiveTransientDebrisCount, Is.EqualTo(2));

                presenter.ClearTransientDebris();
                Assert.That(presenter.ActiveTransientDebrisCount, Is.Zero);
                presenter.Present(intact);
                Assert.That(visual.GetComponent<Renderer>().enabled, Is.True);
                Assert.That(visual.GetComponent<Collider>().enabled, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(prop);
                Object.DestroyImmediate(fracturePrefab);
                Object.DestroyImmediate(profile);
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

        [Test]
        public void PushMovesCollisionImmediatelyButInterpolatesVisibleProp()
        {
            var prop = new GameObject("Pushed Prop");
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                visual.transform.SetParent(prop.transform, false);
                var presenter = prop.AddComponent<DestructiblePropPresenter>();
                var previousPose = new GameplayPropPose(
                    new GameplayPosition(0f, 0f, 0f),
                    0f,
                    0f,
                    0f);
                var resultingPose = new GameplayPropPose(
                    new GameplayPosition(2f, 0f, 0f),
                    0f,
                    90f,
                    0f);
                presenter.Bind(new DestructiblePropSnapshot(
                    "crate",
                    DestructiblePropState.Intact,
                    10f,
                    10f,
                    previousPose,
                    DestructiblePropPosture.Upright));
                var record = new DisplacementRecord(
                    1,
                    new DisplacementRequest(
                        "actor",
                        "push",
                        "crate",
                        DisplacementSubjectKind.Prop,
                        30f,
                        resultingPose.Position,
                        DisplacementActionKind.Push),
                    new PropDisplacementState(
                        previousPose,
                        DestructiblePropPosture.Upright),
                    new PropDisplacementState(
                        resultingPose,
                        DestructiblePropPosture.Upright));

                presenter.PresentDisplacement(
                    record,
                    GameplayDisplacementPresentationTiming.PushSeconds);
                Physics.SyncTransforms();

                Assert.That(prop.transform.position.x, Is.EqualTo(2f));
                Assert.That(
                    visual.GetComponent<Collider>().bounds.center.x,
                    Is.EqualTo(2f).Within(0.001f));
                Assert.That(visual.GetComponent<Renderer>().enabled, Is.False);
                Assert.That(presenter.IsPresentingDisplacement, Is.True);
                Assert.That(
                    presenter.DisplacementVisualPosition.x,
                    Is.EqualTo(0f).Within(0.001f));

                presenter.TickDisplacement(
                    GameplayDisplacementPresentationTiming.PushSeconds
                        * GameplayDisplacementPresentationTiming
                            .PushContactNormalizedTime);
                Assert.That(
                    presenter.DisplacementVisualPosition.x,
                    Is.EqualTo(0f).Within(0.001f));

                presenter.TickDisplacement(
                    GameplayDisplacementPresentationTiming.PushSeconds
                        * 0.3f);
                Assert.That(
                    presenter.DisplacementVisualPosition.x,
                    Is.InRange(0.1f, 1.9f));

                presenter.TickDisplacement(
                    GameplayDisplacementPresentationTiming.PushSeconds);
                Assert.That(presenter.IsPresentingDisplacement, Is.False);
                Assert.That(visual.GetComponent<Renderer>().enabled, Is.True);
                Assert.That(prop.transform.position.x, Is.EqualTo(2f));
            }
            finally
            {
                Object.DestroyImmediate(prop);
            }
        }

        [Test]
        public void PushVisualPreservesAuthoredMaterialPropertyOverrides()
        {
            var prop = new GameObject("Pushed Prop");
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                visual.transform.SetParent(prop.transform, false);
                Renderer sourceRenderer = visual.GetComponent<Renderer>();
                var authoredColor = new Color(0.12f, 0.34f, 0.56f, 1f);
                var authoredProperties = new MaterialPropertyBlock();
                authoredProperties.SetColor("_Color", authoredColor);
                authoredProperties.SetColor("_BaseColor", authoredColor);
                sourceRenderer.SetPropertyBlock(authoredProperties);

                var presenter = prop.AddComponent<DestructiblePropPresenter>();
                var previousPose = new GameplayPropPose(
                    new GameplayPosition(0f, 0f, 0f), 0f, 0f, 0f);
                var resultingPose = new GameplayPropPose(
                    new GameplayPosition(2f, 0f, 0f), 0f, 0f, 0f);
                presenter.Bind(new DestructiblePropSnapshot(
                    "barrel",
                    DestructiblePropState.Intact,
                    10f,
                    10f,
                    previousPose,
                    DestructiblePropPosture.Upright));
                var record = new DisplacementRecord(
                    1,
                    new DisplacementRequest(
                        "actor",
                        "push",
                        "barrel",
                        DisplacementSubjectKind.Prop,
                        30f,
                        resultingPose.Position,
                        DisplacementActionKind.Push),
                    new PropDisplacementState(
                        previousPose,
                        DestructiblePropPosture.Upright),
                    new PropDisplacementState(
                        resultingPose,
                        DestructiblePropPosture.Upright));

                presenter.PresentDisplacement(
                    record,
                    GameplayDisplacementPresentationTiming.PushSeconds);

                Renderer[] renderers = Object.FindObjectsOfType<Renderer>();
                Renderer displacementRenderer = System.Array.Find(
                    renderers,
                    candidate => candidate.gameObject.name.Contains(
                        "[Displacement]"));
                Assert.That(displacementRenderer, Is.Not.Null);
                var presentedProperties = new MaterialPropertyBlock();
                displacementRenderer.GetPropertyBlock(presentedProperties);
                Color presentedColor =
                    presentedProperties.GetColor("_BaseColor");
                Assert.That(
                    presentedColor.r,
                    Is.EqualTo(authoredColor.r).Within(0.0001f));
                Assert.That(
                    presentedColor.g,
                    Is.EqualTo(authoredColor.g).Within(0.0001f));
                Assert.That(
                    presentedColor.b,
                    Is.EqualTo(authoredColor.b).Within(0.0001f));
                Assert.That(
                    presentedColor.a,
                    Is.EqualTo(authoredColor.a).Within(0.0001f));
                presenter.Unbind();
            }
            finally
            {
                Object.DestroyImmediate(prop);
            }
        }
    }
}
