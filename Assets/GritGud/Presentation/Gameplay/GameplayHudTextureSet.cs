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
        }

        public Texture2D White => Texture2D.whiteTexture;

        public Texture2D ButtonNormal { get; private set; }

        public Texture2D ButtonHover { get; private set; }

        public Texture2D ButtonActive { get; private set; }

        public Texture2D EquipmentConfirmation { get; private set; }

        public void Dispose()
        {
            Destroy(ButtonNormal);
            Destroy(ButtonHover);
            Destroy(ButtonActive);
            Destroy(EquipmentConfirmation);
            ButtonNormal = null;
            ButtonHover = null;
            ButtonActive = null;
            EquipmentConfirmation = null;
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
