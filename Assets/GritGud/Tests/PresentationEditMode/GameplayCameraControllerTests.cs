using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Presentation.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Utils;

namespace GritGud.Presentation.Tests
{
    public sealed class GameplayCameraControllerTests
    {
        [Test]
        public void CameraModesCullOnlyLocalVisualsAndRestorePresentationState()
        {
            var actor = new GameObject("Camera Test Actor");
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var cameraObject = new GameObject("Camera Mode Test");
            try
            {
                CharacterController characterController =
                    actor.AddComponent<CharacterController>();
                characterController.height = 2f;
                characterController.center = new Vector3(0f, 1f, 0f);
                var stancePresenter = actor.AddComponent<ActorStancePresenter>();
                visual.name = "Camera Test Visual";
                visual.transform.SetParent(actor.transform, false);
                int originalVisualLayer = visual.layer;

                Camera camera = cameraObject.AddComponent<Camera>();
                int localPlayerLayer = LayerMask.NameToLayer(
                    GameplayCameraController.LocalPlayerLayerName);
                Assert.That(localPlayerLayer, Is.GreaterThanOrEqualTo(0));
                int localPlayerMask = 1 << localPlayerLayer;
                int originalCullingMask = camera.cullingMask & ~localPlayerMask;
                camera.cullingMask = originalCullingMask;
                var controller =
                    cameraObject.AddComponent<GameplayCameraController>();

                controller.Bind(
                    actor.transform,
                    new EmptyInputSource(),
                    stancePresenter);

                Assert.That(controller.View,
                    Is.EqualTo(GameplayCameraView.ThirdPerson));
                Assert.That(controller.Target, Is.SameAs(actor.transform));
                Assert.That(visual.layer, Is.EqualTo(localPlayerLayer));
                Assert.That(camera.cullingMask & localPlayerMask,
                    Is.EqualTo(localPlayerMask));
                float standingThirdPersonHeight =
                    camera.transform.position.y - actor.transform.position.y;
                float standingEyeHeight = stancePresenter.FirstPersonEyePosition.y;

                controller.ToggleView();
                controller.RefreshNow();

                Assert.That(controller.View,
                    Is.EqualTo(GameplayCameraView.FirstPerson));
                Assert.That(camera.transform.position,
                    Is.EqualTo(stancePresenter.FirstPersonEyePosition)
                        .Using(Vector3ComparerWithEqualsOperator.Instance));
                Assert.That(camera.cullingMask & localPlayerMask, Is.Zero);

                stancePresenter.ApplyResolved(ActorStance.Crouched);
                controller.RefreshNow();

                Assert.That(camera.transform.position.y,
                    Is.LessThan(standingEyeHeight));
                Assert.That(camera.transform.position,
                    Is.EqualTo(stancePresenter.FirstPersonEyePosition)
                        .Using(Vector3ComparerWithEqualsOperator.Instance));

                controller.ToggleView();

                Assert.That(controller.View,
                    Is.EqualTo(GameplayCameraView.ThirdPerson));
                float crouchedThirdPersonHeight =
                    camera.transform.position.y - actor.transform.position.y;
                float crouchedEyeHeight = stancePresenter.FirstPersonEyePosition.y;
                Assert.That(standingThirdPersonHeight - crouchedThirdPersonHeight,
                    Is.EqualTo(standingEyeHeight - crouchedEyeHeight).Within(0.001f));
                Assert.That(camera.cullingMask & localPlayerMask,
                    Is.EqualTo(localPlayerMask));

                controller.Unbind();

                Assert.That(controller.Target, Is.Null);
                Assert.That(visual.layer, Is.EqualTo(originalVisualLayer));
                Assert.That(camera.cullingMask, Is.EqualTo(originalCullingMask));
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(actor);
            }
        }

        [Test]
        public void MouseWheelTraversesThirdPersonZoomAndFirstPersonEndpoints()
        {
            var actor = new GameObject("Camera Zoom Test Actor");
            var cameraObject = new GameObject("Camera Zoom Test");
            try
            {
                actor.AddComponent<CharacterController>();
                var stancePresenter = actor.AddComponent<ActorStancePresenter>();
                cameraObject.AddComponent<Camera>();
                var controller =
                    cameraObject.AddComponent<GameplayCameraController>();
                controller.Bind(
                    actor.transform,
                    new EmptyInputSource(),
                    stancePresenter);
                Vector3 fullThirdPersonPosition = cameraObject.transform.position;

                controller.ApplyZoomInput(1f);

                Assert.That(controller.View,
                    Is.EqualTo(GameplayCameraView.ThirdPerson));
                Assert.That(controller.ThirdPersonZoom, Is.EqualTo(0.75f));
                Assert.That(
                    Vector3.Distance(
                        cameraObject.transform.position,
                        stancePresenter.FirstPersonEyePosition),
                    Is.LessThan(Vector3.Distance(
                        fullThirdPersonPosition,
                        stancePresenter.FirstPersonEyePosition)));

                controller.ApplyZoomInput(1f);
                controller.ApplyZoomInput(1f);
                controller.ApplyZoomInput(1f);

                Assert.That(controller.View,
                    Is.EqualTo(GameplayCameraView.FirstPerson));
                Assert.That(cameraObject.transform.position,
                    Is.EqualTo(stancePresenter.FirstPersonEyePosition)
                        .Using(Vector3ComparerWithEqualsOperator.Instance));

                controller.ApplyZoomInput(-1f);

                Assert.That(controller.View,
                    Is.EqualTo(GameplayCameraView.ThirdPerson));
                Assert.That(controller.ThirdPersonZoom, Is.EqualTo(0.25f));
                Assert.That(
                    Vector3.Distance(
                        cameraObject.transform.position,
                        stancePresenter.FirstPersonEyePosition),
                    Is.GreaterThan(0.01f));
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(actor);
            }
        }

