using System;
using System.Collections.Generic;

namespace GritGud.Domain.Gameplay
{
    public enum DamageMechanism
    {
        Ballistic = 0,
        Blast = 1,
        Thermal = 2,
        Blunt = 3,
        Penetrating = 4,
    }

    public enum ActorLifeState
    {
        Active = 0,
        Incapacitated = 1,
        Dead = 2,
    }

    public sealed class LocalizedImpact
    {
        public LocalizedImpact(
            string combatEventId,
            string sourceActorId,
            string targetActorId,
            string weaponId,
            TargetRegionId? region,
            DamageMechanism mechanism,
            int severity,
            long sequence)
        {
            CombatEventId = RequireText(combatEventId, nameof(combatEventId));
            SourceActorId = RequireText(sourceActorId, nameof(sourceActorId));
            TargetActorId = RequireText(targetActorId, nameof(targetActorId));
            WeaponId = RequireText(weaponId, nameof(weaponId));
            if (region.HasValue
                && !Enum.IsDefined(typeof(TargetRegionId), region.Value))
                throw new ArgumentOutOfRangeException(nameof(region));
            if (!Enum.IsDefined(typeof(DamageMechanism), mechanism))
                throw new ArgumentOutOfRangeException(nameof(mechanism));
            if (severity < 1 || severity > 100)
                throw new ArgumentOutOfRangeException(nameof(severity));
            if (sequence <= 0L)
                throw new ArgumentOutOfRangeException(nameof(sequence));
            Region = region;
            Mechanism = mechanism;
            Severity = severity;
            Sequence = sequence;
        }

        public string CombatEventId { get; }
        public string SourceActorId { get; }
        public string TargetActorId { get; }
        public string WeaponId { get; }
        public TargetRegionId? Region { get; }
        public DamageMechanism Mechanism { get; }
        public int Severity { get; }
        public long Sequence { get; }

