using System;
using System.Collections.Generic;
using GritGud.Domain.Turns;

namespace GritGud.Domain.Gameplay
{
    public sealed class ThrownExplosiveDefinition : ConsumablePowerDefinition
    {
        public const string TypeId = "thrown-explosive";

        public ThrownExplosiveDefinition(
            string id,
            ActionCost turnCost,
            float maximumRange,
            float standingLaunchHeight,
            float crouchedLaunchHeight,
            float baseUncertaintyRadius,
            float uncertaintyPerMeter,
            float blastRadius,
            float blastWoundMovementPenalty = 0f,
            float blastIntegrityDamage = 0f,
            SmokeFieldDefinition smokeField = null,
            FireFieldDefinition fireField = null)
            : base(id, turnCost)
        {
            if (!IsFinite(maximumRange)
                || !IsFinite(standingLaunchHeight)
                || !IsFinite(crouchedLaunchHeight)
                || !IsFinite(baseUncertaintyRadius)
                || !IsFinite(uncertaintyPerMeter) || !IsFinite(blastRadius)
                || !IsFinite(blastWoundMovementPenalty)
                || !IsFinite(blastIntegrityDamage)
                || maximumRange <= 0f
                || standingLaunchHeight < 0f
                || crouchedLaunchHeight < 0f
                || baseUncertaintyRadius < 0f
                || uncertaintyPerMeter < 0f || blastRadius < 0f
                || blastWoundMovementPenalty < 0f
                || blastIntegrityDamage < 0f
                || ((blastWoundMovementPenalty > 0f
                        || blastIntegrityDamage > 0f)
                    != (blastRadius > 0f))
                || CountPayloads(blastRadius, smokeField, fireField) != 1)
                throw new ArgumentOutOfRangeException(nameof(maximumRange));
            MaximumRange = maximumRange;
            StandingLaunchHeight = standingLaunchHeight;
            CrouchedLaunchHeight = crouchedLaunchHeight;
            BaseUncertaintyRadius = baseUncertaintyRadius;
            UncertaintyPerMeter = uncertaintyPerMeter;
            BlastRadius = blastRadius;
            BlastWoundMovementPenalty = blastWoundMovementPenalty;
            BlastIntegrityDamage = blastIntegrityDamage;
            SmokeField = smokeField;
            FireField = fireField;
        }

        public override string PowerTypeId => TypeId;

        public float MaximumRange { get; }
        public float StandingLaunchHeight { get; }
        public float CrouchedLaunchHeight { get; }
        public float BaseUncertaintyRadius { get; }
        public float UncertaintyPerMeter { get; }
        public float BlastRadius { get; }
        public float BlastWoundMovementPenalty { get; }
        public float BlastIntegrityDamage { get; }
        public SmokeFieldDefinition SmokeField { get; }
        public FireFieldDefinition FireField { get; }
        public bool DeploysSmoke => SmokeField != null;
        public bool DeploysFire => FireField != null;
        public float AreaRadius => SmokeField?.Radius
            ?? FireField?.MaximumRadius
            ?? BlastRadius;

        public GameplayPosition GetLaunchOrigin(GameplayActorPose pose)
        {
            float height = pose.Stance == ActorStance.Crouched
                ? CrouchedLaunchHeight
                : StandingLaunchHeight;
            return new GameplayPosition(
                pose.Position.X,
                pose.Position.Y + height,
                pose.Position.Z);
        }

        public float GetUncertaintyRadius(float distance)
        {
            if (!IsFinite(distance) || distance < 0f)
                throw new ArgumentOutOfRangeException(nameof(distance));
            return BaseUncertaintyRadius + (distance * UncertaintyPerMeter);
        }

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);

