using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace GritGud.Presentation.Tests
{
    public sealed class RuntimeShaderBuildSettingsTests
    {
        private static readonly string[] RuntimeShaderNames =
        {
            "GritGud/RuntimeColor",
            "GritGud/CelSurface",
            "GritGud/RuntimeOutline",
            "GritGud/EmissiveSurface",
            "GritGud/TacticalWireframe",
        };

        [Test]
        public void ShadersLoadedOnlyByNameAreRetainedInPlayerBuilds()
        {
            GraphicsSettings settings = AssetDatabase
                .LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")
                .OfType<GraphicsSettings>()
                .Single();
            var serializedSettings = new SerializedObject(settings);
            SerializedProperty includedProperty = serializedSettings.FindProperty(
                "m_AlwaysIncludedShaders");
            var includedNames = new HashSet<string>();
            for (int index = 0; index < includedProperty.arraySize; index++)
            {
                var shader = includedProperty
                    .GetArrayElementAtIndex(index)
                    .objectReferenceValue as Shader;
                if (shader != null)
                {
                    includedNames.Add(shader.name);
                }
            }

            Assert.That(
                includedNames,
                Is.SupersetOf(RuntimeShaderNames),
                "Shaders created through Shader.Find must be retained explicitly so " +
                "player stripping cannot remove them.");
        }

        [Test]
        public void EnvironmentShadersExposeWallCutoutControl()
        {
            Shader celSurface = Shader.Find("GritGud/CelSurface");
            Shader outline = Shader.Find("GritGud/RuntimeOutline");

            Assert.That(celSurface, Is.Not.Null);
            Assert.That(outline, Is.Not.Null);
            Assert.That(
                celSurface.FindPropertyIndex("_PlayerCutoutEnabled"),
                Is.GreaterThanOrEqualTo(0));
            Assert.That(
                outline.FindPropertyIndex("_PlayerCutoutEnabled"),
                Is.GreaterThanOrEqualTo(0));
            Assert.That(
                outline.FindPropertyIndex("_OutlineEnabled"),
                Is.GreaterThanOrEqualTo(0));
        }
    }
}
