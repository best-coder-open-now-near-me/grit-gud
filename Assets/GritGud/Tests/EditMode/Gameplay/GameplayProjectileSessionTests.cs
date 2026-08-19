using System;
using System.Collections.Generic;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;
using NUnit.Framework;

namespace GritGud.Domain.Tests.Gameplay
{
    public sealed class GameplayProjectileSessionTests
    {
        [Test]
        public void LaunchModeRequirementSeparatesInertShotsFromReactiveTargets()
        {
            GameplaySession gameplay = CreateGameplay(
                targetStartsEncounter: true);
            var projectiles = CreateProjectileSession(
                gameplay,
                new QueuedSegmentQuery());

            Assert.That(
                projectiles.GetLaunchModeRequirement("world.aim-point"),
                Is.EqualTo(
                    ProjectileLaunchModeRequirement.VoluntaryTurnMode));
            Assert.That(
                projectiles.GetLaunchModeRequirement("target"),
                Is.EqualTo(ProjectileLaunchModeRequirement.Encounter));

            Assert.That(gameplay.EnterTurnMode(), Is.True);
            Assert.That(
                projectiles.GetLaunchModeRequirement("world.aim-point"),
                Is.EqualTo(ProjectileLaunchModeRequirement.None));
            Assert.That(
                projectiles.GetLaunchModeRequirement("target"),
                Is.EqualTo(ProjectileLaunchModeRequirement.Encounter));

            Assert.That(gameplay.BeginEncounter(), Is.True);
            Assert.That(
                projectiles.GetLaunchModeRequirement("target"),
                Is.EqualTo(ProjectileLaunchModeRequirement.None));
        }

        [Test]
        public void LaunchSpendsTheProjectileWeaponsAuthoredCost()
        {
            GameplaySession gameplay = CreateGameplay();
            gameplay.EnterTurnMode();
            var projectiles = CreateProjectileSession(
                gameplay,
                new QueuedSegmentQuery());

            bool launched = projectiles.TryLaunch(
                "player",
                "target",
                new GameplayPosition(0f, 0f, 10f),
                out GameplayActionRecord action,
                out ProjectileLaunchFailure failure);

            Assert.That(launched, Is.True);
            Assert.That(failure, Is.EqualTo(ProjectileLaunchFailure.None));
            Assert.That(action.Cost.ActionPoints, Is.EqualTo(2));
            Assert.That(gameplay.GetActor("player").TurnBudget.ActionPoints,
                Is.EqualTo(2));
            Assert.That(projectiles.Launches, Has.Count.EqualTo(1));
            ProjectileLaunchRecord launch = projectiles.Launches[0];
            Assert.That(launch.TurnActionPointTimeScale, Is.EqualTo(4));
            Assert.That(
                launch.RemainingActionPointsAfterLaunch,
                Is.EqualTo(2));
            Assert.That(projectiles.GetProjectile("projectile.1").Status,
                Is.EqualTo(ProjectileFlightStatus.InFlight));
            Assert.That(projectiles.HasActiveProjectiles, Is.True);
            Assert.That(action.Outcomes[0],
                Is.TypeOf<ProjectileLaunchedActionOutcome>());
        }

        [Test]
        public void PreparedLaunchIsNonMutatingAndMatchesAuthoritativeFlight()
        {
            GameplaySession gameplay = CreateGameplay();
            gameplay.EnterTurnMode();
            var projectiles = CreateProjectileSession(
                gameplay,
                new QueuedSegmentQuery());

            Assert.That(projectiles.TryPrepareLaunch(
                "player",
                "target",
                new GameplayPosition(0f, 0f, 10f),
                out GameplayPreparedTransition<GameplayActionRecord> prepared,
                out ProjectileLaunchFailure failure), Is.True);

            Assert.That(failure, Is.EqualTo(ProjectileLaunchFailure.None));
            Assert.That(gameplay.ResolvedActions, Is.Empty);
            Assert.That(projectiles.Launches, Is.Empty);
            Assert.That(projectiles.ProjectileIds, Is.Empty);
            Assert.That(prepared.Predicted.Projectiles, Has.Count.EqualTo(1));
            ProjectileFlightSnapshot predicted = prepared.Predicted.Projectiles[0];
            Assert.That(predicted.ProjectileId, Is.EqualTo("projectile.1"));
            Assert.That(predicted.Status, Is.EqualTo(
                ProjectileFlightStatus.InFlight));
            Assert.That(predicted.Position, Is.EqualTo(predicted.Launch.Origin));
            Assert.That(
                prepared.Predicted.Session.GetActor("player")
                    .TurnBudget.ActionPoints,
                Is.EqualTo(2));

            GameplayTransitionCommitResult result =
                projectiles.CommitPreparedLaunch(prepared);

            Assert.That(result.MatchesPrediction, Is.True);
            Assert.That(projectiles.Launches, Has.Count.EqualTo(1));
            Assert.That(projectiles.GetProjectile("projectile.1").Status,
                Is.EqualTo(ProjectileFlightStatus.InFlight));
        }

