using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GritGud.Application.Levels;
using GritGud.Domain.Levels;
using GritGud.Presentation.LevelEditing.Core;
using GritGud.Presentation.LevelEditing.Persistence;
using GritGud.Presentation.LevelEditing.Tools;
using GritGud.Presentation.Levels.Runtime;
using UnityEngine;

namespace GritGud.Presentation.LevelEditing.UI
{
    public sealed class LevelEditorGui
    {
        public const float ToolbarHeight = 92f;
        public const float PaletteWidth = 300f;
        public const float InspectorWidth = 360f;

        private readonly LevelEditorWorkspace workspace;
        private readonly LevelSelectionModel selection;
        private readonly LevelArchetypeCatalog catalog;
        private readonly ScenarioAuthoringCatalog scenarioCatalog;
        private readonly LevelEditorToolManager toolManager;
        private readonly PlacementLevelEditorTool placementTool;
        private readonly TerrainToolPanelModel terrainPanel;
        private readonly SelectionLevelEditorTool selectionTool;
        private readonly LevelSnapSettings snapSettings;
        private readonly LevelEditorPersistenceCoordinator persistence;
        private readonly Action back;
        private readonly Action togglePreview;
        private readonly Action testPlay;
        private readonly Action createNew;
        private readonly Action loadMainLevel;
        private readonly Action frameSelection;
        private readonly Action frameLevel;
        private readonly Action<string> focusEntity;
        private readonly Action<string, string, string, string> applyTransform;
        private readonly Action<string, string, string, string> applyPlayerStart;
        private readonly Action addInteractionPoint;
        private readonly Action<string, string, string, string, string> applyInteractionPoint;
        private readonly Action deleteInteractionPoint;
        private readonly Action<string, string, string> applyDestructibleDefaults;
        private readonly Action<string> addScenarioActor;
        private readonly Action<
            string,
            string,
            string,
            string,
            string,
            bool,
            bool,
            bool> applyScenarioActor;
        private readonly Action<string> deleteScenarioActor;
        private readonly Action<string> placeScenarioActorAtView;
        private readonly Action<string, bool, string, string, bool> applyScenarioProp;
        private readonly Action<
            string,
            string,
            bool,
            string,
            string,
            string,
            string> applyScenarioObjective;
        private readonly Action<
            string,
            bool,
            string,
            string,
            string,
            string,
            string,
            string,
            string,
            string,
            string,
            bool> applyScenarioVehicle;
        private Vector2 paletteScroll;
        private Vector2 inspectorScroll;
        private string xText = "0";
        private string yText = "0";
        private string zText = "0";
        private string yawText = "0";
        private string playerStartXText = "0";
        private string playerStartYText = "0";
        private string playerStartZText = "0";
        private string playerStartYawText = "0";
        private string interactionType = "objective";
        private string interactionXText = "0";
        private string interactionYText = "0";
        private string interactionZText = "0";
        private string interactionRadiusText = "0.5";
        private string destructibleState = "intact";
        private string destructibleIntegrity = "10";
        private bool destructibleEnabled = true;
        private string lastInteractionSelectionId = string.Empty;
        private string lastDestructibleEntityId = string.Empty;
        private string paletteSearch = string.Empty;
        private string paletteCategory = string.Empty;
        private readonly HashSet<string> collapsedPaletteCategories =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private string hierarchySearch = string.Empty;
        private int leftPanel;
        private string selectedScenarioActorId = string.Empty;
        private string scenarioXText = "0";
        private string scenarioYText = "0";
        private string scenarioZText = "0";
        private string scenarioYawText = "0";
        private bool scenarioPlayerControlled;
        private bool scenarioInitiallySelected;
        private bool scenarioPrimaryTarget;
        private string lastScenarioPropEntityId = string.Empty;
        private bool scenarioPropEnabled;
        private string scenarioPropMassText = "25";
        private string scenarioPropSize = "medium";
        private bool scenarioPropStartsEncounter;
        private string lastScenarioObjectiveKey = string.Empty;
        private bool scenarioObjectiveEnabled;
        private string scenarioObjectiveDisplayName = "Objective";
        private string scenarioObjectiveActiveText = "Complete the objective";
        private string scenarioObjectiveCompletedText = "Objective complete";
        private string scenarioObjectiveCostText = "1";
        private string lastScenarioVehicleEntityId = string.Empty;
        private bool scenarioVehicleEnabled;
        private string scenarioVehicleMaximumSpeedText = "12";
        private string scenarioVehicleAccelerationText = "3";
        private string scenarioVehicleBrakingText = "4";
        private string scenarioVehicleLowTurnText = "45";
        private string scenarioVehicleHighTurnText = "15";
        private string scenarioVehicleBaseRadiusText = "2";
        private string scenarioVehicleRadiusFactorText = "0.25";
        private string scenarioVehicleStartingSpeedText = "0";
        private string scenarioVehicleOccupantId = string.Empty;
        private bool scenarioVehicleStartsEncounter;
        private bool showControls;

