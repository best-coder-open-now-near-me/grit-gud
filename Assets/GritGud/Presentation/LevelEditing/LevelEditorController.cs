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
using GritGud.Presentation.Characters;
using GritGud.Presentation.Persistence;
using GritGud.Presentation.Supabase;
using UnityEngine;

namespace GritGud.Presentation.LevelEditing
{
    public sealed class LevelEditorController : MonoBehaviour, ILevelEditorGuiActions
    {
        private LevelArchetypeCatalog catalog;
        private ScenarioAuthoringCatalog scenarioCatalog;
        private LevelDressingCatalog dressingCatalog;
        private UnityCharacterLibrary characterLibrary;
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
        private LevelEditorPhysicsPlacementCoordinator physicsPlacement;
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
        private bool sessionReady;
        private bool cloudOperationRunning;
        private int sessionGeneration;
        private LevelEditorSessionLifecycle sessionLifecycle;

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
            sessionLifecycle = new LevelEditorSessionLifecycle();
            previewMode = false;
            suspended = false;
            sessionReady = false;
            sourceDocument = initialDocument.DeepCopy();
            sourceDocumentIsSaved = initialDocumentIsSaved;
            sourceLabel = string.IsNullOrWhiteSpace(initialSourceLabel)
                ? initialDocument.displayName
                : initialSourceLabel.Trim();
            GameplayContentPackage defaultContent = GameplayContentLoader.LoadDefault();
            catalog = defaultContent.Archetypes;
            scenarioCatalog = ScenarioAuthoringCatalog.Create(defaultContent.Scenario);
            dressingCatalog = LevelDressingCatalog.LoadDefault();
            characterLibrary = defaultContent.Characters;
            TextFileImportReceiver textTransfer = GetComponent<TextFileImportReceiver>();
            if (textTransfer == null)
            {
                textTransfer = gameObject.AddComponent<TextFileImportReceiver>();
            }