        [Test]
        public void PreparedLaunchRejectsInterveningTurnBeforeMutation()
        {
            GameplaySession gameplay = CreateGameplay();
            gameplay.EnterTurnMode();
            var projectiles = CreateProjectileSession(
                gameplay,
                new QueuedSegmentQuery());
            Assert.That(projectiles.TryPrepareLaunch(
                "player",
                "target",
                new GameplayPosition(0f, 0f, 10f),
                out GameplayPreparedTransition<GameplayActionRecord> prepared,
                out _), Is.True);
            Assert.That(gameplay.TryEndTurn("player", out _), Is.True);

            Assert.Throws<InvalidOperationException>(
                () => projectiles.CommitPreparedLaunch(prepared));

            Assert.That(projectiles.Launches, Is.Empty);
            Assert.That(projectiles.ProjectileIds, Is.Empty);
            Assert.That(gameplay.ResolvedActions, Is.Empty);
        }

        [Test]
        public void ResponsiveExplorationLaunchCommitsBeforeEncounterBegins()
        {
            GameplaySession gameplay = CreateGameplay(
                targetStartsEncounter: true);
            GameplayProjectileSession projectiles = CreateProjectileSession(
                gameplay,
                new QueuedSegmentQuery());

            Assert.That(projectiles.TryLaunch(
                "player",
                "target",
                new GameplayPosition(0f, 0f, 10f),
                out GameplayActionRecord action,
                out ProjectileLaunchFailure failure), Is.True);

            Assert.That(failure, Is.EqualTo(ProjectileLaunchFailure.None));
            Assert.That(action.Cost.ActionPoints, Is.EqualTo(2));
            Assert.That(gameplay.GetActor("player").TurnBudget.ActionPoints,
                Is.EqualTo(2));
            Assert.That(gameplay.Mode,
                Is.EqualTo(GameplaySessionMode.Exploration));
            Assert.That(gameplay.ActionStartsEncounter(action), Is.True);
            Assert.That(gameplay.BeginEncounterFromAction(action), Is.True);
            Assert.That(gameplay.Mode,
                Is.EqualTo(GameplaySessionMode.TurnBased));
        }

        [Test]
        public void PredictionQueriesWithoutChangingAuthoritativeFlight()
        {
            GameplaySession gameplay = CreateGameplay();
            gameplay.EnterTurnMode();
            var query = new QueuedSegmentQuery(
                ProjectileSegmentQueryResult.Collision(
                    worldStateRevision: 20,
                    hitEntityId: "cover.before-reaction",
                    collisionFraction: 0.25f),
                ProjectileSegmentQueryResult.Collision(
                    worldStateRevision: 21,
                    hitEntityId: "cover.after-reaction",
                    collisionFraction: 0.5f));
            var projectiles = CreateProjectileSession(gameplay, query);
            projectiles.TryLaunch(
                "player",
                "world.aim-point",
                new GameplayPosition(0f, 0f, 10f),
                out _,
                out _);

            ProjectileAdvancePrediction prediction = projectiles.PredictAdvance(
                "projectile.1",
                turnTime: 1f);

            Assert.That(prediction.HasCollision, Is.True);
            Assert.That(prediction.CollisionTurnTime, Is.EqualTo(0.25f));
            Assert.That(prediction.CollisionPosition.Z, Is.EqualTo(1f));
            Assert.That(projectiles.Advances, Is.Empty);
            Assert.That(
                projectiles.GetProjectile("projectile.1").DistanceTraveled,
                Is.Zero);

            ProjectileAdvanceRecord committed = projectiles.Advance(
                "projectile.1",
                turnTime: 1f);
            Assert.That(committed.Resulting.Impact.HitEntityId, Is.EqualTo(
                "cover.after-reaction"));
            Assert.That(committed.Resulting.Position.Z, Is.EqualTo(2f));
            Assert.That(query.Queries, Has.Count.EqualTo(2));
        }

