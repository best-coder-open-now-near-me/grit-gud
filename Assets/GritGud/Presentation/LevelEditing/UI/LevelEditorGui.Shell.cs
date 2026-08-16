using System;
using System.Collections.Generic;
using GritGud.Presentation.LevelEditing.Core;
using UnityEngine;

namespace GritGud.Presentation.LevelEditing.UI
{
    public sealed partial class LevelEditorGui
    {
        private LevelEditorShellLayout ShellLayout => LevelEditorShellLayout.Calculate(
            Screen.width,
            drawingPreviewMode,
            showLeftPanel,
            showInspectorPanel);

        private bool IsCompactLayout => ShellLayout.IsCompact;

        private float VisibleLeftPanelWidth => ShellLayout.LeftPanelWidth;

        private float VisibleInspectorWidth => ShellLayout.InspectorWidth;

        private Rect ViewportRect => new Rect(
            VisibleLeftPanelWidth,
            LevelEditorGuiMetrics.ToolbarHeight,
            ShellLayout.ViewportWidth,
            Mathf.Max(
                0f,
                Screen.height
                - LevelEditorGuiMetrics.ToolbarHeight
                - LevelEditorGuiMetrics.StatusBarHeight));

        private void SynchronizeResponsiveShell()
        {
            bool compact = IsCompactLayout;
            LevelEditorInspectorTarget target = presentationState.InspectorTarget;
            if (!shellLayoutInitialized)
            {
                shellLayoutInitialized = true;
                shellWasCompact = compact;
                lastResponsiveInspectorTarget = target;
                showLeftPanel = true;
                showInspectorPanel = target.Kind != LevelEditorInspectorTargetKind.None
                    || presentationState.InspectorPage == LevelEditorInspectorPage.Level;
                if (compact && showInspectorPanel)
                    showLeftPanel = false;
                return;
            }

            if (compact != shellWasCompact)
            {
                shellWasCompact = compact;
                if (compact)
                {
                    showInspectorPanel = target.Kind != LevelEditorInspectorTargetKind.None;
                    showLeftPanel = !showInspectorPanel;
                }
                else
                {
                    showLeftPanel = true;
                    showInspectorPanel = target.Kind != LevelEditorInspectorTargetKind.None
                        || presentationState.InspectorPage == LevelEditorInspectorPage.Level;
                }
            }

            if (!target.Equals(lastResponsiveInspectorTarget))
            {
                if (target.Kind != LevelEditorInspectorTargetKind.None)
                {
                    showInspectorPanel = true;
                    if (compact)
                        showLeftPanel = false;
                }
                else if (presentationState.InspectorPage != LevelEditorInspectorPage.Level)
                {
                    showInspectorPanel = false;
                    if (compact)
                        showLeftPanel = true;
                }
            }

            lastResponsiveInspectorTarget = target;
        }

        private void DrawToolbarMenuButton(
            string label,
            LevelEditorMenuKind kind,
            float width)
        {
            Color previous = GUI.backgroundColor;
            if (activeMenu == kind)
                GUI.backgroundColor = LevelEditorTheme.Active;
            if (GUILayout.Button(label + " ▾", ToolbarButtonLayout(width)))
            {
                Rect local = GUILayoutUtility.GetLastRect();
                ToggleMenu(kind, new Rect(local.x, local.y, local.width, local.height));
            }
            GUI.backgroundColor = previous;
        }

        private void ToggleMenu(LevelEditorMenuKind kind, Rect anchor)
        {
            if (activeMenu == kind)
            {
                CloseMenu();
                return;
            }
            activeMenu = kind;
            activeMenuAnchor = anchor;
            activeMenuRect = default;
        }

        private void CloseMenu()
        {
            activeMenu = LevelEditorMenuKind.None;
            activeMenuAnchor = default;
            activeMenuRect = default;
        }

        private void HandleMenuDismissal()
        {
            Event current = Event.current;
            if (activeMenu == LevelEditorMenuKind.None
                || current == null
                || current.type != EventType.MouseDown
                || current.mousePosition.y <= LevelEditorGuiMetrics.ToolbarHeight
                || activeMenuRect.Contains(current.mousePosition))
            {
                return;
            }

            CloseMenu();
            current.Use();
        }

