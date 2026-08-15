using System;
using GritGud.Domain.Levels;
using UnityEngine;

namespace GritGud.Presentation.LevelEditing.Core
{
    public enum LevelEditorCameraView
    {
        Perspective,
        Top,
        Front,
        Right,
    }

    [Serializable]
    public sealed class LevelEditorCameraState
    {
        public Vector3 target;
        public float yaw;
        public float pitch;
        public float distance;
        public LevelEditorCameraView view;
        public float orthographicSize;
        public float perspectiveYaw;
        public float perspectivePitch;
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
        private float perspectiveYaw;
        private float perspectivePitch = 55f;
        private LevelEditorCameraView view;

        public LevelEditorCameraController(Camera camera)
        {
            this.camera = camera != null
                ? camera
                : throw new ArgumentNullException(nameof(camera));
            ApplyTransform();
        }

        public Camera Camera => camera;

        public LevelEditorCameraView View => view;

        public LevelEditorCameraState CaptureState()
        {
            return new LevelEditorCameraState
            {
                target = target,
                yaw = yaw,
                pitch = pitch,
                distance = distance,
                view = view,
                orthographicSize = camera.orthographicSize,
                perspectiveYaw = view == LevelEditorCameraView.Perspective
                    ? yaw
                    : perspectiveYaw,
                perspectivePitch = view == LevelEditorCameraView.Perspective
                    ? pitch
                    : perspectivePitch,
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
            view = Enum.IsDefined(typeof(LevelEditorCameraView), state.view)
                ? state.view
                : LevelEditorCameraView.Perspective;
            yaw = state.yaw;
            pitch = view == LevelEditorCameraView.Perspective
                ? Mathf.Clamp(state.pitch, MinimumPitch, MaximumPitch)
                : state.pitch;
            distance = Mathf.Clamp(state.distance, MinimumDistance, MaximumDistance);
            bool hasPerspectiveMemory = IsFinite(state.perspectivePitch)
                && state.perspectivePitch >= MinimumPitch;
            perspectiveYaw = hasPerspectiveMemory && IsFinite(state.perspectiveYaw)
                ? state.perspectiveYaw
                : state.yaw;
            perspectivePitch = hasPerspectiveMemory
                ? Mathf.Clamp(state.perspectivePitch, MinimumPitch, MaximumPitch)
                : Mathf.Clamp(state.pitch, MinimumPitch, MaximumPitch);
            camera.orthographicSize = IsFinite(state.orthographicSize)
                && state.orthographicSize > 0f
                ? state.orthographicSize
                : Mathf.Max(2f, distance * 0.5f);
            ApplyViewAngles();
            ApplyTransform();
        }

        public void SetView(LevelEditorCameraView requestedView)
        {
            if (!Enum.IsDefined(typeof(LevelEditorCameraView), requestedView))
                throw new ArgumentOutOfRangeException(nameof(requestedView));
            if (view == LevelEditorCameraView.Perspective)
            {
                perspectiveYaw = yaw;
                perspectivePitch = pitch;
            }

            view = requestedView;
            if (view == LevelEditorCameraView.Perspective)
            {
                yaw = perspectiveYaw;
                pitch = Mathf.Clamp(perspectivePitch, MinimumPitch, MaximumPitch);
            }
            else
            {
                camera.orthographicSize = Mathf.Max(2f, distance * 0.5f);
                ApplyViewAngles();
            }
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
            if (view != LevelEditorCameraView.Perspective)
                camera.orthographicSize = Mathf.Max(2f, bounds.extents.magnitude * 1.15f);
            ApplyTransform();
        }

        public void Tick(LevelEditorInputState input)
        {
            Vector3 forward = view == LevelEditorCameraView.Perspective
                ? Quaternion.Euler(0f, yaw, 0f) * Vector3.forward
                : camera.transform.up;
            Vector3 right = view == LevelEditorCameraView.Perspective
                ? Quaternion.Euler(0f, yaw, 0f) * Vector3.right
                : camera.transform.right;
            float panSpeed = Mathf.Max(4f, distance * 0.6f)
                * (input.FastCameraMovement ? 3f : 1f);
            target += (forward * input.MoveForward + right * input.MoveRight)
                * panSpeed
                * Time.unscaledDeltaTime;
            if (view == LevelEditorCameraView.Perspective)
                yaw += input.CameraRotation * KeyboardOrbitSpeed * Time.unscaledDeltaTime;

            if ((input.MiddleHeld || input.SecondaryHeld) && !input.PointerBlocked)
            {
                if (view != LevelEditorCameraView.Perspective)
                    SetView(LevelEditorCameraView.Perspective);
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
                if (view == LevelEditorCameraView.Perspective)
                {
                    distance = Mathf.Clamp(
                        distance * Mathf.Pow(1f - ZoomFractionPerStep, zoomSteps),
                        MinimumDistance,
                        MaximumDistance);
                }
                else
                {
                    camera.orthographicSize = Mathf.Clamp(
                        camera.orthographicSize
                            * Mathf.Pow(1f - ZoomFractionPerStep, zoomSteps),
                        1f,
                        MaximumDistance);
                }
            }

            ApplyTransform();
        }

        private void ApplyTransform()
        {
            camera.orthographic = view != LevelEditorCameraView.Perspective;
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            camera.transform.SetPositionAndRotation(
                target - rotation * Vector3.forward * distance,
                rotation);
        }

        private void ApplyViewAngles()
        {
            switch (view)
            {
                case LevelEditorCameraView.Top:
                    yaw = 0f;
                    pitch = 90f;
                    break;
                case LevelEditorCameraView.Front:
                    yaw = 0f;
                    pitch = 0f;
                    break;
                case LevelEditorCameraView.Right:
                    yaw = 90f;
                    pitch = 0f;
                    break;
            }
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
