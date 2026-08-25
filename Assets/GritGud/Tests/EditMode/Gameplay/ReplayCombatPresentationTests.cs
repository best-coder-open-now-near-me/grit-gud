using System;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;
using NUnit.Framework;

namespace GritGud.Domain.Tests
{
    public sealed class ReplayCombatPresentationTests
    {
        [Test]
        public void HitscanActionProjectsOneTimedDischarge()
        {
            var discharge = new WeaponDischargeRecord(
                sequence: 7,
                attackerId: "attacker",
                actionId: "attack.rifle",
                targetId: "target",
                origin: new GameplayPosition(1f, 2f, 3f),
                aimPoint: new GameplayPosition(4f, 2f, 3f));
            var action = new GameplayActionRecord(
                sequence: 7,
                new GameplayActionRequest(
                    "attacker",
                    "attack.rifle",
                    "target"),
                new ActionCost(1, 0f, ActionMobility.Set),
                new TurnBudget(2, 0f),
                new TurnBudget(1, 0f),
                new GameplayActionOutcome[]
                {
                    new WeaponDischargedActionOutcome(discharge),
                });

            ReplayCombatPresentationEvent presentationEvent =
                ReplayCombatPresentationEventProjector.Project(7, action)[0];

            Assert.That(
                presentationEvent.Kind,
                Is.EqualTo(ReplayCombatPresentationEventKind.WeaponDischarge));
            Assert.That(presentationEvent.ActorId, Is.EqualTo("attacker"));
            Assert.That(presentationEvent.TargetId, Is.EqualTo("target"));
            Assert.That(
                presentationEvent.NormalizedTime,
                Is.EqualTo(GameplaySemanticReplayPresentationTiming
                    .ActionResolutionProgress));
            Assert.That(
                presentationEvent.StableKey,
                Is.EqualTo(
                    "replay-combat:7:0:WeaponDischarge:Actor:attacker:Actor:target:"));
            Assert.That(presentationEvent.EventOrdinal, Is.Zero);
        }

        [Test]
        public void ProjectileImpactUsesArrivalTimeWithinAdvance()
        {
            ProjectileAdvanceRecord advance = CreateImpactAdvance();

            float progress = GameplaySemanticReplayPresentationTiming
                .GetProjectileImpactProgress(advance);
            ReplayCombatPresentationEvent presentationEvent =
                ReplayCombatPresentationEventProjector.Project(
                    9,
                    advance)[0];

            Assert.That(progress, Is.EqualTo(0.25f).Within(0.0001f));
            Assert.That(
                presentationEvent.Kind,
                Is.EqualTo(ReplayCombatPresentationEventKind.ProjectileImpact));
            Assert.That(
                presentationEvent.NormalizedTime,
                Is.EqualTo(progress));
            Assert.That(
                presentationEvent.StableKey,
                Is.EqualTo(
                    "replay-combat:9:0:ProjectileImpact:Actor:attacker:Actor:target:projectile.1"));
            Assert.That(
                GameplayProjectilePresentationSampler.Sample(
                    advance,
                    0.24f).Status,
                Is.EqualTo(ProjectileFlightStatus.InFlight));
            ProjectileFlightSnapshot atImpact =
                GameplayProjectilePresentationSampler.Sample(advance, 0.25f);
            Assert.That(
                atImpact.Status,
                Is.EqualTo(ProjectileFlightStatus.Impacted));
            Assert.That(
                atImpact.Position.DistanceTo(advance.Resulting.Position),
                Is.LessThan(0.0001f));
        }

        [Test]
        public void ProjectileImpactReactionStartsAtTheCollisionThreshold()
        {
            ProjectileAdvanceRecord advance = CreateImpactAdvance();
            GameplayActorSnapshot previous = CreateActor(
                "target",
                woundCount: 0,
                maximumWounds: 1);
            GameplayActorSnapshot resulting = CreateActor(
                "target",
                woundCount: 1,
                maximumWounds: 1);

            TurnReplayActorActionState reaction =
                TurnReplayActorActionProjector
                    .ProjectProjectileImpactReactions(
                        advance,
                        sequence: 9,
                        progress: 0.25f,
                        new[] { previous },
                        new[] { resulting })[0];

            Assert.That(
                reaction.EventNormalizedTime,
                Is.EqualTo(GameplaySemanticReplayPresentationTiming
                    .GetProjectileImpactProgress(advance)));
            Assert.That(reaction.ActorId, Is.EqualTo("target"));
            Assert.That(reaction.ResultingWoundCount, Is.EqualTo(1));
            Assert.That(resulting.IsIncapacitated, Is.True);
        }

