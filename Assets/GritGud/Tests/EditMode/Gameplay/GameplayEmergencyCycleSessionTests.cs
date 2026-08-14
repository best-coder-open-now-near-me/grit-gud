using System;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;
using NUnit.Framework;

namespace GritGud.Domain.Tests.Gameplay
{
    public sealed class GameplayEmergencyCycleSessionTests
    {
        [Test]
        public void NonProjectileTriggerUsesSharedEmergencyLifecycle()
        {
            GameplaySession gameplay = CreateGameplay();
            gameplay.BeginEncounter();
            var resolution = new TestResolution();
            var cycle = new GameplayEmergencyCycleSession(gameplay);

            Assert.That(cycle.TryOpen("environment", "alarm.01", "player", 1, resolution), Is.True);
            Assert.That(cycle.TryEndTurn("player", out _), Is.True);
            Assert.That(gameplay.ActiveActorId, Is.EqualTo("guard"));
            Assert.That(gameplay.GetActor("guard").TurnBudget.ActionPoints, Is.EqualTo(1));

            Assert.That(cycle.TryEndTurn("guard", out _), Is.True);
            Assert.That(resolution.ResolveCount, Is.EqualTo(1));
            Assert.That(cycle.CurrentWindow.Status, Is.EqualTo(EmergencyReactionWindowStatus.Completed));
            Assert.That(gameplay.ActiveActorId, Is.EqualTo("player"));
        }

        private static GameplaySession CreateGameplay()
        {
            var player = new ScenarioActorDefinition(
                "player", 10, new GameplayActorPose(new GameplayPosition(0f, 0f, 0f), 0f),
                new TurnBudget(4, 8f));
            var guard = new ScenarioActorDefinition(
                "guard", 0, new GameplayActorPose(new GameplayPosition(0f, 0f, 5f), 180f),
                new TurnBudget(4, 8f));
            return new GameplaySession(new ScenarioDefinition(
                "emergency-cycle-test", new ScenarioTimingDefinition(1f),
                new[] { player, guard }, Array.Empty<ScenarioObjectiveDefinition>()));
        }

        private sealed class TestResolution : IEmergencyCycleResolution
        {
            public int ResolveCount { get; private set; }
            public bool IsResolved => ResolveCount > 0;
            public void ResolveAfterResponsePass() => ResolveCount++;
        }
    }
}
