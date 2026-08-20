using System;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    [DisallowMultipleComponent]
    internal sealed class GameplayReplayCameraCutPresenter : MonoBehaviour
    {
        private const float FadeSeconds = 0.18f;
        private GameplayCameraRig cameraRig;
        private GameplayWorldRegistry world;
        private string focusedActorId = string.Empty;
        private string actorLabel = string.Empty;
        private float remaining;
        private GUIStyle labelStyle;

        public void Begin(
            GameplayCameraRig rig,
            GameplayWorldRegistry registry)
        {
            End();
            cameraRig = rig ?? throw new ArgumentNullException(nameof(rig));
            world = registry ?? throw new ArgumentNullException(
                nameof(registry));
            cameraRig.BeginReplayPresentation();
            enabled = true;
        }

        public void Focus(string actorId, string displayName)
        {
            if (cameraRig == null || world == null)
                throw new InvalidOperationException(
                    "Bind replay camera cuts before focusing an actor.");
            if (string.Equals(
                    focusedActorId,
                    actorId,
                    StringComparison.Ordinal))
                return;
            Transform target;
            if (world.TryGetActor(actorId, out GameplayActorView actor))
            {
                target = actor.Transform;
            }
            else if (world.TryGetLevelEntity(actorId, out var entity))
            {
                target = entity.transform;
            }
            else
            {
                throw new InvalidOperationException(
                    $"Replay camera focus '{actorId}' is not registered.");
            }
            cameraRig.FocusReplayTarget(target);
            focusedActorId = actorId;
            actorLabel = string.IsNullOrWhiteSpace(displayName)
                ? actorId
                : displayName;
            remaining = FadeSeconds;
        }

        public void End()
        {
            cameraRig?.EndReplayPresentation();
            cameraRig = null;
            world = null;
            focusedActorId = string.Empty;
            actorLabel = string.Empty;
            remaining = 0f;
            enabled = false;
        }

        private void Update()
        {
            if (remaining > 0f)
                remaining = Mathf.Max(0f, remaining - Time.unscaledDeltaTime);
        }

        private void OnGUI()
        {
            if (remaining <= 0f) return;
            float alpha = Mathf.Clamp01(remaining / FadeSeconds);
            Color prior = GUI.color;
            GUI.color = new Color(0.01f, 0.015f, 0.025f, alpha);
            GUI.DrawTexture(
                new Rect(0f, 0f, Screen.width, Screen.height),
                Texture2D.whiteTexture);
            GUI.color = new Color(1f, 1f, 1f, alpha);
            labelStyle ??= new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 18,
                fontStyle = FontStyle.Bold,
            };
            GUI.Label(
                new Rect(0f, (Screen.height * 0.5f) - 24f, Screen.width, 48f),
                actorLabel.ToUpperInvariant(),
                labelStyle);
            GUI.color = prior;
        }

        private void OnDisable()
        {
            if (cameraRig != null) End();
        }
    }
}
