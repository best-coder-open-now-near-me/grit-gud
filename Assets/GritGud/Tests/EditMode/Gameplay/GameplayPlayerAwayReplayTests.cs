using System;
using System.Collections.Generic;
using System.Linq;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;
using NUnit.Framework;

namespace GritGud.Domain.Tests.Gameplay
{
    public sealed class GameplayPlayerAwayReplayTests
    {
        [Test]
        public void ReplayIncludesEveryTurnSinceControlledActorLastEndedTurn()
        {
            GameplaySession gameplay = CreateGameplay();
            gameplay.BeginEncounter();
            GameplayCombatStateSnapshot initial =
                GameplayCombatStateCapture.Capture(gameplay);
            var inputs = new List<GameplayReachableInput>();
            foreach (string actorId in new[] { "player", "enemy-a", "enemy-b" })
            {
                inputs.Add(new GameplayReachableInput(
                    GameplayReachableInputKind.StanceControl,
                    "control.stance." + actorId,
                    actorId,
                    GameplayCapabilityProfiles.ChangeStance()));
                inputs.Add(new GameplayReachableInput(
                    GameplayReachableInputKind.EndTurnControl,
                    "control.end-turn." + actorId,
                    actorId,
                    GameplayCapabilityProfiles.EndTurn(emergency: false)));
            }
            GameplayTransitionReducerRegistry reducers =
                GameplaySimulationReducers.CreateCurrent();
            GameplayCapabilityRegistry capabilities =
                GameplayCurrentCapabilityCatalog.Create(reducers, inputs);
            var routes = new GameplayCandidateExecutionRouteRegistry(
                capabilities);
            routes.Register(new GameplayStanceCandidateExecutionRoute());
            routes.Register(new GameplayEndTurnCandidateExecutionRoute(
                gameplay.Scenario));
            var candidates = new GameplayReachableCandidateBuilder(
                capabilities);
            var runtime = new GameplaySimulationRuntime(
                CreateExecutionIdentity(gameplay),
                initial,
                reducers,
                capabilities);

            Assert.That(runtime.TryCreateReplaySinceActorLastTurn(
                "player",
                out _,
                out _), Is.False);

            Execute(runtime, routes, candidates.Build(inputs[1]));
            Execute(runtime, routes, candidates.Build(inputs[2]));
            Execute(runtime, routes, candidates.Build(inputs[3]));
            Execute(runtime, routes, candidates.Build(inputs[4]));
            Execute(runtime, routes, candidates.Build(inputs[5]));

            Assert.That(runtime.TryCreateReplaySinceActorLastTurn(
                "player",
                out GameplaySemanticReplayTimeline replay,
                out GameplayPlayerAwayReplayInterval interval), Is.True);
            Assert.That(interval.Windows.Select(window => window.ActorId),
                Is.EqualTo(new[] { "enemy-a", "enemy-b" }));
            Assert.That(interval.ActorIds,
                Is.EqualTo(new[] { "enemy-a", "enemy-b" }));
            Assert.That(interval.TransitionCount, Is.EqualTo(4));
            Assert.That(replay.Frames.Select(frame =>
                    frame.Transition.Identity.ActorId),
                Is.EqualTo(new[]
                {
                    "enemy-a",
                    "enemy-a",
                    "enemy-b",
                    "enemy-b",
                }));
            Assert.That(replay.Frames.Select(frame =>
                    frame.Transition.Profile.Capability),
                Is.EqualTo(new[]
                {
                    GameplaySemanticCapability.ChangeStance,
                    GameplaySemanticCapability.EndTurn,
                    GameplaySemanticCapability.ChangeStance,
                    GameplaySemanticCapability.EndTurn,
                }));

            var playback = new GameplaySemanticReplayPlaybackTimeline(replay);
            var summary = new GameplayReplayContentSummary(
                "Replay source: live since player's last turn",
                playback);
            summary.SetPresentationCompatibility(
                new GameplayReplayPresentationCompatibility(
                    summary.ReplayActorIds,
                    Array.Empty<string>()));

            Assert.That(playback.TurnGroups.Select(group => group.ActorId),
                Is.EqualTo(new[] { "enemy-a", "enemy-b" }));
            Assert.That(summary.SemanticFrames, Is.EqualTo(4));
            Assert.That(summary.EndTurnFrames, Is.EqualTo(2));
            Assert.That(summary.ActorPoseDeltaFrames, Is.EqualTo(2));
            Assert.That(summary.Transcript.Entries, Is.Empty);
            Assert.That(summary.IsReadyToOpen, Is.True);
            StringAssert.Contains("3/3 actors matched", summary.ToDisplayText());

            summary.SetPresentationCompatibility(
                new GameplayReplayPresentationCompatibility(
                    new[] { "player", "enemy-a" },
                    new[] { "enemy-b" }));

            Assert.That(summary.IsReadyToOpen, Is.False);
            Assert.That(summary.ValidationMessage, Is.EqualTo(
                "REPLAY ACTOR IDENTITIES DO NOT MATCH THE SCENE: enemy-b"));
        }

