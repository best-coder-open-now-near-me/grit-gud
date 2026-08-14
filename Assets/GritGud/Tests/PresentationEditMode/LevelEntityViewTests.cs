using GritGud.Presentation.Levels.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace GritGud.Presentation.Tests
{
    public sealed class LevelEntityViewTests
    {
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
    }
}
