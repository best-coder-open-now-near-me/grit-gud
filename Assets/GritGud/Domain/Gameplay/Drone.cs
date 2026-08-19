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
