using System;
using GritGud.Application.Gameplay;
using NUnit.Framework;

namespace GritGud.Domain.Tests.Gameplay
{
    public sealed class GameplayBattleArtifactTests
    {
        private const string InitialHash =
            "0000000000000000000000000000000000000000000000000000000000000000";
        private const string FirstHash =
            "1111111111111111111111111111111111111111111111111111111111111111";
        private const string FinalHash =
            "2222222222222222222222222222222222222222222222222222222222222222";
        private const string CandidateDigest =
            "3333333333333333333333333333333333333333333333333333333333333333";
        private const string CanonicalJson = "{}";

        [Test]
        public void StructurallyLinkedTrajectoryIsAccepted()
        {
            Assert.DoesNotThrow(() => CreateContent());
        }

        [Test]
        public void DecisionMustIdentifyAnExistingTransition()
        {
            GameplayBattleArtifactTransition[] transitions =
            {
                CreateTransition(7L, InitialHash, FirstHash, null),
                CreateTransition(8L, FirstHash, FinalHash, null),
            };

            Assert.Throws<ArgumentException>(() => CreateContent(
                transitions: transitions));
        }

        [Test]
        public void TransitionCannotReferenceAnAbsentDecision()
        {
            GameplayBattleArtifactTransition[] transitions =
            {
                CreateTransition(7L, InitialHash, FirstHash, null),
                CreateTransition(8L, FirstHash, FinalHash, 1),
            };

            Assert.Throws<ArgumentException>(() => CreateContent(
                transitions: transitions));
        }

        [Test]
        public void DecisionAndTransitionMappingMustBeOneToOne()
        {
            GameplayBattleArtifactTransition[] transitions =
            {
                CreateTransition(7L, InitialHash, FirstHash, 0),
                CreateTransition(8L, FirstHash, FinalHash, 0),
            };

            Assert.Throws<ArgumentException>(() => CreateContent(
                transitions: transitions));
        }

        [TestCase(9L, FirstHash, FinalHash, null, TestName =
            "DecisionRejectsWrongTransitionSequence")]
        [TestCase(8L, InitialHash, FinalHash, null, TestName =
            "DecisionRejectsWrongPreviousHash")]
        [TestCase(8L, FirstHash, InitialHash, null, TestName =
            "DecisionRejectsWrongResultingHash")]
        [TestCase(8L, FirstHash, FinalHash,
            "4444444444444444444444444444444444444444444444444444444444444444",
            TestName = "DecisionRejectsWrongPayloadDigest")]
        public void DecisionMustMatchItsTransition(
            long sequence,
            string previousHash,
            string resultingHash,
            string payloadDigest)
        {
            GameplayBattleArtifactDecision[] decisions =
            {
                CreateDecision(
                    sequence,
                    previousHash,
                    resultingHash,
                    payloadDigest),
            };

            Assert.Throws<ArgumentException>(() => CreateContent(
                decisions: decisions));
        }

        [Test]
        public void DecisionActorMustMatchItsTransitionActor()
        {
            GameplayBattleArtifactDecision[] decisions =
            {
                CreateDecision(actorId: "other-actor"),
            };

            Assert.Throws<ArgumentException>(() => CreateContent(
                decisions: decisions));
        }

        [Test]
        public void TerminalSequenceMustIdentifyTheFinalTransition()
        {
            Assert.Throws<ArgumentException>(() => CreateContent(
                terminalSequence: 7L));
        }

        [TestCase(0, 2)]
        [TestCase(1, 1)]
        public void ScoreboardCountsMustMatchTrajectory(
            int decisionCount,
            int transitionCount)
        {
            Assert.Throws<ArgumentException>(() => CreateContent(
                scoreboard: CreateScoreboard(
                    decisionCount,
                    transitionCount)));
        }

        [Test]
        public void AdjacentTransitionSequencesMustRemainContiguous()
        {
            GameplayBattleArtifactTransition[] transitions =
            {
                CreateTransition(7L, InitialHash, FirstHash, null),
                CreateTransition(9L, FirstHash, FinalHash, 0),
            };
            GameplayBattleArtifactDecision[] decisions =
            {
                CreateDecision(sequence: 9L),
            };

            Assert.Throws<ArgumentException>(() => CreateContent(
                transitions,
                decisions,
                terminalSequence: 9L));
        }

        private static GameplayBattleArtifactContent CreateContent(
            GameplayBattleArtifactTransition[] transitions = null,
            GameplayBattleArtifactDecision[] decisions = null,
            long terminalSequence = 8L,
            GameplayBattleScoreboard scoreboard = null)
        {
            transitions ??= new[]
            {
                CreateTransition(7L, InitialHash, FirstHash, null),
                CreateTransition(8L, FirstHash, FinalHash, 0),
            };
            decisions ??= new[] { CreateDecision() };
            return new GameplayBattleArtifactContent(
                GameplayNumericPolicy.CurrentVersion,
                CreateExecutionIdentity(),
                new GameplayBattleArtifactProvenance(
                    "revision",
                    "branch",
                    "test"),
                InitialHash,
                CanonicalJson,
                transitions,
                decisions,
                new GameplayBattleArtifactTerminal(
                    GameplayBattleTerminalKind.PartyVictory,
                    terminalSequence,
                    FinalHash,
                    new[] { "actor" },
                    Array.Empty<string>(),
                    null,
                    string.Empty),
                scoreboard ?? CreateScoreboard(
                    decisions.Length,
                    transitions.Length));
        }

        private static GameplayBattleArtifactTransition CreateTransition(
            long sequence,
            string previousHash,
            string resultingHash,
            int? decisionIndex)
        {
            string payloadDigest = GameplayCanonicalValueDigest
                .CalculateCanonicalJson(CanonicalJson);
            return new GameplayBattleArtifactTransition(
                sequence,
                "Test",
                "actor",
                "subject",
                previousHash,
                resultingHash,
                payloadDigest,
                CanonicalJson,
                decisionIndex,
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                CanonicalJson);
        }

        private static GameplayBattleArtifactDecision CreateDecision(
            long sequence = 8L,
            string previousHash = FirstHash,
            string resultingHash = FinalHash,
            string payloadDigest = null,
            string actorId = "actor") =>
            new GameplayBattleArtifactDecision(
                0,
                "policy.test",
                1,
                actorId,
                previousHash,
                CandidateDigest,
                new[] { "candidate" },
                new[] { "candidate" },
                "candidate",
                GameplayPolicySelectionReason.HighestScore,
                1f,
                Array.Empty<GameplayPolicyScoreComponent>(),
                sequence,
                payloadDigest ?? GameplayCanonicalValueDigest
                    .CalculateCanonicalJson(CanonicalJson),
                resultingHash);

        private static GameplayBattleScoreboard CreateScoreboard(
            int decisions,
            int transitions) => new GameplayBattleScoreboard(
                decisions,
                transitions,
                turnsCompleted: 0,
                attacks: 0,
                hits: 0,
                wounds: 0,
                explosiveThrows: 0,
                concussiveTargets: 0,
                fireDeployments: 0,
                droneMoves: 0,
                droneAttacks: 0,
                Array.Empty<GameplayBattleActorScore>());

        private static GameplayExecutionIdentity CreateExecutionIdentity() =>
            new GameplayExecutionIdentity(
                new GameplayContentIdentity(
                    "scenario",
                    1,
                    1,
                    InitialHash),
                new SpatialContentIdentity(
                    "level",
                    1,
                    1,
                    FirstHash),
                new ScenarioRunIdentity("run", 7u));
    }
}
