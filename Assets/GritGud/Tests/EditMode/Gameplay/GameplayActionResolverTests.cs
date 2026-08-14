using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;
using NUnit.Framework;

namespace GritGud.Domain.Tests.Gameplay
{
    public sealed class GameplayActionResolverTests
    {
        [Test]
        public void TurnInteractionUsesTheTargetOwnedCost()
        {
            GameplaySession session = CreateSession(
                actorX: 0f,
                objectiveX: 1f,
                new ActionCost(2, 3f, ActionMobility.Set));
            session.EnterTurnMode();
            var resolver = new GameplayActionResolver(session);

            bool resolved = resolver.TryResolveInteraction(
                "player",
                "objective",
                out GameplayActionRecord record,
                out GameplayActionFailure failure);

            GameplayActorSnapshot actor = session.GetActor("player");
            Assert.That(resolved, Is.True);
            Assert.That(failure, Is.EqualTo(GameplayActionFailure.None));
            Assert.That(record.Request.ActionId, Is.EqualTo("objective.use"));
            Assert.That(record.Request.TargetId, Is.EqualTo("objective"));
            Assert.That(record.Cost.ActionPoints, Is.EqualTo(2));
            Assert.That(record.Cost.MovementOpportunity, Is.EqualTo(3f));
            Assert.That(record.Outcomes, Has.Count.EqualTo(1));
            Assert.That(
                record.Outcomes[0],
                Is.TypeOf<ObjectiveCompletedActionOutcome>());
            Assert.That(
                ((ObjectiveCompletedActionOutcome)record.Outcomes[0]).ObjectiveId,
                Is.EqualTo("objective"));
            Assert.That(actor.TurnBudget.ActionPoints, Is.EqualTo(2));
            Assert.That(actor.TurnBudget.MovementOpportunity, Is.EqualTo(5f));
            Assert.That(session.GetObjective("objective").IsCompleted, Is.True);
            Assert.That(session.Journal.Entries, Has.Count.EqualTo(2));
            Assert.That(
                session.Journal.Entries[1],
                Is.TypeOf<ActionResolvedJournalEntry>());
        }

        [Test]
        public void ExplorationInteractionIsContextualAndDoesNotSpendTurnBudget()
        {
            GameplaySession session = CreateSession(
                actorX: 0f,
                objectiveX: 1f,
                new ActionCost(3, 7f, ActionMobility.Set));
            var resolver = new GameplayActionResolver(session);

            Assert.That(resolver.TryResolveInteraction(
                "player",
                "objective",
                out GameplayActionRecord record,
                out GameplayActionFailure failure), Is.True);

            Assert.That(failure, Is.EqualTo(GameplayActionFailure.None));
            Assert.That(record.Cost.ActionPoints, Is.Zero);
            Assert.That(record.Cost.MovementOpportunity, Is.Zero);
            Assert.That(session.GetActor("player").TurnBudget.ActionPoints, Is.EqualTo(4));
            Assert.That(session.GetActor("player").TurnBudget.MovementOpportunity, Is.EqualTo(8f));
            Assert.That(session.GetObjective("objective").IsCompleted, Is.True);
        }

        [Test]
        public void UnaffordableTargetInteractionDoesNotMutateState()
        {
            GameplaySession session = CreateSession(
                actorX: 0f,
                objectiveX: 1f,
                new ActionCost(5, 0f, ActionMobility.Set));
            session.EnterTurnMode();
            var resolver = new GameplayActionResolver(session);

            Assert.That(resolver.TryResolveInteraction(
                "player",
                "objective",
                out GameplayActionRecord record,
                out GameplayActionFailure failure), Is.False);

            Assert.That(record, Is.Null);
            Assert.That(failure, Is.EqualTo(GameplayActionFailure.InsufficientActionPoints));
            Assert.That(session.GetObjective("objective").IsCompleted, Is.False);
            Assert.That(session.GetActor("player").TurnBudget.ActionPoints, Is.EqualTo(4));
            Assert.That(session.ResolvedActions, Is.Empty);
        }

        [Test]
        public void OutOfRangeInteractionDoesNotMutateObjectiveOrBudget()
        {
            GameplaySession session = CreateSession(
                actorX: 0f,
                objectiveX: 5f,
                new ActionCost(1, 1f, ActionMobility.Set));
            session.EnterTurnMode();
            var resolver = new GameplayActionResolver(session);

            bool resolved = resolver.TryResolveInteraction(
                "player",
                "objective",
                out GameplayActionRecord record,
                out GameplayActionFailure failure);

            Assert.That(resolved, Is.False);
            Assert.That(record, Is.Null);
            Assert.That(failure, Is.EqualTo(GameplayActionFailure.TargetOutOfRange));
            Assert.That(session.GetObjective("objective").IsCompleted, Is.False);
            Assert.That(session.GetActor("player").TurnBudget.ActionPoints, Is.EqualTo(4));
            Assert.That(session.ResolvedActions, Is.Empty);
        }

        [Test]
        public void InteractionRecordReplaysTheSameAuthoritativeOutcome()
        {
            ActionCost cost = new ActionCost(1, 2f, ActionMobility.Set);
            GameplaySession source = CreateSession(0f, 1f, cost);
            source.EnterTurnMode();
            var resolver = new GameplayActionResolver(source);
            Assert.That(resolver.TryResolveInteraction(
                "player",
                "objective",
                out GameplayActionRecord record,
                out GameplayActionFailure failure), Is.True);
            Assert.That(failure, Is.EqualTo(GameplayActionFailure.None));

            GameplaySession replay = CreateSession(0f, 1f, cost);
            replay.EnterTurnMode();
            replay.CommitAction(record);

            Assert.That(source.GetObjective("objective").IsCompleted, Is.True);
            Assert.That(replay.GetObjective("objective").IsCompleted, Is.True);
            Assert.That(
                replay.GetActor("player").TurnBudget.ActionPoints,
                Is.EqualTo(source.GetActor("player").TurnBudget.ActionPoints));
            Assert.That(
                replay.GetActor("player").TurnBudget.MovementOpportunity,
                Is.EqualTo(source.GetActor("player").TurnBudget.MovementOpportunity));
            Assert.That(replay.LastResolvedAction, Is.SameAs(record));
        }

        private static GameplaySession CreateSession(
            float actorX,
            float objectiveX,
            ActionCost interactionCost)
        {
            var actor = new ScenarioActorDefinition(
                "player",
                initiative: 10,
                new GameplayActorPose(
                    new GameplayPosition(actorX, 0f, 0f),
                    0f),
                new TurnBudget(4, 8f));
            var objective = new ScenarioObjectiveDefinition(
                "objective",
                new GameplayPosition(objectiveX, 0f, 0f),
                interactionRadius: 1.5f,
                new GameplayInteractionDefinition(
                    "objective.use",
                    "Use objective",
                    interactionCost));
            return new GameplaySession(new ScenarioDefinition(
                "action-test",
                new ScenarioTimingDefinition(1.25f),
                new[] { actor },
                new[] { objective }));
        }
    }
}
