using System;
using System.Collections.Generic;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;
using NUnit.Framework;

namespace GritGud.Domain.Tests.Gameplay
{
    public sealed class GameplayImpactCycleSessionTests
    {
        [Test]
        public void RemainingLaunchApAdvancesFlightAndPredictedImpactSetsReactionAp()
        {
            GameplaySession gameplay = CreateGameplay(
                emergencyEligible: true,
                responderCount: 1);
            var query = new SequencedSegmentQuery(
                ProjectileSegmentQueryResult.Clear(1),
                ProjectileSegmentQueryResult.Collision(
                    2,
                    "cover.wall",
                    0.5f),
                ProjectileSegmentQueryResult.Collision(
                    3,
                    "cover.wall",
                    1f));
            var projectiles = CreateProjectileSession(gameplay, query);
            var cycle = new GameplayImpactCycleSession(gameplay, projectiles);
            gameplay.BeginEncounter();
            ProjectileLaunchRecord launch = Launch(projectiles);

            Assert.That(cycle.ObserveLaunch(launch), Is.True);

            ProjectileFlightSnapshot staged = projectiles.GetProjectile(
                launch.ProjectileId);
            Assert.That(staged.DistanceTraveled, Is.EqualTo(2f));
            Assert.That(staged.ElapsedTurnTime, Is.EqualTo(0.5f));
            Assert.That(projectiles.Advances, Has.Count.EqualTo(1));
            Assert.That(cycle.CurrentWindow.ActionPointAllowance, Is.EqualTo(2));

            Assert.That(cycle.TryEndTurn("player", out _), Is.True);
            Assert.That(gameplay.TurnPhase, Is.EqualTo(
                GameplayTurnPhase.EmergencyReaction));
            Assert.That(gameplay.ActiveActorId, Is.EqualTo("target.1"));
            Assert.That(
                gameplay.GetActor("target.1").TurnBudget.ActionPoints,
                Is.EqualTo(2));

            Assert.That(cycle.TryEndTurn("target.1", out _), Is.True);
            Assert.That(projectiles.Advances, Has.Count.EqualTo(2));
            Assert.That(
                projectiles.GetProjectile(launch.ProjectileId).Status,
                Is.EqualTo(ProjectileFlightStatus.Impacted));
            Assert.That(cycle.CurrentWindow.Status, Is.EqualTo(
                EmergencyReactionWindowStatus.Completed));
            Assert.That(gameplay.TurnPhase, Is.EqualTo(GameplayTurnPhase.Normal));
            Assert.That(gameplay.ActiveActorId, Is.EqualTo("player"));
        }

        [Test]
        public void EveryResponderSharesOneIntervalAndProjectileAdvancesOnce()
        {
            GameplaySession gameplay = CreateGameplay(
                emergencyEligible: true,
                responderCount: 2);
            var query = new SequencedSegmentQuery(
                ProjectileSegmentQueryResult.Clear(1),
                ProjectileSegmentQueryResult.Collision(
                    2,
                    "cover.wall",
                    0.25f),
                ProjectileSegmentQueryResult.Collision(
                    3,
                    "moved.cover",
                    1f));
            var projectiles = CreateProjectileSession(gameplay, query);
            var cycle = new GameplayImpactCycleSession(gameplay, projectiles);
            gameplay.BeginEncounter();
            ProjectileLaunchRecord launch = Launch(projectiles);

            Assert.That(cycle.ObserveLaunch(launch), Is.True);
            Assert.That(cycle.CurrentWindow.ActionPointAllowance, Is.EqualTo(1));
            Assert.That(cycle.TryEndTurn("player", out _), Is.True);

            Assert.That(cycle.TryEndTurn("target.1", out _), Is.True);
            Assert.That(projectiles.Advances, Has.Count.EqualTo(1));
            Assert.That(gameplay.ActiveActorId, Is.EqualTo("target.2"));
            Assert.That(
                gameplay.GetActor("target.2").TurnBudget.ActionPoints,
                Is.EqualTo(1));

            Assert.That(cycle.TryEndTurn("target.2", out _), Is.True);
            Assert.That(projectiles.Advances, Has.Count.EqualTo(2));
            Assert.That(
                projectiles.GetProjectile(launch.ProjectileId).Impact.HitEntityId,
                Is.EqualTo("moved.cover"),
                "The shared interval must re-query the post-reaction world.");
        }