        private static int CountPayloads(
            float blastRadius,
            SmokeFieldDefinition smokeField,
            FireFieldDefinition fireField) =>
            (blastRadius > 0f ? 1 : 0)
            + (smokeField != null ? 1 : 0)
            + (fireField != null ? 1 : 0);
    }

    public sealed class ThrownExplosiveRecord
    {
        public ThrownExplosiveRecord(
            long sequence,
            string throwerId,
            ThrownExplosiveDefinition definition,
            GameplayPosition origin,
            GameplayPosition launchOrigin,
            GameplayPosition intendedLanding,
            GameplayPosition sampledLanding,
            GameplayPosition resolvedLanding,
            float uncertaintyRadius,
            long worldStateRevision,
            IEnumerable<BlastEffectRecord> blastEffects,
            SmokeFieldRecord smokeField = null,
            FireFieldRecord fireField = null)
        {
            if (sequence <= 0) throw new ArgumentOutOfRangeException(nameof(sequence));
            if (string.IsNullOrWhiteSpace(throwerId)) throw new ArgumentException("Throws require an actor.", nameof(throwerId));
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            if (float.IsNaN(uncertaintyRadius) || float.IsInfinity(uncertaintyRadius)
                || uncertaintyRadius < 0f)
                throw new ArgumentOutOfRangeException(nameof(uncertaintyRadius));
            if (worldStateRevision < 0L) throw new ArgumentOutOfRangeException(nameof(worldStateRevision));
            if (blastEffects == null) throw new ArgumentNullException(nameof(blastEffects));
            Sequence = sequence;
            ThrowerId = throwerId;
            Origin = origin;
            LaunchOrigin = launchOrigin;
            IntendedLanding = intendedLanding;
            SampledLanding = sampledLanding;
            ResolvedLanding = resolvedLanding;
            UncertaintyRadius = uncertaintyRadius;
            WorldStateRevision = worldStateRevision;
            BlastEffects = new List<BlastEffectRecord>(blastEffects).AsReadOnly();
            if ((Definition.SmokeField == null) != (smokeField == null))
                throw new ArgumentException(
                    "Thrown smoke payloads require their matching field record.",
                    nameof(smokeField));
            if (smokeField != null
                && (!string.Equals(
                        smokeField.SourceActorId,
                        throwerId,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        smokeField.SourceItemId,
                        Definition.Id,
                        StringComparison.Ordinal)
                    || smokeField.Origin.DistanceTo(resolvedLanding) > 0f
                    || !Definition.SmokeField.Matches(
                        smokeField.Definition)))
                throw new ArgumentException(
                    "The smoke field does not match its thrown payload.",
                    nameof(smokeField));
            SmokeField = smokeField;
            if ((Definition.FireField == null) != (fireField == null))
                throw new ArgumentException(
                    "Incendiary payloads require their matching fire field record.",
                    nameof(fireField));
            if (fireField != null
                && (!string.Equals(
                        fireField.SourceActorId,
                        throwerId,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        fireField.SourceItemId,
                        Definition.Id,
                        StringComparison.Ordinal)
                    || fireField.Origin.DistanceTo(resolvedLanding) > 0f
                    || !Definition.FireField.Matches(
                        fireField.Definition)))
            {
                throw new ArgumentException(
                    "The fire field does not match its thrown payload.",
                    nameof(fireField));
            }
            FireField = fireField;
        }

        public long Sequence { get; }
        public string ThrowerId { get; }
        public ThrownExplosiveDefinition Definition { get; }
        public GameplayPosition Origin { get; }
        public GameplayPosition LaunchOrigin { get; }
        public GameplayPosition IntendedLanding { get; }
        public GameplayPosition SampledLanding { get; }
        public GameplayPosition ResolvedLanding { get; }
        public float UncertaintyRadius { get; }
        public long WorldStateRevision { get; }
        public IReadOnlyList<BlastEffectRecord> BlastEffects { get; }
        public SmokeFieldRecord SmokeField { get; }
        public FireFieldRecord FireField { get; }
    }

    public sealed class ThrownExplosiveActionOutcome : GameplayActionOutcome
    {
        public ThrownExplosiveActionOutcome(ThrownExplosiveRecord record)
            : base((record ?? throw new ArgumentNullException(nameof(record))).Definition.Id)
        {
            Record = record;
        }
        public ThrownExplosiveRecord Record { get; }
    }
}
