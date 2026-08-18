using System;
using GritGud.Domain.Gameplay;

namespace GritGud.Application.Gameplay
{
    internal sealed class GameplayAttackOutcomeValidator :
        GameplayActionOutcomeValidator<AttackResolvedActionOutcome>
    {
        private readonly GameplaySession session;

        public GameplayAttackOutcomeValidator(GameplaySession session)
        {
            this.session = session ?? throw new ArgumentNullException(
                nameof(session));
        }

        protected override void Validate(
            GameplayActionRecord action,
            AttackResolvedActionOutcome outcome)
        {
            AttackResolutionRecord attack = outcome.Attack;
            if (attack == null)
            {
                throw new InvalidOperationException(
                    "Attack outcomes require a resolution record.");
            }

            if (!string.Equals(
                    action.Request.ActorId,
                    attack.AttackerId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    action.Request.TargetId,
                    attack.TargetId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "The attack record does not match its action request.");
            }

            AttackDefinition equippedAttack = session.GetEquippedAttack(
                attack.AttackerId);
            if (equippedAttack == null
                || !string.Equals(
                    equippedAttack.ActionId,
                    action.Request.ActionId,
                    StringComparison.Ordinal)
                || !GameplayActionValidationRules.ActionCostsMatch(
                    action.Cost,
                    GameplayActionValidationRules.GetAttackActionCost(
                        session,
                        equippedAttack,
                        action))
                || !GameplayActionValidationRules.AccuracyDecayDefinitionsMatch(
                    equippedAttack.AccuracyDecay,
                    attack.AccuracyDecay))
            {
                throw new InvalidOperationException(
                    "The actor does not own the recorded attack action.");
            }

            GameplayActorState target = session.RequireActor(attack.TargetId);
            GameplayActorState attacker = session.RequireActor(attack.AttackerId);
            if (attacker.Pose.Position.DistanceTo(target.Pose.Position)
                != attack.Distance)
            {
                throw new InvalidOperationException(
                    "The attack distance no longer matches the authoritative actor positions.");
            }

            if (!target.Wounds.HasSameState(attack.TargetWoundsBefore))
            {
                throw new InvalidOperationException(
                    "The attack no longer begins at the target's authoritative wound state.");
            }
        }
    }

    internal sealed class GameplayWeaponDischargeOutcomeValidator :
        GameplayActionOutcomeValidator<WeaponDischargedActionOutcome>
    {
        private readonly GameplaySession session;

        public GameplayWeaponDischargeOutcomeValidator(GameplaySession session)
        {
            this.session = session ?? throw new ArgumentNullException(
                nameof(session));
        }

        protected override void Validate(
            GameplayActionRecord action,
            WeaponDischargedActionOutcome outcome)
        {
            WeaponDischargeRecord discharge = outcome.Discharge;
            if (discharge == null
                || !string.Equals(
                    action.Request.ActorId,
                    discharge.AttackerId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    action.Request.ActionId,
                    discharge.ActionId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    action.Request.TargetId,
                    discharge.TargetId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "The weapon discharge does not match its action request.");
            }

            AttackDefinition equippedAttack = session.GetEquippedAttack(
                discharge.AttackerId);
            GameplayActorState attacker = session.RequireActor(
                discharge.AttackerId);
            if (equippedAttack == null
                || equippedAttack.Projectile != null
                || !GameplayActionValidationRules.ActionCostsMatch(
                    action.Cost,
                    GameplayActionValidationRules.GetAttackActionCost(
                        session,
                        equippedAttack,
                        action))
                || !string.Equals(
                    equippedAttack.ActionId,
                    discharge.ActionId,
                    StringComparison.Ordinal)
                || attacker.Pose.Position.DistanceTo(discharge.Origin) > 0f)
            {
                throw new InvalidOperationException(
                    "The actor does not own the recorded immediate weapon discharge.");
            }
        }
    }

    internal sealed class GameplayProjectileLaunchOutcomeValidator :
        GameplayActionOutcomeValidator<ProjectileLaunchedActionOutcome>
    {
        private readonly GameplaySession session;

        public GameplayProjectileLaunchOutcomeValidator(GameplaySession session)
        {
            this.session = session ?? throw new ArgumentNullException(
                nameof(session));
        }

        protected override void Validate(
            GameplayActionRecord action,
            ProjectileLaunchedActionOutcome outcome)
        {
            ProjectileLaunchRecord launch = outcome.Launch;
            if (launch == null
                || !string.Equals(
                    action.Request.ActorId,
                    launch.AttackerId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    action.Request.TargetId,
                    launch.IntendedTargetId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    action.Request.ActionId,
                    launch.ActionId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "The projectile launch does not match its action request.");
            }

            AttackDefinition weapon = session.GetEquippedAttack(
                launch.AttackerId);
            if (weapon?.Projectile == null
                || !string.Equals(
                    weapon.ActionId,
                    launch.ActionId,
                    StringComparison.Ordinal)
                || !GameplayActionValidationRules.ActionCostsMatch(
                    action.Cost,
                    weapon.TurnCost)
                || !GameplayActionValidationRules.ProjectileDefinitionsMatch(
                    launch.Definition,
                    weapon.Projectile))
            {
                throw new InvalidOperationException(
                    "The actor does not own the recorded projectile weapon.");
            }

            GameplayActorState attacker = session.RequireActor(
                launch.AttackerId);
            GameplayPosition expectedOrigin = weapon.Projectile.GetLaunchOrigin(
                attacker.Pose);
            if (expectedOrigin.DistanceTo(launch.Origin) > 0f)
            {
                throw new InvalidOperationException(
                    "The projectile launch no longer starts at the attacker's authored launch point.");
            }
        }
    }
}
