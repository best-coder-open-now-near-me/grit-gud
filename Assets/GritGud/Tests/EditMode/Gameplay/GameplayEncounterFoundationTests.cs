using System;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;
using NUnit.Framework;

namespace GritGud.Domain.Tests.Gameplay
{
    public sealed class GameplayEncounterFoundationTests
    {
        [Test]
        public void SoundThenSightTransitionsAwarenessAndJournalsFrozenEvidence()
        {
            GameplaySession gameplay = CreateSession();
            GameplayPosition playerPosition = gameplay.GetActor("player")
                .Pose.Position;
            var soundObservation = new EncounterObservation(
                "enemy",
                sound: new EncounterSoundEvidence(
                    "player",
                    playerPosition,
                    audibility: 1f));

            EnemyAwarenessTransitionRecord heard = gameplay
                .PrepareAwarenessTransition("enemy", soundObservation);
            gameplay.CommitAwarenessTransition(heard);

            Assert.That(heard.Resulting.State,
                Is.EqualTo(EncounterAwarenessState.Suspicious));
            Assert.That(heard.Resulting.Suspicion, Is.EqualTo(60));
            Assert.That(gameplay.Journal.LastEntry,
                Is.TypeOf<EnemyAwarenessChangedJournalEntry>());

            var sight = new TargetExposureSnapshot(
                "enemy",
                "player",
                new[] { new TargetRegionExposure(TargetRegionId.Torso, 1, 1) });
            var sightObservation = new EncounterObservation(
                "enemy",
                sight,
                playerPosition);
            EnemyAwarenessTransitionRecord seen = gameplay
                .PrepareAwarenessTransition("enemy", sightObservation);
            gameplay.CommitAwarenessTransition(seen);

            Assert.That(seen.Resulting.State,
                Is.EqualTo(EncounterAwarenessState.Alert));
            Assert.That(seen.Resulting.LastKnownHostileId, Is.EqualTo("player"));
            Assert.That(seen.Resulting.LastKnownHostilePosition,
                Is.EqualTo(playerPosition));
        }

        [Test]
        public void PatrolAdvancesOnlyThroughTheAuthoredNextWaypoint()
        {
            GameplaySession gameplay = CreateSession();
            PatrolRouteDefinition patrol = gameplay.Scenario.GetActor("enemy")
                .Combat.EnemyBehavior.PatrolRoute;
            GameplayActorSnapshot enemy = gameplay.GetActor("enemy");
            var route = new MovementRouteRecord(
                "enemy",
                enemy.Pose,
                new[] { patrol.GetWaypoint(1) });

            PatrolAdvanceRecord advance = gameplay.PreparePatrolAdvance(
                "enemy",
                route);
            gameplay.CommitPatrolAdvance(advance);

            Assert.That(gameplay.GetActor("enemy").Pose.Position,
                Is.EqualTo(patrol.GetWaypoint(1)));
            Assert.That(gameplay.EncounterState.GetAwareness("enemy")
                    .PatrolWaypointIndex,
                Is.EqualTo(1));
            Assert.That(gameplay.Journal.LastEntry,
                Is.TypeOf<PatrolAdvancedJournalEntry>());
        }

        [Test]
        public void ScopedEncounterReplacesGlobalInitiativeAndRepairsActiveActor()
        {
            GameplaySession gameplay = CreateSession();
            Assert.That(gameplay.TryEnterTurnMode(out _), Is.True);
            Assert.That(gameplay.ActiveActorId, Is.EqualTo("bystander"));

            var scope = new[] { "player", "enemy" };
            Assert.That(gameplay.BeginEncounter(scope), Is.True);

            Assert.That(gameplay.EncounterState.ParticipantIds,
                Is.EqualTo(scope));
            Assert.That(gameplay.InitiativeOrder, Is.EqualTo(scope));
            Assert.That(gameplay.ActiveActorId, Is.EqualTo("player"));
            Assert.That(gameplay.InitiativeOrder, Does.Not.Contain("bystander"));
            Assert.That(gameplay.Journal.LastEntry,
                Is.TypeOf<EncounterChangedJournalEntry>());
        }

        private static GameplaySession CreateSession()
        {
            var behavior = new EnemyBehaviorDefinition(
                "behavior.encounter-test",
                perceptionRange: 20f,
                viewAngleDegrees: 120f,
                preferredEngagementRange: 12f,
                movementSearchRadius: 6f,
                maximumAttacksPerTurn: 1,
                awarenessPolicy: new EncounterAwarenessPolicyDefinition(
                    hearingRange: 12f,
                    sightSuspicionGain: 100,
                    soundSuspicionGain: 60,
                    suspicionDecayPerTick: 10,
                    alertThreshold: 100),
                patrolRoute: new PatrolRouteDefinition(
                    new[]
                    {
                        new GameplayPosition(0f, 0f, 0f),
                        new GameplayPosition(0f, 0f, 3f),
                    },
                    loops: true));
            var player = new ScenarioActorDefinition(
                "player",
                5,
                new GameplayActorPose(new GameplayPosition(0f, 0f, 6f), 180f),
                new TurnBudget(4, 8f),
                combat: new ActorCombatDefinition("player", new[] { "raider" }, 2));
            var enemy = new ScenarioActorDefinition(
                "enemy",
                3,
                new GameplayActorPose(new GameplayPosition(0f, 0f, 0f), 0f),
                new TurnBudget(4, 8f),
                combat: new ActorCombatDefinition(
                    "raider",
                    new[] { "player" },
                    2,
                    behavior));
            var bystander = new ScenarioActorDefinition(
                "bystander",
                10,
                new GameplayActorPose(new GameplayPosition(30f, 0f, 0f), 0f),
                new TurnBudget(4, 8f),
                combat: new ActorCombatDefinition("neutral", Array.Empty<string>(), 1));
            return new GameplaySession(new ScenarioDefinition(
                "encounter-foundation-test",
                new ScenarioTimingDefinition(1f),
                new[] { player, enemy, bystander },
                Array.Empty<ScenarioObjectiveDefinition>()));
        }
    }
}
