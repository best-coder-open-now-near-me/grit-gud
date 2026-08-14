using System;

namespace GritGud.Domain.Gameplay
{
    public sealed class AccuracyDecayDefinition
    {
        public static AccuracyDecayDefinition None { get; } =
            new AccuracyDecayDefinition(1f, 100f);

        public AccuracyDecayDefinition(
            float halfLifeDistance,
            float minimumAccuracyPercent)
        {
            if (!IsFinite(halfLifeDistance) || halfLifeDistance <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(halfLifeDistance));
            }

            if (!IsFinite(minimumAccuracyPercent)
                || minimumAccuracyPercent <= 0f
                || minimumAccuracyPercent > 100f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(minimumAccuracyPercent));
            }

            HalfLifeDistance = halfLifeDistance;
            MinimumAccuracyPercent = minimumAccuracyPercent;
        }

        public float HalfLifeDistance { get; }

        public float MinimumAccuracyPercent { get; }

        public float EvaluatePercent(float distance)
        {
            if (!IsFinite(distance) || distance < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(distance));
            }

            if (MinimumAccuracyPercent == 100f)
            {
                return 100f;
            }

            double remainingAccuracy = 100d - MinimumAccuracyPercent;
            double decay = Math.Pow(0.5d, distance / HalfLifeDistance);
            return (float)(MinimumAccuracyPercent
                + (remainingAccuracy * decay));
        }

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }

    public static class AttackHitChanceRules
    {
        public static int CalculateFinalHitChancePercent(
            TargetExposureSnapshot exposure,
            AccuracyDecayDefinition accuracyDecay,
            float distance)
        {
            if (exposure == null)
            {
                throw new ArgumentNullException(nameof(exposure));
            }

            if (accuracyDecay == null)
            {
                throw new ArgumentNullException(nameof(accuracyDecay));
            }

            int geometricChance =
                TargetExposureRules.CalculateHitChancePercent(exposure);
            if (geometricChance == 0)
            {
                return 0;
            }

            float accuracyPercent = accuracyDecay.EvaluatePercent(distance);
            int combinedChance = (int)Math.Round(
                geometricChance * accuracyPercent / 100f,
                MidpointRounding.AwayFromZero);
            return Math.Max(1, Math.Min(100, combinedChance));
        }
    }
}
