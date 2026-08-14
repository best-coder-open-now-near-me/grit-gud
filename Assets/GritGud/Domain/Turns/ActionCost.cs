using System;

namespace GritGud.Domain.Turns
{
    /// <summary>
    /// The independent resources an action consumes. Movement opportunity is
    /// measured in the same world-distance units used by authoritative movement.
    /// </summary>
    public readonly struct ActionCost
    {
        public ActionCost(int actionPoints, float movementOpportunity, ActionMobility mobility)
        {
            if (actionPoints < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(actionPoints));
            }

            if (float.IsNaN(movementOpportunity)
                || float.IsInfinity(movementOpportunity)
                || movementOpportunity < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(movementOpportunity));
            }

            if (!Enum.IsDefined(typeof(ActionMobility), mobility))
            {
                throw new ArgumentOutOfRangeException(nameof(mobility));
            }

            ActionPoints = actionPoints;
            MovementOpportunity = movementOpportunity;
            Mobility = mobility;
        }

        public int ActionPoints { get; }

        public float MovementOpportunity { get; }

        public ActionMobility Mobility { get; }

        public static ActionCost Combine(ActionCost left, ActionCost right)
        {
            float movementOpportunity =
                left.MovementOpportunity + right.MovementOpportunity;
            if (float.IsNaN(movementOpportunity)
                || float.IsInfinity(movementOpportunity))
            {
                throw new OverflowException(
                    "Combined action movement cost is not finite.");
            }

            ActionMobility mobility =
                left.Mobility == ActionMobility.Set
                    || right.Mobility == ActionMobility.Set
                        ? ActionMobility.Set
                        : left.Mobility == ActionMobility.Momentum
                            || right.Mobility == ActionMobility.Momentum
                                ? ActionMobility.Momentum
                                : ActionMobility.Mobile;
            return new ActionCost(
                checked(left.ActionPoints + right.ActionPoints),
                movementOpportunity,
                mobility);
        }
    }
}
