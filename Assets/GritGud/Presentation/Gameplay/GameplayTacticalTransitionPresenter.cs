using System;
using GritGud.Application.Gameplay;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    [DisallowMultipleComponent]
    internal sealed class GameplayTacticalTransitionPresenter : MonoBehaviour
    {
        private GameplaySession session;
        private TacticalTransitionPresentationDefinition definition;
        private GameplaySessionMode observedMode;
        private float remainingSeconds;
        private Texture2D whiteTexture;

        public void Bind(GameplaySession gameplaySession, GameplayVisualTheme theme)
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
            definition = theme.TacticalTransition;
            observedMode = session.Mode;
            whiteTexture = Texture2D.whiteTexture;
            enabled = true;
        }

        public void Unbind()
        {
            session = null;
            definition = null;
            remainingSeconds = 0f;
            whiteTexture = null;
            enabled = false;
        }

        private void Update()
        {
            if (session == null || definition == null)
            {
                return;
            }

            if (session.Mode != observedMode)
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

            float progress = 1f - (remainingSeconds / definition.DurationSeconds);
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
