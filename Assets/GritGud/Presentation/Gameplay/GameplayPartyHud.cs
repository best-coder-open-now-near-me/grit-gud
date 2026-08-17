using System;
using GritGud.Application.Gameplay;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class GameplayPartyHud : MonoBehaviour
    {
        private const float ReferenceHeight = 900f;
        private const float Margin = 14f;
        private const float PanelWidth = 304f;
        private const float HeaderHeight = 28f;
        private const float MemberHeight = 52f;
        private const float MemberGap = 6f;
        private static readonly Color PanelColor = GameplayVisualPalette.HudPanel;
        private static readonly Color BorderColor = GameplayVisualPalette.HudBorder;
        private static readonly Color SelectedColor =
            GameplayVisualPalette.HudSecondarySignal;
        private static readonly Color CommandColor =
            GameplayVisualPalette.HudPrimarySignal;
        private static readonly Color PrimaryTextColor =
            GameplayVisualPalette.HudTextPrimary;
        private static readonly Color SecondaryTextColor =
            GameplayVisualPalette.HudTextSecondary;

        private GameplaySession gameplay;
        private GameplayPartyControlSession partyControl;
        private IGameplayInputSource inputSource;
        private GUIStyle headerStyle;
        private GUIStyle nameStyle;
        private GUIStyle detailStyle;
        private GUIStyle disabledDetailStyle;
        private GUIStyle memberButtonStyle;
        private GUIStyle actionButtonStyle;
        private Texture2D memberNormalTexture;
        private Texture2D memberHoverTexture;
        private Texture2D memberActiveTexture;
        private Texture2D whiteTexture;
        private GameplayHudTextureSet textureSet;
        private string status = string.Empty;
        private Func<bool> replayAvailable;
        private Action replayRequested;

        public bool IsVisible => enabled;

        internal GameplayPartyHudModel CurrentModel =>
            gameplay == null || partyControl == null
                ? null
                : GameplayPartyHudModelBuilder.Build(gameplay, partyControl);

        public void Bind(
            GameplaySession session,
            GameplayPartyControlSession control,
            IGameplayInputSource authoritativeInputSource,
            Func<bool> canOpenReplay = null,
            Action openReplay = null)
        {
            Unbind();
            gameplay = session ?? throw new ArgumentNullException(nameof(session));
            partyControl = control ?? throw new ArgumentNullException(nameof(control));
            inputSource = authoritativeInputSource ?? throw new ArgumentNullException(
                nameof(authoritativeInputSource));
            replayAvailable = canOpenReplay;
            replayRequested = openReplay;
            status = string.Empty;
            partyControl.ControlChanged += HandleControlChanged;
            enabled = true;
        }

        public void Unbind()
        {
            if (partyControl != null)
                partyControl.ControlChanged -= HandleControlChanged;

            gameplay = null;
            partyControl = null;
            inputSource = null;
            replayAvailable = null;
            replayRequested = null;
            status = string.Empty;
            enabled = false;
        }

        internal void PresentSelectionFailure(
            GameplayPartySelectionFailure failure)
        {
            status = GetFailureMessage(failure);
        }

        internal bool ContainsInteractiveScreenPoint(Vector2 screenPoint)
        {
            GameplayPartyHudModel model = CurrentModel;
            if (model == null)
                return false;

            float uiScale = CalculateUiScale();
            var guiPoint = new Vector2(
                screenPoint.x / uiScale,
                (Screen.height - screenPoint.y) / uiScale);
            return CalculatePanelRectangle(
                Screen.width / uiScale,
                model.Members.Count,
                hasStatus: !string.IsNullOrWhiteSpace(status)).Contains(guiPoint);
        }

        internal static Rect CalculatePanelRectangle(
            float canvasWidth,
            int memberCount,
            bool hasStatus)
        {
            float contentHeight = HeaderHeight
                + (memberCount * MemberHeight)
                + (Mathf.Max(0, memberCount - 1) * MemberGap)
                + 16f;
            if (hasStatus)
                contentHeight += 22f;
            return new Rect(
                canvasWidth - Margin - PanelWidth,
                Margin,
                PanelWidth,
                contentHeight);
        }

        private void OnGUI()
        {
            GameplayPartyHudModel model = CurrentModel;
            if (model == null)
                return;

            EnsureStyles();
            float uiScale = CalculateUiScale();
            float canvasWidth = Screen.width / uiScale;
            Matrix4x4 previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(uiScale, uiScale, 1f));
            Draw(model, CalculatePanelRectangle(
                canvasWidth,
                model.Members.Count,
                hasStatus: !string.IsNullOrWhiteSpace(status)));
            GUI.matrix = previousMatrix;
        }

        private void Draw(GameplayPartyHudModel model, Rect panel)
        {
            DrawRectangle(panel, BorderColor);
            DrawRectangle(
                new Rect(
                    panel.x + 1f,
                    panel.y + 1f,
                    panel.width - 2f,
                    panel.height - 2f),
                PanelColor);

            string header;
            if (model.Members.Count == 1)
            {
                header = "CHARACTER";
            }
            else
            {
                string binding = inputSource.GetBindingDisplay(
                    GameplayControl.CyclePartyMember);
                header = model.InitiativeControlsSelection
                    ? "PARTY - INITIATIVE CONTROL"
                    : $"PARTY - [{binding}] SWITCH";
            }
            GUI.Label(
                new Rect(panel.x + 12f, panel.y + 5f, panel.width - 24f, 20f),
                header,
                headerStyle);

            float y = panel.y + HeaderHeight;
            foreach (GameplayPartyMemberHudModel member in model.Members)
            {
                DrawMember(
                    member,
                    new Rect(
                        panel.x + 8f,
                        y,
                        panel.width - 16f,
                        MemberHeight));
                y += MemberHeight + MemberGap;
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                GUI.Label(
                    new Rect(panel.x + 12f, y, panel.width - 24f, 18f),
                    status,
                    disabledDetailStyle);
            }
        }

        private void DrawMember(
            GameplayPartyMemberHudModel member,
            Rect rectangle)
        {
            bool hasReplay = member.Commanding
                && replayAvailable?.Invoke() == true;
            bool hasActionRail = hasReplay;
            Rect selectionRectangle = hasActionRail
                ? new Rect(
                    rectangle.x,
                    rectangle.y,
                    rectangle.width - 76f,
                    rectangle.height)
                : rectangle;
            bool previousEnabled = GUI.enabled;
            GUI.enabled = member.CanSelect;
            if (GUI.Button(
                    selectionRectangle,
                    GUIContent.none,
                    memberButtonStyle))
            {
                if (!partyControl.TrySelectActor(
                        member.ActorId,
                        out GameplayPartySelectionFailure failure))
                {
                    PresentSelectionFailure(failure);
                }
            }
            GUI.enabled = previousEnabled;

            Color frameColor = member.Selected
                ? SelectedColor
                : member.Commanding
                    ? CommandColor
                    : BorderColor;
            DrawFrame(rectangle, frameColor, member.Selected ? 2f : 1f);
            if (member.Commanding)
            {
                DrawRectangle(
                    new Rect(rectangle.x, rectangle.y, 3f, rectangle.height),
                    CommandColor);
            }

            GUI.Label(
                new Rect(
                    rectangle.x + 12f,
                    rectangle.y + 4f,
                    rectangle.width - (hasActionRail ? 100f : 24f),
                    20f),
                member.DisplayName.ToUpperInvariant(),
                nameStyle);
            string details = member.Incapacitated
                ? "INCAPACITATED"
                : $"AP {member.TurnBudget.ActionPoints}  -  MOVE "
                    + $"{member.TurnBudget.MovementOpportunity:0.#}  -  "
                    + $"WOUNDS {member.WoundCount}/{member.MaximumWounds}";
            GUI.Label(
                new Rect(
                    rectangle.x + 12f,
                    rectangle.y + 26f,
                    rectangle.width - (hasActionRail ? 100f : 24f),
                    18f),
                details,
                member.Incapacitated ? disabledDetailStyle : detailStyle);
            if (hasReplay
                && GUI.Button(
                    new Rect(
                        rectangle.xMax - 68f,
                        rectangle.y + 5f,
                        58f,
                        18f),
                    "REPLAY",
                    actionButtonStyle))
            {
                replayRequested?.Invoke();
            }
        }

        private void HandleControlChanged(GameplayPartyControlSnapshot _)
        {
            status = string.Empty;
        }

        private void EnsureStyles()
        {
            if (textureSet != null)
                return;

            textureSet = new GameplayHudTextureSet();
            whiteTexture = textureSet.White;
            memberNormalTexture = textureSet.ButtonNormal;
            memberHoverTexture = textureSet.ButtonHover;
            memberActiveTexture = textureSet.ButtonActive;
            headerStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                normal = { textColor = CommandColor },
            };
            nameStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                normal = { textColor = PrimaryTextColor },
            };
            detailStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 10,
                fontStyle = FontStyle.Bold,
                normal = { textColor = SecondaryTextColor },
            };
            disabledDetailStyle = new GUIStyle(detailStyle)
            {
                normal = { textColor = SelectedColor },
            };
            memberButtonStyle = new GUIStyle(GUI.skin.button)
            {
                border = new RectOffset(0, 0, 0, 0),
                normal = { background = memberNormalTexture },
                hover = { background = memberHoverTexture },
                active = { background = memberActiveTexture },
                focused = { background = memberNormalTexture },
                onNormal = { background = memberActiveTexture },
            };
            actionButtonStyle = new GUIStyle(memberButtonStyle)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 9,
                fontStyle = FontStyle.Bold,
                normal =
                {
                    background = memberNormalTexture,
                    textColor = PrimaryTextColor,
                },
                hover =
                {
                    background = memberHoverTexture,
                    textColor = PrimaryTextColor,
                },
                active =
                {
                    background = memberActiveTexture,
                    textColor = PrimaryTextColor,
                },
            };
        }

        private void DrawFrame(Rect rectangle, Color color, float thickness)
        {
            DrawRectangle(new Rect(rectangle.x, rectangle.y, rectangle.width, thickness), color);
            DrawRectangle(new Rect(rectangle.x, rectangle.yMax - thickness, rectangle.width, thickness), color);
            DrawRectangle(new Rect(rectangle.x, rectangle.y, thickness, rectangle.height), color);
            DrawRectangle(new Rect(rectangle.xMax - thickness, rectangle.y, thickness, rectangle.height), color);
        }

        private void DrawRectangle(Rect rectangle, Color color)
        {
            Color previousColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rectangle, whiteTexture);
            GUI.color = previousColor;
        }

        private static float CalculateUiScale() => Mathf.Clamp(
            Screen.height / ReferenceHeight,
            0.75f,
            1.35f);

        private static string GetFailureMessage(
            GameplayPartySelectionFailure failure)
        {
            switch (failure)
            {
                case GameplayPartySelectionFailure.None:
                    return string.Empty;
                case GameplayPartySelectionFailure.NotPartyMember:
                    return "ACTOR IS NOT IN THE PLAYER PARTY";
                case GameplayPartySelectionFailure.ActorIncapacitated:
                    return "INCAPACITATED ACTORS CANNOT BE SELECTED";
                case GameplayPartySelectionFailure.TurnBasedControlFollowsInitiative:
                    return "PARTY CONTROL FOLLOWS INITIATIVE IN TURN MODE";
                case GameplayPartySelectionFailure.NoAlternateCapableActor:
                    return "NO OTHER CAPABLE PARTY MEMBER";
                default:
                    throw new ArgumentOutOfRangeException(nameof(failure));
            }
        }

        private void OnDestroy()
        {
            Unbind();
            textureSet?.Dispose();
            textureSet = null;
            memberNormalTexture = null;
            memberHoverTexture = null;
            memberActiveTexture = null;
            whiteTexture = null;
        }
    }
}
