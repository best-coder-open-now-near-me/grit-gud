using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GritGud.Application.Levels;
using GritGud.Domain.Levels;
using GritGud.Presentation.Bootstrap;
using GritGud.Presentation.Gameplay;
using GritGud.Presentation.LevelEditing.Core;
using GritGud.Presentation.LevelEditing.Persistence;
using GritGud.Presentation.LevelEditing.Tools;
using GritGud.Presentation.LevelEditing.UI;
using GritGud.Presentation.Levels;
using GritGud.Presentation.Levels.Persistence;
using GritGud.Presentation.Levels.Runtime;
using UnityEngine;

namespace GritGud.Presentation.LevelEditing
{
    public sealed class LevelEditorController : MonoBehaviour, ILevelEditorGuiActions
    {
        private LevelArchetypeCatalog catalog;
        private ScenarioAuthoringCatalog scenarioCatalog;
        private LevelEditorPersistenceCoordinator persistence;
        private LevelEditorWorkspace workspace;
        private LevelSelectionModel selection;
        private LevelWorldProjector projector;
        private TerrainWorldProjector terrainProjector;
        private InteractionPointHandleProjector interactionPointHandles;
        private ScenarioActorHandleProjector scenarioActorHandles;
        private LevelEditorInputRouter inputRouter;
        private LevelEditorCameraController cameraController;
        private LevelSnapSettings snapSettings;
        private ILevelEditorPreferencesStore preferencesStore;
        private LevelEditorSceneQuery sceneQuery;
        private LevelEditorToolManager toolManager;
        private PlacementLevelEditorTool placementTool;
        private TerrainHeightLevelEditorTool terrainTool;
        private LevelEditorGui gui;
        private LevelEditorPresentationState presentationState;
        private LevelDocument viewDocument;
        private ScenarioAuthoringCoordinator scenarioAuthoring;
        private TerrainAuthoringCoordinator terrainAuthoring;
        private LevelEntityView selectedView;
        private LevelEditorOutlinePresenter outlinePresenter;
        private IReadOnlyList<LevelValidationIssue> validationIssues =
            Array.Empty<LevelValidationIssue>();
        private bool previewMode;
        private bool suspended;
        private LevelDocument sourceDocument;
        private string sourceLabel = string.Empty;
        private string statusMessage = string.Empty;

        public void Begin(bool startInPreview)
        {
            CommittedLevelLibrary levels = UnityCommittedLevelLibrary.LoadDefault();
            CommittedLevelEntry entry = levels.Find(
                UnityCommittedLevelLibrary.DefaultResourceKey)
                ?? throw new InvalidOperationException(
                    "The default committed level was not found.");
            Begin(
                startInPreview,
                levels.OpenForEditing(entry.ResourceKey),
                entry.DisplayName);
        }

        public void Begin(
            bool startInPreview,
            LevelDocument initialDocument,
            string initialSourceLabel,
            bool initialDocumentIsSaved = true)
        {
            if (initialDocument == null)
            {
                throw new ArgumentNullException(nameof(initialDocument));
            }

            EndSession();
            try
            {
                InitializeSession(
                    startInPreview,
                    initialDocument,
                    initialSourceLabel,
                    initialDocumentIsSaved);
            }
            catch
            {
                EndSession();
                throw;
            }
        }

