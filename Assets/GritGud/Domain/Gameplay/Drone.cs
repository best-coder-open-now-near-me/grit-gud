using System;
using GritGud.Domain.Turns;

namespace GritGud.Domain.Gameplay
{
    public enum DroneInitiativeBinding
    {
        ControllerTurn,
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

    /// <summary>
    /// An unmanned vehicle controlled through an actor's canonical turn. The
    /// explicit initiative binding leaves autonomous scheduling as a future
    /// policy instead of making the drone a disguised humanoid actor.
    /// </summary>
    public sealed class DroneDefinition
    {
        public DroneDefinition(
            string id,
            string controllerActorId,
            GameplayPosition startingPosition,
            float startingFacingDegrees,
            float maximumIntegrity,
            float maximumMoveDistance,
            ActionCost moveCost,
            DroneSensorDefinition sensor,
            AttackDefinition attack,
            DroneInitiativeBinding initiativeBinding =
                DroneInitiativeBinding.ControllerTurn)
        {
            Id = RequireText(id, nameof(id));
            ControllerActorId = RequireText(
                controllerActorId,
                nameof(controllerActorId));
            if (!IsFinite(startingFacingDegrees))
                throw new ArgumentOutOfRangeException(
                    nameof(startingFacingDegrees));
            if (!IsFinite(maximumIntegrity) || maximumIntegrity <= 0f)
                throw new ArgumentOutOfRangeException(nameof(maximumIntegrity));
            if (!IsFinite(maximumMoveDistance) || maximumMoveDistance <= 0f)
                throw new ArgumentOutOfRangeException(
                    nameof(maximumMoveDistance));
            if (!Enum.IsDefined(typeof(DroneInitiativeBinding), initiativeBinding))
                throw new ArgumentOutOfRangeException(nameof(initiativeBinding));
            StartingPosition = startingPosition;
            StartingFacingDegrees = NormalizeDegrees(startingFacingDegrees);
            MaximumIntegrity = maximumIntegrity;
            MaximumMoveDistance = maximumMoveDistance;
            MoveCost = moveCost;
            Sensor = sensor ?? throw new ArgumentNullException(nameof(sensor));
            Attack = attack ?? throw new ArgumentNullException(nameof(attack));
            if (attack.Projectile != null || attack.Contact != null)
                throw new NotSupportedException(
                    "Drone weapons currently require immediate ranged delivery.");
            InitiativeBinding = initiativeBinding;
        }

        public string Id { get; }
        public string ControllerActorId { get; }
        public GameplayPosition StartingPosition { get; }
        public float StartingFacingDegrees { get; }
        public float MaximumIntegrity { get; }
        public float MaximumMoveDistance { get; }
        public ActionCost MoveCost { get; }
        public DroneSensorDefinition Sensor { get; }
        public AttackDefinition Attack { get; }
        public DroneInitiativeBinding InitiativeBinding { get; }

        public DroneSnapshot CreateInitialSnapshot() => new DroneSnapshot(
            this,
            StartingPosition,
            StartingFacingDegrees,
            MaximumIntegrity);

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

    public readonly struct DroneSnapshot
    {
        public DroneSnapshot(
            DroneDefinition definition,
            GameplayPosition position,
            float facingDegrees,
            float remainingIntegrity)
        {
            Definition = definition ?? throw new ArgumentNullException(
                nameof(definition));
            if (float.IsNaN(facingDegrees) || float.IsInfinity(facingDegrees))
                throw new ArgumentOutOfRangeException(nameof(facingDegrees));
            if (float.IsNaN(remainingIntegrity)
                || float.IsInfinity(remainingIntegrity)
                || remainingIntegrity < 0f
                || remainingIntegrity > definition.MaximumIntegrity)
                throw new ArgumentOutOfRangeException(nameof(remainingIntegrity));
            Position = position;
            FacingDegrees = DroneDefinition.NormalizeDegrees(facingDegrees);
            RemainingIntegrity = remainingIntegrity;
        }

        public string DroneId => Definition.Id;
        public DroneDefinition Definition { get; }
        public GameplayPosition Position { get; }
        public float FacingDegrees { get; }
        public float RemainingIntegrity { get; }
        public bool IsOperational => RemainingIntegrity > 0f;
    }