            persistence = new LevelEditorPersistenceCoordinator(
                new UnityLevelJsonSerializer(),
                new PlayerPrefsLevelDraftStore(),
                textTransfer,
                defaultContent.ValidationContent);
            sessionLifecycle.Subscribe(
                () => persistence.DocumentLoaded += HandleDocumentLoaded,
                () => persistence.DocumentLoaded -= HandleDocumentLoaded);
            sessionLifecycle.Subscribe(
                () => persistence.StatusChanged += SetStatus,
                () => persistence.StatusChanged -= SetStatus);
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
            sessionLifecycle.Subscribe(
                () => workspace.Changed += HandleWorkspaceChanged,
                () => workspace.Changed -= HandleWorkspaceChanged);
            selection = new LevelSelectionModel();
            sessionLifecycle.Subscribe(
                () => selection.Changed += HandleSelectionChanged,
                () => selection.Changed -= HandleSelectionChanged);
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
            sessionLifecycle.Subscribe(
                () => gridSettings.Changed += HandleGridSettingsChanged,
                () => gridSettings.Changed -= HandleGridSettingsChanged);
            organizationModel = new LevelEditorOrganizationModel(catalog);
            organizationModel.Synchronize(viewDocument);
            sessionLifecycle.Subscribe(
                () => organizationModel.Changed +=
                    HandleOrganizationViewChanged,
                () => organizationModel.Changed -=
                    HandleOrganizationViewChanged);
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
            physicsPlacement = new LevelEditorPhysicsPlacementCoordinator(
                this,
                workspace,
                selection,
                projector,
                toolManager.ActivateDefault,
                SetStatus,
                SyncInspectorFields);
            terrainAuthoring = new TerrainAuthoringCoordinator(workspace);
            sessionLifecycle.Subscribe(
                () => terrainAuthoring.StatusChanged += SetStatus,
                () => terrainAuthoring.StatusChanged -= SetStatus);
            playabilityAuthoring = new LevelEditorPlayabilityCoordinator(
                workspace,
                terrainProjector);
            sessionLifecycle.Subscribe(
                () => playabilityAuthoring.StatusChanged += SetStatus,
                () => playabilityAuthoring.StatusChanged -= SetStatus);
            var terrainPanel = new TerrainToolPanelModel(
                toolManager,
                terrainTool,
                terrainAuthoring,
                FrameTerrain);
            presentationState = new LevelEditorPresentationState();
            sessionLifecycle.Subscribe(
                () => toolManager.ActiveToolChanged += HandleActiveToolChanged,
                () => toolManager.ActiveToolChanged -= HandleActiveToolChanged);
            scenarioAuthoring = new ScenarioAuthoringCoordinator(
                workspace,
                scenarioCatalog,
                cameraController.CaptureState);
            sessionLifecycle.Subscribe(
                () => scenarioAuthoring.StatusChanged += SetStatus,
                () => scenarioAuthoring.StatusChanged -= SetStatus);
            sessionLifecycle.Subscribe(
                () => scenarioAuthoring.ActorFocusRequested +=
                    HandleScenarioActorFocusRequested,
                () => scenarioAuthoring.ActorFocusRequested -=
                    HandleScenarioActorFocusRequested);
            sessionLifecycle.Subscribe(
                () => scenarioAuthoring.ActorChanged +=
                    HandleScenarioActorChanged,
                () => scenarioAuthoring.ActorChanged -=
                    HandleScenarioActorChanged);
            sessionLifecycle.Subscribe(
                () => scenarioAuthoring.PlayerStartChanged +=
                    HandlePlayerStartChanged,
                () => scenarioAuthoring.PlayerStartChanged -=
                    HandlePlayerStartChanged);
            environmentAuthoring = new EnvironmentAuthoringCoordinator(
                workspace,
                cameraController.CaptureState);
            sessionLifecycle.Subscribe(
                () => environmentAuthoring.StatusChanged += SetStatus,
                () => environmentAuthoring.StatusChanged -= SetStatus);
            sessionLifecycle.Subscribe(
                () => environmentAuthoring.PracticalLightFocusRequested +=
                    HandlePracticalLightFocusRequested,
                () => environmentAuthoring.PracticalLightFocusRequested -=
                    HandlePracticalLightFocusRequested);
            dressingAuthoring = new LevelDressingAuthoringCoordinator(
                workspace,
                cameraController.CaptureState);
            sessionLifecycle.Subscribe(
                () => dressingAuthoring.StatusChanged += SetStatus,
                () => dressingAuthoring.StatusChanged -= SetStatus);
            sessionLifecycle.Subscribe(
                () => dressingAuthoring.FocusRequested +=
                    HandleDressingFocusRequested,
                () => dressingAuthoring.FocusRequested -=
                    HandleDressingFocusRequested);
            layoutAuthoring = new LevelEditorLayoutCoordinator(
                workspace,
                selection,
                cameraController,
                gridSettings);
            sessionLifecycle.Subscribe(
                () => layoutAuthoring.StatusChanged += SetStatus,
                () => layoutAuthoring.StatusChanged -= SetStatus);
            organizationAuthoring = new LevelEditorOrganizationCoordinator(
                workspace,
                selection,
                organizationModel);
            sessionLifecycle.Subscribe(
                () => organizationAuthoring.StatusChanged += SetStatus,
                () => organizationAuthoring.StatusChanged -= SetStatus);
            sessionLifecycle.Subscribe(
                () => organizationAuthoring.GroupFocusRequested +=
                    HandleGroupFocusRequested,
                () => organizationAuthoring.GroupFocusRequested -=
                    HandleGroupFocusRequested);
            gui = new LevelEditorGui(
                selection,
                catalog,
                scenarioCatalog,
                dressingCatalog,
                characterLibrary,
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
            sessionReady = true;
            enabled = true;
        }