        private void InitializeSession(
            bool startInPreview,
            LevelDocument initialDocument,
            string initialSourceLabel,
            bool initialDocumentIsSaved)
        {
            previewMode = false;
            suspended = false;
            enabled = true;
            sourceDocument = initialDocument.DeepCopy();
            sourceLabel = string.IsNullOrWhiteSpace(initialSourceLabel)
                ? initialDocument.displayName
                : initialSourceLabel.Trim();
            GameplayContentPackage defaultContent = GameplayContentLoader.LoadDefault();
            catalog = defaultContent.Archetypes;
            scenarioCatalog = ScenarioAuthoringCatalog.Create(defaultContent.Scenario);
            LevelTextTransfer textTransfer = GetComponent<LevelTextTransfer>();
            if (textTransfer == null)
            {
                textTransfer = gameObject.AddComponent<LevelTextTransfer>();
            }

            persistence = new LevelEditorPersistenceCoordinator(
                new UnityLevelJsonSerializer(),
                new PlayerPrefsLevelDraftStore(),
                textTransfer,
                defaultContent.ValidationContent);
            persistence.DocumentLoaded += HandleDocumentLoaded;
            persistence.StatusChanged += SetStatus;
            Camera sceneCamera = Camera.main;
            if (sceneCamera == null)
            {
                throw new InvalidOperationException("The bootstrap scene needs a Main Camera.");
            }

            workspace = new LevelEditorWorkspace(
                sourceDocument.DeepCopy(),
                defaultContent.ValidationContent,
                initialDocumentIsSaved);
            viewDocument = workspace.CreateSnapshot();
            workspace.Changed += HandleWorkspaceChanged;
            selection = new LevelSelectionModel();
            selection.Changed += HandleSelectionChanged;
            projector = new LevelWorldProjector(catalog, transform);
            terrainProjector = new TerrainWorldProjector(transform);
            interactionPointHandles = new InteractionPointHandleProjector();
            scenarioActorHandles = new ScenarioActorHandleProjector(transform);
            inputRouter = new LevelEditorInputRouter();
            cameraController = new LevelEditorCameraController(sceneCamera);
            cameraController.Frame(workspace.CreateSnapshot().bounds);
            preferencesStore = new PlayerPrefsLevelEditorPreferences();
            LevelEditorLocalPreferences preferences = preferencesStore.Load();
            cameraController.RestoreState(preferences.camera);
            sceneQuery = new LevelEditorSceneQuery(sceneCamera);
            snapSettings = new LevelSnapSettings { Enabled = preferences.snapEnabled };
            var toolContext = new LevelEditorToolContext(
                workspace,
                selection,
                projector,
                terrainProjector,
                sceneQuery,
                snapSettings,
                SetStatus,
                SyncInspectorFields);
            toolManager = new LevelEditorToolManager(toolContext, SelectionLevelEditorTool.ToolId);
            placementTool = new PlacementLevelEditorTool();
            terrainTool = new TerrainHeightLevelEditorTool();
            var selectionTool = new SelectionLevelEditorTool();
            toolManager.Register(selectionTool);
            toolManager.Register(placementTool);
            toolManager.Register(terrainTool);
            toolManager.ActivateDefault();
            terrainAuthoring = new TerrainAuthoringCoordinator(workspace);
            terrainAuthoring.StatusChanged += SetStatus;
            var terrainPanel = new TerrainToolPanelModel(
                toolManager,
                terrainTool,
                terrainAuthoring,
                FrameTerrain);
            presentationState = new LevelEditorPresentationState();
            toolManager.ActiveToolChanged += HandleActiveToolChanged;
            scenarioAuthoring = new ScenarioAuthoringCoordinator(
                workspace,
                scenarioCatalog,
                cameraController.CaptureState);
            scenarioAuthoring.StatusChanged += SetStatus;
            scenarioAuthoring.ActorFocusRequested += HandleScenarioActorFocusRequested;
            scenarioAuthoring.ActorChanged += HandleScenarioActorChanged;
            scenarioAuthoring.PlayerStartChanged += HandlePlayerStartChanged;
            gui = new LevelEditorGui(
                selection,
                catalog,
                scenarioCatalog,
                toolManager,
                placementTool,
                terrainPanel,
                selectionTool,
                snapSettings,
                presentationState,
                this);
            gui.SyncScenarioFields(viewDocument);
            outlinePresenter = new LevelEditorOutlinePresenter(transform);
            validationIssues = workspace.ValidationIssues;

            if (startInPreview)
            {
                EnterPreview();
            }
            else
            {
                projector.Replace(workspace.CreateSnapshot());
                terrainProjector.Replace(workspace.CreateSnapshot());
                scenarioActorHandles.Refresh(workspace.CreateSnapshot());
                SetStatus("Edit the main level or choose New to start from an empty level.");
            }
        }