        [Test]
        public void RangedReactionWaitsForTheSharedResolutionEvent()
        {
            var state = new TurnReplayActorActionState(
                "target",
                TurnReplayActorActionKind.Reaction,
                journalSequence: 3,
                normalizedProgress: 0.5f,
                contactReaction: false,
                resultingWoundCount: 1);

            Assert.That(
                state.EventNormalizedTime,
                Is.EqualTo(GameplaySemanticReplayPresentationTiming
                    .ActionResolutionProgress));
        }

        [Test]
        public void DroneCrashConsequencesWaitForGroundImpact()
        {
            var crash = new DroneCrashDefinition(
                impactRadius: 2f,
                injuryMovementPenalty: 0.5f,
                destructibleIntegrityDamage: 1f,
                maximumActionPointReduction: 1,
                maximumDriftDistance: 2f,
                impactPlaybackSeconds: 0.75f);
            var definition = new DroneArchetypeDefinition(
                "drone.scout",
                maximumIntegrity: 6f,
                maximumMoveDistance: 5f,
                moveCost: new ActionCost(1, 0f, ActionMobility.Mobile),
                sensor: new DroneSensorDefinition(14f, 120f),
                attack: new AttackDefinition(
                    "attack.drone.light",
                    "Drone light weapon",
                    new ActionCost(1, 0f, ActionMobility.Set),
                    woundMovementPenalty: 1f,
                    accuracyDecay: AccuracyDecayDefinition.None),
                presentationId: "presentation.drone.scout",
                crash: crash);
            var trajectory = new DroneCrashTrajectoryRecord(
                new GameplayPosition(1f, 2f, 1f),
                new GameplayPosition(2f, 0f, 1f),
                disabledTransitionSequence: 18L);
            var crashing = new SummonedDroneSnapshot(
                definition,
                "drone:controller:1",
                "ability.summon-drone",
                new DroneTurnPartnership("controller"),
                trajectory.Origin,
                facingDegrees: 0f,
                remainingIntegrity: 0f,
                lifecycle: SummonLifecycleState.Crashing,
                crashTrajectory: trajectory);
            SummonedDroneSnapshot destroyed = crashing.WithLifecycle(
                SummonLifecycleState.Destroyed,
                0f,
                null,
                trajectory,
                trajectory.ImpactPosition);
            var record = new DroneCrashImpactRecord(
                19L,
                crashing,
                destroyed,
                crash,
                Array.Empty<BlastEffectRecord>(),
                Array.Empty<ConcussiveActionPointEffectRecord>());

            Assert.That(
                GameplaySemanticReplayPresentationTiming
                    .GetResolutionProgress(record),
                Is.EqualTo(1f));
            ReplayCombatPresentationEvent presentationEvent =
                ReplayCombatPresentationEventProjector.Project(19L, record)[0];
            Assert.That(presentationEvent.NormalizedTime, Is.EqualTo(1f));
        }

