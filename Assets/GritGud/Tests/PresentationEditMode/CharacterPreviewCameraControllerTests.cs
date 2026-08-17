using GritGud.Presentation.CharacterEditing;
using NUnit.Framework;
using UnityEngine;

namespace GritGud.Tests.PresentationEditMode
{
    public sealed class CharacterPreviewCameraControllerTests
    {
        [Test]
        public void DragOrbitsPreviewCameraInYawAndPitch()
        {
            var cameraObject = new GameObject("Character Preview Camera Test");
            try
            {
                Camera camera = cameraObject.AddComponent<Camera>();
                var controller = new CharacterPreviewCameraController(camera);

                controller.Orbit(new Vector2(100f, -50f));

                Assert.That(controller.Yaw, Is.EqualTo(28f).Within(0.01f));
                Assert.That(controller.Pitch, Is.EqualTo(18f).Within(0.01f));
                Assert.That(camera.transform.forward.y, Is.LessThan(0f));
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
            }
        }

        [Test]
        public void WheelZoomMovesCloserAndClampsAtUsefulLimit()
        {
            var cameraObject = new GameObject("Character Preview Zoom Test");
            try
            {
                Camera camera = cameraObject.AddComponent<Camera>();
                var controller = new CharacterPreviewCameraController(camera);
                float initialDistance = controller.Distance;

                controller.Zoom(1f);
                Assert.That(controller.Distance, Is.LessThan(initialDistance));

                controller.Zoom(100f);
                Assert.That(controller.Distance, Is.EqualTo(0.8f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
            }
        }

        [Test]
        public void ResetRestoresFramedDistanceAndFrontView()
        {
            var cameraObject = new GameObject("Character Preview Reset Test");
            var character = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            try
            {
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.aspect = 1.5f;
                var controller = new CharacterPreviewCameraController(camera);
                controller.Frame(character.GetComponentsInChildren<Renderer>());
                float framedDistance = controller.Distance;
                controller.Orbit(new Vector2(80f, 30f));
                controller.Zoom(2f);

                controller.ResetView();

                Assert.That(controller.Yaw, Is.Zero.Within(0.001f));
                Assert.That(controller.Pitch, Is.EqualTo(4f).Within(0.001f));
                Assert.That(controller.Distance, Is.EqualTo(framedDistance).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(character);
                Object.DestroyImmediate(cameraObject);
            }
        }
    }
}
