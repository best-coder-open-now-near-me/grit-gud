using System;
using System.Collections.Generic;
using System.Linq;
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

        [Test]
        public void CommittedAttackAppliesSoundBeforeStartingScopedEncounter()
        {
            GameplaySession gameplay = CreateSession(soundSuspicionGain: 100);
            var attacks = new GameplayAttackSession(gameplay);
            var observedStates = new List<EncounterAwarenessState>();
            using var consequences =
                new GameplayCommittedActionConsequenceCoordinator(
                    gameplay,
                    new AudibleSoundQuery(),
                    scope =>
                    {
                        observedStates.Add(gameplay.EncounterState
                            .GetAwareness("enemy").State);
                        return gameplay.BeginEncounter(scope);
                    });
            var exposure = new TargetExposureSnapshot(
                "player",
                "enemy",
                new[]
                {
                    new TargetRegionExposure(TargetRegionId.Torso, 1, 1),
                });

            Assert.That(attacks.TryResolve(
                "player",
                exposure,
                out GameplayActionRecord action,
                out AttackResolutionFailure failure), Is.True);

            Assert.That(failure, Is.EqualTo(AttackResolutionFailure.None));
            Assert.That(action.Context, Is.Not.Null);
            Assert.That(gameplay.EncounterState.GetAwareness("enemy").State,
                Is.EqualTo(EncounterAwarenessState.Alert));
            Assert.That(observedStates,
                Is.EqualTo(new[] { EncounterAwarenessState.Alert }));
            Assert.That(gameplay.EncounterActive, Is.True);
            Assert.That(gameplay.EncounterState.ParticipantIds,
                Does.Contain("player"));
            Assert.That(gameplay.EncounterState.ParticipantIds,
                Does.Contain("enemy"));
            Assert.That(gameplay.Journal.Entries
                    .OfType<EnemyAwarenessChangedJournalEntry>().Count(),
                Is.EqualTo(1));
        }

        private static GameplaySession CreateSession(
            int soundSuspicionGain = 60)
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
                    soundSuspicionGain,
                    suspicionDecayPerTick: 10,
                    alertThreshold: 100),
                patrolRoute: new PatrolRouteDefinition(
                    new[]
                    {
                        new GameplayPosition(0f, 0f, 0f),
                        new GameplayPosition(0f, 0f, 3f),
                    },
                    loops: true));
            var attack = new AttackDefinition(
                "attack.rifle",
                "Fire rifle",
                new ActionCost(1, 0f, ActionMobility.Set),
                woundMovementPenalty: 2f,
                accuracyDecay: AccuracyDecayDefinition.None,
                soundSignature: 1f);
            var player = new ScenarioActorDefinition(
                "player",
                5,
                new GameplayActorPose(new GameplayPosition(0f, 0f, 6f), 180f),
                new TurnBudget(4, 8f),
                attack,
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

        private sealed class AudibleSoundQuery :
            IGameplayCommittedActionSoundQuery
        {
            public EncounterSoundEvidence Capture(
                string observerActorId,
                string sourceActorId,
                GameplayPosition origin,
                float soundSignature) => new EncounterSoundEvidence(
                    sourceActorId,
                    origin,
                    soundSignature);
        }
    }
}
