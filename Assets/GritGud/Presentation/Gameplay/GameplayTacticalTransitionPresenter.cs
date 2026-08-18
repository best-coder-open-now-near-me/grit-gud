using System;
using GritGud.Application.Gameplay;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    [DisallowMultipleComponent]
    internal sealed class GameplayTacticalTransitionPresenter : MonoBehaviour
    {
        private GameplaySession session;
        private GameplayInputController input;
        private GameplayHud hud;
        private GameplayPartyHud partyHud;
        private TacticalTransitionPresentationDefinition definition;
        private GameplaySessionMode observedMode;
        private float remainingSeconds;
        private Texture2D whiteTexture;
        private bool combatEntryActive;
        private string combatEntryMessage = string.Empty;
        private bool hudWasEnabled;
        private bool partyHudWasEnabled;

        public void Bind(
            GameplaySession gameplaySession,
            GameplayVisualTheme theme,
            GameplayInputController gameplayInput,
            GameplayHud gameplayHud,
            GameplayPartyHud gameplayPartyHud)
        {
            if (gameplaySession == null)
            {
                throw new ArgumentNullException(nameof(gameplaySession));
            }
            if (theme == null)
            {
                throw new ArgumentNullException(nameof(theme));
            }

            Unbind();
            session = gameplaySession;
            input = gameplayInput ?? throw new ArgumentNullException(
                nameof(gameplayInput));
            hud = gameplayHud ?? throw new ArgumentNullException(
                nameof(gameplayHud));
            partyHud = gameplayPartyHud ?? throw new ArgumentNullException(
                nameof(gameplayPartyHud));
            definition = theme.TacticalTransition;
            observedMode = session.Mode;
            whiteTexture = Texture2D.whiteTexture;
            enabled = true;
        }

        public void Unbind()
        {
            input?.SetSuppressed(false);
            RestoreInterfaceState();
            session = null;
            input = null;
            hud = null;
            partyHud = null;
            definition = null;
            remainingSeconds = 0f;
            whiteTexture = null;
            combatEntryActive = false;
            combatEntryMessage = string.Empty;
            enabled = false;
        }

        public bool CombatEntryReady =>
            combatEntryActive && remainingSeconds <= 0f;

        public void BeginCombatEntry(string observerId, string detectedActorId)
        {
            if (session == null || definition == null || input == null)
                throw new InvalidOperationException(
                    "Bind the tactical transition presenter before use.");
            combatEntryActive = true;
            remainingSeconds = definition.CombatEntryDelaySeconds;
            combatEntryMessage = "CONTACT\n" + detectedActorId.ToUpperInvariant()
                + " DETECTED";
            input.SetSuppressed(true);
            hudWasEnabled = hud.enabled;
            partyHudWasEnabled = partyHud.enabled;
            hud.enabled = false;
            partyHud.enabled = false;
        }

        public void CompleteCombatEntry()
        {
            RestoreInterfaceState();
            combatEntryActive = false;
            remainingSeconds = 0f;
            combatEntryMessage = string.Empty;
            input?.SetSuppressed(false);
        }

        private void RestoreInterfaceState()
        {
            if (!combatEntryActive)
                return;
            if (hud != null)
                hud.enabled = hudWasEnabled;
            if (partyHud != null)
                partyHud.enabled = partyHudWasEnabled;
        }

        private void Update()
        {
            if (session == null || definition == null)
            {
                return;
            }

            if (!combatEntryActive && session.Mode != observedMode)
            {
                observedMode = session.Mode;
                remainingSeconds = definition.DurationSeconds;
            }

            remainingSeconds = Mathf.Max(
                0f,
                remainingSeconds - Time.unscaledDeltaTime);
        }

        private void OnGUI()
        {
            if (remainingSeconds <= 0f || definition == null || whiteTexture == null)
            {
                return;
            }

            float duration = combatEntryActive
                ? definition.CombatEntryDelaySeconds
                : definition.DurationSeconds;
            float progress = 1f - (remainingSeconds / duration);
            float eased = 1f - Mathf.Pow(1f - Mathf.Clamp01(progress), 3f);
            float fade = Mathf.Sin(Mathf.Clamp01(progress) * Mathf.PI);
            Color signal = observedMode == GameplaySessionMode.TurnBased
                ? definition.TurnModeColor
                : definition.ExplorationColor;

            DrawRect(
                new Rect(0f, 0f, Screen.width, Screen.height),
                new Color(
                    signal.r,
                    signal.g,
                    signal.b,
                    definition.WashOpacity * fade));

            float scanX = Mathf.Lerp(
                -definition.ScanLineWidth,
                Screen.width,
                eased);
            Color lineColor = new Color(
                signal.r,
                signal.g,
                signal.b,
                0.74f * fade);
            DrawRect(
                new Rect(
                    scanX,
                    0f,
                    definition.ScanLineWidth,
                    definition.EdgeBandHeight),
                lineColor);
            DrawRect(
                new Rect(
                    Screen.width - scanX - definition.ScanLineWidth,
                    Screen.height - definition.EdgeBandHeight,
                    definition.ScanLineWidth,
                    definition.EdgeBandHeight),
                lineColor);

            Color edgeColor = new Color(
                signal.r,
                signal.g,
                signal.b,
                0.16f * fade);
            DrawRect(
                new Rect(0f, 0f, Screen.width * eased, 1f),
                edgeColor);
            DrawRect(
                new Rect(
                    Screen.width * (1f - eased),
                    Screen.height - 1f,
                    Screen.width * eased,
                    1f),
                edgeColor);

            if (combatEntryActive)
            {
                var banner = new Rect(
                    Screen.width * 0.5f - 180f,
                    Screen.height * 0.22f,
                    360f,
                    74f);
                DrawRect(banner, new Color(0.015f, 0.025f, 0.04f, 0.94f));
                DrawRect(new Rect(banner.x, banner.y, banner.width, 3f), signal);
                var style = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold,
                    fontSize = 18,
                };
                style.normal.textColor = Color.white;
                GUI.Label(banner, combatEntryMessage, style);
            }
        }

        private void DrawRect(Rect rectangle, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rectangle, whiteTexture);
            GUI.color = previous;
        }

        private void OnDestroy() => Unbind();
    }
}
