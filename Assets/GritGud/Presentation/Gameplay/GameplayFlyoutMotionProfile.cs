using System;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    [Serializable]
    internal sealed class GameplayFlyoutMotionData
    {
        public float revealSeconds;
        public string easing = "smoothStep";
        public float laserOuterWidth;
        public float laserInnerWidth;
        public float laserCoreWidth;
        public float laserOuterAlpha;
        public float laserInnerAlpha;
    }

    internal sealed class GameplayFlyoutMotionProfile
    {
        internal const string ResourcePath = "Gameplay/flyout-motion";

        private readonly GameplayFlyoutMotionData data;

        private GameplayFlyoutMotionProfile(GameplayFlyoutMotionData authoredData)
        {
            data = authoredData ?? throw new ArgumentNullException(
                nameof(authoredData));
            Validate(data);
        }

        public float RevealSeconds => data.revealSeconds;

        public float LaserOuterWidth => data.laserOuterWidth;

        public float LaserInnerWidth => data.laserInnerWidth;

        public float LaserCoreWidth => data.laserCoreWidth;

        public float LaserOuterAlpha => data.laserOuterAlpha;

        public float LaserInnerAlpha => data.laserInnerAlpha;

        public static GameplayFlyoutMotionProfile LoadDefault()
        {
            TextAsset source = Resources.Load<TextAsset>(ResourcePath);
            if (source == null)
            {
                throw new InvalidOperationException(
                    $"Flyout motion profile '{ResourcePath}' was not found.");
            }

            GameplayFlyoutMotionData authored = JsonUtility.FromJson<
                GameplayFlyoutMotionData>(source.text);
            return new GameplayFlyoutMotionProfile(authored);
        }

        public float Advance(float current, bool expanded, float deltaTime)
        {
            if (float.IsNaN(deltaTime)
                || float.IsInfinity(deltaTime)
                || deltaTime < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaTime));
            }

            return Mathf.MoveTowards(
                current,
                expanded ? 1f : 0f,
                deltaTime / data.revealSeconds);
        }

        public float Evaluate(float progress)
        {
            progress = Mathf.Clamp01(progress);
            switch (data.easing)
            {
                case "linear":
                    return progress;
                case "smoothStep":
                    return progress * progress * (3f - (2f * progress));
                default:
                    throw new InvalidOperationException(
                        $"Flyout easing '{data.easing}' is unsupported.");
            }
        }

        private static void Validate(GameplayFlyoutMotionData authored)
        {
            RequirePositive(authored.revealSeconds, "reveal seconds");
            RequirePositive(authored.laserOuterWidth, "outer laser width");
            RequirePositive(authored.laserInnerWidth, "inner laser width");
            RequirePositive(authored.laserCoreWidth, "core laser width");
            RequireUnit(authored.laserOuterAlpha, "outer laser alpha");
            RequireUnit(authored.laserInnerAlpha, "inner laser alpha");
            if (!string.Equals(
                    authored.easing,
                    "linear",
                    StringComparison.Ordinal)
                && !string.Equals(
                    authored.easing,
                    "smoothStep",
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Flyout easing '{authored.easing}' is unsupported.");
            }
        }

        private static void RequirePositive(float value, string label)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f)
            {
                throw new InvalidOperationException(
                    $"Flyout {label} must be positive and finite.");
            }
        }

        private static void RequireUnit(float value, string label)
        {
            if (float.IsNaN(value)
                || float.IsInfinity(value)
                || value < 0f
                || value > 1f)
            {
                throw new InvalidOperationException(
                    $"Flyout {label} must be between zero and one.");
            }
        }
    }
}
