using GritGud.Presentation.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace GritGud.Presentation.Tests
{
    public sealed class UnityWeaponDischargeOriginResolverTests
    {
        [Test]
        public void CapsuleToMuzzleLineAcrossWallReturnsFirstObstruction()
        {
            var actor = new GameObject("Discharge Clearance Actor");
            var muzzle = new GameObject("Discharge Clearance Muzzle");
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                CharacterController capsule = actor
                    .AddComponent<CharacterController>();
                capsule.center = new Vector3(0f, 0.9f, 0f);
                capsule.height = 1.8f;
                actor.AddComponent<ActorStancePresenter>();
                var view = new GameplayActorView(
                    "actor",
                    "test",
                    targetable: false,
                    actor);
                muzzle.transform.SetParent(actor.transform, false);
                muzzle.transform.localPosition = new Vector3(0f, 1.2f, 2f);
                wall.name = "Barrel Clearance Wall";
                wall.transform.position = new Vector3(0f, 1.2f, 1f);
                wall.transform.localScale = new Vector3(2f, 2f, 0.2f);
                Physics.SyncTransforms();

                var resolver = new UnityWeaponDischargeOriginResolver();
                Assert.That(
                    resolver.TryBuildDischargeLine(
                        view,
                        muzzle.transform,
                        out WeaponDischargeLine line),
                    Is.True);
                bool blocked = resolver.TryResolve(
                    view,
                    muzzle.transform,
                    out RaycastHit obstruction);

                Assert.That(
                    line.AntiMuzzlePosition.z,
                    Is.EqualTo(0.5f).Within(0.01f));
                Assert.That(blocked, Is.True);
                Assert.That(obstruction.collider.gameObject, Is.SameAs(wall));
                Assert.That(obstruction.point.z, Is.EqualTo(0.9f).Within(0.01f));
            }
            finally
            {
                Object.DestroyImmediate(wall);
                Object.DestroyImmediate(actor);
            }
        }

        [Test]
        public void ClearBarrelLineHasNoObstruction()
        {
            var actor = new GameObject("Clear Discharge Line Actor");
            var muzzle = new GameObject("Clear Discharge Line Muzzle");
            try
            {
                CharacterController capsule = actor
                    .AddComponent<CharacterController>();
                capsule.center = new Vector3(0f, 0.9f, 0f);
                capsule.height = 1.8f;
                actor.AddComponent<ActorStancePresenter>();
                var view = new GameplayActorView(
                    "actor",
                    "test",
                    targetable: false,
                    actor);
                muzzle.transform.SetParent(actor.transform, false);
                muzzle.transform.localPosition = new Vector3(0f, 1.2f, 2f);
                Physics.SyncTransforms();

                bool blocked = new UnityWeaponDischargeOriginResolver()
                    .TryResolve(
                        view,
                        muzzle.transform,
                        out _);

                Assert.That(blocked, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(actor);
            }
        }
    }
}
