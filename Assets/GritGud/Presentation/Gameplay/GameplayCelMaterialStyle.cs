using System;
using System.Collections.Generic;
using GritGud.Presentation.Levels.Runtime;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    internal sealed class GameplayCelMaterialStyle : IDisposable
    {
        internal const string ShaderName = "GritGud/CelSurface";

        private static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        private static readonly int AlbedoMap = Shader.PropertyToID("_Albedo_Map");
        private static readonly int MainTex = Shader.PropertyToID("_MainTex");
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int Color = Shader.PropertyToID("_Color");
        private static readonly int PlayerCutoutEnabled =
            Shader.PropertyToID("_PlayerCutoutEnabled");
        private static readonly int Smoothness = Shader.PropertyToID("_Smoothness");
        private static readonly int Glossiness = Shader.PropertyToID("_Glossiness");
        private static readonly int Metallic = Shader.PropertyToID("_Metallic");
        private static readonly int SpecularStrength =
            Shader.PropertyToID("_SpecularStrength");

        private readonly List<RendererSnapshot> rendererSnapshots =
            new List<RendererSnapshot>();
        private readonly Dictionary<Material, Material> celMaterials =
            new Dictionary<Material, Material>();
        private readonly Dictionary<Material, Material> cutoutCelMaterials =
            new Dictionary<Material, Material>();
        private readonly Dictionary<string, Material> variantCelMaterials =
            new Dictionary<string, Material>(StringComparer.Ordinal);

        private GameplayCelMaterialStyle()
        {
        }

        public static GameplayCelMaterialStyle Create(
            Transform root,
            Func<Renderer, bool> usesPlayerCutout = null,
            Func<Renderer, bool> includeRenderer = null,
            Func<Material, bool> preserveMaterial = null,
            Action<Material> configureMaterial = null,
            Action<Renderer, Material> configureRendererMaterial = null,
            Func<Renderer, string> materialVariantKey = null)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            Shader celShader = Shader.Find(ShaderName);
            if (celShader == null)
            {
                throw new InvalidOperationException(
                    $"Gameplay cel shader '{ShaderName}' could not be loaded.");
            }

            var style = new GameplayCelMaterialStyle();
            style.Apply(
                root,
                celShader,
                usesPlayerCutout,
                includeRenderer,
                preserveMaterial,
                configureMaterial,
                configureRendererMaterial,
                materialVariantKey);
            return style;
        }

        public void Dispose()
        {
            foreach (RendererSnapshot snapshot in rendererSnapshots)
            {
                snapshot.Restore();
            }

            rendererSnapshots.Clear();
            DestroyMaterials(celMaterials);
            DestroyMaterials(cutoutCelMaterials);
            foreach (Material material in variantCelMaterials.Values)
            {
                GameplayObjectLifecycle.Destroy(material);
            }
            variantCelMaterials.Clear();
        }

        private void Apply(
            Transform root,
            Shader celShader,
            Func<Renderer, bool> usesPlayerCutout,
            Func<Renderer, bool> includeRenderer,
            Func<Material, bool> preserveMaterial,
            Action<Material> configureMaterial,
            Action<Renderer, Material> configureRendererMaterial,
            Func<Renderer, string> materialVariantKey)
        {
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (includeRenderer != null && !includeRenderer(renderer))
                {
                    continue;
                }

                bool cutoutEnabled = usesPlayerCutout?.Invoke(renderer) ?? false;
                Material[] originalMaterials = renderer.sharedMaterials;
                var replacementMaterials = new Material[originalMaterials.Length];
                bool changed = false;
                for (int index = 0; index < originalMaterials.Length; index++)
                {
                    Material original = originalMaterials[index];
                    Material replacement = CreateCelMaterial(
                        original,
                        celShader,
                        cutoutEnabled,
                        preserveMaterial,
                        configureMaterial,
                        configureRendererMaterial,
                        renderer,
                        materialVariantKey);
                    replacementMaterials[index] = replacement;
                    changed |= replacement != original;
                }

                if (!changed)
                {
                    continue;
                }

                rendererSnapshots.Add(new RendererSnapshot(renderer, originalMaterials));
                renderer.sharedMaterials = replacementMaterials;
            }
        }

        private Material CreateCelMaterial(
            Material source,
            Shader celShader,
            bool usesPlayerCutout,
            Func<Material, bool> preserveMaterial,
            Action<Material> configureMaterial,
            Action<Renderer, Material> configureRendererMaterial,
            Renderer renderer,
            Func<Renderer, string> materialVariantKey)
        {
            if (source == null)
            {
                return source;
            }

            if (preserveMaterial?.Invoke(source) ?? false)
            {
                return source;
            }

            string variant = materialVariantKey?.Invoke(renderer) ?? string.Empty;
            Dictionary<Material, Material> cache = usesPlayerCutout
                ? cutoutCelMaterials
                : celMaterials;
            if (!usesPlayerCutout
                && source.shader == celShader
                && configureMaterial == null
                && configureRendererMaterial == null)
            {
                return source;
            }

            string variantCacheKey = string.IsNullOrEmpty(variant)
                ? null
                : $"{source.GetInstanceID()}:{(usesPlayerCutout ? 1 : 0)}:{variant}";
            if (variantCacheKey != null
                && variantCelMaterials.TryGetValue(
                    variantCacheKey,
                    out Material variantExisting))
            {
                return variantExisting;
            }
            if (variantCacheKey == null
                && cache.TryGetValue(source, out Material existing))
            {
                return existing;
            }

            var material = new Material(celShader)
            {
                name = source.name + " - Cel",
                hideFlags = HideFlags.HideAndDontSave,
                enableInstancing = source.enableInstancing,
            };
            UnityEngine.Color color = ReadColor(source);
            material.SetColor(BaseColor, color);
            material.SetColor(Color, color);
            material.SetFloat(
                PlayerCutoutEnabled,
                usesPlayerCutout ? 1f : 0f);
            CopyTexture(source, material);
            CopySurfaceResponse(source, material);
            configureMaterial?.Invoke(material);
            configureRendererMaterial?.Invoke(renderer, material);
            if (variantCacheKey == null)
            {
                cache.Add(source, material);
            }
            else
            {
                variantCelMaterials.Add(variantCacheKey, material);
            }
            return material;
        }

        private static UnityEngine.Color ReadColor(Material source)
        {
            if (source.HasProperty(BaseColor))
            {
                return source.GetColor(BaseColor);
            }

            return source.HasProperty(Color)
                ? source.GetColor(Color)
                : UnityEngine.Color.white;
        }

        private static void CopyTexture(Material source, Material target)
        {
            int sourceProperty = source.HasProperty(BaseMap)
                ? BaseMap
                : source.HasProperty(AlbedoMap)
                    ? AlbedoMap
                    : source.HasProperty(MainTex)
                        ? MainTex
                        : -1;
            if (sourceProperty < 0)
            {
                return;
            }

            target.SetTexture(BaseMap, source.GetTexture(sourceProperty));
            target.SetTextureScale(BaseMap, source.GetTextureScale(sourceProperty));
            target.SetTextureOffset(BaseMap, source.GetTextureOffset(sourceProperty));
        }

        private static void CopySurfaceResponse(Material source, Material target)
        {
            float smoothness = source.HasProperty(Smoothness)
                ? source.GetFloat(Smoothness)
                : source.HasProperty(Glossiness)
                    ? source.GetFloat(Glossiness)
                    : 0.15f;
            float metallic = source.HasProperty(Metallic)
                ? source.GetFloat(Metallic)
                : 0f;
            target.SetFloat(Smoothness, Mathf.Clamp01(smoothness));
            target.SetFloat(
                SpecularStrength,
                Mathf.Clamp01(0.05f + (metallic * 0.28f)));
        }

        private static void DestroyMaterials(Dictionary<Material, Material> materials)
        {
            foreach (Material material in materials.Values)
            {
                GameplayObjectLifecycle.Destroy(material);
            }

            materials.Clear();
        }

        private sealed class RendererSnapshot
        {
            private readonly Renderer renderer;
            private readonly Material[] materials;

            public RendererSnapshot(Renderer source, Material[] originalMaterials)
            {
                renderer = source;
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
