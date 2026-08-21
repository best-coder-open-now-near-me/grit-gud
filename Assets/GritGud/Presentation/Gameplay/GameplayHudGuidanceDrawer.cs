using System;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    internal sealed partial class GameplayHudRenderer
    {
        private sealed class GameplayHudGuidanceDrawer
        {
            private static readonly Color PanelStrongColor =
                GameplayHudRenderer.PanelStrongColor;
            private static readonly Color BorderColor =
                GameplayHudRenderer.BorderColor;
            private static readonly Color SignalColor =
                GameplayHudRenderer.SignalColor;
            private static readonly Color SignalSoftColor =
                GameplayHudRenderer.SignalSoftColor;

            private readonly GameplayHudRenderer owner;
            private Vector2 tipsScrollPosition;

            public GameplayHudGuidanceDrawer(GameplayHudRenderer owner)
            {
                this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
            }

            private float flyoutReveal => owner.flyoutReveal;
            private bool flyoutExpanded => owner.flyoutExpanded;
            private GameplayGuidanceCatalog guidanceCatalog =>
                owner.guidanceCatalog;
            private GameplayTipCatalog tipCatalog => owner.tipCatalog;
            private GameplayHudBindings bindings => owner.bindings;
            private GameplayActionController actionController =>
                owner.actionController;
            private string CurrentGuidanceId => owner.CurrentGuidanceId;
            private string bugReportStatus => owner.bugReportStatus;
            private string bugReportNote
            {
                get => owner.bugReportNote;
                set => owner.bugReportNote = value;
            }

            private GUIStyle headerStyle => owner.headerStyle;
            private GUIStyle bodyStyle => owner.bodyStyle;
            private GUIStyle guidanceStyle => owner.guidanceStyle;
            private GUIStyle tabStyle => owner.tabStyle;
            private GUIStyle tipTitleStyle => owner.tipTitleStyle;
            private GUIStyle tipBodyStyle => owner.tipBodyStyle;
            private GUIStyle buttonStyle => owner.buttonStyle;
            private GUIStyle statusStyle => owner.statusStyle;
            private GUIStyle modeButtonStyle => owner.modeButtonStyle;

            private void ToggleFlyout() => owner.ToggleFlyout();
            private string GetBindingDisplay(GameplayControl control) =>
                owner.GetBindingDisplay(control);
            private float EvaluateFlyoutReveal(float progress) =>
                owner.EvaluateFlyoutReveal(progress);
            private void DrawRectangle(Rect rectangle, Color color) =>
                owner.DrawRectangle(rectangle, color);
            private void DrawGlowLine(Rect rectangle, Color color) =>
                owner.DrawGlowLine(rectangle, color);
            private void DrawFramedPanel(Rect rectangle, Color color) =>
                owner.DrawFramedPanel(rectangle, color);
            private void DrawLaserReveal(
                float x,
                float y,
                float height,
                Color color,
                float progress) =>
                owner.DrawLaserReveal(x, y, height, color, progress);

            public void DrawAnimatedFlyout(
                float canvasWidth,
                float canvasHeight,
                string scenarioDisplayName,
                string mode,
                string resources)
            {
                if (!flyoutExpanded && flyoutReveal <= 0f)
                {
                    DrawFlyout(
                        canvasWidth,
                        canvasHeight,
                        scenarioDisplayName,
                        mode,
                        resources,
                        expanded: false);
                    return;
                }

                float width = Mathf.Min(470f, canvasWidth - 58f);
                float eased = EvaluateFlyoutReveal(flyoutReveal);
                const float expandedTabWidth = 38f;
                float revealEdge = (width + expandedTabWidth) * eased;
                GUI.BeginClip(new Rect(0f, 0f, revealEdge, canvasHeight));
                DrawFlyout(
                    canvasWidth,
                    canvasHeight,
                    scenarioDisplayName,
                    mode,
                    resources,
                    expanded: true);
                GUI.EndClip();
                DrawLaserReveal(
                    revealEdge,
                    18f,
                    canvasHeight - 36f,
                    SignalColor,
                    flyoutReveal);
            }

            private void DrawFlyout(
                float canvasWidth,
                float canvasHeight,
                string scenarioDisplayName,
                string mode,
                string resources,
                bool expanded)
            {
                float width = Mathf.Min(470f, canvasWidth - 58f);
                if (!expanded)
                {
                    DrawFlyoutTab(new Rect(0f, 36f, 42f, 82f), expanded: false);
                    return;
                }

                var rectangle = new Rect(0f, 18f, width, canvasHeight - 36f);
                DrawFramedPanel(rectangle, PanelStrongColor);
                DrawGlowLine(
                    new Rect(rectangle.xMax - 2f, rectangle.y, 2f, rectangle.height),
                    SignalColor);

                var tabRectangle = new Rect(rectangle.xMax, rectangle.y + 18f, 38f, 72f);
                DrawFlyoutTab(tabRectangle, expanded: true);

                float x = rectangle.x + 18f;
                float innerWidth = rectangle.width - 38f;
                float y = rectangle.y + 17f;
                GUI.Label(
                    new Rect(x, y, innerWidth, 22f),
                    $"{scenarioDisplayName.ToUpperInvariant()} - {mode}",
                    headerStyle);
                y += 30f;
                DrawSectionRule(x, y, innerWidth);
                y += 10f;

                float resourceHeight = Mathf.Max(
                    42f,
                    bodyStyle.CalcHeight(new GUIContent(resources), innerWidth));
                GUI.Label(
                    new Rect(x, y, innerWidth, resourceHeight),
                    resources,
                    bodyStyle);
                y += resourceHeight + 10f;
                DrawSectionRule(x, y, innerWidth);
                y += 10f;

                float guidanceHeight = DrawGuidance(x, y, innerWidth);
                y += guidanceHeight + 12f;
                float tipsBottom = rectangle.yMax - 58f;
                if (tipsBottom > y + 72f)
                {
                    DrawTips(x, y, innerWidth, tipsBottom - y);
                }

                DrawBugReportExport(x, rectangle.yMax - 48f, innerWidth);
            }

            private void DrawFlyoutTab(Rect rectangle, bool expanded)
            {
                DrawRectangle(rectangle, BorderColor);
                var buttonRectangle = new Rect(
                    rectangle.x + 1f,
                    rectangle.y + 1f,
                    rectangle.width - 2f,
                    rectangle.height - 2f);
                string label = expanded ? "<<" : ">>";
                if (GUI.Button(buttonRectangle, label, tabStyle))
                {
                    ToggleFlyout();
                }
            }

            private float DrawGuidance(float x, float y, float width)
            {
                string guidanceId = CurrentGuidanceId;
                if (guidanceId == null || guidanceCatalog == null)
                {
                    return 0f;
                }

                GameplayGuidanceEntry guidance = guidanceCatalog.Require(guidanceId);
                string text = $"EXPECTED  {guidance.ExpectedBehavior}\n" +
                    $"WHY  {guidance.Rationale}\n" +
                    $"TIP  {guidance.PlayerTip}";
                float contentHeight = guidanceStyle.CalcHeight(
                    new GUIContent(text),
                    width);
                float height = contentHeight + 31f;
                GUI.Label(
                    new Rect(x, y, width, 21f),
                    $"FIELD GUIDE - {guidance.Title.ToUpperInvariant()}",
                    headerStyle);
                GUI.Label(
                    new Rect(x, y + 28f, width, contentHeight),
                    new GUIContent(text, guidance.PlayerTip),
                    guidanceStyle);
                return height;
            }

            private void DrawTips(float x, float y, float width, float height)
            {
                const float headerHeight = 24f;
                GUI.Label(
                    new Rect(x, y, width, headerHeight),
                    "TIPS - ALWAYS AVAILABLE",
                    headerStyle);
                DrawGlowLine(
                    new Rect(x, y + headerHeight - 2f, width, 1f),
                    SignalSoftColor);

                Rect viewport = new Rect(
                    x,
                    y + headerHeight + 3f,
                    width,
                    Mathf.Max(36f, height - headerHeight - 3f));
                float contentWidth = Mathf.Max(60f, viewport.width - 22f);
                float contentHeight = CalculateTipsContentHeight(contentWidth);
                var content = new Rect(0f, 0f, contentWidth, contentHeight);
                tipsScrollPosition = GUI.BeginScrollView(
                    viewport,
                    tipsScrollPosition,
                    content,
                    alwaysShowHorizontal: false,
                    alwaysShowVertical: true);
                float contentY = 3f;
                if (tipCatalog != null)
                {
                    foreach (GameplayTipEntry tip in tipCatalog.Entries)
                    {
                        GUI.Label(
                            new Rect(2f, contentY, contentWidth - 4f, 18f),
                            tip.Category + " / " + tip.Title.ToUpperInvariant(),
                            tipTitleStyle);
                        contentY += 20f;
                        float bodyHeight = tipBodyStyle.CalcHeight(
                            new GUIContent(tip.Text),
                            contentWidth - 4f);
                        GUI.Label(
                            new Rect(2f, contentY, contentWidth - 4f, bodyHeight),
                            tip.Text,
                            tipBodyStyle);
                        contentY += bodyHeight + 11f;
                    }
                }
                GUI.EndScrollView();
            }

            private float CalculateTipsContentHeight(float width)
            {
                float height = 6f;
                if (tipCatalog == null || tipBodyStyle == null)
                {
                    return height;
                }
                foreach (GameplayTipEntry tip in tipCatalog.Entries)
                {
                    height += 31f + tipBodyStyle.CalcHeight(
                        new GUIContent(tip.Text),
                        width - 4f);
                }
                return height;
            }

            private void DrawBugReportExport(float x, float y, float width)
            {
                const float buttonWidth = 224f;
                string exportBinding = GetBindingDisplay(
                    GameplayControl.ExportBugReport);
                if (GUI.Button(
                    new Rect(x, y, buttonWidth, 30f),
                    "EXPORT BUG REPORT - " + exportBinding,
                    buttonStyle))
                {
                    bindings.OpenBugReportNote();
                }

                if (!string.IsNullOrWhiteSpace(bugReportStatus))
                {
                    GUI.Label(
                        new Rect(
                            x + buttonWidth + 10f,
                            y + 2f,
                            width - buttonWidth - 10f,
                            28f),
                        bugReportStatus.ToUpperInvariant(),
                        statusStyle);
                }
            }

            public void DrawBugReportNoteModal(float canvasWidth, float canvasHeight)
            {
                const float width = 560f;
                const float height = 310f;
                var rectangle = new Rect((canvasWidth - width) * 0.5f,
                    (canvasHeight - height) * 0.5f, width, height);
                DrawRectangle(new Rect(0f, 0f, canvasWidth, canvasHeight),
                    new Color(0f, 0f, 0f, 0.72f));
                DrawFramedPanel(rectangle, PanelStrongColor);
                GUI.Label(new Rect(rectangle.x + 20f, rectangle.y + 18f,
                    width - 40f, 26f), "EXPORT BUG REPORT", headerStyle);
                GUI.Label(new Rect(rectangle.x + 20f, rectangle.y + 50f,
                    width - 40f, 42f),
                    "Add what you observed, what you expected, and steps to reproduce. "
                    + "This note is prepended to the diagnostic report.", bodyStyle);
                bugReportNote = GUI.TextArea(new Rect(rectangle.x + 20f,
                    rectangle.y + 98f, width - 40f, 142f),
                    bugReportNote ?? string.Empty, 2000);
                if (GUI.Button(new Rect(rectangle.x + 20f, rectangle.yMax - 50f,
                        140f, 30f), "CANCEL", buttonStyle))
                {
                    bindings.CancelBugReportNote();
                }
                if (GUI.Button(new Rect(rectangle.xMax - 210f, rectangle.yMax - 50f,
                        190f, 30f), "EXPORT REPORT", buttonStyle))
                {
                    bindings.SubmitBugReportNote(bugReportNote);
                }
            }

            private void DrawSectionRule(float x, float y, float width)
            {
                DrawGlowLine(new Rect(x, y, 54f, 1f), SignalColor);
                DrawGlowLine(new Rect(x + 58f, y, width - 58f, 1f), SignalSoftColor);
            }

            public void DrawInteractionPrompt(
                float centerX,
                float centerY,
                bool interactionAvailable)
            {
                if (!interactionAvailable)
                {
                    return;
                }

                const float width = 280f;
                string binding = GetBindingDisplay(GameplayControl.Interact);
                GUI.Label(
                    new Rect(centerX - (width * 0.5f), centerY + 34f, width, 30f),
                    binding + "  "
                        + actionController.InteractionDisplayName.ToUpperInvariant(),
                    modeButtonStyle);
            }

        }
    }
}
