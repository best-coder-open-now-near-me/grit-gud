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
        private Func<bool?> getTargetingValidity;
        private Texture2D validTargetingCursor;
        private Texture2D invalidTargetingCursor;
        private bool targetingVisible;
        private bool targetingValid = true;

        internal bool IsTargetingVisible => targetingVisible;

        internal bool IsTargetingValid => targetingValid;

        internal void Bind(
            Func<bool> targetingCursorRequested,
            Func<bool?> targetingValidity = null)
        {
            Unbind();
            shouldShowTargetingCursor = targetingCursorRequested
                ?? throw new ArgumentNullException(
                    nameof(targetingCursorRequested));
            getTargetingValidity = targetingValidity;
            enabled = true;
            RefreshNow();
        }

        internal void Unbind()
        {
            shouldShowTargetingCursor = null;
            getTargetingValidity = null;
            SetTargetingVisible(visible: false, valid: true);
            enabled = false;
        }

        internal void RefreshNow()
        {
            bool visible = shouldShowTargetingCursor?.Invoke() ?? false;
            bool valid = getTargetingValidity?.Invoke() != false;
            SetTargetingVisible(visible, valid);
        }

        private void LateUpdate()
        {
            RefreshNow();
        }

        private void SetTargetingVisible(bool visible, bool valid)
        {
            if (targetingVisible == visible
                && (!visible || targetingValid == valid))
                return;
            targetingVisible = visible;
            targetingValid = valid;
            Cursor.SetCursor(
                visible ? EnsureTargetingCursor(valid) : null,
                visible ? CursorHotspot : Vector2.zero,
                CursorMode.Auto);
        }

        private Texture2D EnsureTargetingCursor(bool valid)
        {
            Texture2D existing = valid
                ? validTargetingCursor
                : invalidTargetingCursor;
            if (existing != null)
                return existing;

            var cursor = new Texture2D(
                CursorSize,
                CursorSize,
                TextureFormat.RGBA32,
                mipChain: false)
            {
                name = valid
                    ? "Gameplay Valid Targeting Cursor"
                    : "Gameplay Invalid Targeting Cursor",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            var pixels = new Color32[CursorSize * CursorSize];
            Color32 outline = GameplayVisualPalette.OutlineDark;
            Color32 signal = valid
                ? GameplayVisualPalette.TargetingValid
                : GameplayVisualPalette.TargetingInvalid;
            DrawHorizontal(pixels, 4, 12, 15, outline);
            DrawHorizontal(pixels, 19, 27, 15, outline);
            DrawVertical(pixels, 4, 12, 15, outline);
            DrawVertical(pixels, 19, 27, 15, outline);
            DrawHorizontal(pixels, 5, 11, 16, signal);
            DrawHorizontal(pixels, 20, 26, 16, signal);
            DrawVertical(pixels, 5, 11, 16, signal);
            DrawVertical(pixels, 20, 26, 16, signal);
            SetPixel(pixels, 16, 16, signal);
            cursor.SetPixels32(pixels);
            cursor.Apply(
                updateMipmaps: false,
                makeNoLongerReadable: false);
            if (valid)
            {
                validTargetingCursor = cursor;
            }
            else
            {
                invalidTargetingCursor = cursor;
            }
            return cursor;
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
            GameplayObjectLifecycle.Destroy(validTargetingCursor);
            GameplayObjectLifecycle.Destroy(invalidTargetingCursor);
            validTargetingCursor = null;
            invalidTargetingCursor = null;
        }
    }
}