        public void EndSession()
        {
            sessionGeneration++;
            sessionReady = false;
            enabled = false;
            SaveLocalPreferences();
            physicsPlacement?.Dispose();
            sessionLifecycle?.Dispose();
            sessionLifecycle = null;
            persistence?.Dispose();
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
            physicsPlacement = null;
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
            cloudOperationRunning = false;
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

        private void PlaceSpatialRecord(
            LevelSpatialPlacementKind kind,
            string targetId,
            Vector3 position)
        {
            bool relocate = !string.IsNullOrEmpty(targetId);
            switch (kind)
            {
                case LevelSpatialPlacementKind.PracticalLight:
                    if (relocate)
                        environmentAuthoring.MovePracticalLightAt(targetId, position);
                    else
                        environmentAuthoring.AddPracticalLightAt(position);
                    break;
                case LevelSpatialPlacementKind.AmbientVfx:
                    if (relocate)
                        dressingAuthoring.MoveAmbientVfxAt(targetId, position);
                    else
                        dressingAuthoring.AddAmbientVfxAt(position);
                    break;
                case LevelSpatialPlacementKind.AudioZone:
                    if (relocate)
                        dressingAuthoring.MoveAudioZoneAt(targetId, position);
                    else
                        dressingAuthoring.AddAudioZoneAt(position);
                    break;
                default:
                    if (relocate)
                        dressingAuthoring.MoveDecalAt(targetId, position);
                    else
                        dressingAuthoring.AddDecalAt(position);
                    break;
            }
            if (relocate)
                toolManager.ActivateDefault();
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
            if (sessionReady
                && !suspended
                && workspace != null
                && viewDocument != null
                && persistence != null
                && gui != null)
            {
                gui.Draw(new LevelEditorViewState(
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
                if (physicsPlacement?.IsRunning == true)
                    physicsPlacement.Cancel();
                else
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
            string pitchText,
            string yawText,
            string rollText)
        {
            LevelEntity entity = workspace.FindEntitySnapshot(selection.PrimaryEntityId);
            if (entity == null)
            {
                return;
            }

            if (!TryParse(xText, out float x)
                || !TryParse(yText, out float y)
                || !TryParse(zText, out float z)
                || !TryParse(pitchText, out float pitch)
                || !TryParse(yawText, out float yaw)
                || !TryParse(rollText, out float roll))
            {
                SetStatus("Transform values must be finite numbers.");
                return;
            }

            var after = new LevelTransformData(
                new Float3Data(x, y, z),
                NormalizeYaw(pitch),
                NormalizeYaw(yaw),
                NormalizeYaw(roll));
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

        private void DropAndSettleSelection(string dropHeightText, bool keepUpright)
            => physicsPlacement?.Start(dropHeightText, keepUpright);

        internal static bool RequiresPhysicsBoundsFallback(IEnumerable<Collider> colliders)
            => LevelEditorPhysicsPlacementCoordinator.RequiresBoundsFallback(
                colliders);

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

        void ILevelEditorPreviewTestActions.ReturnToMenu() =>
            GameBootstrap.Instance.ReturnToMenu();

        bool ILevelEditorFileActions.HasDraft => persistence?.HasDraft ?? false;

        bool ILevelEditorFileActions.HasCloudDraftContext =>
            GameBootstrap.Instance?.ActiveCloudDraft != null;

        bool ILevelEditorFileActions.CloudOperationRunning => cloudOperationRunning;

        int ILevelEditorFileActions.RecoveryGenerationCount =>
            LevelEditorPersistenceCoordinator.RecoveryGenerationCount;

        bool ILevelEditorFileActions.HasRecovery(int generation) =>
            persistence?.HasRecovery(generation) ?? false;

        bool ILevelEditorFileActions.UsesBrowserFileDialog =>
            persistence?.UsesBrowserFileDialog ?? false;

        string ILevelEditorFileActions.DesktopImportPath
        {
            get => persistence?.DesktopImportPath ?? string.Empty;
            set
            {
                if (persistence != null)
                    persistence.DesktopImportPath = value;
            }
        }

        LevelEditorCameraView ILevelEditorSelectionGroupActions.CameraView =>
            cameraController.View;

        string ILevelEditorSelectionGroupActions.IsolatedGroupId =>
            organizationModel.IsolatedGroupId;

        string ILevelEditorSelectionGroupActions.SelectionCategoryFilter =>
            organizationModel.CategoryFilter;

        string ILevelEditorSelectionGroupActions.SelectionGroupFilter =>
            organizationModel.GroupFilter;

        LevelPlayabilityReport ILevelEditorPreviewTestActions.PlayabilityReport =>
            playabilityAuthoring.Report;

        bool ILevelEditorPreviewTestActions.PlayabilityReportIsStale =>
            playabilityAuthoring.IsStale;

        bool ILevelEditorPreviewTestActions.SlopeOverlayEnabled =>
            playabilityAuthoring.SlopeOverlayEnabled;

        bool ILevelEditorEnvironmentDressingActions.AudioZonePreviewEnabled =>
            audioZonePreviewEnabled;

        bool ILevelEditorSpatialPlacementActions.PhysicsPlacementRunning =>
            physicsPlacement?.IsRunning == true;

        void ILevelEditorHistoryActions.Undo() => workspace.Undo();

        void ILevelEditorHistoryActions.Redo() => workspace.Redo();

        void ILevelEditorFileActions.SaveDraft() => persistence.SaveDraft(workspace);

        async void ILevelEditorFileActions.SaveToCloud()
        {
            GameBootstrap bootstrap = GameBootstrap.Instance;
            LevelDraftLibraryCoordinator library = bootstrap?.DraftLibrary;
            if (library == null || workspace == null || cloudOperationRunning)
            {
                SetStatus(bootstrap?.Supabase?.Status ?? "Cloud saves are not configured.");
                return;
            }

            int generation = sessionGeneration;
            int savedRevision = workspace.Revision;
            LevelDocument snapshot = workspace.CreateSnapshot();
            cloudOperationRunning = true;
            SetStatus("Saving cloud draft…");
            try
            {
                LevelDraftRecord active = bootstrap.ActiveCloudDraft;
                if (active == null)
                {
                    string name = string.IsNullOrWhiteSpace(snapshot.displayName)
                        ? "Untitled Level"
                        : snapshot.displayName;
                    active = await library.CreateAsync(name, snapshot);
                    if (generation != sessionGeneration || !sessionReady) return;
                    bootstrap.AdoptActiveCloudDraft(active);
                    sourceDocument = snapshot.DeepCopy();
                    sourceDocumentIsSaved = true;
                    sourceLabel = "cloud draft: " + active.Summary.Name;
                }
                else
                {
                    LevelDraftSummary summary = await library.SaveAsync(
                        active.Summary.Id,
                        active.Summary.Revision,
                        snapshot);
                    if (generation != sessionGeneration || !sessionReady) return;
                    bootstrap.AdoptActiveCloudDraft(new LevelDraftRecord(summary, snapshot));
                    sourceDocument = snapshot.DeepCopy();
                    sourceDocumentIsSaved = true;
                }

                if (workspace.Revision == savedRevision) workspace.MarkSaved();
                SetStatus(library.Status);
            }
            catch (Exception exception)
            {
                if (generation == sessionGeneration && sessionReady)
                    SetStatus(exception.Message);
            }
            finally
            {
                if (generation == sessionGeneration) cloudOperationRunning = false;
            }
        }

        async void ILevelEditorFileActions.LoadFromCloud()
        {
            GameBootstrap bootstrap = GameBootstrap.Instance;
            LevelDraftRecord active = bootstrap?.ActiveCloudDraft;
            LevelDraftLibraryCoordinator library = bootstrap?.DraftLibrary;
            if (active == null || library == null || cloudOperationRunning)
            {
                SetStatus("Open a cloud draft before loading it.");
                return;
            }

            int generation = sessionGeneration;
            cloudOperationRunning = true;
            SetStatus("Loading cloud draft…");
            try
            {
                LevelDraftRecord loaded = await library.LoadAsync(active.Summary.Id);
                if (generation != sessionGeneration || !sessionReady) return;
                LevelDocument document = loaded.CreateDocumentSnapshot();
                bootstrap.AdoptActiveCloudDraft(loaded);
                sourceDocument = document.DeepCopy();
                sourceDocumentIsSaved = true;
                sourceLabel = "cloud draft: " + loaded.Summary.Name;
                ReplaceWorkspaceDocument(document, isSaved: true);
                SetStatus("Loaded cloud draft.");
            }
            catch (Exception exception)
            {
                if (generation == sessionGeneration && sessionReady)
                    SetStatus(exception.Message);
            }
            finally
            {
                if (generation == sessionGeneration) cloudOperationRunning = false;
            }
        }

        void ILevelEditorFileActions.LoadDraft() => persistence.LoadDraft();

        void ILevelEditorFileActions.LoadRecovery(int generation) =>
            persistence.LoadRecovery(generation);

        void ILevelEditorFileActions.Export() => persistence.Export(workspace);

        void ILevelEditorFileActions.RequestImport() => persistence.RequestImport();

        void ILevelEditorPreviewTestActions.TogglePreview() => TogglePreview();

        void ILevelEditorPreviewTestActions.StartTestPlay() => StartTestPlay();

        void ILevelEditorFileActions.CreateNewLevel() => CreateNewLevel();

        void ILevelEditorFileActions.ReloadSourceLevel() => ReloadSourceLevel();

        void ILevelEditorSelectionGroupActions.FrameSelection() =>
            FrameSelection();

        void ILevelEditorSelectionGroupActions.FrameLevel() => FrameLevel();

        void ILevelEditorSelectionGroupActions.FocusEntity(string entityId) =>
            FocusEntity(entityId);

        void ILevelEditorSpatialPlacementActions.ApplyLevelDisplayName(
            string displayName) =>
            ApplyLevelDisplayName(displayName);

        void ILevelEditorSpatialPlacementActions.ApplyLevelBounds(
            LevelBoundsAuthoringRequest request) =>
            layoutAuthoring.ApplyBounds(request);

        void ILevelEditorSpatialPlacementActions.ConfigureGrid(
            LevelGridAuthoringRequest request) =>
            layoutAuthoring.ConfigureGrid(request);

        void ILevelEditorSelectionGroupActions.SetCameraView(
            LevelEditorCameraView view) =>
            layoutAuthoring.SetCameraView(view);

        void ILevelEditorSelectionGroupActions.DuplicateArray(
            LevelArrayAuthoringRequest request) =>
            layoutAuthoring.DuplicateArray(request);

        void ILevelEditorSelectionGroupActions.CreateEntityGroup(
            string displayName) =>
            organizationAuthoring.CreateGroup(displayName);

        void ILevelEditorSelectionGroupActions.RenameEntityGroup(
            string groupId,
            string displayName) =>
            organizationAuthoring.RenameGroup(groupId, displayName);

        void ILevelEditorSelectionGroupActions.SetEntityGroupLocked(
            string groupId,
            bool locked) =>
            organizationAuthoring.SetGroupLocked(groupId, locked);

        void ILevelEditorSelectionGroupActions.SetEntityGroupHidden(
            string groupId,
            bool hidden) =>
            organizationAuthoring.SetGroupHidden(groupId, hidden);

        void ILevelEditorSelectionGroupActions.AssignSelectionToGroup(
            string groupId) =>
            organizationAuthoring.AssignSelection(groupId);

        void ILevelEditorSelectionGroupActions.DeleteEntityGroup(
            string groupId) =>
            organizationAuthoring.DeleteGroup(groupId);

        void ILevelEditorSelectionGroupActions.IsolateEntityGroup(
            string groupId) =>
            organizationAuthoring.IsolateGroup(groupId);

        void ILevelEditorSelectionGroupActions.SetSelectionCategoryFilter(
            string category) =>
            organizationAuthoring.SetCategoryFilter(category);

        void ILevelEditorSelectionGroupActions.SetSelectionGroupFilter(
            string groupId) =>
            organizationAuthoring.SetGroupFilter(groupId);

        void ILevelEditorSelectionGroupActions.SelectMatchingEntities() =>
            organizationAuthoring.SelectMatching();

        void ILevelEditorPreviewTestActions.RunPlayabilityDiagnostics() =>
            playabilityAuthoring.Run();

        void ILevelEditorPreviewTestActions.SetSlopeOverlayEnabled(bool enabled) =>
            playabilityAuthoring.SetSlopeOverlay(enabled);

        void ILevelEditorEnvironmentDressingActions.ApplyEnvironment(
            LevelEnvironmentAuthoringRequest request) =>
            environmentAuthoring.ApplyEnvironment(request);

        void ILevelEditorEnvironmentDressingActions.AddPracticalLight() =>
            environmentAuthoring.AddPracticalLight();

        void ILevelEditorSpatialPlacementActions.QueueSpatialPlacement(
            LevelSpatialPlacementKind kind)
        {
            spatialPlacementTool.Queue(kind);
            toolManager.Activate(SpatialRecordPlacementTool.ToolId);
        }

        void ILevelEditorSpatialPlacementActions.QueueSpatialRelocation(
            LevelSpatialPlacementKind kind,
            string targetId)
        {
            spatialPlacementTool.Queue(kind, targetId);
            toolManager.Activate(SpatialRecordPlacementTool.ToolId);
        }

        void ILevelEditorEnvironmentDressingActions.ApplyPracticalLight(
            LevelPracticalLightAuthoringRequest request) =>
            environmentAuthoring.ApplyPracticalLight(request);

        void ILevelEditorEnvironmentDressingActions.DeletePracticalLight(
            string lightId) =>
            environmentAuthoring.DeletePracticalLight(lightId);

        void ILevelEditorEnvironmentDressingActions.AddDecal() =>
            dressingAuthoring.AddDecal();

        void ILevelEditorEnvironmentDressingActions.ApplyDecal(
            LevelDecalAuthoringRequest request) =>
            dressingAuthoring.ApplyDecal(request);

        void ILevelEditorEnvironmentDressingActions.DeleteDecal(
            string decalId) =>
            dressingAuthoring.DeleteDecal(decalId);

        void ILevelEditorEnvironmentDressingActions.AddAmbientVfx() =>
            dressingAuthoring.AddAmbientVfx();

        void ILevelEditorEnvironmentDressingActions.ApplyAmbientVfx(
            LevelAmbientVfxAuthoringRequest request) =>
            dressingAuthoring.ApplyAmbientVfx(request);

        void ILevelEditorEnvironmentDressingActions.DeleteAmbientVfx(
            string effectId) =>
            dressingAuthoring.DeleteAmbientVfx(effectId);

        void ILevelEditorEnvironmentDressingActions.AddAudioZone() =>
            dressingAuthoring.AddAudioZone();

        void ILevelEditorEnvironmentDressingActions.ApplyAudioZone(
            LevelAudioZoneAuthoringRequest request) =>
            dressingAuthoring.ApplyAudioZone(request);

        void ILevelEditorEnvironmentDressingActions.DeleteAudioZone(
            string zoneId) =>
            dressingAuthoring.DeleteAudioZone(zoneId);

        void ILevelEditorEnvironmentDressingActions
            .SetAudioZonePreviewEnabled(bool enabled)
        {
            audioZonePreviewEnabled = enabled;
            dressingProjector?.SetEditorPresentation(
                showZoneGizmos: !previewMode,
                playAudio: previewMode || enabled);
            SetStatus(enabled
                ? "Ambient audio preview enabled."
                : "Ambient audio preview muted.");
        }

        void ILevelEditorSpatialPlacementActions.ApplyEntityTransform(
            string x,
            string y,
            string z,
            string pitch,
            string yaw,
            string roll) => ApplyInspectorTransform(x, y, z, pitch, yaw, roll);

        void ILevelEditorSpatialPlacementActions.DropAndSettleSelection(
            string dropHeight,
            bool keepUpright) => DropAndSettleSelection(dropHeight, keepUpright);

        void ILevelEditorSpatialPlacementActions.CancelPhysicsPlacement() =>
            physicsPlacement?.Cancel();

        void ILevelEditorSpatialPlacementActions.SetEntityRotationPivot(
            float normalizedX,
            float normalizedZ) =>
            SetEntityRotationPivot(normalizedX, normalizedZ);

        void ILevelEditorSpatialPlacementActions.ResetEntityRotationPivot() =>
            ResetEntityRotationPivot();

        void ILevelEditorSpatialPlacementActions.ApplyPlayerStart(
            string x,
            string y,
            string z,
            string yaw) => scenarioAuthoring.ApplyPlayerStart(x, y, z, yaw);

        void ILevelEditorSpatialPlacementActions.AddInteractionPoint() =>
            AddInteractionPoint();

        void ILevelEditorSpatialPlacementActions.ApplyInteractionPoint(
            string type,
            string x,
            string y,
            string z,
            string radius) => ApplyInteractionPoint(type, x, y, z, radius);

        void ILevelEditorSpatialPlacementActions.DeleteInteractionPoint() =>
            DeleteInteractionPoint();

        void ILevelEditorSpatialPlacementActions.ApplyDestructibleDefaults(
            string enabled,
            string state,
            string integrity) => ApplyDestructibleDefaults(enabled, state, integrity);

        void ILevelEditorSpatialPlacementActions.AddScenarioActor(
            string templateId) =>
            scenarioAuthoring.AddActor(templateId);

        void ILevelEditorSpatialPlacementActions.ApplyScenarioActorCharacter(
            string actorId,
            string characterId) =>
            scenarioAuthoring.ApplyActorCharacter(actorId, characterId);

        void ILevelEditorSpatialPlacementActions.ApplyScenarioActor(
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

        void ILevelEditorSpatialPlacementActions.DeleteScenarioActor(
            string actorId) =>
            scenarioAuthoring.DeleteActor(actorId);

        void ILevelEditorSpatialPlacementActions.PlaceScenarioActorAtView(
            string actorId) =>
            scenarioAuthoring.PlaceActorAtView(actorId);

        void ILevelEditorSpatialPlacementActions.ApplyScenarioProp(
            string entityId,
            bool enabled,
            string mass,
            string sizeClass,
            bool startsEncounter,
            bool topplingEnabled,
            string topplingPitch,
            string topplingRoll,
            string topplingElevation,
            bool pinningEnabled,
            string maximumPinnedActorMass,
            string minimumPinContactDepth) => scenarioAuthoring.ApplyProp(
                entityId,
                enabled,
                mass,
                sizeClass,
                startsEncounter,
                topplingEnabled,
                topplingPitch,
                topplingRoll,
                topplingElevation,
                pinningEnabled,
                maximumPinnedActorMass,
                minimumPinContactDepth);

        void ILevelEditorSpatialPlacementActions.ApplyScenarioObjective(
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

        void ILevelEditorSpatialPlacementActions.ApplyScenarioVehicle(
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
