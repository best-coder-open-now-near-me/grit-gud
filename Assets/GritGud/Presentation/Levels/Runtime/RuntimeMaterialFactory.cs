using UnityEngine;

namespace GritGud.Presentation.Levels.Runtime
{
    public static class RuntimeMaterialFactory
    {
        private static readonly string[] FallbackShaderNames =
        {
            "GritGud/RuntimeColor",
            "Universal Render Pipeline/Unlit",
            "Sprites/Default",
            "UI/Default",
            "Hidden/Internal-Colored",
        };

        public static Material CreateCelColor(Color color, string materialName)
        {
            Shader shader = Shader.Find("GritGud/CelSurface");
            if (shader == null)
            {
                return CreateColor(color, materialName);
            }

            return CreateMaterial(shader, color, materialName);
        }

        public static Material CreateColor(Color color, string materialName)
        {
            Shader shader = null;
            foreach (string shaderName in FallbackShaderNames)
            {
                shader = Shader.Find(shaderName);
                if (shader != null)
                {
                    break;
                }
            }

            if (shader == null)
            {
                Debug.LogError(
                    $"Could not create runtime material '{materialName}': no supported shader is available.");
                return null;
            }

            return CreateMaterial(shader, color, materialName);
        }

        private static Material CreateMaterial(
            Shader shader,
            Color color,
            string materialName)
        {
            var material = new Material(shader)
            {
                name = materialName,
                color = color,
                hideFlags = HideFlags.HideAndDontSave,
            };
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            return material;
        }
    }
}