        public LevelEditorGui(
            LevelEditorWorkspace workspace,
            LevelSelectionModel selection,
            LevelArchetypeCatalog catalog,
            ScenarioAuthoringCatalog scenarioCatalog,
            LevelEditorToolManager toolManager,
            PlacementLevelEditorTool placementTool,
            TerrainToolPanelModel terrainPanel,
            SelectionLevelEditorTool selectionTool,
            LevelSnapSettings snapSettings,
            LevelEditorPersistenceCoordinator persistence,
            Action back,
            Action togglePreview,
            Action testPlay,
            Action createNew,
            Action loadMainLevel,
            Action frameSelection,
            Action frameLevel,
            Action<string> focusEntity,
            Action<string, string, string, string> applyTransform,
            Action<string, string, string, string> applyPlayerStart,
            Action addInteractionPoint,
            Action<string, string, string, string, string> applyInteractionPoint,
            Action deleteInteractionPoint,
            Action<string, string, string> applyDestructibleDefaults,
            Action<string> addScenarioActor,
            Action<string, string, string, string, string, bool, bool, bool>
                applyScenarioActor,
            Action<string> deleteScenarioActor,
            Action<string> placeScenarioActorAtView,
            Action<string, bool, string, string, bool> applyScenarioProp,
            Action<string, string, bool, string, string, string, string>
                applyScenarioObjective,
            Action<string, bool, string, string, string, string, string, string,
                string, string, string, bool> applyScenarioVehicle)
        {
            this.workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
            this.selection = selection ?? throw new ArgumentNullException(nameof(selection));
            this.catalog = catalog != null ? catalog : throw new ArgumentNullException(nameof(catalog));
            this.scenarioCatalog = scenarioCatalog
                ?? throw new ArgumentNullException(nameof(scenarioCatalog));
            this.toolManager = toolManager ?? throw new ArgumentNullException(nameof(toolManager));
            this.placementTool = placementTool ?? throw new ArgumentNullException(nameof(placementTool));
            this.terrainPanel = terrainPanel ?? throw new ArgumentNullException(nameof(terrainPanel));
            this.selectionTool = selectionTool ?? throw new ArgumentNullException(nameof(selectionTool));
            this.snapSettings = snapSettings ?? throw new ArgumentNullException(nameof(snapSettings));
            this.persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
            this.back = back ?? throw new ArgumentNullException(nameof(back));
            this.togglePreview = togglePreview ?? throw new ArgumentNullException(nameof(togglePreview));
            this.testPlay = testPlay ?? throw new ArgumentNullException(nameof(testPlay));
            this.createNew = createNew ?? throw new ArgumentNullException(nameof(createNew));
            this.loadMainLevel = loadMainLevel
                ?? throw new ArgumentNullException(nameof(loadMainLevel));
            this.frameSelection = frameSelection
                ?? throw new ArgumentNullException(nameof(frameSelection));
            this.frameLevel = frameLevel ?? throw new ArgumentNullException(nameof(frameLevel));
            this.focusEntity = focusEntity
                ?? throw new ArgumentNullException(nameof(focusEntity));
            this.applyTransform = applyTransform ?? throw new ArgumentNullException(nameof(applyTransform));
            this.applyPlayerStart = applyPlayerStart
                ?? throw new ArgumentNullException(nameof(applyPlayerStart));
            this.addInteractionPoint = addInteractionPoint
                ?? throw new ArgumentNullException(nameof(addInteractionPoint));
            this.applyInteractionPoint = applyInteractionPoint
                ?? throw new ArgumentNullException(nameof(applyInteractionPoint));
            this.deleteInteractionPoint = deleteInteractionPoint
                ?? throw new ArgumentNullException(nameof(deleteInteractionPoint));
            this.applyDestructibleDefaults = applyDestructibleDefaults
                ?? throw new ArgumentNullException(nameof(applyDestructibleDefaults));
            this.addScenarioActor = addScenarioActor
                ?? throw new ArgumentNullException(nameof(addScenarioActor));
            this.applyScenarioActor = applyScenarioActor
                ?? throw new ArgumentNullException(nameof(applyScenarioActor));
            this.deleteScenarioActor = deleteScenarioActor
                ?? throw new ArgumentNullException(nameof(deleteScenarioActor));
            this.placeScenarioActorAtView = placeScenarioActorAtView
                ?? throw new ArgumentNullException(nameof(placeScenarioActorAtView));
            this.applyScenarioProp = applyScenarioProp
                ?? throw new ArgumentNullException(nameof(applyScenarioProp));
            this.applyScenarioObjective = applyScenarioObjective
                ?? throw new ArgumentNullException(nameof(applyScenarioObjective));
            this.applyScenarioVehicle = applyScenarioVehicle
                ?? throw new ArgumentNullException(nameof(applyScenarioVehicle));
        }

        public void Draw(
            bool previewMode,
            LevelEntityView selectedView,
            IReadOnlyList<LevelValidationIssue> validationIssues,
            string statusMessage)
        {
            DrawToolbar(previewMode);
            if (!previewMode)
            {
                DrawPalette();
                DrawInspector(selectedView, validationIssues);
            }

            DrawStatusBar(statusMessage);
        }

        public bool IsPointerOverInterface(Vector2 screenPosition)
        {
            float guiY = Screen.height - screenPosition.y;
            if (guiY <= ToolbarHeight || guiY >= Screen.height - 30f)
            {
                return true;
            }

            return screenPosition.x <= PaletteWidth
                || screenPosition.x >= Screen.width - InspectorWidth;
        }

        public void SyncTransformFields(LevelTransformData value)
        {
            xText = value.position.x.ToString("0.###", CultureInfo.InvariantCulture);
            yText = value.position.y.ToString("0.###", CultureInfo.InvariantCulture);
            zText = value.position.z.ToString("0.###", CultureInfo.InvariantCulture);
            yawText = value.yawDegrees.ToString("0.###", CultureInfo.InvariantCulture);
        }

        public void SyncPlayerStartFields(LevelTransformData value)
        {
            playerStartXText = value.position.x.ToString("0.###", CultureInfo.InvariantCulture);
            playerStartYText = value.position.y.ToString("0.###", CultureInfo.InvariantCulture);
            playerStartZText = value.position.z.ToString("0.###", CultureInfo.InvariantCulture);
            playerStartYawText = value.yawDegrees.ToString("0.###", CultureInfo.InvariantCulture);
        }

        public void SelectScenarioActor(string actorId)
        {
            selectedScenarioActorId = actorId ?? string.Empty;
            LevelScenarioActorData actor = workspace.CreateSnapshot().scenario.actors
                .FirstOrDefault(candidate => string.Equals(
                    candidate?.id,
                    selectedScenarioActorId,
                    StringComparison.Ordinal));
            if (actor != null)
                SyncScenarioActorFields(actor);
        }

        public void SyncScenarioActorFields(LevelScenarioActorData actor)
        {
            if (actor == null)
                return;
            selectedScenarioActorId = actor.id;
            scenarioXText = actor.transform.position.x.ToString(
                "0.###",
                CultureInfo.InvariantCulture);
            scenarioYText = actor.transform.position.y.ToString(
                "0.###",
                CultureInfo.InvariantCulture);
            scenarioZText = actor.transform.position.z.ToString(
                "0.###",
                CultureInfo.InvariantCulture);
            scenarioYawText = actor.transform.yawDegrees.ToString(
                "0.###",
                CultureInfo.InvariantCulture);
            scenarioPlayerControlled = actor.playerControlled;
            scenarioInitiallySelected = actor.initiallySelected;
            scenarioPrimaryTarget = actor.primaryTarget;
        }

        public void SyncScenarioFields(LevelDocument document)
        {
            lastScenarioPropEntityId = string.Empty;
            lastScenarioObjectiveKey = string.Empty;
            lastScenarioVehicleEntityId = string.Empty;
            LevelScenarioActorData player = document?.scenario?
                .FindInitiallySelectedPlayer();
            if (player != null)
                SyncPlayerStartFields(player.transform);
            LevelScenarioActorData selected = document?.scenario?.actors
                .FirstOrDefault(actor => string.Equals(
                    actor?.id,
                    selectedScenarioActorId,
                    StringComparison.Ordinal));
            if (selected != null)
                SyncScenarioActorFields(selected);
            else
                selectedScenarioActorId = string.Empty;
        }

