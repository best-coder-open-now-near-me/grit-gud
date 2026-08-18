using System;
using System.Collections.Generic;
using GritGud.Presentation.Levels.Runtime;
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

        private const float DefaultPivotHeight = 1.3f;
        private const float DefaultEyeHeight = 1.62f;
        private const int RaycastBufferSize = 64;
        private const int VisibilityMaskWidth = 12;
        private const int VisibilityMaskHeight = 20;
        private const float SurfaceSeparation = 0.03f;
        private static readonly int PlayerCutoutEnabled =
            Shader.PropertyToID("_PlayerCutoutEnabled");
        private static readonly int PlayerCutout =
            Shader.PropertyToID("_GritGudPlayerCutout");
        private static readonly int PlayerCutoutLeftExtension =
            Shader.PropertyToID("_GritGudPlayerCutoutLeftExtension");
        private static readonly int PlayerCutoutVerticalRadius =
            Shader.PropertyToID("_GritGudPlayerCutoutVerticalRadius");
        private static readonly int PlayerCutoutVisibilityMask =
            Shader.PropertyToID("_GritGudPlayerCutoutVisibilityMask");
        private static readonly int PlayerCutoutVisibilityRect =
            Shader.PropertyToID("_GritGudPlayerCutoutVisibilityRect");

        private Camera gameplayCamera;
        private Transform target;
        private ActorStancePresenter stancePresenter;
        private readonly RaycastHit[] raycastBuffer =
            new RaycastHit[RaycastBufferSize];
        private readonly Dictionary<LevelEntityView, List<Renderer>>
            renderersByEntity =
                new Dictionary<LevelEntityView, List<Renderer>>();
        private HashSet<Renderer> activeOccluders = new HashSet<Renderer>();
        private HashSet<Renderer> nextOccluders = new HashSet<Renderer>();
        private MaterialPropertyBlock propertyBlock;
        private Texture2D visibilityMask;
        private byte[] visibilityMaskPixels;

        public bool IsBound => target != null;

        public bool PresentationEnabled { get; private set; }

        public Transform Target => target;

        public Vector4 CurrentShaderData { get; private set; }

        public float CurrentLeftExtension { get; private set; }

        public float CurrentVerticalRadius { get; private set; }

        public int ActiveOccluderCount => activeOccluders.Count;

        private void Awake()
        {
            propertyBlock = new MaterialPropertyBlock();
            visibilityMask = new Texture2D(
                VisibilityMaskWidth,
                VisibilityMaskHeight,
                TextureFormat.R8,
                mipChain: false,
                linear: true)
            {
                name = "Player POV Visibility Mask",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            visibilityMaskPixels = new byte[
                VisibilityMaskWidth * VisibilityMaskHeight];
        }

        public void Bind(
            Camera camera,
            Transform followTarget,
            ActorStancePresenter actorStancePresenter,
            IReadOnlyList<Renderer> playerCutoutRenderers)
        {
            gameplayCamera = camera != null
                ? camera
                : throw new ArgumentNullException(nameof(camera));
            target = followTarget != null
                ? followTarget
                : throw new ArgumentNullException(nameof(followTarget));
            stancePresenter = actorStancePresenter;
            RegisterCutoutRenderers(playerCutoutRenderers);
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
            ClearOccludingRenderers();
            renderersByEntity.Clear();
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
                ClearOccludingRenderers();
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
                ClearOccludingRenderers();
                ClearShaderData();
                return;
            }

            RefreshOccludingRenderers(
                gameplayCamera.transform.position,
                focus,
                viewport);

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
        }

        private void OnPreCull()
        {
            RefreshNow();
        }

        private void OnDisable()
        {
            ClearOccludingRenderers();
            ClearShaderData();
        }

        private void OnDestroy()
        {
            ClearOccludingRenderers();
            ClearShaderData();
            GameplayObjectLifecycle.Destroy(visibilityMask);
            visibilityMask = null;
            visibilityMaskPixels = null;
        }

        private void RegisterCutoutRenderers(
            IReadOnlyList<Renderer> playerCutoutRenderers)
        {
            ClearOccludingRenderers();
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

                SetCutoutEnabled(renderer, false);
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

        private void RefreshOccludingRenderers(
            Vector3 cameraPosition,
            Vector3 focus,
            Vector3 focusViewport)
        {
            nextOccluders.Clear();
            Array.Clear(
                visibilityMaskPixels,
                0,
                visibilityMaskPixels.Length);
            var visibilityRect = new Rect(
                focusViewport.x - HorizontalViewportRadius
                    - LeftViewportExtension,
                focusViewport.y - VerticalViewportRadius,
                (HorizontalViewportRadius * 2f) + LeftViewportExtension,
                VerticalViewportRadius * 2f);
            Vector3 playerEye = stancePresenter != null
                ? stancePresenter.FirstPersonEyePosition
                : target.position + (Vector3.up * DefaultEyeHeight);
            float maximumWallDistance =
                Vector3.Distance(cameraPosition, focus) + 0.5f;
            for (int row = 0; row < VisibilityMaskHeight; row++)
            {
                float viewportY = visibilityRect.yMin
                    + (((row + 0.5f) / VisibilityMaskHeight)
                        * visibilityRect.height);
                for (int column = 0; column < VisibilityMaskWidth; column++)
                {
                    float viewportX = visibilityRect.xMin
                        + (((column + 0.5f) / VisibilityMaskWidth)
                            * visibilityRect.width);
                    Ray ray = gameplayCamera.ViewportPointToRay(
                        new Vector3(viewportX, viewportY));
                    if (!TryFindVisibleCameraOccluder(
                        ray,
                        maximumWallDistance,
                        playerEye,
                        out LevelEntityView occludingEntity))
                    {
                        continue;
                    }

                    visibilityMaskPixels[
                        (row * VisibilityMaskWidth) + column] = byte.MaxValue;
                    AddOccludingEntity(occludingEntity);
                }
            }

            visibilityMask.LoadRawTextureData(visibilityMaskPixels);
            visibilityMask.Apply(updateMipmaps: false, makeNoLongerReadable: false);
            Shader.SetGlobalTexture(PlayerCutoutVisibilityMask, visibilityMask);
            Shader.SetGlobalVector(
                PlayerCutoutVisibilityRect,
                new Vector4(
                    visibilityRect.xMin,
                    visibilityRect.yMin,
                    visibilityRect.width,
                    visibilityRect.height));

            foreach (Renderer renderer in activeOccluders)
            {
                if (!nextOccluders.Contains(renderer))
                {
                    SetCutoutEnabled(renderer, false);
                }
            }

            foreach (Renderer renderer in nextOccluders)
            {
                if (!activeOccluders.Contains(renderer))
                {
                    SetCutoutEnabled(renderer, true);
                }
            }

            HashSet<Renderer> previous = activeOccluders;
            activeOccluders = nextOccluders;
            nextOccluders = previous;
        }

        private bool TryFindVisibleCameraOccluder(
            Ray cameraRay,
            float maximumWallDistance,
            Vector3 playerEye,
            out LevelEntityView occludingEntity)
        {
            occludingEntity = null;
            RaycastHit[] hits = GetRayHits(
                cameraRay,
                gameplayCamera.farClipPlane,
                out int hitCount);
            float nearestWallDistance = float.PositiveInfinity;
            for (int index = 0; index < hitCount; index++)
            {
                RaycastHit hit = hits[index];
                LevelEntityView entity = hit.collider != null
                    ? hit.collider.GetComponentInParent<LevelEntityView>()
                    : null;
                if (hit.distance >= nearestWallDistance
                    || hit.distance > maximumWallDistance
                    || entity == null
                    || !GameplayCameraOcclusionRules.UsesPlayerCutout(
                        entity.ArchetypeId)
                    || !renderersByEntity.ContainsKey(entity))
                {
                    continue;
                }

                nearestWallDistance = hit.distance;
                occludingEntity = entity;
            }

            if (occludingEntity == null)
            {
                return false;
            }

            float nearestRevealedDistance = float.PositiveInfinity;
            Collider revealedCollider = null;
            Vector3 revealedPoint = cameraRay.GetPoint(
                gameplayCamera.farClipPlane * 0.95f);
            for (int index = 0; index < hitCount; index++)
            {
                RaycastHit hit = hits[index];
                if (hit.distance <= nearestWallDistance + SurfaceSeparation
                    || hit.distance >= nearestRevealedDistance)
                {
                    continue;
                }

                LevelEntityView entity = hit.collider != null
                    ? hit.collider.GetComponentInParent<LevelEntityView>()
                    : null;
                if (entity == occludingEntity)
                {
                    continue;
                }

                nearestRevealedDistance = hit.distance;
                revealedCollider = hit.collider;
                revealedPoint = hit.point;
            }

            if (IsPlayerCollider(revealedCollider))
            {
                return true;
            }

            return HasPlayerLineOfSight(
                playerEye,
                revealedPoint,
                revealedCollider);
        }

        private RaycastHit[] GetRayHits(
            Ray ray,
            float distance,
            out int hitCount)
        {
            hitCount = Physics.RaycastNonAlloc(
                ray,
                raycastBuffer,
                distance,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            if (hitCount < raycastBuffer.Length)
            {
                return raycastBuffer;
            }

            RaycastHit[] overflowHits = Physics.RaycastAll(
                ray,
                distance,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            hitCount = overflowHits.Length;
            return overflowHits;
        }

        private bool HasPlayerLineOfSight(
            Vector3 playerEye,
            Vector3 revealedPoint,
            Collider revealedCollider)
        {
            Vector3 offset = revealedPoint - playerEye;
            float distance = offset.magnitude;
            if (distance <= SurfaceSeparation)
            {
                return true;
            }

            RaycastHit[] hits = GetRayHits(
                new Ray(playerEye, offset / distance),
                distance,
                out int hitCount);
            for (int index = 0; index < hitCount; index++)
            {
                RaycastHit hit = hits[index];
                if (IsPlayerCollider(hit.collider)
                    || (hit.collider == revealedCollider
                        && hit.distance >= distance - SurfaceSeparation))
                {
                    continue;
                }

                if (hit.distance < distance - SurfaceSeparation)
                {
                    return false;
                }
            }

            return true;
        }

        private bool IsPlayerCollider(Collider collider)
        {
            Transform source = collider != null ? collider.transform : null;
            return source != null
                && (source == target || source.IsChildOf(target));
        }

        private void AddOccludingEntity(LevelEntityView entity)
        {
            if (entity == null
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
                    nextOccluders.Add(renderer);
                }
            }
        }

        private void ClearOccludingRenderers()
        {
            foreach (Renderer renderer in activeOccluders)
            {
                SetCutoutEnabled(renderer, false);
            }

            activeOccluders.Clear();
            nextOccluders.Clear();
        }

        private void SetCutoutEnabled(Renderer renderer, bool cutoutEnabled)
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
                PlayerCutoutEnabled,
                cutoutEnabled ? 1f : 0f);
            renderer.SetPropertyBlock(propertyBlock);
            propertyBlock.Clear();
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
                PlayerCutoutVisibilityMask,
                Texture2D.blackTexture);
            Shader.SetGlobalVector(PlayerCutoutVisibilityRect, Vector4.zero);
        }
    }
}