        [Test]
        public void LaunchCanAimAtWorldGeometryWithoutAnActorTarget()
        {
            GameplaySession gameplay = CreateGameplay();
            gameplay.EnterTurnMode();
            var projectiles = CreateProjectileSession(
                gameplay,
                new QueuedSegmentQuery());

            bool launched = projectiles.TryLaunch(
                "player",
                "world.aim-point",
                new GameplayPosition(3f, 1f, 10f),
                out GameplayActionRecord action,
                out ProjectileLaunchFailure failure);

            Assert.That(launched, Is.True);
            Assert.That(failure, Is.EqualTo(ProjectileLaunchFailure.None));
            var launch = ((ProjectileLaunchedActionOutcome)action.Outcomes[0]).Launch;
            Assert.That(launch.IntendedTargetId, Is.EqualTo("world.aim-point"));
            Assert.That(launch.AimPoint, Is.EqualTo(new GameplayPosition(3f, 1f, 10f)));
        }

        [Test]
        public void CommittedLaunchFacesAttackerTowardAimPoint()
        {
            GameplaySession gameplay = CreateGameplay();
            gameplay.EnterTurnMode();
            var projectiles = CreateProjectileSession(
                gameplay,
                new QueuedSegmentQuery());

            Assert.That(projectiles.TryLaunch(
                "player",
                "world.aim-point",
                new GameplayPosition(10f, 0f, 0f),
                out _,
                out _), Is.True);

            Assert.That(
                gameplay.GetActor("player").Pose.FacingDegrees,
                Is.EqualTo(90f).Within(0.001f));
        }

        [Test]
        public void LaunchUsesTheStanceAuthoredRocketHeight()
        {
            GameplaySession gameplay = CreateGameplayWithLaunchHeight(
                ActorStance.Crouched);
            gameplay.EnterTurnMode();
            var projectiles = CreateProjectileSession(
                gameplay,
                new QueuedSegmentQuery());

            projectiles.TryLaunch(
                "player",
                "target",
                new GameplayPosition(0f, 0.8f, 10f),
                out GameplayActionRecord action,
                out _);

            ProjectileLaunchRecord launch =
                ((ProjectileLaunchedActionOutcome)action.Outcomes[0]).Launch;
            Assert.That(launch.Origin.Y, Is.EqualTo(0.9f));
            Assert.That(launch.Definition.OpensEmergencyReactionWindow, Is.True);
        }

        [Test]
        public void EachTurnTimeAdvanceQueriesTheCurrentArrivalWorldState()
        {
            GameplaySession gameplay = CreateGameplay();
            gameplay.EnterTurnMode();
            var query = new QueuedSegmentQuery(
                ProjectileSegmentQueryResult.Clear(worldStateRevision: 100),
                ProjectileSegmentQueryResult.Collision(
                    worldStateRevision: 101,
                    hitEntityId: "target",
                    collisionFraction: 0.5f));
            var projectiles = CreateProjectileSession(gameplay, query);
            projectiles.TryLaunch(
                "player",
                "target",
                new GameplayPosition(0f, 0f, 10f),
                out _,
                out _);

            ProjectileAdvanceRecord first = projectiles.Advance(
                "projectile.1",
                turnTime: 1f);
            ProjectileAdvanceRecord second = projectiles.Advance(
                "projectile.1",
                turnTime: 1f);

            Assert.That(first.Resulting.Status,
                Is.EqualTo(ProjectileFlightStatus.InFlight));
            Assert.That(first.Resulting.Position.Z, Is.EqualTo(4f));
            Assert.That(second.Previous.Position.Z, Is.EqualTo(4f));
            Assert.That(second.SegmentEnd.Z, Is.EqualTo(8f));
            Assert.That(second.Resulting.Status,
                Is.EqualTo(ProjectileFlightStatus.Impacted));
            Assert.That(second.Resulting.Position.Z, Is.EqualTo(6f));
            Assert.That(second.Resulting.ElapsedTurnTime, Is.EqualTo(1.5f));
            Assert.That(second.Resulting.Impact.HitEntityId, Is.EqualTo("target"));
            Assert.That(second.Resulting.Impact.WorldStateRevision, Is.EqualTo(101));
            Assert.That(query.Queries, Has.Count.EqualTo(2));
            Assert.That(query.Queries[0].StartingTurnTime, Is.EqualTo(0f));
            Assert.That(query.Queries[0].ArrivalTurnTime, Is.EqualTo(1f));
            Assert.That(query.Queries[1].StartingTurnTime, Is.EqualTo(1f));
            Assert.That(query.Queries[1].ArrivalTurnTime, Is.EqualTo(2f));
            Assert.That(gameplay.Journal.LastEntry,
                Is.TypeOf<ProjectileAdvancedJournalEntry>());
        }

