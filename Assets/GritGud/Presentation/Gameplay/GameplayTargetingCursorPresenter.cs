using System;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public sealed class GameplayTargetingCursorPresenter : MonoBehaviour
    {
        private const int CursorSize = 32;
        private static readonly Vector2 CursorHotspot =
            new Vector2(CursorSize * 0.5f, CursorSize * 0.5f);
        private Func<bool> shouldShowTargetingCursor;
        private Texture2D targetingCursor;
        private bool targetingVisible;

        internal void Bind(Func<bool> targetingCursorRequested)
        {
            Unbind();
            shouldShowTargetingCursor = targetingCursorRequested
                ?? throw new ArgumentNullException(
                    nameof(targetingCursorRequested));
            enabled = true;
            RefreshNow();
        }

        internal void Unbind()
        {
            shouldShowTargetingCursor = null;
            SetTargetingVisible(false);
            enabled = false;
        }

        internal void RefreshNow()
        {
            SetTargetingVisible(
                shouldShowTargetingCursor?.Invoke() ?? false);
        }

        private void LateUpdate()
        {
            RefreshNow();
        }

        private void SetTargetingVisible(bool visible)
        {
            if (targetingVisible == visible)
                return;
            targetingVisible = visible;
            Cursor.SetCursor(
                visible ? EnsureTargetingCursor() : null,
                visible ? CursorHotspot : Vector2.zero,
                CursorMode.Auto);
        }

        private Texture2D EnsureTargetingCursor()
        {
            if (targetingCursor != null)
                return targetingCursor;

            targetingCursor = new Texture2D(
                CursorSize,
                CursorSize,
                TextureFormat.RGBA32,
                mipChain: false)
            {
                name = "Gameplay Targeting Cursor",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            var pixels = new Color32[CursorSize * CursorSize];
            Color32 outline = GameplayVisualPalette.OutlineDark;
            Color32 signal = GameplayVisualPalette.SignalBlueBright;
            DrawHorizontal(pixels, 4, 12, 15, outline);
            DrawHorizontal(pixels, 19, 27, 15, outline);
            DrawVertical(pixels, 4, 12, 15, outline);
            DrawVertical(pixels, 19, 27, 15, outline);
            DrawHorizontal(pixels, 5, 11, 16, signal);
            DrawHorizontal(pixels, 20, 26, 16, signal);
            DrawVertical(pixels, 5, 11, 16, signal);
            DrawVertical(pixels, 20, 26, 16, signal);
            SetPixel(pixels, 16, 16, signal);
            targetingCursor.SetPixels32(pixels);
            targetingCursor.Apply(
                updateMipmaps: false,
                makeNoLongerReadable: false);
            return targetingCursor;
        }

        private static void DrawHorizontal(
            Color32[] pixels,
            int start,
            int end,
            int y,
            Color32 color)
        {
            for (int x = start; x <= end; x++)
                SetPixel(pixels, x, y, color);
        }

        private static void DrawVertical(
            Color32[] pixels,
            int start,
            int end,
            int x,
            Color32 color)
        {
            for (int y = start; y <= end; y++)
                SetPixel(pixels, x, y, color);
        }

        private static void SetPixel(
            Color32[] pixels,
            int x,
            int y,
            Color32 color)
        {
            pixels[(y * CursorSize) + x] = color;
        }

        private void OnDestroy()
        {
            Unbind();
            GameplayObjectLifecycle.Destroy(targetingCursor);
            targetingCursor = null;
        }
    }
}