        private void DrawActiveMenu(LevelEditorViewState state)
        {
            if (activeMenu == LevelEditorMenuKind.None)
                return;

            LevelEditorMenuModel model = BuildMenu(state, activeMenu);
            float height = 12f;
            foreach (LevelEditorMenuItem item in model.Items)
            {
                height += item.IsSeparator
                    ? LevelEditorGuiMetrics.MenuSeparatorHeight
                    : LevelEditorGuiMetrics.MenuItemHeight;
            }

            float width = LevelEditorGuiMetrics.MenuWidth;
            float x = Mathf.Clamp(
                activeMenuAnchor.x,
                4f,
                Mathf.Max(4f, Screen.width - width - 4f));
            float y = activeMenuAnchor.yMax + 4f;
            if (y + height > Screen.height - LevelEditorGuiMetrics.StatusBarHeight - 4f)
                y = Mathf.Max(LevelEditorGuiMetrics.ToolbarHeight + 4f, activeMenuAnchor.y - height - 4f);
            activeMenuRect = new Rect(x, y, width, height);

            GUILayout.BeginArea(activeMenuRect, styles.FloatingPanel);
            foreach (LevelEditorMenuItem item in model.Items)
            {
                if (item.IsSeparator)
                {
                    GUILayout.Space(3f);
                    GUILayout.Box(
                        GUIContent.none,
                        styles.MenuSeparator,
                        GUILayout.Height(1f),
                        GUILayout.ExpandWidth(true));
                    GUILayout.Space(3f);
                    continue;
                }

                GUI.enabled = item.Enabled;
                GUIStyle style = item.Destructive
                    ? styles.MenuItemDestructive
                    : item.Selected ? styles.MenuItemSelected : styles.MenuItem;
                if (GUILayout.Button(MenuItemLabel(item), style,
                        GUILayout.Height(LevelEditorGuiMetrics.MenuItemHeight)))
                {
                    Action execute = item.Execute;
                    CloseMenu();
                    execute?.Invoke();
                }
            }
            GUI.enabled = true;
            GUILayout.EndArea();
        }

        private static string MenuItemLabel(LevelEditorMenuItem item)
        {
            string marker = item.Selected ? "✓  " : "   ";
            return string.IsNullOrEmpty(item.Shortcut)
                ? marker + item.Label
                : $"{marker}{item.Label}     {item.Shortcut}";
        }

        private LevelEditorMenuModel BuildMenu(
            LevelEditorViewState state,
            LevelEditorMenuKind kind)
        {
            switch (kind)
            {
                case LevelEditorMenuKind.File:
                    return BuildFileMenu(state);
                case LevelEditorMenuKind.Edit:
                    return BuildEditMenu(state);
                case LevelEditorMenuKind.Camera:
                    return BuildCameraMenu();
                default:
                    return BuildViewMenu();
            }
        }

        private LevelEditorMenuModel BuildFileMenu(LevelEditorViewState state)
        {
            bool canAuthor = !state.PreviewMode;
            return new LevelEditorMenuModel(
                LevelEditorMenuKind.File,
                new[]
                {
                    new LevelEditorMenuItem(
                        "New level…",
                        canAuthor,
                        () => RequestDocumentReplacement(
                            state,
                            "Create a new level and discard the current unsaved changes?",
                            actions.CreateNewLevel)),
                    new LevelEditorMenuItem(
                        "Reload source…",
                        canAuthor,
                        () => RequestDocumentReplacement(
                            state,
                            "Reload the source level and discard the current unsaved changes?",
                            actions.ReloadSourceLevel)),
                    LevelEditorMenuItem.Separator,
                    new LevelEditorMenuItem("Save local draft", canAuthor, actions.SaveDraft, shortcut: "Ctrl+S"),
                    new LevelEditorMenuItem(
                        "Save cloud draft",
                        canAuthor && !actions.CloudOperationRunning,
                        actions.SaveToCloud),
                    new LevelEditorMenuItem(
                        "Load local draft…",
                        canAuthor && actions.HasDraft,
                        () => RequestDocumentReplacement(
                            state,
                            "Load the saved draft and discard the current unsaved changes?",
                            actions.LoadDraft)),
                    new LevelEditorMenuItem(
                        "Load cloud draft…",
                        canAuthor && actions.HasCloudDraftContext && !actions.CloudOperationRunning,
                        () => RequestDocumentReplacement(
                            state,
                            "Load the cloud draft and discard the current unsaved changes?",
                            actions.LoadFromCloud)),
                    LevelEditorMenuItem.Separator,
                    new LevelEditorMenuItem("Export portable JSON", canAuthor, actions.Export),
                    new LevelEditorMenuItem(
                        "Import portable JSON…",
                        canAuthor,
                        () => RequestDocumentReplacement(
                            state,
                            "Import another level and discard the current unsaved changes?",
                            actions.RequestImport)),
                });
        }

