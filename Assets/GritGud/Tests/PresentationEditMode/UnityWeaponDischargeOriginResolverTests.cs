using GritGud.Presentation.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Utils;

namespace GritGud.Presentation.Tests
{
    public sealed class UnityWeaponDischargeOriginResolverTests
    {
        [Test]
        public void MuzzleAcrossWallFallsBackToActorEye()
        {
            var actor = new GameObject("Discharge Origin Actor");
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                CharacterController controller = actor
                    .AddComponent<CharacterController>();
                controller.center = new Vector3(0f, 0.9f, 0f);
                controller.height = 1.8f;
                ActorStancePresenter stance = actor
                    .AddComponent<ActorStancePresenter>();
                var view = new GameplayActorView(
                    "actor",
                    "test",
                    targetable: false,
                    actor);
                Vector3 eye = stance.FirstPersonEyePosition;
                Vector3 muzzle = eye + (Vector3.forward * 2f);
                wall.name = "Muzzle Clearance Wall";
                wall.transform.position = eye + Vector3.forward;
                wall.transform.localScale = new Vector3(2f, 2f, 0.2f);
                Physics.SyncTransforms();

                Vector3 resolved = new UnityWeaponDischargeOriginResolver()
                    .Resolve(view, muzzle);

                Assert.That(
                    resolved,
                    Is.EqualTo(eye)
                        .Using(Vector3ComparerWithEqualsOperator.Instance));
            }
            finally
            {
                Object.DestroyImmediate(wall);
                Object.DestroyImmediate(actor);
            }
        }

        [Test]
        public void ClearMuzzleUsesPresentedOrigin()
        {
            var actor = new GameObject("Clear Discharge Origin Actor");
            try
            {
                CharacterController controller = actor
                    .AddComponent<CharacterController>();
                controller.center = new Vector3(0f, 0.9f, 0f);
                controller.height = 1.8f;
                ActorStancePresenter stance = actor
                    .AddComponent<ActorStancePresenter>();
                var view = new GameplayActorView(
                    "actor",
                    "test",
                    targetable: false,
                    actor);
                Vector3 muzzle = stance.FirstPersonEyePosition
                    + (Vector3.forward * 2f);
                Physics.SyncTransforms();

                Vector3 resolved = new UnityWeaponDischargeOriginResolver()
                    .Resolve(view, muzzle);

                Assert.That(
                    resolved,
                    Is.EqualTo(muzzle)
                        .Using(Vector3ComparerWithEqualsOperator.Instance));
            }
            finally
            {
                Object.DestroyImmediate(actor);
            }
        }
    }
}
