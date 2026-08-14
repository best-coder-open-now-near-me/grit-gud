using System;
using System.Collections.Generic;
using GritGud.Presentation.Levels.Runtime;
using UnityEngine;
using UnityEngine.Rendering;

namespace GritGud.Presentation.Gameplay
{
    internal sealed class GameplayEnvironmentStyle : IDisposable
    {
        private const string OutlineShaderName = "GritGud/RuntimeOutline";
        private static readonly int OutlineColor = Shader.PropertyToID("_OutlineColor");
        private static readonly int OutlineWidth = Shader.PropertyToID("_OutlineWidth");
        private static readonly int PlayerCutoutEnabled =
            Shader.PropertyToID("_PlayerCutoutEnabled");
        private static readonly int CelThreshold = Shader.PropertyToID("_CelThreshold");
        private static readonly int CelSoftness = Shader.PropertyToID("_CelSoftness");
        private static readonly int ShadowColor = Shader.PropertyToID("_ShadowColor");
        private static readonly int ShadowStrength = Shader.PropertyToID("_ShadowStrength");
        private static readonly int AmbientStrength = Shader.PropertyToID("_AmbientStrength");
        private static readonly int Smoothness = Shader.PropertyToID("_Smoothness");
        private static readonly int SpecularStrength = Shader.PropertyToID("_SpecularStrength");
        private static readonly int SpecularColor = Shader.PropertyToID("_SpecularColor");
        private static readonly int EdgeSheenStrength =
            Shader.PropertyToID("_EdgeSheenStrength");

        private readonly List<GameObject> outlineObjects = new List<GameObject>();
        private GameplayCelMaterialStyle materialStyle;
        private Material outlineMaterial;
        private Material wallOutlineMaterial;
        private GameplayVisualTheme theme;
        private SurfacePresentationCatalog surfaceCatalog;

        private GameplayEnvironmentStyle()
        {
        }

        public static GameplayEnvironmentStyle Create(
            Transform environmentRoot,
            GameplayVisualTheme visualTheme,
            SurfacePresentationCatalog surfaces)
        {
            if (environmentRoot == null)
            {
                throw new ArgumentNullException(nameof(environmentRoot));
            }
            if (visualTheme == null)
            {
                throw new ArgumentNullException(nameof(visualTheme));
            }
            if (surfaces == null)
            {
                throw new ArgumentNullException(nameof(surfaces));
            }

            Shader outlineShader = Shader.Find(OutlineShaderName);
            if (outlineShader == null)
            {
                throw new InvalidOperationException(
                    "Gameplay environment shaders could not be loaded.");
            }

            var style = new GameplayEnvironmentStyle
            {
                theme = visualTheme,
                surfaceCatalog = surfaces,
                outlineMaterial = CreateOutlineMaterial(
                    outlineShader,
                    "Gameplay Environment Outline",
                    usesPlayerCutout: false,
                    visualTheme.Outlines),
                wallOutlineMaterial = CreateOutlineMaterial(
                    outlineShader,
                    "Gameplay Wall Cutout Outline",
                    usesPlayerCutout: true,
                    visualTheme.Outlines),
            };
            style.materialStyle = GameplayCelMaterialStyle.Create(
                environmentRoot,
                UsesPlayerCutout,
                includeRenderer: IsEnvironmentSurfaceRenderer,
                configureRendererMaterial: style.ConfigureSurfaceMaterial,
                materialVariantKey: GetSurfaceVariantKey);
            style.ApplyOutlines(environmentRoot);
            return style;
        }

        public void Dispose()
        {
            materialStyle?.Dispose();
            materialStyle = null;
            foreach (GameObject outlineObject in outlineObjects)
            {
                GameplayObjectLifecycle.Destroy(outlineObject);
            }

            outlineObjects.Clear();
            GameplayObjectLifecycle.Destroy(outlineMaterial);
            outlineMaterial = null;
            GameplayObjectLifecycle.Destroy(wallOutlineMaterial);
            wallOutlineMaterial = null;
            theme = null;
            surfaceCatalog = null;
        }

        private void ApplyOutlines(Transform environmentRoot)
        {
            Renderer[] renderers = environmentRoot.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in renderers)
            {
                bool usesPlayerCutout = UsesPlayerCutout(renderer);
                if (ShouldOutline(renderer))
                {
                    CreateOutline(renderer, usesPlayerCutout);
                }
            }
        }

