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
using GritGud.Presentation.Characters;
using UnityEngine;

namespace GritGud.Presentation.LevelEditing.UI
{
    public sealed partial class LevelEditorGui
    {
        private static readonly string[] ObjectiveMobilityLabels =
            { "MOBILE", "MOMENTUM", "SET" };
        private static readonly string[] ObjectiveMobilityValues =
            { "mobile", "momentum", "set" };

        private readonly LevelSelectionModel selection;
        private readonly LevelArchetypeCatalog catalog;
        private readonly ScenarioAuthoringCatalog scenarioCatalog;
        private readonly LevelDressingCatalog dressingCatalog;
        private readonly UnityCharacterLibrary characterLibrary;
        private readonly LevelEditorToolManager toolManager;
        private readonly PlacementLevelEditorTool placementTool;
        private readonly TerrainToolPanelModel terrainPanel;
        private readonly SelectionLevelEditorTool selectionTool;
        private readonly LevelSnapSettings snapSettings;
        private readonly LevelEditorPresentationState presentationState;
        private readonly ILevelEditorGuiActions actions;
        private readonly LevelEditorDocumentActionConfirmation documentActionConfirmation =
            new LevelEditorDocumentActionConfirmation();
        private Vector2 paletteScroll;
        private Vector2 inspectorScroll;
        private string xText = "0";
        private string yText = "0";
        private string zText = "0";
        private string pitchText = "0";
        private string yawText = "0";
        private string rollText = "0";
        private string physicsDropHeightText = "2";
        private bool physicsKeepUpright;
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
        private string scenarioXText = "0";
        private string scenarioYText = "0";
        private string scenarioZText = "0";
        private string scenarioYawText = "0";
        private string levelDisplayNameText = string.Empty;
        private string synchronizedLevelIdentity = string.Empty;
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
        private string scenarioObjectiveMovementCostText = "0";
        private string scenarioObjectiveMobility = "set";
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
        private bool showActorTemplates;
        private bool showValidation = true;
        private bool showPortableFiles;
        private bool showLeftPanel = true;
        private bool showInspectorPanel = true;
        private bool shellLayoutInitialized;
        private bool shellWasCompact;
        private bool drawingPreviewMode;
        private LevelEditorInspectorTarget lastResponsiveInspectorTarget;
        private LevelEditorMenuKind activeMenu;
        private Rect activeMenuAnchor;
        private Rect activeMenuRect;
        private readonly LevelEditorGuiStyles styles = new LevelEditorGuiStyles();

        private string SelectedScenarioActorId =>
            presentationState.InspectorTarget.Kind
                == LevelEditorInspectorTargetKind.ScenarioActor
                ? presentationState.InspectorTarget.TargetId
                : string.Empty;

        public LevelEditorGui(
            LevelSelectionModel selection,
            LevelArchetypeCatalog catalog,
            ScenarioAuthoringCatalog scenarioCatalog,
            LevelDressingCatalog dressingCatalog,
            UnityCharacterLibrary characterLibrary,
            LevelEditorToolManager toolManager,
            PlacementLevelEditorTool placementTool,
            TerrainToolPanelModel terrainPanel,
            SelectionLevelEditorTool selectionTool,
            LevelSnapSettings snapSettings,
            LevelEditorPresentationState presentationState,
            ILevelEditorGuiActions actions)
        {
            this.selection = selection ?? throw new ArgumentNullException(nameof(selection));
            this.catalog = catalog != null ? catalog : throw new ArgumentNullException(nameof(catalog));
            this.scenarioCatalog = scenarioCatalog
                ?? throw new ArgumentNullException(nameof(scenarioCatalog));
            this.dressingCatalog = dressingCatalog
                ?? throw new ArgumentNullException(nameof(dressingCatalog));
            this.characterLibrary = characterLibrary
                ?? throw new ArgumentNullException(nameof(characterLibrary));
            this.toolManager = toolManager ?? throw new ArgumentNullException(nameof(toolManager));
            this.placementTool = placementTool ?? throw new ArgumentNullException(nameof(placementTool));
            this.terrainPanel = terrainPanel ?? throw new ArgumentNullException(nameof(terrainPanel));
            this.selectionTool = selectionTool ?? throw new ArgumentNullException(nameof(selectionTool));
            this.snapSettings = snapSettings ?? throw new ArgumentNullException(nameof(snapSettings));
            this.presentationState = presentationState
                ?? throw new ArgumentNullException(nameof(presentationState));
            this.actions = actions ?? throw new ArgumentNullException(nameof(actions));
        }

