using System;
using System.Linq;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;
using NUnit.Framework;

namespace GritGud.Domain.Tests.Gameplay
{
    public sealed class GameplaySessionTests
    {
        [Test]
        public void EnterTurnModeUsesDeterministicInitiative()
        {
            GameplaySession session = CreateSession(
                CreateActor("bravo", initiative: 10),
                CreateActor("charlie", initiative: 5),
                CreateActor("alpha", initiative: 10));

            bool entered = session.EnterTurnMode();

            Assert.That(entered, Is.True);
            Assert.That(session.Mode, Is.EqualTo(GameplaySessionMode.TurnBased));
            Assert.That(session.InitiativeOrder,
                Is.EqualTo(new[] { "alpha", "bravo", "charlie" }));
            Assert.That(session.ActiveActorId, Is.EqualTo("alpha"));
            Assert.That(session.InitiativeResults, Has.Count.EqualTo(3));
            Assert.That(session.InitiativeResults[0].ActorId, Is.EqualTo("alpha"));
            Assert.That(session.InitiativeResults[0].Dexterity, Is.EqualTo(10));
            Assert.That(session.InitiativeResults[0].ParticipantCount, Is.EqualTo(3));
            Assert.That(session.InitiativeResults[0].ReactionAdvance, Is.EqualTo(3));

            GameplaySession repeated = CreateSession(
                CreateActor("bravo", initiative: 10),
                CreateActor("charlie", initiative: 5),
                CreateActor("alpha", initiative: 10));
            Assert.That(repeated.InitiativeOrder, Is.EqualTo(session.InitiativeOrder));
            Assert.That(repeated.InitiativeResults[0].ReactionAdvance,
                Is.EqualTo(session.InitiativeResults[0].ReactionAdvance));
        }

        [Test]
        public void InitiativeDiagnosticExplainsReactionAdvanceAndEqualTurns()
        {
            GameplaySession session = CreateSession(
                CreateActor("bravo", initiative: 10),
                CreateActor("alpha", initiative: 10));

            GameplayDiagnosticProjection diagnostic =
                GameplayCombatDiagnosticFormatter.FormatInitiative(session);

            Assert.That(diagnostic.Title, Is.EqualTo("Initiative order"));
            Assert.That(diagnostic.Lines, Has.Count.EqualTo(3));
            StringAssert.Contains("DEX 10 with 2 combatants", diagnostic.Lines[0]);
            StringAssert.Contains("advance 2", diagnostic.Lines[0]);
            StringAssert.Contains("position 1", diagnostic.Lines[0]);
            StringAssert.Contains("repeat this order", diagnostic.Lines[2]);
        }

        [Test]
        public void VoluntaryExitCompletesCycleAndLocksReentryForOneMinimumTurn()
        {
            GameplaySession session = CreateSession(CreateActor("player", 10));
            VoluntaryTurnCycleRecord completedCycle = null;
            session.VoluntaryTurnCycleCompleted += cycle => completedCycle = cycle;
            session.EnterTurnMode();
            session.SpendMovement("player", 3f);
            ResolveInteraction(session, "player", "raised-deck");

            bool exited = session.TryExitTurnMode(out TurnModeExitFailure failure);
            bool immediateReentry = session.TryEnterTurnMode(
                out TurnModeEntryFailure entryFailure);
            GameplayActorSnapshot actor = session.GetActor("player");

            Assert.That(exited, Is.True);
            Assert.That(failure, Is.EqualTo(TurnModeExitFailure.None));
            Assert.That(immediateReentry, Is.False);
            Assert.That(entryFailure,
                Is.EqualTo(TurnModeEntryFailure.VoluntaryReentryLocked));
            Assert.That(session.CanEnterTurnMode, Is.False);
            Assert.That(session.VoluntaryTurnReentrySecondsRemaining,
                Is.EqualTo(session.Scenario.Timing.MinimumVoluntaryTurnSeconds));
            Assert.That(completedCycle, Is.Not.Null);
            Assert.That(completedCycle.Sequence, Is.EqualTo(1));
            Assert.That(completedCycle.Actors.Count, Is.EqualTo(1));
            Assert.That(
                completedCycle.Actors[0].TurnBudget.ActionPoints,
                Is.EqualTo(3));
            Assert.That(
                completedCycle.Actors[0].TurnBudget.MovementOpportunity,
                Is.EqualTo(4f));
            Assert.That(
                session.LastCompletedVoluntaryTurnCycle,
                Is.SameAs(completedCycle));
            Assert.That(actor.TurnBudget.ActionPoints, Is.EqualTo(4));
            Assert.That(actor.TurnBudget.MovementOpportunity, Is.EqualTo(8f));

            session.AdvanceContinuousTime(
                session.Scenario.Timing.MinimumVoluntaryTurnSeconds - 0.01f);
            Assert.That(session.EnterTurnMode(), Is.False);
            Assert.That(session.VoluntaryTurnReentrySecondsRemaining,
                Is.EqualTo(0.01f).Within(0.0001f));

            session.AdvanceContinuousTime(0.01f);
            Assert.That(session.CanEnterTurnMode, Is.True);
            Assert.That(session.EnterTurnMode(), Is.True);
            Assert.That(session.ActiveActorId, Is.EqualTo("player"));
            Assert.That(session.TurnContext,
                Is.EqualTo(TurnModeContext.Voluntary));
        }

