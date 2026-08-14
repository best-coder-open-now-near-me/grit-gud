using UnityEngine;

namespace GritGud.Presentation.Bootstrap
{
    /// <summary>
    /// Draws the application entry point from the authored UI palette while
    /// keeping the bootstrap scene reproducible and resolution-independent.
    /// </summary>
    public sealed class StartMenu : MonoBehaviour
    {
        private const float ReferenceWidth = 1600f;
        private const float ReferenceHeight = 900f;

        private static readonly Color BackgroundColor = GameplayVisualPalette.Backdrop;
        private static readonly Color PanelColor = GameplayVisualPalette.WithAlpha(
            GameplayVisualPalette.Panel,
            0.64f);
        private static readonly Color BorderColor = GameplayVisualPalette.WithAlpha(
            GameplayVisualPalette.Border,
            0.36f);
        private static readonly Color SignalColor = GameplayVisualPalette.SignalBlue;
        private static readonly Color MutedSignalColor = GameplayVisualPalette.WithAlpha(
            GameplayVisualPalette.SignalBlue,
            0.22f);
        private static readonly Color PrimaryTextColor = GameplayVisualPalette.TextBright;

        private GUIStyle titleStyle;
        private GUIStyle subtitleStyle;
        private GUIStyle buttonStyle;
        private Texture2D whiteTexture;
        private Texture2D buttonNormalTexture;
        private Texture2D buttonHoverTexture;
        private Texture2D buttonActiveTexture;

        private void OnGUI()
        {
            EnsureStyles();

            float scale = Mathf.Min(Screen.width / ReferenceWidth, Screen.height / ReferenceHeight);
            Matrix4x4 previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(
                new Vector3(
                    (Screen.width - (ReferenceWidth * scale)) * 0.5f,
                    (Screen.height - (ReferenceHeight * scale)) * 0.5f,
                    0f),
                Quaternion.identity,
                new Vector3(scale, scale, 1f));

            DrawBackdrop();
            DrawMenu();

            GUI.matrix = previousMatrix;
        }

        private void DrawBackdrop()
        {
            Rect screen = new Rect(0f, 0f, ReferenceWidth, ReferenceHeight);
            DrawRectangle(screen, BackgroundColor);
            DrawRectangle(
                new Rect(58f, 70f, 500f, 720f),
                BorderColor);
            DrawRectangle(
                new Rect(60f, 72f, 496f, 716f),
                PanelColor);
            DrawGlowLine(
                new Rect(60f, 72f, 3f, 716f),
                SignalColor);
            DrawGlowLine(new Rect(60f, 72f, 128f, 2f), SignalColor);
            DrawGlowLine(new Rect(188f, 72f, 368f, 1f), MutedSignalColor);
            DrawGlowLine(new Rect(60f, 787f, 496f, 1f), MutedSignalColor);
        }

        private void DrawMenu()
        {
            GUI.Label(new Rect(90f, 112f, 440f, 105f), "GRIT GUD", titleStyle);
            GUI.Label(new Rect(94f, 211f, 420f, 42f), "TACTICAL ROLE-PLAYING", subtitleStyle);
            DrawGlowLine(new Rect(94f, 261f, 88f, 2f), SignalColor);
            DrawGlowLine(new Rect(186f, 261f, 248f, 1f), MutedSignalColor);

            if (DrawMenuButton(new Rect(92f, 320f, 350f, 62f), "PLAY MAIN LEVEL"))
            {
                GameBootstrap.Instance.PlayMainLevel();
            }

            if (DrawMenuButton(new Rect(92f, 401f, 350f, 62f), "LEVEL EDITOR"))
            {
                GameBootstrap.Instance.OpenLevelEditor();
            }

            if (DrawMenuButton(new Rect(92f, 482f, 350f, 62f), "QUIT"))
            {
                Quit();
            }

        }

        private bool DrawMenuButton(Rect rectangle, string label)
        {
            bool wasPressed = GUI.Button(rectangle, label, buttonStyle);
            DrawGlowLine(
                new Rect(rectangle.x, rectangle.y, 3f, rectangle.height),
                SignalColor);
            DrawGlowLine(
                new Rect(rectangle.xMax - 12f, rectangle.y + 8f, 1f, rectangle.height - 16f),
                MutedSignalColor);
            return wasPressed;
        }

        private void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            UnityEngine.Application.Quit();
#endif
        }

        private void OnDestroy()
        {
            Destroy(buttonNormalTexture);
            Destroy(buttonHoverTexture);
            Destroy(buttonActiveTexture);
        }

        private void EnsureStyles()
        {
            if (whiteTexture != null)
            {
                return;
            }

            whiteTexture = Texture2D.whiteTexture;
            buttonNormalTexture = CreateTexture(GameplayVisualPalette.WithAlpha(
                GameplayVisualPalette.ButtonNormal,
                0.78f));
            buttonHoverTexture = CreateTexture(GameplayVisualPalette.WithAlpha(
                GameplayVisualPalette.ButtonHover,
                0.92f));
            buttonActiveTexture = CreateTexture(GameplayVisualPalette.WithAlpha(
                GameplayVisualPalette.ButtonActive,
                0.96f));
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 68,
                fontStyle = FontStyle.Bold,
                normal = { textColor = PrimaryTextColor },
            };
            subtitleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                normal = { textColor = SignalColor },
            };
            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 21,
                fontStyle = FontStyle.Bold,
                padding = new RectOffset(26, 16, 0, 0),
                border = new RectOffset(0, 0, 0, 0),
                normal =
                {
                    background = buttonNormalTexture,
                    textColor = GameplayVisualPalette.TextPrimary,
                },
                hover =
                {
                    background = buttonHoverTexture,
                    textColor = PrimaryTextColor,
                },
                active =
                {
                    background = buttonActiveTexture,
                    textColor = PrimaryTextColor,
                },
            };
        }

        private void DrawGlowLine(Rect rectangle, Color color)
        {
            bool horizontal = rectangle.width >= rectangle.height;
            Rect outerGlow = horizontal
                ? new Rect(rectangle.x, rectangle.y - 4f, rectangle.width, rectangle.height + 8f)
                : new Rect(rectangle.x - 4f, rectangle.y, rectangle.width + 8f, rectangle.height);
            Rect innerGlow = horizontal
                ? new Rect(rectangle.x, rectangle.y - 2f, rectangle.width, rectangle.height + 4f)
                : new Rect(rectangle.x - 2f, rectangle.y, rectangle.width + 4f, rectangle.height);

            DrawRectangle(
                outerGlow,
                new Color(color.r, color.g, color.b, color.a * 0.06f));
            DrawRectangle(
                innerGlow,
                new Color(color.r, color.g, color.b, color.a * 0.14f));
            DrawRectangle(rectangle, color);
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
    }
}