        [Test]
        public void RetargetingPreservesViewAndRestoresPreviousActorLayers()
        {
            var firstActor = new GameObject("First Camera Actor");
            var secondActor = new GameObject("Second Camera Actor");
            GameObject firstVisual = GameObject.CreatePrimitive(
                PrimitiveType.Cube);
            GameObject secondVisual = GameObject.CreatePrimitive(
                PrimitiveType.Cube);
            var cameraObject = new GameObject("Retargeting Camera");
            try
            {
                firstActor.AddComponent<CharacterController>();
                secondActor.AddComponent<CharacterController>();
                var firstStance = firstActor.AddComponent<ActorStancePresenter>();
                var secondStance = secondActor.AddComponent<ActorStancePresenter>();
                firstActor.transform.position = new Vector3(2f, 0f, 3f);
                secondActor.transform.position = new Vector3(-4f, 0f, 8f);
                firstVisual.transform.SetParent(firstActor.transform, false);
                secondVisual.transform.SetParent(secondActor.transform, false);
                int firstOriginalLayer = firstVisual.layer;
                int secondOriginalLayer = secondVisual.layer;

                Camera camera = cameraObject.AddComponent<Camera>();
                int localPlayerLayer = LayerMask.NameToLayer(
                    GameplayCameraController.LocalPlayerLayerName);
                int localPlayerMask = 1 << localPlayerLayer;
                int originalCullingMask = camera.cullingMask & ~localPlayerMask;
                camera.cullingMask = originalCullingMask;
                GameplayCameraController controller = cameraObject
                    .AddComponent<GameplayCameraController>();
                controller.Bind(
                    firstActor.transform,
                    new EmptyInputSource(),
                    firstStance);
                controller.ToggleView();

                controller.SetTarget(secondActor.transform, secondStance);

                Assert.That(controller.Target, Is.SameAs(secondActor.transform));
                Assert.That(controller.View,
                    Is.EqualTo(GameplayCameraView.FirstPerson));
                Assert.That(firstVisual.layer, Is.EqualTo(firstOriginalLayer));
                Assert.That(secondVisual.layer, Is.EqualTo(localPlayerLayer));
                Assert.That(camera.cullingMask & localPlayerMask, Is.Zero);
                Assert.That(camera.transform.position,
                    Is.EqualTo(secondStance.FirstPersonEyePosition)
                        .Using(Vector3ComparerWithEqualsOperator.Instance));

                controller.Unbind();

                Assert.That(secondVisual.layer, Is.EqualTo(secondOriginalLayer));
                Assert.That(camera.cullingMask, Is.EqualTo(originalCullingMask));
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(firstActor);
                Object.DestroyImmediate(secondActor);
            }
        }

        [Test]
        public void ReapplyingSameTargetPreservesPlayerOrbit()
        {
            var actor = new GameObject("Stable Camera Actor");
            var cameraObject = new GameObject("Stable Camera");
            try
            {
                actor.AddComponent<CharacterController>();
                var stance = actor.AddComponent<ActorStancePresenter>();
                cameraObject.AddComponent<Camera>();
                GameplayCameraController controller = cameraObject
                    .AddComponent<GameplayCameraController>();
                controller.Bind(
                    actor.transform,
                    new EmptyInputSource(),
                    stance);
                Vector3 playerChosenPosition = cameraObject.transform.position;

                actor.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
                controller.SetTarget(actor.transform, stance);

                Assert.That(cameraObject.transform.position,
                    Is.EqualTo(playerChosenPosition)
                        .Using(Vector3ComparerWithEqualsOperator.Instance));
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(actor);
            }
        }

        private sealed class EmptyInputSource : IGameplayInputSource
        {
            public GameplayInputFrame CurrentFrame => default;

            public string GetBindingDisplay(GameplayControl control) =>
                control.ToString();
        }
    }
}
