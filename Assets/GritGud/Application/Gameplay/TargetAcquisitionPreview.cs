using System;
using GritGud.Domain.Gameplay;

namespace GritGud.Application.Gameplay
{
    public sealed class TargetAcquisitionPreview
    {
        public TargetAcquisitionPreview(
            TargetExposureSnapshot exposure,
            AccuracyDecayDefinition accuracyDecay,
            float distance,
            int hitChancePercent,
            float? maximumReach = null)
        {
            Exposure = exposure ?? throw new ArgumentNullException(nameof(exposure));
            AccuracyDecay = accuracyDecay ?? throw new ArgumentNullException(
                nameof(accuracyDecay));
            AccuracyDecay.EvaluatePercent(distance);
            if (hitChancePercent < 0 || hitChancePercent > 100)
            {
                throw new ArgumentOutOfRangeException(nameof(hitChancePercent));
            }
            if (maximumReach.HasValue
                && (float.IsNaN(maximumReach.Value)
                    || float.IsInfinity(maximumReach.Value)
                    || maximumReach.Value <= 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(maximumReach));
            }

            HitChancePercent = hitChancePercent;
            Distance = distance;
            MaximumReach = maximumReach;
        }

        public string ObserverId => Exposure.ObserverId;

        public string TargetId => Exposure.TargetId;

        public TargetExposureSnapshot Exposure { get; }

        public AccuracyDecayDefinition AccuracyDecay { get; }

        public float Distance { get; }

        public float? MaximumReach { get; }

        public bool IsWithinReach =>
            !MaximumReach.HasValue
            || Distance <= MaximumReach.Value + 0.0001f;

        public float AccuracyPercent =>
            AccuracyDecay.EvaluatePercent(Distance);

        public int HitChancePercent { get; }
    }

    public static class TargetPreviewCalculator
    {
        public static TargetAcquisitionPreview Calculate(
            TargetExposureSnapshot exposure,
            AccuracyDecayDefinition accuracyDecay,
            float distance,
            ContactAttackDefinition contact = null)
        {
            if (exposure == null)
            {
                throw new ArgumentNullException(nameof(exposure));
            }

            return new TargetAcquisitionPreview(
                exposure,
                accuracyDecay,
                distance,
                contact != null && distance > contact.MaximumReach + 0.0001f
                    ? 0
                    : AttackHitChanceRules.CalculateFinalHitChancePercent(
                        exposure,
                        accuracyDecay,
                        distance),
                contact?.MaximumReach);
        }
    }
}
