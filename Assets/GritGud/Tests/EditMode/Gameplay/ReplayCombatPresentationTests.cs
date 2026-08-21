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
                Is.EqualTo("7:WeaponDischarge:attacker:"));
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
                Is.EqualTo("9:ProjectileImpact:attacker:projectile.1"));
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
    }
}
