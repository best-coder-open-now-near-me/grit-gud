using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GritGud.Presentation.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class TargetChancePresenter : MonoBehaviour
    {
        private const float ReferenceHeight = 900f;
        private TargetAcquisitionPresenter acquisition;
        private GUIStyle chanceStyle;
        private Texture2D chanceBackground;

        public bool IsBound => acquisition != null;

        internal void Bind(TargetAcquisitionPresenter targetAcquisition)
        {
            acquisition = targetAcquisition ??
                throw new ArgumentNullException(nameof(targetAcquisition));
            enabled = true;
        }

        internal void Unbind()
        {
            acquisition = null;
            enabled = false;
        }

        private void OnGUI()
        {
            if (acquisition == null
                || !acquisition.TryGetPointerFeedback(
                    out TargetingPointerFeedback feedback))
            {
                return;
            }

            EnsureGuiResources();
            float uiScale = Mathf.Clamp(
                Screen.height / ReferenceHeight,
                0.75f,
                1.35f);
            float canvasWidth = Screen.width / uiScale;
            float canvasHeight = Screen.height / uiScale;
            Matrix4x4 previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(uiScale, uiScale, 1f));
            const float height = 27f;
            float width = Mathf.Clamp(
                chanceStyle.CalcSize(new GUIContent(feedback.Text)).x + 24f,
                184f,
                360f);
            Vector2 screenPointer = Mouse.current == null
                ? new Vector2(Screen.width * 0.5f, Screen.height * 0.5f)
                : Mouse.current.position.ReadValue();
            var pointer = new Vector2(
                screenPointer.x / uiScale,
                (Screen.height - screenPointer.y) / uiScale);
            var rectangle = new Rect(
                Mathf.Clamp(pointer.x + 16f, 8f, canvasWidth - width - 8f),
                Mathf.Clamp(pointer.y + 18f, 8f, canvasHeight - height - 8f),
                width,
                height);
            GUI.DrawTexture(
                rectangle,
                chanceBackground,
                ScaleMode.StretchToFill,
                true);
            chanceStyle.normal.textColor = feedback.IsValid
                ? GameplayVisualPalette.TargetingValid
                : GameplayVisualPalette.TargetingInvalid;
            GUI.Label(rectangle, feedback.Text, chanceStyle);
            GUI.matrix = previousMatrix;
        }

        private void EnsureGuiResources()
        {
            if (chanceBackground == null)
            {
                chanceBackground = new Texture2D(1, 1)
                {
                    name = "Target Chance Background",
                    hideFlags = HideFlags.HideAndDontSave,
                };
                chanceBackground.SetPixel(
                    0,
                    0,
                    GameplayVisualPalette.WithAlpha(
                        GameplayVisualPalette.Panel,
                        0.68f));
                chanceBackground.Apply();
            }

            if (chanceStyle == null)
            {
                chanceStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 11,
                    fontStyle = FontStyle.Bold,
                    normal =
                    {
                        textColor = GameplayVisualPalette.SignalBlueBright,
                    },
                };
            }
        }

        private void OnDestroy()
        {
            Unbind();
            GameplayObjectLifecycle.Destroy(chanceBackground);
            chanceBackground = null;
        }
    }
}