        public void EndSession()
        {
            SaveLocalPreferences();
            if (workspace != null)
            {
                workspace.Changed -= HandleWorkspaceChanged;
            }

            if (selection != null)
            {
                selection.Changed -= HandleSelectionChanged;
            }

            if (persistence != null)
            {
                persistence.DocumentLoaded -= HandleDocumentLoaded;
                persistence.StatusChanged -= SetStatus;
                persistence.Dispose();
            }

            if (toolManager != null)
                toolManager.ActiveToolChanged -= HandleActiveToolChanged;
            if (scenarioAuthoring != null)
            {
                scenarioAuthoring.StatusChanged -= SetStatus;
                scenarioAuthoring.ActorFocusRequested -= HandleScenarioActorFocusRequested;
                scenarioAuthoring.ActorChanged -= HandleScenarioActorChanged;
                scenarioAuthoring.PlayerStartChanged -= HandlePlayerStartChanged;
            }
            if (terrainAuthoring != null)
                terrainAuthoring.StatusChanged -= SetStatus;
            toolManager?.Dispose();
            projector?.Dispose();
            terrainProjector?.Dispose();
            interactionPointHandles?.Dispose();
            scenarioActorHandles?.Dispose();
            outlinePresenter?.Dispose();
            workspace?.Dispose();
            toolManager = null;
            projector = null;
            terrainProjector = null;
            interactionPointHandles = null;
            scenarioActorHandles = null;
            workspace = null;
            selection = null;
            persistence = null;
            placementTool = null;
            terrainTool = null;
            terrainAuthoring = null;
            catalog = null;
            scenarioCatalog = null;
            inputRouter = null;
            cameraController = null;
            presentationState = null;
            scenarioAuthoring = null;
            selectedView = null;
            outlinePresenter = null;
            sceneQuery = null;
            snapSettings = null;
            preferencesStore = null;
            gui = null;

            previewMode = false;
            suspended = false;
            viewDocument = null;
            validationIssues = Array.Empty<LevelValidationIssue>();
            sourceDocument = null;
            sourceLabel = string.Empty;
            statusMessage = string.Empty;
            enabled = false;
        }

        private void HandleScenarioActorFocusRequested(string actorId)
        {
            selection?.Clear();
            gui?.SelectScenarioActor(actorId);
            LevelScenarioActorData actor = viewDocument?.scenario?.actors.FirstOrDefault(candidate =>
                string.Equals(candidate?.id, actorId, StringComparison.Ordinal));
            if (actor != null)
                gui?.SyncScenarioActorFields(actor);
        }

        private void HandleScenarioActorChanged(LevelScenarioActorData actor)
        {
            gui?.SyncScenarioActorFields(actor);
        }

        private void HandlePlayerStartChanged(LevelTransformData transformData)
        {
            gui?.SyncPlayerStartFields(transformData);
        }

        private void HandleActiveToolChanged(ILevelEditorTool tool)
        {
            if (tool == null || presentationState == null)
                return;
            if (tool.Id == PlacementLevelEditorTool.ToolId)
                presentationState.SynchronizeCreateMode(LevelEditorCreateMode.Place);
            else if (tool.Id == TerrainHeightLevelEditorTool.ToolId)
                presentationState.SynchronizeCreateMode(LevelEditorCreateMode.Terrain);
            else
                presentationState.SynchronizeCreateMode(LevelEditorCreateMode.Select);
        }

        private void Update()
        {
            if (suspended || workspace == null || cameraController == null)
            {
                return;
            }

            Vector2 pointerPosition = inputRouter.PointerPosition;
            LevelEditorInputState input = inputRouter.Capture(
                gui.IsPointerOverInterface(pointerPosition),
                GUIUtility.keyboardControl != 0);
            cameraController.Tick(input);
            HandleGlobalShortcuts(input);
            if (!previewMode)
            {
                toolManager.Tick(input);
            }

            UpdateHover(pointerPosition, input.PointerBlocked);
            UpdateOutlines();
        }