        private void DrawToolbar(bool previewMode)
        {
            GUILayout.BeginArea(new Rect(0f, 0f, Screen.width, ToolbarHeight), GUI.skin.box);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("BACK", GUILayout.Width(72f), GUILayout.Height(30f)))
            {
                back();
            }

            if (GUILayout.Button(
                previewMode ? "RETURN TO EDIT" : "LEVEL PREVIEW",
                GUILayout.Width(128f),
                GUILayout.Height(30f)))
            {
                togglePreview();
            }

            GUI.enabled = !previewMode;
            if (GUILayout.Button("TEST PLAY", GUILayout.Width(92f), GUILayout.Height(30f)))
            {
                testPlay();
            }

            GUI.enabled = !previewMode;
            if (GUILayout.Button("NEW", GUILayout.Width(60f), GUILayout.Height(30f)))
            {
                createNew();
            }

            if (GUILayout.Button("LOAD MAIN", GUILayout.Width(100f), GUILayout.Height(30f)))
            {
                loadMainLevel();
            }

            GUI.enabled = workspace.CanUndo && !previewMode;
            if (GUILayout.Button("UNDO", GUILayout.Width(64f), GUILayout.Height(30f)))
            {
                workspace.Undo();
            }

            GUI.enabled = workspace.CanRedo && !previewMode;
            if (GUILayout.Button("REDO", GUILayout.Width(64f), GUILayout.Height(30f)))
            {
                workspace.Redo();
            }

            GUI.enabled = !previewMode && selection.Primary != null;
            if (GUILayout.Button("FRAME", GUILayout.Width(68f), GUILayout.Height(30f)))
            {
                frameSelection();
            }

            GUI.enabled = !previewMode && selection.Targets.Count > 0;
            if (GUILayout.Button("DUPLICATE", GUILayout.Width(86f), GUILayout.Height(30f)))
            {
                selectionTool.DuplicateSelection();
            }

            GUI.enabled = !previewMode;
            if (GUILayout.Button("FRAME ALL", GUILayout.Width(86f), GUILayout.Height(30f)))
            {
                frameLevel();
            }

            GUI.enabled = true;
            if (GUILayout.Button(
                showControls ? "HIDE HELP" : "CONTROLS",
                GUILayout.Width(88f),
                GUILayout.Height(30f)))
            {
                showControls = !showControls;
            }

            GUI.enabled = true;
            GUILayout.FlexibleSpace();
            GUILayout.Label(
                previewMode
                    ? "LEVEL PREVIEW — AUTHORING LOCKED"
                    : workspace.IsDirty ? "UNSAVED DRAFT" : "SAVED",
                GUILayout.Height(30f));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUI.enabled = !previewMode;
            snapSettings.Enabled = GUILayout.Toggle(
                snapSettings.Enabled,
                "SNAP",
                GUI.skin.button,
                GUILayout.Width(68f),
                GUILayout.Height(30f));
            if (GUILayout.Button("SAVE DRAFT", GUILayout.Width(96f), GUILayout.Height(30f)))
            {
                persistence.SaveDraft(workspace);
            }

            GUI.enabled = !previewMode && persistence.HasDraft;
            if (GUILayout.Button("LOAD DRAFT", GUILayout.Width(96f), GUILayout.Height(30f)))
            {
                persistence.LoadDraft();
            }

            GUI.enabled = !previewMode;
            if (GUILayout.Button("EXPORT", GUILayout.Width(76f), GUILayout.Height(30f)))
            {
                persistence.Export(workspace);
            }

            if (GUILayout.Button("IMPORT", GUILayout.Width(76f), GUILayout.Height(30f)))
            {
                persistence.RequestImport();
            }

            GUI.enabled = true;
            if (selectionTool.IsDragging)
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label(selectionTool.DragFeedback, GUI.skin.box, GUILayout.Height(30f));
            }

