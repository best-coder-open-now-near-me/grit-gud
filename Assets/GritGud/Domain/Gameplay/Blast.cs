using System;

namespace GritGud.Domain.Gameplay
{
    public enum BlastSubjectKind
    {
        Actor,
        DestructibleProp,
        Vehicle,
    }

    public sealed class BlastEffectRecord
    {
        public BlastEffectRecord(
            string entityId,
            BlastSubjectKind subjectKind,
            float distance,
            float occlusionExposure,
            float distanceFalloff,
            TargetRegionId? injuryRegion = null)
        {
            if (string.IsNullOrWhiteSpace(entityId))
            {
                throw new ArgumentException(
                    "Blast effects require an entity.",
                    nameof(entityId));
            }

            if (!Enum.IsDefined(typeof(BlastSubjectKind), subjectKind))
            {
                throw new ArgumentOutOfRangeException(nameof(subjectKind));
            }

            if (!IsFinite(distance) || distance < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(distance));
            }

            if (!IsUnitInterval(occlusionExposure))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(occlusionExposure));
            }

            if (!IsUnitInterval(distanceFalloff))
            {
                throw new ArgumentOutOfRangeException(nameof(distanceFalloff));
            }

            if (injuryRegion.HasValue
                && !Enum.IsDefined(
                    typeof(TargetRegionId),
                    injuryRegion.Value))
            {
                throw new ArgumentOutOfRangeException(nameof(injuryRegion));
            }

            if (subjectKind != BlastSubjectKind.Actor
                && injuryRegion.HasValue)
            {
                throw new ArgumentException(
                    "Only actor blast effects can identify an injury region.",
                    nameof(injuryRegion));
            }

            EntityId = entityId;
            SubjectKind = subjectKind;
            Distance = distance;
            OcclusionExposure = occlusionExposure;
            DistanceFalloff = distanceFalloff;
            InjuryRegion = injuryRegion;
        }

        public string EntityId { get; }

        public BlastSubjectKind SubjectKind { get; }

        public float Distance { get; }

        public float OcclusionExposure { get; }

        public float DistanceFalloff { get; }

        public float Exposure => OcclusionExposure * DistanceFalloff;

        public TargetRegionId? InjuryRegion { get; }

        public bool IsLocalizedActorInjury =>
            SubjectKind == BlastSubjectKind.Actor
            && InjuryRegion.HasValue;

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);

        private static bool IsUnitInterval(float value) =>
            IsFinite(value) && value >= 0f && value <= 1f;
    }
}
