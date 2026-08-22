using System;
using System.Collections.Generic;
using GritGud.Domain.Turns;

namespace GritGud.Domain.Gameplay
{
    public enum DroneTurnPoolingPolicy
    {
        SharedSummonerBudget,
    }

    public enum SummonLifecycleState
    {
        Active = 0,
        Crashing = 1,
        Destroyed = 2,
        Dismissed = 3,
        Expired = 4,
    }

    /// <summary>
    /// Canonical ownership for a partnered summoner/drone activation. The
    /// summoner keeps the initiative slot and owns the one AP pool consumed by
    /// actions from either partner.
    /// </summary>
    public sealed class DroneTurnPartnership
    {
        public DroneTurnPartnership(
            string summonerActorId,
            DroneTurnPoolingPolicy poolingPolicy =
                DroneTurnPoolingPolicy.SharedSummonerBudget)
        {
            SummonerActorId = DroneArchetypeDefinition.RequireText(
                summonerActorId,
                nameof(summonerActorId));
            if (!Enum.IsDefined(typeof(DroneTurnPoolingPolicy), poolingPolicy))
                throw new ArgumentOutOfRangeException(nameof(poolingPolicy));
            PoolingPolicy = poolingPolicy;
        }

        public string SummonerActorId { get; }
        public string SharedBudgetActorId => SummonerActorId;
        public DroneTurnPoolingPolicy PoolingPolicy { get; }

        public bool OwnsSharedBudget(string actorId) => string.Equals(
            SharedBudgetActorId,
            actorId,
            StringComparison.Ordinal);
    }

    public sealed class DroneSensorDefinition
    {
        public DroneSensorDefinition(float range, float viewAngleDegrees)
        {
            RequirePositive(range, nameof(range));
            if (!IsFinite(viewAngleDegrees)
                || viewAngleDegrees <= 0f
                || viewAngleDegrees > 360f)
                throw new ArgumentOutOfRangeException(nameof(viewAngleDegrees));
            Range = range;
            ViewAngleDegrees = viewAngleDegrees;
        }

        public float Range { get; }
        public float ViewAngleDegrees { get; }

        private static void RequirePositive(float value, string parameter)
        {
            if (!IsFinite(value) || value <= 0f)
                throw new ArgumentOutOfRangeException(parameter);
        }

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }

    public sealed class DroneCrashDefinition
    {
        public DroneCrashDefinition(
            float impactRadius,
            float injuryMovementPenalty,
            float destructibleIntegrityDamage,
            int maximumActionPointReduction,
            float maximumDriftDistance,
            float impactPlaybackSeconds)
        {
            RequirePositive(impactRadius, nameof(impactRadius));
            RequireNonNegative(
                injuryMovementPenalty,
                nameof(injuryMovementPenalty));
            RequireNonNegative(
                destructibleIntegrityDamage,
                nameof(destructibleIntegrityDamage));
            if (maximumActionPointReduction < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(maximumActionPointReduction));
            RequireNonNegative(
                maximumDriftDistance,
                nameof(maximumDriftDistance));
            RequirePositive(
                impactPlaybackSeconds,
                nameof(impactPlaybackSeconds));
            ImpactRadius = impactRadius;
            InjuryMovementPenalty = injuryMovementPenalty;
            DestructibleIntegrityDamage = destructibleIntegrityDamage;
            MaximumActionPointReduction = maximumActionPointReduction;
            MaximumDriftDistance = maximumDriftDistance;
            ImpactPlaybackSeconds = impactPlaybackSeconds;
        }

        public float ImpactRadius { get; }
        public float InjuryMovementPenalty { get; }
        public float DestructibleIntegrityDamage { get; }
        public int MaximumActionPointReduction { get; }
        public float MaximumDriftDistance { get; }
        public float ImpactPlaybackSeconds { get; }

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

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }

