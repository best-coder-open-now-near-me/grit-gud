using System;

namespace GritGud.Domain.Gameplay
{
    public readonly struct ActorWoundSnapshot
    {
        public ActorWoundSnapshot(
            string actorId,
            int woundCount,
            float movementPenalty)
            : this(
                actorId,
                headWounds: 0,
                torsoWounds: 0,
                leftArmWounds: 0,
                rightArmWounds: 0,
                leftLegWounds: 0,
                rightLegWounds: 0,
                unlocalizedWounds: woundCount,
                movementPenalty: movementPenalty)
        {
        }

        public ActorWoundSnapshot(
            string actorId,
            int headWounds,
            int torsoWounds,
            int leftArmWounds,
            int rightArmWounds,
            int leftLegWounds,
            int rightLegWounds,
            float movementPenalty)
            : this(
                actorId,
                headWounds,
                torsoWounds,
                leftArmWounds,
                rightArmWounds,
                leftLegWounds,
                rightLegWounds,
                unlocalizedWounds: 0,
                movementPenalty: movementPenalty)
        {
        }

        public ActorWoundSnapshot(
            string actorId,
            int headWounds,
            int torsoWounds,
            int leftArmWounds,
            int rightArmWounds,
            int leftLegWounds,
            int rightLegWounds,
            int unlocalizedWounds,
            float movementPenalty)
        {
            if (string.IsNullOrWhiteSpace(actorId))
            {
                throw new ArgumentException(
                    "Wound snapshots require an actor identifier.",
                    nameof(actorId));
            }

            if (headWounds < 0
                || torsoWounds < 0
                || leftArmWounds < 0
                || rightArmWounds < 0
                || leftLegWounds < 0
                || rightLegWounds < 0
                || unlocalizedWounds < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(headWounds),
                    "Regional wound counts cannot be negative.");
            }

            int woundCount = checked(
                headWounds
                + torsoWounds
                + leftArmWounds
                + rightArmWounds
                + leftLegWounds
                + rightLegWounds
                + unlocalizedWounds);

            if (float.IsNaN(movementPenalty)
                || float.IsInfinity(movementPenalty)
                || movementPenalty < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(movementPenalty));
            }

            if ((woundCount == 0) != (movementPenalty == 0f))
            {
                throw new ArgumentException(
                    "Wound count and movement penalty must describe the same state.",
                    nameof(movementPenalty));
            }

