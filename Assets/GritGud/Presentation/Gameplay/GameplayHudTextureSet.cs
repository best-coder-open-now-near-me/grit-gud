using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GritGud.Presentation.Gameplay
{
    internal sealed class GameplayHudTextureSet : IDisposable
    {
        public GameplayHudTextureSet()
        {
            ButtonNormal = Create(GameplayVisualPalette.ButtonNormal);
            ButtonHover = Create(GameplayVisualPalette.ButtonHover);
            ButtonActive = Create(GameplayVisualPalette.ButtonActive);
            EquipmentConfirmation = Create(GameplayVisualPalette.WithAlpha(
                GameplayVisualPalette.SignalOrange,
                0.42f));
            BodyRegionCircleMask = CreateCircleMask(64);
        }

        public Texture2D White => Texture2D.whiteTexture;

        public Texture2D ButtonNormal { get; private set; }

        public Texture2D ButtonHover { get; private set; }

        public Texture2D ButtonActive { get; private set; }

        public Texture2D EquipmentConfirmation { get; private set; }

        public Texture2D BodyRegionCircleMask { get; private set; }

        public void Dispose()
        {
            Destroy(ButtonNormal);
            Destroy(ButtonHover);
            Destroy(ButtonActive);
            Destroy(EquipmentConfirmation);
            Destroy(BodyRegionCircleMask);
            ButtonNormal = null;
            ButtonHover = null;
            ButtonActive = null;
            EquipmentConfirmation = null;
            BodyRegionCircleMask = null;
        }

        private static Texture2D Create(Color color)
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
            };
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        private static Texture2D CreateCircleMask(int size)
        {
            var texture = new Texture2D(
                size,
                size,
                TextureFormat.RGBA32,
                false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            var pixels = new Color[size * size];
            float center = (size - 1f) * 0.5f;
            float radius = center - 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Mathf.Sqrt(
                        ((x - center) * (x - center))
                        + ((y - center) * (y - center)));
                    float alpha = Mathf.Clamp01(radius + 1f - distance);
                    pixels[(y * size) + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(updateMipmaps: false, makeNoLongerReadable: true);
            return texture;
        }

        private static void Destroy(Object target)
        {
            if (target == null)
            {
                return;
            }

            if (UnityEngine.Application.isPlaying)
            {
                Object.Destroy(target);
            }
            else
            {
                Object.DestroyImmediate(target);
            }
        }
    }
}
