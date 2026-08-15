using System.Collections.Generic;
using UnityEngine;

namespace GritGud.Presentation.LevelEditing.UI
{
    public sealed class LevelEditorGuiStyles
    {
        private GUISkin sourceSkin;
        private GUISkin themedSkin;
        private readonly List<Texture2D> generatedTextures = new List<Texture2D>();
        private GUIStyle toolbar;
        private GUIStyle panel;
        private GUIStyle statusBar;
        private GUIStyle floatingPanel;
        private GUIStyle sectionHeader;
        private GUIStyle mutedLabel;

        public GUIStyle Toolbar => toolbar;
        public GUIStyle Panel => panel;
        public GUIStyle StatusBar => statusBar;
        public GUIStyle FloatingPanel => floatingPanel;
        public GUIStyle SectionHeader => sectionHeader;
        public GUIStyle MutedLabel => mutedLabel;

        public GUISkin ResolveSkin(GUISkin currentSkin)
        {
            if (currentSkin == null)
                throw new System.ArgumentNullException(nameof(currentSkin));

            EnsureCurrentSkin(currentSkin);
            return themedSkin;
        }

        private void EnsureCurrentSkin(GUISkin currentSkin)
        {
            if (ReferenceEquals(sourceSkin, currentSkin) && themedSkin != null)
                return;

            ReleaseGeneratedResources();
            sourceSkin = currentSkin;
            themedSkin = Object.Instantiate(sourceSkin);
            themedSkin.name = "Grit Gud Level Editor Dark";
            themedSkin.hideFlags = HideFlags.HideAndDontSave;

            Texture2D panelTexture = CreateTexture("Panel", LevelEditorTheme.PanelBackground);
            Texture2D buttonTexture = CreateTexture(
                "Button",
                LevelEditorTheme.ButtonBackground);
            Texture2D buttonHoverTexture = CreateTexture(
                "Button Hover",
                LevelEditorTheme.ButtonHoverBackground);
            Texture2D buttonPressedTexture = CreateTexture(
                "Button Pressed",
                LevelEditorTheme.ButtonPressedBackground);
            Texture2D buttonSelectedTexture = CreateTexture(
                "Button Selected",
                LevelEditorTheme.ButtonSelectedBackground);
            Texture2D fieldTexture = CreateTexture("Field", LevelEditorTheme.FieldBackground);
            Texture2D fieldFocusedTexture = CreateTexture(
                "Field Focused",
                LevelEditorTheme.FieldFocusedBackground);

            StyleSurface(themedSkin.box, panelTexture, LevelEditorTheme.PrimaryText);
            StyleButton(
                themedSkin.button,
                buttonTexture,
                buttonHoverTexture,
                buttonPressedTexture,
                buttonSelectedTexture);
            StyleTextInput(themedSkin.textField, fieldTexture, fieldFocusedTexture);
            StyleTextInput(themedSkin.textArea, fieldTexture, fieldFocusedTexture);
            StyleText(themedSkin.label, LevelEditorTheme.PrimaryText);
            StyleText(themedSkin.toggle, LevelEditorTheme.PrimaryText);
            themedSkin.settings.cursorColor = LevelEditorTheme.PrimaryText;
            themedSkin.settings.selectionColor = LevelEditorTheme.ButtonSelectedBackground;

            themedSkin.window.normal.background = CreateTexture(
                "Window",
                LevelEditorTheme.FloatingPanelBackground);
            themedSkin.window.normal.textColor = LevelEditorTheme.PrimaryText;
            themedSkin.window.border = new RectOffset();

            toolbar = CreateChromeStyle(
                sourceSkin.box,
                "Toolbar",
                LevelEditorTheme.ToolbarBackground,
                new RectOffset(
                    LevelEditorGuiMetrics.ToolbarHorizontalPadding,
                    LevelEditorGuiMetrics.ToolbarHorizontalPadding,
                    LevelEditorGuiMetrics.ToolbarVerticalPadding,
                    LevelEditorGuiMetrics.ToolbarVerticalPadding));
            panel = CreateChromeStyle(
                sourceSkin.box,
                "Panel",
                LevelEditorTheme.PanelBackground,
                new RectOffset(
                    LevelEditorGuiMetrics.PanelHorizontalPadding,
                    LevelEditorGuiMetrics.PanelHorizontalPadding,
                    LevelEditorGuiMetrics.PanelVerticalPadding,
                    LevelEditorGuiMetrics.PanelVerticalPadding));
            statusBar = CreateChromeStyle(
                sourceSkin.box,
                "Status Bar",
                LevelEditorTheme.StatusBarBackground,
                new RectOffset(
                    LevelEditorGuiMetrics.StatusBarHorizontalPadding,
                    LevelEditorGuiMetrics.StatusBarHorizontalPadding,
                    LevelEditorGuiMetrics.StatusBarVerticalPadding,
                    LevelEditorGuiMetrics.StatusBarVerticalPadding));
            floatingPanel = new GUIStyle(themedSkin.window)
            {
                name = "Floating Panel",
            };
            sectionHeader = new GUIStyle(themedSkin.box)
            {
                name = "Section Header",
                alignment = TextAnchor.MiddleLeft,
                fontStyle = FontStyle.Bold,
                padding = new RectOffset(
                    LevelEditorGuiMetrics.SectionHeaderLeftPadding,
                    LevelEditorGuiMetrics.SectionHeaderRightPadding,
                    LevelEditorGuiMetrics.SectionHeaderVerticalPadding,
                    LevelEditorGuiMetrics.SectionHeaderVerticalPadding),
            };
            sectionHeader.normal.background = CreateTexture(
                "Section Header",
                LevelEditorTheme.SectionHeaderBackground);
            sectionHeader.normal.textColor = LevelEditorTheme.SectionHeaderText;
            sectionHeader.hover.background = sectionHeader.normal.background;
            sectionHeader.hover.textColor = LevelEditorTheme.PrimaryText;
            sectionHeader.active.background = buttonPressedTexture;
            sectionHeader.active.textColor = LevelEditorTheme.PrimaryText;
            sectionHeader.border = new RectOffset();
            mutedLabel = new GUIStyle(themedSkin.label)
            {
                name = "Muted Label",
            };
            StyleText(mutedLabel, LevelEditorTheme.MutedText);
        }