        private void SaveLocalPreferences()
        {
            if (preferencesStore == null || cameraController == null || snapSettings == null)
            {
                return;
            }

            preferencesStore.Save(new LevelEditorLocalPreferences
            {
                snapEnabled = snapSettings.Enabled,
                camera = cameraController.CaptureState(),
            });
        }

        private void OnGUI()
        {
            if (!suspended && workspace != null && viewDocument != null)
            {
                gui?.Draw(new LevelEditorViewState(
                    viewDocument,
                    workspace.Revision,
                    workspace.CanUndo,
                    workspace.CanRedo,
                    workspace.IsDirty,
                    previewMode,
                    selectedView,
                    validationIssues,
                    statusMessage));
            }
        }

        public void SuspendForTestPlay()
        {
            if (workspace == null || suspended)
            {
                return;
            }

            toolManager.CancelActive();
            suspended = true;
            projector.SetVisible(false);
            terrainProjector.SetVisible(false);
            scenarioActorHandles.SetVisible(false);
            outlinePresenter.HideAll();
        }

        public void ResumeFromTestPlay()
        {
            if (!suspended || workspace == null)
            {
                return;
            }

            suspended = false;
            projector.SetVisible(true);
            terrainProjector.SetVisible(true);
            scenarioActorHandles.SetVisible(!previewMode);
            SetStatus("Returned from isolated test play.");
        }

        private void StartTestPlay()
        {
            LevelDocument snapshot = workspace.CreateSnapshot();
            if (LevelValidator.HasErrors(workspace.Validate(LevelValidationProfile.Publish)))
            {
                SetStatus("Fix publish validation errors before test play.");
                return;
            }

            GameBootstrap.Instance.PlayEditorTest(snapshot);
        }

        private void HandleGlobalShortcuts(LevelEditorInputState input)
        {
            if (previewMode)
            {
                return;
            }

            if (input.UndoPressed)
            {
                workspace.Undo();
            }
            else if (input.RedoPressed)
            {
                workspace.Redo();
            }

            if (input.CancelPressed)
            {
                toolManager.CancelActive();
            }

            if (input.FrameSelectionPressed)
            {
                FrameSelection();
            }
            else if (input.FrameLevelPressed)
            {
                FrameLevel();
            }
        }

        private void FrameSelection()
        {
            RefreshSelectedView();
            if (selectedView == null)
            {
                SetStatus("Select an entity before framing it.");
                return;
            }

            cameraController.Frame(selectedView.GetWorldBounds());
            SetStatus("Framed selected entity.");
        }

        private void FrameLevel()
        {
            cameraController.Frame(workspace.CreateSnapshot().bounds);
            SetStatus("Framed level bounds.");
        }

        private void FrameTerrain()
        {
            LevelDocument document = workspace.CreateSnapshot();
            Bounds? combined = null;
            foreach (TerrainSurfaceData surface in document.terrainSurfaces)
            {
                if (surface == null || surface.heightSamples.Count == 0)
                {
                    continue;
                }

                Bounds surfaceBounds = TerrainMeshBuilder.CalculateBounds(surface);
                if (combined.HasValue)
                {
                    Bounds value = combined.Value;
                    value.Encapsulate(surfaceBounds);
                    combined = value;
                }
                else
                {
                    combined = surfaceBounds;
                }
            }

            if (!combined.HasValue)
            {
                SetStatus("The level does not contain terrain to frame.");
                return;
            }

            cameraController.Frame(combined.Value);
            SetStatus("Framed terrain surfaces.");
        }