        [Test]
        public void EndTurnOnlyAwayIntervalIsRejectedWithExplicitDiagnostic()
        {
            GameplaySession gameplay = CreateGameplay();
            gameplay.BeginEncounter();
            GameplayCombatStateSnapshot initial =
                GameplayCombatStateCapture.Capture(gameplay);
            GameplayReachableInput[] inputs =
            {
                new GameplayReachableInput(
                    GameplayReachableInputKind.EndTurnControl,
                    "control.end-turn.player",
                    "player",
                    GameplayCapabilityProfiles.EndTurn(emergency: false)),
                new GameplayReachableInput(
                    GameplayReachableInputKind.EndTurnControl,
                    "control.end-turn.enemy-a",
                    "enemy-a",
                    GameplayCapabilityProfiles.EndTurn(emergency: false)),
            };
            GameplayTransitionReducerRegistry reducers =
                GameplaySimulationReducers.CreateCurrent();
            GameplayCapabilityRegistry capabilities =
                GameplayCurrentCapabilityCatalog.Create(reducers, inputs);
            var routes = new GameplayCandidateExecutionRouteRegistry(
                capabilities);
            routes.Register(new GameplayEndTurnCandidateExecutionRoute(
                gameplay.Scenario));
            var candidates = new GameplayReachableCandidateBuilder(
                capabilities);
            var runtime = new GameplaySimulationRuntime(
                CreateExecutionIdentity(gameplay),
                initial,
                reducers,
                capabilities);

            Execute(runtime, routes, candidates.Build(inputs[0]));
            Execute(runtime, routes, candidates.Build(inputs[1]));

            Assert.That(runtime.TryCreateReplaySinceActorLastTurn(
                "player",
                out GameplaySemanticReplayTimeline replay,
                out GameplayPlayerAwayReplayInterval interval), Is.True);
            Assert.That(interval.Windows.Select(window => window.ActorId),
                Is.EqualTo(new[] { "enemy-a" }));

            var summary = new GameplayReplayContentSummary(
                "Replay source: live since player's last turn",
                new GameplaySemanticReplayPlaybackTimeline(replay));

            Assert.That(summary.SemanticFrames, Is.EqualTo(1));
            Assert.That(summary.EndTurnFrames, Is.EqualTo(1));
            Assert.That(summary.ReplayableSemanticFrames, Is.Zero);
            Assert.That(summary.IsReadyToOpen, Is.False);
            Assert.That(summary.ValidationMessage, Is.EqualTo(
                "LATEST PLAYER-AWAY INTERVAL CONTAINS NO REPLAYABLE ACTIONS"));
        }

        private static GameplaySession CreateGameplay()
        {
            ScenarioActorDefinition CreateActor(string id, int initiative) =>
                new ScenarioActorDefinition(
                    id,
                    initiative,
                    new GameplayActorPose(
                        new GameplayPosition(initiative, 0f, 0f),
                        0f),
                    new TurnBudget(2, 8f),
                    combat: new ActorCombatDefinition(
                        id == "player" ? "party" : "hostile",
                        id == "player"
                            ? new[] { "hostile" }
                            : new[] { "party" },
                        maximumWounds: 10));
            return new GameplaySession(
                new ScenarioDefinition(
                    "player-away-replay-test",
                    new ScenarioTimingDefinition(1f),
                    new[]
                    {
                        CreateActor("player", 30),
                        CreateActor("enemy-a", 20),
                        CreateActor("enemy-b", 10),
                    },
                    Array.Empty<ScenarioObjectiveDefinition>()),
                scenarioSeed: 17u);
        }

        private static GameplayExecutionIdentity CreateExecutionIdentity(
            GameplaySession gameplay) => new GameplayExecutionIdentity(
                new GameplayContentIdentity(
                    gameplay.Scenario.Id,
                    scenarioSchemaVersion: 1,
                    rulesSchemaVersion: 1,
                    new string('a', 64)),
                new SpatialContentIdentity(
                    "player-away-replay-level",
                    levelSchemaVersion: 1,
                    evidenceAlgorithmVersion: 1,
                    new string('b', 64)),
                gameplay.RunIdentity);

        private static void Execute(
            GameplaySimulationRuntime runtime,
            GameplayCandidateExecutionRouteRegistry routes,
            GameplayCandidate candidate)
        {
            var context = new GameplayDecisionContext(
                runtime.CurrentState,
                GameplayObservationSnapshot.FullState(
                    candidate.ActorId,
                    runtime.CurrentState));
            GameplayExecutableCandidateEvaluation evaluation = routes.Evaluate(
                context,
                candidate);
            Assert.That(evaluation.IsLegal, Is.True, evaluation.FailureCode);
            runtime.Execute(routes.Prepare(context, evaluation));
        }
    }
}