        private static string RequireText(string value, string parameter)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException(
                    "Localized impact identifiers cannot be empty.",
                    parameter);
            return value.Trim();
        }
    }

    public sealed class InjuryRecord
    {
        public InjuryRecord(
            string injuryId,
            string combatEventId,
            TargetRegionId? region,
            DamageMechanism mechanism,
            int severity,
            int structuralDamage,
            int motorLoss,
            int sensoryLoss,
            int bleedRate,
            bool vitalDamage,
            float compatibilityMovementPenalty,
            int systemicTraumaContribution = 0)
        {
            InjuryId = RequireText(injuryId, nameof(injuryId));
            CombatEventId = RequireText(combatEventId, nameof(combatEventId));
            if (region.HasValue
                && !Enum.IsDefined(typeof(TargetRegionId), region.Value))
                throw new ArgumentOutOfRangeException(nameof(region));
            if (!Enum.IsDefined(typeof(DamageMechanism), mechanism))
                throw new ArgumentOutOfRangeException(nameof(mechanism));
            RequirePercent(severity, nameof(severity), minimum: 1);
            RequirePercent(structuralDamage, nameof(structuralDamage));
            RequirePercent(motorLoss, nameof(motorLoss));
            RequirePercent(sensoryLoss, nameof(sensoryLoss));
            if (bleedRate < 0 || bleedRate > 100)
                throw new ArgumentOutOfRangeException(nameof(bleedRate));
            if (float.IsNaN(compatibilityMovementPenalty)
                || float.IsInfinity(compatibilityMovementPenalty)
                || compatibilityMovementPenalty < 0f)
                throw new ArgumentOutOfRangeException(
                    nameof(compatibilityMovementPenalty));
            if (systemicTraumaContribution < 0
                || systemicTraumaContribution > 100)
                throw new ArgumentOutOfRangeException(
                    nameof(systemicTraumaContribution));
            Region = region;
            Mechanism = mechanism;
            Severity = severity;
            StructuralDamage = structuralDamage;
            MotorLoss = motorLoss;
            SensoryLoss = sensoryLoss;
            BleedRate = bleedRate;
            VitalDamage = vitalDamage;
            CompatibilityMovementPenalty = compatibilityMovementPenalty;
            SystemicTraumaContribution = systemicTraumaContribution;
        }

        public string InjuryId { get; }
        public string CombatEventId { get; }
        public TargetRegionId? Region { get; }
        public DamageMechanism Mechanism { get; }
        public int Severity { get; }
        public int StructuralDamage { get; }
        public int MotorLoss { get; }
        public int SensoryLoss { get; }
        public int BleedRate { get; }
        public bool VitalDamage { get; }

        public int SystemicTraumaContribution { get; }

        // Transitional metadata used only to reproduce the old UI counters.
        // Capability and life-state rules never consume this value.
        public float CompatibilityMovementPenalty { get; }

        private static string RequireText(string value, string parameter)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException(
                    "Injury identifiers cannot be empty.",
                    parameter);
            return value.Trim();
        }

        private static void RequirePercent(
            int value,
            string parameter,
            int minimum = 0)
        {
            if (value < minimum || value > 100)
                throw new ArgumentOutOfRangeException(parameter);
        }
    }

    public sealed class BodyRegionCondition
    {
        internal BodyRegionCondition(
            TargetRegionId region,
            IEnumerable<InjuryRecord> injuries)
        {
            if (!Enum.IsDefined(typeof(TargetRegionId), region))
                throw new ArgumentOutOfRangeException(nameof(region));
            Region = region;
            var copy = new List<InjuryRecord>();
            int structuralDamage = 0;
            int motorLoss = 0;
            int sensoryLoss = 0;
            int bleedRate = 0;
            foreach (InjuryRecord injury in injuries
                ?? throw new ArgumentNullException(nameof(injuries)))
            {
                if (injury == null || injury.Region != region)
                    throw new ArgumentException(
                        "Body-region injuries must match their region.",
                        nameof(injuries));
                copy.Add(injury);
                structuralDamage = SaturatingAdd(
                    structuralDamage,
                    injury.StructuralDamage);
                motorLoss = SaturatingAdd(motorLoss, injury.MotorLoss);
                sensoryLoss = SaturatingAdd(sensoryLoss, injury.SensoryLoss);
                bleedRate = SaturatingAdd(bleedRate, injury.BleedRate);
            }
            StructuralIntegrity = 100 - structuralDamage;
            MotorFunction = 100 - motorLoss;
            SensoryFunction = 100 - sensoryLoss;
            BleedRate = bleedRate;
            Injuries = copy.AsReadOnly();
        }

        public TargetRegionId Region { get; }
        public int StructuralIntegrity { get; }
        public int MotorFunction { get; }
        public int SensoryFunction { get; }
        public int BleedRate { get; }
        public IReadOnlyList<InjuryRecord> Injuries { get; }

        private static int SaturatingAdd(int left, int right) =>
            Math.Min(100, checked(left + right));
    }

    public sealed class ActorCapabilityState
    {
        public ActorCapabilityState(
            int movementCapacity,
            int standingCapacity,
            int aimStability,
            int gripCapacity,
            int reloadCapacity,
            int throwCapacity,
            bool canStand,
            bool canUseLeftHand,
            bool canUseRightHand,
            bool canUseTwoHandedWeapon)
            : this(
                movementCapacity,
                standingCapacity,
                aimStability,
                gripCapacity,
                reloadCapacity,
                throwCapacity,
                canStand,
                canUseLeftHand,
                canUseRightHand,
                canUseTwoHandedWeapon,
                ActorMobilityCapability.CreateLegacy(
                    movementCapacity,
                    standingCapacity,
                    canStand),
                canUseLeftHand ? gripCapacity : 0,
                canUseRightHand ? gripCapacity : 0,
                canUseLeftHand ? throwCapacity : 0,
                canUseRightHand ? throwCapacity : 0,
                canStand || canUseLeftHand || canUseRightHand
                    || canUseTwoHandedWeapon)
        {
        }

        public ActorCapabilityState(
            int movementCapacity,
            int standingCapacity,
            int aimStability,
            int gripCapacity,
            int reloadCapacity,
            int throwCapacity,
            bool canStand,
            bool canUseLeftHand,
            bool canUseRightHand,
            bool canUseTwoHandedWeapon,
            ActorMobilityCapability mobility,
            int leftGripCapacity,
            int rightGripCapacity,
            int leftThrowCapacity,
            int rightThrowCapacity,
            bool isActive)
        {
            MovementCapacity = RequirePercent(
                movementCapacity,
                nameof(movementCapacity));
            StandingCapacity = RequirePercent(
                standingCapacity,
                nameof(standingCapacity));
            AimStability = RequirePercent(aimStability, nameof(aimStability));
            GripCapacity = RequirePercent(gripCapacity, nameof(gripCapacity));
            ReloadCapacity = RequirePercent(
                reloadCapacity,
                nameof(reloadCapacity));
            ThrowCapacity = RequirePercent(throwCapacity, nameof(throwCapacity));
            CanStand = canStand;
            CanUseLeftHand = canUseLeftHand;
            CanUseRightHand = canUseRightHand;
            CanUseTwoHandedWeapon = canUseTwoHandedWeapon;
            Mobility = mobility ?? throw new ArgumentNullException(
                nameof(mobility));
            if (Mobility.MovementPercent != MovementCapacity
                || Mobility.StandingPercent != StandingCapacity
                || Mobility.CanStand != CanStand)
                throw new ArgumentException(
                    "Aggregate movement capability must match its gait projection.",
                    nameof(mobility));
            LeftGripCapacity = RequirePercent(
                leftGripCapacity,
                nameof(leftGripCapacity));
            RightGripCapacity = RequirePercent(
                rightGripCapacity,
                nameof(rightGripCapacity));
            LeftThrowCapacity = RequirePercent(
                leftThrowCapacity,
                nameof(leftThrowCapacity));
            RightThrowCapacity = RequirePercent(
                rightThrowCapacity,
                nameof(rightThrowCapacity));
            IsActive = isActive;
            if (!IsActive
                && (CanStand || CanUseLeftHand || CanUseRightHand
                    || CanUseTwoHandedWeapon))
                throw new ArgumentException(
                    "Inactive actors cannot retain active action capabilities.",
                    nameof(isActive));
        }

        public int MovementCapacity { get; }
        public int StandingCapacity { get; }
        public int AimStability { get; }
        public int GripCapacity { get; }
        public int ReloadCapacity { get; }
        public int ThrowCapacity { get; }
        public bool CanStand { get; }
        public bool CanUseLeftHand { get; }
        public bool CanUseRightHand { get; }
        public bool CanUseTwoHandedWeapon { get; }
        public ActorMobilityCapability Mobility { get; }
        public int LeftGripCapacity { get; }
        public int RightGripCapacity { get; }
        public int LeftThrowCapacity { get; }
        public int RightThrowCapacity { get; }
        public bool IsActive { get; }

        private static int RequirePercent(int value, string parameter)
        {
            if (value < 0 || value > 100)
                throw new ArgumentOutOfRangeException(parameter);
            return value;
        }
    }

    public enum ActorImpairedSide
    {
        None = 0,
        Left = 1,
        Right = 2,
    }

    public enum ActorGait
    {
        Normal = 0,
        MildLimp = 1,
        SevereLimp = 2,
        Crawling = 3,
        Immobile = 4,
    }

    public sealed class ActorMobilityCapability
    {
        public ActorMobilityCapability(
            ActorGait gait,
            ActorImpairedSide impairedSide,
            int movementPercent,
            int standingPercent,
            bool canSprint,
            bool canStand)
        {
            if (!Enum.IsDefined(typeof(ActorGait), gait))
                throw new ArgumentOutOfRangeException(nameof(gait));
            if (!Enum.IsDefined(typeof(ActorImpairedSide), impairedSide))
                throw new ArgumentOutOfRangeException(nameof(impairedSide));
            if (gait == ActorGait.Normal
                && impairedSide != ActorImpairedSide.None)
                throw new ArgumentException(
                    "Normal gait cannot identify an impaired side.",
                    nameof(impairedSide));
            if ((gait == ActorGait.Crawling || gait == ActorGait.Immobile)
                && canStand)
                throw new ArgumentException(
                    "Crawling and immobile actors cannot stand.",
                    nameof(canStand));
            if (canSprint && (!canStand || gait != ActorGait.Normal))
                throw new ArgumentException(
                    "Only actors with normal standing gait can sprint.",
                    nameof(canSprint));
            Gait = gait;
            ImpairedSide = impairedSide;
            MovementPercent = RequirePercent(
                movementPercent,
                nameof(movementPercent));
            StandingPercent = RequirePercent(
                standingPercent,
                nameof(standingPercent));
            CanSprint = canSprint;
            CanStand = canStand;
        }

        public ActorGait Gait { get; }
        public ActorImpairedSide ImpairedSide { get; }
        public int MovementPercent { get; }
        public int StandingPercent { get; }
        public bool CanSprint { get; }
        public bool CanStand { get; }

        internal static ActorMobilityCapability CreateLegacy(
            int movementPercent,
            int standingPercent,
            bool canStand)
        {
            bool resolvedCanStand = movementPercent > 0 && canStand;
            return new ActorMobilityCapability(
                movementPercent <= 0
                    ? ActorGait.Immobile
                    : resolvedCanStand
                        ? ActorGait.Normal
                        : ActorGait.Crawling,
                ActorImpairedSide.None,
                movementPercent,
                standingPercent,
                canSprint: resolvedCanStand,
                canStand: resolvedCanStand);
        }

        private static int RequirePercent(int value, string parameter)
        {
            if (value < 0 || value > 100)
                throw new ArgumentOutOfRangeException(parameter);
            return value;
        }
    }

    public sealed class ActorPhysiologyState
    {
        public ActorPhysiologyState(
            int bloodReserve,
            int shock,
            int consciousness,
            int respiration)
        {
            BloodReserve = RequirePercent(bloodReserve, nameof(bloodReserve));
            Shock = RequirePercent(shock, nameof(shock));
            Consciousness = RequirePercent(
                consciousness,
                nameof(consciousness));
            Respiration = RequirePercent(respiration, nameof(respiration));
        }

        public int BloodReserve { get; }
        public int Shock { get; }
        public int Consciousness { get; }
        public int Respiration { get; }

        public static ActorPhysiologyState Healthy =>
            new ActorPhysiologyState(100, 0, 100, 100);

        private static int RequirePercent(int value, string parameter)
        {
            if (value < 0 || value > 100)
                throw new ArgumentOutOfRangeException(parameter);
            return value;
        }
    }

    public sealed class ActorInjuryState
    {
        private readonly IReadOnlyList<InjuryRecord> injuries;
        private readonly IReadOnlyDictionary<TargetRegionId, BodyRegionCondition>
            regions;

        public ActorInjuryState(
            string actorId,
            IEnumerable<InjuryRecord> injuries,
            ActorPhysiologyState physiology,
            ActorLifeState lifeState)
        {
            if (string.IsNullOrWhiteSpace(actorId))
                throw new ArgumentException(
                    "Injury state requires an actor identifier.",
                    nameof(actorId));
            if (!Enum.IsDefined(typeof(ActorLifeState), lifeState))
                throw new ArgumentOutOfRangeException(nameof(lifeState));
            ActorId = actorId;
            Physiology = physiology ?? throw new ArgumentNullException(
                nameof(physiology));
            LifeState = lifeState;
            var copied = new List<InjuryRecord>();
            var identities = new HashSet<string>(StringComparer.Ordinal);
            foreach (InjuryRecord injury in injuries
                ?? throw new ArgumentNullException(nameof(injuries)))
            {
                if (injury == null || !identities.Add(injury.InjuryId))
                    throw new ArgumentException(
                        "Actor injuries must be non-null and uniquely identified.",
                        nameof(injuries));
                copied.Add(injury);
            }
            this.injuries = copied.AsReadOnly();
            int systemicTrauma = 0;
            foreach (InjuryRecord injury in copied)
                systemicTrauma = checked(
                    systemicTrauma + injury.SystemicTraumaContribution);
            SystemicTrauma = systemicTrauma;
            var indexed = new Dictionary<TargetRegionId, BodyRegionCondition>();
            foreach (TargetRegionId region in Enum.GetValues(
                typeof(TargetRegionId)))
            {
                var regional = new List<InjuryRecord>();
                foreach (InjuryRecord injury in copied)
                    if (injury.Region == region)
                        regional.Add(injury);
                indexed.Add(region, new BodyRegionCondition(region, regional));
            }
            regions = indexed;
            Capabilities = ActorCapabilityRules.Project(this);
        }

        public string ActorId { get; }
        public IReadOnlyList<InjuryRecord> Injuries => injuries;
        public ActorPhysiologyState Physiology { get; }
        public ActorLifeState LifeState { get; }
        public ActorCapabilityState Capabilities { get; }
        public int SystemicTrauma { get; }

        public BodyRegionCondition GetRegion(TargetRegionId region)
        {
            if (!regions.TryGetValue(region, out BodyRegionCondition value))
                throw new ArgumentOutOfRangeException(nameof(region));
            return value;
        }

        public bool HasSameState(ActorInjuryState other)
        {
            if (other == null
                || !string.Equals(ActorId, other.ActorId,
                    StringComparison.Ordinal)
                || LifeState != other.LifeState
                || Physiology.BloodReserve != other.Physiology.BloodReserve
                || Physiology.Shock != other.Physiology.Shock
                || Physiology.Consciousness != other.Physiology.Consciousness
                || Physiology.Respiration != other.Physiology.Respiration
                || Injuries.Count != other.Injuries.Count)
                return false;
            for (int index = 0; index < Injuries.Count; index++)
            {
                InjuryRecord left = Injuries[index];
                InjuryRecord right = other.Injuries[index];
                if (!string.Equals(left.InjuryId, right.InjuryId,
                        StringComparison.Ordinal)
                    || !string.Equals(left.CombatEventId, right.CombatEventId,
                        StringComparison.Ordinal)
                    || left.Region != right.Region
                    || left.Mechanism != right.Mechanism
                    || left.Severity != right.Severity
                    || left.StructuralDamage != right.StructuralDamage
                    || left.MotorLoss != right.MotorLoss
                    || left.SensoryLoss != right.SensoryLoss
                    || left.BleedRate != right.BleedRate
                    || left.VitalDamage != right.VitalDamage
                    || left.SystemicTraumaContribution
                        != right.SystemicTraumaContribution
                    || left.CompatibilityMovementPenalty
                        != right.CompatibilityMovementPenalty)
                    return false;
            }
            return true;
        }

        public static ActorInjuryState CreateHealthy(string actorId) =>
            new ActorInjuryState(
                actorId,
                Array.Empty<InjuryRecord>(),
                ActorPhysiologyState.Healthy,
                ActorLifeState.Active);
    }

    public sealed class ActorInjuryDelta
    {
        public ActorInjuryDelta(
            LocalizedImpact impact,
            InjuryRecord injury,
            ActorPhysiologyState previousPhysiology,
            ActorPhysiologyState resultingPhysiology,
            ActorLifeState previousLifeState,
            ActorLifeState resultingLifeState,
            int previousSystemicTrauma = 0,
            int resultingSystemicTrauma = 0)
        {
            Impact = impact ?? throw new ArgumentNullException(nameof(impact));
            Injury = injury ?? throw new ArgumentNullException(nameof(injury));
            PreviousPhysiology = previousPhysiology
                ?? throw new ArgumentNullException(nameof(previousPhysiology));
            ResultingPhysiology = resultingPhysiology
                ?? throw new ArgumentNullException(nameof(resultingPhysiology));
            if (!Enum.IsDefined(typeof(ActorLifeState), previousLifeState))
                throw new ArgumentOutOfRangeException(nameof(previousLifeState));
            if (!Enum.IsDefined(typeof(ActorLifeState), resultingLifeState))
                throw new ArgumentOutOfRangeException(nameof(resultingLifeState));
            if (previousSystemicTrauma < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(previousSystemicTrauma));
            if (resultingSystemicTrauma < previousSystemicTrauma)
                throw new ArgumentOutOfRangeException(
                    nameof(resultingSystemicTrauma));
            if (!string.Equals(
                    impact.CombatEventId,
                    injury.CombatEventId,
                    StringComparison.Ordinal)
                || injury.Region != impact.Region)
                throw new ArgumentException(
                    "Injury deltas must preserve impact identity.");
            PreviousLifeState = previousLifeState;
            ResultingLifeState = resultingLifeState;
            PreviousSystemicTrauma = previousSystemicTrauma;
            ResultingSystemicTrauma = resultingSystemicTrauma;
        }

        public LocalizedImpact Impact { get; }
        public InjuryRecord Injury { get; }
        public ActorPhysiologyState PreviousPhysiology { get; }
        public ActorPhysiologyState ResultingPhysiology { get; }
        public ActorLifeState PreviousLifeState { get; }
        public ActorLifeState ResultingLifeState { get; }
        public int PreviousSystemicTrauma { get; }
        public int ResultingSystemicTrauma { get; }
    }

    public sealed class ActorInjuryResolution
    {
        public ActorInjuryResolution(
            ActorInjuryDelta delta,
            ActorInjuryState resulting)
        {
            Delta = delta ?? throw new ArgumentNullException(nameof(delta));
            Resulting = resulting ?? throw new ArgumentNullException(
                nameof(resulting));
        }

        public ActorInjuryDelta Delta { get; }
        public ActorInjuryState Resulting { get; }
        public InjuryRecord Injury => Delta.Injury;
    }

    public static class ActorCapabilityRules
    {
        public static ActorCapabilityState Project(ActorInjuryState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            int leftArm = state.GetRegion(TargetRegionId.LeftArm).MotorFunction;
            int rightArm = state.GetRegion(TargetRegionId.RightArm).MotorFunction;
            int leftLeg = state.GetRegion(TargetRegionId.LeftLeg).MotorFunction;
            int rightLeg = state.GetRegion(TargetRegionId.RightLeg).MotorFunction;
            int headSense = state.GetRegion(TargetRegionId.Head).SensoryFunction;
            int torso = state.GetRegion(TargetRegionId.Torso).StructuralIntegrity;
            int systemic = Math.Min(
                state.Physiology.Consciousness,
                Math.Min(state.Physiology.BloodReserve,
                    state.Physiology.Respiration));
            bool active = state.LifeState == ActorLifeState.Active;
            ActorMobilityCapability mobility = ProjectMobility(
                leftLeg,
                rightLeg,
                systemic,
                active);
            int aim = Scale((headSense + leftArm + rightArm + torso) / 4,
                systemic);
            int leftGrip = Scale(leftArm, systemic);
            int rightGrip = Scale(rightArm, systemic);
            int grip = (leftGrip + rightGrip) / 2;
            int reload = Scale(Math.Min(leftArm, rightArm), systemic);
            int leftThrow = Scale(leftArm, systemic);
            int rightThrow = Scale(rightArm, systemic);
            int throwing = Math.Max(leftThrow, rightThrow);
            bool leftHand = active && leftGrip >= 30;
            bool rightHand = active && rightGrip >= 30;
            return new ActorCapabilityState(
                mobility.MovementPercent,
                mobility.StandingPercent,
                aim,
                grip,
                reload,
                throwing,
                mobility.CanStand,
                leftHand,
                rightHand,
                leftHand && rightHand && aim >= 20,
                mobility,
                leftGrip,
                rightGrip,
                leftThrow,
                rightThrow,
                active);
        }

        private static ActorMobilityCapability ProjectMobility(
            int leftLeg,
            int rightLeg,
            int systemic,
            bool active)
        {
            int weaker = Math.Min(leftLeg, rightLeg);
            ActorImpairedSide side = leftLeg == rightLeg
                ? ActorImpairedSide.None
                : leftLeg < rightLeg
                    ? ActorImpairedSide.Left
                    : ActorImpairedSide.Right;
            ActorGait gait;
            int regionalMovement;
            int regionalStanding;
            if (leftLeg < 10 && rightLeg < 10)
            {
                gait = ActorGait.Immobile;
                regionalMovement = 0;
                regionalStanding = 0;
            }
            else if (leftLeg < 40 && rightLeg < 40)
            {
                gait = ActorGait.Crawling;
                regionalMovement = 15;
                regionalStanding = 0;
            }
            else if (weaker < 40)
            {
                gait = ActorGait.SevereLimp;
                regionalMovement = weaker < 15 ? 35 : 45;
                regionalStanding = 40;
            }
            else if (weaker < 70)
            {
                gait = ActorGait.MildLimp;
                regionalMovement = 75;
                regionalStanding = 75;
            }
            else
            {
                gait = ActorGait.Normal;
                side = ActorImpairedSide.None;
                regionalMovement = 100;
                regionalStanding = 100;
            }

            int movement = active
                ? Scale(regionalMovement, systemic)
                : 0;
            int standing = active
                ? Scale(regionalStanding, systemic)
                : 0;
            bool canStand = active
                && gait != ActorGait.Crawling
                && gait != ActorGait.Immobile
                && standing >= 25;
            return new ActorMobilityCapability(
                gait,
                side,
                movement,
                standing,
                canSprint: active
                    && gait == ActorGait.Normal
                    && systemic >= 70,
                canStand: canStand);
        }

        private static int Scale(int regional, int systemic) =>
            Math.Max(0, Math.Min(100,
                (regional * systemic + 50) / 100));
    }

    public static class ActorInjuryRules
    {
        public static int CalculateImpactSeverity(
            float weaponPayload,
            float rangeFactorPercent,
            int hitChancePercent,
            int hitRoll,
            int exposedAreaPercent,
            int armorTransferPercent = 100)
        {
            if (float.IsNaN(weaponPayload)
                || float.IsInfinity(weaponPayload)
                || weaponPayload <= 0f)
                throw new ArgumentOutOfRangeException(nameof(weaponPayload));
            int payload = Clamp(
                (int)Math.Round(
                    weaponPayload * 25f,
                    MidpointRounding.AwayFromZero),
                20,
                100);
            int range = Clamp(
                (int)Math.Round(
                    rangeFactorPercent,
                    MidpointRounding.AwayFromZero),
                25,
                100);
            int exposure = Clamp(exposedAreaPercent, 35, 100);
            int quality = Clamp(
                70 + Math.Max(0, hitChancePercent - hitRoll) / 2,
                60,
                100);
            int armor = Clamp(armorTransferPercent, 0, 100);
            long product = (long)payload * range * exposure * quality * armor;
            return Clamp(
                (int)((product + 50000000L) / 100000000L),
                1,
                100);
        }

        public static ActorInjuryResolution ApplyImpact(
            ActorInjuryState previous,
            LocalizedImpact impact,
            float compatibilityMovementPenalty)
        {
            if (previous == null) throw new ArgumentNullException(nameof(previous));
            if (impact == null) throw new ArgumentNullException(nameof(impact));
            if (!string.Equals(
                    previous.ActorId,
                    impact.TargetActorId,
                    StringComparison.Ordinal))
                throw new ArgumentException(
                    "Impact target does not match injury state.",
                    nameof(impact));
            int severity = impact.Severity;
            int structural = severity;
            int motor = ResolveMotorLoss(impact.Region, severity);
            int sensory = ResolveSensoryLoss(impact.Region, severity);
            int bleed = ResolveBleedRate(impact.Region, severity);
            bool vital = (impact.Region == TargetRegionId.Head && severity >= 85)
                || (impact.Region == TargetRegionId.Torso && severity >= 95);
            var injury = new InjuryRecord(
                impact.CombatEventId + ":injury",
                impact.CombatEventId,
                impact.Region,
                impact.Mechanism,
                severity,
                structural,
                motor,
                sensory,
                bleed,
                vital,
                compatibilityMovementPenalty);
            var injuries = new List<InjuryRecord>(previous.Injuries)
            {
                injury,
            };
            int blood = Clamp(
                previous.Physiology.BloodReserve - bleed / 3,
                0,
                100);
            int shock = Clamp(
                previous.Physiology.Shock
                    + (severity / 3)
                    + (vital ? 20 : 0),
                0,
                100);
            int consciousnessLoss = impact.Region == TargetRegionId.Head
                ? severity * 3 / 4
                : vital ? severity / 3 : 0;
            int consciousness = Clamp(
                previous.Physiology.Consciousness - consciousnessLoss,
                0,
                100);
            int respiration = Clamp(
                previous.Physiology.Respiration
                    - (impact.Region == TargetRegionId.Torso
                        ? severity * 3 / 4
                        : 0),
                0,
                100);
            var physiology = new ActorPhysiologyState(
                blood,
                shock,
                consciousness,
                respiration);
            var provisional = new ActorInjuryState(
                previous.ActorId,
                injuries,
                physiology,
                ActorLifeState.Active);
            ActorLifeState life = DeriveLifeState(
                provisional,
                impact.Region,
                severity,
                vital);
            var resulting = new ActorInjuryState(
                previous.ActorId,
                injuries,
                physiology,
                life);
            var delta = new ActorInjuryDelta(
                impact,
                injury,
                previous.Physiology,
                physiology,
                previous.LifeState,
                life,
                previous.SystemicTrauma,
                resulting.SystemicTrauma);
            return new ActorInjuryResolution(delta, resulting);
        }

        public static ActorInjuryResolution ApplyImpact(
            ActorInjuryState previous,
            LocalizedImpact impact,
            WeaponDamageProfileDefinition damageProfile)
        {
            if (previous == null) throw new ArgumentNullException(nameof(previous));
            if (impact == null) throw new ArgumentNullException(nameof(impact));
            if (damageProfile == null) throw new ArgumentNullException(
                nameof(damageProfile));
            if (!string.Equals(
                    previous.ActorId,
                    impact.TargetActorId,
                    StringComparison.Ordinal))
                throw new ArgumentException(
                    "Impact target does not match injury state.",
                    nameof(impact));
            if (!impact.Region.HasValue)
                throw new ArgumentException(
                    "Weapon damage profiles require a localized impact region.",
                    nameof(impact));
            if (impact.Mechanism != damageProfile.Mechanism)
                throw new ArgumentException(
                    "Impact mechanism does not match its weapon damage profile.",
                    nameof(impact));

            RegionConsequenceProfile consequences = damageProfile.GetRegion(
                impact.Region.Value);
            int transferredImpact = impact.Severity;
            int systemic = consequences.Project(
                consequences.SystemicPerHundred,
                transferredImpact);
            int structural = consequences.Project(
                consequences.StructuralPerHundred,
                transferredImpact);
            int motor = consequences.Project(
                consequences.MotorPerHundred,
                transferredImpact);
            int sensory = consequences.Project(
                consequences.SensoryPerHundred,
                transferredImpact);
            int bleed = consequences.Project(
                consequences.BleedPerHundred,
                transferredImpact);
            int consciousnessLoss = consequences.Project(
                consequences.ConsciousnessPerHundred,
                transferredImpact);
            int respirationLoss = consequences.Project(
                consequences.RespirationPerHundred,
                transferredImpact);
            bool vital = consequences.CausesVitalDamage(transferredImpact);
            // The legacy wound scalar is a count-compatible projection only.
            // Canonical movement comes from leg function, never this value.
            const float compatibilityMovementPenalty = 1f;
            var injury = new InjuryRecord(
                impact.CombatEventId + ":injury",
                impact.CombatEventId,
                impact.Region,
                impact.Mechanism,
                transferredImpact,
                structural,
                motor,
                sensory,
                bleed,
                vital,
                compatibilityMovementPenalty,
                systemic);
            var injuries = new List<InjuryRecord>(previous.Injuries)
            {
                injury,
            };
            int blood = Clamp(
                previous.Physiology.BloodReserve - bleed / 3,
                0,
                100);
            int shock = Clamp(
                previous.Physiology.Shock + systemic / 2 + (vital ? 10 : 0),
                0,
                100);
            int consciousness = Clamp(
                previous.Physiology.Consciousness - consciousnessLoss,
                0,
                100);
            int respiration = Clamp(
                previous.Physiology.Respiration - respirationLoss,
                0,
                100);
            var physiology = new ActorPhysiologyState(
                blood,
                shock,
                consciousness,
                respiration);
            var provisional = new ActorInjuryState(
                previous.ActorId,
                injuries,
                physiology,
                ActorLifeState.Active);
            ActorLifeState life = DeriveProfileLifeState(
                previous.LifeState,
                provisional,
                consequences.CausesCriticalIncapacitation(
                    transferredImpact));
            var resulting = new ActorInjuryState(
                previous.ActorId,
                injuries,
                physiology,
                life);
            var delta = new ActorInjuryDelta(
                impact,
                injury,
                previous.Physiology,
                physiology,
                previous.LifeState,
                life,
                previous.SystemicTrauma,
                resulting.SystemicTrauma);
            return new ActorInjuryResolution(delta, resulting);
        }

        public static ActorInjuryState ApplyDelta(
            ActorInjuryState previous,
            ActorInjuryDelta delta)
        {
            if (previous == null) throw new ArgumentNullException(nameof(previous));
            if (delta == null) throw new ArgumentNullException(nameof(delta));
            if (!string.Equals(
                    previous.ActorId,
                    delta.Impact.TargetActorId,
                    StringComparison.Ordinal)
                || previous.LifeState != delta.PreviousLifeState
                || !PhysiologyMatches(
                    previous.Physiology,
                    delta.PreviousPhysiology))
                throw new InvalidOperationException(
                    "Localized injury delta no longer starts at canonical state.");
            var injuries = new List<InjuryRecord>(previous.Injuries)
            {
                delta.Injury,
            };
            return new ActorInjuryState(
                previous.ActorId,
                injuries,
                delta.ResultingPhysiology,
                delta.ResultingLifeState);
        }

        public static ActorInjuryState AdvanceSystemic(
            ActorInjuryState previous)
        {
            if (previous == null) throw new ArgumentNullException(nameof(previous));
            if (previous.LifeState == ActorLifeState.Dead) return previous;
            int bleedRate = 0;
            foreach (InjuryRecord injury in previous.Injuries)
                bleedRate = Math.Min(100, bleedRate + injury.BleedRate);
            if (bleedRate == 0) return previous;
            int bloodLoss = Math.Max(1, bleedRate / 3);
            int blood = Clamp(
                previous.Physiology.BloodReserve - bloodLoss,
                0,
                100);
            int shock = Clamp(
                previous.Physiology.Shock + Math.Max(1, bleedRate / 4),
                0,
                100);
            int consciousnessLoss = shock >= 50
                ? Math.Max(1, (shock - 40) / 10)
                : 0;
            int consciousness = Clamp(
                previous.Physiology.Consciousness - consciousnessLoss,
                0,
                100);
            var physiology = new ActorPhysiologyState(
                blood,
                shock,
                consciousness,
                previous.Physiology.Respiration);
            ActorLifeState life = blood == 0
                ? ActorLifeState.Dead
                : consciousness <= 25 || shock >= 90
                    ? ActorLifeState.Incapacitated
                    : previous.LifeState;
            return new ActorInjuryState(
                previous.ActorId,
                previous.Injuries,
                physiology,
                life);
        }

        private static ActorLifeState DeriveLifeState(
            ActorInjuryState state,
            TargetRegionId? region,
            int severity,
            bool vital)
        {
            if ((vital && severity >= 95)
                || state.Physiology.BloodReserve == 0
                || state.Physiology.Respiration == 0
                || state.GetRegion(TargetRegionId.Head).StructuralIntegrity == 0
                || state.GetRegion(TargetRegionId.Torso).StructuralIntegrity == 0)
                return ActorLifeState.Dead;
            if ((region == TargetRegionId.Head && severity >= 70)
                || (region == TargetRegionId.Torso && severity >= 85)
                || state.Physiology.Consciousness <= 25
                || state.Physiology.Shock >= 90)
                return ActorLifeState.Incapacitated;
            return ActorLifeState.Active;
        }

        private static ActorLifeState DeriveProfileLifeState(
            ActorLifeState previousLifeState,
            ActorInjuryState state,
            bool criticalIncapacitation)
        {
            if (previousLifeState == ActorLifeState.Dead
                || state.SystemicTrauma >= 100
                || state.Physiology.BloodReserve == 0
                || state.Physiology.Respiration == 0)
                return ActorLifeState.Dead;
            if (previousLifeState == ActorLifeState.Incapacitated
                || state.SystemicTrauma >= 80
                || criticalIncapacitation
                || state.Physiology.Consciousness <= 25
                || state.Physiology.Shock >= 90)
                return ActorLifeState.Incapacitated;
            return ActorLifeState.Active;
        }

        private static int ResolveMotorLoss(
            TargetRegionId? region,
            int severity)
        {
            if (region == TargetRegionId.LeftArm
                || region == TargetRegionId.RightArm)
                return Clamp(severity * 5 / 4, 0, 100);
            if (region == TargetRegionId.LeftLeg
                || region == TargetRegionId.RightLeg)
                return Clamp(severity * 4 / 3, 0, 100);
            if (region == TargetRegionId.Torso)
                return severity / 3;
            if (region == TargetRegionId.Head)
                return severity / 5;
            return severity / 4;
        }

        private static int ResolveSensoryLoss(
            TargetRegionId? region,
            int severity) => region == TargetRegionId.Head
                ? Clamp(severity * 5 / 4, 0, 100)
                : severity / 6;

        private static int ResolveBleedRate(
            TargetRegionId? region,
            int severity)
        {
            int divisor = region == TargetRegionId.Head
                    || region == TargetRegionId.Torso
                ? 8
                : 12;
            return Math.Max(1, severity / divisor);
        }

        private static int Clamp(int value, int minimum, int maximum) =>
            Math.Max(minimum, Math.Min(maximum, value));

        private static bool PhysiologyMatches(
            ActorPhysiologyState left,
            ActorPhysiologyState right) =>
            left.BloodReserve == right.BloodReserve
            && left.Shock == right.Shock
            && left.Consciousness == right.Consciousness
            && left.Respiration == right.Respiration;
    }

    public static class LegacyWoundProjection
    {
        public static ActorWoundSnapshot From(ActorInjuryState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            int head = 0;
            int torso = 0;
            int leftArm = 0;
            int rightArm = 0;
            int leftLeg = 0;
            int rightLeg = 0;
            int unlocalized = 0;
            float movementPenalty = 0f;
            foreach (InjuryRecord injury in state.Injuries)
            {
                movementPenalty += injury.CompatibilityMovementPenalty;
                switch (injury.Region)
                {
                    case TargetRegionId.Head: head++; break;
                    case TargetRegionId.Torso: torso++; break;
                    case TargetRegionId.LeftArm: leftArm++; break;
                    case TargetRegionId.RightArm: rightArm++; break;
                    case TargetRegionId.LeftLeg: leftLeg++; break;
                    case TargetRegionId.RightLeg: rightLeg++; break;
                    case null: unlocalized++; break;
                    default: throw new ArgumentOutOfRangeException();
                }
            }
            return new ActorWoundSnapshot(
                state.ActorId,
                head,
                torso,
                leftArm,
                rightArm,
                leftLeg,
                rightLeg,
                unlocalized,
                movementPenalty);
        }

        public static ActorInjuryState ToInjuryState(
            ActorWoundSnapshot wounds,
            int legacyMaximumWounds)
        {
            if (legacyMaximumWounds <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(legacyMaximumWounds));
            if (wounds.WoundCount == 0)
                return ActorInjuryState.CreateHealthy(wounds.ActorId);
            var injuries = new List<InjuryRecord>(wounds.WoundCount);
            float remainingPenalty = wounds.MovementPenalty;
            int remainingCount = wounds.WoundCount;
            int ordinal = 0;
            AddLegacy(
                injuries,
                wounds.ActorId,
                TargetRegionId.Head,
                wounds.HeadWounds,
                ref remainingPenalty,
                ref remainingCount,
                ref ordinal);
            AddLegacy(injuries, wounds.ActorId, TargetRegionId.Torso,
                wounds.TorsoWounds, ref remainingPenalty, ref remainingCount,
                ref ordinal);
            AddLegacy(injuries, wounds.ActorId, TargetRegionId.LeftArm,
                wounds.LeftArmWounds, ref remainingPenalty, ref remainingCount,
                ref ordinal);
            AddLegacy(injuries, wounds.ActorId, TargetRegionId.RightArm,
                wounds.RightArmWounds, ref remainingPenalty, ref remainingCount,
                ref ordinal);
            AddLegacy(injuries, wounds.ActorId, TargetRegionId.LeftLeg,
                wounds.LeftLegWounds, ref remainingPenalty, ref remainingCount,
                ref ordinal);
            AddLegacy(injuries, wounds.ActorId, TargetRegionId.RightLeg,
                wounds.RightLegWounds, ref remainingPenalty, ref remainingCount,
                ref ordinal);
            AddLegacy(injuries, wounds.ActorId, null,
                wounds.UnlocalizedWounds, ref remainingPenalty,
                ref remainingCount, ref ordinal);
            ActorLifeState life = wounds.WoundCount >= legacyMaximumWounds
                ? ActorLifeState.Incapacitated
                : ActorLifeState.Active;
            return new ActorInjuryState(
                wounds.ActorId,
                injuries,
                ActorPhysiologyState.Healthy,
                life);
        }

        private static void AddLegacy(
            ICollection<InjuryRecord> injuries,
            string actorId,
            TargetRegionId? region,
            int count,
            ref float remainingPenalty,
            ref int remainingCount,
            ref int ordinal)
        {
            for (int index = 0; index < count; index++)
            {
                ordinal++;
                float penalty = remainingCount == 1
                    ? remainingPenalty
                    : 0f;
                remainingPenalty -= penalty;
                remainingCount--;
                string id = "legacy-injury:" + actorId + ":" + ordinal;
                int motor = region == TargetRegionId.LeftArm
                        || region == TargetRegionId.RightArm
                        || region == TargetRegionId.LeftLeg
                        || region == TargetRegionId.RightLeg
                    ? 35
                    : 10;
                injuries.Add(new InjuryRecord(
                    id,
                    id,
                    region,
                    DamageMechanism.Blunt,
                    severity: 35,
                    structuralDamage: 35,
                    motorLoss: motor,
                    sensoryLoss: region == TargetRegionId.Head ? 35 : 5,
                    bleedRate: 0,
                    vitalDamage: false,
                    compatibilityMovementPenalty: penalty));
            }
        }
    }
}