        [Test]
        public void InitiatedEncounterInterruptsVoluntaryReentryLockout()
        {
            GameplaySession session = CreateSession(CreateActor("player", 10));
            session.EnterTurnMode();
            Assert.That(session.TryExitTurnMode(out _), Is.True);
            Assert.That(session.CanEnterTurnMode, Is.False);

            Assert.That(session.BeginEncounter(), Is.True);

            Assert.That(session.Mode, Is.EqualTo(GameplaySessionMode.TurnBased));
            Assert.That(session.TurnContext,
                Is.EqualTo(TurnModeContext.InitiatedEncounter));
        }

        [Test]
        public void InitiatedEncounterBlocksExitWithoutRefreshingResources()
        {
            GameplaySession session = CreateSession(CreateActor("player", 10));
            session.EnterTurnMode();
            session.SpendMovement("player", 3f);
            ResolveInteraction(session, "player", "raised-deck");
            Assert.That(session.BeginEncounter(), Is.True);

            bool exited = session.TryExitTurnMode(
                out TurnModeExitFailure failure);
            GameplayActorSnapshot actor = session.GetActor("player");

            Assert.That(exited, Is.False);
            Assert.That(failure, Is.EqualTo(TurnModeExitFailure.EncounterActive));
            Assert.That(session.Mode, Is.EqualTo(GameplaySessionMode.TurnBased));
            Assert.That(
                session.TurnContext,
                Is.EqualTo(TurnModeContext.InitiatedEncounter));
            Assert.That(actor.TurnBudget.ActionPoints, Is.EqualTo(3));
            Assert.That(actor.TurnBudget.MovementOpportunity, Is.EqualTo(4f));
            Assert.That(session.LastCompletedVoluntaryTurnCycle, Is.Null);

            Assert.That(session.CompleteEncounter(), Is.True);
            Assert.That(session.TryExitTurnMode(out failure), Is.True);
            Assert.That(
                session.GetActor("player").TurnBudget.ActionPoints,
                Is.EqualTo(4));
        }

        [Test]
        public void ResolvedActionsDoNotWaitOnPresentationTiming()
        {
            GameplaySession session = CreateSession(CreateActor("player", 10));
            session.EnterTurnMode();
            ResolveInteraction(session, "player", "raised-deck");

            bool exited = session.TryExitTurnMode(out TurnModeExitFailure failure);

            Assert.That(exited, Is.True);
            Assert.That(failure, Is.EqualTo(TurnModeExitFailure.None));
            Assert.That(session.Mode, Is.EqualTo(GameplaySessionMode.Exploration));
        }

        [Test]
        public void EndTurnAdvancesEncounterInitiativeWithoutExitingTurnMode()
        {
            GameplaySession session = CreateSession(
                CreateActor("player", 10),
                CreateActor("target", 0));
            TurnEndRecord endedTurn = null;
            session.TurnEnded += record => endedTurn = record;
            Assert.That(session.BeginEncounter(), Is.True);
            session.SpendMovement("player", 3f);

            bool ended = session.TryEndTurn("player", out TurnEndFailure failure);

            Assert.That(ended, Is.True);
            Assert.That(failure, Is.EqualTo(TurnEndFailure.None));
            Assert.That(session.Mode, Is.EqualTo(GameplaySessionMode.TurnBased));
            Assert.That(session.ActiveActorId, Is.EqualTo("target"));
            Assert.That(endedTurn, Is.SameAs(session.LastEndedTurn));
            Assert.That(endedTurn.EndingActorId, Is.EqualTo("player"));
            Assert.That(endedTurn.NextActorId, Is.EqualTo("target"));
            Assert.That(session.LastCompletedVoluntaryTurnCycle, Is.Null);
        }