        private void FocusEntity(string entityId)
        {
            if (string.IsNullOrWhiteSpace(entityId))
            {
                SetStatus("The requested entity is not loaded.");
                return;
            }

            if (projector.TryGetEntity(entityId, out LevelEntityView view))
            {
                selection.SetSingle(entityId);
                cameraController.Frame(view.GetWorldBounds());
                SetStatus($"Focused entity '{entityId}'.");
                return;
            }

            LevelScenarioActorData actor = viewDocument?.scenario?.actors.FirstOrDefault(candidate =>
                string.Equals(candidate?.id, entityId, StringComparison.Ordinal));
            if (actor == null)
            {
                SetStatus("The requested entity is not loaded.");
                return;
            }

            selection.Clear();
            gui.SelectScenarioActor(actor.id);
            gui.SyncScenarioActorFields(actor);
            if (scenarioActorHandles.TryGetHandle(actor.id, out GameObject handle))
            {
                Collider collider = handle.GetComponent<Collider>();
                if (collider != null)
                    cameraController.Frame(collider.bounds);
            }
            SetStatus($"Focused scenario actor '{actor.id}'.");
        }

        private void ApplyInspectorTransform(
            string xText,
            string yText,
            string zText,
            string yawText)
        {
            LevelEntity entity = workspace.FindEntitySnapshot(selection.PrimaryEntityId);
            if (entity == null)
            {
                return;
            }

            if (!TryParse(xText, out float x)
                || !TryParse(yText, out float y)
                || !TryParse(zText, out float z)
                || !TryParse(yawText, out float yaw))
            {
                SetStatus("Transform values must be finite numbers.");
                return;
            }

            var after = new LevelTransformData(
                new Float3Data(x, y, z),
                NormalizeYaw(yaw));
            workspace.Execute(new SetEntityTransformCommand(entity.id, entity.transform, after));
            SetStatus("Applied numeric transform.");
        }

        private void ExecuteScenarioCommands(
            string description,
            IReadOnlyList<ILevelEditCommand> commands)
        {
            if (commands.Count == 1)
                workspace.Execute(commands[0]);
            else
                workspace.ExecuteTransaction(description, commands);
        }

        private void TogglePreview()
        {
            if (previewMode)
            {
                ExitPreview();
            }
            else
            {
                EnterPreview();
            }
        }

        private void EnterPreview()
        {
            placementTool.SelectArchetype(null);
            toolManager.ActivateDefault();
            selection.Clear();
            previewMode = true;
            outlinePresenter.HideAll();
            projector.Replace(workspace.CreateSnapshot());
            terrainProjector.Replace(workspace.CreateSnapshot());
            interactionPointHandles.Refresh(workspace.CreateSnapshot(), selection, projector);
            scenarioActorHandles.SetVisible(false);
            SetStatus("Level Preview uses an isolated snapshot; authored data is locked.");
        }

        private void ExitPreview()
        {
            previewMode = false;
            projector.Replace(workspace.CreateSnapshot());
            terrainProjector.Replace(workspace.CreateSnapshot());
            interactionPointHandles.Refresh(workspace.CreateSnapshot(), selection, projector);
            scenarioActorHandles.Refresh(workspace.CreateSnapshot());
            scenarioActorHandles.SetVisible(true);
            SetStatus("Returned to the authored level.");
        }

        private void HandleWorkspaceChanged(
            object sender,
            LevelEditorWorkspaceChangedEventArgs args)
        {
            validationIssues = args.ValidationIssues;
            if (previewMode)
            {
                return;
            }

            try
            {
                LevelDocument snapshot = workspace.CreateSnapshot();
                viewDocument = snapshot;
                projector.Apply(snapshot, args.SessionChange);
                terrainProjector.Apply(snapshot, args.SessionChange);
                RefreshSelectedView();
                interactionPointHandles.Refresh(snapshot, selection, projector);
                scenarioActorHandles.Refresh(snapshot);
                gui.SyncScenarioFields(snapshot);
            }
            catch (LevelLoadException exception)
            {
                SetStatus(exception.Message);
                Debug.LogException(exception);
            }
        }

        private void HandleSelectionChanged()
        {
            presentationState?.FocusWorldSelection(selection?.Primary);
            RefreshSelectedView();
            if (!previewMode && workspace != null)
            {
                interactionPointHandles.Refresh(workspace.CreateSnapshot(), selection, projector);
            }
        }

