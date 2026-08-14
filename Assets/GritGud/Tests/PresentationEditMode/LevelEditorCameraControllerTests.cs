using GritGud.Presentation.LevelEditing.Core;
using NUnit.Framework;
using UnityEngine;

namespace GritGud.Presentation.Tests
{
    public sealed class LevelEditorCameraControllerTests
    {
        private GameObject cameraObject;
        private Camera camera;
        private LevelEditorCameraController controller;

        [SetUp]
        public void SetUp()
        {
            cameraObject = new GameObject("Level Editor Camera Test");
            camera = cameraObject.AddComponent<Camera>();
            controller = new LevelEditorCameraController(camera);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(cameraObject);
        }

        [Test]
        public void RightDragOrbitsCameraInYawAndPitch()
        {
            Quaternion before = camera.transform.rotation;

            controller.Tick(new LevelEditorInputState
            {
                SecondaryHeld = true,
                PointerDelta = new Vector2(40f, 20f),
            });

            Assert.That(Quaternion.Angle(before, camera.transform.rotation), Is.GreaterThan(1f));
            Assert.That(camera.transform.eulerAngles.x, Is.EqualTo(50f).Within(0.01f));
            Assert.That(camera.transform.eulerAngles.y, Is.EqualTo(10f).Within(0.01f));
        }

        [Test]
        public void ScrollZoomUsesAUsefulStepForSmallBrowserDeltas()
        {
            Vector3 target = camera.transform.position + camera.transform.forward * 15f;

            controller.Tick(new LevelEditorInputState { ZoomDelta = 1f });

            float distance = Vector3.Distance(camera.transform.position, target);
            Assert.That(distance, Is.EqualTo(12.75f).Within(0.01f));
        }

        [Test]
        public void CameraGesturesAreIgnoredOverInterface()
        {
            Vector3 positionBefore = camera.transform.position;
            Quaternion rotationBefore = camera.transform.rotation;

            controller.Tick(new LevelEditorInputState
            {
                PointerBlocked = true,
                SecondaryHeld = true,
                PointerDelta = new Vector2(40f, 20f),
                ZoomDelta = 120f,
            });

            Assert.That(camera.transform.position, Is.EqualTo(positionBefore));
            Assert.That(camera.transform.rotation, Is.EqualTo(rotationBefore));
        }

        [Test]
        public void FrameCentersCameraOnRequestedBounds()
        {
            var bounds = new Bounds(new Vector3(12f, 3f, -8f), new Vector3(4f, 2f, 6f));

            controller.Frame(bounds);

            Ray cameraRay = new Ray(camera.transform.position, camera.transform.forward);
            Vector3 closestPoint = cameraRay.GetPoint(
                Vector3.Dot(bounds.center - cameraRay.origin, cameraRay.direction));
            Assert.That(Vector3.Distance(closestPoint, bounds.center), Is.LessThan(0.001f));
            Assert.That(Vector3.Distance(camera.transform.position, bounds.center),
                Is.GreaterThan(bounds.extents.magnitude));
        }

        [Test]
        public void CapturedCameraStateRestoresTheSameView()
        {
            controller.Tick(new LevelEditorInputState
            {
                SecondaryHeld = true,
                PointerDelta = new Vector2(80f, -24f),
                ZoomDelta = 120f,
            });
            LevelEditorCameraState state = controller.CaptureState();
            Vector3 expectedPosition = camera.transform.position;
            Quaternion expectedRotation = camera.transform.rotation;

            controller.Frame(new Bounds(new Vector3(20f, 0f, 10f), Vector3.one));
            controller.RestoreState(state);

            Assert.That(camera.transform.position, Is.EqualTo(expectedPosition));
            Assert.That(camera.transform.rotation, Is.EqualTo(expectedRotation));
        }
    }
}
