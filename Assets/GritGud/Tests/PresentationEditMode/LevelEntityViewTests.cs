using GritGud.Presentation.Levels.Runtime;
using GritGud.Domain.Levels;
using NUnit.Framework;
using UnityEngine;

namespace GritGud.Presentation.Tests
{
    public sealed class LevelEntityViewTests
    {
        [Test]
        public void EntityViewRoundTripsThreeAxisRotation()
        {
            var owner = new GameObject("Three Axis Transform Test");
            LevelEntityView view = owner.AddComponent<LevelEntityView>();
            try
            {
                view.ApplyTransform(new LevelTransformData(
                    new Float3Data(1f, 2f, 3f),
                    20f,
                    -35f,
                    12f));

                LevelTransformData result = view.ReadTransform();

                Assert.That(result.position.x, Is.EqualTo(1f));
                Assert.That(result.pitchDegrees, Is.EqualTo(20f).Within(0.001f));
                Assert.That(result.yawDegrees, Is.EqualTo(-35f).Within(0.001f));
                Assert.That(result.rollDegrees, Is.EqualTo(12f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void BoundsPivotMapsTopViewCoordinatesToBottomFace()
        {
            var bounds = new Bounds(new Vector3(2f, 3f, 4f), new Vector3(6f, 8f, 10f));

            Vector3 pivot = LevelEntityView.CalculateBoundsPivot(bounds, -1f, 1f);

            Assert.That(pivot, Is.EqualTo(new Vector3(-1f, -1f, 9f)));
        }

        [Test]
        public void TransformBoundsAccountsForEntityYaw()
        {
            var owner = new GameObject("Rotated Bounds Test");
            try
            {
                owner.transform.SetPositionAndRotation(
                    new Vector3(5f, 2f, -3f),
                    Quaternion.Euler(0f, 90f, 0f));
                var localBounds = new Bounds(Vector3.zero, new Vector3(6f, 2f, 1f));

                Bounds worldBounds = LevelEntityView.TransformBounds(localBounds, owner.transform);

                Assert.That(worldBounds.center, Is.EqualTo(owner.transform.position));
                Assert.That(worldBounds.size.x, Is.EqualTo(1f).Within(0.001f));
                Assert.That(worldBounds.size.y, Is.EqualTo(2f).Within(0.001f));
                Assert.That(worldBounds.size.z, Is.EqualTo(6f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void TransformBoundsAccountsForScaleAndLocalCenter()
        {
            var owner = new GameObject("Scaled Bounds Test");
            try
            {
                owner.transform.position = new Vector3(2f, 3f, 4f);
                owner.transform.localScale = new Vector3(2f, 3f, 4f);
                var localBounds = new Bounds(new Vector3(1f, 0.5f, -1f), Vector3.one);

                Bounds worldBounds = LevelEntityView.TransformBounds(localBounds, owner.transform);

                Assert.That(worldBounds.center, Is.EqualTo(new Vector3(4f, 4.5f, 0f)));
                Assert.That(worldBounds.size, Is.EqualTo(new Vector3(2f, 3f, 4f)));
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void CalculateVisualLocalBoundsUsesMeshGeometryInsteadOfAuthoredFallback()
        {
            var root = new GameObject("Visual Bounds Root");
            var child = new GameObject("Offset Mesh");
            var mesh = new Mesh
            {
                vertices = new[]
                {
                    new Vector3(-0.5f, -0.5f, -0.5f),
                    new Vector3(0.5f, 0.5f, 0.5f),
                },
            };
            child.transform.SetParent(root.transform, false);
            child.transform.localPosition = new Vector3(2f, 1f, -1f);
            child.AddComponent<MeshFilter>().sharedMesh = mesh;

            try
            {
                Bounds bounds = LevelEntityView.CalculateVisualLocalBounds(
                    root,
                    new Bounds(Vector3.zero, Vector3.one * 20f));

                Assert.That(bounds.center, Is.EqualTo(new Vector3(2f, 1f, -1f)));
                Assert.That(bounds.size, Is.EqualTo(Vector3.one));
            }
            finally
            {
                Object.DestroyImmediate(mesh);
                Object.DestroyImmediate(root);
            }
        }
    }
}
