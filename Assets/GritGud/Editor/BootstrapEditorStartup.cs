using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GritGud.Editor
{
    [InitializeOnLoad]
    public static class BootstrapEditorStartup
    {
        private const string OpenedThisSessionKey =
            "GritGud.Editor.BootstrapSceneOpenedThisSession";

        static BootstrapEditorStartup()
        {
            EditorApplication.delayCall += ConfigureStartupScene;
        }

        private static void ConfigureStartupScene()
        {
            if (UnityEngine.Application.isBatchMode)
            {
                return;
            }

            SceneAsset bootstrap = AssetDatabase.LoadAssetAtPath<SceneAsset>(
                BootstrapSceneGenerator.ScenePath);
            if (bootstrap == null)
            {
                Debug.LogWarning(
                    $"Bootstrap scene '{BootstrapSceneGenerator.ScenePath}' was not found.");
                return;
            }

            EditorSceneManager.playModeStartScene = bootstrap;

            if (SessionState.GetBool(OpenedThisSessionKey, false))
            {
                return;
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            if (HasDirtyScene())
            {
                SessionState.SetBool(OpenedThisSessionKey, true);
                return;
            }

            SessionState.SetBool(OpenedThisSessionKey, true);
            if (string.Equals(
                SceneManager.GetActiveScene().path,
                BootstrapSceneGenerator.ScenePath,
                System.StringComparison.Ordinal))
            {
                return;
            }

            EditorSceneManager.OpenScene(
                BootstrapSceneGenerator.ScenePath,
                OpenSceneMode.Single);
        }

        private static bool HasDirtyScene()
        {
            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                if (SceneManager.GetSceneAt(index).isDirty)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
