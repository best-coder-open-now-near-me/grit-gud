using System;
using System.Collections.Generic;

namespace GritGud.Domain.Gameplay
{
    /// <summary>
    /// Portable authoritative behavior for a persistent ground fire. Visual
    /// particles are projections of this definition and never drive damage or
    /// spatial evidence.
    /// </summary>
    public sealed class FireFieldDefinition
    {
        public FireFieldDefinition(
            float initialRadius,
            float maximumRadius,
            float height,
            float explorationDurationSeconds,
            int durationTurnEnds,
            float explorationPulseSeconds,
            float actorWoundMovementPenalty,
            float destructibleIntegrityDamage,
            float minimumHazardPath)
        {
            RequirePositive(initialRadius, nameof(initialRadius));
            RequirePositive(maximumRadius, nameof(maximumRadius));
            if (maximumRadius < initialRadius)
                throw new ArgumentOutOfRangeException(nameof(maximumRadius));
            RequirePositive(height, nameof(height));
            RequirePositive(
                explorationDurationSeconds,
                nameof(explorationDurationSeconds));
            if (durationTurnEnds <= 0)
                throw new ArgumentOutOfRangeException(nameof(durationTurnEnds));
            RequirePositive(
                explorationPulseSeconds,
                nameof(explorationPulseSeconds));
            RequireNonNegative(
                actorWoundMovementPenalty,
                nameof(actorWoundMovementPenalty));
            RequireNonNegative(
                destructibleIntegrityDamage,
                nameof(destructibleIntegrityDamage));
            if (actorWoundMovementPenalty <= 0f
                && destructibleIntegrityDamage <= 0f)
            {
                throw new ArgumentException(
                    "Persistent fire requires at least one authoritative consequence.");
            }
            RequirePositive(minimumHazardPath, nameof(minimumHazardPath));
            if (minimumHazardPath > maximumRadius * 2f)
                throw new ArgumentOutOfRangeException(nameof(minimumHazardPath));

            InitialRadius = initialRadius;
            MaximumRadius = maximumRadius;
            Height = height;
            ExplorationDurationSeconds = explorationDurationSeconds;
            DurationTurnEnds = durationTurnEnds;
            ExplorationPulseSeconds = explorationPulseSeconds;
            ActorWoundMovementPenalty = actorWoundMovementPenalty;
            DestructibleIntegrityDamage = destructibleIntegrityDamage;
            MinimumHazardPath = minimumHazardPath;
        }

        public float InitialRadius { get; }
        public float MaximumRadius { get; }
        public float Height { get; }
        public float ExplorationDurationSeconds { get; }
        public int DurationTurnEnds { get; }
        public float ExplorationPulseSeconds { get; }
        public float ActorWoundMovementPenalty { get; }
        public float DestructibleIntegrityDamage { get; }
        public float MinimumHazardPath { get; }

        public float RadiusAt(float remainingFraction)
        {
            RequireFraction(remainingFraction, nameof(remainingFraction));
            float elapsedFraction = 1f - remainingFraction;
            return InitialRadius
                + ((MaximumRadius - InitialRadius) * elapsedFraction);
        }

        public bool Matches(FireFieldDefinition other) =>
            other != null
            && InitialRadius == other.InitialRadius
            && MaximumRadius == other.MaximumRadius
            && Height == other.Height
            && ExplorationDurationSeconds == other.ExplorationDurationSeconds
            && DurationTurnEnds == other.DurationTurnEnds
            && ExplorationPulseSeconds == other.ExplorationPulseSeconds
            && ActorWoundMovementPenalty == other.ActorWoundMovementPenalty
            && DestructibleIntegrityDamage == other.DestructibleIntegrityDamage
            && MinimumHazardPath == other.MinimumHazardPath;

        private static void RequirePositive(float value, string parameter)
        {
            if (!IsFinite(value) || value <= 0f)
                throw new ArgumentOutOfRangeException(parameter);
        }

        private static void RequireNonNegative(float value, string parameter)
        {
            if (!IsFinite(value) || value < 0f)
                throw new ArgumentOutOfRangeException(parameter);
        }

        internal static void RequireFraction(float value, string parameter)
        {
            if (!IsFinite(value) || value < 0f || value > 1f)
                throw new ArgumentOutOfRangeException(parameter);
        }

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }

