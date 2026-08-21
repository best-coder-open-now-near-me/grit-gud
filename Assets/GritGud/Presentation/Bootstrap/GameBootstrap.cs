using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GritGud.Application.Gameplay;
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
        SimulationViewer,
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
        private LevelDraftLibraryCoordinator draftLibrary;
        private CloudDraftNavigationCommands cloudNavigation;
        private CancellationTokenSource simulationPreparationCancellation;

        public LevelDraftRecord ActiveCloudDraft { get; private set; }

        public void AdoptActiveCloudDraft(LevelDraftRecord draft)
        {
            ActiveCloudDraft = draft ?? throw new System.ArgumentNullException(nameof(draft));
        }

        public static GameBootstrap Instance { get; private set; }

        public SupabaseRuntime Supabase => supabase;

        public LevelDraftLibraryCoordinator DraftLibrary
        {
            get
            {
                EnsureDraftLibrary();
                return draftLibrary;
            }
        }

        public ApplicationMode CurrentMode { get; private set; } = ApplicationMode.Menu;

        public bool IsPreparingSimulation =>
            simulationPreparationCancellation != null;

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
            cloudNavigation = new CloudDraftNavigationCommands(
                new GameBootstrapCloudDraftNavigationGateway(
                    () => DraftLibrary,
                    () => supabase?.Status),
                new GameBootstrapCloudDraftNavigationHost(
                    () => gameplayStartRoutine == null
                        && !IsGameplaySessionActive,
                    () => CurrentMode == ApplicationMode.Menu,
                    BeginCloudDraftGameplay,
                    BeginCloudDraftEditor));
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
            CancelCloudNavigation();
            ActiveCloudDraft = null;
            startMenu?.SetLaunchStatus(string.Empty);
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
            CancelCloudNavigation();
            ActiveCloudDraft = null;
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
            CancelCloudNavigation();
            ActiveCloudDraft = null;
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
            if (gameplayStartRoutine != null || IsGameplaySessionActive)
            {
                return;
            }

            CancelCloudNavigation();
            ActiveCloudDraft = null;
            EnsureCommittedLevels();
            LevelDocument level = committedLevels.OpenForPlay(resourceKey);
            gameplayStartRoutine = StartCoroutine(BeginGameplayOnNextFrame(level));
        }

        public void WatchFirstSimulation()
        {
            if (gameplayStartRoutine != null || IsGameplaySessionActive)
            {
                return;
            }

            CancelCloudNavigation();
            ActiveCloudDraft = null;
            gameplayStartRoutine = StartCoroutine(
                PrepareFirstSimulationOnMenu());
        }

        public Task PlayCloudDraftAsync(
            LevelDraftId id,
            System.Action<string> status) =>
            cloudNavigation.PlayAsync(id, status);

        public Task OpenCloudDraftEditorAsync(
            LevelDraftId id,
            System.Action<string> status) =>
            cloudNavigation.OpenEditorAsync(id, status);

        private void BeginCloudDraftGameplay(LevelDraftRecord draft)
        {
            LevelDocument level = draft.CreateDocumentSnapshot();
            GameplayContentPackage content = GameplayContentLoader.LoadDefault();
            LevelValidationIssue error = LevelValidator
                .Validate(
                    level,
                    content.ValidationContent,
                    LevelValidationProfile.Runtime)
                .FirstOrDefault(issue =>
                    issue.Severity == LevelValidationSeverity.Error);
            if (error != null)
                throw new System.InvalidOperationException(error.Message);

            ActiveCloudDraft = draft;
            gameplayStartRoutine = StartCoroutine(
                BeginGameplayOnNextFrame(level, sandbox: true));
        }

        private void BeginCloudDraftEditor(LevelDraftRecord draft)
        {
            ActiveCloudDraft = draft;
            OpenLevelTool(
                startInPreview: false,
                draft.CreateDocumentSnapshot(),
                "cloud draft: " + draft.Summary.Name,
                initialDocumentIsSaved: true);
        }
        public void PlayEditorTest(LevelDocument snapshot)
        {
            if (snapshot == null || gameplayStartRoutine != null || IsGameplaySessionActive)
            {
                return;
            }

            gameplayStartRoutine = StartCoroutine(BeginEditorTestOnNextFrame(snapshot.DeepCopy()));
        }

        private IEnumerator BeginGameplayOnNextFrame(
            LevelDocument level,
            bool sandbox = false)
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
                if (sandbox)
                    gameplay.BeginSandbox(level);
                else
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

        private IEnumerator PrepareFirstSimulationOnMenu()
        {
            EnsureStartMenu();
            var owner = new CancellationTokenSource();
            simulationPreparationCancellation = owner;
            startMenu.SetLaunchStatus(
                "LOADING DEPOT FIRST SIM…\n"
                + "The viewer will open when its replay is ready.");

            // Leave the menu frame that requested playback before loading and
            // validating the permanent artifact.
            yield return null;

            Task<GameplayBattleReplayPreparationResult<
                GameplayBattleArtifact,
                GameplaySemanticReplayTimeline>> preparation = null;
            GameplayContentPackage simulationContent = null;
            Exception startupFailure = null;
            try
            {
                simulationContent = GameplayContentLoader.LoadDefault();
                preparation = GameplayFirstSimulationPreparationService
                    .PrepareAsync(
                        simulationContent.Assembly,
                        simulationContent.SpatialContent,
                        owner.Token);
            }
            catch (Exception exception)
            {
                startupFailure = exception;
            }

            if (startupFailure != null)
            {
                FinishSimulationPreparationFailure(owner, startupFailure);
                yield break;
            }

            while (!preparation.IsCompleted)
            {
                yield return null;
            }

            if (!ReferenceEquals(simulationPreparationCancellation, owner))
            {
                owner.Dispose();
                yield break;
            }
            if (preparation.IsCanceled)
            {
                ReleaseSimulationPreparation(owner);
                yield break;
            }
            if (preparation.IsFaulted)
            {
                FinishSimulationPreparationFailure(
                    owner,
                    preparation.Exception.GetBaseException());
                yield break;
            }

            GameplayBattleReplayPreparationResult<
                GameplayBattleArtifact,
                GameplaySemanticReplayTimeline> prepared = preparation.Result;
            if (!prepared.IsReady)
            {
                FinishSimulationPreparationFailure(
                    owner,
                    new InvalidOperationException(
                        "The embedded simulation does not match Depot content."));
                yield break;
            }

            editorTestActive = false;
            levelEditor?.EndSession();
            characterEditor?.EndSession();
            if (gameplay == null)
                gameplay = gameObject.AddComponent<GameplayController>();
            startMenu.SetLaunchStatus("OPENING VERIFIED SIMULATION…");
            startMenu.enabled = false;
            try
            {
                gameplay.BeginSimulation(simulationContent, prepared);
                CurrentMode = ApplicationMode.SimulationViewer;
                ReleaseSimulationPreparation(owner);
            }
            catch (Exception exception)
            {
                gameplay.EndSession();
                startMenu.enabled = true;
                CurrentMode = ApplicationMode.Menu;
                FinishSimulationPreparationFailure(owner, exception);
            }
        }

        private void FinishSimulationPreparationFailure(
            CancellationTokenSource owner,
            Exception exception)
        {
            if (!ReferenceEquals(simulationPreparationCancellation, owner))
            {
                owner.Dispose();
                return;
            }
            startMenu.SetLaunchStatus(
                "SIMULATION UNAVAILABLE — " + exception.Message);
            Debug.LogException(exception, this);
            ReleaseSimulationPreparation(owner);
        }

        private void ReleaseSimulationPreparation(
            CancellationTokenSource owner)
        {
            if (ReferenceEquals(simulationPreparationCancellation, owner))
            {
                simulationPreparationCancellation = null;
                gameplayStartRoutine = null;
            }
            owner.Dispose();
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
            CancelCloudNavigation();
            CancelPendingGameplayStart();
            editorTestActive = false;
            levelEditor?.EndSession();
            gameplay?.EndSession();
            characterEditor?.EndSession();
            startMenu.enabled = true;
            startMenu.SetLaunchStatus(string.Empty);
            CurrentMode = ApplicationMode.Menu;
            ActiveCloudDraft = null;
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
            if (CurrentMode == ApplicationMode.SimulationViewer)
            {
                if (GUI.Button(
                    new Rect(Screen.width - 180f, 18f, 162f, 36f),
                    "RETURN TO MENU"))
                {
                    ReturnToMenu();
                }
                return;
            }

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

        private void EnsureDraftLibrary()
        {
            if (draftLibrary != null || supabase?.DraftLibrary == null) return;
            draftLibrary = new LevelDraftLibraryCoordinator(supabase.DraftLibrary);
            _ = draftLibrary.RefreshAsync();
        }

        private void CancelPendingGameplayStart()
        {
            if (simulationPreparationCancellation != null)
            {
                simulationPreparationCancellation.Cancel();
                simulationPreparationCancellation = null;
                gameplayStartRoutine = null;
                startMenu?.SetLaunchStatus(string.Empty);
                return;
            }
            if (gameplayStartRoutine == null)
            {
                return;
            }

            StopCoroutine(gameplayStartRoutine);
            gameplayStartRoutine = null;
        }

        private void CancelCloudNavigation() => cloudNavigation?.Cancel();

        private bool IsGameplaySessionActive =>
            CurrentMode == ApplicationMode.Gameplay
            || CurrentMode == ApplicationMode.SimulationViewer;

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
            draftLibrary?.Dispose();
            draftLibrary = null;
            cloudNavigation?.Dispose();
            cloudNavigation = null;
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
