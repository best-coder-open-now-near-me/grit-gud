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
                Is.LessThan(50));
            Assert.That(resulting.Capabilities.Mobility.Gait,
                Is.EqualTo(ActorGait.SevereLimp));
            Assert.That(resulting.Capabilities.CanStand, Is.True);
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
        public void MobilityProjectionUsesDeterministicLimpAndCrawlTiers()
        {
            ActorInjuryState mild = WithMotorLoss(
                leftLegLoss: 55,
                rightLegLoss: 0);
            ActorInjuryState severe = WithMotorLoss(
                leftLegLoss: 100,
                rightLegLoss: 0);
            ActorInjuryState crawling = WithMotorLoss(
                leftLegLoss: 70,
                rightLegLoss: 65);
            ActorInjuryState immobile = WithMotorLoss(
                leftLegLoss: 95,
                rightLegLoss: 95);

            Assert.That(mild.Capabilities.Mobility.Gait,
                Is.EqualTo(ActorGait.MildLimp));
            Assert.That(mild.Capabilities.Mobility.ImpairedSide,
                Is.EqualTo(ActorImpairedSide.Left));
            Assert.That(mild.Capabilities.Mobility.MovementPercent,
                Is.EqualTo(75));
            Assert.That(mild.Capabilities.Mobility.CanSprint, Is.False);
            Assert.That(severe.Capabilities.Mobility.Gait,
                Is.EqualTo(ActorGait.SevereLimp));
            Assert.That(severe.Capabilities.Mobility.MovementPercent,
                Is.EqualTo(35));
            Assert.That(crawling.Capabilities.Mobility.Gait,
                Is.EqualTo(ActorGait.Crawling));
            Assert.That(crawling.Capabilities.Mobility.CanStand, Is.False);
            Assert.That(immobile.Capabilities.Mobility.Gait,
                Is.EqualTo(ActorGait.Immobile));
            Assert.That(immobile.Capabilities.Mobility.MovementPercent,
                Is.Zero);
        }

        [Test]
        public void AuthoredWeaponProfilesEvaluateSpecificHandsAndThresholds()
        {
            var rifle = AttackWithHandling(new WeaponHandlingProfileDefinition(
                1, 2, WeaponPrimaryHand.Right, 35, 35, 25, 25,
                canBraceWithOneHand: false,
                canFireProne: true));
            var launcher = AttackWithHandling(
                new WeaponHandlingProfileDefinition(
                    1, 2, WeaponPrimaryHand.Right, 50, 50, 35, 40,
                    canBraceWithOneHand: false,
                    canFireProne: true));
            var impaired = new ActorCapabilityState(
                100, 100, 34, 45, 39, 70,
                true, true, true, true,
                new ActorMobilityCapability(
                    ActorGait.Normal,
                    ActorImpairedSide.None,
                    100,
                    100,
                    canSprint: true,
                    canStand: true),
                leftGripCapacity: 40,
                rightGripCapacity: 50,
                leftThrowCapacity: 70,
                rightThrowCapacity: 20,
                isActive: true);

            Assert.That(GameplayInjuryCapabilityProjection.CanUseAttack(
                impaired, rifle), Is.True);
            Assert.That(GameplayInjuryCapabilityProjection.CanUseAttack(
                impaired, launcher), Is.False);
        }

        [Test]
        public void ExplosivesCanUseEitherFunctionalThrowingArm()
        {
            var leftOnly = new ActorCapabilityState(
                100, 100, 100, 50, 0, 80,
                true, true, false, false,
                new ActorMobilityCapability(
                    ActorGait.Normal,
                    ActorImpairedSide.None,
                    100,
                    100,
                    canSprint: true,
                    canStand: true),
                leftGripCapacity: 80,
                rightGripCapacity: 0,
                leftThrowCapacity: 80,
                rightThrowCapacity: 0,
                isActive: true);

            Assert.That(GameplayInjuryCapabilityProjection
                .CanThrowExplosive(leftOnly), Is.True);
        }

        [Test]
        public void UnsupportedStandingPoseProjectsToCanonicalCrouchedPosture()
        {
            ActorInjuryState crawling = WithMotorLoss(
                leftLegLoss: 70,
                rightLegLoss: 65);
            ActorWoundSnapshot wounds = LegacyWoundProjection.From(crawling);
            var snapshot = new GameplayActorSnapshot(
                "target",
                new GameplayActorPose(
                    new GameplayPosition(1f, 0f, 2f),
                    45f,
                    ActorStance.Standing),
                new TurnBudget(2, 1f),
                wounds,
                equippedItemId: null,
                equipmentEffects: EquipmentEffectSet.None,
                injuries: crawling);

            Assert.That(snapshot.Pose.Stance,
                Is.EqualTo(ActorStance.Crouched));
            Assert.That(snapshot.Pose.Position.X, Is.EqualTo(1f));
            Assert.That(snapshot.Pose.FacingDegrees, Is.EqualTo(45f));
        }

        [Test]
        public void LifeStateDoesNotEraseLocalizedLimbGait()
        {
            ActorInjuryState active = WithMotorLoss(
                leftLegLoss: 55,
                rightLegLoss: 0);
            var incapacitated = new ActorInjuryState(
                active.ActorId,
                active.Injuries,
                active.Physiology,
                ActorLifeState.Incapacitated);

            Assert.That(incapacitated.Capabilities.IsActive, Is.False);
            Assert.That(incapacitated.Capabilities.Mobility.Gait,
                Is.EqualTo(ActorGait.MildLimp));
            Assert.That(incapacitated.Capabilities.MovementCapacity, Is.Zero);
            Assert.That(incapacitated.Capabilities.CanStand, Is.False);
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

        private static ActorInjuryState WithMotorLoss(
            int leftLegLoss,
            int rightLegLoss)
        {
            var injuries = new System.Collections.Generic.List<InjuryRecord>();
            if (leftLegLoss > 0)
                injuries.Add(new InjuryRecord(
                    "left-leg",
                    "event:left-leg",
                    TargetRegionId.LeftLeg,
                    DamageMechanism.Ballistic,
                    50,
                    0,
                    leftLegLoss,
                    0,
                    0,
                    false,
                    1f));
            if (rightLegLoss > 0)
                injuries.Add(new InjuryRecord(
                    "right-leg",
                    "event:right-leg",
                    TargetRegionId.RightLeg,
                    DamageMechanism.Ballistic,
                    50,
                    0,
                    rightLegLoss,
                    0,
                    0,
                    false,
                    1f));
            return new ActorInjuryState(
                "target",
                injuries,
                ActorPhysiologyState.Healthy,
                ActorLifeState.Active);
        }

        private static AttackDefinition AttackWithHandling(
            WeaponHandlingProfileDefinition handling) =>
            new AttackDefinition(
                "weapon.test",
                "Test Weapon",
                new ActionCost(1, 0f, ActionMobility.Set),
                woundMovementPenalty: 1f,
                accuracyDecay: AccuracyDecayDefinition.None,
                handlingProfile: handling);
    }
}
