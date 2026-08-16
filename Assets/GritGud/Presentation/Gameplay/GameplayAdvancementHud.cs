using System;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class GameplayAdvancementHud : MonoBehaviour
    {
        private const float ReferenceHeight = 900f;
        private const float PanelWidth = 520f;
        private const float HeaderHeight = 44f;
        private const float OptionHeight = 48f;
        private const float OptionGap = 6f;
        private const float FooterHeight = 82f;
        private const float Margin = 14f;

        private GameplayPartyProgressionSession progression;
        private GameplayPartyControlSession partyControl;
        private GameplayPartyPersistenceSession persistence;
        private string actorId;
        private string pendingOptionId;
        private string status = string.Empty;
        private Vector2 scrollPosition;
        private GUIStyle headerStyle;
        private GUIStyle identityStyle;
        private GUIStyle detailStyle;
        private GUIStyle statusStyle;
        private GUIStyle buttonStyle;
        private GUIStyle closeStyle;
        private Texture2D panelTexture;
        private Texture2D borderTexture;
        private Texture2D buttonTexture;
        private Texture2D buttonHoverTexture;
        private Texture2D buttonActiveTexture;

        internal event Action<bool> OpenChanged;

        public bool IsOpen => enabled && actorId != null;

        internal string ActorId => actorId;

        internal string PendingOptionId => pendingOptionId;

        public void Bind(
            GameplayPartyProgressionSession progressionSession,
            GameplayPartyControlSession control,
            GameplayPartyPersistenceSession persistenceSession)
        {
            Unbind();
            progression = progressionSession
                ?? throw new ArgumentNullException(nameof(progressionSession));
            partyControl = control
                ?? throw new ArgumentNullException(nameof(control));
            persistence = persistenceSession
                ?? throw new ArgumentNullException(nameof(persistenceSession));
            persistence.StatusChanged += HandlePersistenceStatusChanged;
            partyControl.ControlChanged += HandleControlChanged;
            status = persistence.Status;
            enabled = false;
        }

        public void Open(string partyActorId)
        {
            if (progression == null || persistence == null)
            {
                throw new InvalidOperationException(
                    "Bind advancement before opening it.");
            }
            progression.GetProgression(partyActorId);
            bool wasOpen = IsOpen;
            actorId = partyActorId;
            pendingOptionId = null;
            scrollPosition = Vector2.zero;
            status = persistence.Status;
            enabled = true;
            if (!wasOpen)
                OpenChanged?.Invoke(true);
        }

        public void Close()
        {
            bool wasOpen = IsOpen;
            actorId = null;
            pendingOptionId = null;
            scrollPosition = Vector2.zero;
            enabled = false;
            if (wasOpen)
                OpenChanged?.Invoke(false);
        }

        public void Unbind()
        {
            if (persistence != null)
                persistence.StatusChanged -= HandlePersistenceStatusChanged;
            if (partyControl != null)
                partyControl.ControlChanged -= HandleControlChanged;
            Close();
            progression = null;
            partyControl = null;
            persistence = null;
            status = string.Empty;
        }

        internal void SelectOption(string optionId)
        {
            CharacterAdvancementAvailability availability = persistence
                ?.EvaluateAdvancement(actorId, optionId);
            if (availability == null || !availability.CanAdvance)
                return;
            pendingOptionId = optionId;
            status = "Confirm this permanent advancement.";
        }

        internal bool ConfirmPending()
        {
            if (pendingOptionId == null || persistence == null)
                return false;
            if (!persistence.TryAdvance(
                    actorId,
                    pendingOptionId,
                    out CharacterAdvancementFailure failure))
            {
                status = GetFailureMessage(failure);
                pendingOptionId = null;
                return false;
            }

            pendingOptionId = null;
            status = persistence.Status;
            return true;
        }

        internal bool ContainsInteractiveScreenPoint(Vector2 screenPoint)
        {
            if (!IsOpen)
                return false;
            float scale = CalculateUiScale();
            var guiPoint = new Vector2(
                screenPoint.x / scale,
                (Screen.height - screenPoint.y) / scale);
            CharacterProfileDefinition profile =
                progression.GetProgression(actorId).Profile;
            return CalculatePanelRectangle(
                Screen.width / scale,
                Screen.height / scale,
                profile.AdvancementOptions.Count,
                pendingOptionId != null).Contains(guiPoint);
        }

        internal static Rect CalculatePanelRectangle(
            float canvasWidth,
            float canvasHeight,
            int optionCount,
            bool hasConfirmation)
        {
            if (canvasWidth <= 0f || canvasHeight <= 0f)
                throw new ArgumentOutOfRangeException(nameof(canvasWidth));
            if (optionCount < 0)
                throw new ArgumentOutOfRangeException(nameof(optionCount));
            float desiredHeight = HeaderHeight
                + 70f
                + (optionCount * (OptionHeight + OptionGap))
                + (hasConfirmation ? FooterHeight : 32f);
            float height = Mathf.Min(
                desiredHeight,
                Mathf.Max(180f, canvasHeight - (Margin * 2f)));
            return new Rect(
                Mathf.Max(Margin, (canvasWidth - PanelWidth) * 0.5f),
                Mathf.Max(Margin, (canvasHeight - height) * 0.5f),
                Mathf.Min(PanelWidth, canvasWidth - (Margin * 2f)),
                height);
        }

        private void OnGUI()
        {
            if (!IsOpen)
                return;
            if (Event.current.type == EventType.KeyDown
                && Event.current.keyCode == KeyCode.Escape)
            {
                Event.current.Use();
                Close();
                return;
            }

            EnsureStyles();
            float scale = CalculateUiScale();
            float canvasWidth = Screen.width / scale;
            float canvasHeight = Screen.height / scale;
            CharacterProfileDefinition profile =
                progression.GetProgression(actorId).Profile;
            Rect panel = CalculatePanelRectangle(
                canvasWidth,
                canvasHeight,
                profile.AdvancementOptions.Count,
                pendingOptionId != null);

            Matrix4x4 previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            Draw(profile, panel);
            GUI.matrix = previousMatrix;
        }

        private void Draw(CharacterProfileDefinition profile, Rect panel)
        {
            GUI.DrawTexture(panel, borderTexture);
            GUI.DrawTexture(
                new Rect(
                    panel.x + 1f,
                    panel.y + 1f,
                    panel.width - 2f,
                    panel.height - 2f),
                panelTexture);
            GUI.Label(
                new Rect(panel.x + 16f, panel.y + 8f, panel.width - 72f, 28f),
                "CHARACTER ADVANCEMENT",
                headerStyle);
            if (GUI.Button(
                    new Rect(panel.xMax - 44f, panel.y + 8f, 28f, 24f),
                    "X",
                    closeStyle))
            {
                Close();
                return;
            }

            Rect viewport = new Rect(
                panel.x + 12f,
                panel.y + HeaderHeight,
                panel.width - 24f,
                panel.height - HeaderHeight - 12f);
            float contentHeight = 64f
                + (profile.AdvancementOptions.Count
                    * (OptionHeight + OptionGap))
                + (pendingOptionId != null ? FooterHeight : 32f);
            var view = new Rect(
                0f,
                0f,
                viewport.width - 18f,
                Mathf.Max(viewport.height, contentHeight));
            scrollPosition = GUI.BeginScrollView(
                viewport,
                scrollPosition,
                view);

            CharacterProgressionSnapshot snapshot = progression.GetSnapshot(
                actorId);
            GUI.Label(
                new Rect(4f, 0f, view.width - 8f, 22f),
                $"{profile.DisplayName.ToUpperInvariant()}  /  "
                + profile.Archetype.ToUpperInvariant(),
                identityStyle);
            GUI.Label(
                new Rect(4f, 24f, view.width - 8f, 22f),
                $"UNSPENT PROGRESSION: {snapshot.UnspentPoints}",
                detailStyle);

            float y = 58f;
            foreach (CharacterAdvancementOption option in
                profile.AdvancementOptions)
            {
                CharacterAdvancementAvailability availability =
                    persistence.EvaluateAdvancement(actorId, option.Id);
                bool previousEnabled = GUI.enabled;
                GUI.enabled = availability.CanAdvance
                    && pendingOptionId == null;
                string label = FormatSkill(option.SkillId)
                    + $"   {availability.BaselineRating}"
                    + (availability.CurrentBonus > 0
                        ? $" + {availability.CurrentBonus}"
                        : string.Empty)
                    + $"   /   COST {option.PointCost}"
                    + $"   /   CAP +{option.MaximumBonus}";
                if (GUI.Button(
                        new Rect(4f, y, view.width - 8f, OptionHeight),
                        label,
                        buttonStyle))
                {
                    SelectOption(option.Id);
                }
                GUI.enabled = previousEnabled;

                if (!availability.CanAdvance)
                {
                    GUI.Label(
                        new Rect(
                            view.width - 158f,
                            y + OptionHeight - 20f,
                            146f,
                            18f),
                        GetFailureMessage(availability.Failure),
                        statusStyle);
                }
                y += OptionHeight + OptionGap;
            }

            if (profile.AdvancementOptions.Count == 0)
            {
                GUI.Label(
                    new Rect(4f, y, view.width - 8f, 32f),
                    "NO AUTHORED ADVANCEMENTS ARE AVAILABLE.",
                    statusStyle);
                y += 38f;
            }

            if (pendingOptionId != null)
            {
                CharacterAdvancementOption pending = profile.GetAdvancement(
                    pendingOptionId);
                GUI.Label(
                    new Rect(4f, y, view.width - 8f, 24f),
                    $"SPEND {pending.PointCost} POINT ON "
                    + $"{FormatSkill(pending.SkillId)}?",
                    detailStyle);
                if (GUI.Button(
                        new Rect(4f, y + 30f, 128f, 34f),
                        "CONFIRM",
                        buttonStyle))
                {
                    ConfirmPending();
                }
                if (GUI.Button(
                        new Rect(140f, y + 30f, 112f, 34f),
                        "CANCEL",
                        buttonStyle))
                {
                    pendingOptionId = null;
                    status = string.Empty;
                }
                y += FooterHeight;
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                GUI.Label(
                    new Rect(4f, y, view.width - 8f, 24f),
                    status.ToUpperInvariant(),
                    statusStyle);
            }
            GUI.EndScrollView();
        }

        private void HandlePersistenceStatusChanged(string value)
        {
            status = value ?? string.Empty;
        }

        private void HandleControlChanged(GameplayPartyControlSnapshot control)
        {
            if (IsOpen
                && !string.Equals(
                    control.SelectedActorId,
                    actorId,
                    StringComparison.Ordinal))
            {
                progression.GetProgression(control.SelectedActorId);
                actorId = control.SelectedActorId;
                pendingOptionId = null;
                scrollPosition = Vector2.zero;
            }
        }

        private void EnsureStyles()
        {
            if (panelTexture != null)
                return;
            panelTexture = CreateTexture(GameplayVisualPalette.Panel);
            borderTexture = CreateTexture(GameplayVisualPalette.WithAlpha(
                GameplayVisualPalette.SignalBlueGlow,
                0.72f));
            buttonTexture = CreateTexture(GameplayVisualPalette.ButtonNormal);
            buttonHoverTexture = CreateTexture(GameplayVisualPalette.ButtonHover);
            buttonActiveTexture = CreateTexture(GameplayVisualPalette.ButtonActive);
            headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                normal = { textColor = GameplayVisualPalette.SignalBlueGlow },
            };
            identityStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                normal = { textColor = GameplayVisualPalette.TextPrimary },
            };
            detailStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                normal = { textColor = GameplayVisualPalette.TextSecondary },
            };
            statusStyle = new GUIStyle(detailStyle)
            {
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = GameplayVisualPalette.SignalOrangeGlow },
            };
            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(14, 10, 4, 4),
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                normal =
                {
                    background = buttonTexture,
                    textColor = GameplayVisualPalette.TextPrimary,
                },
                hover =
                {
                    background = buttonHoverTexture,
                    textColor = GameplayVisualPalette.TextPrimary,
                },
                active =
                {
                    background = buttonActiveTexture,
                    textColor = GameplayVisualPalette.TextPrimary,
                },
            };
            closeStyle = new GUIStyle(buttonStyle)
            {
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(),
            };
        }

        private static string FormatSkill(string skillId)
        {
            string value = skillId ?? string.Empty;
            int separator = value.LastIndexOf('.');
            if (separator >= 0 && separator + 1 < value.Length)
                value = value.Substring(separator + 1);
            return value.Replace('-', ' ').ToUpperInvariant();
        }

        internal static string GetFailureMessage(
            CharacterAdvancementFailure failure)
        {
            switch (failure)
            {
                case CharacterAdvancementFailure.None:
                    return string.Empty;
                case CharacterAdvancementFailure.UnknownOption:
                    return "UNKNOWN OPTION";
                case CharacterAdvancementFailure.InsufficientPoints:
                    return "NEEDS POINTS";
                case CharacterAdvancementFailure.MaximumReached:
                    return "MAXIMUM";
                case CharacterAdvancementFailure.TurnBasedModeActive:
                    return "EXPLORATION ONLY";
                default:
                    throw new ArgumentOutOfRangeException(nameof(failure));
            }
        }

        private static float CalculateUiScale() => Mathf.Clamp(
            Screen.height / ReferenceHeight,
            0.75f,
            1.35f);

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
            Unbind();
            GameplayObjectLifecycle.Destroy(panelTexture);
            GameplayObjectLifecycle.Destroy(borderTexture);
            GameplayObjectLifecycle.Destroy(buttonTexture);
            GameplayObjectLifecycle.Destroy(buttonHoverTexture);
            GameplayObjectLifecycle.Destroy(buttonActiveTexture);
        }
    }
}
