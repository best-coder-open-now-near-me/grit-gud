using System;

namespace GritGud.Domain.Gameplay
{
    public sealed class SmokeFieldDefinition
    {
        public SmokeFieldDefinition(
            float radius,
            float height,
            float explorationDurationSeconds,
            int durationTurnEnds,
            float minimumObscuredPath)
        {
            if (!IsFinite(radius) || radius <= 0f)
                throw new ArgumentOutOfRangeException(nameof(radius));
            if (!IsFinite(height) || height <= 0f)
                throw new ArgumentOutOfRangeException(nameof(height));
            if (!IsFinite(explorationDurationSeconds)
                || explorationDurationSeconds <= 0f)
                throw new ArgumentOutOfRangeException(
                    nameof(explorationDurationSeconds));
            if (durationTurnEnds <= 0)
                throw new ArgumentOutOfRangeException(nameof(durationTurnEnds));
            if (!IsFinite(minimumObscuredPath)
                || minimumObscuredPath <= 0f
                || minimumObscuredPath > radius * 2f)
                throw new ArgumentOutOfRangeException(
                    nameof(minimumObscuredPath));

            Radius = radius;
            Height = height;
            ExplorationDurationSeconds = explorationDurationSeconds;
            DurationTurnEnds = durationTurnEnds;
            MinimumObscuredPath = minimumObscuredPath;
        }

        public float Radius { get; }

        public float Height { get; }

        public float ExplorationDurationSeconds { get; }

        public int DurationTurnEnds { get; }

        public float MinimumObscuredPath { get; }

        public bool Matches(SmokeFieldDefinition other) =>
            other != null
            && Radius == other.Radius
            && Height == other.Height
            && ExplorationDurationSeconds
                == other.ExplorationDurationSeconds
            && DurationTurnEnds == other.DurationTurnEnds
            && MinimumObscuredPath == other.MinimumObscuredPath;

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }

    public sealed class SmokeFieldRecord
    {
        public SmokeFieldRecord(
            string id,
            string sourceActorId,
            string sourceItemId,
            GameplayPosition origin,
            SmokeFieldDefinition definition)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException(
                    "Smoke fields require an identifier.",
                    nameof(id));
            if (string.IsNullOrWhiteSpace(sourceActorId))
                throw new ArgumentException(
                    "Smoke fields require a source actor.",
                    nameof(sourceActorId));
            if (string.IsNullOrWhiteSpace(sourceItemId))
                throw new ArgumentException(
                    "Smoke fields require a source item.",
                    nameof(sourceItemId));

            Id = id;
            SourceActorId = sourceActorId;
            SourceItemId = sourceItemId;
            Origin = origin;
            Definition = definition ?? throw new ArgumentNullException(
                nameof(definition));
        }

        public string Id { get; }

        public string SourceActorId { get; }

        public string SourceItemId { get; }

        public GameplayPosition Origin { get; }

        public SmokeFieldDefinition Definition { get; }
    }

    public readonly struct SmokeFieldSnapshot
    {
        public SmokeFieldSnapshot(
            SmokeFieldRecord field,
            float remainingFraction)
        {
            Field = field ?? throw new ArgumentNullException(nameof(field));
            if (float.IsNaN(remainingFraction)
                || float.IsInfinity(remainingFraction)
                || remainingFraction < 0f
                || remainingFraction > 1f)
                throw new ArgumentOutOfRangeException(
                    nameof(remainingFraction));
            RemainingFraction = remainingFraction;
        }

        public SmokeFieldRecord Field { get; }

        public float RemainingFraction { get; }
    }
}