        [Test]
        public void TargetFacingPhaseTurnsOnTheShortestArcByRelease()
        {
            ActorTargetFacingActionPhase phase =
                GameplayThrownExplosivePresentationTiming.CreateFacingPhase(
                    startingFacingDegrees: 350f,
                    targetFacingDegrees: 90f);

            Assert.That(
                phase.SampleFacingDegrees(0f),
                Is.EqualTo(350f).Within(0.001f));
            Assert.That(
                phase.SampleFacingDegrees(
                    phase.ReleaseNormalizedTime * 0.5f),
                Is.EqualTo(40f).Within(0.001f));
            Assert.That(
                phase.SampleFacingDegrees(phase.ReleaseNormalizedTime),
                Is.EqualTo(90f).Within(0.001f));
            Assert.That(
                phase.SampleFacingDegrees(1f),
                Is.EqualTo(90f).Within(0.001f));
            Assert.That(
                phase.GetPhase(phase.ReleaseNormalizedTime * 0.5f),
                Is.EqualTo(ActorActionPresentationPhase.WindUp));
            Assert.That(
                phase.GetPhase(
                    (phase.ReleaseNormalizedTime
                        + phase.RecoveryEndNormalizedTime) * 0.5f),
                Is.EqualTo(ActorActionPresentationPhase.Recovery));
            Assert.That(
                phase.SampleActionProgress(
                    phase.RecoveryEndNormalizedTime),
                Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void ThrownExplosiveProjectsDeterministicReleaseAndImpact()
        {
            GameplayActionRecord action = CreateThrownAction();

            var events = ReplayCombatPresentationEventProjector.Project(
                transitionSequence: 12,
                action);

            Assert.That(events, Has.Count.EqualTo(2));
            Assert.That(
                events[0].Kind,
                Is.EqualTo(ReplayCombatPresentationEventKind
                    .ThrownExplosiveRelease));
            Assert.That(
                events[0].NormalizedTime,
                Is.EqualTo(GameplayThrownExplosivePresentationTiming
                    .ReleaseNormalizedTime).Within(0.0001f));
            Assert.That(
                events[1].Kind,
                Is.EqualTo(ReplayCombatPresentationEventKind
                    .ThrownExplosiveImpact));
            Assert.That(
                events[1].NormalizedTime,
                Is.EqualTo(GameplayThrownExplosivePresentationTiming
                    .ImpactNormalizedTime).Within(0.0001f));
            Assert.That(events[0].ProjectileId, Is.EqualTo(
                "thrown-explosive:thrower:4"));
            Assert.That(events[1].ProjectileId,
                Is.EqualTo(events[0].ProjectileId));
            Assert.That(events[0].StableKey,
                Is.Not.EqualTo(events[1].StableKey));
            Assert.That(
                GameplaySemanticReplayPresentationTiming
                    .GetActionResolutionProgress(action),
                Is.EqualTo(GameplayThrownExplosivePresentationTiming
                    .ImpactNormalizedTime).Within(0.0001f));
        }

        [Test]
        public void TerminalPoseEpisodeSurvivesStatusChangesUntilRecovery()
        {
            ReplayActorLifeStateEvent Event(
                long sequence,
                ActorLifeState previous,
                ActorLifeState resulting,
                float timeSeconds,
                TargetRegionId? region) => new ReplayActorLifeStateEvent(
                new ReplayActorLifeStateTransition(
                    sequence,
                    "target",
                    previous,
                    resulting,
                    normalizedTime: 0.5f,
                    region,
                    region.HasValue
                        ? DamageMechanism.Ballistic
                        : (DamageMechanism?)null),
                timeSeconds);

            ReplayActorLifeStateEvent[] lifeStateEvents =
            {
                Event(
                    1,
                    ActorLifeState.Active,
                    ActorLifeState.Incapacitated,
                    1f,
                    TargetRegionId.Torso),
                Event(
                    2,
                    ActorLifeState.Incapacitated,
                    ActorLifeState.Dead,
                    1.2f,
                    TargetRegionId.Head),
                Event(
                    3,
                    ActorLifeState.Dead,
                    ActorLifeState.Active,
                    1.6f,
                    region: null),
                Event(
                    4,
                    ActorLifeState.Active,
                    ActorLifeState.Dead,
                    2f,
                    TargetRegionId.LeftLeg),
            };

            var episodes = ReplayActorTerminalPoseEpisodeProjector.Project(
                lifeStateEvents);

            Assert.That(episodes, Has.Count.EqualTo(2));
            Assert.That(
                episodes[0].EpisodeId,
                Is.EqualTo("terminal:target:1"));
            Assert.That(
                episodes[0].EnteredLifeState,
                Is.EqualTo(ActorLifeState.Incapacitated));
            Assert.That(
                episodes[0].PoseKind,
                Is.EqualTo(ReplayActorTerminalPoseKind.ShoulderFall));
            Assert.That(episodes[0].HitRegion, Is.EqualTo(TargetRegionId.Torso));
            Assert.That(episodes[0].RecoveryTimeSeconds, Is.EqualTo(1.6f));
            Assert.That(episodes[0].Contains(1.5f), Is.True);
            Assert.That(episodes[0].Contains(1.6f), Is.False);
            Assert.That(
                episodes[1].EpisodeId,
                Is.EqualTo("terminal:target:4"));
            Assert.That(
                episodes[1].PoseKind,
                Is.EqualTo(ReplayActorTerminalPoseKind.FallOver));
        }

        private static GameplayActionRecord CreateThrownAction()
        {
            var cost = new ActionCost(1, 0f, ActionMobility.Mobile);
            var definition = new ThrownExplosiveDefinition(
                "item.test-grenade",
                cost,
                maximumRange: 10f,
                standingLaunchHeight: 1.2f,
                crouchedLaunchHeight: 0.8f,
                baseUncertaintyRadius: 0.5f,
                uncertaintyPerMeter: 0.1f,
                blastRadius: 3f);
            var record = new ThrownExplosiveRecord(
                sequence: 4,
                throwerId: "thrower",
                definition: definition,
                origin: new GameplayPosition(0f, 0f, 0f),
                launchOrigin: new GameplayPosition(0f, 1.2f, 0f),
                intendedLanding: new GameplayPosition(5f, 0f, 0f),
                sampledLanding: new GameplayPosition(5f, 0f, 0f),
                resolvedLanding: new GameplayPosition(5f, 0f, 0f),
                uncertaintyRadius: 1f,
                worldStateRevision: 3,
                blastEffects: new BlastEffectRecord[0]);
            return new GameplayActionRecord(
                sequence: 4,
                request: new GameplayActionRequest(
                    "thrower",
                    definition.Id,
                    GameplayTargetIds.WorldAimPoint),
                cost: cost,
                previousBudget: new TurnBudget(2, 0f),
                resultingBudget: new TurnBudget(1, 0f),
                outcomes: new GameplayActionOutcome[]
                {
                    new ThrownExplosiveActionOutcome(record),
                });
        }

        private static ProjectileAdvanceRecord CreateImpactAdvance()
        {
            var definition = new ProjectileFlightDefinition(
                "projectile.test",
                speedPerTurn: 4f,
                radius: 0.1f,
                maximumRange: 12f);
            var launch = new ProjectileLaunchRecord(
                sequence: 1,
                projectileId: "projectile.1",
                attackerId: "attacker",
                intendedTargetId: "target",
                actionId: "attack.rocket",
                origin: new GameplayPosition(0f, 0f, 0f),
                aimPoint: new GameplayPosition(0f, 0f, 10f),
                definition,
                turnActionPointTimeScale: 4,
                remainingActionPointsAfterLaunch: 2);
            var previous = new ProjectileFlightSnapshot(
                launch,
                launch.GetPosition(4f),
                distanceTraveled: 4f,
                elapsedTurnTime: 1f,
                ProjectileFlightStatus.InFlight);
            var impact = new ProjectileImpactRecord(
                "projectile.1",
                "target",
                launch.GetPosition(5f),
                arrivalTurnTime: 1.25f,
                worldStateRevision: 4);
            var resulting = new ProjectileFlightSnapshot(
                launch,
                impact.Position,
                distanceTraveled: 5f,
                elapsedTurnTime: 1.25f,
                ProjectileFlightStatus.Impacted,
                impact);
            return new ProjectileAdvanceRecord(
                sequence: 9,
                previous,
                resulting,
                requestedTurnTime: 1f,
                segmentEnd: launch.GetPosition(8f),
                worldStateRevision: 4,
                collisionFraction: 0.25f);
        }

        private static GameplayActorSnapshot CreateActor(
            string actorId,
            int woundCount,
            int maximumWounds) => new GameplayActorSnapshot(
                actorId,
                new GameplayActorPose(
                    new GameplayPosition(0f, 0f, 5f),
                    0f,
                    ActorStance.Standing),
                new TurnBudget(2, 3f),
                new ActorWoundSnapshot(actorId, woundCount, 0f),
                equippedItemId: null,
                equipmentEffects: EquipmentEffectSet.None,
                maximumWounds: maximumWounds);
    }
}
