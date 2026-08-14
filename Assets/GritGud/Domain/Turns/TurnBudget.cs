using System;

namespace GritGud.Domain.Turns
{
    /// <summary>
    /// Immutable AP and movement resources for one actor's current turn.
    /// </summary>
    public readonly struct TurnBudget
    {
        public TurnBudget(int actionPoints, float movementOpportunity)
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

            ActionPoints = actionPoints;
            MovementOpportunity = movementOpportunity;
        }

        public int ActionPoints { get; }

        public float MovementOpportunity { get; }

        public bool CanAfford(ActionCost cost)
        {
            return ActionPoints >= cost.ActionPoints
                && MovementOpportunity >= cost.MovementOpportunity;
        }

        public TurnBudget SpendAction(ActionCost cost)
        {
            if (!CanAfford(cost))
            {
                throw new InvalidOperationException("The turn budget cannot afford this action.");
            }

            return new TurnBudget(
                ActionPoints - cost.ActionPoints,
                MovementOpportunity - cost.MovementOpportunity);
        }

        public TurnBudget SpendMovement(float amount)
        {
            if (float.IsNaN(amount) || float.IsInfinity(amount) || amount < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(amount));
            }

            if (amount > MovementOpportunity)
            {
                throw new InvalidOperationException("The turn budget cannot afford this movement.");
            }

            return new TurnBudget(ActionPoints, MovementOpportunity - amount);
        }
    }
}
