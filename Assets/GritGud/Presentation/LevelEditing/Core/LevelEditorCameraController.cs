using System;
using GritGud.Domain.Levels;
using UnityEngine;

namespace GritGud.Presentation.LevelEditing.Core
{
    [Serializable]
    public sealed class LevelEditorCameraState
    {
        public Vector3 target;
        public float yaw;
        public float pitch;
        public float distance;
    }

    public sealed class LevelEditorCameraController
    {
        private const float KeyboardOrbitSpeed = 70f;
        private const float PointerOrbitSensitivity = 0.25f;
        private const float MinimumPitch = 20f;
        private const float MaximumPitch = 80f;
        private const float MinimumDistance = 4f;
        private const float MaximumDistance = 45f;
        private const float ZoomFractionPerStep = 0.15f;
        private const float StandardScrollDelta = 120f;

        private readonly Camera camera;
        private Vector3 target = new Vector3(0f, 0f, 2.5f);
        private float yaw;
        private float pitch = 55f;
        private float distance = 15f;

        public LevelEditorCameraController(Camera camera)
        {
            this.camera = camera != null
                ? camera
                : throw new ArgumentNullException(nameof(camera));
            ApplyTransform();
        }

        public Camera Camera => camera;

        public LevelEditorCameraState CaptureState()
        {
            return new LevelEditorCameraState
            {
                target = target,
                yaw = yaw,
                pitch = pitch,
                distance = distance,
            };
        }

        public void RestoreState(LevelEditorCameraState state)
        {
            if (state == null
                || !IsFinite(state.target)
                || !IsFinite(state.yaw)
                || !IsFinite(state.pitch)
                || !IsFinite(state.distance))
            {
                return;
            }

            target = state.target;
            yaw = state.yaw;
            pitch = Mathf.Clamp(state.pitch, MinimumPitch, MaximumPitch);
            distance = Mathf.Clamp(state.distance, MinimumDistance, MaximumDistance);
            ApplyTransform();
        }

        public void Frame(LevelBoundsData bounds)
        {
            Frame(new Bounds(
                new Vector3(bounds.center.x, bounds.center.y, bounds.center.z),
                new Vector3(bounds.size.x, bounds.size.y, bounds.size.z)));
        }

        public void Frame(Bounds bounds)
        {
            target = bounds.center;
            float halfFieldOfView = Mathf.Max(1f, camera.fieldOfView * 0.5f) * Mathf.Deg2Rad;
            float framingDistance = bounds.extents.magnitude / Mathf.Sin(halfFieldOfView) * 1.15f;
            distance = Mathf.Clamp(framingDistance, MinimumDistance, MaximumDistance);
            ApplyTransform();
        }

        public void Tick(LevelEditorInputState input)
        {
            Vector3 forward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            Vector3 right = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
            float panSpeed = Mathf.Max(4f, distance * 0.6f)
                * (input.FastCameraMovement ? 3f : 1f);
            target += (forward * input.MoveForward + right * input.MoveRight)
                * panSpeed
                * Time.unscaledDeltaTime;
            yaw += input.CameraRotation * KeyboardOrbitSpeed * Time.unscaledDeltaTime;

            if ((input.MiddleHeld || input.SecondaryHeld) && !input.PointerBlocked)
            {
                yaw += input.PointerDelta.x * PointerOrbitSensitivity;
                pitch = Mathf.Clamp(
                    pitch - input.PointerDelta.y * PointerOrbitSensitivity,
                    MinimumPitch,
                    MaximumPitch);
            }

            if (!input.PointerBlocked && Mathf.Abs(input.ZoomDelta) > 0.01f)
            {
                float zoomSteps = Mathf.Sign(input.ZoomDelta)
                    * Mathf.Max(1f, Mathf.Abs(input.ZoomDelta) / StandardScrollDelta);
                distance = Mathf.Clamp(
                    distance * Mathf.Pow(1f - ZoomFractionPerStep, zoomSteps),
                    MinimumDistance,
                    MaximumDistance);
            }

            ApplyTransform();
        }

        private void ApplyTransform()
        {
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            camera.transform.SetPositionAndRotation(
                target - rotation * Vector3.forward * distance,
                rotation);
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
