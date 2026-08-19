using System;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;

namespace GritGud.Application.Gameplay
{
    internal interface IGameplayActionOutcomeValidator
    {
        Type OutcomeType { get; }

        void Validate(
            GameplayActionRecord action,
            GameplayActionOutcome outcome);
    }

    internal abstract class GameplayActionOutcomeValidator<TOutcome> :
        IGameplayActionOutcomeValidator
        where TOutcome : GameplayActionOutcome
    {
        public Type OutcomeType => typeof(TOutcome);

        public void Validate(
            GameplayActionRecord action,
            GameplayActionOutcome outcome)
        {
            if (!(outcome is TOutcome typedOutcome))
            {
                throw new ArgumentException(
                    $"Expected outcome '{typeof(TOutcome).Name}'.",
                    nameof(outcome));
            }

            Validate(action, typedOutcome);
        }

        protected abstract void Validate(
            GameplayActionRecord action,
            TOutcome outcome);
    }

    internal static class GameplayActionValidationRules
    {
        public static bool TurnBudgetsMatch(
            TurnBudget left,
            TurnBudget right) =>
            left.ActionPoints == right.ActionPoints
            && left.MovementOpportunity == right.MovementOpportunity;

        public static bool ActionCostsMatch(ActionCost left, ActionCost right) =>
            left.ActionPoints == right.ActionPoints
            && left.MovementOpportunity == right.MovementOpportunity
            && left.Mobility == right.Mobility;

        public static ActionCost GetAttackActionCost(
            GameplaySession session,
            AttackDefinition attack,
            GameplayActionRecord action) =>
            ShouldChargeTurnCost(session, action)
                ? attack.TurnCost
                : WithoutTurnSpend(attack.TurnCost);

        public static ActionCost GetThrownExplosiveActionCost(
            GameplaySession session,
            ThrownExplosiveDefinition definition,
            GameplayActionRecord action) =>
            ShouldChargeTurnCost(session, action)
                ? definition.TurnCost
                : WithoutTurnSpend(definition.TurnCost);

        public static bool ShouldChargeTurnCost(
            GameplaySession session,
            GameplayActionRecord action) =>
            session.Mode == GameplaySessionMode.TurnBased
            || (!session.EncounterActive && session.ActionStartsEncounter(action));

        public static bool AccuracyDecayDefinitionsMatch(
            AccuracyDecayDefinition left,
            AccuracyDecayDefinition right) =>
            left != null
            && right != null
            && left.HalfLifeDistance == right.HalfLifeDistance
            && left.MinimumAccuracyPercent == right.MinimumAccuracyPercent;

        public static bool ProjectileDefinitionsMatch(
            ProjectileFlightDefinition left,
            ProjectileFlightDefinition right) =>
            left != null
            && right != null
            && string.Equals(left.Id, right.Id, StringComparison.Ordinal)
            && left.SpeedPerTurn == right.SpeedPerTurn
            && left.Radius == right.Radius
            && left.MaximumRange == right.MaximumRange
            && left.StandingLaunchHeight == right.StandingLaunchHeight
            && left.CrouchedLaunchHeight == right.CrouchedLaunchHeight
            && left.OpensEmergencyReactionWindow
                == right.OpensEmergencyReactionWindow
            && left.BlastRadius == right.BlastRadius
            && left.BlastWoundMovementPenalty
                == right.BlastWoundMovementPenalty
            && left.BlastIntegrityDamage == right.BlastIntegrityDamage;

        public static bool ThrownExplosiveDefinitionsMatch(
            ThrownExplosiveDefinition left,
            ThrownExplosiveDefinition right) =>
            left != null
            && right != null
            && string.Equals(left.Id, right.Id, StringComparison.Ordinal)
            && ActionCostsMatch(left.TurnCost, right.TurnCost)
            && left.MaximumRange == right.MaximumRange
            && left.StandingLaunchHeight == right.StandingLaunchHeight
            && left.CrouchedLaunchHeight == right.CrouchedLaunchHeight
            && left.BaseUncertaintyRadius == right.BaseUncertaintyRadius
            && left.UncertaintyPerMeter == right.UncertaintyPerMeter
            && left.BlastRadius == right.BlastRadius
            && left.BlastWoundMovementPenalty
                == right.BlastWoundMovementPenalty
            && left.BlastIntegrityDamage == right.BlastIntegrityDamage
            && SmokeFieldDefinitionsMatch(left.SmokeField, right.SmokeField)
            && FireFieldDefinitionsMatch(left.FireField, right.FireField);

        private static bool SmokeFieldDefinitionsMatch(
            SmokeFieldDefinition left,
            SmokeFieldDefinition right) =>
            left == null ? right == null : left.Matches(right);

        private static bool FireFieldDefinitionsMatch(
            FireFieldDefinition left,
            FireFieldDefinition right) =>
            left == null ? right == null : left.Matches(right);

        private static ActionCost WithoutTurnSpend(ActionCost cost) =>
            new ActionCost(0, 0f, cost.Mobility);
    }
}
