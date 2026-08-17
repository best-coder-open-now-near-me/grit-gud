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
        private string targetId;
        private Transform targetRoot;
        private LineRenderer groundHalo;
        private Material groundHaloMaterial;
        private Material targetOutlineMaterial;
        private Color feedbackColor = ValidColor;

        public static readonly Color ValidColor =
            GameplayVisualPalette.TargetingValid;

        public static readonly Color InvalidColor =
            GameplayVisualPalette.TargetingInvalid;

        public static readonly Color AcquisitionOutlineColor = ValidColor;

        public string TargetId => targetId;

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

            SetTarget(actor.ActorId, actor.Transform);
        }

        public void SetTarget(string id, Transform root)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException(
                    "Target feedback requires a target identifier.",
                    nameof(id));
            }
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            if (string.Equals(targetId, id, StringComparison.Ordinal)
                && ReferenceEquals(targetRoot, root))
            {
                return;
            }

            ClearTarget();
            targetId = id;
            targetRoot = root;
            EnsureTargetOutline();
            EnsureGroundHalo();
        }

        public void SetColor(Color color)
        {
            feedbackColor = color;
            if (groundHaloMaterial != null)
            {
                groundHaloMaterial.SetColor(EmissionColor, feedbackColor);
            }
            if (targetOutlineMaterial != null)
            {
                targetOutlineMaterial.SetColor(OutlineColor, feedbackColor);
            }
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
            targetId = null;
            targetRoot = null;
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
                feedbackColor);
            groundHaloMaterial.SetFloat(EmissionIntensity, 2.8f);

            var haloObject = new GameObject("Target Turn Ground Halo");
            haloObject.transform.SetParent(targetRoot, false);
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
                feedbackColor);
            targetOutlineMaterial.SetFloat(OutlineWidth, 0.024f);
            targetOutlineMaterial.SetFloat(OutlineEnabled, 0f);

            foreach (Renderer renderer in
                targetRoot.GetComponentsInChildren<Renderer>(true))
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
