using System;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class GameplayDialogueDrawer : MonoBehaviour
    {
        private const string DefaultHeaderLabel = "DIALOGUE - TRANSCRIPT";
        private const string DefaultEmptyMessage =
            "NO ENTRIES MATCH THE ACTIVE FILTERS.";
        private const float ReferenceHeight = 900f;
        private static readonly Color PanelColor = GameplayVisualPalette.Panel;
        private static readonly Color BorderColor = GameplayVisualPalette.WithAlpha(
            GameplayVisualPalette.Border,
            0.24f);
        private static readonly Color SignalColor = GameplayVisualPalette.SignalBlue;
        private static readonly Color SignalSoftColor = GameplayVisualPalette.WithAlpha(
            GameplayVisualPalette.SignalBlue,
            0.16f);
        private static readonly Color ButtonEdgeColor = GameplayVisualPalette.WithAlpha(
            GameplayVisualPalette.SignalBlue,
            0.48f);
        private static readonly Color PrimaryTextColor = GameplayVisualPalette.TextPrimary;
        private static readonly Color SecondaryTextColor = GameplayVisualPalette.TextSecondary;

        private IGameplayDialogueEntrySource source;
        private Action exportRequested;
        private string exportStatus = string.Empty;
        private string headerLabel = DefaultHeaderLabel;
        private string emptyMessage = DefaultEmptyMessage;
        private string contextStatus = string.Empty;
        private GameplayFlyoutMotionProfile flyoutMotion;
        private GameplayDialogueChannel filters = GameplayDialogueChannel.All;
        private GUIStyle headerStyle;
        private GUIStyle entryHeaderStyle;
        private GUIStyle bodyStyle;
        private GUIStyle emptyStyle;
        private GUIStyle buttonStyle;
        private Texture2D whiteTexture;
        private Texture2D buttonNormalTexture;
        private Texture2D buttonHoverTexture;
        private Texture2D buttonActiveTexture;
        private Vector2 scrollPosition;
        private long observedSequence;
        private bool expanded;
        private float revealProgress;

        public GameplayDialogueLog Log => source as GameplayDialogueLog;

        public IGameplayDialogueEntrySource Source => source;

        public bool IsVisible => enabled;

        internal bool IsExpanded => expanded;

        internal GameplayDialogueChannel ActiveFilters => filters;

        internal int VisibleEntryCount => source?.CountVisible(filters) ?? 0;

        internal string HeaderLabel => headerLabel;

        internal string EmptyMessage => emptyMessage;

        internal string ContextStatus => contextStatus;

        private void Awake()
        {
            flyoutMotion = GameplayFlyoutMotionProfile.LoadDefault();
        }

        internal bool ContainsInteractiveScreenPoint(Vector2 screenPoint)
        {
            float uiScale = Mathf.Clamp(
                Screen.height / ReferenceHeight,
                0.75f,
                1.35f);
            float canvasWidth = Screen.width / uiScale;
            float canvasHeight = Screen.height / uiScale;
            var guiPoint = new Vector2(
                screenPoint.x / uiScale,
                (Screen.height - screenPoint.y) / uiScale);
            Rect commandBar = GameplayHud.CalculateCommandBarRectangle(
                canvasWidth,
                canvasHeight);
            Rect button = GameplayHud.CalculateDialogueButtonRectangle(
                canvasWidth,
                commandBar);
            if (button.Contains(guiPoint))
            {
                return true;
            }

            if (!expanded)
            {
                return false;
            }

            return CalculatePanelRectangle(
                canvasWidth,
                commandBar,
                button).Contains(guiPoint);
        }

        internal static Rect CalculatePanelRectangle(
            float canvasWidth,
            Rect commandBarRectangle,
            Rect buttonRectangle)
        {
            const float gap = 8f;
            float availableWidth = Mathf.Max(
                0f,
                canvasWidth - (GameplayHud.CommandBarMargin * 2f));
            float panelWidth = Mathf.Min(600f, availableWidth);
            float panelHeight = Mathf.Min(
                390f,
                Mathf.Max(210f, commandBarRectangle.y - 48f));
            return new Rect(
                canvasWidth - GameplayHud.CommandBarMargin - panelWidth,
                buttonRectangle.y - panelHeight - gap,
                panelWidth,
                panelHeight);
        }

        public void Bind(
            IGameplayDialogueEntrySource entrySource,
            Action onExportRequested = null)
        {
            source = entrySource ?? throw new ArgumentNullException(
                nameof(entrySource));
            scrollPosition = Vector2.zero;
            observedSequence = source.LatestSequence;
            exportRequested = onExportRequested;
            exportStatus = string.Empty;
        }

        public void Unbind()
        {
            source = null;
            expanded = false;
            revealProgress = 0f;
            scrollPosition = Vector2.zero;
            observedSequence = 0;
            exportRequested = null;
            exportStatus = string.Empty;
            headerLabel = DefaultHeaderLabel;
            emptyMessage = DefaultEmptyMessage;
            contextStatus = string.Empty;
        }

        public void Show()
        {
            expanded = false;
            revealProgress = 0f;
            filters = GameplayDialogueChannel.All;
            scrollPosition = Vector2.zero;
            enabled = true;
        }

        public void Hide()
        {
            expanded = false;
            revealProgress = 0f;
            enabled = false;
        }

        internal void Toggle()
        {
            expanded = !expanded;
        }

        internal void SetExpanded(bool value)
        {
            expanded = value;
        }

        internal void SetFilters(GameplayDialogueChannel value)
        {
            filters = value;
        }

        internal void ConfigurePresentation(
            string header,
            string emptyState,
            string status = null)
        {
            headerLabel = string.IsNullOrWhiteSpace(header)
                ? DefaultHeaderLabel
                : header.Trim();
            emptyMessage = string.IsNullOrWhiteSpace(emptyState)
                ? DefaultEmptyMessage
                : emptyState.Trim();
            contextStatus = status?.Trim() ?? string.Empty;
        }

        internal void ToggleFilter(GameplayDialogueChannel channel)
        {
            GameplayDialogueLog.RequireSingleChannel(channel, nameof(channel));
            filters ^= channel;
        }

        private void Update()
        {
            EnsureFlyoutMotion();
            revealProgress = flyoutMotion.Advance(
                revealProgress,
                expanded,
                Time.unscaledDeltaTime);
        }

        private void OnGUI()
        {
            EnsureStyles();
            float uiScale = Mathf.Clamp(Screen.height / ReferenceHeight, 0.75f, 1.35f);
            float canvasWidth = Screen.width / uiScale;
            float canvasHeight = Screen.height / uiScale;
            Matrix4x4 previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(uiScale, uiScale, 1f));
            Draw(
                canvasWidth,
                GameplayHud.CalculateCommandBarRectangle(
                    canvasWidth,
                    canvasHeight));
            GUI.matrix = previousMatrix;
        }

        private void Draw(float canvasWidth, Rect commandBarRectangle)
        {
            Rect buttonRectangle = GameplayHud.CalculateDialogueButtonRectangle(
                canvasWidth,
                commandBarRectangle);
            if (GUI.Button(
                buttonRectangle,
                expanded ? "CLOSE DIALOGUE" : "DIALOGUE",
                buttonStyle))
            {
                Toggle();
            }

            DrawGlowFrame(buttonRectangle, ButtonEdgeColor);
            if (!expanded && revealProgress <= 0f)
            {
                return;
            }

            Rect worldPanelRectangle = CalculatePanelRectangle(
                canvasWidth,
                commandBarRectangle,
                buttonRectangle);
            EnsureFlyoutMotion();
            float revealEdge = canvasWidth
                - ((canvasWidth - worldPanelRectangle.x)
                    * flyoutMotion.Evaluate(revealProgress));
            var clipRectangle = new Rect(
                revealEdge,
                0f,
                canvasWidth - revealEdge,
                commandBarRectangle.yMax);
            GUI.BeginClip(clipRectangle);
            var panelRectangle = worldPanelRectangle;
            panelRectangle.x -= clipRectangle.x;
            DrawFramedPanel(panelRectangle);
            DrawGlowLine(
                new Rect(
                    panelRectangle.x,
                    panelRectangle.y,
                    panelRectangle.width,
                    2f),
                SignalColor);

            float contentX = panelRectangle.x + 16f;
            float contentWidth = panelRectangle.width - 32f;
            float y = panelRectangle.y + 14f;
            GUI.Label(
                new Rect(contentX, y, contentWidth - 172f, 22f),
                headerLabel,
                headerStyle);
            if (exportRequested != null
                && GUI.Button(
                    new Rect(contentX + contentWidth - 164f, y - 2f, 164f, 26f),
                    "EXPORT DIALOGUE",
                    buttonStyle))
            {
                exportRequested?.Invoke();
            }
            y += 28f;
            if (!string.IsNullOrWhiteSpace(exportStatus))
            {
                GUI.Label(new Rect(contentX, y, contentWidth, 18f),
                    exportStatus.ToUpperInvariant(), entryHeaderStyle);
                y += 20f;
            }
            if (!string.IsNullOrWhiteSpace(contextStatus))
            {
                float statusHeight = Mathf.Min(
                    76f,
                    bodyStyle.CalcHeight(
                        new GUIContent(contextStatus),
                        contentWidth));
                GUI.Label(
                    new Rect(contentX, y, contentWidth, statusHeight),
                    contextStatus,
                    bodyStyle);
                y += statusHeight + 5f;
            }
            DrawFilters(contentX, y, contentWidth);
            y += 34f;
            DrawSectionRule(contentX, y, contentWidth);
            y += 9f;
            DrawEntries(
                new Rect(
                    contentX,
                    y,
                    contentWidth,
                    panelRectangle.yMax - y - 14f));
            GUI.EndClip();
            DrawLaserReveal(
                revealEdge,
                worldPanelRectangle.y,
                worldPanelRectangle.height,
                SignalColor,
                revealProgress);
        }

        public void SetExportStatus(string status) =>
            exportStatus = status ?? string.Empty;

        private void DrawFilters(float x, float y, float width)
        {
            const float gap = 6f;
            float compactWidth = Mathf.Max(
                82f,
                (width - 220f - (gap * 2f)) * 0.5f);
            DrawFilterButton(
                new Rect(x, y, compactWidth, 26f),
                GameplayDialogueChannel.Dialogue,
                "DIALOGUE");
            DrawFilterButton(
                new Rect(x + compactWidth + gap, y, compactWidth, 26f),
                GameplayDialogueChannel.System,
                "SYSTEM");
            DrawFilterButton(
                new Rect(
                    x + (compactWidth * 2f) + (gap * 2f),
                    y,
                    width - (compactWidth * 2f) - (gap * 2f),
                    26f),
                GameplayDialogueChannel.CombatDiagnostics,
                "COMBAT DIAGNOSTICS");
        }

        private void DrawFilterButton(
            Rect rectangle,
            GameplayDialogueChannel channel,
            string label)
        {
            bool active = (filters & channel) != 0;
            if (GUI.Button(
                rectangle,
                active ? "[X]  " + label : "[ ]  " + label,
                buttonStyle))
            {
                ToggleFilter(channel);
            }

            DrawGlowFrame(rectangle, active ? ButtonEdgeColor : BorderColor);
        }

        private void DrawEntries(Rect viewport)
        {
            if (source != null && source.LatestSequence != observedSequence)
            {
                observedSequence = source.LatestSequence;
                scrollPosition.y = float.MaxValue;
            }

            float viewWidth = Mathf.Max(1f, viewport.width - 18f);
            float contentHeight = CalculateContentHeight(viewWidth);
            var contentRectangle = new Rect(
                0f,
                0f,
                viewWidth,
                Mathf.Max(viewport.height, contentHeight));
            scrollPosition = GUI.BeginScrollView(
                viewport,
                scrollPosition,
                contentRectangle);

            float y = 4f;
            int visibleEntries = 0;
            if (source != null)
            {
                foreach (GameplayDialogueEntry entry in source.Entries)
                {
                    if ((filters & entry.Channel) == 0)
                    {
                        continue;
                    }

                    visibleEntries++;
                    float bodyHeight = bodyStyle.CalcHeight(
                        new GUIContent(entry.Message),
                        viewWidth - 20f);
                    float entryHeight = bodyHeight + 39f;
                    DrawRectangle(
                        new Rect(0f, y, viewWidth, entryHeight),
                        GameplayVisualPalette.WithAlpha(
                            entry.Sequence == source.HighlightedSequence
                                ? GameplayVisualPalette.SignalOrangeGlow
                                : GameplayVisualPalette.ButtonNormal,
                            entry.Sequence == source.HighlightedSequence
                                ? 0.2f
                                : 0.3f));
                    GUI.Label(
                        new Rect(10f, y + 6f, viewWidth - 20f, 18f),
                        $"#{entry.Sequence:0000}  {GetChannelLabel(entry.Channel)} - {entry.Title.ToUpperInvariant()}",
                        entryHeaderStyle);
                    GUI.Label(
                        new Rect(10f, y + 26f, viewWidth - 20f, bodyHeight),
                        entry.Message,
                        bodyStyle);
                    y += entryHeight + 6f;
                }
            }

            if (visibleEntries == 0)
            {
                GUI.Label(
                    new Rect(8f, 8f, viewWidth - 16f, 36f),
                    emptyMessage,
                    emptyStyle);
            }

            GUI.EndScrollView();
        }

        private float CalculateContentHeight(float width)
        {
            float height = 8f;
            if (source == null)
            {
                return 48f;
            }

            foreach (GameplayDialogueEntry entry in source.Entries)
            {
                if ((filters & entry.Channel) == 0)
                {
                    continue;
                }

                height += bodyStyle.CalcHeight(
                    new GUIContent(entry.Message),
                    width - 20f) + 45f;
            }

            return Mathf.Max(48f, height);
        }

        private static string GetChannelLabel(GameplayDialogueChannel channel)
        {
            switch (channel)
            {
                case GameplayDialogueChannel.Dialogue:
                    return "DIALOGUE";
                case GameplayDialogueChannel.System:
                    return "SYSTEM";
                case GameplayDialogueChannel.CombatDiagnostics:
                    return "COMBAT DIAGNOSTICS";
                default:
                    throw new ArgumentOutOfRangeException(nameof(channel), channel, null);
            }
        }

        private void EnsureStyles()
        {
            if (whiteTexture != null)
            {
                return;
            }

            whiteTexture = Texture2D.whiteTexture;
            buttonNormalTexture = CreateTexture(GameplayVisualPalette.ButtonNormal);
            buttonHoverTexture = CreateTexture(GameplayVisualPalette.ButtonHover);
            buttonActiveTexture = CreateTexture(GameplayVisualPalette.ButtonActive);
            headerStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                normal = { textColor = SignalColor },
            };
            entryHeaderStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = 10,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                normal = { textColor = SecondaryTextColor },
            };
            bodyStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = 12,
                wordWrap = true,
                normal = { textColor = PrimaryTextColor },
            };
            emptyStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                normal = { textColor = SecondaryTextColor },
            };
            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                wordWrap = false,
                clipping = TextClipping.Clip,
                padding = new RectOffset(8, 8, 0, 0),
                border = new RectOffset(0, 0, 0, 0),
                normal =
                {
                    background = buttonHoverTexture,
                    textColor = PrimaryTextColor,
                },
                hover =
                {
                    background = buttonActiveTexture,
                    textColor = PrimaryTextColor,
                },
                active =
                {
                    background = buttonActiveTexture,
                    textColor = PrimaryTextColor,
                },
                focused =
                {
                    background = buttonHoverTexture,
                    textColor = PrimaryTextColor,
                },
            };
        }

        private void DrawFramedPanel(Rect rectangle)
        {
            DrawRectangle(rectangle, BorderColor);
            DrawRectangle(
                new Rect(
                    rectangle.x + 1f,
                    rectangle.y + 1f,
                    rectangle.width - 2f,
                    rectangle.height - 2f),
                PanelColor);
        }

        private void DrawSectionRule(float x, float y, float width)
        {
            DrawGlowLine(new Rect(x, y, 54f, 1f), SignalColor);
            DrawGlowLine(
                new Rect(x + 58f, y, width - 58f, 1f),
                SignalSoftColor);
        }

        private void DrawGlowFrame(Rect rectangle, Color color)
        {
            DrawGlowLine(
                new Rect(rectangle.x, rectangle.y, rectangle.width, 1f),
                color);
            DrawGlowLine(
                new Rect(
                    rectangle.x,
                    rectangle.yMax - 1f,
                    rectangle.width,
                    1f),
                color);
            DrawGlowLine(
                new Rect(rectangle.x, rectangle.y, 1f, rectangle.height),
                color);
            DrawGlowLine(
                new Rect(
                    rectangle.xMax - 1f,
                    rectangle.y,
                    1f,
                    rectangle.height),
                color);
        }

        private void DrawGlowLine(Rect rectangle, Color color)
        {
            bool horizontal = rectangle.width >= rectangle.height;
            Rect outerGlow = horizontal
                ? new Rect(
                    rectangle.x,
                    rectangle.y - 4f,
                    rectangle.width,
                    rectangle.height + 8f)
                : new Rect(
                    rectangle.x - 4f,
                    rectangle.y,
                    rectangle.width + 8f,
                    rectangle.height);
            Rect innerGlow = horizontal
                ? new Rect(
                    rectangle.x,
                    rectangle.y - 2f,
                    rectangle.width,
                    rectangle.height + 4f)
                : new Rect(
                    rectangle.x - 2f,
                    rectangle.y,
                    rectangle.width + 4f,
                    rectangle.height);
            DrawRectangle(
                outerGlow,
                new Color(color.r, color.g, color.b, color.a * 0.06f));
            DrawRectangle(
                innerGlow,
                new Color(color.r, color.g, color.b, color.a * 0.14f));
            DrawRectangle(rectangle, color);
        }

        private void DrawLaserReveal(
            float x,
            float y,
            float height,
            Color color,
            float progress)
        {
            if (progress <= 0f || progress >= 1f)
            {
                return;
            }

            DrawGlowLine(
                new Rect(
                    x - (flyoutMotion.LaserOuterWidth * 0.5f),
                    y,
                    flyoutMotion.LaserOuterWidth,
                    height),
                GameplayVisualPalette.WithAlpha(
                    color,
                    flyoutMotion.LaserOuterAlpha));
            DrawGlowLine(
                new Rect(
                    x - (flyoutMotion.LaserInnerWidth * 0.5f),
                    y,
                    flyoutMotion.LaserInnerWidth,
                    height),
                GameplayVisualPalette.WithAlpha(
                    color,
                    flyoutMotion.LaserInnerAlpha));
            DrawGlowLine(
                new Rect(
                    x - (flyoutMotion.LaserCoreWidth * 0.5f),
                    y,
                    flyoutMotion.LaserCoreWidth,
                    height),
                color);
        }

        private void EnsureFlyoutMotion()
        {
            if (flyoutMotion == null)
            {
                flyoutMotion = GameplayFlyoutMotionProfile.LoadDefault();
            }
        }

        private void DrawRectangle(Rect rectangle, Color color)
        {
            Color previousColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rectangle, whiteTexture);
            GUI.color = previousColor;
        }

        private static Texture2D CreateTexture(Color color)
        {
            var texture = new Texture2D(1, 1)
            {
                hideFlags = HideFlags.HideAndDontSave,
            };
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        private void OnDestroy()
        {
            Destroy(buttonNormalTexture);
            Destroy(buttonHoverTexture);
            Destroy(buttonActiveTexture);
        }
    }
}