    public static class DroneSensorRules
    {
        public static bool CanObserve(
            DroneSnapshot drone,
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

    public sealed class DroneMoveRecord
    {
        public DroneMoveRecord(
            string controllerActorId,
            string droneId,
            GameplayPosition origin,
            GameplayPosition destination,
            float resultingFacingDegrees,
            ActionCost cost,
            TurnBudget previousBudget,
            TurnBudget resultingBudget)
        {
            ControllerActorId = DroneDefinition.RequireText(
                controllerActorId,
                nameof(controllerActorId));
            DroneId = DroneDefinition.RequireText(
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
            ResultingFacingDegrees = DroneDefinition.NormalizeDegrees(
                resultingFacingDegrees);
            Cost = cost;
            PreviousBudget = previousBudget;
            ResultingBudget = resultingBudget;
        }

        public string ControllerActorId { get; }
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
            DroneSnapshot previous,
            DroneSnapshot resulting)
        {
            if (float.IsNaN(appliedDamage)
                || float.IsInfinity(appliedDamage)
                || appliedDamage <= 0f)
                throw new ArgumentOutOfRangeException(nameof(appliedDamage));
            if (!string.Equals(
                    previous.DroneId,
                    resulting.DroneId,
                    StringComparison.Ordinal)
                || previous.Position.DistanceTo(resulting.Position) != 0f
                || previous.FacingDegrees != resulting.FacingDegrees
                || resulting.RemainingIntegrity
                    != Math.Max(0f, previous.RemainingIntegrity - appliedDamage))
                throw new ArgumentException(
                    "Drone integrity damage must preserve identity and pose and clamp at zero.",
                    nameof(resulting));
            AppliedDamage = appliedDamage;
            Previous = previous;
            Resulting = resulting;
        }

        public string DroneId => Previous.DroneId;
        public float AppliedDamage { get; }
        public DroneSnapshot Previous { get; }
        public DroneSnapshot Resulting { get; }
    }

    public sealed class DroneExposureSnapshot
    {
        public DroneExposureSnapshot(
            string observerId,
            string droneId,
            int visibleSampleCount,
            int totalSampleCount)
        {
            ObserverId = DroneDefinition.RequireText(
                observerId, nameof(observerId));
            DroneId = DroneDefinition.RequireText(droneId, nameof(droneId));
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
            DroneIntegrityDamageRecord damage)
        {
            if (sequence <= 0) throw new ArgumentOutOfRangeException(
                nameof(sequence));
            AttackerId = DroneDefinition.RequireText(
                attackerId, nameof(attackerId));
            AttackId = DroneDefinition.RequireText(attackId, nameof(attackId));
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
        public bool Hit => Damage != null;
        public DroneIntegrityDamageRecord Damage { get; }
    }

    public static class DroneDirectAttackRules
    {
        public static int CalculateHitChancePercent(
            AttackDefinition attack,
            DroneExposureSnapshot exposure,
            float distance)
        {
            if (attack == null) throw new ArgumentNullException(nameof(attack));
            if (exposure == null) throw new ArgumentNullException(nameof(exposure));
            if (float.IsNaN(distance) || float.IsInfinity(distance)
                || distance < 0f)
                throw new ArgumentOutOfRangeException(nameof(distance));
            float accuracy = attack.AccuracyDecay.EvaluatePercent(distance);
            return Math.Max(0, Math.Min(100, (int)Math.Round(
                accuracy * exposure.VisibleFraction,
                MidpointRounding.AwayFromZero)));
        }

        public static ActorDroneAttackRecord Resolve(
            long sequence,
            uint resolutionSeed,
            string attackerId,
            AttackDefinition attack,
            TurnBudget previousBudget,
            DroneExposureSnapshot exposure,
            float distance,
            DroneSnapshot target)
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
            int chance = CalculateHitChancePercent(attack, exposure, distance);
            int roll = Roll100(resolutionSeed);
            DroneIntegrityDamageRecord damage = roll <= chance
                ? new DroneIntegrityDamageRecord(
                    attack.DirectVehicleIntegrityDamage,
                    target,
                    new DroneSnapshot(
                        target.Definition,
                        target.Position,
                        target.FacingDegrees,
                        Math.Max(
                            0f,
                            target.RemainingIntegrity
                                - attack.DirectVehicleIntegrityDamage)))
                : null;
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
                damage);
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
            string controllerActorId,
            string droneId,
            string targetId,
            string targetKind,
            ActionCost cost,
            TurnBudget previousBudget,
            TurnBudget resultingBudget,
            object consequence)
        {
            ControllerActorId = DroneDefinition.RequireText(
                controllerActorId, nameof(controllerActorId));
            DroneId = DroneDefinition.RequireText(droneId, nameof(droneId));
            TargetId = DroneDefinition.RequireText(targetId, nameof(targetId));
            TargetKind = DroneDefinition.RequireText(targetKind, nameof(targetKind));
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

        public string ControllerActorId { get; }
        public string DroneId { get; }
        public string TargetId { get; }
        public string TargetKind { get; }
        public ActionCost Cost { get; }
        public TurnBudget PreviousBudget { get; }
        public TurnBudget ResultingBudget { get; }
        public object Consequence { get; }
    }
}