        private GUIStyle CreateChromeStyle(
            GUIStyle source,
            string name,
            Color background,
            RectOffset padding)
        {
            GUIStyle style = new GUIStyle(source)
            {
                name = name,
                padding = padding,
                border = new RectOffset(),
            };
            Texture2D texture = CreateTexture(name, background);
            style.normal.background = texture;
            style.normal.textColor = LevelEditorTheme.PrimaryText;
            return style;
        }

        private Texture2D CreateTexture(string name, Color color)
        {
            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                name = $"Level Editor {name}",
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Point,
            };
            texture.SetPixel(0, 0, color);
            texture.Apply(false, true);
            generatedTextures.Add(texture);
            return texture;
        }

        private static void StyleSurface(GUIStyle style, Texture2D texture, Color textColor)
        {
            style.normal.background = texture;
            style.normal.textColor = textColor;
            style.border = new RectOffset();
        }

        private static void StyleButton(
            GUIStyle style,
            Texture2D normal,
            Texture2D hover,
            Texture2D pressed,
            Texture2D selected)
        {
            SetState(style.normal, normal, LevelEditorTheme.PrimaryText);
            SetState(style.hover, hover, LevelEditorTheme.PrimaryText);
            SetState(style.active, pressed, LevelEditorTheme.PrimaryText);
            SetState(style.focused, hover, LevelEditorTheme.PrimaryText);
            SetState(style.onNormal, selected, LevelEditorTheme.PrimaryText);
            SetState(style.onHover, hover, LevelEditorTheme.PrimaryText);
            SetState(style.onActive, pressed, LevelEditorTheme.PrimaryText);
            SetState(style.onFocused, selected, LevelEditorTheme.PrimaryText);
            style.border = new RectOffset();
        }

        private static void StyleTextInput(
            GUIStyle style,
            Texture2D normal,
            Texture2D focused)
        {
            SetState(style.normal, normal, LevelEditorTheme.PrimaryText);
            SetState(style.hover, normal, LevelEditorTheme.PrimaryText);
            SetState(style.active, focused, LevelEditorTheme.PrimaryText);
            SetState(style.focused, focused, LevelEditorTheme.PrimaryText);
            style.border = new RectOffset();
        }

        private static void StyleText(GUIStyle style, Color textColor)
        {
            style.normal.textColor = textColor;
            style.hover.textColor = textColor;
            style.active.textColor = textColor;
            style.focused.textColor = textColor;
            style.onNormal.textColor = textColor;
            style.onHover.textColor = textColor;
            style.onActive.textColor = textColor;
            style.onFocused.textColor = textColor;
        }

        private static void SetState(
            GUIStyleState state,
            Texture2D background,
            Color textColor)
        {
            state.background = background;
            state.textColor = textColor;
        }

        private void ReleaseGeneratedResources()
        {
            if (themedSkin != null)
                Destroy(themedSkin);
            for (int index = 0; index < generatedTextures.Count; index++)
                Destroy(generatedTextures[index]);
            generatedTextures.Clear();
            themedSkin = null;
        }

        private static void Destroy(Object value)
        {
            if (value == null)
                return;
            if (UnityEngine.Application.isPlaying)
                Object.Destroy(value);
            else
                Object.DestroyImmediate(value);
        }
    }
}
