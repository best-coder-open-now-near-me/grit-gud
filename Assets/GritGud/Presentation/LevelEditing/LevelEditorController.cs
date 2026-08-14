using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GritGud.Application.Levels;
using GritGud.Domain.Levels;
using GritGud.Presentation.Bootstrap;
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
    public sealed class LevelEditorController : MonoBehaviour
    {
        private const string MainLevelResourceName = "Levels/main-level";

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
        private LevelEntityView selectedView;
        private RuntimeBoundsOutline selectionOutline;
        private RuntimeBoundsOutline hoverOutline;
        private RuntimeBoundsOutline placementOutline;
        private readonly Dictionary<string, RuntimeBoundsOutline> secondarySelectionOutlines =
            new Dictionary<string, RuntimeBoundsOutline>(StringComparer.Ordinal);
        private IReadOnlyList<LevelValidationIssue> validationIssues =
            Array.Empty<LevelValidationIssue>();
        private bool previewMode;
        private bool suspended;
        private string statusMessage = string.Empty;

        public void Begin(bool startInPreview)
        {
            EndSession();
            suspended = false;
            enabled = true;
            catalog = LevelArchetypeCatalog.LoadDefault();
            scenarioCatalog = ScenarioAuthoringCatalog.LoadDefault();
            LevelTextTransfer textTransfer = GetComponent<LevelTextTransfer>();
            if (textTransfer == null)
            {
                textTransfer = gameObject.AddComponent<LevelTextTransfer>();
            }

            persistence = new LevelEditorPersistenceCoordinator(
                new UnityLevelJsonSerializer(),
                new PlayerPrefsLevelDraftStore(),
                textTransfer,
                catalog);
            persistence.DocumentLoaded += HandleDocumentLoaded;
            persistence.StatusChanged += SetStatus;
            Camera sceneCamera = Camera.main;
            if (sceneCamera == null)
            {
                throw new InvalidOperationException("The bootstrap scene needs a Main Camera.");
            }

            workspace = new LevelEditorWorkspace(LoadMainLevel(), catalog.CreateKnownIdSet());
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
            var terrainPanel = new TerrainToolPanelModel(
                toolManager,
                terrainTool,
                FrameTerrain);
            gui = new LevelEditorGui(
                workspace,
                selection,
                catalog,
                scenarioCatalog,
                toolManager,
                placementTool,
                terrainPanel,
                selectionTool,
                snapSettings,
                persistence,
                () => GameBootstrap.Instance.ReturnToMenu(),
                TogglePreview,
                StartTestPlay,
                CreateNewLevel,
                ReloadMainLevel,
                FrameSelection,
                FrameLevel,
                FocusValidationEntity,
                ApplyInspectorTransform,
                ApplyPlayerStart,
                AddInteractionPoint,
                ApplyInteractionPoint,
                DeleteInteractionPoint,
                ApplyDestructibleDefaults,
                AddScenarioActor,
                ApplyScenarioActor,
                DeleteScenarioActor,
                PlaceScenarioActorAtView,
                ApplyScenarioProp,
                ApplyScenarioObjective,
                ApplyScenarioVehicle);
            gui.SyncPlayerStartFields(RequireSelectedPlayer(workspace.CreateSnapshot()).transform);
            EnsureOutlines();
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

            toolManager?.Dispose();
            projector?.Dispose();
            terrainProjector?.Dispose();
            interactionPointHandles?.Dispose();
            scenarioActorHandles?.Dispose();
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
            selectedView = null;
            sceneQuery = null;
            snapSettings = null;
            preferencesStore = null;
            gui = null;

            if (selectionOutline != null)
            {
                Destroy(selectionOutline.gameObject);
            }

            if (hoverOutline != null)
            {
                Destroy(hoverOutline.gameObject);
            }

            if (placementOutline != null)
            {
                Destroy(placementOutline.gameObject);
            }

            foreach (RuntimeBoundsOutline outline in secondarySelectionOutlines.Values)
            {
                if (outline != null)
                {
                    Destroy(outline.gameObject);
                }
            }

            secondarySelectionOutlines.Clear();

            selectionOutline = null;
            hoverOutline = null;
            placementOutline = null;
            suspended = false;
            enabled = false;
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
            if (!suspended)
            {
                gui?.Draw(previewMode, selectedView, validationIssues, statusMessage);
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
            selectionOutline.gameObject.SetActive(false);
            hoverOutline.gameObject.SetActive(false);
            placementOutline.gameObject.SetActive(false);
            foreach (RuntimeBoundsOutline outline in secondarySelectionOutlines.Values)
            {
                if (outline != null)
                {
                    outline.gameObject.SetActive(false);
                }
            }
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

            LevelScenarioActorData unavailableTemplate = snapshot.scenario.actors
                .FirstOrDefault(actor => actor != null
                    && !scenarioCatalog.ContainsActor(actor.templateId));
            if (unavailableTemplate != null)
            {
                SetStatus(
                    $"Actor '{unavailableTemplate.id}' uses unavailable template "
                    + $"'{unavailableTemplate.templateId}'.");
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

        private void FocusValidationEntity(string entityId)
        {
            if (string.IsNullOrWhiteSpace(entityId)
                || !projector.TryGetEntity(entityId, out LevelEntityView view))
            {
                SetStatus("The validation issue does not reference a loaded entity.");
                return;
            }

            selection.SetSingle(entityId);
            cameraController.Frame(view.GetWorldBounds());
            SetStatus($"Focused validation issue on {entityId}.");
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

        private void ApplyPlayerStart(
            string xText,
            string yText,
            string zText,
            string yawText)
        {
            if (!TryParse(xText, out float x)
                || !TryParse(yText, out float y)
                || !TryParse(zText, out float z)
                || !TryParse(yawText, out float yaw))
            {
                SetStatus("Player-start values must be finite numbers.");
                return;
            }

            LevelTransformData before = RequireSelectedPlayer(workspace.CreateSnapshot()).transform;
            var after = new LevelTransformData(
                new Float3Data(x, y, z),
                NormalizeYaw(yaw));
            workspace.Execute(new SetPlayerStartCommand(before, after));
            gui.SyncPlayerStartFields(after);
            SetStatus("Updated the selected scenario player's start.");
        }

        private static LevelScenarioActorData RequireSelectedPlayer(LevelDocument document)
        {
            LevelScenarioActorData player = document?.scenario?
                .FindInitiallySelectedPlayer();
            return player ?? throw new InvalidOperationException(
                "The level scenario does not define an initially selected player actor.");
        }

        private void AddScenarioActor(string templateId)
        {
            ScenarioActorTemplateDefinition template = scenarioCatalog.GetActor(templateId);
            LevelDocument snapshot = workspace.CreateSnapshot();
            LevelEditorCameraState cameraState = cameraController.CaptureState();
            bool playerControlled = template.PlayerTemplate;
            bool hasSelectedPlayer = snapshot.scenario.FindInitiallySelectedPlayer() != null;
            bool hasPrimaryTarget = snapshot.scenario.actors.Any(actor => actor?.primaryTarget == true);
            var actor = new LevelScenarioActorData
            {
                id = "actor-" + LevelDocumentFactory.NewStableId(),
                templateId = template.TemplateId,
                transform = new LevelTransformData(
                    new Float3Data(
                        cameraState.target.x,
                        cameraState.target.y,
                        cameraState.target.z),
                    NormalizeYaw(cameraState.yaw)),
                playerControlled = playerControlled,
                initiallySelected = playerControlled && !hasSelectedPlayer,
                primaryTarget = !playerControlled && !hasPrimaryTarget,
            };
            workspace.Execute(new AddScenarioActorCommand(actor));
            gui.SelectScenarioActor(actor.id);
            SetStatus($"Added {template.DisplayName} to the scenario at the camera focus.");
        }

        private void ApplyScenarioActor(
            string actorId,
            string xText,
            string yText,
            string zText,
            string yawText,
            bool playerControlled,
            bool initiallySelected,
            bool primaryTarget)
        {
            if (!TryParse(xText, out float x)
                || !TryParse(yText, out float y)
                || !TryParse(zText, out float z)
                || !TryParse(yawText, out float yaw))
            {
                SetStatus("Scenario actor transforms must contain finite numbers.");
                return;
            }

            LevelDocument snapshot = workspace.CreateSnapshot();
            LevelScenarioActorData before = FindScenarioActor(snapshot, actorId);
            if (before == null)
                return;
            if (initiallySelected && !playerControlled)
            {
                SetStatus("The initially selected actor must be player controlled.");
                return;
            }

            if (primaryTarget && playerControlled)
            {
                SetStatus("A player actor cannot also be the primary target.");
                return;
            }

            if (before.initiallySelected && !initiallySelected)
            {
                SetStatus("Select another player actor before clearing the current selection.");
                return;
            }

            var commands = new List<ILevelEditCommand>();
            if (initiallySelected)
            {
                foreach (LevelScenarioActorData other in snapshot.scenario.actors.Where(actor =>
                    actor != null
                    && actor.initiallySelected
                    && !string.Equals(actor.id, actorId, StringComparison.Ordinal)))
                {
                    LevelScenarioActorData replacement = other.DeepCopy();
                    replacement.initiallySelected = false;
                    commands.Add(new SetScenarioActorCommand(
                        other.id,
                        other,
                        replacement));
                }
            }

            if (primaryTarget)
            {
                foreach (LevelScenarioActorData other in snapshot.scenario.actors.Where(actor =>
                    actor != null
                    && actor.primaryTarget
                    && !string.Equals(actor.id, actorId, StringComparison.Ordinal)))
                {
                    LevelScenarioActorData replacement = other.DeepCopy();
                    replacement.primaryTarget = false;
                    commands.Add(new SetScenarioActorCommand(
                        other.id,
                        other,
                        replacement));
                }
            }

            LevelScenarioActorData after = before.DeepCopy();
            after.transform = new LevelTransformData(
                new Float3Data(x, y, z),
                NormalizeYaw(yaw));
            after.playerControlled = playerControlled;
            after.initiallySelected = initiallySelected;
            after.primaryTarget = primaryTarget;
            commands.Add(new SetScenarioActorCommand(actorId, before, after));
            ExecuteScenarioCommands("Edit scenario actor", commands);
            if (after.initiallySelected)
                gui.SyncPlayerStartFields(after.transform);
            gui.SyncScenarioActorFields(after);
            SetStatus($"Updated scenario actor '{actorId}'.");
        }

        private void DeleteScenarioActor(string actorId)
        {
            LevelDocument snapshot = workspace.CreateSnapshot();
            LevelScenarioActorData actor = FindScenarioActor(snapshot, actorId);
            if (actor == null)
                return;
            LevelScenarioActorData[] otherPlayers = snapshot.scenario.actors
                .Where(candidate => candidate != null
                    && candidate.playerControlled
                    && !string.Equals(candidate.id, actorId, StringComparison.Ordinal))
                .ToArray();
            if (actor.playerControlled && otherPlayers.Length == 0)
            {
                SetStatus("A scenario must keep at least one player actor.");
                return;
            }

            var commands = new List<ILevelEditCommand>();
            LevelScenarioData linksAfter = snapshot.scenario.DeepCopy();
            bool clearedOccupant = false;
            foreach (LevelScenarioVehicleData vehicle in linksAfter.vehicles.Where(vehicle =>
                string.Equals(
                    vehicle?.startingOccupantActorId,
                    actorId,
                    StringComparison.Ordinal)))
            {
                vehicle.startingOccupantActorId = string.Empty;
                clearedOccupant = true;
            }
            if (clearedOccupant)
            {
                commands.Add(new SetScenarioConfigurationCommand(
                    "Clear deleted vehicle occupant",
                    snapshot.scenario,
                    linksAfter,
                    linksAfter.vehicles.Select(vehicle => vehicle?.entityId)));
            }

            if (actor.initiallySelected)
            {
                LevelScenarioActorData next = otherPlayers[0];
                LevelScenarioActorData selected = next.DeepCopy();
                selected.initiallySelected = true;
                commands.Add(new SetScenarioActorCommand(next.id, next, selected));
            }

            commands.Add(new DeleteScenarioActorCommand(actorId));
            ExecuteScenarioCommands("Delete scenario actor", commands);
            gui.SelectScenarioActor(null);
            LevelScenarioActorData selectedPlayer = workspace.CreateSnapshot().scenario
                .FindInitiallySelectedPlayer();
            if (selectedPlayer != null)
                gui.SyncPlayerStartFields(selectedPlayer.transform);
            SetStatus($"Deleted scenario actor '{actorId}'.");
        }

        private void PlaceScenarioActorAtView(string actorId)
        {
            LevelDocument snapshot = workspace.CreateSnapshot();
            LevelScenarioActorData before = FindScenarioActor(snapshot, actorId);
            if (before == null)
                return;
            LevelEditorCameraState cameraState = cameraController.CaptureState();
            LevelScenarioActorData after = before.DeepCopy();
            after.transform = new LevelTransformData(
                new Float3Data(
                    cameraState.target.x,
                    cameraState.target.y,
                    cameraState.target.z),
                after.transform.yawDegrees);
            workspace.Execute(new SetScenarioActorCommand(actorId, before, after));
            gui.SyncScenarioActorFields(after);
            if (after.initiallySelected)
                gui.SyncPlayerStartFields(after.transform);
            SetStatus($"Placed scenario actor '{actorId}' at the camera focus.");
        }

        private void ApplyScenarioProp(
            string entityId,
            bool enabled,
            string massText,
            string sizeClass,
            bool startsEncounter)
        {
            LevelDocument snapshot = workspace.CreateSnapshot();
            LevelScenarioData after = snapshot.scenario.DeepCopy();
            after.props.RemoveAll(prop =>
                string.Equals(prop?.entityId, entityId, StringComparison.Ordinal));
            if (enabled)
            {
                if (!TryParse(massText, out float mass) || mass <= 0f)
                {
                    SetStatus("Scenario prop mass must be a positive finite number.");
                    return;
                }

                if (!IsSupportedSizeClass(sizeClass))
                {
                    SetStatus("Choose a supported scenario prop size.");
                    return;
                }

                after.props.Add(new LevelScenarioPropData
                {
                    entityId = entityId,
                    mass = mass,
                    sizeClass = sizeClass,
                    startsEncounterOnAttack = startsEncounter,
                });
            }

            workspace.Execute(new SetScenarioConfigurationCommand(
                enabled ? "Configure scenario prop" : "Remove scenario prop",
                snapshot.scenario,
                after,
                new[] { entityId }));
            SetStatus(enabled
                ? "Linked the selected entity as a gameplay physics prop."
                : "Removed the selected entity's gameplay prop link.");
        }

        private void ApplyScenarioObjective(
            string entityId,
            string pointId,
            bool enabled,
            string displayName,
            string activeText,
            string completedText,
            string costText)
        {
            LevelDocument snapshot = workspace.CreateSnapshot();
            LevelScenarioData after = snapshot.scenario.DeepCopy();
            LevelScenarioObjectiveData existing = after.objectives.FirstOrDefault(objective =>
                string.Equals(objective?.entityId, entityId, StringComparison.Ordinal)
                && string.Equals(
                    objective?.interactionPointId,
                    pointId,
                    StringComparison.Ordinal));
            after.objectives.Remove(existing);
            if (enabled)
            {
                if (!int.TryParse(
                        costText,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out int actionPointCost)
                    || actionPointCost < 0)
                {
                    SetStatus("Objective action-point cost must be a non-negative whole number.");
                    return;
                }

                if (string.IsNullOrWhiteSpace(displayName))
                {
                    SetStatus("An enabled objective needs a display name.");
                    return;
                }

                after.objectives.Add(new LevelScenarioObjectiveData
                {
                    id = existing?.id ?? "objective-" + LevelDocumentFactory.NewStableId(),
                    entityId = entityId,
                    interactionPointId = pointId,
                    actionId = existing?.actionId ?? "interact",
                    displayName = displayName.Trim(),
                    activeHudText = activeText?.Trim() ?? string.Empty,
                    completedHudText = completedText?.Trim() ?? string.Empty,
                    actionPointCost = actionPointCost,
                });
            }

            workspace.Execute(new SetScenarioConfigurationCommand(
                enabled ? "Configure scenario objective" : "Remove scenario objective",
                snapshot.scenario,
                after,
                new[] { entityId }));
            SetStatus(enabled
                ? "Linked the interaction point as a scenario objective."
                : "Removed the interaction point's scenario objective link.");
        }

        private void ApplyScenarioVehicle(
            string entityId,
            bool enabled,
            string maximumSpeedText,
            string accelerationText,
            string brakingText,
            string lowTurnText,
            string highTurnText,
            string baseRadiusText,
            string radiusFactorText,
            string startingSpeedText,
            string occupantActorId,
            bool startsEncounter)
        {
            LevelDocument snapshot = workspace.CreateSnapshot();
            LevelScenarioData after = snapshot.scenario.DeepCopy();
            after.vehicles.RemoveAll(vehicle =>
                string.Equals(vehicle?.entityId, entityId, StringComparison.Ordinal));
            if (enabled)
            {
                if (!TryParse(maximumSpeedText, out float maximumSpeed)
                    || !TryParse(accelerationText, out float acceleration)
                    || !TryParse(brakingText, out float braking)
                    || !TryParse(lowTurnText, out float lowTurn)
                    || !TryParse(highTurnText, out float highTurn)
                    || !TryParse(baseRadiusText, out float baseRadius)
                    || !TryParse(radiusFactorText, out float radiusFactor)
                    || !TryParse(startingSpeedText, out float startingSpeed)
                    || maximumSpeed <= 0f
                    || acceleration < 0f
                    || braking < 0f
                    || lowTurn < 0f
                    || highTurn < 0f
                    || baseRadius <= 0f
                    || radiusFactor < 0f
                    || startingSpeed < 0f
                    || startingSpeed > maximumSpeed)
                {
                    SetStatus(
                        "Vehicle values must be finite and non-negative; start speed cannot exceed max speed.");
                    return;
                }

                string occupant = occupantActorId?.Trim() ?? string.Empty;
                if (!string.IsNullOrEmpty(occupant)
                    && FindScenarioActor(snapshot, occupant) == null)
                {
                    SetStatus($"Vehicle occupant actor '{occupant}' does not exist.");
                    return;
                }

                after.vehicles.Add(new LevelScenarioVehicleData
                {
                    entityId = entityId,
                    maximumSpeed = maximumSpeed,
                    accelerationPerTurn = acceleration,
                    brakingPerTurn = braking,
                    lowSpeedTurnDegrees = lowTurn,
                    highSpeedTurnDegrees = highTurn,
                    baseTurningRadius = baseRadius,
                    speedTurningRadiusFactor = radiusFactor,
                    startingSpeed = startingSpeed,
                    startingOccupantActorId = occupant,
                    startsEncounterOnAttack = startsEncounter,
                });
            }

            workspace.Execute(new SetScenarioConfigurationCommand(
                enabled ? "Configure scenario vehicle" : "Remove scenario vehicle",
                snapshot.scenario,
                after,
                new[] { entityId }));
            SetStatus(enabled
                ? "Linked the selected entity as a driveable scenario vehicle."
                : "Removed the selected entity's scenario vehicle link.");
        }

        private static bool IsSupportedSizeClass(string value)
        {
            return string.Equals(value, "small", StringComparison.Ordinal)
                || string.Equals(value, "medium", StringComparison.Ordinal)
                || string.Equals(value, "large", StringComparison.Ordinal)
                || string.Equals(value, "huge", StringComparison.Ordinal);
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

        private static LevelScenarioActorData FindScenarioActor(
            LevelDocument document,
            string actorId)
        {
            return document?.scenario?.actors.FirstOrDefault(actor =>
                string.Equals(actor?.id, actorId, StringComparison.Ordinal));
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
            selectionOutline.gameObject.SetActive(false);
            placementOutline.gameObject.SetActive(false);
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

            if (selectionOutline != null)
            {
                selectionOutline.gameObject.SetActive(selectedView != null && !previewMode);
            }

            if (selectedView != null)
            {
                SyncInspectorFields(selectedView.ReadTransform());
            }
        }

        private void CreateNewLevel()
        {
            ReplaceWorkspaceDocument(LevelDocumentFactory.CreateEmpty());
            SetStatus("Created a new empty level. Choose an archetype to begin placing.");
        }

        private void ReloadMainLevel()
        {
            ReplaceWorkspaceDocument(LoadMainLevel());
            SetStatus("Reloaded the committed main level.");
        }

        private void ReplaceWorkspaceDocument(LevelDocument document)
        {
            placementTool.SelectArchetype(null);
            toolManager.ActivateDefault();
            selection.Clear();
            workspace.ReplaceDocument(document);
            cameraController.Frame(document.bounds);
            scenarioActorHandles.Refresh(document);
            gui.SelectScenarioActor(null);
            gui.SyncScenarioFields(document);
        }

        private void HandleDocumentLoaded(object sender, LevelDocumentLoadedEventArgs args)
        {
            ReplaceWorkspaceDocument(args.Document);
            SetStatus($"Loaded level from {args.SourceLabel}.");
        }

        private LevelDocument LoadMainLevel()
        {
            TextAsset level = Resources.Load<TextAsset>(MainLevelResourceName);
            if (level == null)
            {
                throw new InvalidOperationException(
                    $"Main level '{MainLevelResourceName}' was not found.");
            }

            return persistence.Deserialize(level.text);
        }

        private void EnsureOutlines()
        {
            var selectionObject = new GameObject("Selection Outline");
            selectionObject.transform.SetParent(transform, false);
            selectionOutline = selectionObject.AddComponent<RuntimeBoundsOutline>();
            selectionOutline.Initialize(new Color(0.2f, 0.8f, 1f));
            selectionOutline.gameObject.SetActive(false);

            var hoverObject = new GameObject("Hover Outline");
            hoverObject.transform.SetParent(transform, false);
            hoverOutline = hoverObject.AddComponent<RuntimeBoundsOutline>();
            hoverOutline.Initialize(new Color(1f, 0.9f, 0.25f));
            hoverOutline.gameObject.SetActive(false);

            var placementObject = new GameObject("Placement Outline");
            placementObject.transform.SetParent(transform, false);
            placementOutline = placementObject.AddComponent<RuntimeBoundsOutline>();
            placementOutline.Initialize(new Color(1f, 0.6f, 0.15f));
            placementOutline.gameObject.SetActive(false);
        }

        private void UpdateOutlines()
        {
            if (selectionOutline != null && selectedView != null && !previewMode)
            {
                selectionOutline.gameObject.SetActive(true);
                selectionOutline.SetBounds(selectedView.GetWorldBounds());
            }

            UpdateSecondarySelectionOutlines();

            if (placementOutline != null
                && !previewMode
                && toolManager.ActiveTool == placementTool
                && placementTool.TryGetPreviewBounds(out Bounds previewBounds))
            {
                placementOutline.gameObject.SetActive(true);
                placementOutline.SetBounds(previewBounds);
            }
            else if (placementOutline != null)
            {
                placementOutline.gameObject.SetActive(false);
            }
        }

        private void UpdateSecondarySelectionOutlines()
        {
            var visibleIds = new HashSet<string>(StringComparer.Ordinal);
            if (!previewMode)
            {
                foreach (LevelSelectionTarget target in selection.Targets.Skip(1))
                {
                    if (!visibleIds.Add(target.EntityId)
                        || !projector.TryGetEntity(target.EntityId, out LevelEntityView view))
                    {
                        continue;
                    }

                    if (!secondarySelectionOutlines.TryGetValue(
                        target.EntityId,
                        out RuntimeBoundsOutline outline))
                    {
                        var outlineObject = new GameObject("Secondary Selection Outline");
                        outlineObject.transform.SetParent(transform, false);
                        outline = outlineObject.AddComponent<RuntimeBoundsOutline>();
                        outline.Initialize(new Color(0.35f, 1f, 0.65f));
                        secondarySelectionOutlines.Add(target.EntityId, outline);
                    }

                    outline.gameObject.SetActive(true);
                    outline.SetBounds(view.GetWorldBounds());
                }
            }

            foreach (KeyValuePair<string, RuntimeBoundsOutline> entry
                in secondarySelectionOutlines)
            {
                if (!visibleIds.Contains(entry.Key))
                {
                    entry.Value.gameObject.SetActive(false);
                }
            }
        }

        private void UpdateHover(Vector2 pointerPosition, bool pointerBlocked)
        {
            if (hoverOutline == null)
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
                hoverOutline.gameObject.SetActive(true);
                hoverOutline.SetBounds(view.GetWorldBounds());
                return;
            }

            hoverOutline.gameObject.SetActive(false);
        }

        private void SyncInspectorFields(LevelTransformData value)
        {
            gui?.SyncTransformFields(value);
        }

        private void SetStatus(string message)
        {
            statusMessage = message ?? string.Empty;
        }

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