        public void Draw(LevelEditorViewState state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            GUISkin previousSkin = GUI.skin;
            GUI.skin = styles.ResolveSkin(previousSkin);
            try
            {
                drawingPreviewMode = state.PreviewMode;
                SynchronizeResponsiveShell();
                DrawToolbar(state, !documentActionConfirmation.HasPendingAction);
                HandleMenuDismissal();
                GUI.enabled = !documentActionConfirmation.HasPendingAction;
                if (!state.PreviewMode)
                {
                    if (showLeftPanel)
                        DrawPalette(state.Document);
                    if (showInspectorPanel)
                        DrawInspector(state);
                }

                DrawViewportToolbar(state);

                if (showControls)
                    DrawShortcutsOverlay();

                GUI.enabled = true;
                DrawStatusBar(state.StatusMessage);
                DrawActiveMenu(state);
                if (documentActionConfirmation.HasPendingAction)
                    DrawUnsavedChangesConfirmation();
            }
            finally
            {
                GUI.enabled = true;
                GUI.skin = previousSkin;
            }
        }

        public bool IsPointerOverInterface(Vector2 screenPosition)
        {
            if (documentActionConfirmation.HasPendingAction)
                return true;

            float guiY = Screen.height - screenPosition.y;
            if (guiY <= LevelEditorGuiMetrics.ToolbarHeight
                || guiY >= Screen.height - LevelEditorGuiMetrics.StatusBarHeight)
            {
                return true;
            }

            if (showControls && ShortcutOverlayRect().Contains(new Vector2(screenPosition.x, guiY)))
            {
                return true;
            }

            Vector2 guiPosition = new Vector2(screenPosition.x, guiY);
            if (activeMenu != LevelEditorMenuKind.None && activeMenuRect.Contains(guiPosition))
                return true;
            if (ViewportToolbarRect().Contains(guiPosition)
                || LeftPanelRevealRect().Contains(guiPosition)
                || InspectorRevealRect().Contains(guiPosition))
            {
                return true;
            }

            return (!drawingPreviewMode
                    && showLeftPanel
                    && screenPosition.x <= LevelEditorGuiMetrics.LeftPanelWidth)
                || (!drawingPreviewMode
                    && showInspectorPanel
                    && screenPosition.x >= Screen.width - LevelEditorGuiMetrics.InspectorWidth);
        }

        public void SyncTransformFields(LevelTransformData value)
        {
            xText = value.position.x.ToString("0.###", CultureInfo.InvariantCulture);
            yText = value.position.y.ToString("0.###", CultureInfo.InvariantCulture);
            zText = value.position.z.ToString("0.###", CultureInfo.InvariantCulture);
            pitchText = value.pitchDegrees.ToString("0.###", CultureInfo.InvariantCulture);
            yawText = value.yawDegrees.ToString("0.###", CultureInfo.InvariantCulture);
            rollText = value.rollDegrees.ToString("0.###", CultureInfo.InvariantCulture);
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
            presentationState.ShowPage(LevelEditorWorkspacePage.Scenario);
            presentationState.FocusScenarioActor(actorId);
        }

        public void SyncScenarioActorFields(LevelScenarioActorData actor)
        {
            if (actor == null)
                return;
            presentationState.FocusScenarioActor(actor.id);
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

        public void SyncScenarioFields(LevelDocument document, bool forceLevelIdentity = false)
        {
            string levelIdentity = document == null
                ? string.Empty
                : $"{document.levelId}\n{document.displayName}";
            if (forceLevelIdentity
                || !string.Equals(
                    synchronizedLevelIdentity,
                    levelIdentity,
                    StringComparison.Ordinal))
            {
                synchronizedLevelIdentity = levelIdentity;
                levelDisplayNameText = document?.displayName ?? string.Empty;
            }

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
                    SelectedScenarioActorId,
                    StringComparison.Ordinal));
            if (selected != null)
                SyncScenarioActorFields(selected);
            else if (presentationState.InspectorTarget.Kind
                == LevelEditorInspectorTargetKind.ScenarioActor)
                presentationState.ClearInspectorFocus();
        }