        [Test]
        public void ActiveActorChangePublishesFullyRefreshedTurnState()
        {
            GameplaySession session = CreateSession(
                CreateActor("player", 10),
                CreateActor("target", 0));
            session.BeginEncounter();
            session.SpendMovement("player", 3f);
            GameplayActiveActorChange? observed = null;
            TurnBudget observedBudget = default;
            session.ActiveActorChanged += change =>
            {
                observed = change;
                observedBudget = session.GetActor(change.CurrentActorId)
                    .TurnBudget;
            };

            Assert.That(session.TryEndTurn("player", out _), Is.True);

            Assert.That(observed.HasValue, Is.True);
            Assert.That(observed.Value.PreviousActorId, Is.EqualTo("player"));
            Assert.That(observed.Value.CurrentActorId, Is.EqualTo("target"));
            Assert.That(observedBudget.ActionPoints, Is.EqualTo(4));
            Assert.That(observedBudget.MovementOpportunity, Is.EqualTo(8f));
        }

        [Test]
        public void TurnModeObserversSeeTheCommittedContextAndJournal()
        {
            GameplaySession session = CreateSession(
                CreateActor("player", 10));
            TurnModeContext observedContext = TurnModeContext.None;
            GameplaySessionOperation observedOperation =
                GameplaySessionOperation.ResolvingWorldTurn;
            TurnModeChangedJournalEntry observedJournal = null;
            session.ModeChanged += _ =>
            {
                observedContext = session.TurnContext;
                observedOperation = session.Operation;
                observedJournal = session.Journal.Entries
                    .OfType<TurnModeChangedJournalEntry>()
                    .LastOrDefault();
            };

            Assert.That(session.EnterTurnMode(), Is.True);

            Assert.That(observedContext, Is.EqualTo(TurnModeContext.Voluntary));
            Assert.That(
                observedOperation,
                Is.EqualTo(GameplaySessionOperation.None));
            Assert.That(observedJournal, Is.Not.Null);
            Assert.That(
                observedJournal.ResultingMode,
                Is.EqualTo(GameplaySessionMode.TurnBased));
            Assert.That(observedJournal.Context, Is.EqualTo(observedContext));
            Assert.That(observedJournal.ActiveActorId, Is.EqualTo("player"));
        }

        [Test]
        public void ThrowingTurnObserverCannotInterruptCommitOrLaterObservers()
        {
            GameplaySession session = CreateSession(
                CreateActor("player", 10),
                CreateActor("target", 0));
            Assert.That(session.BeginEncounter(), Is.True);
            TurnEndRecord observedCommittedTurn = null;
            TurnEndRecord observedJournalTurn = null;
            bool laterActorObserverRan = false;
            bool turnObserverRan = false;
            session.ActiveActorChanged += _ =>
            {
                observedCommittedTurn = session.LastEndedTurn;
                observedJournalTurn = session.Journal.Entries
                    .OfType<TurnEndedJournalEntry>()
                    .LastOrDefault()
                    ?.Turn;
                throw new InvalidOperationException("projection failed");
            };
            session.ActiveActorChanged += _ => laterActorObserverRan = true;
            session.TurnEnded += _ => turnObserverRan = true;

            InvalidOperationException exception =
                Assert.Throws<InvalidOperationException>(() =>
                    session.TryEndTurn("player", out _));

            Assert.That(exception.Message, Is.EqualTo("projection failed"));
            Assert.That(session.ActiveActorId, Is.EqualTo("target"));
            Assert.That(observedCommittedTurn, Is.SameAs(session.LastEndedTurn));
            Assert.That(observedJournalTurn, Is.SameAs(session.LastEndedTurn));
            Assert.That(session.LastEndedTurn.EndingActorId, Is.EqualTo("player"));
            Assert.That(session.LastEndedTurn.NextActorId, Is.EqualTo("target"));
            Assert.That(laterActorObserverRan, Is.True);
            Assert.That(turnObserverRan, Is.True);
        }

