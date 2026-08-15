using System.Collections;
using System.Collections.Generic;
using GritGud.Application.Levels;
using GritGud.Presentation.LevelEditing;
using GritGud.Presentation.Gameplay;
using GritGud.Domain.Levels;
using GritGud.Presentation.Levels;
using GritGud.Presentation.CharacterEditing;
using GritGud.Presentation.Supabase;
using UnityEngine;

namespace GritGud.Presentation.Bootstrap
{
    public enum ApplicationMode
    {
        Menu,
        Gameplay,
        LevelEditor,
        CharacterEditor,
    }

    [DefaultExecutionOrder(-1000)]
    public sealed class GameBootstrap : MonoBehaviour
    {
        private StartMenu startMenu;
        private LevelEditorController levelEditor;
        private CharacterEditorController characterEditor;
        private GameplayController gameplay;
        private Coroutine gameplayStartRoutine;
        private CommittedLevelLibrary committedLevels;
        private bool editorTestActive;
        private SupabaseRuntime supabase;

        public static GameBootstrap Instance { get; private set; }

        public SupabaseRuntime Supabase => supabase;

        public ApplicationMode CurrentMode { get; private set; } = ApplicationMode.Menu;

        public IReadOnlyList<CommittedLevelEntry> CommittedLevels
        {
            get
            {
                EnsureCommittedLevels();
                return committedLevels.Entries;
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            supabase = GetComponent<SupabaseRuntime>() ?? gameObject.AddComponent<SupabaseRuntime>();
            EnsureCommittedLevels();
            EnsureStartMenu();
        }

        public void OpenLevelEditor()
        {
            OpenCommittedLevelEditor(
                RequireDefaultCommittedLevel(requirePlayable: false).ResourceKey);
        }

        public void OpenCommittedLevelEditor(string resourceKey)
        {
            EnsureCommittedLevels();
            EnsureStartMenu();
            CommittedLevelEntry entry = committedLevels.Find(resourceKey)
                ?? throw new System.InvalidOperationException(
                    $"Committed level resource '{resourceKey}' was not found.");
            OpenLevelTool(
                startInPreview: false,
                committedLevels.OpenForEditing(resourceKey),
                entry.DisplayName);
        }

        public void OpenNewLevelEditor()
        {
            EnsureStartMenu();
            OpenLevelTool(
                startInPreview: false,
                LevelDocumentFactory.CreateNew(),
                "new level",
                initialDocumentIsSaved: false);
        }

        public void OpenCharacterEditor()
        {
            EnsureStartMenu();
            CancelPendingGameplayStart();
            gameplay?.EndSession();
            levelEditor?.EndSession();
            startMenu.enabled = false;
            if (characterEditor == null)
                characterEditor = gameObject.AddComponent<CharacterEditorController>();
            try
            {
                characterEditor.Begin();
                CurrentMode = ApplicationMode.CharacterEditor;
            }
            catch
            {
                characterEditor.EndSession();
                startMenu.enabled = true;
                throw;
            }
        }

        public void PlayMainLevel()
        {
            PlayCommittedLevel(
                RequireDefaultCommittedLevel(requirePlayable: true).ResourceKey);
        }

        public void PlayCommittedLevel(string resourceKey)
        {
            if (gameplayStartRoutine != null || CurrentMode == ApplicationMode.Gameplay)
            {
                return;
            }

            EnsureCommittedLevels();
            LevelDocument level = committedLevels.OpenForPlay(resourceKey);
            gameplayStartRoutine = StartCoroutine(BeginGameplayOnNextFrame(level));
        }

        public void PlayEditorTest(LevelDocument snapshot)
        {
            if (snapshot == null || gameplayStartRoutine != null || CurrentMode == ApplicationMode.Gameplay)
            {
                return;
            }

            gameplayStartRoutine = StartCoroutine(BeginEditorTestOnNextFrame(snapshot.DeepCopy()));
        }

        private IEnumerator BeginGameplayOnNextFrame(LevelDocument level)
        {
            EnsureStartMenu();
            editorTestActive = false;
            levelEditor?.EndSession();
            characterEditor?.EndSession();
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
                gameplay.BeginCommitted(level);
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
            characterEditor?.EndSession();
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

        private void OpenLevelTool(
            bool startInPreview,
            LevelDocument initialDocument,
            string sourceLabel,
            bool initialDocumentIsSaved = true)
        {
            EnsureStartMenu();
            CancelPendingGameplayStart();
            gameplay?.EndSession();
            characterEditor?.EndSession();
            startMenu.enabled = false;
            if (levelEditor == null)
            {
                levelEditor = gameObject.AddComponent<LevelEditorController>();
            }

            try
            {
                levelEditor.Begin(
                    startInPreview,
                    initialDocument,
                    sourceLabel,
                    initialDocumentIsSaved);
                CurrentMode = ApplicationMode.LevelEditor;
            }
            catch
            {
                levelEditor.EndSession();
                startMenu.enabled = true;
                throw;
            }
        }

        private CommittedLevelEntry RequireDefaultCommittedLevel(bool requirePlayable)
        {
            EnsureCommittedLevels();
            CommittedLevelEntry configured = committedLevels.Find(
                UnityCommittedLevelLibrary.DefaultResourceKey);
            if (configured != null
                && (requirePlayable ? configured.CanPlay : configured.CanEdit))
            {
                return configured;
            }

            foreach (CommittedLevelEntry entry in committedLevels.Entries)
            {
                if (requirePlayable ? entry.CanPlay : entry.CanEdit)
                {
                    return entry;
                }
            }

            throw new System.InvalidOperationException(
                requirePlayable
                    ? "No playable committed levels were found."
                    : "No editable committed levels were found.");
        }

        private void EnsureCommittedLevels()
        {
            if (committedLevels == null)
            {
                committedLevels = UnityCommittedLevelLibrary.LoadDefault();
            }
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
