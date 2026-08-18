using System;
using System.Collections.Generic;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Presentation.Levels.Runtime;
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
        private const float FloorPatchRefreshSeconds = 0.2f;
        private const float FloorViewHeight = 0.12f;
        private const int RaycastBufferSize = 64;
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
        private static readonly int PlayerCutoutOvalEnabled =
            Shader.PropertyToID("_PlayerCutoutOvalEnabled");
        private static readonly Vector2[] FloorPatchOffsets =
        {
            new Vector2(0.55f, 0f),
            new Vector2(-0.55f, 0f),
            new Vector2(0f, 0.55f),
            new Vector2(0f, -0.55f),
            new Vector2(1.1f, 0f),
            new Vector2(-1.1f, 0f),
            new Vector2(0f, 1.1f),
            new Vector2(0f, -1.1f),
            new Vector2(0.78f, 0.78f),
            new Vector2(-0.78f, 0.78f),
            new Vector2(0.78f, -0.78f),
            new Vector2(-0.78f, -0.78f),
        };
        private static readonly Vector2[] BodyViewportOffsets =
        {
            Vector2.zero,
            new Vector2(0f, 0.08f),
            new Vector2(0f, 0.14f),
            new Vector2(0f, -0.08f),
            new Vector2(-0.025f, 0.05f),
            new Vector2(0.025f, 0.05f),
            new Vector2(-0.025f, -0.06f),
            new Vector2(0.025f, -0.06f),
        };

        private Camera gameplayCamera;
        private Camera silhouetteCamera;
        private RenderTexture silhouetteMask;
        private Transform target;
        private ActorStancePresenter stancePresenter;
        private UnityMovementRouteSegmentValidator walkabilityValidator;
        private readonly RaycastHit[] raycastBuffer =
            new RaycastHit[RaycastBufferSize];
        private readonly Dictionary<LevelEntityView, List<Renderer>>
            renderersByEntity =
                new Dictionary<LevelEntityView, List<Renderer>>();
        private HashSet<Renderer> activeOvalOccluders =
            new HashSet<Renderer>();
        private HashSet<Renderer> nextOvalOccluders =
            new HashSet<Renderer>();
        private readonly List<Vector3> walkableFloorTargets =
            new List<Vector3>();
        private MaterialPropertyBlock propertyBlock;
        private Vector3 lastFloorPatchOrigin =
            new Vector3(float.PositiveInfinity, 0f, 0f);
        private float nextFloorPatchRefreshTime;

        public bool IsBound => target != null;

        public bool PresentationEnabled { get; private set; }

        public Transform Target => target;

        public Vector4 CurrentShaderData { get; private set; }

        public float CurrentLeftExtension { get; private set; }

        public float CurrentVerticalRadius { get; private set; }

        public bool HasSilhouetteMask =>
            silhouetteCamera != null && silhouetteMask != null;

        private void Awake()
        {
            propertyBlock = new MaterialPropertyBlock();
        }

        public void Bind(
            Camera camera,
            Transform followTarget,
            ActorStancePresenter actorStancePresenter,
            IReadOnlyList<Renderer> playerCutoutRenderers)
        {
            ReleaseSilhouetteResources();
            gameplayCamera = camera != null
                ? camera
                : throw new ArgumentNullException(nameof(camera));
            target = followTarget != null
                ? followTarget
                : throw new ArgumentNullException(nameof(followTarget));
            stancePresenter = actorStancePresenter;
            ConfigureWalkability(followTarget);
            RegisterCutoutRenderers(playerCutoutRenderers);
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
            ConfigureWalkability(followTarget);
            enabled = PresentationEnabled;
            RefreshNow();
        }

        public void Unbind()
        {
            ClearOvalOccluders();
            renderersByEntity.Clear();
            walkableFloorTargets.Clear();
            walkabilityValidator = null;
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

            ClearOvalOccluders();
            ClearShaderData();
        }

        public void RefreshNow()
        {
            if (!PresentationEnabled || gameplayCamera == null || target == null)
            {
                ClearOvalOccluders();
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
                ClearOvalOccluders();
                ClearShaderData();
                return;
            }

            RefreshWalkableFloorTargets();
            RefreshOvalOccluders(viewport);
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
            ClearOvalOccluders();
            ClearShaderData();
        }

        private void OnDestroy()
        {
            ClearOvalOccluders();
            ClearShaderData();
            ReleaseSilhouetteResources();
        }

        private void ConfigureWalkability(Transform followTarget)
        {
            CharacterController controller = followTarget != null
                ? followTarget.GetComponent<CharacterController>()
                : null;
            walkabilityValidator = controller != null
                ? new UnityMovementRouteSegmentValidator(controller)
                : null;
            walkableFloorTargets.Clear();
            lastFloorPatchOrigin = new Vector3(
                float.PositiveInfinity,
                0f,
                0f);
            nextFloorPatchRefreshTime = 0f;
        }

        private void RegisterCutoutRenderers(
            IReadOnlyList<Renderer> playerCutoutRenderers)
        {
            ClearOvalOccluders();
            renderersByEntity.Clear();
            if (playerCutoutRenderers == null)
            {
                return;
            }

            for (int index = 0; index < playerCutoutRenderers.Count; index++)
            {
                Renderer renderer = playerCutoutRenderers[index];
                if (renderer == null)
                {
                    continue;
                }

                SetOvalEnabled(renderer, false);
                LevelEntityView entity =
                    renderer.GetComponentInParent<LevelEntityView>();
                if (entity == null)
                {
                    continue;
                }

                if (!renderersByEntity.TryGetValue(
                    entity,
                    out List<Renderer> entityRenderers))
                {
                    entityRenderers = new List<Renderer>();
                    renderersByEntity.Add(entity, entityRenderers);
                }

                entityRenderers.Add(renderer);
            }
        }

        private void RefreshWalkableFloorTargets()
        {
            Vector3 origin = target.position;
            if (Time.unscaledTime < nextFloorPatchRefreshTime
                && (origin - lastFloorPatchOrigin).sqrMagnitude < 0.0225f)
            {
                return;
            }

            walkableFloorTargets.Clear();
            walkableFloorTargets.Add(origin + (Vector3.up * FloorViewHeight));
            lastFloorPatchOrigin = origin;
            nextFloorPatchRefreshTime =
                Time.unscaledTime + FloorPatchRefreshSeconds;
            if (walkabilityValidator == null)
            {
                return;
            }

            var from = new GameplayPosition(origin.x, origin.y, origin.z);
            for (int index = 0; index < FloorPatchOffsets.Length; index++)
            {
                Vector2 offset = FloorPatchOffsets[index];
                var requested = new GameplayPosition(
                    origin.x + offset.x,
                    origin.y,
                    origin.z + offset.y);
                MovementRouteSegmentValidation validation =
                    walkabilityValidator.Validate(
                        string.Empty,
                        from,
                        requested);
                if (!validation.IsValid)
                {
                    continue;
                }

                GameplayPosition resolved = validation.ResolvedPosition;
                walkableFloorTargets.Add(new Vector3(
                    resolved.X,
                    resolved.Y + FloorViewHeight,
                    resolved.Z));
            }
        }

        private void RefreshOvalOccluders(Vector3 playerViewport)
        {
            nextOvalOccluders.Clear();
            for (int index = 0; index < BodyViewportOffsets.Length; index++)
            {
                Vector2 offset = BodyViewportOffsets[index];
                Vector3 bodyTarget = gameplayCamera.ViewportToWorldPoint(
                    new Vector3(
                        playerViewport.x + offset.x,
                        playerViewport.y + offset.y,
                        playerViewport.z));
                AddWallsBetweenCameraAnd(bodyTarget);
            }

            for (int index = 0; index < walkableFloorTargets.Count; index++)
            {
                AddWallsBetweenCameraAnd(walkableFloorTargets[index]);
            }

            foreach (Renderer renderer in activeOvalOccluders)
            {
                if (!nextOvalOccluders.Contains(renderer))
                {
                    SetOvalEnabled(renderer, false);
                }
            }

            foreach (Renderer renderer in nextOvalOccluders)
            {
                if (!activeOvalOccluders.Contains(renderer))
                {
                    SetOvalEnabled(renderer, true);
                }
            }

            HashSet<Renderer> previous = activeOvalOccluders;
            activeOvalOccluders = nextOvalOccluders;
            nextOvalOccluders = previous;
        }

        private void AddWallsBetweenCameraAnd(Vector3 worldTarget)
        {
            Vector3 cameraPosition = gameplayCamera.transform.position;
            Vector3 offset = worldTarget - cameraPosition;
            float distance = offset.magnitude;
            if (distance <= 0.001f)
            {
                return;
            }

            int hitCount = Physics.RaycastNonAlloc(
                cameraPosition,
                offset / distance,
                raycastBuffer,
                distance,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            RaycastHit[] hits = raycastBuffer;
            if (hitCount == raycastBuffer.Length)
            {
                hits = Physics.RaycastAll(
                    cameraPosition,
                    offset / distance,
                    distance,
                    Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore);
                hitCount = hits.Length;
            }

            for (int index = 0; index < hitCount; index++)
            {
                AddOvalOccludingEntity(hits[index].collider);
            }
        }

        private void AddOvalOccludingEntity(Collider collider)
        {
            LevelEntityView entity = collider != null
                ? collider.GetComponentInParent<LevelEntityView>()
                : null;
            if (entity == null
                || !GameplayCameraOcclusionRules.UsesPlayerCutout(
                    entity.ArchetypeId)
                || !renderersByEntity.TryGetValue(
                    entity,
                    out List<Renderer> entityRenderers))
            {
                return;
            }

            for (int index = 0; index < entityRenderers.Count; index++)
            {
                Renderer renderer = entityRenderers[index];
                if (renderer != null)
                {
                    nextOvalOccluders.Add(renderer);
                }
            }
        }

        private void ClearOvalOccluders()
        {
            foreach (Renderer renderer in activeOvalOccluders)
            {
                SetOvalEnabled(renderer, false);
            }

            activeOvalOccluders.Clear();
            nextOvalOccluders.Clear();
        }

        private void SetOvalEnabled(Renderer renderer, bool ovalEnabled)
        {
            if (renderer == null)
            {
                return;
            }

            if (propertyBlock == null)
            {
                propertyBlock = new MaterialPropertyBlock();
            }

            renderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetFloat(
                PlayerCutoutOvalEnabled,
                ovalEnabled ? 1f : 0f);
            renderer.SetPropertyBlock(propertyBlock);
            propertyBlock.Clear();
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