        [Test]
        public void RequestedEncounterCompletionClosesOnEndTurnAndRefreshesMovement()
        {
            GameplaySession session = CreateSession(CreateActor("player", 10));
            Assert.That(session.BeginEncounter(), Is.True);
            session.SpendMovement("player", 3f);

            Assert.That(
                session.RequestEncounterCompletionAtTurnEnd(),
                Is.True);
            Assert.That(session.EncounterActive, Is.True);
            Assert.That(session.EncounterCompletionRequested, Is.True);
            Assert.That(
                session.GetActor("player").TurnBudget.MovementOpportunity,
                Is.EqualTo(5f));

            Assert.That(
                session.TryEndTurn("player", out TurnEndFailure failure),
                Is.True);

            Assert.That(failure, Is.EqualTo(TurnEndFailure.None));
            Assert.That(session.EncounterActive, Is.False);
            Assert.That(session.EncounterCompletionRequested, Is.False);
            Assert.That(session.Mode, Is.EqualTo(GameplaySessionMode.Exploration));
            Assert.That(session.Operation, Is.EqualTo(GameplaySessionOperation.None));
            Assert.That(session.TurnContext, Is.EqualTo(TurnModeContext.None));
            Assert.That(
                session.GetActor("player").TurnBudget.MovementOpportunity,
                Is.EqualTo(8f));
            Assert.That(session.LastCompletedVoluntaryTurnCycle, Is.Not.Null);
            Assert.That(
                session.LastCompletedVoluntaryTurnCycle.Actors[0]
                    .TurnBudget.MovementOpportunity,
                Is.EqualTo(5f));
            Assert.That(
                session.VoluntaryTurnReentrySecondsRemaining,
                Is.EqualTo(session.Scenario.Timing.MinimumVoluntaryTurnSeconds));
            Assert.That(session.LastEndedTurn.EndingActorId, Is.EqualTo("player"));
            Assert.That(session.LastEndedTurn.NextActorId, Is.EqualTo("player"));
        }

        [Test]
        public void EndTurnLocksVoluntaryModeUntilWorldTurnCompletes()
        {
            GameplaySession session = CreateSession(CreateActor("player", 10));
            VoluntaryTurnCycleRecord completedCycle = null;
            TurnEndRecord endedTurn = null;
            session.VoluntaryTurnCycleCompleted += cycle => completedCycle = cycle;
            session.TurnEnded += record => endedTurn = record;
            session.EnterTurnMode();
            session.SpendMovement("player", 3f);
            ResolveInteraction(session, "player", "raised-deck");

            Assert.That(
                session.TryEndTurn("player", out TurnEndFailure endFailure),
                Is.True);
            Assert.That(endFailure, Is.EqualTo(TurnEndFailure.None));
            Assert.That(session.Mode, Is.EqualTo(GameplaySessionMode.TurnBased));
            Assert.That(session.TurnContext, Is.EqualTo(TurnModeContext.Voluntary));
            Assert.That(session.ActiveActorId, Is.EqualTo("player"));
            Assert.That(session.Operation,
                Is.EqualTo(GameplaySessionOperation.ResolvingWorldTurn));
            Assert.That(session.PendingVoluntaryTurnCycle, Is.Not.Null);
            Assert.That(completedCycle, Is.Null);
            Assert.That(session.LastCompletedVoluntaryTurnCycle, Is.Null);
            Assert.That(session.GetActor("player").TurnBudget.ActionPoints,
                Is.EqualTo(3));
            Assert.That(session.GetActor("player").TurnBudget.MovementOpportunity,
                Is.EqualTo(4f));
            Assert.Throws<InvalidOperationException>(() =>
                session.SpendMovement("player", 1f));
            Assert.That(
                session.TryExitTurnMode(out TurnModeExitFailure exitFailure),
                Is.False);
            Assert.That(exitFailure,
                Is.EqualTo(TurnModeExitFailure.OperationInProgress));

            Assert.That(session.CompleteVoluntaryWorldTurn(), Is.True);

            Assert.That(
                session.Operation,
                Is.EqualTo(GameplaySessionOperation.None));
            Assert.That(session.PendingVoluntaryTurnCycle, Is.Null);
            Assert.That(completedCycle, Is.SameAs(
                session.LastCompletedVoluntaryTurnCycle));
            Assert.That(completedCycle.Actors[0].TurnBudget.ActionPoints,
                Is.EqualTo(3));
            Assert.That(completedCycle.Actors[0].TurnBudget.MovementOpportunity,
                Is.EqualTo(4f));
            Assert.That(session.GetActor("player").TurnBudget.ActionPoints,
                Is.EqualTo(4));
            Assert.That(session.GetActor("player").TurnBudget.MovementOpportunity,
                Is.EqualTo(8f));
            Assert.That(endedTurn, Is.SameAs(session.LastEndedTurn));
            Assert.That(endedTurn.EndingActorId, Is.EqualTo("player"));
            Assert.That(endedTurn.NextActorId, Is.EqualTo("player"));
            Assert.That(session.CompleteVoluntaryWorldTurn(), Is.False);
        }

