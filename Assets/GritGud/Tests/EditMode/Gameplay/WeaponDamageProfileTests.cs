using System.Collections.Generic;
using GritGud.Domain.Gameplay;
using NUnit.Framework;

namespace GritGud.Domain.Tests.Gameplay
{
    public sealed class WeaponDamageProfileTests
    {
        [Test]
        public void StandardRifleLimbHitsFollowThirtyPointTraumaCalibration()
        {
            WeaponDamageProfileDefinition rifle = CreateStandardRifle();
            ActorInjuryState state = ActorInjuryState.CreateHealthy("target");

            state = Apply(state, rifle, TargetRegionId.LeftLeg, 1).Resulting;
            Assert.That(state.SystemicTrauma, Is.EqualTo(30));
            Assert.That(state.GetRegion(TargetRegionId.LeftLeg).MotorFunction,
                Is.EqualTo(45));
            Assert.That(state.LifeState, Is.EqualTo(ActorLifeState.Active));

            state = Apply(state, rifle, TargetRegionId.LeftLeg, 2).Resulting;
            Assert.That(state.SystemicTrauma, Is.EqualTo(60));
            Assert.That(state.GetRegion(TargetRegionId.LeftLeg).MotorFunction,
                Is.Zero);
            Assert.That(state.LifeState, Is.EqualTo(ActorLifeState.Active));

            state = Apply(state, rifle, TargetRegionId.RightArm, 3).Resulting;
            Assert.That(state.SystemicTrauma, Is.EqualTo(90));
            Assert.That(state.LifeState,
                Is.EqualTo(ActorLifeState.Incapacitated));

            state = Apply(state, rifle, TargetRegionId.RightArm, 4).Resulting;
            Assert.That(state.SystemicTrauma, Is.EqualTo(120));
            Assert.That(state.LifeState, Is.EqualTo(ActorLifeState.Dead));
        }

        [Test]
        public void StandardRifleTorsoHitsIncapacitateThenKill()
        {
            WeaponDamageProfileDefinition rifle = CreateStandardRifle();
            ActorInjuryState state = ActorInjuryState.CreateHealthy("target");

            state = Apply(state, rifle, TargetRegionId.Torso, 1).Resulting;
            Assert.That(state.SystemicTrauma, Is.EqualTo(40));
            Assert.That(state.LifeState, Is.EqualTo(ActorLifeState.Active));

            state = Apply(state, rifle, TargetRegionId.Torso, 2).Resulting;
            Assert.That(state.SystemicTrauma, Is.EqualTo(80));
            Assert.That(state.LifeState,
                Is.EqualTo(ActorLifeState.Incapacitated));

            state = Apply(state, rifle, TargetRegionId.Torso, 3).Resulting;
            Assert.That(state.SystemicTrauma, Is.EqualTo(120));
            Assert.That(state.LifeState, Is.EqualTo(ActorLifeState.Dead));
        }

        [Test]
        public void StandardRifleHeadHitUsesExplicitCriticalPathAndSecondHitKills()
        {
            WeaponDamageProfileDefinition rifle = CreateStandardRifle();
            ActorInjuryState state = ActorInjuryState.CreateHealthy("target");

            state = Apply(state, rifle, TargetRegionId.Head, 1).Resulting;
            Assert.That(state.SystemicTrauma, Is.EqualTo(50));
            Assert.That(state.LifeState,
                Is.EqualTo(ActorLifeState.Incapacitated));

            state = Apply(state, rifle, TargetRegionId.Head, 2).Resulting;
            Assert.That(state.SystemicTrauma, Is.EqualTo(100));
            Assert.That(state.LifeState, Is.EqualTo(ActorLifeState.Dead));
        }

        [Test]
        public void IdenticalProfileImpactProducesExactlyTheSameCanonicalState()
        {
            WeaponDamageProfileDefinition rifle = CreateStandardRifle();
            ActorInjuryState initial = ActorInjuryState.CreateHealthy("target");

            ActorInjuryResolution first = Apply(
                initial,
                rifle,
                TargetRegionId.RightArm,
                17);
            ActorInjuryResolution repeated = Apply(
                initial,
                rifle,
                TargetRegionId.RightArm,
                17);

            Assert.That(first.Resulting.HasSameState(repeated.Resulting), Is.True);
            Assert.That(first.Injury.SystemicTraumaContribution, Is.EqualTo(30));
            Assert.That(first.Delta.PreviousSystemicTrauma, Is.Zero);
            Assert.That(first.Delta.ResultingSystemicTrauma, Is.EqualTo(30));
        }

        private static ActorInjuryResolution Apply(
            ActorInjuryState previous,
            WeaponDamageProfileDefinition rifle,
            TargetRegionId region,
            long sequence)
        {
            int impact = rifle.ResolveTransferredImpact(distance: 75f);
            return ActorInjuryRules.ApplyImpact(
                previous,
                new LocalizedImpact(
                    "combat:" + sequence + ":rifle:target",
                    "attacker",
                    "target",
                    "weapon.rifle",
                    region,
                    rifle.Mechanism,
                    impact,
                    sequence),
                rifle);
        }

        private static WeaponDamageProfileDefinition CreateStandardRifle()
        {
            var consequences = new List<RegionConsequenceProfile>
            {
                new RegionConsequenceProfile(
                    TargetRegionId.Head,
                    50, 45, 5, 60, 20, 60, 0,
                    criticalIncapacitationImpact: 100,
                    vitalImpact: 100),
                new RegionConsequenceProfile(
                    TargetRegionId.Torso,
                    40, 40, 10, 5, 40, 0, 35),
                new RegionConsequenceProfile(
                    TargetRegionId.LeftArm,
                    30, 40, 55, 10, 20, 0, 0),
                new RegionConsequenceProfile(
                    TargetRegionId.RightArm,
                    30, 40, 55, 10, 20, 0, 0),
                new RegionConsequenceProfile(
                    TargetRegionId.LeftLeg,
                    30, 45, 55, 5, 25, 0, 0),
                new RegionConsequenceProfile(
                    TargetRegionId.RightLeg,
                    30, 45, 55, 5, 25, 0, 0),
            };
            return new WeaponDamageProfileDefinition(
                WeaponDamageProfileDefinition.CurrentSchemaVersion,
                "damage.ballistic.rifle.standard",
                DamageMechanism.Ballistic,
                baseImpact: 100,
                penetration: 60,
                WeaponDamageRangeProfile.NoDecay,
                consequences);
        }
    }
}
