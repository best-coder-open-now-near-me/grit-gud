using System;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class GameplayPlayerCutoutPresenter : MonoBehaviour
    {
        public const float HorizontalViewportRadius = 0.12f;
        public const float VerticalViewportRadius = 0.3f;
        public const float LeftViewportExtension = 0.04f;
        public const float CameraCorridorHalfWidth = 0.04f;
        public const float TargetCorridorHalfWidth = 0.42f;

        private const float DefaultPivotHeight = 1.3f;
        private static readonly int PlayerCutout =
            Shader.PropertyToID("_GritGudPlayerCutout");
        private static readonly int PlayerCutoutLeftExtension =
            Shader.PropertyToID("_GritGudPlayerCutoutLeftExtension");
        private static readonly int PlayerCutoutVerticalRadius =
            Shader.PropertyToID("_GritGudPlayerCutoutVerticalRadius");
        private static readonly int PlayerCutoutRayStart =
            Shader.PropertyToID("_GritGudPlayerCutoutRayStart");
        private static readonly int PlayerCutoutRayEnd =
            Shader.PropertyToID("_GritGudPlayerCutoutRayEnd");
        private static readonly int PlayerCutoutCameraRight =
            Shader.PropertyToID("_GritGudPlayerCutoutCameraRight");
        private static readonly int PlayerCutoutCorridorWidths =
            Shader.PropertyToID("_GritGudPlayerCutoutCorridorWidths");

        private Camera gameplayCamera;
        private Transform target;
        private ActorStancePresenter stancePresenter;

        public bool IsBound => target != null;

        public bool PresentationEnabled { get; private set; }

        public Transform Target => target;

        public Vector4 CurrentShaderData { get; private set; }

        public float CurrentLeftExtension { get; private set; }

        public float CurrentVerticalRadius { get; private set; }

        public void Bind(
            Camera camera,
            Transform followTarget,
            ActorStancePresenter actorStancePresenter = null)
        {
            gameplayCamera = camera != null
                ? camera
                : throw new ArgumentNullException(nameof(camera));
            target = followTarget != null
                ? followTarget
                : throw new ArgumentNullException(nameof(followTarget));
            stancePresenter = actorStancePresenter;
            PresentationEnabled = true;
            enabled = true;
            RefreshNow();
        }

        public void SetTarget(
            Transform followTarget,
            ActorStancePresenter actorStancePresenter = null)
        {
            if (gameplayCamera == null)
            {
                throw new InvalidOperationException(
                    "Bind the player cutout before changing its target.");
            }

            target = followTarget != null
                ? followTarget
                : throw new ArgumentNullException(nameof(followTarget));
            stancePresenter = actorStancePresenter;
            enabled = PresentationEnabled;
            RefreshNow();
        }

        public void Unbind()
        {
            target = null;
            stancePresenter = null;
            gameplayCamera = null;
            PresentationEnabled = false;
            enabled = false;
            ClearShaderData();
        }

        public void SetPresentationEnabled(bool presentationEnabled)
        {
            PresentationEnabled = presentationEnabled;
            enabled = presentationEnabled && IsBound;
            if (enabled)
            {
                RefreshNow();
                return;
            }

            ClearShaderData();
        }

        public void RefreshNow()
        {
            if (!PresentationEnabled || gameplayCamera == null || target == null)
            {
                ClearShaderData();
                return;
            }

            float pivotHeight = stancePresenter != null
                ? stancePresenter.CameraPivotHeight
                : DefaultPivotHeight;
            Vector3 focus = target.position + (Vector3.up * pivotHeight);
            Vector3 viewport = gameplayCamera.WorldToViewportPoint(focus);
            if (viewport.z <= gameplayCamera.nearClipPlane)
            {
                ClearShaderData();
                return;
            }

            CurrentShaderData = new Vector4(
                viewport.x,
                viewport.y,
                HorizontalViewportRadius,
                viewport.z);
            CurrentLeftExtension = LeftViewportExtension;
            CurrentVerticalRadius = VerticalViewportRadius;
            Shader.SetGlobalVector(PlayerCutout, CurrentShaderData);
            Shader.SetGlobalFloat(
                PlayerCutoutLeftExtension,
                CurrentLeftExtension);
            Shader.SetGlobalFloat(
                PlayerCutoutVerticalRadius,
                CurrentVerticalRadius);
            Shader.SetGlobalVector(
                PlayerCutoutRayStart,
                gameplayCamera.transform.position);
            Shader.SetGlobalVector(PlayerCutoutRayEnd, focus);
            Shader.SetGlobalVector(
                PlayerCutoutCameraRight,
                gameplayCamera.transform.right);
            Shader.SetGlobalVector(
                PlayerCutoutCorridorWidths,
                new Vector4(
                    CameraCorridorHalfWidth,
                    TargetCorridorHalfWidth));
        }

        private void OnPreCull()
        {
            RefreshNow();
        }

        private void OnDisable()
        {
            ClearShaderData();
        }

        private void OnDestroy()
        {
            ClearShaderData();
        }

        private void ClearShaderData()
        {
            CurrentShaderData = Vector4.zero;
            CurrentLeftExtension = 0f;
            CurrentVerticalRadius = 0f;
            Shader.SetGlobalVector(PlayerCutout, Vector4.zero);
            Shader.SetGlobalFloat(PlayerCutoutLeftExtension, 0f);
            Shader.SetGlobalFloat(PlayerCutoutVerticalRadius, 0f);
            Shader.SetGlobalVector(PlayerCutoutRayStart, Vector4.zero);
            Shader.SetGlobalVector(PlayerCutoutRayEnd, Vector4.zero);
            Shader.SetGlobalVector(PlayerCutoutCameraRight, Vector4.zero);
            Shader.SetGlobalVector(PlayerCutoutCorridorWidths, Vector4.zero);
        }
    }
}
