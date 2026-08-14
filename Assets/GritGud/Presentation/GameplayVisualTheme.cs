using System;
using UnityEngine;

namespace GritGud.Presentation
{
    [Serializable]
    public sealed class PostProcessingPresentationDefinition
    {
        [SerializeField] private float postExposure = 0.08f;
        [SerializeField] private float contrast = 7f;
        [SerializeField] private Color colorFilter = Color.white;
        [SerializeField] private float saturation = -4f;
        [SerializeField, Min(0f)] private float bloomThreshold = 0.9f;
        [SerializeField, Min(0f)] private float bloomIntensity = 0.28f;
        [SerializeField, Range(0f, 1f)] private float bloomScatter = 0.55f;
        [SerializeField] private Color bloomTint = Color.white;
        [SerializeField, Range(1, 8)] private int bloomIterations = 5;
        [SerializeField] private Color vignetteColor = Color.black;
        [SerializeField, Range(0f, 1f)] private float vignetteIntensity = 0.1f;
        [SerializeField, Range(0.01f, 1f)] private float vignetteSmoothness = 0.72f;

        public float PostExposure => postExposure;
        public float Contrast => contrast;
        public Color ColorFilter => colorFilter;
        public float Saturation => saturation;
        public float BloomThreshold => Mathf.Max(0f, bloomThreshold);
        public float BloomIntensity => Mathf.Max(0f, bloomIntensity);
        public float BloomScatter => Mathf.Clamp01(bloomScatter);
        public Color BloomTint => bloomTint;
        public int BloomIterations => Mathf.Clamp(bloomIterations, 1, 8);
        public Color VignetteColor => vignetteColor;
        public float VignetteIntensity => Mathf.Clamp01(vignetteIntensity);
        public float VignetteSmoothness => Mathf.Clamp(vignetteSmoothness, 0.01f, 1f);
    }

    [Serializable]
    public sealed class CelSurfacePresentationDefinition
    {
        [SerializeField, Range(0f, 1f)] private float threshold = 0.48f;
        [SerializeField, Range(0.001f, 0.25f)] private float softness = 0.035f;
        [SerializeField] private Color shadowColor = new Color(0.34f, 0.42f, 0.55f, 1f);
        [SerializeField, Range(0f, 1f)] private float shadowStrength = 0.44f;
        [SerializeField, Range(0f, 2f)] private float ambientStrength = 0.9f;

        public float Threshold => Mathf.Clamp01(threshold);
        public float Softness => Mathf.Clamp(softness, 0.001f, 0.25f);
        public Color ShadowColor => shadowColor;
        public float ShadowStrength => Mathf.Clamp01(shadowStrength);
        public float AmbientStrength => Mathf.Clamp(ambientStrength, 0f, 2f);
    }

    [Serializable]
    public sealed class ActorSurfacePresentationDefinition
    {
        [SerializeField, Range(0f, 2f)] private float ambientStrength = 1.25f;
        [SerializeField, Range(0f, 1f)] private float shadowStrength = 0.58f;
        [SerializeField] private Color shadowColor = new Color(0.3f, 0.42f, 0.58f, 1f);
        [SerializeField, Range(0f, 0.5f)] private float silhouetteWidth = 0.085f;
        [SerializeField, Range(0.001f, 0.3f)] private float silhouetteSoftness = 0.06f;
        [SerializeField, Range(0f, 1f)] private float smoothness = 0.28f;
        [SerializeField, Range(0f, 1f)] private float specularStrength = 0.12f;
        [SerializeField, Range(0f, 1f)] private float edgeSheenStrength = 0.035f;

        public float AmbientStrength => Mathf.Clamp(ambientStrength, 0f, 2f);
        public float ShadowStrength => Mathf.Clamp01(shadowStrength);
        public Color ShadowColor => shadowColor;
        public float SilhouetteWidth => Mathf.Clamp(silhouetteWidth, 0f, 0.5f);
        public float SilhouetteSoftness => Mathf.Clamp(silhouetteSoftness, 0.001f, 0.3f);
        public float Smoothness => Mathf.Clamp01(smoothness);
        public float SpecularStrength => Mathf.Clamp01(specularStrength);
        public float EdgeSheenStrength => Mathf.Clamp01(edgeSheenStrength);
    }