        [Test]
        public void VoluntaryModeRestartsAtDeterministicInitiativeAfterEncounter()
        {
            GameplaySession session = CreateSession(
                CreateActor("player", 10),
                CreateActor("target", 0));
            session.BeginEncounter();
            Assert.That(session.TryEndTurn("player", out _), Is.True);
            Assert.That(session.ActiveActorId, Is.EqualTo("target"));
            Assert.That(session.CompleteEncounter(), Is.True);
            Assert.That(session.TryExitTurnMode(out _), Is.True);

            session.AdvanceContinuousTime(
                session.Scenario.Timing.MinimumVoluntaryTurnSeconds);

            Assert.That(session.EnterTurnMode(), Is.True);

            Assert.That(session.ActiveActorId, Is.EqualTo("player"));
            Assert.That(session.TurnContext, Is.EqualTo(TurnModeContext.Voluntary));
        }

        [Test]
        public void OnlyTheActiveActorCanSpendTurnResources()
        {
            GameplaySession session = CreateSession(
                CreateActor("player", initiative: 10),
                CreateActor("target", initiative: 0));
            session.EnterTurnMode();

            Assert.Throws<InvalidOperationException>(() =>
                session.SpendMovement("target", 1f));
            Assert.That(
                session.GetActor("target").TurnBudget.MovementOpportunity,
                Is.EqualTo(8f));
        }

        [Test]
        public void ScenarioRejectsDuplicateActorIdentifiers()
        {
            Assert.Throws<ArgumentException>(() =>
                new ScenarioDefinition(
                    "depot-training",
                    new ScenarioTimingDefinition(1.25f),
                    new[] { CreateActor("player", 10), CreateActor("player", 5) },
                    Array.Empty<ScenarioObjectiveDefinition>()));
        }

        [Test]
        public void ActorFacingIsNormalizedForAuthoritativeState()
        {
            var pose = new GameplayActorPose(
                new GameplayPosition(1f, 2f, 3f),
                -90f);

            Assert.That(pose.FacingDegrees, Is.EqualTo(270f));
        }

        [Test]
        public void ExplorationPoseCannotChangeDuringTurnMode()
        {
            GameplaySession session = CreateSession(CreateActor("player", 10));
            var explorationPose = new GameplayActorPose(
                new GameplayPosition(2f, 0f, 4f),
                90f);
            session.UpdateExplorationPose("player", explorationPose);
            session.EnterTurnMode();

            Assert.That(
                session.GetActor("player").Pose.Position.X,
                Is.EqualTo(2f));
            Assert.Throws<InvalidOperationException>(() =>
                session.UpdateExplorationPose(
                    "player",
                    new GameplayActorPose(
                        new GameplayPosition(3f, 0f, 4f),
                        90f)));
        }

        [Test]
        public void StanceIsAuthoritativeAndPreservedByMovementResolution()
        {
            GameplaySession session = CreateSession(CreateActor("player", 10));
            session.EnterTurnMode();

            var resolver = new StanceChangeResolver(session, new AllowStanceChanges());
            Assert.That(resolver.TryResolve(
                "player",
                ActorStance.Crouched,
                out StanceChangeRecord stanceChange,
                out StanceChangeFailure failure,
                out string failureCode), Is.True);
            Assert.That(failure, Is.EqualTo(StanceChangeFailure.None));
            Assert.That(failureCode, Is.Empty);
            Assert.That(stanceChange.PreviousPose.Stance,
                Is.EqualTo(ActorStance.Standing));
            Assert.That(stanceChange.ResultingPose.Stance,
                Is.EqualTo(ActorStance.Crouched));
            GameplaySession replay = CreateSession(CreateActor("player", 10));
            replay.EnterTurnMode();
            replay.CommitStanceChange(stanceChange);
            Assert.That(
                replay.GetActor("player").Pose.Stance,
                Is.EqualTo(ActorStance.Crouched));
            GameplayActorSnapshot crouched = session.GetActor("player");
            Assert.That(crouched.Pose.Stance, Is.EqualTo(ActorStance.Crouched));
            var route = new MovementRouteRecord(
                "player",
                crouched.Pose,
                new[] { new GameplayPosition(1f, 0f, 0f) });

            session.CommitMovementRoute(route);
            session.CompleteMovementResolution();

            GameplayActorSnapshot resolved = session.GetActor("player");
            Assert.That(resolved.Pose.Position.X, Is.EqualTo(1f));
            Assert.That(resolved.Pose.Stance, Is.EqualTo(ActorStance.Crouched));
        }