            ActorId = actorId;
            WoundCount = woundCount;
            MovementPenalty = movementPenalty;
            HeadWounds = headWounds;
            TorsoWounds = torsoWounds;
            LeftArmWounds = leftArmWounds;
            RightArmWounds = rightArmWounds;
            LeftLegWounds = leftLegWounds;
            RightLegWounds = rightLegWounds;
            UnlocalizedWounds = unlocalizedWounds;
        }

        public string ActorId { get; }

        public int WoundCount { get; }

        public float MovementPenalty { get; }

        public int HeadWounds { get; }

        public int TorsoWounds { get; }

        public int LeftArmWounds { get; }

        public int RightArmWounds { get; }

        public int LeftLegWounds { get; }

        public int RightLegWounds { get; }

        public int UnlocalizedWounds { get; }

        public int GetWoundCount(TargetRegionId region)
        {
            switch (region)
            {
                case TargetRegionId.Head:
                    return HeadWounds;
                case TargetRegionId.Torso:
                    return TorsoWounds;
                case TargetRegionId.LeftArm:
                    return LeftArmWounds;
                case TargetRegionId.RightArm:
                    return RightArmWounds;
                case TargetRegionId.LeftLeg:
                    return LeftLegWounds;
                case TargetRegionId.RightLeg:
                    return RightLegWounds;
                default:
                    throw new ArgumentOutOfRangeException(nameof(region));
            }
        }

        public ActorWoundSnapshot AddWound(
            TargetRegionId region,
            float movementPenalty)
        {
            if (!Enum.IsDefined(typeof(TargetRegionId), region))
            {
                throw new ArgumentOutOfRangeException(nameof(region));
            }

            if (float.IsNaN(movementPenalty)
                || float.IsInfinity(movementPenalty)
                || movementPenalty <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(movementPenalty));
            }

            return new ActorWoundSnapshot(
                ActorId,
                HeadWounds + (region == TargetRegionId.Head ? 1 : 0),
                TorsoWounds + (region == TargetRegionId.Torso ? 1 : 0),
                LeftArmWounds + (region == TargetRegionId.LeftArm ? 1 : 0),
                RightArmWounds + (region == TargetRegionId.RightArm ? 1 : 0),
                LeftLegWounds + (region == TargetRegionId.LeftLeg ? 1 : 0),
                RightLegWounds + (region == TargetRegionId.RightLeg ? 1 : 0),
                UnlocalizedWounds,
                MovementPenalty + movementPenalty);
        }

        public ActorWoundSnapshot AddUnlocalizedWound(float movementPenalty)
        {
            if (float.IsNaN(movementPenalty)
                || float.IsInfinity(movementPenalty)
                || movementPenalty <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(movementPenalty));
            }

            return new ActorWoundSnapshot(
                ActorId,
                HeadWounds,
                TorsoWounds,
                LeftArmWounds,
                RightArmWounds,
                LeftLegWounds,
                RightLegWounds,
                UnlocalizedWounds + 1,
                MovementPenalty + movementPenalty);
        }

        public bool HasSameState(ActorWoundSnapshot other) =>
            string.Equals(ActorId, other.ActorId, StringComparison.Ordinal)
            && WoundCount == other.WoundCount
            && MovementPenalty == other.MovementPenalty
            && HeadWounds == other.HeadWounds
            && TorsoWounds == other.TorsoWounds
            && LeftArmWounds == other.LeftArmWounds
            && RightArmWounds == other.RightArmWounds
            && LeftLegWounds == other.LeftLegWounds
            && RightLegWounds == other.RightLegWounds
            && UnlocalizedWounds == other.UnlocalizedWounds;
    }

    public sealed class ActorWoundRecord
    {
        public ActorWoundRecord(
            TargetRegionId region,
            float appliedMovementPenalty,
            ActorWoundSnapshot previous,
            ActorWoundSnapshot resulting)
        {
            if (!Enum.IsDefined(typeof(TargetRegionId), region))
            {
                throw new ArgumentOutOfRangeException(nameof(region));
            }

            if (float.IsNaN(appliedMovementPenalty)
                || float.IsInfinity(appliedMovementPenalty)
                || appliedMovementPenalty <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(appliedMovementPenalty));
            }

            if (!string.Equals(
                    previous.ActorId,
                    resulting.ActorId,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "A wound record cannot change actor identity.",
                    nameof(resulting));
            }

            ActorWoundSnapshot expected = previous.AddWound(
                region,
                appliedMovementPenalty);
            if (!resulting.HasSameState(expected))
            {
                throw new ArgumentException(
                    "The resulting wound state does not match the applied regional wound.",
                    nameof(resulting));
            }

            Region = region;
            AppliedMovementPenalty = appliedMovementPenalty;
            Previous = previous;
            Resulting = resulting;
        }

        public string ActorId => Previous.ActorId;

        public TargetRegionId Region { get; }

        public float AppliedMovementPenalty { get; }

        public ActorWoundSnapshot Previous { get; }

        public ActorWoundSnapshot Resulting { get; }
    }

    public sealed class AttackResolutionRecord
    {
        public AttackResolutionRecord(
            long sequence,
            uint resolutionSeed,
            TargetExposureSnapshot exposure,
            AccuracyDecayDefinition accuracyDecay,
            float distance,
            ActorWoundSnapshot targetWoundsBefore,
            int hitRoll,
            int regionRoll,
            TargetRegionId? hitRegion,
            ActorWoundRecord wound,
            float? maximumReach = null,
            IGameplayActionContext context = null,
            ActorInjuryState targetInjuryStateBefore = null,
            ActorInjuryDelta injury = null,
            int capabilityAccuracyDeltaPercent = 0)
        {
            if (sequence <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sequence));
            }

            Exposure = exposure ?? throw new ArgumentNullException(nameof(exposure));
            AccuracyDecay = accuracyDecay ?? throw new ArgumentNullException(
                nameof(accuracyDecay));
            if (float.IsNaN(distance)
                || float.IsInfinity(distance)
                || distance < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(distance));
            }
            if (maximumReach.HasValue
                && (float.IsNaN(maximumReach.Value)
                    || float.IsInfinity(maximumReach.Value)
                    || maximumReach.Value <= 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(maximumReach));
            }
            if (maximumReach.HasValue
                && distance > maximumReach.Value + 0.0001f)
            {
                throw new ArgumentException(
                    "Contact attack records cannot exceed their authored reach.",
                    nameof(distance));
            }
            if (!string.Equals(
                    exposure.TargetId,
                    targetWoundsBefore.ActorId,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Attack exposure and wound state must describe the same target.",
                    nameof(targetWoundsBefore));
            }
            if (context != null
                && (!string.Equals(
                        context.AttackerId,
                        exposure.ObserverId,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        context.SubjectId,
                        exposure.TargetId,
                        StringComparison.Ordinal)))
            {
                throw new ArgumentException(
                    "Attack context identities must match recorded exposure.",
                    nameof(context));
            }

            if (hitRoll < 1 || hitRoll > 100)
            {
                throw new ArgumentOutOfRangeException(nameof(hitRoll));
            }
            if (capabilityAccuracyDeltaPercent < -100
                || capabilityAccuracyDeltaPercent > 100)
                throw new ArgumentOutOfRangeException(
                    nameof(capabilityAccuracyDeltaPercent));

            int hitChance = AttackHitChanceRules.CalculateFinalHitChancePercent(
                exposure,
                accuracyDecay,
                distance,
                (context?.AccuracyDeltaPercent ?? 0)
                    + capabilityAccuracyDeltaPercent);
            bool hit = hitRoll <= hitChance;
            ActorInjuryState resolvedInjuryStateBefore =
                targetInjuryStateBefore
                ?? LegacyWoundProjection.ToInjuryState(
                    targetWoundsBefore,
                    int.MaxValue);
            if (!string.Equals(
                    resolvedInjuryStateBefore.ActorId,
                    targetWoundsBefore.ActorId,
                    StringComparison.Ordinal)
                || !LegacyWoundProjection.From(resolvedInjuryStateBefore)
                    .HasSameState(targetWoundsBefore))
                throw new ArgumentException(
                    "Attack injury state must project the recorded prior wounds.",
                    nameof(targetInjuryStateBefore));
            ActorInjuryDelta resolvedInjury = injury;
            if (hit)
            {
                if (!hitRegion.HasValue || wound == null)
                {
                    throw new ArgumentException(
                        "Hits require a recorded region and wound.",
                        nameof(wound));
                }

                if (regionRoll < 1 || regionRoll > exposure.VisibleSampleCount)
                {
                    throw new ArgumentOutOfRangeException(nameof(regionRoll));
                }

                TargetRegionId selected = TargetExposureRules.SelectVisibleRegion(
                    exposure,
                    regionRoll);
                if (selected != hitRegion.Value
                    || wound.Region != hitRegion.Value
                    || !WoundsMatch(wound.Previous, targetWoundsBefore))
                {
                    throw new ArgumentException(
                        "The wound does not match the recorded region roll.",
                        nameof(wound));
                }
                if (resolvedInjury == null)
                {
                    int legacySeverity = Math.Max(
                        1,
                        Math.Min(
                            100,
                            (int)Math.Round(
                                wound.AppliedMovementPenalty * 25f,
                                MidpointRounding.AwayFromZero)));
                    var legacyImpact = new LocalizedImpact(
                        "impact:" + sequence + ":" + exposure.ObserverId
                            + ":" + exposure.TargetId,
                        exposure.ObserverId,
                        exposure.TargetId,
                        "attack.legacy",
                        hitRegion,
                        maximumReach.HasValue
                            ? DamageMechanism.Blunt
                            : DamageMechanism.Ballistic,
                        legacySeverity,
                        sequence);
                    resolvedInjury = ActorInjuryRules.ApplyImpact(
                        resolvedInjuryStateBefore,
                        legacyImpact,
                        wound.AppliedMovementPenalty).Delta;
                }
                ActorInjuryState resolvedInjuryStateAfter =
                    ActorInjuryRules.ApplyDelta(
                        resolvedInjuryStateBefore,
                        resolvedInjury);
                if (resolvedInjury.Injury.Region != hitRegion
                    || !LegacyWoundProjection.From(resolvedInjuryStateAfter)
                        .HasSameState(wound.Resulting))
                    throw new ArgumentException(
                        "Localized injury does not match the recorded wound.",
                        nameof(injury));
            }
            else if (regionRoll != 0 || hitRegion.HasValue || wound != null
                || resolvedInjury != null)
            {
                throw new ArgumentException(
                    "Misses cannot contain a region or wound outcome.",
                    nameof(wound));
            }

            AttackResolutionRules.ValidateRecordedRolls(
                resolutionSeed,
                exposure.VisibleSampleCount,
                hit,
                hitRoll,
                regionRoll);

            Sequence = sequence;
            ResolutionSeed = resolutionSeed;
            Distance = distance;
            MaximumReach = maximumReach;
            TargetWoundsBefore = targetWoundsBefore;
            TargetInjuryStateBefore = resolvedInjuryStateBefore;
            HitRoll = hitRoll;
            RegionRoll = regionRoll;
            HitRegion = hitRegion;
            Wound = wound;
            Context = context;
            Injury = resolvedInjury;
            CapabilityAccuracyDeltaPercent =
                capabilityAccuracyDeltaPercent;
        }

        public long Sequence { get; }

        public uint ResolutionSeed { get; }

        public string AttackerId => Exposure.ObserverId;

        public string TargetId => Exposure.TargetId;

        public TargetExposureSnapshot Exposure { get; }

        public AccuracyDecayDefinition AccuracyDecay { get; }

        public float Distance { get; }

        public float? MaximumReach { get; }

        public bool IsContactAttack => MaximumReach.HasValue;

        public ActorWoundSnapshot TargetWoundsBefore { get; }

        public ActorInjuryState TargetInjuryStateBefore { get; }

        public ActorWoundSnapshot TargetWoundsAfter =>
            Wound == null ? TargetWoundsBefore : Wound.Resulting;

        public int BaseHitChancePercent => 100;

        public int ExposureModifierPercent =>
            GeometricHitChancePercent - BaseHitChancePercent;

        public int GeometricHitChancePercent =>
            TargetExposureRules.CalculateHitChancePercent(Exposure);

        public float AccuracyPercent =>
            AccuracyDecay.EvaluatePercent(Distance);

        public int AccuracyModifierPercent =>
            FinalHitChancePercent - GeometricHitChancePercent;

        public int FinalHitChancePercent =>
            AttackHitChanceRules.CalculateFinalHitChancePercent(
                Exposure,
                AccuracyDecay,
                Distance,
                (Context?.AccuracyDeltaPercent ?? 0)
                    + CapabilityAccuracyDeltaPercent);

        public int CapabilityAccuracyDeltaPercent { get; }

        public int HitRoll { get; }

        public bool Hit => HitRoll <= FinalHitChancePercent;

        public int RegionRoll { get; }

        public TargetRegionId? HitRegion { get; }

        public ActorWoundRecord Wound { get; }

        public ActorInjuryDelta Injury { get; }

        public IGameplayActionContext Context { get; }

        private static bool WoundsMatch(
            ActorWoundSnapshot left,
            ActorWoundSnapshot right)
        {
            return left.HasSameState(right);
        }
    }

    public static class AttackResolutionRules
    {
        internal static void ValidateRecordedRolls(
            uint resolutionSeed,
            int visibleSampleCount,
            bool hit,
            int hitRoll,
            int regionRoll)
        {
            var rolls = new SeededAttackRolls(resolutionSeed);
            if (rolls.Roll(100) != hitRoll
                || (hit && rolls.Roll(visibleSampleCount) != regionRoll))
            {
                throw new ArgumentException(
                    "The recorded rolls do not match the resolution seed.",
                    nameof(hitRoll));
            }
        }

        public static uint DeriveResolutionSeed(uint scenarioSeed, long sequence)
        {
            if (sequence <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sequence));
            }

            unchecked
            {
                uint mixedSequence = (uint)sequence * 0x9E3779B9u;
                mixedSequence ^= (uint)(sequence >> 32);
                return scenarioSeed ^ mixedSequence ^ 0xA341316Cu;
            }
        }

        public static AttackResolutionRecord Resolve(
            long sequence,
            uint resolutionSeed,
            TargetExposureSnapshot exposure,
            AccuracyDecayDefinition accuracyDecay,
            float distance,
            ActorWoundSnapshot targetWoundsBefore,
            float woundMovementPenalty,
            ContactAttackDefinition contact = null,
            IGameplayActionContext context = null,
            ActorInjuryState targetInjuryStateBefore = null,
            string weaponId = null,
            DamageMechanism? damageMechanism = null,
            int capabilityAccuracyDeltaPercent = 0)
        {
            return ResolveInternal(
                sequence,
                resolutionSeed,
                exposure,
                accuracyDecay,
                distance,
                targetWoundsBefore,
                woundMovementPenalty,
                damageProfile: null,
                contact,
                context,
                targetInjuryStateBefore,
                weaponId,
                damageMechanism,
                capabilityAccuracyDeltaPercent);
        }

        public static AttackResolutionRecord Resolve(
            long sequence,
            uint resolutionSeed,
            TargetExposureSnapshot exposure,
            float distance,
            ActorWoundSnapshot targetWoundsBefore,
            AttackDefinition attack,
            IGameplayActionContext context = null,
            ActorInjuryState targetInjuryStateBefore = null,
            int capabilityAccuracyDeltaPercent = 0)
        {
            if (attack == null) throw new ArgumentNullException(nameof(attack));
            return attack.UsesLegacyWoundPayload
                ? Resolve(
                    sequence,
                    resolutionSeed,
                    exposure,
                    attack.AccuracyDecay,
                    distance,
                    targetWoundsBefore,
                    attack.WoundMovementPenalty,
                    attack.Contact,
                    context,
                    targetInjuryStateBefore,
                    attack.ActionId,
                    attack.DamageProfile.Mechanism,
                    capabilityAccuracyDeltaPercent)
                : Resolve(
                    sequence,
                    resolutionSeed,
                    exposure,
                    attack.AccuracyDecay,
                    distance,
                    targetWoundsBefore,
                    attack.DamageProfile,
                    attack.Contact,
                    context,
                    targetInjuryStateBefore,
                    attack.ActionId,
                    capabilityAccuracyDeltaPercent);
        }

        public static AttackResolutionRecord Resolve(
            long sequence,
            uint resolutionSeed,
            TargetExposureSnapshot exposure,
            AccuracyDecayDefinition accuracyDecay,
            float distance,
            ActorWoundSnapshot targetWoundsBefore,
            WeaponDamageProfileDefinition damageProfile,
            ContactAttackDefinition contact = null,
            IGameplayActionContext context = null,
            ActorInjuryState targetInjuryStateBefore = null,
            string weaponId = null,
            int capabilityAccuracyDeltaPercent = 0)
        {
            if (damageProfile == null) throw new ArgumentNullException(
                nameof(damageProfile));
            return ResolveInternal(
                sequence,
                resolutionSeed,
                exposure,
                accuracyDecay,
                distance,
                targetWoundsBefore,
                woundMovementPenalty: 0.01f,
                damageProfile,
                contact,
                context,
                targetInjuryStateBefore,
                weaponId,
                damageProfile.Mechanism,
                capabilityAccuracyDeltaPercent);
        }

        private static AttackResolutionRecord ResolveInternal(
            long sequence,
            uint resolutionSeed,
            TargetExposureSnapshot exposure,
            AccuracyDecayDefinition accuracyDecay,
            float distance,
            ActorWoundSnapshot targetWoundsBefore,
            float woundMovementPenalty,
            WeaponDamageProfileDefinition damageProfile,
            ContactAttackDefinition contact,
            IGameplayActionContext context,
            ActorInjuryState targetInjuryStateBefore,
            string weaponId,
            DamageMechanism? damageMechanism,
            int capabilityAccuracyDeltaPercent)
        {
            if (exposure == null)
            {
                throw new ArgumentNullException(nameof(exposure));
            }

            if (accuracyDecay == null)
            {
                throw new ArgumentNullException(nameof(accuracyDecay));
            }

            accuracyDecay.EvaluatePercent(distance);

            if (float.IsNaN(woundMovementPenalty)
                || float.IsInfinity(woundMovementPenalty)
                || woundMovementPenalty <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(woundMovementPenalty));
            }

            var rolls = new SeededAttackRolls(resolutionSeed);
            int hitRoll = rolls.Roll(100);
            int hitChance = AttackHitChanceRules.CalculateFinalHitChancePercent(
                exposure,
                accuracyDecay,
                distance,
                (context?.AccuracyDeltaPercent ?? 0)
                    + capabilityAccuracyDeltaPercent);
            if (hitRoll > hitChance)
            {
                return new AttackResolutionRecord(
                    sequence,
                    resolutionSeed,
                    exposure,
                    accuracyDecay,
                    distance,
                    targetWoundsBefore,
                    hitRoll,
                    regionRoll: 0,
                    hitRegion: null,
                    wound: null,
                    maximumReach: contact?.MaximumReach,
                    context: context,
                    targetInjuryStateBefore: targetInjuryStateBefore,
                    capabilityAccuracyDeltaPercent:
                        capabilityAccuracyDeltaPercent);
            }

            int regionRoll = rolls.Roll(exposure.VisibleSampleCount);
            TargetRegionId hitRegion = TargetExposureRules.SelectVisibleRegion(
                exposure,
                regionRoll);
            ActorInjuryState resolvedInjuries = targetInjuryStateBefore
                ?? LegacyWoundProjection.ToInjuryState(
                    targetWoundsBefore,
                    int.MaxValue);
            int severity = damageProfile == null
                ? ActorInjuryRules.CalculateImpactSeverity(
                    woundMovementPenalty,
                    accuracyDecay.EvaluatePercent(distance),
                    hitChance,
                    hitRoll,
                    TargetExposureRules.CalculateHitChancePercent(exposure))
                : damageProfile.ResolveTransferredImpact(distance);
            var impact = new LocalizedImpact(
                "impact:" + sequence + ":" + exposure.ObserverId
                    + ":" + exposure.TargetId,
                exposure.ObserverId,
                exposure.TargetId,
                string.IsNullOrWhiteSpace(weaponId)
                    ? "attack.legacy"
                    : weaponId,
                hitRegion,
                damageProfile?.Mechanism ?? damageMechanism ?? (contact == null
                    ? DamageMechanism.Ballistic
                    : DamageMechanism.Blunt),
                severity,
                sequence);
            ActorInjuryResolution injury = damageProfile == null
                ? ActorInjuryRules.ApplyImpact(
                    resolvedInjuries,
                    impact,
                    woundMovementPenalty)
                : ActorInjuryRules.ApplyImpact(
                    resolvedInjuries,
                    impact,
                    damageProfile);
            ActorWoundSnapshot resultingWounds =
                LegacyWoundProjection.From(injury.Resulting);
            var wound = new ActorWoundRecord(
                hitRegion,
                injury.Injury.CompatibilityMovementPenalty,
                targetWoundsBefore,
                resultingWounds);
            return new AttackResolutionRecord(
                sequence,
                resolutionSeed,
                exposure,
                accuracyDecay,
                distance,
                targetWoundsBefore,
                hitRoll,
                regionRoll,
                hitRegion,
                wound,
                contact?.MaximumReach,
                context,
                resolvedInjuries,
                injury.Delta,
                capabilityAccuracyDeltaPercent);
        }

        private sealed class SeededAttackRolls
        {
            private uint state;

            public SeededAttackRolls(uint seed)
            {
                state = seed != 0u ? seed : 0x6D2B79F5u;
            }

            public int Roll(int inclusiveMaximum)
            {
                if (inclusiveMaximum <= 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(inclusiveMaximum));
                }

                state ^= state << 13;
                state ^= state >> 17;
                state ^= state << 5;
                return (int)(state % (uint)inclusiveMaximum) + 1;
            }
        }
    }
}