    [Serializable]
    public sealed class OutlinePresentationDefinition
    {
        [SerializeField] private Color color = new Color(0.008f, 0.015f, 0.03f, 1f);
        [SerializeField, Min(0f)] private float environmentWidth = 0.028f;
        [SerializeField, Min(0f)] private float actorWidth = 0.012f;

        public Color Color => color;
        public float EnvironmentWidth => Mathf.Max(0f, environmentWidth);
        public float ActorWidth => Mathf.Max(0f, actorWidth);
    }

    [Serializable]
    public sealed class CharacterGroundingPresentationDefinition
    {
        [SerializeField] private bool enabled = true;
        [SerializeField, Min(0.05f)] private float diameter = 0.82f;
        [SerializeField, Min(0f)] private float heightOffset = 0.018f;
        [SerializeField, Range(0f, 1f)] private float opacity = 0.34f;
        [SerializeField, Range(0.01f, 1f)] private float edgeSoftness = 0.62f;
        [SerializeField] private Color color = new Color(0f, 0.008f, 0.022f, 1f);

        public bool Enabled => enabled;
        public float Diameter => Mathf.Max(0.05f, diameter);
        public float HeightOffset => Mathf.Max(0f, heightOffset);
        public float Opacity => Mathf.Clamp01(opacity);
        public float EdgeSoftness => Mathf.Clamp(edgeSoftness, 0.01f, 1f);
        public Color Color => color;
    }

    [Serializable]
    public sealed class TacticalTransitionPresentationDefinition
    {
        [SerializeField, Min(0.05f)] private float durationSeconds = 0.46f;
        [SerializeField, Min(1f)] private float edgeBandHeight = 3f;
        [SerializeField, Min(1f)] private float scanLineWidth = 220f;
        [SerializeField, Range(0f, 1f)] private float washOpacity = 0.075f;
        [SerializeField] private Color turnModeColor = new Color(0.02f, 1.45f, 2.4f, 1f);
        [SerializeField] private Color explorationColor = new Color(0.55f, 0.9f, 1.35f, 1f);

        public float DurationSeconds => Mathf.Max(0.05f, durationSeconds);
        public float EdgeBandHeight => Mathf.Max(1f, edgeBandHeight);
        public float ScanLineWidth => Mathf.Max(1f, scanLineWidth);
        public float WashOpacity => Mathf.Clamp01(washOpacity);
        public Color TurnModeColor => turnModeColor;
        public Color ExplorationColor => explorationColor;
    }

    [CreateAssetMenu(
        fileName = "GameplayVisualTheme",
        menuName = "Grit Gud/Gameplay Visual Theme")]
    public sealed class GameplayVisualTheme : ScriptableObject
    {
        internal const string DefaultResourceName = "Gameplay/GameplayVisualTheme";

        [SerializeField] private PostProcessingPresentationDefinition postProcessing =
            new PostProcessingPresentationDefinition();
        [SerializeField] private CelSurfacePresentationDefinition celSurface =
            new CelSurfacePresentationDefinition();
        [SerializeField] private ActorSurfacePresentationDefinition actorSurface =
            new ActorSurfacePresentationDefinition();
        [SerializeField] private OutlinePresentationDefinition outlines =
            new OutlinePresentationDefinition();
        [SerializeField] private CharacterGroundingPresentationDefinition grounding =
            new CharacterGroundingPresentationDefinition();
        [SerializeField] private TacticalTransitionPresentationDefinition tacticalTransition =
            new TacticalTransitionPresentationDefinition();

        public PostProcessingPresentationDefinition PostProcessing => postProcessing;
        public CelSurfacePresentationDefinition CelSurface => celSurface;
        public ActorSurfacePresentationDefinition ActorSurface => actorSurface;
        public OutlinePresentationDefinition Outlines => outlines;
        public CharacterGroundingPresentationDefinition Grounding => grounding;
        public TacticalTransitionPresentationDefinition TacticalTransition => tacticalTransition;

        public static GameplayVisualTheme LoadDefault()
        {
            GameplayVisualTheme theme = Resources.Load<GameplayVisualTheme>(
                DefaultResourceName);
            if (theme == null)
            {
                throw new InvalidOperationException(
                    $"The Resources visual theme '{DefaultResourceName}' could not be loaded.");
            }

            return theme;
        }
    }
}