        [Test]
        public void ClearPredictionDoesNotOpenReactionButKeepsCommittedPreTravel()
        {
            GameplaySession gameplay = CreateGameplay(
                emergencyEligible: true,
                responderCount: 1);
            var projectiles = CreateProjectileSession(
                gameplay,
                new ClearSegmentQuery());
            var cycle = new GameplayImpactCycleSession(gameplay, projectiles);
            gameplay.BeginEncounter();
            ProjectileLaunchRecord launch = Launch(projectiles);

            Assert.That(cycle.ObserveLaunch(launch), Is.False);

            Assert.That(cycle.CurrentWindow, Is.Null);
            Assert.That(projectiles.Advances, Has.Count.EqualTo(1));
            Assert.That(
                projectiles.GetProjectile(launch.ProjectileId).DistanceTraveled,
                Is.EqualTo(2f));
        }

        [Test]
        public void LaunchAsFinalActionLeavesFullImpactTimeForReactionPass()
        {
            GameplaySession gameplay = CreateGameplay(
                emergencyEligible: true,
                responderCount: 1);
            gameplay.BeginEncounter();
            SpendActionPoints(gameplay, 2);
            var query = new SequencedSegmentQuery(
                ProjectileSegmentQueryResult.Collision(
                    1,
                    "cover.wall",
                    0.5f),
                ProjectileSegmentQueryResult.Collision(
                    2,
                    "cover.wall",
                    1f));
            var projectiles = CreateProjectileSession(gameplay, query);
            var cycle = new GameplayImpactCycleSession(gameplay, projectiles);
            ProjectileLaunchRecord launch = Launch(projectiles);

            Assert.That(launch.RemainingActionPointsAfterLaunch, Is.Zero);
            Assert.That(cycle.ObserveLaunch(launch), Is.True);

            Assert.That(projectiles.Advances, Is.Empty);
            Assert.That(cycle.CurrentWindow.ActionPointAllowance, Is.EqualTo(2));
            Assert.That(
                projectiles.GetProjectile(launch.ProjectileId).DistanceTraveled,
                Is.Zero);
        }

        [Test]
        public void IneligibleLaunchKeepsApTravelWithoutOpeningWindow()
        {
            GameplaySession gameplay = CreateGameplay(
                emergencyEligible: false,
                responderCount: 1);
            var projectiles = CreateProjectileSession(
                gameplay,
                new ClearSegmentQuery());
            var cycle = new GameplayImpactCycleSession(gameplay, projectiles);
            gameplay.BeginEncounter();
            ProjectileLaunchRecord launch = Launch(projectiles);

            Assert.That(cycle.ObserveLaunch(launch), Is.False);

            Assert.That(cycle.CurrentWindow, Is.Null);
            Assert.That(projectiles.Advances, Has.Count.EqualTo(1));
            Assert.That(
                projectiles.GetProjectile(launch.ProjectileId).DistanceTraveled,
                Is.EqualTo(2f));
        }

        [Test]
        public void VoluntaryProjectileTravelCannotOpenAnEmergencyWindow()
        {
            GameplaySession gameplay = CreateGameplay(
                emergencyEligible: true,
                responderCount: 1);
            Assert.That(gameplay.EnterTurnMode(), Is.True);
            var projectiles = CreateProjectileSession(
                gameplay,
                new SequencedSegmentQuery(
                    ProjectileSegmentQueryResult.Clear(1)));
            var cycle = new GameplayImpactCycleSession(gameplay, projectiles);
            ProjectileLaunchRecord launch = Launch(projectiles);

            Assert.That(cycle.ObserveLaunch(launch), Is.False);
            Assert.That(cycle.CurrentWindow, Is.Null);
            Assert.That(projectiles.Advances, Has.Count.EqualTo(1));
            Assert.That(gameplay.EncounterActive, Is.False);
        }

        [Test]
        public void ExistingEmergencyTriggerPreventsProjectileFromChangingTheCycle()
        {
            GameplaySession gameplay = CreateGameplay(
                emergencyEligible: true,
                responderCount: 1);
            var projectiles = CreateProjectileSession(
                gameplay,
                new ClearSegmentQuery());
            var sharedCycle = new GameplayEmergencyCycleSession(gameplay);
            var cycle = new GameplayImpactCycleSession(
                gameplay,
                projectiles,
                sharedCycle);
            gameplay.BeginEncounter();
            Assert.That(sharedCycle.TryOpen(
                "environment",
                "alarm.01",
                "player",
                2,
                new PendingResolution()), Is.True);
            ProjectileLaunchRecord launch = Launch(projectiles);

            Assert.That(cycle.ObserveLaunch(launch), Is.False);

            Assert.That(sharedCycle.CurrentWindow.TriggerType, Is.EqualTo(
                "environment"));
            Assert.That(sharedCycle.CurrentWindow.TriggerId, Is.EqualTo(
                "alarm.01"));
            Assert.That(projectiles.Advances, Has.Count.EqualTo(1));
        }

