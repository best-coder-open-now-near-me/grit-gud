using UnityEngine;

namespace GritGud.Presentation.CharacterEditing
{
    public sealed class CharacterPreviewCameraController
    {
        private const float OrbitSensitivity = 0.28f;
        private const float ZoomFractionPerStep = 0.14f;
        private const float MinimumDistance = 0.8f;
        private const float MaximumDistance = 8f;
        private const float MinimumPitch = -25f;
        private const float MaximumPitch = 55f;

        private readonly Camera camera;
        private Vector3 pivot = new Vector3(0f, 1.05f, 0f);
        private float framedDistance = 3.2f;
        private float distance = 3.2f;
        private float yaw;
        private float pitch = 4f;

        public CharacterPreviewCameraController(Camera camera)
        {
            this.camera = camera != null
                ? camera
                : throw new System.ArgumentNullException(nameof(camera));
            ApplyTransform();
        }

        public float Distance => distance;

        public float Yaw => yaw;

        public float Pitch => pitch;

        public void Orbit(Vector2 pointerDelta)
        {
            yaw = Mathf.Repeat(yaw + (pointerDelta.x * OrbitSensitivity), 360f);
            pitch = Mathf.Clamp(
                pitch - (pointerDelta.y * OrbitSensitivity),
                MinimumPitch,
                MaximumPitch);
            ApplyTransform();
        }

        public void Zoom(float steps)
        {
            if (Mathf.Abs(steps) < 0.01f)
                return;
            distance = Mathf.Clamp(
                distance * Mathf.Pow(1f - ZoomFractionPerStep, steps),
                MinimumDistance,
                MaximumDistance);
            ApplyTransform();
        }

        public void Frame(Renderer[] renderers)
        {
            if (renderers == null || renderers.Length == 0)
            {
                ResetView();
                return;
            }

            Bounds bounds = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index++)
                bounds.Encapsulate(renderers[index].bounds);

            pivot = bounds.center;
            float verticalFov = Mathf.Max(1f, camera.fieldOfView) * Mathf.Deg2Rad;
            float fitHeight = bounds.extents.y / Mathf.Tan(verticalFov * 0.5f);
            float horizontalFov = 2f * Mathf.Atan(
                Mathf.Tan(verticalFov * 0.5f) * Mathf.Max(0.1f, camera.aspect));
            float fitWidth = bounds.extents.x / Mathf.Tan(horizontalFov * 0.5f);
            framedDistance = Mathf.Clamp(
                Mathf.Max(fitHeight, fitWidth, bounds.extents.z) * 1.2f,
                MinimumDistance,
                MaximumDistance);
            distance = framedDistance;
            ApplyTransform();
        }

        public void ResetView()
        {
            yaw = 0f;
            pitch = 4f;
            distance = framedDistance;
            ApplyTransform();
        }

        private void ApplyTransform()
        {
            Quaternion orbit = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 position = pivot + (orbit * new Vector3(0f, 0f, distance));
            camera.transform.SetPositionAndRotation(
                position,
                Quaternion.LookRotation(pivot - position, Vector3.up));
        }
    }
}
