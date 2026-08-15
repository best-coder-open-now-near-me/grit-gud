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
        private LevelDressingCatalog dressingCatalog;
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
        private LevelEditorGridSettings gridSettings;
        private LevelEditorGridPresenter gridPresenter;
        private ILevelEditorPreferencesStore preferencesStore;
        private LevelEditorSceneQuery sceneQuery;
        private LevelEditorToolManager toolManager;
        private PlacementLevelEditorTool placementTool;
        private SpatialRecordPlacementTool spatialPlacementTool;
        private TerrainHeightLevelEditorTool terrainTool;
        private LevelEditorGui gui;
        private LevelEditorPresentationState presentationState;
        private LevelDocument viewDocument;
        private ScenarioAuthoringCoordinator scenarioAuthoring;
        private TerrainAuthoringCoordinator terrainAuthoring;
        private EnvironmentAuthoringCoordinator environmentAuthoring;
        private LevelEditorLayoutCoordinator layoutAuthoring;
        private LevelEditorOrganizationModel organizationModel;
        private LevelEditorOrganizationCoordinator organizationAuthoring;
        private LevelEditorPlayabilityCoordinator playabilityAuthoring;
        private LevelDressingAuthoringCoordinator dressingAuthoring;
        private GameplayEnvironmentLighting environmentLighting;
        private LevelDressingProjector dressingProjector;
        private LevelEntityView selectedView;
        private LevelEditorOutlinePresenter outlinePresenter;
        private IReadOnlyList<LevelValidationIssue> validationIssues =
            Array.Empty<LevelValidationIssue>();
        private bool previewMode;
        private bool suspended;
        private bool audioZonePreviewEnabled;
        private LevelDocument sourceDocument;
        private bool sourceDocumentIsSaved;
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
            sourceDocumentIsSaved = initialDocumentIsSaved;
            sourceLabel = string.IsNullOrWhiteSpace(initialSourceLabel)
                ? initialDocument.displayName
                : initialSourceLabel.Trim();
            GameplayContentPackage defaultContent = GameplayContentLoader.LoadDefault();
            catalog = defaultContent.Archetypes;
            scenarioCatalog = ScenarioAuthoringCatalog.Create(defaultContent.Scenario);
            dressingCatalog = LevelDressingCatalog.LoadDefault();
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
            dressingProjector = new LevelDressingProjector(transform, dressingCatalog);
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
            gridSettings = new LevelEditorGridSettings();
            float restoredGridSpacing = preferences.gridSpacing > 0f
                ? preferences.gridSpacing
                : 2.5f;
            gridSettings.Configure(
                preferences.gridVisible,
                restoredGridSpacing,
                preferences.gridElevation);
            gridPresenter = new LevelEditorGridPresenter(transform);
            gridSettings.Changed += HandleGridSettingsChanged;
            organizationModel = new LevelEditorOrganizationModel(catalog);
            organizationModel.Synchronize(viewDocument);
            organizationModel.Changed += HandleOrganizationViewChanged;
            var toolContext = new LevelEditorToolContext(
                workspace,
                selection,
                projector,
                terrainProjector,
                sceneQuery,
                snapSettings,
                SetStatus,
                SyncInspectorFields,
                organizationModel);
            toolManager = new LevelEditorToolManager(toolContext, SelectionLevelEditorTool.ToolId);
            placementTool = new PlacementLevelEditorTool();
            spatialPlacementTool = new SpatialRecordPlacementTool(PlaceSpatialRecord);
            terrainTool = new TerrainHeightLevelEditorTool();
            var selectionTool = new SelectionLevelEditorTool();
            toolManager.Register(selectionTool);
            toolManager.Register(placementTool);
            toolManager.Register(spatialPlacementTool);
            toolManager.Register(terrainTool);
            toolManager.ActivateDefault();
            terrainAuthoring = new TerrainAuthoringCoordinator(workspace);
            terrainAuthoring.StatusChanged += SetStatus;
            playabilityAuthoring = new LevelEditorPlayabilityCoordinator(
                workspace,
                terrainProjector);
            playabilityAuthoring.StatusChanged += SetStatus;
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
            environmentAuthoring = new EnvironmentAuthoringCoordinator(
                workspace,
                cameraController.CaptureState);
            environmentAuthoring.StatusChanged += SetStatus;
            environmentAuthoring.PracticalLightFocusRequested +=
                HandlePracticalLightFocusRequested;
            dressingAuthoring = new LevelDressingAuthoringCoordinator(
                workspace,
                cameraController.CaptureState);
            dressingAuthoring.StatusChanged += SetStatus;
            dressingAuthoring.FocusRequested += HandleDressingFocusRequested;
            layoutAuthoring = new LevelEditorLayoutCoordinator(
                workspace,
                selection,
                cameraController,
                gridSettings);
            layoutAuthoring.StatusChanged += SetStatus;
            organizationAuthoring = new LevelEditorOrganizationCoordinator(
                workspace,
                selection,
                organizationModel);
            organizationAuthoring.StatusChanged += SetStatus;
            organizationAuthoring.GroupFocusRequested += HandleGroupFocusRequested;
            gui = new LevelEditorGui(
                selection,
                catalog,
                scenarioCatalog,
                dressingCatalog,
                toolManager,
                placementTool,
                terrainPanel,
                selectionTool,
                snapSettings,
                presentationState,
                this);
            gui.SyncScenarioFields(viewDocument, forceLevelIdentity: true);
            gui.SyncEnvironmentFields(viewDocument, force: true);
            gui.SyncDressingFields(viewDocument, force: true);
            gui.SyncLayoutFields(viewDocument, gridSettings, force: true);
            gui.SyncOrganizationFields(viewDocument, force: true);
            outlinePresenter = new LevelEditorOutlinePresenter(transform);
            validationIssues = workspace.ValidationIssues;
            gridPresenter.Refresh(viewDocument.bounds, gridSettings);

            if (startInPreview)
            {
                EnterPreview();
            }
            else
            {
                projector.Replace(workspace.CreateSnapshot());
                terrainProjector.Replace(workspace.CreateSnapshot());
                dressingProjector.Replace(
                    viewDocument.dressing,
                    showZoneGizmos: true,
                    playAudio: audioZonePreviewEnabled);
                scenarioActorHandles.Refresh(workspace.CreateSnapshot());
                organizationModel.ApplyProjection(projector);
                SetStatus("Edit the main level or choose New to start from an empty level.");
            }

            RefreshEnvironmentLighting(viewDocument);
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
            if (playabilityAuthoring != null)
                playabilityAuthoring.StatusChanged -= SetStatus;
            if (environmentAuthoring != null)
            {
                environmentAuthoring.StatusChanged -= SetStatus;
                environmentAuthoring.PracticalLightFocusRequested -=
                    HandlePracticalLightFocusRequested;
            }
            if (dressingAuthoring != null)
            {
                dressingAuthoring.StatusChanged -= SetStatus;
                dressingAuthoring.FocusRequested -= HandleDressingFocusRequested;
            }
            if (layoutAuthoring != null)
                layoutAuthoring.StatusChanged -= SetStatus;
            if (organizationAuthoring != null)
            {
                organizationAuthoring.StatusChanged -= SetStatus;
                organizationAuthoring.GroupFocusRequested -= HandleGroupFocusRequested;
            }
            if (organizationModel != null)
                organizationModel.Changed -= HandleOrganizationViewChanged;
            if (gridSettings != null)
                gridSettings.Changed -= HandleGridSettingsChanged;
            environmentLighting?.Dispose();
            toolManager?.Dispose();
            projector?.Dispose();
            terrainProjector?.Dispose();
            dressingProjector?.Dispose();
            interactionPointHandles?.Dispose();
            scenarioActorHandles?.Dispose();
            gridPresenter?.Dispose();
            outlinePresenter?.Dispose();
            workspace?.Dispose();
            toolManager = null;
            projector = null;
            terrainProjector = null;
            dressingProjector = null;
            interactionPointHandles = null;
            scenarioActorHandles = null;
            workspace = null;
            selection = null;
            persistence = null;
            placementTool = null;
            terrainTool = null;
            terrainAuthoring = null;
            playabilityAuthoring = null;
            environmentAuthoring = null;
            dressingAuthoring = null;
            layoutAuthoring = null;
            organizationAuthoring = null;
            organizationModel = null;
            environmentLighting = null;
            gridPresenter = null;
            gridSettings = null;
            catalog = null;
            scenarioCatalog = null;
            dressingCatalog = null;
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
            audioZonePreviewEnabled = false;
            viewDocument = null;
            validationIssues = Array.Empty<LevelValidationIssue>();
            sourceDocument = null;
            sourceDocumentIsSaved = false;
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
            else if (tool.Id == SpatialRecordPlacementTool.ToolId)
                presentationState.SynchronizeCreateMode(LevelEditorCreateMode.Place);
            else if (tool.Id == TerrainHeightLevelEditorTool.ToolId)
                presentationState.SynchronizeCreateMode(LevelEditorCreateMode.Terrain);
            else
                presentationState.SynchronizeCreateMode(LevelEditorCreateMode.Select);
        }

        private void PlaceSpatialRecord(LevelSpatialPlacementKind kind, Vector3 position)
        {
            switch (kind)
            {
                case LevelSpatialPlacementKind.PracticalLight:
                    environmentAuthoring.AddPracticalLightAt(position);
                    break;
                case LevelSpatialPlacementKind.AmbientVfx:
                    dressingAuthoring.AddAmbientVfxAt(position);
                    break;
                case LevelSpatialPlacementKind.AudioZone:
                    dressingAuthoring.AddAudioZoneAt(position);
                    break;
                default:
                    dressingAuthoring.AddDecalAt(position);
                    break;
            }
        }

        private void Update()
        {
            if (!suspended && workspace != null && persistence != null)
            {
                persistence.TickAutosave(workspace, Time.unscaledTimeAsDouble);
            }

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
                gridVisible = gridSettings?.Visible ?? true,
                gridSpacing = gridSettings?.Spacing ?? 2.5f,
                gridElevation = gridSettings?.Elevation ?? 0f,
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
            dressingProjector.SetVisible(false);
            scenarioActorHandles.SetVisible(false);
            gridPresenter.SetVisible(false);
            outlinePresenter.HideAll();
            environmentLighting?.Dispose();
            environmentLighting = null;
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
            dressingProjector.SetVisible(true);
            dressingProjector.SetEditorPresentation(
                showZoneGizmos: !previewMode,
                playAudio: previewMode || audioZonePreviewEnabled);
            scenarioActorHandles.SetVisible(!previewMode);
            gridPresenter.Refresh(workspace.CreateSnapshot().bounds, gridSettings);
            gridPresenter.SetVisible(!previewMode && gridSettings.Visible);
            if (!previewMode)
                organizationModel.ApplyProjection(projector);
            playabilityAuthoring.SetAuthoringProjectionVisible(!previewMode);
            RefreshEnvironmentLighting(workspace.CreateSnapshot());
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
                if (!organizationModel.CanSelect(entityId))
                {
                    SetStatus(
                        "That entity is hidden, locked, or excluded by the selection filter.");
                    return;
                }
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

        private void SetEntityRotationPivot(float normalizedX, float normalizedZ)
        {
            var commands = new List<ILevelEditCommand>();
            foreach (string entityId in selection.Targets
                .Select(target => target.EntityId)
                .Distinct(StringComparer.Ordinal))
            {
                LevelEntity entity = workspace.FindEntitySnapshot(entityId);
                if (entity == null
                    || !projector.TryGetEntity(entityId, out LevelEntityView view))
                {
                    continue;
                }

                Bounds bounds = LevelEntityView.CalculateVisualLocalBounds(
                    view.Archetype.Presentation.Prefab,
                    view.Archetype.Presentation.LocalBounds);
                Vector3 pivot = LevelEntityView.CalculateBoundsPivot(
                    bounds,
                    Mathf.Clamp(normalizedX, -1f, 1f),
                    Mathf.Clamp(normalizedZ, -1f, 1f));
                var after = new LevelRotationPivotData
                {
                    mode = "bounds",
                    localPosition = new Float3Data(pivot.x, pivot.y, pivot.z),
                };
                commands.Add(new SetEntityRotationPivotCommand(
                    entity.id,
                    entity.rotationPivot,
                    after));
            }

            if (commands.Count == 0)
                return;

            workspace.Execute(commands.Count == 1
                ? commands[0]
                : new CompositeLevelEditCommand("Set entity rotation pivots", commands));
            SetStatus(commands.Count == 1
                ? "Set rotation pivot."
                : "Set rotation pivots for selected entities.");
        }

        private void ResetEntityRotationPivot()
        {
            var commands = new List<ILevelEditCommand>();
            foreach (string entityId in selection.Targets
                .Select(target => target.EntityId)
                .Distinct(StringComparer.Ordinal))
            {
                LevelEntity entity = workspace.FindEntitySnapshot(entityId);
                if (entity?.rotationPivot != null)
                {
                    commands.Add(new SetEntityRotationPivotCommand(
                        entity.id,
                        entity.rotationPivot,
                        null));
                }
            }

            if (commands.Count == 0)
                return;

            workspace.Execute(commands.Count == 1
                ? commands[0]
                : new CompositeLevelEditCommand("Reset entity rotation pivots", commands));
            SetStatus("Restored asset rotation pivot.");
        }

        private void ApplyLevelDisplayName(string displayName)
        {
            string normalized = displayName?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalized))
            {
                SetStatus("The level display name cannot be empty.");
                return;
            }

            LevelDocument snapshot = workspace.CreateSnapshot();
            if (string.Equals(snapshot.displayName, normalized, StringComparison.Ordinal))
            {
                SetStatus("The level already has that display name.");
                return;
            }

            workspace.Execute(new SetLevelDisplayNameCommand(snapshot.displayName, normalized));
            SetStatus($"Renamed the level to '{normalized}'.");
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
            playabilityAuthoring.SetAuthoringProjectionVisible(false);
            outlinePresenter.HideAll();
            projector.Replace(workspace.CreateSnapshot());
            terrainProjector.Replace(workspace.CreateSnapshot());
            dressingProjector.Replace(
                workspace.CreateSnapshot().dressing,
                showZoneGizmos: false,
                playAudio: true);
            interactionPointHandles.Refresh(workspace.CreateSnapshot(), selection, projector);
            scenarioActorHandles.SetVisible(false);
            gridPresenter.SetVisible(false);
            SetStatus("Level Preview uses an isolated snapshot; authored data is locked.");
        }

        private void ExitPreview()
        {
            previewMode = false;
            projector.Replace(workspace.CreateSnapshot());
            terrainProjector.Replace(workspace.CreateSnapshot());
            dressingProjector.Replace(
                workspace.CreateSnapshot().dressing,
                showZoneGizmos: true,
                playAudio: audioZonePreviewEnabled);
            interactionPointHandles.Refresh(workspace.CreateSnapshot(), selection, projector);
            scenarioActorHandles.Refresh(workspace.CreateSnapshot());
            scenarioActorHandles.SetVisible(true);
            gridPresenter.Refresh(workspace.CreateSnapshot().bounds, gridSettings);
            organizationModel.Synchronize(workspace.CreateSnapshot());
            organizationModel.ApplyProjection(projector);
            playabilityAuthoring.SetAuthoringProjectionVisible(true);
            SetStatus("Returned to the authored level.");
        }

        private void HandleWorkspaceChanged(
            object sender,
            LevelEditorWorkspaceChangedEventArgs args)
        {
            validationIssues = args.ValidationIssues;
            playabilityAuthoring?.MarkStale();
            persistence?.ScheduleAutosave(
                args.SessionChange.Revision,
                Time.unscaledTimeAsDouble);
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
                organizationModel.Synchronize(snapshot);
                organizationModel.ApplyProjection(projector);
                ReconcileSelection(snapshot);
                RefreshSelectedView();
                interactionPointHandles.Refresh(snapshot, selection, projector);
                scenarioActorHandles.Refresh(snapshot);
                gui.SyncScenarioFields(snapshot);
                if (args.SessionChange.RequiresFullProjection
                    || IsEnvironmentCommand(args.SessionChange.Command))
                {
                    gui.SyncEnvironmentFields(snapshot, force: true);
                    RefreshEnvironmentLighting(snapshot);
                }
                if (args.SessionChange.RequiresFullProjection
                    || IsDressingCommand(args.SessionChange.Command))
                {
                    gui.SyncDressingFields(snapshot, force: true);
                    dressingProjector.Replace(
                        snapshot.dressing,
                        showZoneGizmos: true,
                        playAudio: audioZonePreviewEnabled);
                }
                if (args.SessionChange.RequiresFullProjection
                    || IsBoundsCommand(args.SessionChange.Command))
                {
                    gui.SyncLayoutFields(snapshot, gridSettings, force: true);
                    gridPresenter.Refresh(snapshot.bounds, gridSettings);
                }
                if (args.SessionChange.RequiresFullProjection
                    || IsOrganizationCommand(args.SessionChange.Command))
                {
                    gui.SyncOrganizationFields(snapshot, force: true);
                }
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
            sourceDocumentIsSaved = false;
            sourceLabel = "new level";
            ReplaceWorkspaceDocument(sourceDocument.DeepCopy(), isSaved: false);
            SetStatus("Created a new level with flat terrain covering its bounds.");
        }

        private void ReloadSourceLevel()
        {
            ReplaceWorkspaceDocument(sourceDocument.DeepCopy(), sourceDocumentIsSaved);
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
            gui.SyncScenarioFields(document, forceLevelIdentity: true);
            gui.SyncEnvironmentFields(document, force: true);
            gui.SyncDressingFields(document, force: true);
            gui.SyncLayoutFields(document, gridSettings, force: true);
            gui.SyncOrganizationFields(document, force: true);
        }

        private void RefreshEnvironmentLighting(LevelDocument document)
        {
            if (document?.environment == null || suspended)
                return;

            environmentLighting?.Dispose();
            environmentLighting = GameplayEnvironmentLighting.Create(
                transform,
                document.environment);
        }

        private void HandleDressingFocusRequested(
            LevelDressingTargetKind kind,
            string id) =>
            gui?.SelectDressingItem(kind, id, workspace?.CreateSnapshot());

        private void HandlePracticalLightFocusRequested(string lightId)
        {
            gui?.SelectPracticalLight(lightId, workspace?.CreateSnapshot());
        }

        private static bool IsEnvironmentCommand(ILevelEditCommand command)
        {
            if (command is ILevelEnvironmentEditCommand)
                return true;
            if (command is ILevelEditCommandGroup group)
                return group.Commands.Any(IsEnvironmentCommand);
            return false;
        }

        private static bool IsBoundsCommand(ILevelEditCommand command)
        {
            if (command is ILevelBoundsEditCommand)
                return true;
            if (command is ILevelEditCommandGroup group)
                return group.Commands.Any(IsBoundsCommand);
            return false;
        }

        private static bool IsOrganizationCommand(ILevelEditCommand command)
        {
            if (command is ILevelOrganizationEditCommand)
                return true;
            if (command is ILevelEditCommandGroup group)
                return group.Commands.Any(IsOrganizationCommand);
            return false;
        }

        private static bool IsDressingCommand(ILevelEditCommand command)
        {
            if (command is ILevelDressingEditCommand)
                return true;
            if (command is ILevelEditCommandGroup group)
                return group.Commands.Any(IsDressingCommand);
            return false;
        }

        private void HandleGridSettingsChanged()
        {
            if (workspace == null || gridPresenter == null || previewMode || suspended)
                return;
            gridPresenter.Refresh(workspace.CreateSnapshot().bounds, gridSettings);
            gui?.SyncLayoutFields(workspace.CreateSnapshot(), gridSettings, force: true);
        }

        private void ReconcileSelection(LevelDocument document)
        {
            if (selection == null || document == null || selection.Targets.Count == 0)
                return;
            LevelSelectionTarget[] retained = selection.Targets.Where(target =>
            {
                LevelEntity entity = document.entities.FirstOrDefault(candidate => string.Equals(
                    candidate?.id,
                    target.EntityId,
                    StringComparison.Ordinal));
                if (entity == null)
                    return false;
                return (organizationModel == null || organizationModel.CanSelect(entity.id))
                    && (target.Kind != LevelSelectionKind.InteractionPoint
                    || entity.interactionPoints.Any(point => string.Equals(
                        point?.id,
                        target.ElementId,
                        StringComparison.Ordinal)));
            }).ToArray();
            selection.Set(retained);
        }

        private void HandleOrganizationViewChanged()
        {
            if (workspace == null || organizationModel == null || previewMode || suspended)
                return;
            LevelDocument snapshot = workspace.CreateSnapshot();
            organizationModel.ApplyProjection(projector);
            ReconcileSelection(snapshot);
        }

        private void HandleGroupFocusRequested(string groupId)
        {
            gui?.SelectEntityGroup(groupId, workspace?.CreateSnapshot());
        }

        private void HandleDocumentLoaded(object sender, LevelDocumentLoadedEventArgs args)
        {
            sourceDocument = args.Document.DeepCopy();
            sourceDocumentIsSaved = args.IsSaved;
            sourceLabel = args.SourceLabel;
            ReplaceWorkspaceDocument(sourceDocument.DeepCopy(), args.IsSaved);
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
            else if (!previewMode
                && toolManager.ActiveTool == spatialPlacementTool
                && spatialPlacementTool.HasPreview)
            {
                outlinePresenter.PresentPlacement(spatialPlacementTool.GetPreviewBounds());
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
                && organizationModel.CanSelect(view.EntityId)
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

        int ILevelEditorGuiActions.RecoveryGenerationCount =>
            LevelEditorPersistenceCoordinator.RecoveryGenerationCount;

        bool ILevelEditorGuiActions.HasRecovery(int generation) =>
            persistence.HasRecovery(generation);

        bool ILevelEditorGuiActions.UsesBrowserFileDialog => persistence.UsesBrowserFileDialog;

        string ILevelEditorGuiActions.DesktopImportPath
        {
            get => persistence.DesktopImportPath;
            set => persistence.DesktopImportPath = value;
        }

        LevelEditorCameraView ILevelEditorGuiActions.CameraView => cameraController.View;

        string ILevelEditorGuiActions.IsolatedGroupId => organizationModel.IsolatedGroupId;

        string ILevelEditorGuiActions.SelectionCategoryFilter =>
            organizationModel.CategoryFilter;

        string ILevelEditorGuiActions.SelectionGroupFilter => organizationModel.GroupFilter;

        LevelPlayabilityReport ILevelEditorGuiActions.PlayabilityReport =>
            playabilityAuthoring.Report;

        bool ILevelEditorGuiActions.PlayabilityReportIsStale =>
            playabilityAuthoring.IsStale;

        bool ILevelEditorGuiActions.SlopeOverlayEnabled =>
            playabilityAuthoring.SlopeOverlayEnabled;

        bool ILevelEditorGuiActions.AudioZonePreviewEnabled =>
            audioZonePreviewEnabled;

        void ILevelEditorGuiActions.Undo() => workspace.Undo();

        void ILevelEditorGuiActions.Redo() => workspace.Redo();

        void ILevelEditorGuiActions.SaveDraft() => persistence.SaveDraft(workspace);

        void ILevelEditorGuiActions.LoadDraft() => persistence.LoadDraft();

        void ILevelEditorGuiActions.LoadRecovery(int generation) =>
            persistence.LoadRecovery(generation);

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

        void ILevelEditorGuiActions.ApplyLevelDisplayName(string displayName) =>
            ApplyLevelDisplayName(displayName);

        void ILevelEditorGuiActions.ApplyLevelBounds(LevelBoundsAuthoringRequest request) =>
            layoutAuthoring.ApplyBounds(request);

        void ILevelEditorGuiActions.ConfigureGrid(LevelGridAuthoringRequest request) =>
            layoutAuthoring.ConfigureGrid(request);

        void ILevelEditorGuiActions.SetCameraView(LevelEditorCameraView view) =>
            layoutAuthoring.SetCameraView(view);

        void ILevelEditorGuiActions.DuplicateArray(LevelArrayAuthoringRequest request) =>
            layoutAuthoring.DuplicateArray(request);

        void ILevelEditorGuiActions.CreateEntityGroup(string displayName) =>
            organizationAuthoring.CreateGroup(displayName);

        void ILevelEditorGuiActions.RenameEntityGroup(string groupId, string displayName) =>
            organizationAuthoring.RenameGroup(groupId, displayName);

        void ILevelEditorGuiActions.SetEntityGroupLocked(string groupId, bool locked) =>
            organizationAuthoring.SetGroupLocked(groupId, locked);

        void ILevelEditorGuiActions.SetEntityGroupHidden(string groupId, bool hidden) =>
            organizationAuthoring.SetGroupHidden(groupId, hidden);

        void ILevelEditorGuiActions.AssignSelectionToGroup(string groupId) =>
            organizationAuthoring.AssignSelection(groupId);

        void ILevelEditorGuiActions.DeleteEntityGroup(string groupId) =>
            organizationAuthoring.DeleteGroup(groupId);

        void ILevelEditorGuiActions.IsolateEntityGroup(string groupId) =>
            organizationAuthoring.IsolateGroup(groupId);

        void ILevelEditorGuiActions.SetSelectionCategoryFilter(string category) =>
            organizationAuthoring.SetCategoryFilter(category);

        void ILevelEditorGuiActions.SetSelectionGroupFilter(string groupId) =>
            organizationAuthoring.SetGroupFilter(groupId);

        void ILevelEditorGuiActions.SelectMatchingEntities() =>
            organizationAuthoring.SelectMatching();

        void ILevelEditorGuiActions.RunPlayabilityDiagnostics() =>
            playabilityAuthoring.Run();

        void ILevelEditorGuiActions.SetSlopeOverlayEnabled(bool enabled) =>
            playabilityAuthoring.SetSlopeOverlay(enabled);

        void ILevelEditorGuiActions.ApplyEnvironment(
            LevelEnvironmentAuthoringRequest request) =>
            environmentAuthoring.ApplyEnvironment(request);

        void ILevelEditorGuiActions.AddPracticalLight() =>
            environmentAuthoring.AddPracticalLight();

        void ILevelEditorGuiActions.QueueSpatialPlacement(LevelSpatialPlacementKind kind)
        {
            spatialPlacementTool.Queue(kind);
            toolManager.Activate(SpatialRecordPlacementTool.ToolId);
        }

        void ILevelEditorGuiActions.ApplyPracticalLight(
            LevelPracticalLightAuthoringRequest request) =>
            environmentAuthoring.ApplyPracticalLight(request);

        void ILevelEditorGuiActions.DeletePracticalLight(string lightId) =>
            environmentAuthoring.DeletePracticalLight(lightId);

        void ILevelEditorGuiActions.AddDecal() => dressingAuthoring.AddDecal();

        void ILevelEditorGuiActions.ApplyDecal(LevelDecalAuthoringRequest request) =>
            dressingAuthoring.ApplyDecal(request);

        void ILevelEditorGuiActions.DeleteDecal(string decalId) =>
            dressingAuthoring.DeleteDecal(decalId);

        void ILevelEditorGuiActions.AddAmbientVfx() =>
            dressingAuthoring.AddAmbientVfx();

        void ILevelEditorGuiActions.ApplyAmbientVfx(
            LevelAmbientVfxAuthoringRequest request) =>
            dressingAuthoring.ApplyAmbientVfx(request);

        void ILevelEditorGuiActions.DeleteAmbientVfx(string effectId) =>
            dressingAuthoring.DeleteAmbientVfx(effectId);

        void ILevelEditorGuiActions.AddAudioZone() => dressingAuthoring.AddAudioZone();

        void ILevelEditorGuiActions.ApplyAudioZone(LevelAudioZoneAuthoringRequest request) =>
            dressingAuthoring.ApplyAudioZone(request);

        void ILevelEditorGuiActions.DeleteAudioZone(string zoneId) =>
            dressingAuthoring.DeleteAudioZone(zoneId);

        void ILevelEditorGuiActions.SetAudioZonePreviewEnabled(bool enabled)
        {
            audioZonePreviewEnabled = enabled;
            dressingProjector?.SetEditorPresentation(
                showZoneGizmos: !previewMode,
                playAudio: previewMode || enabled);
            SetStatus(enabled
                ? "Ambient audio preview enabled."
                : "Ambient audio preview muted.");
        }

        void ILevelEditorGuiActions.ApplyEntityTransform(
            string x,
            string y,
            string z,
            string yaw) => ApplyInspectorTransform(x, y, z, yaw);

        void ILevelEditorGuiActions.SetEntityRotationPivot(float normalizedX, float normalizedZ) =>
            SetEntityRotationPivot(normalizedX, normalizedZ);

        void ILevelEditorGuiActions.ResetEntityRotationPivot() =>
            ResetEntityRotationPivot();

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
