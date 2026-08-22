using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;
using NUnit.Framework;

namespace GritGud.Domain.Tests
{
    public sealed class LocalizedInjuryTests
    {
        [Test]
        public void SevereArmImpactDisablesThatHandWithoutLegDamage()
        {
            ActorInjuryState resulting = Apply(
                ActorInjuryState.CreateHealthy("target"),
                TargetRegionId.LeftArm,
                severity: 80,
                sequence: 1).Resulting;

            Assert.That(resulting.Capabilities.CanUseLeftHand, Is.False);
            Assert.That(resulting.Capabilities.CanUseRightHand, Is.True);
            Assert.That(
                resulting.GetRegion(TargetRegionId.LeftLeg).MotorFunction,
                Is.EqualTo(100));
            Assert.That(
                resulting.GetRegion(TargetRegionId.RightLeg).MotorFunction,
                Is.EqualTo(100));
            Assert.That(resulting.Capabilities.MovementCapacity,
                Is.GreaterThan(90));
        }

        [Test]
        public void SevereLegImpactReducesMobilityWithoutPreventingWeaponUse()
        {
            ActorInjuryState resulting = Apply(
                ActorInjuryState.CreateHealthy("target"),
                TargetRegionId.LeftLeg,
                severity: 80,
                sequence: 1).Resulting;

            Assert.That(resulting.Capabilities.MovementCapacity,
                Is.LessThan(60));
            Assert.That(resulting.Capabilities.StandingCapacity,
                Is.LessThan(25));
            Assert.That(resulting.Capabilities.CanUseTwoHandedWeapon, Is.True);
            Assert.That(resulting.LifeState, Is.EqualTo(ActorLifeState.Active));
        }

        [Test]
        public void DamageToBothArmsPreventsTwoHandedWeaponUse()
        {
            ActorInjuryState state = ActorInjuryState.CreateHealthy("target");
            state = Apply(state, TargetRegionId.LeftArm, 75, 1).Resulting;
            state = Apply(state, TargetRegionId.RightArm, 75, 2).Resulting;

            Assert.That(state.Capabilities.CanUseLeftHand, Is.False);
            Assert.That(state.Capabilities.CanUseRightHand, Is.False);
            Assert.That(state.Capabilities.CanUseTwoHandedWeapon, Is.False);
            Assert.That(state.LifeState, Is.EqualTo(ActorLifeState.Active));
        }

        [Test]
        public void ArmInjuriesGateRifleReloadAndThrowWithoutLegPenalty()
        {
            ActorInjuryState state = ActorInjuryState.CreateHealthy("target");
            state = Apply(state, TargetRegionId.LeftArm, 75, 1).Resulting;
            state = Apply(state, TargetRegionId.RightArm, 75, 2).Resulting;
            var rifle = new AttackDefinition(
                "weapon.rifle",
                "Rifle",
                new ActionCost(1, 0f, ActionMobility.Set),
                woundMovementPenalty: 1f,
                accuracyDecay: AccuracyDecayDefinition.None);

            Assert.That(GameplayInjuryCapabilityProjection.CanUseAttack(
                state.Capabilities,
                rifle), Is.False);
            Assert.That(state.Capabilities.ReloadCapacity, Is.LessThan(30));
            Assert.That(state.Capabilities.ThrowCapacity, Is.LessThan(30));
            Assert.That(GameplayInjuryCapabilityProjection
                .CalculateMovementAllowance(8f, state.Capabilities),
                Is.GreaterThan(7f));
        }

        [Test]
        public void DamageToBothLegsCanImmobilizeAConsciousActor()
        {
            ActorInjuryState state = ActorInjuryState.CreateHealthy("target");
            state = Apply(state, TargetRegionId.LeftLeg, 80, 1).Resulting;
            state = Apply(state, TargetRegionId.RightLeg, 80, 2).Resulting;

            Assert.That(state.Capabilities.MovementCapacity, Is.Zero);
            Assert.That(state.Capabilities.CanStand, Is.False);
            Assert.That(state.Physiology.Consciousness, Is.EqualTo(100));
            Assert.That(state.LifeState, Is.EqualTo(ActorLifeState.Active));
        }