            GUI.enabled = true;
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void DrawPalette()
        {
            GUILayout.BeginArea(
                new Rect(0f, ToolbarHeight, PaletteWidth, Screen.height - ToolbarHeight - 30f),
                GUI.skin.box);
            paletteScroll = GUILayout.BeginScrollView(paletteScroll);
            GUILayout.BeginHorizontal();
            Color panelToggleColor = GUI.backgroundColor;
            DrawLeftPanelTab("LIBRARY", 0, panelToggleColor);
            DrawLeftPanelTab("SCENE", 1, panelToggleColor);
            DrawLeftPanelTab("SCENARIO", 2, panelToggleColor);
            GUI.backgroundColor = panelToggleColor;
            GUILayout.EndHorizontal();

            if (leftPanel == 1)
            {
                DrawHierarchy();
                GUILayout.EndScrollView();
                GUILayout.EndArea();
                return;
            }

            if (leftPanel == 2)
            {
                DrawScenario();
                GUILayout.EndScrollView();
                GUILayout.EndArea();
                return;
            }

            GUILayout.Label("TOOLS");
            Color previous = GUI.backgroundColor;
            if (toolManager.ActiveTool == selectionTool)
            {
                GUI.backgroundColor = new Color(0.2f, 0.75f, 1f);
            }

            if (GUILayout.Button("SELECT / MOVE", GUILayout.Height(34f)))
            {
                toolManager.Activate(SelectionLevelEditorTool.ToolId);
            }
            GUI.backgroundColor = previous;

            if (showControls)
            {
                GUILayout.Space(6f);
                GUILayout.Label("CAMERA", GUI.skin.box);
                GUILayout.Label("WASD / arrows — pan (Shift: fast)");
                GUILayout.Label("Right-drag — orbit; middle-drag — pan");
                GUILayout.Label("Wheel — zoom; F — frame; Home — frame all");
                GUILayout.Label("EDITING", GUI.skin.box);
                GUILayout.Label("Click — select; Ctrl-click — add/remove");
                GUILayout.Label("R — rotate; Delete — remove; Esc — cancel");
                GUILayout.Label("Ctrl+C / Ctrl+V — copy / paste; Ctrl+D — duplicate");
                GUILayout.Label("Ctrl+Z / Ctrl+Y — undo / redo");
            }

            GUILayout.Space(8f);
            GUILayout.Label("TERRAIN HEIGHT");
            GUILayout.BeginHorizontal();
            if (terrainPanel.IsRaiseActive)
            {
                GUI.backgroundColor = new Color(0.3f, 0.9f, 0.4f);
            }
            if (GUILayout.Button("RAISE", GUILayout.Height(30f)))
            {
                terrainPanel.ActivateRaise();
            }
            GUI.backgroundColor = previous;

            if (terrainPanel.IsLowerActive)
            {
                GUI.backgroundColor = new Color(1f, 0.4f, 0.25f);
            }
            if (GUILayout.Button("LOWER", GUILayout.Height(30f)))
            {
                terrainPanel.ActivateLower();
            }
            GUI.backgroundColor = previous;
            GUILayout.EndHorizontal();
            if (GUILayout.Button("FRAME TERRAIN", GUILayout.Height(30f)))
            {
                terrainPanel.FrameTerrain();
            }
            GUILayout.Label($"Radius: {terrainPanel.RadiusInSamples} samples");
            terrainPanel.RadiusInSamples = Mathf.RoundToInt(GUILayout.HorizontalSlider(
                terrainPanel.RadiusInSamples,
                1f,
                16f));
            GUILayout.Label($"Strength: {terrainPanel.QuantizedStrength} steps");
            terrainPanel.QuantizedStrength = Mathf.RoundToInt(GUILayout.HorizontalSlider(
                terrainPanel.QuantizedStrength,
                1f,
                20f));
            GUILayout.Space(8f);
            GUILayout.Label("ARCHETYPES");
            GUILayout.Label("Choose a piece, then click in the world.");
            GUILayout.BeginHorizontal();
            GUILayout.Label("Search", GUILayout.Width(50f));
            paletteSearch = GUILayout.TextField(paletteSearch ?? string.Empty);
            GUILayout.EndHorizontal();

            IReadOnlyList<string> categories = catalog.Entries
                .Where(entry => entry != null && !string.IsNullOrWhiteSpace(entry.Category))
                .Select(entry => entry.Category)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            GUILayout.BeginHorizontal();
            DrawPaletteCategoryButton("ALL", string.Empty, previous);
            foreach (string entryCategory in categories)
            {
                DrawPaletteCategoryButton(entryCategory, entryCategory, previous);
            }
            GUILayout.EndHorizontal();

            IReadOnlyList<LevelArchetypeDefinition> filteredEntries = catalog.Entries
                .Where(MatchesPaletteFilter)
                .ToArray();
            if (filteredEntries.Count == 0)
            {
                GUILayout.Label("No archetypes match this filter.");
            }

            bool searchIsActive = !string.IsNullOrWhiteSpace(paletteSearch);
            foreach (IGrouping<string, LevelArchetypeDefinition> group in filteredEntries
                .GroupBy(entry => string.IsNullOrWhiteSpace(entry.Category)
                    ? "Uncategorized"
                    : entry.Category, StringComparer.OrdinalIgnoreCase)
                .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
            {
                string category = group.Key;
                bool isCollapsed = collapsedPaletteCategories.Contains(category);
                string header = $"{(isCollapsed ? "▶" : "▼")} {category.ToUpperInvariant()} ({group.Count()})";
                GUILayout.Space(8f);
                if (GUILayout.Button(header, GUI.skin.box, GUILayout.Height(26f)))
                {
                    if (isCollapsed)
                        collapsedPaletteCategories.Remove(category);
                    else
                        collapsedPaletteCategories.Add(category);
                }

                if (isCollapsed && !searchIsActive)
                    continue;

                foreach (LevelArchetypeDefinition entry in group.OrderBy(
                    entry => entry.DisplayName,
                    StringComparer.OrdinalIgnoreCase))
                {
                    bool active = toolManager.ActiveTool == placementTool
                        && ReferenceEquals(placementTool.Archetype, entry);
                    previous = GUI.backgroundColor;
                    if (active)
                    {
                        GUI.backgroundColor = new Color(0.95f, 0.55f, 0.2f);
                    }

                    if (GUILayout.Button(entry.DisplayName, GUILayout.Height(34f)))
                    {
                        placementTool.SelectArchetype(entry);
                        toolManager.Activate(PlacementLevelEditorTool.ToolId);
                    }

                    GUI.backgroundColor = previous;
                }
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawLeftPanelTab(string label, int panel, Color previous)
        {
            if (leftPanel == panel)
                GUI.backgroundColor = new Color(0.2f, 0.75f, 1f);
            if (GUILayout.Button(label, GUILayout.Height(30f)))
                leftPanel = panel;
            GUI.backgroundColor = previous;
        }

        private void DrawScenario()
        {
            LevelDocument document = workspace.CreateSnapshot();
            GUILayout.Space(8f);
            GUILayout.Label("SCENARIO COMPOSITION", GUI.skin.box);
            GUILayout.Label(
                "Actors and gameplay links here are the exact data used by Test Play.");
            GUILayout.Space(8f);
            GUILayout.Label("ADD ACTOR AT CAMERA FOCUS");
            string previousGroup = null;
            foreach (ScenarioActorTemplateDefinition template in scenarioCatalog.ActorTemplates)
            {
                string group = template.PlayerTemplate ? "PLAYER PARTY" : "OPPONENTS";
                if (!string.Equals(previousGroup, group, StringComparison.Ordinal))
                {
                    previousGroup = group;
                    GUILayout.Label(group, GUI.skin.box);
                }

                if (GUILayout.Button($"+ {template.DisplayName}", GUILayout.Height(30f)))
                    addScenarioActor(template.TemplateId);
            }

            GUILayout.Space(10f);
            GUILayout.Label($"ACTORS ({document.scenario.actors.Count})", GUI.skin.box);
            foreach (LevelScenarioActorData actor in document.scenario.actors
                .Where(actor => actor != null)
                .OrderByDescending(actor => actor.playerControlled)
                .ThenBy(actor => actor.id, StringComparer.Ordinal))
            {
                Color previous = GUI.backgroundColor;
                if (string.Equals(
                        selectedScenarioActorId,
                        actor.id,
                        StringComparison.Ordinal))
                {
                    GUI.backgroundColor = new Color(0.2f, 0.75f, 1f);
                }

                ScenarioActorTemplateDefinition template =
                    scenarioCatalog.GetActor(actor.templateId);
                string role = actor.playerControlled
                    ? actor.initiallySelected ? "PLAYER • SELECTED" : "PLAYER"
                    : actor.primaryTarget ? "TARGET" : "ENEMY";
                if (GUILayout.Button(
                        $"{template.DisplayName}\n{role}",
                        GUILayout.Height(42f)))
                {
                    SyncScenarioActorFields(actor);
                }
                GUI.backgroundColor = previous;
            }

            LevelScenarioActorData selected = document.scenario.actors.FirstOrDefault(actor =>
                string.Equals(actor?.id, selectedScenarioActorId, StringComparison.Ordinal));
            if (selected == null)
            {
                GUILayout.Space(8f);
                GUILayout.Label("Choose an actor to edit its start position and role.");
                DrawScenarioLinkSummary(document.scenario);
                return;
            }

            GUILayout.Space(10f);
            GUILayout.Label("SELECTED ACTOR", GUI.skin.box);
            GUILayout.Label($"ID: {selected.id}");
            GUILayout.Label($"Template: {selected.templateId}");
            DrawLabeledField("X", ref scenarioXText);
            DrawLabeledField("Y", ref scenarioYText);
            DrawLabeledField("Z", ref scenarioZText);
            DrawLabeledField("Yaw", ref scenarioYawText);
            scenarioPlayerControlled = GUILayout.Toggle(
                scenarioPlayerControlled,
                "Player controlled");
            if (scenarioPlayerControlled)
                scenarioPrimaryTarget = false;
            else
                scenarioInitiallySelected = false;
            GUI.enabled = scenarioPlayerControlled;
            scenarioInitiallySelected = GUILayout.Toggle(
                scenarioInitiallySelected,
                "Initially selected party actor");
            GUI.enabled = !scenarioPlayerControlled;
            scenarioPrimaryTarget = GUILayout.Toggle(
                scenarioPrimaryTarget,
                "Primary target");
            GUI.enabled = true;

            if (GUILayout.Button("APPLY ACTOR", GUILayout.Height(32f)))
            {
                applyScenarioActor(
                    selected.id,
                    scenarioXText,
                    scenarioYText,
                    scenarioZText,
                    scenarioYawText,
                    scenarioPlayerControlled,
                    scenarioInitiallySelected,
                    scenarioPrimaryTarget);
            }

            if (GUILayout.Button("PLACE AT CAMERA FOCUS", GUILayout.Height(30f)))
                placeScenarioActorAtView(selected.id);

            Color deleteColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.7f, 0.25f, 0.2f);
            if (GUILayout.Button("DELETE ACTOR", GUILayout.Height(30f)))
                deleteScenarioActor(selected.id);
            GUI.backgroundColor = deleteColor;
            DrawScenarioLinkSummary(document.scenario);
        }

        private static void DrawScenarioLinkSummary(LevelScenarioData scenario)
        {
            GUILayout.Space(12f);
            GUILayout.Label("GAMEPLAY LINKS", GUI.skin.box);
            GUILayout.Label($"Objectives: {scenario.objectives.Count}");
            GUILayout.Label($"Physics props: {scenario.props.Count}");
            GUILayout.Label($"Vehicles: {scenario.vehicles.Count}");
        }

        private void DrawHierarchy()
        {
            LevelDocument document = workspace.CreateSnapshot();
            GUILayout.Space(8f);
            GUILayout.Label("SCENARIO", GUI.skin.box);
            LevelScenarioActorData selectedPlayer = document.scenario
                .FindInitiallySelectedPlayer();
            if (selectedPlayer != null)
            {
                LevelTransformData start = selectedPlayer.transform;
                GUILayout.Label(
                    $"PLAYER START  ({start.position.x:0.##}, {start.position.y:0.##}, {start.position.z:0.##})");
            }
            else
            {
                GUILayout.Label("PLAYER START  (NOT CONFIGURED)");
            }
            GUILayout.Label($"ACTORS  {document.scenario.actors.Count}");
            GUILayout.Label($"OBJECTIVES  {document.scenario.objectives.Count}");
            GUILayout.Label($"PHYSICS PROPS  {document.scenario.props.Count}");
            GUILayout.Label($"VEHICLES  {document.scenario.vehicles.Count}");
            if (GUILayout.Button("OPEN SCENARIO COMPOSITION", GUILayout.Height(28f)))
                leftPanel = 2;
            GUILayout.Space(8f);
            GUILayout.Label("LEVEL GEOMETRY", GUI.skin.box);
            GUILayout.Label($"TERRAIN SURFACES  {document.terrainSurfaces.Count}");
            GUILayout.Label($"ENTITIES  {document.entities.Count}");
            hierarchySearch = GUILayout.TextField(hierarchySearch ?? string.Empty);
            string previousCategory = null;
            int matches = 0;
            foreach (LevelEntity entity in document.entities
                .Where(MatchesHierarchyFilter)
                .OrderBy(EntityCategory, StringComparer.OrdinalIgnoreCase)
                .ThenBy(EntityDisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(entity => entity.id, StringComparer.Ordinal))
            {
                string category = EntityCategory(entity);
                if (!string.Equals(previousCategory, category, StringComparison.Ordinal))
                {
                    previousCategory = category;
                    GUILayout.Label(category.ToUpperInvariant(), GUI.skin.box);
                }

                matches++;
                Color previous = GUI.backgroundColor;
                if (string.Equals(selection.PrimaryEntityId, entity.id, StringComparison.Ordinal))
                {
                    GUI.backgroundColor = new Color(0.2f, 0.75f, 1f);
                }

                if (GUILayout.Button(EntityDisplayName(entity), GUILayout.Height(26f)))
                {
                    focusEntity(entity.id);
                }

                GUI.backgroundColor = previous;
            }

            if (matches == 0)
            {
                GUILayout.Label("No entities match this filter.");
            }
        }

        private bool MatchesHierarchyFilter(LevelEntity entity)
        {
            if (entity == null)
            {
                return false;
            }

            string search = hierarchySearch?.Trim();
            return string.IsNullOrEmpty(search)
                || EntityDisplayName(entity).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0
                || entity.id.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0
                || EntityCategory(entity).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private string EntityDisplayName(LevelEntity entity)
        {
            return catalog.TryGet(entity.archetypeId, out LevelArchetypeDefinition archetype)
                ? archetype.DisplayName
                : entity.archetypeId;
        }

        private string EntityCategory(LevelEntity entity)
        {
            return catalog.TryGet(entity.archetypeId, out LevelArchetypeDefinition archetype)
                && !string.IsNullOrWhiteSpace(archetype.Category)
                ? archetype.Category
                : "Unknown";
        }

        private void DrawPaletteCategoryButton(string label, string category, Color previous)
        {
            bool active = string.Equals(paletteCategory, category, StringComparison.OrdinalIgnoreCase);
            if (active)
            {
                GUI.backgroundColor = new Color(0.2f, 0.75f, 1f);
            }

            if (GUILayout.Button(label, GUILayout.Height(26f)))
            {
                paletteCategory = category;
            }

            GUI.backgroundColor = previous;
        }

        private bool MatchesPaletteFilter(LevelArchetypeDefinition entry)
        {
            if (entry == null || !string.Equals(
                    paletteCategory,
                    string.Empty,
                    StringComparison.OrdinalIgnoreCase)
                && !string.Equals(entry.Category, paletteCategory, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string search = paletteSearch?.Trim();
            return string.IsNullOrEmpty(search)
                || entry.DisplayName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0
                || entry.ArchetypeId.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0
                || entry.Category.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void DrawInspector(
            LevelEntityView selectedView,
            IReadOnlyList<LevelValidationIssue> validationIssues)
        {
            float left = Screen.width - InspectorWidth;
            GUILayout.BeginArea(
                new Rect(left, ToolbarHeight, InspectorWidth, Screen.height - ToolbarHeight - 30f),
                GUI.skin.box);
            inspectorScroll = GUILayout.BeginScrollView(inspectorScroll);
            GUILayout.Label("INSPECTOR");
            DrawPlayerStartInspector();
            if (selectedView == null)
            {
                GUILayout.Label("Click an entity to select and drag it across its current elevation.");
            }
            else
            {
                GUILayout.Label(selectedView.Archetype.DisplayName);
                GUILayout.Label($"ID: {selection.PrimaryEntityId}");
                LevelEntity entity = workspace.FindEntitySnapshot(selection.PrimaryEntityId);
                LevelSelectionTarget? primary = selection.Primary;
                if (selection.Targets.Count > 1)
                {
                    GUILayout.Label($"{selection.Targets.Count} entities selected");
                }
                GUILayout.Space(8f);
                DrawLabeledField("X", ref xText);
                DrawLabeledField("Y", ref yText);
                DrawLabeledField("Z", ref zText);
                DrawLabeledField("Yaw", ref yawText);
                if (GUILayout.Button("APPLY TRANSFORM", GUILayout.Height(34f)))
                {
                    applyTransform(xText, yText, zText, yawText);
                }

                GUILayout.BeginHorizontal();
                float angleSnap = selectedView.Archetype.PlacementRules.AngleSnap;
                if (GUILayout.Button($"ROTATE -{angleSnap:0.#}°"))
                {
                    selectionTool.RotateSelection(-angleSnap);
                }

                if (GUILayout.Button($"ROTATE +{angleSnap:0.#}°"))
                {
                    selectionTool.RotateSelection(angleSnap);
                }
                GUILayout.EndHorizontal();
                Color previous = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.7f, 0.25f, 0.2f);
                if (GUILayout.Button("DELETE", GUILayout.Height(34f)))
                {
                    selectionTool.DeleteSelection();
                }
                GUI.backgroundColor = previous;

                GUILayout.Space(12f);
                DrawInteractionInspector(entity, primary);
                DrawDestructibleInspector(selectedView, entity);
                DrawScenarioPropInspector(selectedView, entity);
                DrawScenarioVehicleInspector(selectedView, entity);
            }

            GUILayout.Space(16f);
            GUILayout.Label("VALIDATION");
            if (validationIssues == null || validationIssues.Count == 0)
            {
                GUILayout.Label("No validation issues.");
            }
            else
            {
                foreach (LevelValidationIssue issue in validationIssues.Take(8))
                {
                    string issueText = $"{issue.Severity}: {issue.Message}";
                    if (string.IsNullOrWhiteSpace(issue.EntityId))
                    {
                        GUILayout.Label(issueText);
                    }
                    else if (GUILayout.Button(issueText))
                    {
                        focusEntity(issue.EntityId);
                    }
                }

                if (validationIssues.Count > 8)
                {
                    GUILayout.Label($"…and {validationIssues.Count - 8} more.");
                }
            }

            GUILayout.Space(16f);
            GUILayout.Label("PORTABLE FILES");
            if (persistence.UsesBrowserFileDialog)
            {
                GUILayout.Label("Import opens the browser file picker. Export downloads a JSON file.");
            }
            else
            {
                GUILayout.Label("Desktop import path:");
                persistence.DesktopImportPath = GUILayout.TextField(persistence.DesktopImportPath);
                GUILayout.Label("Exports are written beneath the application's persistent-data folder.");
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawPlayerStartInspector()
        {
            GUILayout.Label("SCENARIO PLAYER START", GUI.skin.box);
            DrawLabeledField("X", ref playerStartXText);
            DrawLabeledField("Y", ref playerStartYText);
            DrawLabeledField("Z", ref playerStartZText);
            DrawLabeledField("Yaw", ref playerStartYawText);
            if (GUILayout.Button("APPLY PLAYER START", GUILayout.Height(30f)))
            {
                applyPlayerStart(
                    playerStartXText,
                    playerStartYText,
                    playerStartZText,
                    playerStartYawText);
            }

            GUILayout.Space(12f);
        }

        private void DrawInteractionInspector(LevelEntity entity, LevelSelectionTarget? primary)
        {
            GUILayout.Label("INTERACTION POINTS", GUI.skin.box);
            if (entity == null)
            {
                return;
            }

            if (primary != null && primary.Value.Kind == LevelSelectionKind.InteractionPoint)
            {
                InteractionPointData point = entity.interactionPoints.FirstOrDefault(candidate =>
                    string.Equals(candidate?.id, primary.Value.ElementId, StringComparison.Ordinal));
                if (point != null)
                {
                    SyncInteractionFields(point);
                    GUILayout.Label($"ID: {point.id}");
                    interactionType = GUILayout.SelectionGrid(
                        interactionType == "doorway" ? 1 : 0,
                        new[] { "OBJECTIVE", "DOORWAY" },
                        2) == 1 ? "doorway" : "objective";
                    DrawLabeledField("X", ref interactionXText);
                    DrawLabeledField("Y", ref interactionYText);
                    DrawLabeledField("Z", ref interactionZText);
                    DrawLabeledField("Radius", ref interactionRadiusText);
                    if (GUILayout.Button("APPLY INTERACTION", GUILayout.Height(30f)))
                    {
                        applyInteractionPoint(
                            interactionType,
                            interactionXText,
                            interactionYText,
                            interactionZText,
                            interactionRadiusText);
                    }

                    Color previous = GUI.backgroundColor;
                    GUI.backgroundColor = new Color(0.7f, 0.25f, 0.2f);
                    if (GUILayout.Button("DELETE INTERACTION", GUILayout.Height(30f)))
                    {
                        deleteInteractionPoint();
                    }
                    GUI.backgroundColor = previous;
                    DrawScenarioObjectiveInspector(entity, point);
                    return;
                }
            }

            if (GUILayout.Button("ADD INTERACTION", GUILayout.Height(30f)))
            {
                addInteractionPoint();
            }
            GUILayout.Label("Select a pink world handle to edit an existing point.");
        }

        private void DrawDestructibleInspector(LevelEntityView view, LevelEntity entity)
        {
            if ((view.Archetype.Capabilities & LevelArchetypeCapabilities.Destructible) == 0)
            {
                return;
            }

            GUILayout.Space(12f);
            GUILayout.Label("DESTRUCTIBLE DEFAULTS", GUI.skin.box);
            DestructibleInstanceData data = entity?.destructible;
            if (!string.Equals(lastDestructibleEntityId, entity?.id, StringComparison.Ordinal))
            {
                lastDestructibleEntityId = entity?.id ?? string.Empty;
                if (data != null)
                {
                    destructibleEnabled = data.enabled;
                    destructibleState = data.initialState;
                    destructibleIntegrity = data.integrity.ToString("0.###", CultureInfo.InvariantCulture);
                }
                else
                {
                    destructibleEnabled = true;
                    destructibleState = "intact";
                    destructibleIntegrity = "10";
                }
            }

            destructibleEnabled = GUILayout.Toggle(destructibleEnabled, "ENABLED");
            destructibleState = GUILayout.SelectionGrid(
                DestructibleStateIndex(destructibleState),
                new[] { "INTACT", "DAMAGED", "DESTROYED" },
                3) switch
            {
                1 => "damaged",
                2 => "destroyed",
                _ => "intact",
            };
            DrawLabeledField("Integrity", ref destructibleIntegrity);
            if (GUILayout.Button("APPLY DESTRUCTIBLE", GUILayout.Height(30f)))
            {
                applyDestructibleDefaults(
                    destructibleEnabled ? "true" : "false",
                    destructibleState,
                    destructibleIntegrity);
            }
        }

        private void DrawScenarioPropInspector(LevelEntityView view, LevelEntity entity)
        {
            if ((view.Archetype.Capabilities & LevelArchetypeCapabilities.Destructible) == 0)
                return;

            LevelScenarioPropData configured = workspace.CreateSnapshot().scenario.props
                .FirstOrDefault(prop => string.Equals(
                    prop?.entityId,
                    entity.id,
                    StringComparison.Ordinal));
            if (!string.Equals(lastScenarioPropEntityId, entity.id, StringComparison.Ordinal))
            {
                lastScenarioPropEntityId = entity.id;
                scenarioPropEnabled = configured != null;
                scenarioPropMassText = (configured?.mass ?? 25f).ToString(
                    "0.###",
                    CultureInfo.InvariantCulture);
                scenarioPropSize = configured?.sizeClass ?? "medium";
                scenarioPropStartsEncounter = configured?.startsEncounterOnAttack ?? false;
            }

            GUILayout.Space(12f);
            GUILayout.Label("SCENARIO PHYSICS PROP", GUI.skin.box);
            scenarioPropEnabled = GUILayout.Toggle(
                scenarioPropEnabled,
                "Participates in displacement and combat");
            GUI.enabled = scenarioPropEnabled;
            DrawLabeledField("Mass", ref scenarioPropMassText);
            scenarioPropSize = GUILayout.SelectionGrid(
                ScenarioSizeIndex(scenarioPropSize),
                new[] { "SMALL", "MEDIUM", "LARGE", "HUGE" },
                4) switch
            {
                0 => "small",
                2 => "large",
                3 => "huge",
                _ => "medium",
            };
            scenarioPropStartsEncounter = GUILayout.Toggle(
                scenarioPropStartsEncounter,
                "Attack starts encounter");
            GUI.enabled = true;
            if (GUILayout.Button("APPLY PHYSICS PROP", GUILayout.Height(30f)))
            {
                applyScenarioProp(
                    entity.id,
                    scenarioPropEnabled,
                    scenarioPropMassText,
                    scenarioPropSize,
                    scenarioPropStartsEncounter);
            }
        }

        private void DrawScenarioObjectiveInspector(
            LevelEntity entity,
            InteractionPointData point)
        {
            string key = entity.id + ":" + point.id;
            LevelScenarioObjectiveData configured = workspace.CreateSnapshot().scenario.objectives
                .FirstOrDefault(objective =>
                    string.Equals(objective?.entityId, entity.id, StringComparison.Ordinal)
                    && string.Equals(
                        objective?.interactionPointId,
                        point.id,
                        StringComparison.Ordinal));
            if (!string.Equals(lastScenarioObjectiveKey, key, StringComparison.Ordinal))
            {
                lastScenarioObjectiveKey = key;
                scenarioObjectiveEnabled = configured != null;
                scenarioObjectiveDisplayName = configured?.displayName ?? "Objective";
                scenarioObjectiveActiveText = configured?.activeHudText
                    ?? "Complete the objective";
                scenarioObjectiveCompletedText = configured?.completedHudText
                    ?? "Objective complete";
                scenarioObjectiveCostText = (configured?.actionPointCost ?? 1).ToString(
                    CultureInfo.InvariantCulture);
            }

            GUILayout.Space(12f);
            GUILayout.Label("SCENARIO OBJECTIVE", GUI.skin.box);
            GUI.enabled = string.Equals(point.type, "objective", StringComparison.Ordinal);
            scenarioObjectiveEnabled = GUILayout.Toggle(
                scenarioObjectiveEnabled,
                "Use this point as an objective");
            GUI.enabled = scenarioObjectiveEnabled
                && string.Equals(point.type, "objective", StringComparison.Ordinal);
            GUILayout.Label("Display name");
            scenarioObjectiveDisplayName = GUILayout.TextField(scenarioObjectiveDisplayName);
            GUILayout.Label("Active HUD text");
            scenarioObjectiveActiveText = GUILayout.TextField(scenarioObjectiveActiveText);
            GUILayout.Label("Completed HUD text");
            scenarioObjectiveCompletedText = GUILayout.TextField(scenarioObjectiveCompletedText);
            DrawLabeledField("AP cost", ref scenarioObjectiveCostText);
            GUI.enabled = true;
            if (GUILayout.Button("APPLY OBJECTIVE", GUILayout.Height(30f)))
            {
                applyScenarioObjective(
                    entity.id,
                    point.id,
                    scenarioObjectiveEnabled,
                    scenarioObjectiveDisplayName,
                    scenarioObjectiveActiveText,
                    scenarioObjectiveCompletedText,
                    scenarioObjectiveCostText);
            }
            if (!string.Equals(point.type, "objective", StringComparison.Ordinal))
                GUILayout.Label("Change the interaction type to Objective before enabling this link.");
        }

        private void DrawScenarioVehicleInspector(LevelEntityView view, LevelEntity entity)
        {
            if ((view.Archetype.Capabilities & LevelArchetypeCapabilities.Vehicle) == 0)
                return;

            LevelScenarioVehicleData configured = workspace.CreateSnapshot().scenario.vehicles
                .FirstOrDefault(vehicle => string.Equals(
                    vehicle?.entityId,
                    entity.id,
                    StringComparison.Ordinal));
            if (!string.Equals(lastScenarioVehicleEntityId, entity.id, StringComparison.Ordinal))
            {
                lastScenarioVehicleEntityId = entity.id;
                scenarioVehicleEnabled = configured != null;
                scenarioVehicleMaximumSpeedText = (configured?.maximumSpeed ?? 12f)
                    .ToString("0.###", CultureInfo.InvariantCulture);
                scenarioVehicleAccelerationText = (configured?.accelerationPerTurn ?? 3f)
                    .ToString("0.###", CultureInfo.InvariantCulture);
                scenarioVehicleBrakingText = (configured?.brakingPerTurn ?? 4f)
                    .ToString("0.###", CultureInfo.InvariantCulture);
                scenarioVehicleLowTurnText = (configured?.lowSpeedTurnDegrees ?? 45f)
                    .ToString("0.###", CultureInfo.InvariantCulture);
                scenarioVehicleHighTurnText = (configured?.highSpeedTurnDegrees ?? 15f)
                    .ToString("0.###", CultureInfo.InvariantCulture);
                scenarioVehicleBaseRadiusText = (configured?.baseTurningRadius ?? 2f)
                    .ToString("0.###", CultureInfo.InvariantCulture);
                scenarioVehicleRadiusFactorText = (configured?.speedTurningRadiusFactor ?? 0.25f)
                    .ToString("0.###", CultureInfo.InvariantCulture);
                scenarioVehicleStartingSpeedText = (configured?.startingSpeed ?? 0f)
                    .ToString("0.###", CultureInfo.InvariantCulture);
                scenarioVehicleOccupantId = configured?.startingOccupantActorId ?? string.Empty;
                scenarioVehicleStartsEncounter = configured?.startsEncounterOnAttack ?? false;
            }

            GUILayout.Space(12f);
            GUILayout.Label("SCENARIO VEHICLE", GUI.skin.box);
            scenarioVehicleEnabled = GUILayout.Toggle(
                scenarioVehicleEnabled,
                "Driveable in Test Play");
            GUI.enabled = scenarioVehicleEnabled;
            DrawLabeledField("Max", ref scenarioVehicleMaximumSpeedText);
            DrawLabeledField("Accel", ref scenarioVehicleAccelerationText);
            DrawLabeledField("Brake", ref scenarioVehicleBrakingText);
            DrawLabeledField("Low turn", ref scenarioVehicleLowTurnText);
            DrawLabeledField("High turn", ref scenarioVehicleHighTurnText);
            DrawLabeledField("Radius", ref scenarioVehicleBaseRadiusText);
            DrawLabeledField("Radius ×", ref scenarioVehicleRadiusFactorText);
            DrawLabeledField("Start", ref scenarioVehicleStartingSpeedText);
            GUILayout.Label("Starting occupant actor ID (optional)");
            scenarioVehicleOccupantId = GUILayout.TextField(scenarioVehicleOccupantId);
            scenarioVehicleStartsEncounter = GUILayout.Toggle(
                scenarioVehicleStartsEncounter,
                "Attack starts encounter");
            GUI.enabled = true;
            if (GUILayout.Button("APPLY VEHICLE", GUILayout.Height(30f)))
            {
                applyScenarioVehicle(
                    entity.id,
                    scenarioVehicleEnabled,
                    scenarioVehicleMaximumSpeedText,
                    scenarioVehicleAccelerationText,
                    scenarioVehicleBrakingText,
                    scenarioVehicleLowTurnText,
                    scenarioVehicleHighTurnText,
                    scenarioVehicleBaseRadiusText,
                    scenarioVehicleRadiusFactorText,
                    scenarioVehicleStartingSpeedText,
                    scenarioVehicleOccupantId,
                    scenarioVehicleStartsEncounter);
            }
        }

        private void SyncInteractionFields(InteractionPointData point)
        {
            if (string.Equals(lastInteractionSelectionId, point.id, StringComparison.Ordinal))
            {
                return;
            }

            lastInteractionSelectionId = point.id;
            interactionType = point.type;
            interactionXText = point.localPosition.x.ToString("0.###", CultureInfo.InvariantCulture);
            interactionYText = point.localPosition.y.ToString("0.###", CultureInfo.InvariantCulture);
            interactionZText = point.localPosition.z.ToString("0.###", CultureInfo.InvariantCulture);
            interactionRadiusText = point.radius.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static int DestructibleStateIndex(string value)
        {
            if (string.Equals(value, "damaged", StringComparison.OrdinalIgnoreCase))
            {
                return 1;
            }

            return string.Equals(value, "destroyed", StringComparison.OrdinalIgnoreCase) ? 2 : 0;
        }

        private static int ScenarioSizeIndex(string value)
        {
            if (string.Equals(value, "small", StringComparison.OrdinalIgnoreCase))
                return 0;
            if (string.Equals(value, "large", StringComparison.OrdinalIgnoreCase))
                return 2;
            if (string.Equals(value, "huge", StringComparison.OrdinalIgnoreCase))
                return 3;
            return 1;
        }

        private static void DrawStatusBar(string statusMessage)
        {
            GUILayout.BeginArea(
                new Rect(
                    PaletteWidth,
                    Screen.height - 30f,
                    Screen.width - PaletteWidth - InspectorWidth,
                    30f),
                GUI.skin.box);
            GUILayout.Label(statusMessage ?? string.Empty);
            GUILayout.EndArea();
        }

        private static void DrawLabeledField(string label, ref string value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(42f));
            value = GUILayout.TextField(value);
            GUILayout.EndHorizontal();
        }
    }
}