        private void AddInteractionPoint()
        {
            LevelEntity entity = workspace.FindEntitySnapshot(selection.PrimaryEntityId);
            if (entity == null)
            {
                return;
            }

            var point = new InteractionPointData
            {
                id = LevelDocumentFactory.NewStableId(),
                type = "objective",
                radius = 0.5f,
            };
            workspace.Execute(new AddInteractionPointCommand(entity.id, point));
            selection.Set(new[]
            {
                new LevelSelectionTarget(entity.id, LevelSelectionKind.InteractionPoint, point.id),
            });
            SetStatus("Added interaction point.");
        }

        private void ApplyInteractionPoint(
            string type,
            string xText,
            string yText,
            string zText,
            string radiusText)
        {
            LevelSelectionTarget? target = selection.Primary;
            if (target == null || target.Value.Kind != LevelSelectionKind.InteractionPoint)
            {
                return;
            }

            LevelEntity entity = workspace.FindEntitySnapshot(target.Value.EntityId);
            InteractionPointData before = entity?.interactionPoints.FirstOrDefault(point =>
                string.Equals(point?.id, target.Value.ElementId, StringComparison.Ordinal));
            if (before == null || !TryParse(xText, out float x) || !TryParse(yText, out float y)
                || !TryParse(zText, out float z) || !TryParse(radiusText, out float radius)
                || radius <= 0f)
            {
                SetStatus("Interaction values require a supported type, finite position, and positive radius.");
                return;
            }

            if (!string.Equals(type, "objective", StringComparison.Ordinal)
                && !string.Equals(type, "doorway", StringComparison.Ordinal))
            {
                SetStatus("Choose a supported interaction type.");
                return;
            }

            InteractionPointData after = before.DeepCopy();
            after.type = type;
            after.localPosition = new Float3Data(x, y, z);
            after.radius = radius;
            var commands = new List<ILevelEditCommand>
            {
                new SetInteractionPointCommand(entity.id, before.id, before, after),
            };
            if (!string.Equals(type, "objective", StringComparison.Ordinal))
            {
                LevelScenarioData scenarioBefore = workspace.CreateSnapshot().scenario;
                LevelScenarioData scenarioAfter = scenarioBefore.DeepCopy();
                int removed = scenarioAfter.objectives.RemoveAll(objective =>
                    string.Equals(objective?.entityId, entity.id, StringComparison.Ordinal)
                    && string.Equals(
                        objective?.interactionPointId,
                        before.id,
                        StringComparison.Ordinal));
                if (removed > 0)
                {
                    commands.Add(new SetScenarioConfigurationCommand(
                        "Remove incompatible objective link",
                        scenarioBefore,
                        scenarioAfter,
                        new[] { entity.id }));
                }
            }

            ExecuteScenarioCommands("Edit interaction point", commands);
            SetStatus("Updated interaction point.");
        }

        private void DeleteInteractionPoint()
        {
            LevelSelectionTarget? target = selection.Primary;
            if (target == null || target.Value.Kind != LevelSelectionKind.InteractionPoint)
            {
                return;
            }

            workspace.Execute(new DeleteInteractionPointCommand(
                target.Value.EntityId,
                target.Value.ElementId));
            selection.SetSingle(target.Value.EntityId);
            SetStatus("Deleted interaction point.");
        }

        private void ApplyDestructibleDefaults(string enabledText, string state, string integrityText)
        {
            LevelEntity entity = workspace.FindEntitySnapshot(selection.PrimaryEntityId);
            if (entity == null || !TryParse(integrityText, out float integrity) || integrity <= 0f)
            {
                SetStatus("Destructible integrity must be a positive finite number.");
                return;
            }

            bool enabled = string.Equals(enabledText, "true", StringComparison.OrdinalIgnoreCase);
            var after = new DestructibleInstanceData
            {
                enabled = enabled,
                initialState = state,
                integrity = integrity,
            };
            workspace.Execute(new SetDestructibleInstanceCommand(entity.id, entity.destructible, after));
            SetStatus("Updated destructible defaults.");
        }

