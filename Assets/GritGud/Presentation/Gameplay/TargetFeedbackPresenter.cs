using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace GritGud.Presentation.Gameplay
{
    internal sealed class TargetFeedbackPresenter : IDisposable
    {
        private const string GroundHaloShaderName = "GritGud/EmissiveSurface";
        private const string TargetOutlineShaderName = "GritGud/RuntimeOutline";
        private const int HaloSegments = 48;
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColor =
            Shader.PropertyToID("_EmissionColor");
        private static readonly int EmissionIntensity =
            Shader.PropertyToID("_EmissionIntensity");
        private static readonly int OutlineColor =
            Shader.PropertyToID("_OutlineColor");
        private static readonly int OutlineWidth =
            Shader.PropertyToID("_OutlineWidth");
        private static readonly int OutlineEnabled =
            Shader.PropertyToID("_OutlineEnabled");

        private readonly List<RendererMaterialSnapshot> outlineRenderers =
            new List<RendererMaterialSnapshot>();
        private GameplayActorView target;
        private LineRenderer groundHalo;
        private Material groundHaloMaterial;
        private Material targetOutlineMaterial;

        public static readonly Color AcquisitionOutlineColor =
            GameplayVisualPalette.SignalOrangeGlow;

        public string TargetActorId => target?.ActorId;

        public bool GroundHaloVisible => groundHalo != null && groundHalo.enabled;

        public bool TargetOutlineVisible =>
            targetOutlineMaterial != null
            && targetOutlineMaterial.GetFloat(OutlineEnabled) >= 0.5f;

        public void SetTarget(GameplayActorView actor)
        {
            if (actor == null)
            {
                throw new ArgumentNullException(nameof(actor));
            }

            if (ReferenceEquals(target, actor))
            {
                return;
            }

            ClearTarget();
            target = actor;
            EnsureTargetOutline();
            EnsureGroundHalo();
        }

        public void SetVisible(bool outlineVisible, bool turnHaloVisible)
        {
            if (groundHalo != null)
            {
                groundHalo.enabled = turnHaloVisible;
            }

            if (targetOutlineMaterial != null)
            {
                targetOutlineMaterial.SetFloat(
                    OutlineEnabled,
                    outlineVisible ? 1f : 0f);
            }
        }

        public void ClearTarget()
        {
            SetVisible(outlineVisible: false, turnHaloVisible: false);
            DestroyGroundHalo();
            DestroyTargetOutline();
            target = null;
        }

        public void Dispose()
        {
            ClearTarget();
        }

        private void EnsureGroundHalo()
        {
            Shader shader = Shader.Find(GroundHaloShaderName);
            if (shader == null)
            {
                throw new InvalidOperationException(
                    $"Target halo shader '{GroundHaloShaderName}' was not found.");
            }

            groundHaloMaterial = new Material(shader)
            {
                name = "Target Turn Ground Halo",
                hideFlags = HideFlags.HideAndDontSave,
            };
            groundHaloMaterial.SetColor(
                BaseColor,
                GameplayVisualPalette.EmissionBase);
            groundHaloMaterial.SetColor(
                EmissionColor,
                GameplayVisualPalette.SignalBlueGlow);
            groundHaloMaterial.SetFloat(EmissionIntensity, 2.8f);

            var haloObject = new GameObject("Target Turn Ground Halo");
            haloObject.transform.SetParent(target.Transform, false);
            groundHalo = haloObject.AddComponent<LineRenderer>();
            groundHalo.useWorldSpace = false;
            groundHalo.loop = true;
            groundHalo.positionCount = HaloSegments;
            groundHalo.sharedMaterial = groundHaloMaterial;
            groundHalo.widthMultiplier = 0.045f;
            groundHalo.numCornerVertices = 2;
            groundHalo.numCapVertices = 2;
            groundHalo.textureMode = LineTextureMode.Stretch;
            groundHalo.shadowCastingMode = ShadowCastingMode.Off;
            groundHalo.receiveShadows = false;
            groundHalo.lightProbeUsage = LightProbeUsage.Off;
            groundHalo.reflectionProbeUsage = ReflectionProbeUsage.Off;
            for (int index = 0; index < HaloSegments; index++)
            {
                float angle = index * Mathf.PI * 2f / HaloSegments;
                groundHalo.SetPosition(
                    index,
                    new Vector3(
                        Mathf.Cos(angle) * 0.62f,
                        0.06f,
                        Mathf.Sin(angle) * 0.62f));
            }
        }

        private void EnsureTargetOutline()
        {
            Shader shader = Shader.Find(TargetOutlineShaderName);
            if (shader == null)
            {
                throw new InvalidOperationException(
                    $"Target outline shader '{TargetOutlineShaderName}' was not found.");
            }

            targetOutlineMaterial = new Material(shader)
            {
                name = "Target Acquisition Outline",
                hideFlags = HideFlags.HideAndDontSave,
            };
            targetOutlineMaterial.SetColor(
                OutlineColor,
                AcquisitionOutlineColor);
            targetOutlineMaterial.SetFloat(OutlineWidth, 0.024f);
            targetOutlineMaterial.SetFloat(OutlineEnabled, 0f);

            foreach (Renderer renderer in
                target.Transform.GetComponentsInChildren<Renderer>(true))
            {
                if (!(renderer is MeshRenderer)
                    && !(renderer is SkinnedMeshRenderer))
                {
                    continue;
                }

                Material[] originalMaterials = renderer.sharedMaterials;
                var outlinedMaterials = new Material[originalMaterials.Length + 1];
                originalMaterials.CopyTo(outlinedMaterials, 0);
                outlinedMaterials[outlinedMaterials.Length - 1] =
                    targetOutlineMaterial;
                outlineRenderers.Add(
                    new RendererMaterialSnapshot(renderer, originalMaterials));
                renderer.sharedMaterials = outlinedMaterials;
            }
        }

        private void DestroyGroundHalo()
        {
            GameplayObjectLifecycle.Destroy(
                groundHalo != null ? groundHalo.gameObject : null);
            groundHalo = null;
            GameplayObjectLifecycle.Destroy(groundHaloMaterial);
            groundHaloMaterial = null;
        }

        private void DestroyTargetOutline()
        {
            foreach (RendererMaterialSnapshot snapshot in outlineRenderers)
            {
                snapshot.Restore();
            }

            outlineRenderers.Clear();
            GameplayObjectLifecycle.Destroy(targetOutlineMaterial);
            targetOutlineMaterial = null;
        }

        private sealed class RendererMaterialSnapshot
        {
            private readonly Renderer renderer;
            private readonly Material[] materials;

            public RendererMaterialSnapshot(
                Renderer targetRenderer,
                Material[] originalMaterials)
            {
                renderer = targetRenderer;
                materials = originalMaterials;
            }

            public void Restore()
            {
                if (renderer != null)
                {
                    renderer.sharedMaterials = materials;
                }
            }
        }
    }
}
