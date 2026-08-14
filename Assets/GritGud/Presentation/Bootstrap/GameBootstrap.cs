using System.Collections;
using GritGud.Presentation.LevelEditing;
using GritGud.Presentation.Gameplay;
using GritGud.Domain.Levels;
using UnityEngine;

namespace GritGud.Presentation.Bootstrap
{
    public enum ApplicationMode
    {
        Menu,
        Gameplay,
        LevelEditor,
    }

    [DefaultExecutionOrder(-1000)]
    public sealed class GameBootstrap : MonoBehaviour
    {
        private StartMenu startMenu;
        private LevelEditorController levelEditor;
        private GameplayController gameplay;
        private Coroutine gameplayStartRoutine;
        private bool editorTestActive;

        public static GameBootstrap Instance { get; private set; }

        public ApplicationMode CurrentMode { get; private set; } = ApplicationMode.Menu;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            EnsureStartMenu();
        }

        public void OpenLevelEditor()
        {
            EnsureStartMenu();
            OpenLevelTool(startInPreview: false);
        }

        public void PlayMainLevel()
        {
            if (gameplayStartRoutine != null || CurrentMode == ApplicationMode.Gameplay)
            {
                return;
            }

            gameplayStartRoutine = StartCoroutine(BeginGameplayOnNextFrame());
        }

        public void PlayEditorTest(LevelDocument snapshot)
        {
            if (snapshot == null || gameplayStartRoutine != null || CurrentMode == ApplicationMode.Gameplay)
            {
                return;
            }

            gameplayStartRoutine = StartCoroutine(BeginEditorTestOnNextFrame(snapshot.DeepCopy()));
        }

        private IEnumerator BeginGameplayOnNextFrame()
        {
            EnsureStartMenu();
            editorTestActive = false;
            levelEditor?.EndSession();
            if (gameplay == null)
            {
                gameplay = gameObject.AddComponent<GameplayController>();
            }

            startMenu.enabled = false;
            // This call can instantiate a complete level. Let the IMGUI click
            // event finish first; doing all of that work inside OnGUI can leave
            // the Game view showing the menu until play mode is stopped.
            yield return null;

            try
            {
                gameplay.Begin();
                CurrentMode = ApplicationMode.Gameplay;
            }
            catch
            {
                gameplay.EndSession();
                startMenu.enabled = true;
                CurrentMode = ApplicationMode.Menu;
                throw;
            }
            finally
            {
                gameplayStartRoutine = null;
            }
        }

        private IEnumerator BeginEditorTestOnNextFrame(LevelDocument snapshot)
        {
            EnsureStartMenu();
            if (levelEditor == null)
            {
                gameplayStartRoutine = null;
                yield break;
            }

            levelEditor.SuspendForTestPlay();
            if (gameplay == null)
            {
                gameplay = gameObject.AddComponent<GameplayController>();
            }

            startMenu.enabled = false;
            yield return null;
            try
            {
                gameplay.BeginSandbox(snapshot);
                editorTestActive = true;
                CurrentMode = ApplicationMode.Gameplay;
            }
            catch
            {
                gameplay.EndSession();
                levelEditor.ResumeFromTestPlay();
                CurrentMode = ApplicationMode.LevelEditor;
                throw;
            }
            finally
            {
                gameplayStartRoutine = null;
            }
        }

        public void ReturnToMenu()
        {
            EnsureStartMenu();
            CancelPendingGameplayStart();
            editorTestActive = false;
            levelEditor?.EndSession();
            gameplay?.EndSession();
            startMenu.enabled = true;
            CurrentMode = ApplicationMode.Menu;
        }

        public void ReturnToEditor()
        {
            if (!editorTestActive)
            {
                return;
            }

            gameplay?.EndSession();
            editorTestActive = false;
            levelEditor?.ResumeFromTestPlay();
            startMenu.enabled = false;
            CurrentMode = ApplicationMode.LevelEditor;
        }

        private void OnGUI()
        {
            if (!editorTestActive || CurrentMode != ApplicationMode.Gameplay)
            {
                return;
            }

            if (GUI.Button(new Rect(Screen.width - 180f, 18f, 162f, 36f), "RETURN TO EDITOR"))
            {
                ReturnToEditor();
            }
        }

        private void OpenLevelTool(bool startInPreview)
        {
            EnsureStartMenu();
            CancelPendingGameplayStart();
            gameplay?.EndSession();
            startMenu.enabled = false;
            if (levelEditor == null)
            {
                levelEditor = gameObject.AddComponent<LevelEditorController>();
            }

            levelEditor.Begin(startInPreview);
            CurrentMode = ApplicationMode.LevelEditor;
        }

        private void CancelPendingGameplayStart()
        {
            if (gameplayStartRoutine == null)
            {
                return;
            }

            StopCoroutine(gameplayStartRoutine);
            gameplayStartRoutine = null;
        }

        private void EnsureStartMenu()
        {
            if (startMenu != null)
            {
                return;
            }

            startMenu = GetComponent<StartMenu>();
            if (startMenu == null)
            {
                startMenu = gameObject.AddComponent<StartMenu>();
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
