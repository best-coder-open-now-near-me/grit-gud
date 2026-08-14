using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace GritGud.Presentation.Gameplay
{
    internal sealed class GameplayPostProcessing : IDisposable
    {
        private GameObject root;
        private VolumeProfile profile;

        private GameplayPostProcessing(GameObject volumeRoot, VolumeProfile volumeProfile)
        {
            root = volumeRoot;
            profile = volumeProfile;
        }

        public static GameplayPostProcessing Create(
            Transform parent,
            GameplayVisualTheme theme)
        {
            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }
            if (theme == null)
            {
                throw new ArgumentNullException(nameof(theme));
            }

            var volumeRoot = new GameObject("Gameplay Post Processing");
            volumeRoot.transform.SetParent(parent, false);

            VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "Gameplay Tactical Grade";
            profile.hideFlags = HideFlags.HideAndDontSave;

            Volume volume = volumeRoot.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 100f;
            volume.weight = 1f;
            volume.sharedProfile = profile;

            ConfigureProfile(profile, theme.PostProcessing);
            return new GameplayPostProcessing(volumeRoot, profile);
        }

        public void Dispose()
        {
            GameplayObjectLifecycle.Destroy(root);
            root = null;
            GameplayObjectLifecycle.Destroy(profile);
            profile = null;
        }

        private static void ConfigureProfile(
            VolumeProfile profile,
            PostProcessingPresentationDefinition definition)
        {
            Tonemapping tonemapping = profile.Add<Tonemapping>(true);
            tonemapping.mode.Override(TonemappingMode.Neutral);

            ColorAdjustments color = profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(definition.PostExposure);
            color.contrast.Override(definition.Contrast);
            color.colorFilter.Override(definition.ColorFilter);
            color.saturation.Override(definition.Saturation);

            Bloom bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(definition.BloomThreshold);
            bloom.intensity.Override(definition.BloomIntensity);
            bloom.scatter.Override(definition.BloomScatter);
            bloom.tint.Override(definition.BloomTint);
            bloom.highQualityFiltering.Override(true);
            bloom.maxIterations.Override(definition.BloomIterations);

            Vignette vignette = profile.Add<Vignette>(true);
            vignette.color.Override(definition.VignetteColor);
            vignette.intensity.Override(definition.VignetteIntensity);
            vignette.smoothness.Override(definition.VignetteSmoothness);
        }
    }
}
