using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace GritGud.Presentation.Gameplay
{
    [DefaultExecutionOrder(1000)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class GameplayPlayerCutoutPresenter : MonoBehaviour
    {
        public const float HorizontalViewportRadius = 0.12f;
        public const float VerticalViewportRadius = 0.3f;
        public const float LeftViewportExtension = 0.04f;
        public const string SilhouetteCameraName =
            "Gameplay Player Silhouette Camera";

        private const float DefaultPivotHeight = 1.3f;
        private const int MaximumMaskDimension = 320;
        private const int MinimumMaskDimension = 64;
        private static readonly int PlayerCutout =
            Shader.PropertyToID("_GritGudPlayerCutout");
        private static readonly int PlayerCutoutLeftExtension =
            Shader.PropertyToID("_GritGudPlayerCutoutLeftExtension");
        private static readonly int PlayerCutoutVerticalRadius =
            Shader.PropertyToID("_GritGudPlayerCutoutVerticalRadius");
        private static readonly int PlayerSilhouetteMask =
            Shader.PropertyToID("_GritGudPlayerSilhouetteMask");
        private static readonly int PlayerSilhouetteMaskTexelSize =
            Shader.PropertyToID("_GritGudPlayerSilhouetteMask_TexelSize");

        private Camera gameplayCamera;
        private Camera silhouetteCamera;
        private RenderTexture silhouetteMask;
        private Transform target;
        private ActorStancePresenter stancePresenter;

        public bool IsBound => target != null;

        public bool PresentationEnabled { get; private set; }

        public Transform Target => target;

        public Vector4 CurrentShaderData { get; private set; }

        public float CurrentLeftExtension { get; private set; }

        public float CurrentVerticalRadius { get; private set; }

        public bool HasSilhouetteMask =>
            silhouetteCamera != null && silhouetteMask != null;

        public void Bind(
            Camera camera,
            Transform followTarget,
            ActorStancePresenter actorStancePresenter = null)
        {
            ReleaseSilhouetteResources();
            gameplayCamera = camera != null
                ? camera
                : throw new ArgumentNullException(nameof(camera));
            target = followTarget != null
                ? followTarget
                : throw new ArgumentNullException(nameof(followTarget));
            stancePresenter = actorStancePresenter;
            CreateSilhouetteCamera();
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
            ReleaseSilhouetteResources();
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

            EnsureSilhouetteMask();
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
            SetSilhouetteMaskGlobal();
        }

        private void LateUpdate()
        {
            RefreshNow();
            RenderSilhouetteMask();
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
            ReleaseSilhouetteResources();
        }

        private void CreateSilhouetteCamera()
        {
            int playerLayer = LayerMask.NameToLayer(
                GameplayCameraController.LocalPlayerLayerName);
            if (playerLayer < 0)
            {
                throw new InvalidOperationException(
                    $"Player cutout requires the " +
                    $"'{GameplayCameraController.LocalPlayerLayerName}' " +
                    "project layer.");
            }

            var cameraObject = new GameObject(SilhouetteCameraName)
            {
                hideFlags = HideFlags.HideAndDontSave,
            };
            cameraObject.transform.SetParent(
                gameplayCamera.transform,
                worldPositionStays: false);
            silhouetteCamera = cameraObject.AddComponent<Camera>();
            silhouetteCamera.CopyFrom(gameplayCamera);
            silhouetteCamera.enabled = false;
            silhouetteCamera.cullingMask = 1 << playerLayer;
            silhouetteCamera.clearFlags = CameraClearFlags.SolidColor;
            silhouetteCamera.backgroundColor = Color.clear;
            silhouetteCamera.allowHDR = false;
            silhouetteCamera.allowMSAA = false;
            silhouetteCamera.depthTextureMode = DepthTextureMode.None;
            silhouetteCamera.useOcclusionCulling = false;
            silhouetteCamera.rect = new Rect(0f, 0f, 1f, 1f);

            UniversalAdditionalCameraData cameraData =
                silhouetteCamera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = false;
            cameraData.antialiasing = AntialiasingMode.None;
            EnsureSilhouetteMask();
        }

        private void EnsureSilhouetteMask()
        {
            if (gameplayCamera == null || silhouetteCamera == null)
            {
                return;
            }

            Vector2Int size = CalculateMaskSize(gameplayCamera);
            if (silhouetteMask != null
                && silhouetteMask.width == size.x
                && silhouetteMask.height == size.y)
            {
                return;
            }

            ReleaseSilhouetteMask();
            silhouetteMask = new RenderTexture(
                size.x,
                size.y,
                16,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Linear)
            {
                name = "Gameplay Player Silhouette Mask",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                antiAliasing = 1,
                useMipMap = false,
                autoGenerateMips = false,
                hideFlags = HideFlags.HideAndDontSave,
            };
            silhouetteMask.Create();
            silhouetteCamera.targetTexture = silhouetteMask;

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = silhouetteMask;
            GL.Clear(true, true, Color.clear);
            RenderTexture.active = previous;
            SetSilhouetteMaskGlobal();
        }

        private void RenderSilhouetteMask()
        {
            if (!PresentationEnabled
                || gameplayCamera == null
                || silhouetteCamera == null
                || silhouetteMask == null)
            {
                return;
            }

            Transform gameplayTransform = gameplayCamera.transform;
            silhouetteCamera.transform.SetPositionAndRotation(
                gameplayTransform.position,
                gameplayTransform.rotation);
            silhouetteCamera.worldToCameraMatrix =
                gameplayCamera.worldToCameraMatrix;
            silhouetteCamera.projectionMatrix = gameplayCamera.projectionMatrix;
            silhouetteCamera.nearClipPlane = gameplayCamera.nearClipPlane;
            silhouetteCamera.farClipPlane = gameplayCamera.farClipPlane;
            silhouetteCamera.Render();
            SetSilhouetteMaskGlobal();
        }

        private static Vector2Int CalculateMaskSize(Camera camera)
        {
            int sourceWidth = Mathf.Max(1, camera.pixelWidth);
            int sourceHeight = Mathf.Max(1, camera.pixelHeight);
            float scale = Mathf.Min(
                1f,
                MaximumMaskDimension
                    / (float)Mathf.Max(sourceWidth, sourceHeight));
            return new Vector2Int(
                Mathf.Max(
                    MinimumMaskDimension,
                    Mathf.RoundToInt(sourceWidth * scale)),
                Mathf.Max(
                    MinimumMaskDimension,
                    Mathf.RoundToInt(sourceHeight * scale)));
        }

        private void ReleaseSilhouetteResources()
        {
            ReleaseSilhouetteMask();
            if (silhouetteCamera != null)
            {
                GameObject cameraObject = silhouetteCamera.gameObject;
                silhouetteCamera = null;
                cameraObject.transform.SetParent(null);
                GameplayObjectLifecycle.Destroy(cameraObject);
            }
        }

        private void ReleaseSilhouetteMask()
        {
            if (silhouetteCamera != null)
            {
                silhouetteCamera.targetTexture = null;
            }

            GameplayObjectLifecycle.Destroy(silhouetteMask);
            silhouetteMask = null;
        }

        private void SetSilhouetteMaskGlobal()
        {
            Shader.SetGlobalTexture(PlayerSilhouetteMask, silhouetteMask);
            Shader.SetGlobalVector(
                PlayerSilhouetteMaskTexelSize,
                silhouetteMask != null
                    ? new Vector4(
                        1f / silhouetteMask.width,
                        1f / silhouetteMask.height,
                        silhouetteMask.width,
                        silhouetteMask.height)
                    : Vector4.zero);
        }

        private void ClearShaderData()
        {
            CurrentShaderData = Vector4.zero;
            CurrentLeftExtension = 0f;
            CurrentVerticalRadius = 0f;
            Shader.SetGlobalVector(PlayerCutout, Vector4.zero);
            Shader.SetGlobalFloat(PlayerCutoutLeftExtension, 0f);
            Shader.SetGlobalFloat(PlayerCutoutVerticalRadius, 0f);
            Shader.SetGlobalTexture(
                PlayerSilhouetteMask,
                Texture2D.blackTexture);
            Shader.SetGlobalVector(
                PlayerSilhouetteMaskTexelSize,
                Vector4.zero);
        }
    }
}