        private static Material CreateOutlineMaterial(
            Shader shader,
            string name,
            bool usesPlayerCutout,
            OutlinePresentationDefinition definition)
        {
            var material = new Material(shader)
            {
                name = name,
                hideFlags = HideFlags.HideAndDontSave,
            };
            material.SetColor(
                OutlineColor,
                definition.Color);
            material.SetFloat(OutlineWidth, definition.EnvironmentWidth);
            material.SetFloat(
                PlayerCutoutEnabled,
                usesPlayerCutout ? 1f : 0f);
            return material;
        }

        private void ConfigureSurfaceMaterial(Renderer renderer, Material material)
        {
            CelSurfacePresentationDefinition cel = theme.CelSurface;
            material.SetFloat(CelThreshold, cel.Threshold);
            material.SetFloat(CelSoftness, cel.Softness);
            material.SetColor(ShadowColor, cel.ShadowColor);
            material.SetFloat(ShadowStrength, cel.ShadowStrength);
            material.SetFloat(AmbientStrength, cel.AmbientStrength);

            LevelEntityView entity = renderer.GetComponentInParent<LevelEntityView>();
            SurfacePresentationDefinition surface = surfaceCatalog.Get(
                entity?.Archetype.SurfacePresentationId);
            material.SetFloat(Smoothness, surface.Smoothness);
            material.SetFloat(SpecularStrength, surface.SpecularStrength);
            material.SetColor(SpecularColor, surface.SpecularColor);
            material.SetFloat(EdgeSheenStrength, surface.EdgeSheenStrength);
        }

        private static string GetSurfaceVariantKey(Renderer renderer)
        {
            LevelEntityView entity = renderer.GetComponentInParent<LevelEntityView>();
            return entity?.Archetype.SurfacePresentationId
                ?? SurfacePresentationCatalog.DefaultSurfaceId;
        }


        private static bool ShouldOutline(Renderer renderer)
        {
            if (!(renderer is MeshRenderer) || renderer.GetComponent<MeshFilter>() == null)
            {
                return false;
            }

            LevelEntityView entity = renderer.GetComponentInParent<LevelEntityView>();
            return entity != null
                && entity.ArchetypeId.IndexOf("floor", StringComparison.OrdinalIgnoreCase) < 0;
        }

        private static bool IsEnvironmentSurfaceRenderer(Renderer renderer)
        {
            if (!(renderer is MeshRenderer)
                || renderer.GetComponent<MeshFilter>() == null)
            {
                return false;
            }

            return renderer.GetComponentInParent<LevelEntityView>() != null
                || renderer.GetComponentInParent<TerrainChunkTag>() != null;
        }

        private static bool UsesPlayerCutout(Renderer renderer)
        {
            LevelEntityView entity = renderer.GetComponentInParent<LevelEntityView>();
            return entity != null
                && GameplayCameraOcclusionRules.UsesPlayerCutout(
                    entity.ArchetypeId);
        }

        private void CreateOutline(Renderer source, bool usesPlayerCutout)
        {
            Mesh mesh = source.GetComponent<MeshFilter>().sharedMesh;
            if (mesh == null)
            {
                return;
            }

            var outlineObject = new GameObject(source.name + " - Outline");
            outlineObject.layer = source.gameObject.layer;
            outlineObject.transform.SetParent(source.transform, false);
            outlineObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer outlineRenderer = outlineObject.AddComponent<MeshRenderer>();
            var materials = new Material[Mathf.Max(1, mesh.subMeshCount)];
            for (int index = 0; index < materials.Length; index++)
            {
                materials[index] = usesPlayerCutout
                    ? wallOutlineMaterial
                    : outlineMaterial;
            }

            outlineRenderer.sharedMaterials = materials;
            outlineRenderer.enabled = source.enabled;
            outlineRenderer.shadowCastingMode = ShadowCastingMode.Off;
            outlineRenderer.receiveShadows = false;
            outlineRenderer.lightProbeUsage = LightProbeUsage.Off;
            outlineRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            outlineRenderer.renderingLayerMask = source.renderingLayerMask;
            outlineObjects.Add(outlineObject);
        }
    }
}