        private static ProjectileLaunchRecord Launch(
            GameplayProjectileSession projectiles)
        {
            Assert.That(projectiles.TryLaunch(
                "player",
                "world.aim-point",
                new GameplayPosition(0f, 1f, 10f),
                out GameplayActionRecord action,
                out _), Is.True);
            return ((ProjectileLaunchedActionOutcome)action.Outcomes[0]).Launch;
        }

        private static void SpendActionPoints(
            GameplaySession gameplay,
            int actionPoints)
        {
            Assert.That(actionPoints, Is.EqualTo(2));
            Assert.That(new GameplayActionResolver(gameplay).TryResolveInteraction(
                "player",
                "objective.test-spend",
                out _,
                out _), Is.True);
        }

        private static GameplaySession CreateGameplay(
            bool emergencyEligible,
            int responderCount)
        {
            var projectile = new ProjectileFlightDefinition(
                "projectile.rocket",
                4f,
                0.1f,
                12f,
                1f,
                1f,
                emergencyEligible);
            var actors = new List<ScenarioActorDefinition>
            {
                new ScenarioActorDefinition(
                    "player",
                    10,
                    new GameplayActorPose(
                        new GameplayPosition(0f, 0f, 0f),
                        0f),
                    new TurnBudget(4, 8f),
                    new AttackDefinition(
                        "attack.rocket",
                        "Launch rocket",
                        new ActionCost(2, 0f, ActionMobility.Set),
                        2f,
                        projectile: projectile)),
            };
            for (int index = 1; index <= responderCount; index++)
            {
                actors.Add(new ScenarioActorDefinition(
                    "target." + index,
                    10 - index,
                    new GameplayActorPose(
                        new GameplayPosition(0f, 0f, 8f + index),
                        180f),
                    new TurnBudget(4, 8f)));
            }

            return new GameplaySession(new ScenarioDefinition(
                "impact-cycle-test",
                new ScenarioTimingDefinition(1f),
                actors,
                new[]
                {
                    new ScenarioObjectiveDefinition(
                        "objective.test-spend",
                        new GameplayPosition(0f, 0f, 0f),
                        interactionRadius: 1f,
                        new GameplayInteractionDefinition(
                            "action.test-spend",
                            "Spend test AP",
                            new ActionCost(
                                2,
                                0f,
                                ActionMobility.Set))),
                }));
        }

        private sealed class ClearSegmentQuery : IProjectileSegmentQuery
        {
            private long revision;

            public ProjectileSegmentQueryResult Query(
                ProjectileSegmentQuery query) =>
                ProjectileSegmentQueryResult.Clear(++revision);
        }

        private sealed class SequencedSegmentQuery : IProjectileSegmentQuery
        {
            private readonly Queue<ProjectileSegmentQueryResult> results;

            public SequencedSegmentQuery(
                params ProjectileSegmentQueryResult[] queryResults)
            {
                results = new Queue<ProjectileSegmentQueryResult>(
                    queryResults);
            }

            public ProjectileSegmentQueryResult Query(
                ProjectileSegmentQuery query)
            {
                if (results.Count == 0)
                {
                    throw new InvalidOperationException(
                        "The test did not author enough segment results.");
                }

                return results.Dequeue();
            }
        }

        private sealed class PendingResolution : IEmergencyCycleResolution
        {
            public bool IsResolved => false;

            public void ResolveAfterResponsePass()
            {
            }
        }

        private static GameplayProjectileSession CreateProjectileSession(
            GameplaySession gameplay,
            IProjectileSegmentQuery query)
        {
            var destructibles = new DestructiblePropSession(
                Array.Empty<DestructiblePropDefinition>());
            return new GameplayProjectileSession(
                gameplay,
                query,
                new GameplayBlastConsequenceResolver(
                    gameplay,
                    destructibles));
        }

    }
}