        private LevelEditorMenuModel BuildEditMenu(LevelEditorViewState state)
        {
            bool canAuthor = !state.PreviewMode;
            bool hasSelection = selection.Targets.Count > 0;
            return new LevelEditorMenuModel(
                LevelEditorMenuKind.Edit,
                new[]
                {
                    new LevelEditorMenuItem("Undo", canAuthor && state.CanUndo, actions.Undo, shortcut: "Ctrl+Z"),
                    new LevelEditorMenuItem("Redo", canAuthor && state.CanRedo, actions.Redo, shortcut: "Ctrl+Y"),
                    LevelEditorMenuItem.Separator,
                    new LevelEditorMenuItem(
                        "Duplicate selection",
                        canAuthor && hasSelection,
                        selectionTool.DuplicateSelection,
                        shortcut: "Ctrl+D"),
                    new LevelEditorMenuItem(
                        "Delete selection",
                        canAuthor && hasSelection,
                        selectionTool.DeleteSelection,
                        destructive: true,
                        shortcut: "Delete"),
                });
        }

        private LevelEditorMenuModel BuildViewMenu()
        {
            return new LevelEditorMenuModel(
                LevelEditorMenuKind.View,
                new[]
                {
                    new LevelEditorMenuItem(
                        showLeftPanel ? "Hide tools panel" : "Show tools panel",
                        !drawingPreviewMode,
                        () => SetLeftPanelVisible(!showLeftPanel),
                        selected: showLeftPanel),
                    new LevelEditorMenuItem(
                        showInspectorPanel ? "Hide Inspector" : "Show Inspector",
                        !drawingPreviewMode,
                        () => SetInspectorVisible(!showInspectorPanel),
                        selected: showInspectorPanel),
                    new LevelEditorMenuItem(
                        "Level settings",
                        !drawingPreviewMode,
                        ShowLevelSettings,
                        selected: showInspectorPanel
                            && presentationState.InspectorPage == LevelEditorInspectorPage.Level),
                    LevelEditorMenuItem.Separator,
                    new LevelEditorMenuItem(
                        "Show layout grid",
                        !drawingPreviewMode,
                        ToggleLayoutGrid,
                        selected: gridFields.visible),
                    new LevelEditorMenuItem(
                        "Shortcut reference",
                        true,
                        () => showControls = !showControls,
                        selected: showControls,
                        shortcut: "?"),
                });
        }

        private LevelEditorMenuModel BuildCameraMenu()
        {
            var items = new List<LevelEditorMenuItem>();
            AddCameraMenuItem(items, "Perspective", LevelEditorCameraView.Perspective);
            AddCameraMenuItem(items, "Top", LevelEditorCameraView.Top);
            AddCameraMenuItem(items, "Front", LevelEditorCameraView.Front);
            AddCameraMenuItem(items, "Right", LevelEditorCameraView.Right);
            return new LevelEditorMenuModel(LevelEditorMenuKind.Camera, items);
        }

        private void AddCameraMenuItem(
            ICollection<LevelEditorMenuItem> items,
            string label,
            LevelEditorCameraView view)
        {
            items.Add(new LevelEditorMenuItem(
                label,
                true,
                () => actions.SetCameraView(view),
                selected: actions.CameraView == view));
        }

        private void RequestDocumentReplacement(
            LevelEditorViewState state,
            string prompt,
            Action replacement)
        {
            documentActionConfirmation.Request(state.IsDirty, prompt, replacement);
        }

        private void ToggleLayoutGrid()
        {
            gridFields.visible = !gridFields.visible;
            actions.ConfigureGrid(gridFields);
        }

        private void ShowLevelSettings()
        {
            presentationState.ShowInspectorPage(LevelEditorInspectorPage.Level);
            SetInspectorVisible(true);
        }

