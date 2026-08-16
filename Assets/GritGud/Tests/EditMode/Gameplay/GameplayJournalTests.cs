using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;
using NUnit.Framework;

namespace GritGud.Domain.Tests.Gameplay
{
    public sealed class GameplayJournalTests
    {
        [Test]
        public void SharedJournalOrdersTypedRecordsAcrossGameplaySubsystems()
        {
            var journal = new GameplayJournal();
            GameplaySession gameplay = CreateGameplay(journal);
            var destructibles = new DestructiblePropSession(
                new[]
                {
                    new DestructiblePropDefinition(
                        "crate",
                        10f,
                        DestructiblePropState.Intact,
                        new GameplayPosition(2f, 0f, 0f)),
                },
                journal);
            var vehicle = new VehicleMomentumSession(
                new VehicleMomentumProfile(
                    10f,
                    4f,
                    2f,
                    75f,
                    25f,
                    0.6f,
                    0.16f),
                new VehicleMomentumState(
                    "vehicle",
                    new GameplayPosition(0f, 0f, 0f),
                    0f,
                    0f),
                journal);

            Assert.That(gameplay.EnterTurnMode(), Is.True);
            var actions = new GameplayActionResolver(gameplay);
            Assert.That(actions.TryResolveInteraction(
                "player",
                "objective",
                out _,
                out _), Is.True);
            Assert.That(destructibles.TryApplyDamage("crate", 3f, out _), Is.True);
            Assert.That(vehicle.TryResolvePath(
                new[]
                {
                    new GameplayPosition(0f, 0f, 0f),
                    new GameplayPosition(0f, 0f, 1f),
                },
                out _,
                out _), Is.True);

            Assert.That(journal.Entries, Has.Count.EqualTo(4));
            Assert.That(journal.Entries[0], Is.TypeOf<TurnModeChangedJournalEntry>());
            Assert.That(journal.Entries[1], Is.TypeOf<ActionResolvedJournalEntry>());
            Assert.That(journal.Entries[2], Is.TypeOf<DestructibleDamagedJournalEntry>());
            Assert.That(journal.Entries[3], Is.TypeOf<VehicleMomentumResolvedJournalEntry>());
            Assert.That(journal.Entries[0].Sequence, Is.EqualTo(1));
            Assert.That(journal.LastEntry.Sequence, Is.EqualTo(4));
        }

        [Test]
        public void TurnReviewWindowIncludesActorsLastTurnAndEverythingSince()
        {
            var journal = new GameplayJournal();
            GameplaySession gameplay = CreateEncounterGameplay(journal);
            Assert.That(gameplay.BeginEncounter(), Is.True);

            gameplay.SpendMovement("player", 1f);
            Assert.That(gameplay.TryEndTurn("player", out _), Is.True);
            Assert.That(gameplay.TryEndTurn("enemy", out _), Is.True);

            long secondPlayerTurnStart = journal.LastEntry.Sequence + 1;
            gameplay.SpendMovement("player", 2f);
            Assert.That(gameplay.TryEndTurn("player", out _), Is.True);
            gameplay.SpendMovement("enemy", 3f);

            var review = gameplay.Journal.GetTurnReviewWindow("player");

            Assert.That(review, Is.Not.Empty);
            Assert.That(review[0].Sequence, Is.EqualTo(secondPlayerTurnStart));
            Assert.That(
                review[0],
                Is.TypeOf<MovementBudgetSpentJournalEntry>());
            Assert.That(
                ((MovementBudgetSpentJournalEntry)review[0]).ActorId,
                Is.EqualTo("player"));
            Assert.That(
                review,
                Has.Some.Matches<GameplayJournalEntry>(entry =>
                    entry is TurnEndedJournalEntry ended
                    && ended.Turn.EndingActorId == "player"));
            Assert.That(
                review[review.Count - 1],
                Is.TypeOf<MovementBudgetSpentJournalEntry>());
            Assert.That(
                ((MovementBudgetSpentJournalEntry)review[review.Count - 1])
                    .ActorId,
                Is.EqualTo("enemy"));
        }

        [Test]
        public void TurnReviewWindowIsUnavailableUntilActorCompletesATurn()
        {
            var journal = new GameplayJournal();
            GameplaySession gameplay = CreateEncounterGameplay(journal);
            Assert.That(gameplay.BeginEncounter(), Is.True);
            gameplay.SpendMovement("player", 1f);

            Assert.That(
                gameplay.Journal.GetTurnReviewWindow("player"),
                Is.Empty);
        }

        private static GameplaySession CreateGameplay(GameplayJournal journal)
        {
            var actor = new ScenarioActorDefinition(
                "player",
                10,
                new GameplayActorPose(new GameplayPosition(0f, 0f, 0f), 0f),
                new TurnBudget(4, 8f));
            var objective = new ScenarioObjectiveDefinition(
                "objective",
                new GameplayPosition(0f, 0f, 0f),
                1f,
                new GameplayInteractionDefinition(
                    "objective.use",
                    "Use objective",
                    new ActionCost(1, 1f, ActionMobility.Set)));
            return new GameplaySession(
                new ScenarioDefinition(
                    "journal-test",
                    new ScenarioTimingDefinition(1.25f),
                    new[] { actor },
                    new[] { objective }),
                journal);
        }

        private static GameplaySession CreateEncounterGameplay(
            GameplayJournal journal)
        {
            var player = new ScenarioActorDefinition(
                "player",
                10,
                new GameplayActorPose(new GameplayPosition(0f, 0f, 0f), 0f),
                new TurnBudget(4, 8f));
            var enemy = new ScenarioActorDefinition(
                "enemy",
                0,
                new GameplayActorPose(new GameplayPosition(4f, 0f, 0f), 180f),
                new TurnBudget(4, 8f));
            return new GameplaySession(
                new ScenarioDefinition(
                    "turn-review-test",
                    new ScenarioTimingDefinition(1.25f),
                    new[] { player, enemy }),
                journal);
        }
    }
}
