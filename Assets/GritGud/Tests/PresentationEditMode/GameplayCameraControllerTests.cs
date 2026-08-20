using ArgumentNullException = System.ArgumentNullException;
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
        public void CameraModesHideOnlyLocalVisualsWithoutChangingLayersOrMask()
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
                Collider visualCollider = visual.GetComponent<Collider>();

                Camera camera = cameraObject.AddComponent<Camera>();
                int originalCullingMask = camera.cullingMask;
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
                Assert.That(visual.layer, Is.EqualTo(originalVisualLayer));
                Assert.That(visualCollider.gameObject.layer,
                    Is.EqualTo(originalVisualLayer));
                Assert.That(visual.GetComponent<Renderer>().forceRenderingOff,
                    Is.False);
                Assert.That(camera.cullingMask, Is.EqualTo(originalCullingMask));
                float standingThirdPersonHeight =
                    camera.transform.position.y - actor.transform.position.y;
                float standingEyeHeight = stancePresenter.FirstPersonEyePosition.y;
                int externallyChangedMask = 1 << 0;
                camera.cullingMask = externallyChangedMask;

                controller.ToggleView();
                controller.RefreshNow();

                Assert.That(controller.View,
                    Is.EqualTo(GameplayCameraView.FirstPerson));
                Assert.That(camera.transform.position,
                    Is.EqualTo(stancePresenter.FirstPersonEyePosition)
                        .Using(Vector3ComparerWithEqualsOperator.Instance));
                Assert.That(visual.GetComponent<Renderer>().forceRenderingOff,
                    Is.True);
                Assert.That(camera.cullingMask, Is.EqualTo(externallyChangedMask));

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
                Assert.That(visual.GetComponent<Renderer>().forceRenderingOff,
                    Is.False);
                Assert.That(camera.cullingMask, Is.EqualTo(externallyChangedMask));

                controller.Unbind();

                Assert.That(controller.Target, Is.Null);
                Assert.That(visual.layer, Is.EqualTo(originalVisualLayer));
                Assert.That(visualCollider.gameObject.layer,
                    Is.EqualTo(originalVisualLayer));
                Assert.That(visual.GetComponent<Renderer>().forceRenderingOff,
                    Is.False);
                Assert.That(camera.cullingMask, Is.EqualTo(externallyChangedMask));
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
        public void RetargetingPreservesViewAndRestoresPreviousActorVisibility()
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
                int originalCullingMask = camera.cullingMask;
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
                Assert.That(secondVisual.layer, Is.EqualTo(secondOriginalLayer));
                Assert.That(firstVisual.GetComponent<Renderer>().forceRenderingOff,
                    Is.False);
                Assert.That(secondVisual.GetComponent<Renderer>().forceRenderingOff,
                    Is.True);
                Assert.That(camera.cullingMask, Is.EqualTo(originalCullingMask));
                Assert.That(camera.transform.position,
                    Is.EqualTo(secondStance.FirstPersonEyePosition)
                        .Using(Vector3ComparerWithEqualsOperator.Instance));

                controller.Unbind();

                Assert.That(secondVisual.layer, Is.EqualTo(secondOriginalLayer));
                Assert.That(secondVisual.GetComponent<Renderer>().forceRenderingOff,
                    Is.False);
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

        [Test]
        public void MultipleCameraOwnersRestoreRendererVisibilityOutOfOrder()
        {
            var actor = new GameObject("Shared Camera Actor");
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var firstCameraObject = new GameObject("First Shared Camera");
            var secondCameraObject = new GameObject("Second Shared Camera");
            try
            {
                actor.AddComponent<CharacterController>();
                var stance = actor.AddComponent<ActorStancePresenter>();
                visual.transform.SetParent(actor.transform, false);
                Renderer renderer = visual.GetComponent<Renderer>();
                int originalLayer = visual.layer;
                firstCameraObject.AddComponent<Camera>();
                secondCameraObject.AddComponent<Camera>();
                GameplayCameraController first = firstCameraObject
                    .AddComponent<GameplayCameraController>();
                GameplayCameraController second = secondCameraObject
                    .AddComponent<GameplayCameraController>();
                first.Bind(actor.transform, new EmptyInputSource(), stance);
                second.Bind(actor.transform, new EmptyInputSource(), stance);
                first.SetView(GameplayCameraView.FirstPerson);
                second.SetView(GameplayCameraView.FirstPerson);

                first.Unbind();

                Assert.That(renderer.forceRenderingOff, Is.True);
                Assert.That(visual.layer, Is.EqualTo(originalLayer));

                second.Unbind();

                Assert.That(renderer.forceRenderingOff, Is.False);
                Assert.That(visual.layer, Is.EqualTo(originalLayer));
            }
            finally
            {
                Object.DestroyImmediate(firstCameraObject);
                Object.DestroyImmediate(secondCameraObject);
                Object.DestroyImmediate(actor);
            }
        }

        [Test]
        public void FirstPersonRefreshHidesDynamicallyMountedVisuals()
        {
            var actor = new GameObject("Dynamic Camera Actor");
            var cameraObject = new GameObject("Dynamic Camera");
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
                controller.SetView(GameplayCameraView.FirstPerson);
                GameObject mountedVisual = GameObject.CreatePrimitive(
                    PrimitiveType.Cube);
                mountedVisual.transform.SetParent(actor.transform, false);
                int originalLayer = mountedVisual.layer;

                controller.RefreshNow();

                Assert.That(
                    mountedVisual.GetComponent<Renderer>().forceRenderingOff,
                    Is.True);
                Assert.That(mountedVisual.layer, Is.EqualTo(originalLayer));

                controller.Unbind();

                Assert.That(
                    mountedVisual.GetComponent<Renderer>().forceRenderingOff,
                    Is.False);
                Assert.That(mountedVisual.layer, Is.EqualTo(originalLayer));
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(actor);
            }
        }

        [Test]
        public void InvalidRebindKeepsExistingCameraBinding()
        {
            var actor = new GameObject("Transactional Camera Actor");
            var cameraObject = new GameObject("Transactional Camera");
            try
            {
                actor.AddComponent<CharacterController>();
                var stance = actor.AddComponent<ActorStancePresenter>();
                cameraObject.AddComponent<Camera>();
                GameplayCameraController controller = cameraObject
                    .AddComponent<GameplayCameraController>();
                var input = new EmptyInputSource();
                controller.Bind(actor.transform, input, stance);

                Assert.Throws<ArgumentNullException>(() =>
                    controller.Bind(actor.transform, null, stance));

                Assert.That(controller.Target, Is.SameAs(actor.transform));
                Assert.That(controller.enabled, Is.True);
                Assert.That(controller.View,
                    Is.EqualTo(GameplayCameraView.ThirdPerson));
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
