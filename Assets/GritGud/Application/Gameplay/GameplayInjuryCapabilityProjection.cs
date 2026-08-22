using System;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;

namespace GritGud.Application.Gameplay
{
    public static class GameplayInjuryCapabilityProjection
    {
        public static float CalculateMovementAllowance(
            float authoredMovementAllowance,
            ActorCapabilityState capabilities)
        {
            GameplayNumericPolicy.RequireFinite(
                authoredMovementAllowance,
                nameof(authoredMovementAllowance));
            if (authoredMovementAllowance < 0f)
                throw new ArgumentOutOfRangeException(
                    nameof(authoredMovementAllowance));
            if (capabilities == null) throw new ArgumentNullException(
                nameof(capabilities));
            return authoredMovementAllowance
                * capabilities.MovementCapacity / 100f;
        }

        public static TurnBudget LimitMovement(
            TurnBudget budget,
            float authoredMovementAllowance,
            ActorCapabilityState capabilities) => new TurnBudget(
                budget.ActionPoints,
                Math.Min(
                    budget.MovementOpportunity,
                    CalculateMovementAllowance(
                        authoredMovementAllowance,
                        capabilities)));

        public static int CalculateAccuracyDeltaPercent(
            ActorCapabilityState capabilities)
        {
            if (capabilities == null) throw new ArgumentNullException(
                nameof(capabilities));
            return -(100 - capabilities.AimStability) / 2;
        }

        public static bool CanUseAttack(
            ActorCapabilityState capabilities,
            AttackDefinition attack)
        {
            if (capabilities == null) throw new ArgumentNullException(
                nameof(capabilities));
            if (attack == null) throw new ArgumentNullException(nameof(attack));
            return attack.Contact == null
                ? capabilities.CanUseTwoHandedWeapon
                : capabilities.CanUseLeftHand
                    || capabilities.CanUseRightHand;
        }
    }
}