        [Test]
        public void RecordedFlightReplaysWithoutRepeatingCollisionQueries()
        {
            GameplaySession sourceGameplay = CreateGameplay();
            sourceGameplay.EnterTurnMode();
            var source = CreateProjectileSession(
                sourceGameplay,
                new QueuedSegmentQuery(
                    ProjectileSegmentQueryResult.Collision(
                        worldStateRevision: 22,
                        hitEntityId: "cover.wall",
                        collisionFraction: 0.75f)));
            source.TryLaunch(
                "player",
                "target",
                new GameplayPosition(0f, 0f, 10f),
                out GameplayActionRecord launchAction,
                out _);
            ProjectileAdvanceRecord advance = source.Advance(
                "projectile.1",
                turnTime: 1f);

            GameplaySession replayGameplay = CreateGameplay();
            replayGameplay.EnterTurnMode();
            var replay = CreateProjectileSession(
                replayGameplay,
                new FailingSegmentQuery());
            replay.CommitLaunch(launchAction);
            replay.CommitAdvance(advance);

            ProjectileFlightSnapshot replayed = replay.GetProjectile(
                "projectile.1");
            Assert.That(replayed.Status,
                Is.EqualTo(ProjectileFlightStatus.Impacted));
            Assert.That(replayed.Position.Z, Is.EqualTo(3f));
            Assert.That(replayed.Impact.HitEntityId, Is.EqualTo("cover.wall"));
            Assert.That(replayed.Impact.WorldStateRevision, Is.EqualTo(22));
            Assert.That(replayGameplay.GetActor("player").TurnBudget.ActionPoints,
                Is.EqualTo(2));
        }

        [Test]
        public void ClearProjectileExpiresAtMaximumRange()
        {
            GameplaySession gameplay = CreateGameplay(maximumRange: 6f);
            gameplay.EnterTurnMode();
            var query = new QueuedSegmentQuery(
                ProjectileSegmentQueryResult.Clear(worldStateRevision: 55));
            var projectiles = CreateProjectileSession(gameplay, query);
            projectiles.TryLaunch(
                "player",
                "target",
                new GameplayPosition(0f, 0f, 10f),
                out _,
                out _);

            ProjectileAdvanceRecord advance = projectiles.Advance(
                "projectile.1",
                turnTime: 2f);

            Assert.That(advance.RequestedTurnTime, Is.EqualTo(2f));
            Assert.That(advance.Resulting.Status,
                Is.EqualTo(ProjectileFlightStatus.Expired));
            Assert.That(advance.Resulting.DistanceTraveled, Is.EqualTo(6f));
            Assert.That(advance.Resulting.ElapsedTurnTime, Is.EqualTo(1.5f));
            Assert.That(projectiles.HasActiveProjectiles, Is.False);
            Assert.That(query.Queries, Has.Count.EqualTo(1));
            Assert.That(query.Queries[0].SegmentEnd.Z, Is.EqualTo(6f));
        }

        [Test]
        public void RocketImpactAppliesRecordedBlastToNearbyActors()
        {
            GameplaySession gameplay = CreateGameplayWithBlast();
            gameplay.EnterTurnMode();
            var query = new QueuedSegmentQuery(
                ProjectileSegmentQueryResult.Collision(
                    9L,
                    "cover.wall",
                    0.5f,
                    new[]
                    {
                        new BlastEffectRecord(
                            "target",
                            BlastSubjectKind.Actor,
                            2f,
                            occlusionExposure: 1f,
                            distanceFalloff: 0.5f,
                            injuryRegion: TargetRegionId.LeftLeg),
                    }));
            var projectiles = CreateProjectileSession(gameplay, query);
            projectiles.TryLaunch(
                "player", "world.aim-point", new GameplayPosition(0f, 1f, 10f),
                out _, out _);

            GameplayPreparedTransition<ProjectileAdvanceRecord> prepared =
                projectiles.PrepareAdvance("projectile.1", 1f);

            Assert.That(prepared.Record.Resulting.Impact.BlastEffects,
                Has.Count.EqualTo(1));
            Assert.That(projectiles.HasActiveProjectiles, Is.True);
            Assert.That(gameplay.GetActor("target").Wounds.WoundCount, Is.Zero);
            Assert.That(
                prepared.Predicted.Session.GetActor("target")
                    .Wounds.LeftLegWounds,
                Is.EqualTo(1));
            Assert.That(projectiles.CommitPreparedAdvance(prepared)
                .MatchesPrediction, Is.True);
            Assert.That(projectiles.HasActiveProjectiles, Is.False);
            Assert.That(gameplay.GetActor("target").Wounds.WoundCount, Is.EqualTo(1));
            Assert.That(gameplay.GetActor("target").Wounds.MovementPenalty,
                Is.EqualTo(1f));
            Assert.That(
                gameplay.GetActor("target").Wounds.LeftLegWounds,
                Is.EqualTo(1));
            Assert.That(
                gameplay.GetActor("target").Wounds.TorsoWounds,
                Is.Zero);
        }

