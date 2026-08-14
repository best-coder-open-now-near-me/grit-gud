using GritGud.Presentation.Bootstrap;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GritGud.Editor
{
    public static class BootstrapSceneGenerator
    {
        public const string ScenePath = "Assets/GritGud/Scenes/Bootstrap.unity";

        [MenuItem("Grit Gud/Regenerate Bootstrap Scene")]
        public static void Generate()
        {
            EnsureAssetFolder("Assets/GritGud/Scenes");

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var application = new GameObject("Application");
            application.AddComponent<GameBootstrap>();

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetPositionAndRotation(
                new Vector3(0f, 8f, -10f),
                Quaternion.Euler(32f, 0f, 0f));
            cameraObject.AddComponent<Camera>();

            var lightObject = new GameObject("Directional Light");
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1f;

            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                throw new System.InvalidOperationException($"Could not save {ScenePath}.");
            }

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true),
            };
            EditorSceneManager.playModeStartScene =
                AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);

            AssetDatabase.SaveAssets();
        }

        private static void EnsureAssetFolder(string path)
        {
            string[] segments = path.Split('/');
            string current = segments[0];

            for (int index = 1; index < segments.Length; index++)
            {
                string next = $"{current}/{segments[index]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segments[index]);
                }

                current = next;
            }
        }
    }
}
