using System.Linq;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;
using NUnit.Framework;

namespace GritGud.Domain.Tests.Gameplay
{
    public sealed class TurnReplayWindowTests
    {
        [Test]
        public void WindowBeginsWithActiveActorsPriorTurnAndNestsReactions()
        {
            GameplaySession session = CreateSession();
            Assert.That(session.BeginEncounter(), Is.True);
            string anchorActorId = session.ActiveActorId;

            Assert.That(TurnReplayWindowProjector.TryProject(
                session.Journal,
                anchorActorId,
                out _), Is.False);

            Assert.That(session.TryEndTurn(anchorActorId, out _), Is.True);
            string interruptedActorId = session.ActiveActorId;
            string responderId = session.InitiativeOrder.First(
                actorId => actorId != anchorActorId
                    && actorId != interruptedActorId);
            session.BeginEmergencyReaction(
                interruptedActorId,
                new[] { responderId },
                actionPointAllowance: 1);
            Assert.That(session.TryEndEmergencyTurn(
                responderId,
                out bool passCompleted,
                out _), Is.True);
            Assert.That(passCompleted, Is.True);
            session.CompleteEmergencyReaction(interruptedActorId);

            while (!string.Equals(
                session.ActiveActorId,
                anchorActorId,
                System.StringComparison.Ordinal))
            {
                Assert.That(session.TryEndTurn(
                    session.ActiveActorId,
                    out _), Is.True);
            }

            Assert.That(TurnReplayWindowProjector.TryProject(
                session.Journal,
                anchorActorId,
                out TurnReplayWindow window), Is.True);
            Assert.That(window.ActorId, Is.EqualTo(anchorActorId));
            Assert.That(window.DefaultPlayheadBoundary, Is.EqualTo(1));
            Assert.That(
                window.Segments.Select(segment => segment.ActorId),
                Is.EqualTo(session.InitiativeOrder));
            TurnReplaySegment interruptedSegment = window.Segments.Single(
                segment => segment.ActorId == interruptedActorId);
            Assert.That(interruptedSegment.Entries
                .OfType<TurnEndedJournalEntry>()
                .Count(entry =>
                    entry.Turn.Kind == GameplayTurnKind.EmergencyReaction),
                Is.EqualTo(1));
        }

        [Test]
        public void ProjectedWindowDoesNotChangeWhenJournalAdvances()
        {
            GameplaySession session = CreateSession();
            Assert.That(session.BeginEncounter(), Is.True);
            string anchorActorId = session.ActiveActorId;
            do
            {
                Assert.That(session.TryEndTurn(
                    session.ActiveActorId,
                    out _), Is.True);
            }
            while (!string.Equals(
                session.ActiveActorId,
                anchorActorId,
                System.StringComparison.Ordinal));

            Assert.That(TurnReplayWindowProjector.TryProject(
                session.Journal,
                anchorActorId,
                out TurnReplayWindow window), Is.True);
            int segmentCount = window.Segments.Count;
            int entryCount = window.Segments.Sum(segment => segment.Entries.Count);

            Assert.That(session.TryEndTurn(anchorActorId, out _), Is.True);

            Assert.That(window.Segments, Has.Count.EqualTo(segmentCount));
            Assert.That(
                window.Segments.Sum(segment => segment.Entries.Count),
                Is.EqualTo(entryCount));
        }

        private static GameplaySession CreateSession()
        {
            return new GameplaySession(new ScenarioDefinition(
                "turn-replay-test",
                new ScenarioTimingDefinition(1f),
                new[]
                {
                    CreateActor("mara", 14),
                    CreateActor("raider", 10),
                    CreateActor("guard", 6),
                },
                System.Array.Empty<ScenarioObjectiveDefinition>()));
        }

        private static ScenarioActorDefinition CreateActor(
            string actorId,
            int initiative)
        {
            return new ScenarioActorDefinition(
                actorId,
                initiative,
                new GameplayActorPose(
                    new GameplayPosition(initiative, 0f, 0f),
                    0f),
                new TurnBudget(4, 8f));
        }
    }
}