        private void RefreshSelectedView()
        {
            selectedView = null;
            string entityId = selection?.PrimaryEntityId;
            if (!string.IsNullOrWhiteSpace(entityId))
            {
                projector?.TryGetEntity(entityId, out selectedView);
            }

            if (selectedView != null)
            {
                SyncInspectorFields(selectedView.ReadTransform());
            }
        }

        private void CreateNewLevel()
        {
            sourceDocument = LevelDocumentFactory.CreateNew();
            sourceLabel = "new level";
            ReplaceWorkspaceDocument(sourceDocument.DeepCopy(), isSaved: false);
            SetStatus("Created a new level with flat terrain covering its bounds.");
        }

        private void ReloadSourceLevel()
        {
            ReplaceWorkspaceDocument(sourceDocument.DeepCopy());
            SetStatus($"Reloaded {sourceLabel}.");
        }

        private void ReplaceWorkspaceDocument(LevelDocument document, bool isSaved = true)
        {
            placementTool.SelectArchetype(null);
            toolManager.ActivateDefault();
            selection.Clear();
            workspace.ReplaceDocument(document, isSaved);
            cameraController.Frame(document.bounds);
            scenarioActorHandles.Refresh(document);
            gui.SelectScenarioActor(null);
            gui.SyncScenarioFields(document);
        }

        private void HandleDocumentLoaded(object sender, LevelDocumentLoadedEventArgs args)
        {
            sourceDocument = args.Document.DeepCopy();
            sourceLabel = args.SourceLabel;
            ReplaceWorkspaceDocument(sourceDocument.DeepCopy());
            SetStatus($"Loaded level from {args.SourceLabel}.");
        }

        private void UpdateOutlines()
        {
            outlinePresenter.PresentSelection(
                selectedView,
                selection.Targets,
                projector,
                !previewMode);
            if (!previewMode
                && toolManager.ActiveTool == placementTool
                && placementTool.TryGetPreviewBounds(out Bounds previewBounds))
            {
                outlinePresenter.PresentPlacement(previewBounds);
            }
            else
            {
                outlinePresenter.PresentPlacement(null);
            }
        }

        private void UpdateHover(Vector2 pointerPosition, bool pointerBlocked)
        {
            if (outlinePresenter == null)
            {
                return;
            }

            bool canHover = !previewMode
                && !pointerBlocked
                && toolManager.ActiveTool?.Id == SelectionLevelEditorTool.ToolId;
            if (canHover
                && sceneQuery.TryPickEntity(pointerPosition, out LevelEntityView view, out _)
                && !selection.Targets.Any(target => string.Equals(
                    target.EntityId,
                    view.EntityId,
                    StringComparison.Ordinal)))
            {
                outlinePresenter.PresentHover(view);
                return;
            }

            outlinePresenter.PresentHover(null);
        }

        private void SyncInspectorFields(LevelTransformData value)
        {
            gui?.SyncTransformFields(value);
        }

        private void SetStatus(string message)
        {
            statusMessage = message ?? string.Empty;
        }

        void ILevelEditorGuiActions.ReturnToMenu() => GameBootstrap.Instance.ReturnToMenu();

        bool ILevelEditorGuiActions.HasDraft => persistence.HasDraft;

        bool ILevelEditorGuiActions.UsesBrowserFileDialog => persistence.UsesBrowserFileDialog;

        string ILevelEditorGuiActions.DesktopImportPath
        {
            get => persistence.DesktopImportPath;
            set => persistence.DesktopImportPath = value;
        }

        void ILevelEditorGuiActions.Undo() => workspace.Undo();

        void ILevelEditorGuiActions.Redo() => workspace.Redo();

        void ILevelEditorGuiActions.SaveDraft() => persistence.SaveDraft(workspace);

        void ILevelEditorGuiActions.LoadDraft() => persistence.LoadDraft();

        void ILevelEditorGuiActions.Export() => persistence.Export(workspace);

        void ILevelEditorGuiActions.RequestImport() => persistence.RequestImport();

        void ILevelEditorGuiActions.TogglePreview() => TogglePreview();

