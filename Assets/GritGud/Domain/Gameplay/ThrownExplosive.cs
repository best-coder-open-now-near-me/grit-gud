using System;
using System.Collections.Generic;
using GritGud.Domain.Turns;

namespace GritGud.Domain.Gameplay
{
    public sealed class ThrownExplosiveRangeProjection
    {
        internal ThrownExplosiveRangeProjection(
            GameplayPosition origin,
            GameplayPosition requestedLanding,
            GameplayPosition intendedLanding,
            float maximumRange)
        {
            Origin = origin;
            RequestedLanding = requestedLanding;
            IntendedLanding = intendedLanding;
            MaximumRange = maximumRange;
            RequestedDistance = origin.DistanceTo(requestedLanding);
            IntendedDistance = origin.DistanceTo(intendedLanding);
        }

        public GameplayPosition Origin { get; }
        public GameplayPosition RequestedLanding { get; }
        public GameplayPosition IntendedLanding { get; }
        public float MaximumRange { get; }
        public float RequestedDistance { get; }
        public float IntendedDistance { get; }
        public bool WasClamped => RequestedDistance > MaximumRange;
    }

    public static class ThrownExplosiveRangeRules
    {
        public static ThrownExplosiveRangeProjection Project(
            GameplayPosition origin,
            GameplayPosition requestedLanding,
            float maximumRange)
        {
            if (float.IsNaN(maximumRange)
                || float.IsInfinity(maximumRange)
                || maximumRange <= 0f)
                throw new ArgumentOutOfRangeException(nameof(maximumRange));
            float requestedDistance = origin.DistanceTo(requestedLanding);
            GameplayPosition intended = requestedLanding;
            if (requestedDistance > maximumRange)
            {
                float scale = maximumRange / requestedDistance;
                intended = new GameplayPosition(
                    origin.X + ((requestedLanding.X - origin.X) * scale),
                    origin.Y + ((requestedLanding.Y - origin.Y) * scale),
                    origin.Z + ((requestedLanding.Z - origin.Z) * scale));
            }
            return new ThrownExplosiveRangeProjection(
                origin,
                requestedLanding,
                intended,
                maximumRange);
        }
    }

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
            FireFieldDefinition fireField = null,
            int blastActionPointReduction = 0)
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
                || blastActionPointReduction < 0
                || ((blastWoundMovementPenalty > 0f
                        || blastIntegrityDamage > 0f
                        || blastActionPointReduction > 0)
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
            BlastActionPointReduction = blastActionPointReduction;
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
        public int BlastActionPointReduction { get; }
        public bool IsConcussive => BlastActionPointReduction > 0;
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
            FireFieldRecord fireField = null,
            IEnumerable<ConcussiveActionPointEffectRecord>
                concussiveEffects = null,
            GameplayPosition? requestedLanding = null)
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
            RequestedLanding = requestedLanding ?? intendedLanding;
            ThrownExplosiveRangeProjection range =
                ThrownExplosiveRangeRules.Project(
                    origin,
                    RequestedLanding,
                    Definition.MaximumRange);
            if (range.IntendedLanding.DistanceTo(intendedLanding) > 0.0001f)
                throw new ArgumentException(
                    "Thrown explosive intended landing must be the canonical range projection of its request.",
                    nameof(intendedLanding));
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
            var concussive = new List<ConcussiveActionPointEffectRecord>(
                concussiveEffects
                    ?? Array.Empty<ConcussiveActionPointEffectRecord>());
            var actorIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (ConcussiveActionPointEffectRecord effect in concussive)
                if (effect == null || !actorIds.Add(effect.ActorId))
                    throw new ArgumentException(
                        "Concussive effects require unique non-null actors.",
                        nameof(concussiveEffects));
            concussive.Sort((left, right) => string.CompareOrdinal(
                left.ActorId,
                right.ActorId));
            ValidateConcussiveEffects(
                Definition,
                BlastEffects,
                concussive,
                nameof(concussiveEffects));
            ConcussiveEffects = concussive.AsReadOnly();
        }

        public long Sequence { get; }
        public string ThrowerId { get; }
        public ThrownExplosiveDefinition Definition { get; }
        public GameplayPosition Origin { get; }
        public GameplayPosition LaunchOrigin { get; }
        public GameplayPosition RequestedLanding { get; }
        public GameplayPosition IntendedLanding { get; }
        public GameplayPosition SampledLanding { get; }
        public GameplayPosition ResolvedLanding { get; }
        public float UncertaintyRadius { get; }
        public long WorldStateRevision { get; }
        public IReadOnlyList<BlastEffectRecord> BlastEffects { get; }
        public SmokeFieldRecord SmokeField { get; }
        public FireFieldRecord FireField { get; }
        public IReadOnlyList<ConcussiveActionPointEffectRecord>
            ConcussiveEffects { get; }

        public float RequestedDistance => Origin.DistanceTo(RequestedLanding);
        public float IntendedDistance => Origin.DistanceTo(IntendedLanding);

        private static void ValidateConcussiveEffects(
            ThrownExplosiveDefinition definition,
            IReadOnlyList<BlastEffectRecord> blastEffects,
            IReadOnlyList<ConcussiveActionPointEffectRecord> effects,
            string parameter)
        {
            var expected = new Dictionary<string, int>(StringComparer.Ordinal);
            if (definition.BlastActionPointReduction > 0)
                foreach (BlastEffectRecord blast in blastEffects)
                    if (blast.SubjectKind == BlastSubjectKind.Actor
                        && blast.Exposure > 0f)
                    {
                        if (!expected.TryAdd(
                                blast.EntityId,
                                ConcussiveActionPointRules.RequestedReduction(
                                    definition.BlastActionPointReduction,
                                    blast.Exposure)))
                            throw new ArgumentException(
                                "Concussive blast evidence repeats an actor.",
                                nameof(blastEffects));
                    }
            if (expected.Count != effects.Count)
                throw new ArgumentException(
                    "Concussive effects do not cover the exposed actor set.",
                    parameter);
            foreach (ConcussiveActionPointEffectRecord effect in effects)
                if (!expected.TryGetValue(effect.ActorId, out int requested)
                    || requested != effect.RequestedReduction)
                    throw new ArgumentException(
                        "Concussive AP reduction does not match blast exposure.",
                        parameter);
        }
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
