using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    public sealed class ActorCelShadingPresenter : MonoBehaviour
    {
        internal const string OutlineShaderName = "GritGud/RuntimeOutline";

        private static readonly int AmbientStrength =
            Shader.PropertyToID("_AmbientStrength");
        private static readonly int ShadowStrength =
            Shader.PropertyToID("_ShadowStrength");
        private static readonly int ShadowColor = Shader.PropertyToID("_ShadowColor");
        private static readonly int OutlineWidth = Shader.PropertyToID("_OutlineWidth");
        private static readonly int OutlineSoftness =
            Shader.PropertyToID("_OutlineSoftness");
        private static readonly int OutlineColor = Shader.PropertyToID("_OutlineColor");
        private static readonly int PlayerCutoutEnabled =
            Shader.PropertyToID("_PlayerCutoutEnabled");
        private static readonly int Smoothness = Shader.PropertyToID("_Smoothness");
        private static readonly int SpecularStrength =
            Shader.PropertyToID("_SpecularStrength");
        private static readonly int EdgeSheenStrength =
            Shader.PropertyToID("_EdgeSheenStrength");

        private GameplayCelMaterialStyle style;
        private Material outlineMaterial;
        private GameplayVisualTheme theme;

        public bool IsApplied => style != null;

        public bool IsOutlineApplied => outlineMaterial != null;

        private void Awake()
        {
            Apply();
        }

        internal void Apply()
        {
            if (style == null)
            {
                theme = GameplayVisualTheme.LoadDefault();
                style = GameplayCelMaterialStyle.Create(
                    transform,
                    includeRenderer: IsActiveRenderer,
                    preserveMaterial: IsOutlineMaterial,
                    configureMaterial: material =>
                        ConfigureActorMaterial(material, theme));
                outlineMaterial = CreateOutlineMaterial();
                AppendOutlinePasses();
            }
        }

        private static bool IsActiveRenderer(Renderer renderer)
        {
            return renderer.gameObject.activeInHierarchy;
        }

        private static bool IsOutlineMaterial(Material material)
        {
            return material.shader != null && material.shader.name == OutlineShaderName;
        }

        internal static void ConfigureActorMaterial(Material material)
        {
            ConfigureActorMaterial(material, GameplayVisualTheme.LoadDefault());
        }

        internal static void ConfigureActorMaterial(
            Material material,
            GameplayVisualTheme visualTheme)
        {
            ActorSurfacePresentationDefinition actor = visualTheme.ActorSurface;
            material.SetFloat(AmbientStrength, actor.AmbientStrength);
            material.SetFloat(ShadowStrength, actor.ShadowStrength);
            material.SetColor(ShadowColor, actor.ShadowColor);
            material.SetFloat(OutlineWidth, actor.SilhouetteWidth);
            material.SetFloat(OutlineSoftness, actor.SilhouetteSoftness);
            material.SetFloat(Smoothness, actor.Smoothness);
            material.SetFloat(SpecularStrength, actor.SpecularStrength);
            material.SetFloat(EdgeSheenStrength, actor.EdgeSheenStrength);
        }

        private Material CreateOutlineMaterial()
        {
            Shader shader = Shader.Find(OutlineShaderName);
            if (shader == null)
            {
                throw new System.InvalidOperationException(
                    $"Actor outline shader '{OutlineShaderName}' could not be loaded.");
            }

            var material = new Material(shader)
            {
                name = "Player Cel Outline",
                hideFlags = HideFlags.HideAndDontSave,
            };
            material.SetColor(
                OutlineColor,
                theme.Outlines.Color);
            material.SetFloat(OutlineWidth, theme.Outlines.ActorWidth);
            material.SetFloat(PlayerCutoutEnabled, 0f);
            return material;
        }

        private void AppendOutlinePasses()
        {
            foreach (SkinnedMeshRenderer renderer in
                GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!renderer.gameObject.activeInHierarchy || renderer.sharedMesh == null)
                {
                    continue;
                }

                Material[] materials = renderer.sharedMaterials;
                if (System.Array.Exists(materials, IsOutlineMaterial))
                {
                    continue;
                }

                var outlinedMaterials = new Material[materials.Length + 1];
                materials.CopyTo(outlinedMaterials, 0);
                outlinedMaterials[outlinedMaterials.Length - 1] = outlineMaterial;
                renderer.sharedMaterials = outlinedMaterials;
            }
        }

        private void OnDestroy()
        {
            style?.Dispose();
            style = null;
            GameplayObjectLifecycle.Destroy(outlineMaterial);
            outlineMaterial = null;
            theme = null;
        }
    }
}
