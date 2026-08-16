using System;
using System.Collections.Generic;

namespace GritGud.Domain.Gameplay
{
    public sealed class SurfaceIntegrityDamageModifier
    {
        public SurfaceIntegrityDamageModifier(
            string surfaceId,
            float multiplier)
        {
            if (string.IsNullOrWhiteSpace(surfaceId))
            {
                throw new ArgumentException(
                    "Surface-damage modifiers require a surface identifier.",
                    nameof(surfaceId));
            }

            if (!IsFinite(multiplier) || multiplier < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(multiplier));
            }

            SurfaceId = surfaceId;
            Multiplier = multiplier;
        }

        public string SurfaceId { get; }

        public float Multiplier { get; }

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }

    public sealed class DirectFireDamageDefinition
    {
        private readonly IReadOnlyDictionary<string, float> surfaceMultipliers;

        public DirectFireDamageDefinition(
            string damageTypeId,
            float baseIntegrityDamage,
            IEnumerable<SurfaceIntegrityDamageModifier> modifiers = null)
        {
            if (string.IsNullOrWhiteSpace(damageTypeId))
            {
                throw new ArgumentException(
                    "Direct-fire damage requires a damage-type identifier.",
                    nameof(damageTypeId));
            }

            if (!IsFinite(baseIntegrityDamage) || baseIntegrityDamage <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(baseIntegrityDamage));
            }

            var copied = new Dictionary<string, float>(StringComparer.Ordinal);
            foreach (SurfaceIntegrityDamageModifier modifier in
                modifiers ?? Array.Empty<SurfaceIntegrityDamageModifier>())
            {
                if (modifier == null)
                {
                    throw new ArgumentException(
                        "Surface-damage modifiers cannot contain null entries.",
                        nameof(modifiers));
                }

                if (!copied.TryAdd(modifier.SurfaceId, modifier.Multiplier))
                {
                    throw new ArgumentException(
                        $"Surface-damage modifier '{modifier.SurfaceId}' is duplicated.",
                        nameof(modifiers));
                }
            }

            DamageTypeId = damageTypeId;
            BaseIntegrityDamage = baseIntegrityDamage;
            surfaceMultipliers = copied;
        }

        public string DamageTypeId { get; }

        public float BaseIntegrityDamage { get; }

        public float EvaluateIntegrityDamage(string surfaceId)
        {
            float multiplier = surfaceMultipliers.TryGetValue(
                    surfaceId ?? string.Empty,
                    out float authored)
                ? authored
                : 1f;
            return BaseIntegrityDamage * multiplier;
        }

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }

    public sealed class DirectFireImpactRecord
    {
        public DirectFireImpactRecord(
            string targetId,
            string surfaceId,
            GameplayPosition point,
            float normalX,
            float normalY,
            float normalZ,
            long worldStateRevision)
        {
            if (string.IsNullOrWhiteSpace(targetId))
            {
                throw new ArgumentException(
                    "Direct-fire impacts require a stable target identifier.",
                    nameof(targetId));
            }

            if (string.IsNullOrWhiteSpace(surfaceId))
            {
                throw new ArgumentException(
                    "Direct-fire impacts require an authored surface identifier.",
                    nameof(surfaceId));
            }

            if (!IsFinite(normalX)
                || !IsFinite(normalY)
                || !IsFinite(normalZ)
                || ((normalX * normalX) + (normalY * normalY)
                    + (normalZ * normalZ)) <= 0.0001f)
            {
                throw new ArgumentException(
                    "Direct-fire impact normals must be finite and non-zero.",
                    nameof(normalX));
            }

            if (worldStateRevision < 0L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(worldStateRevision));
            }

            TargetId = targetId;
            SurfaceId = surfaceId;
            Point = point;
            NormalX = normalX;
            NormalY = normalY;
            NormalZ = normalZ;
            WorldStateRevision = worldStateRevision;
        }

        public string TargetId { get; }

        public string SurfaceId { get; }

        public GameplayPosition Point { get; }

        public float NormalX { get; }

        public float NormalY { get; }

        public float NormalZ { get; }

        public long WorldStateRevision { get; }

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