        void ILevelEditorGuiActions.StartTestPlay() => StartTestPlay();

        void ILevelEditorGuiActions.CreateNewLevel() => CreateNewLevel();

        void ILevelEditorGuiActions.ReloadSourceLevel() => ReloadSourceLevel();

        void ILevelEditorGuiActions.FrameSelection() => FrameSelection();

        void ILevelEditorGuiActions.FrameLevel() => FrameLevel();

        void ILevelEditorGuiActions.FocusEntity(string entityId) =>
            FocusEntity(entityId);

        void ILevelEditorGuiActions.ApplyEntityTransform(
            string x,
            string y,
            string z,
            string yaw) => ApplyInspectorTransform(x, y, z, yaw);

        void ILevelEditorGuiActions.ApplyPlayerStart(
            string x,
            string y,
            string z,
            string yaw) => scenarioAuthoring.ApplyPlayerStart(x, y, z, yaw);

        void ILevelEditorGuiActions.AddInteractionPoint() => AddInteractionPoint();

        void ILevelEditorGuiActions.ApplyInteractionPoint(
            string type,
            string x,
            string y,
            string z,
            string radius) => ApplyInteractionPoint(type, x, y, z, radius);

        void ILevelEditorGuiActions.DeleteInteractionPoint() => DeleteInteractionPoint();

        void ILevelEditorGuiActions.ApplyDestructibleDefaults(
            string enabled,
            string state,
            string integrity) => ApplyDestructibleDefaults(enabled, state, integrity);

        void ILevelEditorGuiActions.AddScenarioActor(string templateId) =>
            scenarioAuthoring.AddActor(templateId);

        void ILevelEditorGuiActions.ApplyScenarioActor(
            string actorId,
            string x,
            string y,
            string z,
            string yaw,
            bool playerControlled,
            bool initiallySelected,
            bool primaryTarget) => scenarioAuthoring.ApplyActor(
                actorId,
                x,
                y,
                z,
                yaw,
                playerControlled,
                initiallySelected,
                primaryTarget);

        void ILevelEditorGuiActions.DeleteScenarioActor(string actorId) =>
            scenarioAuthoring.DeleteActor(actorId);

        void ILevelEditorGuiActions.PlaceScenarioActorAtView(string actorId) =>
            scenarioAuthoring.PlaceActorAtView(actorId);

        void ILevelEditorGuiActions.ApplyScenarioProp(
            string entityId,
            bool enabled,
            string mass,
            string sizeClass,
            bool startsEncounter) => scenarioAuthoring.ApplyProp(
                entityId,
                enabled,
                mass,
                sizeClass,
                startsEncounter);

        void ILevelEditorGuiActions.ApplyScenarioObjective(
            string entityId,
            string pointId,
            bool enabled,
            string displayName,
            string activeText,
            string completedText,
            string actionPointCost,
            string movementOpportunityCost,
            string mobility) => scenarioAuthoring.ApplyObjective(
                entityId,
                pointId,
                enabled,
                displayName,
                activeText,
                completedText,
                actionPointCost,
                movementOpportunityCost,
                mobility);

        void ILevelEditorGuiActions.ApplyScenarioVehicle(
            string entityId,
            bool enabled,
            string maximumSpeed,
            string acceleration,
            string braking,
            string lowSpeedTurn,
            string highSpeedTurn,
            string baseRadius,
            string radiusFactor,
            string startingSpeed,
            string occupantActorId,
            bool startsEncounter) => scenarioAuthoring.ApplyVehicle(
                entityId,
                enabled,
                maximumSpeed,
                acceleration,
                braking,
                lowSpeedTurn,
                highSpeedTurn,
                baseRadius,
                radiusFactor,
                startingSpeed,
                occupantActorId,
                startsEncounter);

        private static bool TryParse(string text, out float value)
        {
            bool parsed = float.TryParse(
                text,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out value);
            return parsed && !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static float NormalizeYaw(float yaw)
        {
            return Mathf.Repeat(yaw + 180f, 360f) - 180f;
        }
    }
}
