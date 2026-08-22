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
            WeaponHandlingProfileDefinition handling = attack.HandlingProfile;
            bool primaryIsLeft = handling.PrimaryHand
                == WeaponPrimaryHand.Left;
            int primaryGrip = primaryIsLeft
                ? capabilities.LeftGripCapacity
                : capabilities.RightGripCapacity;
            int supportGrip = primaryIsLeft
                ? capabilities.RightGripCapacity
                : capabilities.LeftGripCapacity;
            bool primaryUsable = primaryIsLeft
                ? capabilities.CanUseLeftHand
                : capabilities.CanUseRightHand;
            bool supportUsable = primaryIsLeft
                ? capabilities.CanUseRightHand
                : capabilities.CanUseLeftHand;
            if (!primaryUsable
                || primaryGrip < handling.MinimumPrimaryGrip
                || capabilities.AimStability
                    < handling.MinimumAimStability)
            {
                return false;
            }
            if (handling.RequiredHands == 1)
                return true;
            return (supportUsable
                    && supportGrip >= handling.MinimumSupportGrip)
                || handling.CanBraceWithOneHand;
        }

        public static bool CanThrowExplosive(
            ActorCapabilityState capabilities,
            int minimumArmMotor = 35,
            int minimumThrowCapacity = 30)
        {
            if (capabilities == null) throw new ArgumentNullException(
                nameof(capabilities));
            if (minimumArmMotor < 0 || minimumArmMotor > 100)
                throw new ArgumentOutOfRangeException(nameof(minimumArmMotor));
            if (minimumThrowCapacity < 0 || minimumThrowCapacity > 100)
                throw new ArgumentOutOfRangeException(
                    nameof(minimumThrowCapacity));
            bool left = capabilities.CanUseLeftHand
                && capabilities.LeftGripCapacity >= minimumArmMotor
                && capabilities.LeftThrowCapacity >= minimumThrowCapacity;
            bool right = capabilities.CanUseRightHand
                && capabilities.RightGripCapacity >= minimumArmMotor
                && capabilities.RightThrowCapacity >= minimumThrowCapacity;
            return left || right;
        }

        public static int CalculateConditionPercent(ActorInjuryState injuries)
        {
            if (injuries == null) throw new ArgumentNullException(
                nameof(injuries));
            if (injuries.LifeState == ActorLifeState.Dead) return 0;
            int structuralTotal = 0;
            foreach (TargetRegionId region in Enum.GetValues(
                typeof(TargetRegionId)))
                structuralTotal += injuries.GetRegion(region)
                    .StructuralIntegrity;
            int structuralAverage = structuralTotal / 6;
            return (injuries.Physiology.BloodReserve
                + (100 - injuries.Physiology.Shock)
                + injuries.Physiology.Consciousness
                + injuries.Physiology.Respiration
                + structuralAverage) / 5;
        }
    }
}
