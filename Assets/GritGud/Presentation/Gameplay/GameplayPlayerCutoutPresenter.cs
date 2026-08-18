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
        private const int RaycastBufferSize = 64;
        private static readonly int PlayerCutoutEnabled =
            Shader.PropertyToID("_PlayerCutoutEnabled");
        private static readonly int PlayerCutout =
            Shader.PropertyToID("_GritGudPlayerCutout");
        private static readonly int PlayerCutoutLeftExtension =
            Shader.PropertyToID("_GritGudPlayerCutoutLeftExtension");
        private static readonly int PlayerCutoutVerticalRadius =
            Shader.PropertyToID("_GritGudPlayerCutoutVerticalRadius");

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
        private readonly MaterialPropertyBlock propertyBlock =
            new MaterialPropertyBlock();

        public bool IsBound => target != null;

        public bool PresentationEnabled { get; private set; }

        public Transform Target => target;

        public Vector4 CurrentShaderData { get; private set; }

        public float CurrentLeftExtension { get; private set; }

        public float CurrentVerticalRadius { get; private set; }

        public int ActiveOccluderCount => activeOccluders.Count;

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
            RefreshOccludingRenderers(
                gameplayCamera.transform.position,
                focus);
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
            Vector3 focus)
        {
            nextOccluders.Clear();
            Vector3 offset = focus - cameraPosition;
            float distance = offset.magnitude;
            if (distance > 0.001f)
            {
                Vector3 direction = offset / distance;
                int hitCount = Physics.RaycastNonAlloc(
                    cameraPosition,
                    direction,
                    raycastBuffer,
                    distance,
                    Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore);
                RaycastHit[] hits = raycastBuffer;
                if (hitCount == raycastBuffer.Length)
                {
                    hits = Physics.RaycastAll(
                        cameraPosition,
                        direction,
                        distance,
                        Physics.DefaultRaycastLayers,
                        QueryTriggerInteraction.Ignore);
                    hitCount = hits.Length;
                }

                for (int index = 0; index < hitCount; index++)
                {
                    AddOccludingEntity(hits[index].collider);
                }
            }

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

        private void AddOccludingEntity(Collider collider)
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
        }
    }
}
