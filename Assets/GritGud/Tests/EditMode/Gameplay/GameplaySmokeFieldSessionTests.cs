using System;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;
using NUnit.Framework;

namespace GritGud.Domain.Tests.Gameplay
{
    public sealed class GameplaySmokeFieldSessionTests
    {
        [Test]
        public void SmokeBlocksOnlySegmentsWithEnoughInteriorTraversal()
        {
            using var smoke = new GameplaySmokeFieldSession(CreateGameplay());
            smoke.Deploy(CreateField(minimumObscuredPath: 0.75f));

            Assert.That(smoke.BlocksSight(
                new GameplayPosition(-5f, 1.4f, 0f),
                new GameplayPosition(5f, 1.4f, 0f)), Is.True);
            Assert.That(smoke.BlocksSight(
                new GameplayPosition(-5f, 1.4f, 3.99f),
                new GameplayPosition(5f, 1.4f, 3.99f)), Is.False);
            Assert.That(smoke.BlocksSight(
                new GameplayPosition(-5f, 3.1f, 0f),
                new GameplayPosition(5f, 3.1f, 0f)), Is.False);
        }

        [Test]
        public void ExplorationLifetimeExpiresByContinuousTime()
        {
            using var smoke = new GameplaySmokeFieldSession(CreateGameplay());
            SmokeFieldRecord field = CreateField(
                explorationDurationSeconds: 10f);
            SmokeFieldRecord expired = null;
            smoke.FieldExpired += value => expired = value;
            smoke.Deploy(field);

            smoke.AdvanceContinuousTime(4f);

            Assert.That(smoke.ActiveCount, Is.EqualTo(1));
            Assert.That(
                smoke.CaptureActiveFields()[0].RemainingFraction,
                Is.EqualTo(0.6f).Within(0.0001f));

            smoke.AdvanceContinuousTime(6f);

            Assert.That(smoke.ActiveCount, Is.Zero);
            Assert.That(expired, Is.SameAs(field));
            Assert.That(smoke.Revision, Is.EqualTo(2));
        }

        [Test]
        public void TacticalLifetimeExpiresOnAuthoredNumberOfTurnEnds()
        {
            GameplaySession gameplay = CreateGameplay();
            using var smoke = new GameplaySmokeFieldSession(gameplay);
            smoke.Deploy(CreateField(durationTurnEnds: 2));
            Assert.That(gameplay.BeginEncounter(), Is.True);

            Assert.That(gameplay.TryEndTurn("player", out _), Is.True);
            Assert.That(smoke.ActiveCount, Is.EqualTo(1));

            Assert.That(gameplay.TryEndTurn("player", out _), Is.True);
            Assert.That(smoke.ActiveCount, Is.Zero);
        }

        private static GameplaySession CreateGameplay()
        {
            var actor = new ScenarioActorDefinition(
                "player",
                10,
                new GameplayActorPose(new GameplayPosition(0f, 0f, 0f), 0f),
                new TurnBudget(4, 8f));
            return new GameplaySession(new ScenarioDefinition(
                "smoke-test",
                new ScenarioTimingDefinition(1f),
                new[] { actor },
                Array.Empty<ScenarioObjectiveDefinition>(),
                Array.Empty<AttackResponseDefinition>()));
        }

        private static SmokeFieldRecord CreateField(
            float minimumObscuredPath = 0.75f,
            float explorationDurationSeconds = 24f,
            int durationTurnEnds = 4) =>
            new SmokeFieldRecord(
                "smoke.player.1",
                "player",
                "item.smoke-grenade",
                new GameplayPosition(0f, 0f, 0f),
                new SmokeFieldDefinition(
                    4f,
                    2.8f,
                    explorationDurationSeconds,
                    durationTurnEnds,
                    minimumObscuredPath));
    }
}