        private void DrawViewportToolbar(LevelEditorViewState state)
        {
            if (ViewportRect.width <= 0f || ViewportRect.height <= 0f)
                return;

            GUI.enabled = !documentActionConfirmation.HasPendingAction;
            Rect leftReveal = LeftPanelRevealRect();
            if (leftReveal.width > 0f && GUI.Button(leftReveal, "TOOLS ›"))
                SetLeftPanelVisible(true);

            Rect inspectorReveal = InspectorRevealRect();
            if (inspectorReveal.width > 0f && GUI.Button(inspectorReveal, "‹ INSPECTOR"))
                SetInspectorVisible(true);

            Rect toolbarRect = ViewportToolbarRect();
            if (toolbarRect.width <= 0f)
            {
                GUI.enabled = true;
                return;
            }

            GUI.Box(toolbarRect, GUIContent.none, styles.ViewportToolbar);
            float x = toolbarRect.x + 4f;
            float y = toolbarRect.y + 3f;
            float height = toolbarRect.height - 6f;
            Rect cameraButton = new Rect(x, y, 118f, height);
            Color previous = GUI.backgroundColor;
            if (activeMenu == LevelEditorMenuKind.Camera)
                GUI.backgroundColor = LevelEditorTheme.Active;
            if (GUI.Button(cameraButton, CameraViewLabel() + " ▾"))
                ToggleMenu(LevelEditorMenuKind.Camera, cameraButton);
            GUI.backgroundColor = previous;

            GUI.enabled = !documentActionConfirmation.HasPendingAction
                && selection.Primary != null;
            Rect frameButton = new Rect(cameraButton.xMax + 4f, y, 72f, height);
            if (GUI.Button(frameButton, new GUIContent("FRAME", "Frame selection (F)")))
                actions.FrameSelection();

            GUI.enabled = !documentActionConfirmation.HasPendingAction;
            Rect allButton = new Rect(frameButton.xMax + 4f, y, 60f, height);
            if (GUI.Button(allButton, new GUIContent("ALL", "Frame level bounds (Home)")))
                actions.FrameLevel();
            GUI.enabled = true;
        }

        private string CameraViewLabel()
        {
            switch (actions.CameraView)
            {
                case LevelEditorCameraView.Top:
                    return "TOP";
                case LevelEditorCameraView.Front:
                    return "FRONT";
                case LevelEditorCameraView.Right:
                    return "RIGHT";
                default:
                    return "PERSPECTIVE";
            }
        }

        private Rect ViewportToolbarRect()
        {
            Rect viewport = ViewportRect;
            float width = Mathf.Min(
                LevelEditorGuiMetrics.ViewportToolbarWidth,
                Mathf.Max(0f, viewport.width - 16f));
            if (width < 266f)
                return default;
            return new Rect(
                viewport.xMax - width - 8f,
                viewport.y + 8f,
                width,
                LevelEditorGuiMetrics.ViewportToolbarHeight);
        }

        private Rect LeftPanelRevealRect()
        {
            if (drawingPreviewMode || showLeftPanel)
                return default;
            Rect viewport = ViewportRect;
            return new Rect(viewport.x + 8f, viewport.y + 8f, 76f, 32f);
        }

        private Rect InspectorRevealRect()
        {
            if (drawingPreviewMode || showInspectorPanel)
                return default;
            Rect viewport = ViewportRect;
            return new Rect(viewport.xMax - 108f, viewport.y + 48f, 100f, 32f);
        }

        private void SetLeftPanelVisible(bool visible)
        {
            showLeftPanel = visible;
            if (visible && IsCompactLayout)
                showInspectorPanel = false;
        }

        private void SetInspectorVisible(bool visible)
        {
            showInspectorPanel = visible;
            if (visible && IsCompactLayout)
                showLeftPanel = false;
        }

        private void ShowWorkspace(LevelEditorWorkspacePage page)
        {
            presentationState.ShowPage(page);
            SetLeftPanelVisible(true);
        }

        private void DrawPanelHeading(string label, bool inspector)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, styles.PanelTitle);
            if (GUILayout.Button(
                    inspector ? "›" : "‹",
                    GUILayout.Width(28f),
                    GUILayout.Height(LevelEditorGuiMetrics.PanelCompactControlHeight)))
            {
                if (inspector)
                    SetInspectorVisible(false);
                else
                    SetLeftPanelVisible(false);
            }
            GUILayout.EndHorizontal();
        }
    }
}