    public sealed class FireFieldRecord
    {
        public FireFieldRecord(
            string id,
            string sourceActorId,
            string sourceItemId,
            GameplayPosition origin,
            FireFieldDefinition definition)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException(
                    "Fire fields require an identifier.", nameof(id));
            if (string.IsNullOrWhiteSpace(sourceActorId))
                throw new ArgumentException(
                    "Fire fields require a source actor.",
                    nameof(sourceActorId));
            if (string.IsNullOrWhiteSpace(sourceItemId))
                throw new ArgumentException(
                    "Fire fields require a source item.",
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
        public FireFieldDefinition Definition { get; }
    }

    public readonly struct FireFieldSnapshot
    {
        public FireFieldSnapshot(
            FireFieldRecord field,
            float remainingFraction,
            float pulseProgress = 0f)
        {
            Field = field ?? throw new ArgumentNullException(nameof(field));
            FireFieldDefinition.RequireFraction(
                remainingFraction,
                nameof(remainingFraction));
            if (float.IsNaN(pulseProgress)
                || float.IsInfinity(pulseProgress)
                || pulseProgress < 0f
                || pulseProgress >= 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(pulseProgress));
            }
            RemainingFraction = remainingFraction;
            PulseProgress = pulseProgress;
        }

        public FireFieldRecord Field { get; }
        public float RemainingFraction { get; }
        public float PulseProgress { get; }
        public float CurrentRadius =>
            Field.Definition.RadiusAt(RemainingFraction);
    }

    public enum FireFieldSubjectKind
    {
        Actor,
        DestructibleProp,
    }

    public sealed class FireFieldEffectRecord
    {
        public FireFieldEffectRecord(
            FireFieldSubjectKind subjectKind,
            string entityId,
            float distance)
        {
            if (!Enum.IsDefined(typeof(FireFieldSubjectKind), subjectKind))
                throw new ArgumentOutOfRangeException(nameof(subjectKind));
            if (string.IsNullOrWhiteSpace(entityId))
                throw new ArgumentException(
                    "Fire effects require a subject identifier.",
                    nameof(entityId));
            if (float.IsNaN(distance)
                || float.IsInfinity(distance)
                || distance < 0f)
                throw new ArgumentOutOfRangeException(nameof(distance));
            SubjectKind = subjectKind;
            EntityId = entityId;
            Distance = distance;
        }

        public FireFieldSubjectKind SubjectKind { get; }
        public string EntityId { get; }
        public float Distance { get; }
    }

    public sealed class FireFieldPulseRecord
    {
        public FireFieldPulseRecord(
            string fieldId,
            float radius,
            IEnumerable<FireFieldEffectRecord> effects)
        {
            if (string.IsNullOrWhiteSpace(fieldId))
                throw new ArgumentException(
                    "Fire pulses require a field identifier.",
                    nameof(fieldId));
            if (float.IsNaN(radius)
                || float.IsInfinity(radius)
                || radius <= 0f)
                throw new ArgumentOutOfRangeException(nameof(radius));
            var copy = new List<FireFieldEffectRecord>(
                effects ?? throw new ArgumentNullException(nameof(effects)));
            foreach (FireFieldEffectRecord effect in copy)
                if (effect == null)
                    throw new ArgumentException(
                        "Fire pulses cannot contain null effects.",
                        nameof(effects));
            copy.Sort((left, right) =>
            {
                int comparison = left.SubjectKind.CompareTo(right.SubjectKind);
                return comparison != 0
                    ? comparison
                    : string.CompareOrdinal(left.EntityId, right.EntityId);
            });
            for (int index = 0; index < copy.Count; index++)
            {
                if (index > 0
                    && copy[index - 1].SubjectKind == copy[index].SubjectKind
                    && string.Equals(
                        copy[index - 1].EntityId,
                        copy[index].EntityId,
                        StringComparison.Ordinal))
                    throw new ArgumentException(
                        "Fire pulses cannot repeat a subject.",
                        nameof(effects));
            }
            FieldId = fieldId;
            Radius = radius;
            Effects = copy.AsReadOnly();
        }

        public string FieldId { get; }
        public float Radius { get; }
        public IReadOnlyList<FireFieldEffectRecord> Effects { get; }
    }
}
