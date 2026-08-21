using System;
using GritGud.Application.Gameplay;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    [DisallowMultipleComponent]
    internal sealed class GameplayBattleReplayController : MonoBehaviour
    {
        internal const string ArtifactResource =
            "SimulationArtifacts/depot-first-sim";

        private GameplayTurnReplayHud hud;
        private GameplayInputController input;
        private GameplayHud gameplayHud;
        private GameplayPartyHud partyHud;
        private bool gameplayHudWasVisible;
        private bool partyHudWasSuppressed;
        private string status = string.Empty;
        private GUIStyle statusStyle;

        public void Bind(
            GameplayBattleReplayPreparationResult<
                GameplayBattleArtifact,
                GameplaySemanticReplayTimeline> prepared,
            GameplayTurnReplayHud replayHud,
            GameplayInputController inputController,
            GameplayHud liveGameplayHud,
            GameplayPartyHud livePartyHud)
        {
            Unbind();
            if (prepared == null) throw new ArgumentNullException(
                nameof(prepared));
            if (!prepared.IsReady)
                throw new ArgumentException(
                    "Simulation viewer presentation requires a prepared replay.",
                    nameof(prepared));
            hud = replayHud ?? throw new ArgumentNullException(nameof(replayHud));
            input = inputController ?? throw new ArgumentNullException(
                nameof(inputController));
            gameplayHud = liveGameplayHud ?? throw new ArgumentNullException(
                nameof(liveGameplayHud));
            partyHud = livePartyHud ?? throw new ArgumentNullException(
                nameof(livePartyHud));
            gameplayHudWasVisible = gameplayHud.IsVisible;
            partyHudWasSuppressed = partyHud.IsPresentationSuppressed;
            gameplayHud.Hide();
            partyHud.SetPresentationSuppressed(true);
            input.SetCameraOnly(true);
            hud.OpenChanged += HandleReplayOpenChanged;
            enabled = true;
            hud.SetVerifiedExternalReplay(prepared.Replay, prepared.Artifact);
            GameplayBattleScoreboard score = prepared.Artifact.Content
                .Scoreboard;
            status = "FIRST SIM READY — "
                + prepared.Artifact.Content.Terminal.Kind.ToString()
                    .ToUpperInvariant()
                + " · " + score.TurnsCompleted + " TURNS — CLICK WATCH";
            hud.OpenVerifiedExternalReplay();
        }

        public void Unbind()
        {
            if (hud != null)
                hud.OpenChanged -= HandleReplayOpenChanged;
            input?.SetCameraOnly(false);
            if (gameplayHudWasVisible)
                gameplayHud?.Show();
            partyHud?.SetPresentationSuppressed(partyHudWasSuppressed);
            hud = null;
            input = null;
            gameplayHud = null;
            partyHud = null;
            gameplayHudWasVisible = false;
            partyHudWasSuppressed = false;
            status = string.Empty;
            enabled = false;
        }

        private void OnGUI()
        {
            if (string.IsNullOrWhiteSpace(status) || hud?.IsOpen == true)
                return;
            statusStyle ??= new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
                fontStyle = FontStyle.Bold,
            };
            float width = Mathf.Min(420f, Screen.width - 28f);
            var rectangle = new Rect(
                (Screen.width - width) * 0.5f,
                14f,
                width,
                28f);
            if (hud?.IsAvailable == true)
            {
                if (GUI.Button(rectangle, status, statusStyle))
                    hud.OpenVerifiedExternalReplay();
            }
            else
            {
                GUI.Box(rectangle, status, statusStyle);
            }
        }

        private void HandleReplayOpenChanged(bool _)
        {
            // The viewer remains non-interactive even after its replay bar is
            // closed. Returning to gameplay requires leaving viewer mode.
            input?.SetCameraOnly(true);
        }

        private void OnDestroy() => Unbind();
    }
}
