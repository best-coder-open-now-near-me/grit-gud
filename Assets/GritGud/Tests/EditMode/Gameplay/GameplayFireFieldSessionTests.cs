using System;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;
using NUnit.Framework;

namespace GritGud.Domain.Tests.Gameplay
{
    public sealed class GameplayFireFieldSessionTests
    {
        [Test]
        public void ExplorationPulseExpandsAndDamagesCanonicalSubjects()
        {
            GameplaySession gameplay = CreateGameplay();
            var destructibles = new DestructiblePropSession(
                new[]
                {
                    new DestructiblePropDefinition(
                        "crate",
                        3f,
                        DestructiblePropState.Intact,
                        new GameplayPosition(0f, 0f, 0.5f)),
                },
                gameplay.Journal);
            using var fire = new GameplayFireFieldSession(
                gameplay,
                destructibles);
            fire.Deploy(CreateField());

            fire.AdvanceContinuousTime(2f);

            FireFieldSnapshot snapshot = fire.CaptureActiveFields()[0];
            Assert.That(snapshot.CurrentRadius, Is.GreaterThan(1f));
            Assert.That(gameplay.GetActor("player").Wounds.WoundCount,
                Is.EqualTo(1));
            Assert.That(destructibles.GetProp("crate").RemainingIntegrity,
                Is.EqualTo(1f));
            Assert.That(fire.CalculateHazardTraversal(
                    new GameplayPosition(-3f, 0.5f, 0f),
                    new GameplayPosition(3f, 0.5f, 0f)),
                Is.GreaterThanOrEqualTo(
                    snapshot.Field.Definition.MinimumHazardPath));
        }

        private static GameplaySession CreateGameplay()
        {
            var actor = new ScenarioActorDefinition(
                "player",
                10,
                new GameplayActorPose(new GameplayPosition(0f, 0f, 0f), 0f),
                new TurnBudget(4, 8f));
            return new GameplaySession(new ScenarioDefinition(
                "fire-test",
                new ScenarioTimingDefinition(1f),
                new[] { actor },
                Array.Empty<ScenarioObjectiveDefinition>(),
                Array.Empty<AttackResponseDefinition>()));
        }

        private static FireFieldRecord CreateField() => new FireFieldRecord(
            "fire.player.1",
            "player",
            "item.incendiary-grenade",
            new GameplayPosition(0f, 0f, 0f),
            new FireFieldDefinition(
                1f,
                2f,
                2f,
                6f,
                3,
                2f,
                1f,
                2f,
                0.5f));
    }
}
