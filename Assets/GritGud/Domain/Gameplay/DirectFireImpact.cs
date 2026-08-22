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
            surfaceMultipliers = new System.Collections.ObjectModel
                .ReadOnlyDictionary<string, float>(copied);
            var ordered = new List<SurfaceIntegrityDamageModifier>();
            foreach (KeyValuePair<string, float> entry in copied)
                ordered.Add(new SurfaceIntegrityDamageModifier(
                    entry.Key,
                    entry.Value));
            ordered.Sort((left, right) => StringComparer.Ordinal.Compare(
                left.SurfaceId,
                right.SurfaceId));
            Modifiers = ordered.AsReadOnly();
        }

        public string DamageTypeId { get; }

        public float BaseIntegrityDamage { get; }

        public IReadOnlyList<SurfaceIntegrityDamageModifier> Modifiers { get; }

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

    public sealed class WeaponDamageRangeProfile
    {
        public WeaponDamageRangeProfile(
            float halfLifeDistance = 0f,
            int minimumTransferPercent = 100)
        {
            if (!IsFinite(halfLifeDistance) || halfLifeDistance < 0f)
                throw new ArgumentOutOfRangeException(nameof(halfLifeDistance));
            if (minimumTransferPercent < 1 || minimumTransferPercent > 100)
                throw new ArgumentOutOfRangeException(
                    nameof(minimumTransferPercent));
            if (halfLifeDistance == 0f && minimumTransferPercent != 100)
                throw new ArgumentException(
                    "Non-decaying damage range must transfer 100 percent.",
                    nameof(minimumTransferPercent));
            HalfLifeDistance = halfLifeDistance;
            MinimumTransferPercent = minimumTransferPercent;
        }

        public float HalfLifeDistance { get; }

        public int MinimumTransferPercent { get; }

        public int EvaluateTransferPercent(float distance)
        {
            if (!IsFinite(distance) || distance < 0f)
                throw new ArgumentOutOfRangeException(nameof(distance));
            if (HalfLifeDistance == 0f) return 100;
            double retained = 100d * Math.Pow(
                0.5d,
                distance / HalfLifeDistance);
            return Math.Max(
                MinimumTransferPercent,
                Math.Min(100, (int)Math.Round(
                    retained,
                    MidpointRounding.AwayFromZero)));
        }

        public static WeaponDamageRangeProfile NoDecay =>
            new WeaponDamageRangeProfile();

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }

    public sealed class RegionConsequenceProfile
    {
        public RegionConsequenceProfile(
            TargetRegionId region,
            int systemicPerHundred,
            int structuralPerHundred,
            int motorPerHundred,
            int sensoryPerHundred,
            int bleedPerHundred,
            int consciousnessPerHundred,
            int respirationPerHundred,
            int criticalIncapacitationImpact = 0,
            int vitalImpact = 0)
        {
            if (!Enum.IsDefined(typeof(TargetRegionId), region))
                throw new ArgumentOutOfRangeException(nameof(region));
            Region = region;
            SystemicPerHundred = RequireConsequence(
                systemicPerHundred,
                nameof(systemicPerHundred));
            StructuralPerHundred = RequireConsequence(
                structuralPerHundred,
                nameof(structuralPerHundred));
            MotorPerHundred = RequireConsequence(
                motorPerHundred,
                nameof(motorPerHundred));
            SensoryPerHundred = RequireConsequence(
                sensoryPerHundred,
                nameof(sensoryPerHundred));
            BleedPerHundred = RequireConsequence(
                bleedPerHundred,
                nameof(bleedPerHundred));
            ConsciousnessPerHundred = RequireConsequence(
                consciousnessPerHundred,
                nameof(consciousnessPerHundred));
            RespirationPerHundred = RequireConsequence(
                respirationPerHundred,
                nameof(respirationPerHundred));
            CriticalIncapacitationImpact = RequireThreshold(
                criticalIncapacitationImpact,
                nameof(criticalIncapacitationImpact));
            VitalImpact = RequireThreshold(vitalImpact, nameof(vitalImpact));
        }

        public TargetRegionId Region { get; }
        public int SystemicPerHundred { get; }
        public int StructuralPerHundred { get; }
        public int MotorPerHundred { get; }
        public int SensoryPerHundred { get; }
        public int BleedPerHundred { get; }
        public int ConsciousnessPerHundred { get; }
        public int RespirationPerHundred { get; }
        public int CriticalIncapacitationImpact { get; }
        public int VitalImpact { get; }

        public int Project(int perHundred, int transferredImpact) => Math.Min(
            100,
            Math.Max(0, (perHundred * transferredImpact + 50) / 100));

        public bool CausesCriticalIncapacitation(int transferredImpact) =>
            CriticalIncapacitationImpact > 0
            && transferredImpact >= CriticalIncapacitationImpact;

        public bool CausesVitalDamage(int transferredImpact) =>
            VitalImpact > 0 && transferredImpact >= VitalImpact;

        private static int RequireConsequence(int value, string parameter)
        {
            if (value < 0 || value > 200)
                throw new ArgumentOutOfRangeException(parameter);
            return value;
        }

        private static int RequireThreshold(int value, string parameter)
        {
            if (value < 0 || value > 100)
                throw new ArgumentOutOfRangeException(parameter);
            return value;
        }
    }

    /// <summary>
    /// Versioned authority for actor injury and optional direct-fire prop
    /// damage. A weapon has one damage profile, so actor and surface tuning
    /// cannot drift in parallel schemas.
    /// </summary>
    public sealed class WeaponDamageProfileDefinition
    {
        public const int CurrentSchemaVersion = 1;
        private readonly IReadOnlyDictionary<
            TargetRegionId,
            RegionConsequenceProfile> regions;
        private readonly IReadOnlyList<RegionConsequenceProfile> orderedRegions;

        public WeaponDamageProfileDefinition(
            int schemaVersion,
            string damageProfileId,
            DamageMechanism mechanism,
            int baseImpact,
            int penetration,
            WeaponDamageRangeProfile range,
            IEnumerable<RegionConsequenceProfile> regions,
            DirectFireDamageDefinition directFireDamage = null)
        {
            if (schemaVersion != CurrentSchemaVersion)
                throw new ArgumentOutOfRangeException(nameof(schemaVersion));
            if (string.IsNullOrWhiteSpace(damageProfileId))
                throw new ArgumentException(
                    "Weapon damage profiles require an identifier.",
                    nameof(damageProfileId));
            if (!Enum.IsDefined(typeof(DamageMechanism), mechanism))
                throw new ArgumentOutOfRangeException(nameof(mechanism));
            if (baseImpact < 1 || baseImpact > 100)
                throw new ArgumentOutOfRangeException(nameof(baseImpact));
            if (penetration < 0 || penetration > 100)
                throw new ArgumentOutOfRangeException(nameof(penetration));
            var indexed = new Dictionary<
                TargetRegionId,
                RegionConsequenceProfile>();
            foreach (RegionConsequenceProfile profile in regions
                ?? throw new ArgumentNullException(nameof(regions)))
            {
                if (profile == null || !indexed.TryAdd(profile.Region, profile))
                    throw new ArgumentException(
                        "Weapon damage region profiles must be non-null and unique.",
                        nameof(regions));
            }
            var ordered = new List<RegionConsequenceProfile>();
            foreach (TargetRegionId region in Enum.GetValues(
                typeof(TargetRegionId)))
            {
                if (!indexed.ContainsKey(region))
                    throw new ArgumentException(
                        $"Weapon damage profile '{damageProfileId}' is missing region '{region}'.",
                        nameof(regions));
                ordered.Add(indexed[region]);
            }

            SchemaVersion = schemaVersion;
            DamageProfileId = damageProfileId.Trim();
            Mechanism = mechanism;
            BaseImpact = baseImpact;
            Penetration = penetration;
            Range = range ?? throw new ArgumentNullException(nameof(range));
            this.regions = indexed;
            orderedRegions = ordered.AsReadOnly();
            DirectFireDamage = directFireDamage;
        }

        public int SchemaVersion { get; }
        public string DamageProfileId { get; }
        public DamageMechanism Mechanism { get; }
        public int BaseImpact { get; }
        public int Penetration { get; }
        public WeaponDamageRangeProfile Range { get; }
        public IReadOnlyList<RegionConsequenceProfile> Regions => orderedRegions;
        public DirectFireDamageDefinition DirectFireDamage { get; }

        public RegionConsequenceProfile GetRegion(TargetRegionId region) =>
            regions.TryGetValue(region, out RegionConsequenceProfile profile)
                ? profile
                : throw new ArgumentOutOfRangeException(nameof(region));

        public int ResolveTransferredImpact(
            float distance,
            int armorTransferPercent = 100)
        {
            if (armorTransferPercent < 0 || armorTransferPercent > 100)
                throw new ArgumentOutOfRangeException(
                    nameof(armorTransferPercent));
            int rangeTransfer = Range.EvaluateTransferPercent(distance);
            long product = (long)BaseImpact * rangeTransfer
                * armorTransferPercent;
            return Math.Max(
                1,
                Math.Min(100, (int)((product + 5000L) / 10000L)));
        }

        internal static WeaponDamageProfileDefinition CreateLegacy(
            string profileId,
            DamageMechanism mechanism,
            float woundMovementPenalty,
            DirectFireDamageDefinition directFireDamage = null)
        {
            if (float.IsNaN(woundMovementPenalty)
                || float.IsInfinity(woundMovementPenalty)
                || woundMovementPenalty <= 0f)
                throw new ArgumentOutOfRangeException(
                    nameof(woundMovementPenalty));
            int impact = Math.Max(
                20,
                Math.Min(100, (int)Math.Round(
                    woundMovementPenalty * 25f,
                    MidpointRounding.AwayFromZero)));
            var profiles = new List<RegionConsequenceProfile>();
            foreach (TargetRegionId region in Enum.GetValues(
                typeof(TargetRegionId)))
            {
                int motor = region == TargetRegionId.LeftArm
                        || region == TargetRegionId.RightArm
                    ? 125
                    : region == TargetRegionId.LeftLeg
                        || region == TargetRegionId.RightLeg
                        ? 133
                        : region == TargetRegionId.Torso ? 33
                        : region == TargetRegionId.Head ? 20 : 25;
                profiles.Add(new RegionConsequenceProfile(
                    region,
                    systemicPerHundred: 0,
                    structuralPerHundred: 100,
                    motorPerHundred: motor,
                    sensoryPerHundred: region == TargetRegionId.Head ? 125 : 16,
                    bleedPerHundred: region == TargetRegionId.Head
                        || region == TargetRegionId.Torso ? 13 : 8,
                    consciousnessPerHundred:
                        region == TargetRegionId.Head ? 75 : 0,
                    respirationPerHundred:
                        region == TargetRegionId.Torso ? 75 : 0,
                    criticalIncapacitationImpact:
                        region == TargetRegionId.Head ? 70
                        : region == TargetRegionId.Torso ? 85 : 0,
                    vitalImpact:
                        region == TargetRegionId.Head ? 85
                        : region == TargetRegionId.Torso ? 95 : 0));
            }
            return new WeaponDamageProfileDefinition(
                CurrentSchemaVersion,
                profileId,
                mechanism,
                impact,
                penetration: 0,
                WeaponDamageRangeProfile.NoDecay,
                profiles,
                directFireDamage);
        }
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
            long worldStateRevision,
            int preferredFractureChunkIndex = -1)
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
            if (preferredFractureChunkIndex < -1
                || preferredFractureChunkIndex
                    >= DestructibleFracture.MaximumChunkCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(preferredFractureChunkIndex));
            }

            TargetId = targetId;
            SurfaceId = surfaceId;
            Point = point;
            NormalX = normalX;
            NormalY = normalY;
            NormalZ = normalZ;
            WorldStateRevision = worldStateRevision;
            PreferredFractureChunkIndex = preferredFractureChunkIndex;
        }

        public string TargetId { get; }

        public string SurfaceId { get; }

        public GameplayPosition Point { get; }

        public float NormalX { get; }

        public float NormalY { get; }

        public float NormalZ { get; }

        public long WorldStateRevision { get; }

        public int PreferredFractureChunkIndex { get; }

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