        [Test]
        public void BleedingAccumulatesAcrossCanonicalAdvances()
        {
            ActorInjuryState state = ActorInjuryState.CreateHealthy("target");
            state = Apply(state, TargetRegionId.LeftArm, 24, 1).Resulting;
            state = Apply(state, TargetRegionId.RightLeg, 24, 2).Resulting;
            int bloodAfterImpacts = state.Physiology.BloodReserve;
            int shockAfterImpacts = state.Physiology.Shock;

            state = ActorInjuryRules.AdvanceSystemic(state);
            state = ActorInjuryRules.AdvanceSystemic(state);

            Assert.That(state.Physiology.BloodReserve,
                Is.LessThan(bloodAfterImpacts));
            Assert.That(state.Physiology.Shock,
                Is.GreaterThan(shockAfterImpacts));
        }

        [Test]
        public void CatastrophicVitalImpactUsesExplicitLifeState()
        {
            ActorInjuryState dead = Apply(
                ActorInjuryState.CreateHealthy("target"),
                TargetRegionId.Head,
                severity: 95,
                sequence: 1).Resulting;
            ActorInjuryState incapacitated = Apply(
                ActorInjuryState.CreateHealthy("other"),
                TargetRegionId.Torso,
                severity: 85,
                sequence: 2,
                actorId: "other").Resulting;

            Assert.That(dead.LifeState, Is.EqualTo(ActorLifeState.Dead));
            Assert.That(incapacitated.LifeState,
                Is.EqualTo(ActorLifeState.Incapacitated));
            Assert.That(incapacitated.LifeState,
                Is.Not.EqualTo(ActorLifeState.Dead));
        }

        [Test]
        public void IdenticalImpactInputsProduceIdenticalInjuryState()
        {
            ActorInjuryState initial = ActorInjuryState.CreateHealthy("target");

            ActorInjuryResolution first = Apply(
                initial,
                TargetRegionId.Torso,
                severity: 61,
                sequence: 7);
            ActorInjuryResolution repeated = Apply(
                initial,
                TargetRegionId.Torso,
                severity: 61,
                sequence: 7);

            Assert.That(first.Resulting.HasSameState(repeated.Resulting),
                Is.True);
            Assert.That(first.Injury.InjuryId,
                Is.EqualTo(repeated.Injury.InjuryId));
            Assert.That(
                LegacyWoundProjection.From(first.Resulting).HasSameState(
                    LegacyWoundProjection.From(repeated.Resulting)),
                Is.True);
        }

        [Test]
        public void LegacyProjectionDoesNotDriveLifeState()
        {
            ActorInjuryState active = Apply(
                ActorInjuryState.CreateHealthy("target"),
                TargetRegionId.LeftArm,
                severity: 20,
                sequence: 1).Resulting;
            ActorWoundSnapshot compatibility = LegacyWoundProjection.From(active);

            Assert.That(compatibility.WoundCount, Is.EqualTo(1));
            Assert.That(active.LifeState, Is.EqualTo(ActorLifeState.Active));
        }

        private static ActorInjuryResolution Apply(
            ActorInjuryState previous,
            TargetRegionId region,
            int severity,
            long sequence,
            string actorId = "target")
        {
            var impact = new LocalizedImpact(
                "combat:" + sequence + ":attacker:" + actorId,
                "attacker",
                actorId,
                "weapon.rifle",
                region,
                DamageMechanism.Ballistic,
                severity,
                sequence);
            return ActorInjuryRules.ApplyImpact(
                previous,
                impact,
                compatibilityMovementPenalty: 1f);
        }
    }
}