        [Test]
        public void PreparedAdvanceRejectsInterveningTurnBeforeMutation()
        {
            GameplaySession gameplay = CreateGameplay();
            gameplay.EnterTurnMode();
            var query = new QueuedSegmentQuery(
                ProjectileSegmentQueryResult.Clear(worldStateRevision: 12));
            var projectiles = CreateProjectileSession(gameplay, query);
            projectiles.TryLaunch(
                "player",
                "target",
                new GameplayPosition(0f, 0f, 10f),
                out _,
                out _);
            GameplayPreparedTransition<ProjectileAdvanceRecord> prepared =
                projectiles.PrepareAdvance("projectile.1", 1f);
            Assert.That(gameplay.TryEndTurn("player", out _), Is.True);

            Assert.Throws<InvalidOperationException>(
                () => projectiles.CommitPreparedAdvance(prepared));

            Assert.That(projectiles.Advances, Is.Empty);
            Assert.That(
                projectiles.GetProjectile("projectile.1").DistanceTraveled,
                Is.Zero);
        }

        [Test]
        public void PreparedImpactPredictsDestructibleDamage()
        {
            GameplaySession gameplay = CreateGameplayWithBlast();
            gameplay.EnterTurnMode();
            var destructibles = new DestructiblePropSession(new[]
            {
                new DestructiblePropDefinition(
                    "crate",
                    10f,
                    DestructiblePropState.Intact),
            }, gameplay.Journal);
            var query = new QueuedSegmentQuery(
                ProjectileSegmentQueryResult.Collision(
                    15L,
                    "crate",
                    0.5f,
                    new[]
                    {
                        new BlastEffectRecord(
                            "crate",
                            BlastSubjectKind.DestructibleProp,
                            1f,
                            occlusionExposure: 1f,
                            distanceFalloff: 0.5f),
                    }));
            GameplayProjectileSession projectiles = CreateProjectileSession(
                gameplay,
                query,
                destructibles);
            projectiles.TryLaunch(
                "player", "world.aim-point", new GameplayPosition(0f, 1f, 10f),
                out _, out _);

            GameplayPreparedTransition<ProjectileAdvanceRecord> prepared =
                projectiles.PrepareAdvance("projectile.1", 1f);

            Assert.That(destructibles.GetProp("crate").RemainingIntegrity,
                Is.EqualTo(10f));
            Assert.That(prepared.Predicted.Destructibles[0].RemainingIntegrity,
                Is.EqualTo(8f));
            Assert.That(projectiles.CommitPreparedAdvance(prepared)
                .MatchesPrediction, Is.True);
            Assert.That(destructibles.GetProp("crate").RemainingIntegrity,
                Is.EqualTo(8f));
        }

        private static GameplaySession CreateGameplay(
            float maximumRange = 12f,
            bool targetStartsEncounter = false)
        {
            var projectile = new ProjectileFlightDefinition(
                "projectile.slow-test",
                speedPerTurn: 4f,
                radius: 0.1f,
                maximumRange: maximumRange);
            var player = new ScenarioActorDefinition(
                "player",
                10,
                new GameplayActorPose(new GameplayPosition(0f, 0f, 0f), 0f),
                new TurnBudget(4, 8f),
                new AttackDefinition(
                    "attack.slow-projectile",
                    "Launch slow projectile",
                    new ActionCost(2, 0f, ActionMobility.Set),
                    woundMovementPenalty: 2f,
                    projectile: projectile));
            var target = new ScenarioActorDefinition(
                "target",
                0,
                new GameplayActorPose(new GameplayPosition(0f, 0f, 10f), 180f),
                new TurnBudget(0, 8f));
            return new GameplaySession(new ScenarioDefinition(
                "projectile-test",
                new ScenarioTimingDefinition(1.25f),
                new[] { player, target },
                Array.Empty<ScenarioObjectiveDefinition>(),
                targetStartsEncounter
                    ? new[] { new AttackResponseDefinition("target", true) }
                    : Array.Empty<AttackResponseDefinition>()));
        }