    /// <summary>
    /// Reusable authored drone capabilities. Instance identity, summoner,
    /// position, integrity, duration, and lifecycle belong exclusively to the
    /// summoned snapshot below.
    /// </summary>
    public sealed class DroneArchetypeDefinition
    {
        public DroneArchetypeDefinition(
            string archetypeId,
            float maximumIntegrity,
            float maximumMoveDistance,
            ActionCost moveCost,
            DroneSensorDefinition sensor,
            AttackDefinition attack,
            string presentationId,
            DroneCrashDefinition crash)
        {
            ArchetypeId = RequireText(archetypeId, nameof(archetypeId));
            if (!IsFinite(maximumIntegrity) || maximumIntegrity <= 0f)
                throw new ArgumentOutOfRangeException(nameof(maximumIntegrity));
            if (!IsFinite(maximumMoveDistance) || maximumMoveDistance <= 0f)
                throw new ArgumentOutOfRangeException(
                    nameof(maximumMoveDistance));
            MaximumIntegrity = maximumIntegrity;
            MaximumMoveDistance = maximumMoveDistance;
            MoveCost = moveCost;
            Sensor = sensor ?? throw new ArgumentNullException(nameof(sensor));
            Attack = attack ?? throw new ArgumentNullException(nameof(attack));
            PresentationId = RequireText(
                presentationId,
                nameof(presentationId));
            Crash = crash ?? throw new ArgumentNullException(nameof(crash));
            if (attack.Projectile != null || attack.Contact != null)
                throw new NotSupportedException(
                    "Drone weapons currently require immediate ranged delivery.");
        }

        public string ArchetypeId { get; }
        public string Id => ArchetypeId;
        public float MaximumIntegrity { get; }
        public float MaximumMoveDistance { get; }
        public ActionCost MoveCost { get; }
        public DroneSensorDefinition Sensor { get; }
        public AttackDefinition Attack { get; }
        public string PresentationId { get; }
        public DroneCrashDefinition Crash { get; }

        internal static float NormalizeDegrees(float value)
        {
            float normalized = value % 360f;
            return normalized < 0f ? normalized + 360f : normalized;
        }

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);

