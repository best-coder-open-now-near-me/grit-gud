using System;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    internal sealed class GameplayHudStyleSet : IDisposable
    {
        public static readonly Color PanelStrongColor =
            GameplayVisualPalette.HudPanel;
        public static readonly Color BorderColor =
            GameplayVisualPalette.HudBorder;
        public static readonly Color SignalColor =
            GameplayVisualPalette.HudPrimarySignal;
        public static readonly Color SignalSoftColor =
            GameplayVisualPalette.WithAlpha(
                GameplayVisualPalette.HudPrimarySignal,
                0.16f);
        public static readonly Color EquipmentSignalColor =
            GameplayVisualPalette.HudSecondarySignal;
        public static readonly Color ModeButtonEdgeColor =
            GameplayVisualPalette.WithAlpha(
                GameplayVisualPalette.HudPrimarySignal,
                0.48f);
        public static readonly Color ModeButtonTextColor =
            GameplayVisualPalette.HudTextBright;
        public static readonly Color PrimaryTextColor =
            GameplayVisualPalette.HudTextPrimary;
        public static readonly Color SecondaryTextColor =
            GameplayVisualPalette.HudTextSecondary;

        public GUIStyle Header { get; private set; }
        public GUIStyle Body { get; private set; }
        public GUIStyle Guidance { get; private set; }
        public GUIStyle Controls { get; private set; }
        public GUIStyle CommandHints { get; private set; }
        public GUIStyle BodyRegion { get; private set; }
        public GUIStyle WoundedBodyRegion { get; private set; }
        public GUIStyle Button { get; private set; }
        public GUIStyle Status { get; private set; }
        public GUIStyle Tab { get; private set; }
        public GUIStyle ResourceLabel { get; private set; }
        public GUIStyle ResourceValue { get; private set; }
        public GUIStyle HotbarNumber { get; private set; }
        public GUIStyle HotbarItem { get; private set; }
        public GUIStyle PendingPowerButton { get; private set; }
        public GUIStyle EquipmentButton { get; private set; }
        public GUIStyle EquippedButton { get; private set; }
        public GUIStyle EquipmentConfirmation { get; private set; }
        public GUIStyle Tooltip { get; private set; }
        public GUIStyle ConfirmationFlyout { get; private set; }
        public GUIStyle WarningHint { get; private set; }
        public GUIStyle ChoiceHeader { get; private set; }
        public GUIStyle TipTitle { get; private set; }
        public GUIStyle TipBody { get; private set; }
        public GUIStyle ModeButton { get; private set; }
        public Texture2D WhiteTexture { get; private set; }
        public Texture2D ButtonNormalTexture { get; private set; }
        public Texture2D ButtonHoverTexture { get; private set; }
        public Texture2D ButtonActiveTexture { get; private set; }
        public Texture2D EquipmentConfirmationTexture { get; private set; }
        public GameplayHudTextureSet TextureSet { get; private set; }

        public void Ensure()
        {
            if (TextureSet != null)
                return;

            TextureSet = new GameplayHudTextureSet();
            WhiteTexture = TextureSet.White;
            ButtonNormalTexture = TextureSet.ButtonNormal;
            ButtonHoverTexture = TextureSet.ButtonHover;
            ButtonActiveTexture = TextureSet.ButtonActive;
            EquipmentConfirmationTexture = TextureSet.EquipmentConfirmation;

            Header = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                normal = { textColor = SignalColor },
            };
            ChoiceHeader = new GUIStyle(Header)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleLeft,
            };
            TipTitle = new GUIStyle(Header)
            {
                fontSize = 10,
                clipping = TextClipping.Clip,
            };
            TipBody = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = 11,
                wordWrap = true,
                normal = { textColor = PrimaryTextColor },
            };
            Body = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                normal = { textColor = PrimaryTextColor },
            };
            Guidance = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = 12,
                wordWrap = true,
                normal = { textColor = PrimaryTextColor },
            };
            Controls = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                normal = { textColor = SecondaryTextColor },
            };
            CommandHints = new GUIStyle(Controls)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 11,
                fontStyle = FontStyle.Normal,
                clipping = TextClipping.Clip,
                wordWrap = false,
                normal = { textColor = PrimaryTextColor },
            };
            Status = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                clipping = TextClipping.Clip,
                normal = { textColor = SecondaryTextColor },
            };
            Tab = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 9,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                border = new RectOffset(0, 0, 0, 0),
                normal =
                {
                    background = ButtonNormalTexture,
                    textColor = SignalColor,
                },
                hover =
                {
                    background = ButtonHoverTexture,
                    textColor = PrimaryTextColor,
                },
                active =
                {
                    background = ButtonActiveTexture,
                    textColor = PrimaryTextColor,
                },
            };
            ResourceLabel = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 9,
                fontStyle = FontStyle.Bold,
                clipping = TextClipping.Clip,
                normal = { textColor = SignalColor },
            };
            ResourceValue = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleRight,
                fontSize = 9,
                fontStyle = FontStyle.Bold,
                clipping = TextClipping.Clip,
                normal = { textColor = PrimaryTextColor },
            };
            HotbarNumber = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                clipping = TextClipping.Clip,
                padding = new RectOffset(0, 0, 0, 0),
                normal = { textColor = ModeButtonTextColor },
            };
            ModeButton = new GUIStyle(GUI.skin.button)
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
                    background = ButtonHoverTexture,
                    textColor = ModeButtonTextColor,
                },
                hover =
                {
                    background = ButtonActiveTexture,
                    textColor = ModeButtonTextColor,
                },
                active =
                {
                    background = ButtonActiveTexture,
                    textColor = ModeButtonTextColor,
                },
                focused =
                {
                    background = ButtonHoverTexture,
                    textColor = ModeButtonTextColor,
                },
            };
            BodyRegion = new GUIStyle(ModeButton)
            {
                fontSize = 8,
                padding = new RectOffset(2, 2, 0, 0),
                normal =
                {
                    background = ButtonNormalTexture,
                    textColor = PrimaryTextColor,
                },
            };
            WoundedBodyRegion = new GUIStyle(BodyRegion)
            {
                normal =
                {
                    background = EquipmentConfirmationTexture,
                    textColor = EquipmentSignalColor,
                },
            };
            HotbarItem = new GUIStyle(ModeButton)
            {
                fontSize = 10,
                padding = new RectOffset(5, 5, 10, 0),
            };
            PendingPowerButton = new GUIStyle(HotbarItem)
            {
                normal =
                {
                    background = EquipmentConfirmationTexture,
                    textColor = EquipmentSignalColor,
                },
                hover =
                {
                    background = EquipmentConfirmationTexture,
                    textColor = GameplayVisualPalette.TextBright,
                },
                active =
                {
                    background = EquipmentConfirmationTexture,
                    textColor = GameplayVisualPalette.TextBright,
                },
                focused =
                {
                    background = EquipmentConfirmationTexture,
                    textColor = EquipmentSignalColor,
                },
            };
            EquipmentButton = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 8,
                fontStyle = FontStyle.Bold,
                wordWrap = false,
                clipping = TextClipping.Clip,
                border = new RectOffset(0, 0, 0, 0),
                normal =
                {
                    background = ButtonNormalTexture,
                    textColor = PrimaryTextColor,
                },
                hover =
                {
                    background = ButtonHoverTexture,
                    textColor = ModeButtonTextColor,
                },
                active =
                {
                    background = ButtonActiveTexture,
                    textColor = ModeButtonTextColor,
                },
            };
            EquippedButton = new GUIStyle(EquipmentButton)
            {
                contentOffset = new Vector2(0f, 1f),
                normal =
                {
                    background = ButtonActiveTexture,
                    textColor = ModeButtonTextColor,
                },
                hover =
                {
                    background = ButtonActiveTexture,
                    textColor = ModeButtonTextColor,
                },
            };
            EquipmentConfirmation = new GUIStyle(EquipmentButton)
            {
                normal =
                {
                    background = EquipmentConfirmationTexture,
                    textColor = EquipmentSignalColor,
                },
                hover =
                {
                    background = EquipmentConfirmationTexture,
                    textColor = GameplayVisualPalette.TextBright,
                },
                active =
                {
                    background = EquipmentConfirmationTexture,
                    textColor = GameplayVisualPalette.TextBright,
                },
            };
            Tooltip = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = 10,
                fontStyle = FontStyle.Bold,
                wordWrap = false,
                padding = new RectOffset(10, 10, 8, 8),
                border = new RectOffset(0, 0, 0, 0),
                normal =
                {
                    background = ButtonNormalTexture,
                    textColor = PrimaryTextColor,
                },
            };
            ConfirmationFlyout = new GUIStyle(Tooltip)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = 10,
                wordWrap = true,
                normal =
                {
                    background = ButtonNormalTexture,
                    textColor = PrimaryTextColor,
                },
            };
            WarningHint = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip,
                fontSize = 9,
                fontStyle = FontStyle.Bold,
                wordWrap = false,
                padding = new RectOffset(0, 0, 0, 0),
                normal =
                {
                    background = null,
                    textColor = EquipmentSignalColor,
                },
            };
            Button = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                padding = new RectOffset(14, 10, 0, 0),
                border = new RectOffset(0, 0, 0, 0),
                normal =
                {
                    background = ButtonNormalTexture,
                    textColor = PrimaryTextColor,
                },
                hover =
                {
                    background = ButtonHoverTexture,
                    textColor = SignalColor,
                },
                active =
                {
                    background = ButtonActiveTexture,
                    textColor = SignalColor,
                },
            };
        }

        public void Dispose()
        {
            TextureSet?.Dispose();
            TextureSet = null;
            WhiteTexture = null;
            ButtonNormalTexture = null;
            ButtonHoverTexture = null;
            ButtonActiveTexture = null;
            EquipmentConfirmationTexture = null;
            Header = null;
            Body = null;
            Guidance = null;
            Controls = null;
            CommandHints = null;
            BodyRegion = null;
            WoundedBodyRegion = null;
            Button = null;
            Status = null;
            Tab = null;
            ResourceLabel = null;
            ResourceValue = null;
            HotbarNumber = null;
            HotbarItem = null;
            PendingPowerButton = null;
            EquipmentButton = null;
            EquippedButton = null;
            EquipmentConfirmation = null;
            Tooltip = null;
            ConfirmationFlyout = null;
            WarningHint = null;
            ChoiceHeader = null;
            TipTitle = null;
            TipBody = null;
            ModeButton = null;
        }
    }
}