        private static GameplaySession CreateGameplayWithBlast()
        {
            var projectile = new ProjectileFlightDefinition(
                "projectile.rocket", 4f, 0.1f, 12f, 1f, 1f, true, 5f, 2f,
                blastIntegrityDamage: 4f);
            var player = new ScenarioActorDefinition(
                "player", 10,
                new GameplayActorPose(new GameplayPosition(0f, 0f, 0f), 0f),
                new TurnBudget(4, 8f),
                new AttackDefinition(
                    "attack.rocket", "Launch rocket",
                    new ActionCost(2, 0f, ActionMobility.Set),
                    2f,
                    projectile));
            var target = new ScenarioActorDefinition(
                "target", 0,
                new GameplayActorPose(new GameplayPosition(0f, 0f, 6f), 180f),
                new TurnBudget(0, 8f));
            return new GameplaySession(new ScenarioDefinition(
                "rocket-blast-test", new ScenarioTimingDefinition(1f),
                new[] { player, target }, Array.Empty<ScenarioObjectiveDefinition>()));
        }

        private static GameplaySession CreateGameplayWithLaunchHeight(
            ActorStance stance)
        {
            var projectile = new ProjectileFlightDefinition(
                "projectile.slow-test",
                speedPerTurn: 4f,
                radius: 0.1f,
                maximumRange: 12f,
                standingLaunchHeight: 1.35f,
                crouchedLaunchHeight: 0.9f,
                opensEmergencyReactionWindow: true);
            var player = new ScenarioActorDefinition(
                "player",
                10,
                new GameplayActorPose(
                    new GameplayPosition(0f, 0f, 0f),
                    0f,
                    stance),
                new TurnBudget(4, 8f),
                new AttackDefinition(
                    "attack.slow-projectile",
                    "Launch slow projectile",
                    new ActionCost(2, 0f, ActionMobility.Set),
                    woundMovementPenalty: 2f,
                    projectile: projectile));
            var target = new ScenarioActorDefinition(
                "target",
                0,
                new GameplayActorPose(
                    new GameplayPosition(0f, 0f, 10f),
                    180f),
                new TurnBudget(0, 8f));
            return new GameplaySession(new ScenarioDefinition(
                "projectile-height-test",
                new ScenarioTimingDefinition(1.25f),
                new[] { player, target },
                Array.Empty<ScenarioObjectiveDefinition>()));
        }

        private sealed class QueuedSegmentQuery : IProjectileSegmentQuery
        {
            private readonly Queue<ProjectileSegmentQueryResult> results;

            public QueuedSegmentQuery(
                params ProjectileSegmentQueryResult[] queuedResults)
            {
                results = new Queue<ProjectileSegmentQueryResult>(queuedResults);
            }

            public List<ProjectileSegmentQuery> Queries { get; } =
                new List<ProjectileSegmentQuery>();

            public ProjectileSegmentQueryResult Query(
                ProjectileSegmentQuery query)
            {
                Queries.Add(query);
                if (results.Count == 0)
                {
                    return ProjectileSegmentQueryResult.Clear(
                        worldStateRevision: Queries.Count);
                }

                return results.Dequeue();
            }
        }

        private static GameplayProjectileSession CreateProjectileSession(
            GameplaySession gameplay,
            IProjectileSegmentQuery query)
        {
            var destructibles = new DestructiblePropSession(
                Array.Empty<DestructiblePropDefinition>());
            return CreateProjectileSession(gameplay, query, destructibles);
        }

        private static GameplayProjectileSession CreateProjectileSession(
            GameplaySession gameplay,
            IProjectileSegmentQuery query,
            DestructiblePropSession destructibles)
        {
            return new GameplayProjectileSession(
                gameplay,
                query,
                new GameplayBlastConsequenceResolver(
                    gameplay,
                    destructibles));
        }

        private sealed class FailingSegmentQuery : IProjectileSegmentQuery
        {
            public ProjectileSegmentQueryResult Query(ProjectileSegmentQuery query)
            {
                throw new AssertionException(
                    "Recorded projectile replay must not repeat collision queries.");
            }
        }
    }
}