        internal static string RequireText(string value, string parameter) =>
            string.IsNullOrWhiteSpace(value)
                ? throw new ArgumentException(
                    "Drone identifiers cannot be empty.", parameter)
                : value;
    }

    public sealed class DroneSummonAbilityDefinition
    {
        public DroneSummonAbilityDefinition(
            string abilityId,
            string droneArchetypeId,
            ActionCost summonCost,
            float maximumSpawnDistance,
            int maximumActiveInstances,
            int? durationTurns,
            float spawnHeight)
        {
            AbilityId = DroneArchetypeDefinition.RequireText(
                abilityId,
                nameof(abilityId));
            DroneArchetypeId = DroneArchetypeDefinition.RequireText(
                droneArchetypeId,
                nameof(droneArchetypeId));
            if (!IsFinite(maximumSpawnDistance)
                || maximumSpawnDistance <= 0f)
                throw new ArgumentOutOfRangeException(
                    nameof(maximumSpawnDistance));
            if (maximumActiveInstances <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(maximumActiveInstances));
            if (durationTurns.HasValue && durationTurns.Value <= 0)
                throw new ArgumentOutOfRangeException(nameof(durationTurns));
            if (!IsFinite(spawnHeight) || spawnHeight < 0f)
                throw new ArgumentOutOfRangeException(nameof(spawnHeight));
            SummonCost = summonCost;
            MaximumSpawnDistance = maximumSpawnDistance;
            MaximumActiveInstances = maximumActiveInstances;
            DurationTurns = durationTurns;
            SpawnHeight = spawnHeight;
        }

        public string AbilityId { get; }
        public string Id => AbilityId;
        public string DroneArchetypeId { get; }
        public ActionCost SummonCost { get; }
        public float MaximumSpawnDistance { get; }
        public int MaximumActiveInstances { get; }
        public int? DurationTurns { get; }
        public float SpawnHeight { get; }

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }

    public sealed class DroneCrashTrajectoryRecord
    {
        public DroneCrashTrajectoryRecord(
            GameplayPosition origin,
            GameplayPosition impactPosition,
            long disabledTransitionSequence)
        {
            if (disabledTransitionSequence <= 0L)
                throw new ArgumentOutOfRangeException(
                    nameof(disabledTransitionSequence));
            Origin = origin;
            ImpactPosition = impactPosition;
            DisabledTransitionSequence = disabledTransitionSequence;
        }

        public GameplayPosition Origin { get; }
        public GameplayPosition ImpactPosition { get; }
        public long DisabledTransitionSequence { get; }
    }

    public readonly struct SummonedDroneSnapshot
    {
        public SummonedDroneSnapshot(
            DroneArchetypeDefinition definition,
            string instanceId,
            string summonAbilityId,
            DroneTurnPartnership turnPartnership,
            GameplayPosition position,
            float facingDegrees,
            float remainingIntegrity,
            SummonLifecycleState lifecycle = SummonLifecycleState.Active,
            int? remainingDurationTurns = null,
            DroneCrashTrajectoryRecord crashTrajectory = null)
        {
            Definition = definition ?? throw new ArgumentNullException(
                nameof(definition));
            InstanceId = DroneArchetypeDefinition.RequireText(
                instanceId,
                nameof(instanceId));
            SummonAbilityId = DroneArchetypeDefinition.RequireText(
                summonAbilityId,
                nameof(summonAbilityId));
            TurnPartnership = turnPartnership ?? throw new ArgumentNullException(
                nameof(turnPartnership));
            if (!Enum.IsDefined(typeof(SummonLifecycleState), lifecycle))
                throw new ArgumentOutOfRangeException(nameof(lifecycle));
            if (float.IsNaN(facingDegrees) || float.IsInfinity(facingDegrees))
                throw new ArgumentOutOfRangeException(nameof(facingDegrees));
            if (float.IsNaN(remainingIntegrity)
                || float.IsInfinity(remainingIntegrity)
                || remainingIntegrity < 0f
                || remainingIntegrity > definition.MaximumIntegrity)
                throw new ArgumentOutOfRangeException(nameof(remainingIntegrity));
            if (remainingDurationTurns.HasValue
                && remainingDurationTurns.Value < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(remainingDurationTurns));
            if (lifecycle == SummonLifecycleState.Active
                && (remainingIntegrity <= 0f
                    || remainingDurationTurns == 0
                    || crashTrajectory != null))
                throw new ArgumentException(
                    "Active summons require integrity and cannot carry a crash trajectory.");
            if ((lifecycle == SummonLifecycleState.Crashing
                    || lifecycle == SummonLifecycleState.Destroyed)
                && (remainingIntegrity != 0f || crashTrajectory == null))
                throw new ArgumentException(
                    "Crashing and destroyed summons require zero integrity and a trajectory.");
            if ((lifecycle == SummonLifecycleState.Dismissed
                    || lifecycle == SummonLifecycleState.Expired)
                && crashTrajectory != null)
                throw new ArgumentException(
                    "Dismissed and expired summons cannot carry crash state.");
            Position = position;
            FacingDegrees = DroneArchetypeDefinition.NormalizeDegrees(facingDegrees);
            RemainingIntegrity = remainingIntegrity;
            Lifecycle = lifecycle;
            RemainingDurationTurns = remainingDurationTurns;
            CrashTrajectory = crashTrajectory;
        }

        public string InstanceId { get; }
        public string DroneId => InstanceId;
        public string ArchetypeId => Definition.ArchetypeId;
        public string SummonAbilityId { get; }
        public DroneTurnPartnership TurnPartnership { get; }
        public string SummonerActorId => TurnPartnership.SummonerActorId;
        public DroneArchetypeDefinition Definition { get; }
        public GameplayPosition Position { get; }
        public float FacingDegrees { get; }
        public float RemainingIntegrity { get; }
        public SummonLifecycleState Lifecycle { get; }
        public int? RemainingDurationTurns { get; }
        public DroneCrashTrajectoryRecord CrashTrajectory { get; }
        public bool IsOperational => Lifecycle == SummonLifecycleState.Active
            && RemainingIntegrity > 0f;
        public bool IsVisible => Lifecycle == SummonLifecycleState.Active
            || Lifecycle == SummonLifecycleState.Crashing
            || Lifecycle == SummonLifecycleState.Destroyed;

        public SummonedDroneSnapshot WithPose(
            GameplayPosition position,
            float facingDegrees) => new SummonedDroneSnapshot(
                Definition,
                InstanceId,
                SummonAbilityId,
                TurnPartnership,
                position,
                facingDegrees,
                RemainingIntegrity,
                Lifecycle,
                RemainingDurationTurns,
                CrashTrajectory);

        public SummonedDroneSnapshot WithLifecycle(
            SummonLifecycleState lifecycle,
            float remainingIntegrity,
            int? remainingDurationTurns,
            DroneCrashTrajectoryRecord crashTrajectory = null,
            GameplayPosition? position = null) => new SummonedDroneSnapshot(
                Definition,
                InstanceId,
                SummonAbilityId,
                TurnPartnership,
                position ?? Position,
                FacingDegrees,
                remainingIntegrity,
                lifecycle,
                remainingDurationTurns,
                crashTrajectory);
    }

    public static class DroneSensorRules
    {
        public static bool CanObserve(
            SummonedDroneSnapshot drone,
            GameplayPosition target)
        {
            if (!drone.IsOperational) return false;
            float dx = target.X - drone.Position.X;
            float dy = target.Y - drone.Position.Y;
            float dz = target.Z - drone.Position.Z;
            float distance = (float)Math.Sqrt(
                (dx * dx) + (dy * dy) + (dz * dz));
            if (distance > drone.Definition.Sensor.Range) return false;
            if (distance == 0f) return true;
            float horizontal = (float)Math.Sqrt((dx * dx) + (dz * dz));
            if (horizontal == 0f) return true;
            double radians = drone.FacingDegrees * Math.PI / 180d;
            float dot = ((dx / horizontal) * (float)Math.Sin(radians))
                + ((dz / horizontal) * (float)Math.Cos(radians));
            dot = Math.Max(-1f, Math.Min(1f, dot));
            float angle = (float)(Math.Acos(dot) * 180d / Math.PI);
            return angle <= drone.Definition.Sensor.ViewAngleDegrees * 0.5f;
        }
    }

    public sealed class SummonDroneRecord
    {
        public SummonDroneRecord(
            long sequence,
            string summonerActorId,
            DroneSummonAbilityDefinition ability,
            DroneArchetypeDefinition archetype,
            GameplayPosition spawnPosition,
            float spawnFacingDegrees,
            TurnBudget previousBudget,
            TurnBudget resultingBudget)
        {
            if (sequence <= 0L)
                throw new ArgumentOutOfRangeException(nameof(sequence));
            SummonerActorId = DroneArchetypeDefinition.RequireText(
                summonerActorId,
                nameof(summonerActorId));
            Ability = ability ?? throw new ArgumentNullException(nameof(ability));
            Archetype = archetype ?? throw new ArgumentNullException(
                nameof(archetype));
            if (!string.Equals(
                    ability.DroneArchetypeId,
                    archetype.ArchetypeId,
                    StringComparison.Ordinal))
                throw new ArgumentException(
                    "Summon ability and drone archetype do not match.",
                    nameof(archetype));
            TurnBudget expected = previousBudget.SpendAction(
                ability.SummonCost);
            if (!BudgetsMatch(expected, resultingBudget))
                throw new ArgumentException(
                    "Drone summon budget does not match its action cost.",
                    nameof(resultingBudget));
            Sequence = sequence;
            SpawnPosition = spawnPosition;
            SpawnFacingDegrees = DroneArchetypeDefinition.NormalizeDegrees(
                spawnFacingDegrees);
            PreviousBudget = previousBudget;
            ResultingBudget = resultingBudget;
            DroneInstanceId = CreateInstanceId(summonerActorId, sequence);
            Resulting = new SummonedDroneSnapshot(
                archetype,
                DroneInstanceId,
                ability.AbilityId,
                new DroneTurnPartnership(summonerActorId),
                spawnPosition,
                SpawnFacingDegrees,
                archetype.MaximumIntegrity,
                remainingDurationTurns: ability.DurationTurns);
        }

        public long Sequence { get; }
        public string SummonerActorId { get; }
        public string DroneInstanceId { get; }
        public DroneSummonAbilityDefinition Ability { get; }
        public DroneArchetypeDefinition Archetype { get; }
        public GameplayPosition SpawnPosition { get; }
        public float SpawnFacingDegrees { get; }
        public TurnBudget PreviousBudget { get; }
        public TurnBudget ResultingBudget { get; }
        public SummonedDroneSnapshot Resulting { get; }

        public static string CreateInstanceId(
            string summonerActorId,
            long summonSequence) => "drone:"
            + DroneArchetypeDefinition.RequireText(
                summonerActorId,
                nameof(summonerActorId))
            + ":"
            + (summonSequence > 0L
                ? summonSequence.ToString(
                    System.Globalization.CultureInfo.InvariantCulture)
                : throw new ArgumentOutOfRangeException(
                    nameof(summonSequence)));

        internal static bool BudgetsMatch(
            TurnBudget left,
            TurnBudget right) => left.ActionPoints == right.ActionPoints
            && left.MovementOpportunity == right.MovementOpportunity;
    }

    public sealed class DismissDroneRecord
    {
        public DismissDroneRecord(
            long sequence,
            string summonerActorId,
            ActionCost cost,
            TurnBudget previousBudget,
            TurnBudget resultingBudget,
            SummonedDroneSnapshot previous,
            SummonedDroneSnapshot resulting)
        {
            if (sequence <= 0L)
                throw new ArgumentOutOfRangeException(nameof(sequence));
            SummonerActorId = DroneArchetypeDefinition.RequireText(
                summonerActorId,
                nameof(summonerActorId));
            if (!SummonDroneRecord.BudgetsMatch(
                    previousBudget.SpendAction(cost),
                    resultingBudget))
                throw new ArgumentException(
                    "Drone dismissal budget does not match its action cost.",
                    nameof(resultingBudget));
            if (!previous.IsOperational
                || resulting.Lifecycle != SummonLifecycleState.Dismissed
                || !SameInstance(previous, resulting)
                || previous.Position.DistanceTo(resulting.Position) != 0f
                || previous.FacingDegrees != resulting.FacingDegrees
                || previous.RemainingIntegrity
                    != resulting.RemainingIntegrity
                || !string.Equals(
                    previous.SummonerActorId,
                    summonerActorId,
                    StringComparison.Ordinal))
                throw new ArgumentException(
                    "Drone dismissal must terminate the summoner's active instance.",
                    nameof(resulting));
            Sequence = sequence;
            Cost = cost;
            PreviousBudget = previousBudget;
            ResultingBudget = resultingBudget;
            Previous = previous;
            Resulting = resulting;
        }

        public long Sequence { get; }
        public string SummonerActorId { get; }
        public string DroneId => Previous.DroneId;
        public ActionCost Cost { get; }
        public TurnBudget PreviousBudget { get; }
        public TurnBudget ResultingBudget { get; }
        public SummonedDroneSnapshot Previous { get; }
        public SummonedDroneSnapshot Resulting { get; }

        internal static bool SameInstance(
            SummonedDroneSnapshot left,
            SummonedDroneSnapshot right) => string.Equals(
                left.DroneId,
                right.DroneId,
                StringComparison.Ordinal)
            && string.Equals(
                left.ArchetypeId,
                right.ArchetypeId,
                StringComparison.Ordinal)
            && string.Equals(
                left.SummonerActorId,
                right.SummonerActorId,
                StringComparison.Ordinal);
    }

    public sealed class ExpireDroneRecord
    {
        public ExpireDroneRecord(
            SummonedDroneSnapshot previous,
            SummonedDroneSnapshot resulting)
        {
            if (!previous.IsOperational
                || previous.RemainingDurationTurns != 1
                || resulting.Lifecycle != SummonLifecycleState.Expired
                || resulting.RemainingDurationTurns != 0
                || !DismissDroneRecord.SameInstance(previous, resulting)
                || previous.Position.DistanceTo(resulting.Position) != 0f
                || previous.FacingDegrees != resulting.FacingDegrees
                || previous.RemainingIntegrity
                    != resulting.RemainingIntegrity)
                throw new ArgumentException(
                    "Drone expiration must consume the final authored duration turn.",
                    nameof(resulting));
            Previous = previous;
            Resulting = resulting;
        }

        public string DroneId => Previous.DroneId;
        public string SummonerActorId => Previous.SummonerActorId;
        public SummonedDroneSnapshot Previous { get; }
        public SummonedDroneSnapshot Resulting { get; }
    }

    public sealed class DroneCrashImpactRecord
    {
        public DroneCrashImpactRecord(
            long sequence,
            SummonedDroneSnapshot previous,
            SummonedDroneSnapshot resulting,
            DroneCrashDefinition definition,
            IEnumerable<BlastEffectRecord> effects,
            IEnumerable<ConcussiveActionPointEffectRecord> concussiveEffects)
        {
            if (sequence <= 0L)
                throw new ArgumentOutOfRangeException(nameof(sequence));
            Definition = definition ?? throw new ArgumentNullException(
                nameof(definition));
            if (previous.Lifecycle != SummonLifecycleState.Crashing
                || resulting.Lifecycle != SummonLifecycleState.Destroyed
                || previous.CrashTrajectory == null
                || !DismissDroneRecord.SameInstance(previous, resulting)
                || resulting.Position.DistanceTo(
                    previous.CrashTrajectory.ImpactPosition) != 0f)
                throw new ArgumentException(
                    "Drone crash impact must finish its frozen trajectory.",
                    nameof(resulting));
            Sequence = sequence;
            Previous = previous;
            Resulting = resulting;
            Effects = new List<BlastEffectRecord>(
                effects ?? throw new ArgumentNullException(nameof(effects)))
                .AsReadOnly();
            ConcussiveEffects = new List<ConcussiveActionPointEffectRecord>(
                concussiveEffects ?? throw new ArgumentNullException(
                    nameof(concussiveEffects)))
                .AsReadOnly();
        }

        public long Sequence { get; }
        public string DroneId => Previous.DroneId;
        public string SummonerActorId => Previous.SummonerActorId;
        public GameplayPosition Origin => Previous.CrashTrajectory.Origin;
        public GameplayPosition ImpactPosition =>
            Previous.CrashTrajectory.ImpactPosition;
        public float ImpactNormalizedTime => 1f;
        public DroneCrashDefinition Definition { get; }
        public IReadOnlyList<BlastEffectRecord> Effects { get; }
        public IReadOnlyList<ConcussiveActionPointEffectRecord>
            ConcussiveEffects { get; }
        public SummonedDroneSnapshot Previous { get; }
        public SummonedDroneSnapshot Resulting { get; }
    }

    public sealed class DroneMoveRecord
    {
        public DroneMoveRecord(
            string summonerActorId,
            string droneId,
            GameplayPosition origin,
            GameplayPosition destination,
            float resultingFacingDegrees,
            ActionCost cost,
            TurnBudget previousBudget,
            TurnBudget resultingBudget)
        {
            SummonerActorId = DroneArchetypeDefinition.RequireText(
                summonerActorId,
                nameof(summonerActorId));
            DroneId = DroneArchetypeDefinition.RequireText(
                droneId,
                nameof(droneId));
            if (float.IsNaN(resultingFacingDegrees)
                || float.IsInfinity(resultingFacingDegrees))
                throw new ArgumentOutOfRangeException(
                    nameof(resultingFacingDegrees));
            TurnBudget expected = previousBudget.SpendAction(cost);
            if (expected.ActionPoints != resultingBudget.ActionPoints
                || expected.MovementOpportunity
                    != resultingBudget.MovementOpportunity)
                throw new ArgumentException(
                    "Drone movement budget does not match its action cost.",
                    nameof(resultingBudget));
            Origin = origin;
            Destination = destination;
            ResultingFacingDegrees = DroneArchetypeDefinition.NormalizeDegrees(
                resultingFacingDegrees);
            Cost = cost;
            PreviousBudget = previousBudget;
            ResultingBudget = resultingBudget;
        }

        public string SummonerActorId { get; }
        public string DroneId { get; }
        public GameplayPosition Origin { get; }
        public GameplayPosition Destination { get; }
        public float ResultingFacingDegrees { get; }
        public ActionCost Cost { get; }
        public TurnBudget PreviousBudget { get; }
        public TurnBudget ResultingBudget { get; }
    }

    public sealed class DroneIntegrityDamageRecord
    {
        public DroneIntegrityDamageRecord(
            float appliedDamage,
            SummonedDroneSnapshot previous,
            SummonedDroneSnapshot resulting)
        {
            if (float.IsNaN(appliedDamage)
                || float.IsInfinity(appliedDamage)
                || appliedDamage <= 0f)
                throw new ArgumentOutOfRangeException(nameof(appliedDamage));
            if (!string.Equals(
                    previous.DroneId,
                    resulting.DroneId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    previous.ArchetypeId,
                    resulting.ArchetypeId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    previous.SummonerActorId,
                    resulting.SummonerActorId,
                    StringComparison.Ordinal)
                || previous.Position.DistanceTo(resulting.Position) != 0f
                || previous.FacingDegrees != resulting.FacingDegrees
                || resulting.RemainingIntegrity
                    != Math.Max(0f, previous.RemainingIntegrity - appliedDamage)
                || !previous.IsOperational
                || (resulting.RemainingIntegrity > 0f
                    && resulting.Lifecycle != SummonLifecycleState.Active)
                || (resulting.RemainingIntegrity == 0f
                    && resulting.Lifecycle != SummonLifecycleState.Crashing))
                throw new ArgumentException(
                    "Drone integrity damage must preserve identity and pose and clamp at zero.",
                    nameof(resulting));
            AppliedDamage = appliedDamage;
            Previous = previous;
            Resulting = resulting;
        }

        public string DroneId => Previous.DroneId;
        public float AppliedDamage { get; }
        public SummonedDroneSnapshot Previous { get; }
        public SummonedDroneSnapshot Resulting { get; }
        public bool StartedCrash => Resulting.Lifecycle
            == SummonLifecycleState.Crashing;
    }

    public sealed class DroneExposureSnapshot
    {
        public DroneExposureSnapshot(
            string observerId,
            string droneId,
            int visibleSampleCount,
            int totalSampleCount)
        {
            ObserverId = DroneArchetypeDefinition.RequireText(
                observerId, nameof(observerId));
            DroneId = DroneArchetypeDefinition.RequireText(droneId, nameof(droneId));
            if (totalSampleCount <= 0
                || visibleSampleCount < 0
                || visibleSampleCount > totalSampleCount)
                throw new ArgumentOutOfRangeException(nameof(visibleSampleCount));
            VisibleSampleCount = visibleSampleCount;
            TotalSampleCount = totalSampleCount;
        }

        public string ObserverId { get; }
        public string DroneId { get; }
        public int VisibleSampleCount { get; }
        public int TotalSampleCount { get; }
        public float VisibleFraction =>
            VisibleSampleCount / (float)TotalSampleCount;
    }

    public sealed class ActorDroneAttackRecord
    {
        internal ActorDroneAttackRecord(
            long sequence,
            string attackerId,
            string attackId,
            ActionCost cost,
            TurnBudget previousBudget,
            TurnBudget resultingBudget,
            DroneExposureSnapshot exposure,
            uint resolutionSeed,
            float distance,
            int hitChancePercent,
            int hitRoll,
            DroneIntegrityDamageRecord damage,
            int capabilityAccuracyDeltaPercent = 0)
        {
            if (sequence <= 0) throw new ArgumentOutOfRangeException(
                nameof(sequence));
            AttackerId = DroneArchetypeDefinition.RequireText(
                attackerId, nameof(attackerId));
            AttackId = DroneArchetypeDefinition.RequireText(attackId, nameof(attackId));
            Exposure = exposure ?? throw new ArgumentNullException(
                nameof(exposure));
            if (!string.Equals(attackerId, exposure.ObserverId,
                    StringComparison.Ordinal))
                throw new ArgumentException(
                    "Drone exposure must originate from the attacking actor.",
                    nameof(exposure));
            if (float.IsNaN(distance) || float.IsInfinity(distance)
                || distance < 0f)
                throw new ArgumentOutOfRangeException(nameof(distance));
            if (hitChancePercent < 0 || hitChancePercent > 100
                || capabilityAccuracyDeltaPercent < -100
                || capabilityAccuracyDeltaPercent > 100
                || hitRoll < 1 || hitRoll > 100
                || (damage != null) != (hitRoll <= hitChancePercent)
                || (damage != null && !string.Equals(
                    damage.DroneId, exposure.DroneId,
                    StringComparison.Ordinal)))
                throw new ArgumentException(
                    "Drone attack roll and integrity consequence are inconsistent.",
                    nameof(damage));
            TurnBudget expected = previousBudget.SpendAction(cost);
            if (expected.ActionPoints != resultingBudget.ActionPoints
                || expected.MovementOpportunity
                    != resultingBudget.MovementOpportunity)
                throw new ArgumentException(
                    "Actor drone attack budget does not match its cost.",
                    nameof(resultingBudget));
            Sequence = sequence;
            Cost = cost;
            PreviousBudget = previousBudget;
            ResultingBudget = resultingBudget;
            ResolutionSeed = resolutionSeed;
            Distance = distance;
            HitChancePercent = hitChancePercent;
            HitRoll = hitRoll;
            Damage = damage;
            CapabilityAccuracyDeltaPercent = capabilityAccuracyDeltaPercent;
        }

        public long Sequence { get; }
        public string AttackerId { get; }
        public string AttackId { get; }
        public string DroneId => Exposure.DroneId;
        public ActionCost Cost { get; }
        public TurnBudget PreviousBudget { get; }
        public TurnBudget ResultingBudget { get; }
        public DroneExposureSnapshot Exposure { get; }
        public uint ResolutionSeed { get; }
        public float Distance { get; }
        public int HitChancePercent { get; }
        public int HitRoll { get; }
        public int CapabilityAccuracyDeltaPercent { get; }
        public bool Hit => Damage != null;
        public DroneIntegrityDamageRecord Damage { get; }
    }

    public static class DroneDirectAttackRules
    {
        public static int CalculateHitChancePercent(
            AttackDefinition attack,
            DroneExposureSnapshot exposure,
            float distance,
            int capabilityAccuracyDeltaPercent = 0)
        {
            if (attack == null) throw new ArgumentNullException(nameof(attack));
            if (exposure == null) throw new ArgumentNullException(nameof(exposure));
            if (float.IsNaN(distance) || float.IsInfinity(distance)
                || distance < 0f)
                throw new ArgumentOutOfRangeException(nameof(distance));
            float accuracy = attack.AccuracyDecay.EvaluatePercent(distance);
            if (capabilityAccuracyDeltaPercent < -100
                || capabilityAccuracyDeltaPercent > 100)
                throw new ArgumentOutOfRangeException(
                    nameof(capabilityAccuracyDeltaPercent));
            return Math.Max(0, Math.Min(100, checked((int)Math.Round(
                accuracy * exposure.VisibleFraction,
                MidpointRounding.AwayFromZero)
                + capabilityAccuracyDeltaPercent)));
        }

        public static ActorDroneAttackRecord Resolve(
            long sequence,
            uint resolutionSeed,
            string attackerId,
            AttackDefinition attack,
            TurnBudget previousBudget,
            DroneExposureSnapshot exposure,
            float distance,
            SummonedDroneSnapshot target,
            int capabilityAccuracyDeltaPercent = 0,
            DroneCrashTrajectoryRecord crashTrajectory = null)
        {
            if (attack == null) throw new ArgumentNullException(nameof(attack));
            if (attack.DirectVehicleIntegrityDamage <= 0f)
                throw new InvalidOperationException(
                    "Attack has no authored vehicle integrity damage.");
            if (!string.Equals(target.DroneId, exposure?.DroneId,
                    StringComparison.Ordinal))
                throw new ArgumentException(
                    "Exposure does not describe the target drone.",
                    nameof(exposure));
            int chance = CalculateHitChancePercent(
                attack,
                exposure,
                distance,
                capabilityAccuracyDeltaPercent);
            int roll = Roll100(resolutionSeed);
            DroneIntegrityDamageRecord damage = null;
            if (roll <= chance)
            {
                float remaining = Math.Max(
                    0f,
                    target.RemainingIntegrity
                        - attack.DirectVehicleIntegrityDamage);
                if (remaining == 0f && crashTrajectory == null)
                    throw new InvalidOperationException(
                        "Lethal drone damage requires a frozen crash trajectory.");
                damage = new DroneIntegrityDamageRecord(
                    attack.DirectVehicleIntegrityDamage,
                    target,
                    new SummonedDroneSnapshot(
                        target.Definition,
                        target.InstanceId,
                        target.SummonAbilityId,
                        target.TurnPartnership,
                        target.Position,
                        target.FacingDegrees,
                        remaining,
                        remaining > 0f
                            ? SummonLifecycleState.Active
                            : SummonLifecycleState.Crashing,
                        target.RemainingDurationTurns,
                        remaining > 0f ? null : crashTrajectory));
            }
            return new ActorDroneAttackRecord(
                sequence,
                attackerId,
                attack.ActionId,
                attack.TurnCost,
                previousBudget,
                previousBudget.SpendAction(attack.TurnCost),
                exposure,
                resolutionSeed,
                distance,
                chance,
                roll,
                damage,
                capabilityAccuracyDeltaPercent);
        }

        private static int Roll100(uint seed)
        {
            uint state = seed != 0u ? seed : 0x6D2B79F5u;
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return (int)(state % 100u) + 1;
        }
    }

    public sealed class DroneAttackRecord
    {
        public DroneAttackRecord(
            string summonerActorId,
            string droneId,
            string targetId,
            string targetKind,
            ActionCost cost,
            TurnBudget previousBudget,
            TurnBudget resultingBudget,
            object consequence)
        {
            SummonerActorId = DroneArchetypeDefinition.RequireText(
                summonerActorId, nameof(summonerActorId));
            DroneId = DroneArchetypeDefinition.RequireText(droneId, nameof(droneId));
            TargetId = DroneArchetypeDefinition.RequireText(targetId, nameof(targetId));
            TargetKind = DroneArchetypeDefinition.RequireText(targetKind, nameof(targetKind));
            TurnBudget expected = previousBudget.SpendAction(cost);
            if (expected.ActionPoints != resultingBudget.ActionPoints
                || expected.MovementOpportunity
                    != resultingBudget.MovementOpportunity)
                throw new ArgumentException(
                    "Drone attack budget does not match its action cost.",
                    nameof(resultingBudget));
            Consequence = consequence ?? throw new ArgumentNullException(
                nameof(consequence));
            Cost = cost;
            PreviousBudget = previousBudget;
            ResultingBudget = resultingBudget;
        }

        public string SummonerActorId { get; }
        public string DroneId { get; }
        public string TargetId { get; }
        public string TargetKind { get; }
        public ActionCost Cost { get; }
        public TurnBudget PreviousBudget { get; }
        public TurnBudget ResultingBudget { get; }
        public object Consequence { get; }
    }
}
