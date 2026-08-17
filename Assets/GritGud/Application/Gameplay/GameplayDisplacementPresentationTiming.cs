using System;
using GritGud.Domain.Gameplay;

namespace GritGud.Application.Gameplay
{
    public static class GameplayDisplacementPresentationTiming
    {
        public const float PushSeconds = 0.75f;
        public const float PushContactNormalizedTime = 0.2f;
        public const float PushReleaseNormalizedTime = 0.9f;

        public static float GetDurationSeconds(DisplacementRecord record)
        {
            if (record == null)
                throw new ArgumentNullException(nameof(record));

            switch (record.Request.ActionKind)
            {
                case DisplacementActionKind.Push:
                    return PushSeconds;
                case DisplacementActionKind.PushOff:
                    return 0.9f;
                default:
                    return Math.Max(
                        0.3f,
                        record.PreviousPosition.DistanceTo(
                            record.ResultingPosition) / 5f);
            }
        }

        public static float EvaluateSubjectProgress(
            DisplacementActionKind actionKind,
            float normalizedProgress)
        {
            float clamped = Math.Max(0f, Math.Min(1f, normalizedProgress));
            if (actionKind != DisplacementActionKind.Push)
                return SmoothStep(clamped);
            if (clamped <= PushContactNormalizedTime)
                return 0f;
            if (clamped >= PushReleaseNormalizedTime)
                return 1f;

            float contactProgress =
                (clamped - PushContactNormalizedTime)
                / (PushReleaseNormalizedTime - PushContactNormalizedTime);
            return SmoothStep(contactProgress);
        }

        private static float SmoothStep(float value) =>
            value * value * (3f - (2f * value));
    }
}