        [Test]
        public void TurnModeRejectsStanceChangesForInactiveActors()
        {
            GameplaySession session = CreateSession(
                CreateActor("player", 10),
                CreateActor("target", 0));
            session.EnterTurnMode();

            var resolver = new StanceChangeResolver(session, new AllowStanceChanges());
            Assert.That(resolver.TryResolve(
                "target",
                ActorStance.Crouched,
                out StanceChangeRecord record,
                out StanceChangeFailure failure,
                out string failureCode), Is.False);
            Assert.That(record, Is.Null);
            Assert.That(failure, Is.EqualTo(StanceChangeFailure.ActorNotActive));
            Assert.That(failureCode, Is.Empty);
            Assert.That(
                session.GetActor("target").Pose.Stance,
                Is.EqualTo(ActorStance.Standing));
        }

        [Test]
        public void BlockedStanceChangeProducesNoAuthoritativeMutation()
        {
            GameplaySession session = CreateSession(CreateActor("player", 10));
            var resolver = new StanceChangeResolver(
                session,
                new BlockStanceChanges("stance.overhead-blocked"));

            Assert.That(resolver.TryResolve(
                "player",
                ActorStance.Crouched,
                out StanceChangeRecord record,
                out StanceChangeFailure failure,
                out string failureCode), Is.False);

            Assert.That(record, Is.Null);
            Assert.That(failure, Is.EqualTo(StanceChangeFailure.SpatiallyBlocked));
            Assert.That(failureCode, Is.EqualTo("stance.overhead-blocked"));
            Assert.That(
                session.GetActor("player").Pose.Stance,
                Is.EqualTo(ActorStance.Standing));
        }

        private static GameplaySession CreateSession(
            params ScenarioActorDefinition[] actors)
        {
            var objective = new ScenarioObjectiveDefinition(
                "raised-deck",
                new GameplayPosition(0f, 0f, 0f),
                interactionRadius: 1f,
                new GameplayInteractionDefinition(
                    "raised-deck.secure",
                    "Secure raised deck",
                    new ActionCost(1, 1f, ActionMobility.Set)));
            var scenario = new ScenarioDefinition(
                "depot-training",
                new ScenarioTimingDefinition(1.25f),
                actors,
                new[] { objective });
            return new GameplaySession(scenario, scenarioSeed: 42u);
        }

        private static ScenarioActorDefinition CreateActor(
            string id,
            int initiative)
        {
            return new ScenarioActorDefinition(
                id,
                initiative,
                new GameplayActorPose(new GameplayPosition(0f, 0f, 0f), 0f),
                new TurnBudget(4, 8f));
        }

        private static GameplayActionRecord ResolveInteraction(
            GameplaySession session,
            string actorId,
            string targetId)
        {
            var resolver = new GameplayActionResolver(session);
            Assert.That(resolver.TryResolveInteraction(
                actorId,
                targetId,
                out GameplayActionRecord record,
                out GameplayActionFailure failure), Is.True);
            Assert.That(failure, Is.EqualTo(GameplayActionFailure.None));
            return record;
        }

        private sealed class AllowStanceChanges : IStanceTransitionValidator
        {
            public StanceTransitionValidation Validate(
                GameplayActorSnapshot actor,
                ActorStance requestedStance)
            {
                return StanceTransitionValidation.Allowed();
            }
        }

        private sealed class BlockStanceChanges : IStanceTransitionValidator
        {
            private readonly string failureCode;

            public BlockStanceChanges(string failureCode)
            {
                this.failureCode = failureCode;
            }

            public StanceTransitionValidation Validate(
                GameplayActorSnapshot actor,
                ActorStance requestedStance)
            {
                return StanceTransitionValidation.Blocked(failureCode);
            }
        }
    }
}