        private void DrawToolbar(LevelEditorViewState state, bool interactionsEnabled)
        {
            bool previewMode = state.PreviewMode;
            GUILayout.BeginArea(
                new Rect(0f, 0f, Screen.width, LevelEditorGuiMetrics.ToolbarHeight),
                styles.Toolbar);
            GUILayout.BeginHorizontal();
            GUI.enabled = interactionsEnabled;
            if (GUILayout.Button(
                    new GUIContent("‹ LIBRARY", "Return to the level library"),
                    ToolbarButtonLayout(82f)))
            {
                documentActionConfirmation.Request(
                    state.IsDirty,
                    "Return to the main menu and discard this level's unsaved changes?",
                    actions.ReturnToMenu);
            }

            GUI.enabled = interactionsEnabled;
            DrawToolbarMenuButton("FILE", LevelEditorMenuKind.File, 54f);
            DrawToolbarMenuButton("EDIT", LevelEditorMenuKind.Edit, 54f);
            DrawToolbarMenuButton("VIEW", LevelEditorMenuKind.View, 58f);

            GUI.enabled = interactionsEnabled && state.CanUndo && !previewMode;
            if (GUILayout.Button(new GUIContent("↶", "Undo (Ctrl+Z)"), ToolbarButtonLayout(38f)))
            {
                actions.Undo();
            }

            GUI.enabled = interactionsEnabled && state.CanRedo && !previewMode;
            if (GUILayout.Button(new GUIContent("↷", "Redo (Ctrl+Y)"), ToolbarButtonLayout(38f)))
            {
                actions.Redo();
            }

            GUI.enabled = interactionsEnabled;
            GUILayout.FlexibleSpace();
            GUILayout.Label(
                ToolbarDocumentLabel(state),
                styles.ToolbarTitle,
                GUILayout.Height(LevelEditorGuiMetrics.ToolbarControlHeight));

            GUI.enabled = interactionsEnabled && !previewMode;
            snapSettings.Enabled = GUILayout.Toggle(
                snapSettings.Enabled,
                "SNAP",
                GUI.skin.button,
                ToolbarButtonLayout(58f));
            if (GUILayout.Button(new GUIContent("SAVE", "Save the local recovery draft"),
                    ToolbarButtonLayout(68f)))
            {
                actions.SaveDraft();
            }

            GUI.enabled = interactionsEnabled;
            if (GUILayout.Button(
                    previewMode ? "EDIT MODE" : "PREVIEW",
                    ToolbarButtonLayout(92f)))
            {
                actions.TogglePreview();
            }
            GUI.enabled = interactionsEnabled && !previewMode;
            Color previous = GUI.backgroundColor;
            GUI.backgroundColor = LevelEditorTheme.PrimaryAction;
            if (GUILayout.Button("TEST PLAY", ToolbarButtonLayout(92f)))
            {
                actions.StartTestPlay();
            }
            GUI.backgroundColor = previous;

            GUI.enabled = true;
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private string ToolbarDocumentLabel(LevelEditorViewState state)
        {
            string displayName = state.Document?.displayName ?? "Untitled level";
            if (state.PreviewMode)
                return $"{displayName}  ·  Preview locked";
            return $"{displayName}  ·  {(state.IsDirty ? "Unsaved" : "Saved")}";
        }

        private void DrawUnsavedChangesConfirmation()
        {
            const float width = 480f;
            const float height = 176f;
            Rect panel = new Rect(
                Mathf.Max(8f, (Screen.width - width) * 0.5f),
                Mathf.Max(8f, (Screen.height - height) * 0.5f),
                Mathf.Min(width, Screen.width - 16f),
                height);
            GUILayout.BeginArea(panel, "UNSAVED CHANGES", styles.FloatingPanel);
            GUILayout.Space(LevelEditorGuiMetrics.SpaceGroup);
            GUILayout.Label(documentActionConfirmation.Prompt);
            GUILayout.Label("Save a draft or export first if you want to keep this work.");
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("KEEP EDITING", PanelPrimaryButtonLayout()))
                documentActionConfirmation.Cancel();
            if (GUILayout.Button("DISCARD & CONTINUE", PanelPrimaryButtonLayout()))
                documentActionConfirmation.ConfirmDiscard();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void DrawShortcutsOverlay()
        {
            GUILayout.BeginArea(ShortcutOverlayRect(), styles.FloatingPanel);
            DrawSectionHeader("SHORTCUTS");
            GUILayout.Label("CAMERA");
            GUILayout.Label("WASD/arrows: pan  ·  Shift: fast");
            GUILayout.Label("MMB/RMB drag: orbit  ·  Wheel: zoom");
            GUILayout.Label("F: frame  ·  Home: frame all");
            GUILayout.Space(LevelEditorGuiMetrics.SpaceSmall);
            GUILayout.Label("AUTHORING");
            GUILayout.Label("Click: select/place  ·  Ctrl-click: add/remove");
            GUILayout.Label("R: rotate  ·  Delete: remove  ·  Esc: cancel");
            GUILayout.Label("Ctrl+C/V: copy/paste  ·  Ctrl+D: duplicate");
            GUILayout.Label("Ctrl+Z/Y: undo/redo");
            GUILayout.EndArea();
        }

        private Rect ShortcutOverlayRect()
        {
            Rect viewport = ViewportRect;
            float width = Mathf.Min(420f, Mathf.Max(0f, viewport.width - 16f));
            return new Rect(
                viewport.x + 8f,
                LevelEditorGuiMetrics.ToolbarHeight + 48f,
                width,
                202f);
        }

        private void DrawStatusBar(string statusMessage)
        {
            float left = VisibleLeftPanelWidth;
            float right = VisibleInspectorWidth;
            GUILayout.BeginArea(
                new Rect(
                    left,
                    Screen.height - LevelEditorGuiMetrics.StatusBarHeight,
                    Mathf.Max(0f, Screen.width - left - right),
                    LevelEditorGuiMetrics.StatusBarHeight),
                styles.StatusBar);
            GUILayout.Label(statusMessage ?? string.Empty, styles.MutedLabel);
            GUILayout.EndArea();
        }

        private static GUILayoutOption[] ToolbarButtonLayout(float width)
        {
            return new[]
            {
                GUILayout.Width(width),
                GUILayout.Height(LevelEditorGuiMetrics.ToolbarControlHeight),
            };
        }

        private static GUILayoutOption[] PanelButtonLayout()
        {
            return new[] { GUILayout.Height(LevelEditorGuiMetrics.PanelControlHeight) };
        }

        private static GUILayoutOption[] PanelPrimaryButtonLayout()
        {
            return new[] { GUILayout.Height(LevelEditorGuiMetrics.PanelPrimaryControlHeight) };
        }

        private static GUILayoutOption[] PanelCompactButtonLayout()
        {
            return new[] { GUILayout.Height(LevelEditorGuiMetrics.PanelCompactControlHeight) };
        }

        private static GUILayoutOption[] PanelIconButtonLayout()
        {
            return new[] { GUILayout.Height(LevelEditorGuiMetrics.PanelIconControlHeight) };
        }

        private static GUILayoutOption[] PanelApplyButtonLayout()
        {
            return new[] { GUILayout.Height(LevelEditorGuiMetrics.PanelApplyControlHeight) };
        }

        private void DrawSectionHeader(string label)
        {
            GUILayout.Label(label, styles.SectionHeader);
        }

        private bool DrawSectionExpander(string label, ref bool expanded)
        {
            if (GUILayout.Button(
                    $"{(expanded ? "▼" : "▶")} {label}",
                    styles.SectionHeader,
                    PanelCompactButtonLayout()))
            {
                expanded = !expanded;
            }
            return expanded;
        }
    }
}
