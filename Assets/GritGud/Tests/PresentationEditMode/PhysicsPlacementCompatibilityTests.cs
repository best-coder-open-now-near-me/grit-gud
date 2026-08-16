using NUnit.Framework;
using GritGud.Presentation.LevelEditing;
using UnityEngine;

namespace GritGud.Presentation.Tests
{
    public sealed class PhysicsPlacementCompatibilityTests
    {
        [Test]
        public void AuthoredBoxColliderDoesNotRequireFallback()
        {
            var owner = new GameObject("Authored Collider");
            try
            {
                BoxCollider collider = owner.AddComponent<BoxCollider>();

                Assert.That(
                    LevelEditorController.RequiresPhysicsBoundsFallback(
                        new Collider[] { collider }),
                    Is.False);
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void MissingOrNonConvexColliderRequiresFallback()
        {
            var owner = new GameObject("Unsafe Collider");
            var mesh = new Mesh();
            try
            {
                MeshCollider collider = owner.AddComponent<MeshCollider>();
                collider.sharedMesh = mesh;
                collider.convex = false;

                Assert.That(
                    LevelEditorController.RequiresPhysicsBoundsFallback(
                        new Collider[] { collider }),
                    Is.True);
                Assert.That(
                    LevelEditorController.RequiresPhysicsBoundsFallback(
                        System.Array.Empty<Collider>()),
                    Is.True);
            }
            finally
            {
                Object.DestroyImmediate(mesh);
                Object.DestroyImmediate(owner);
            }
        }
    }
}
