using System;
using System.Collections.Generic;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;
using NUnit.Framework;

namespace GritGud.Domain.Tests.Gameplay
{
    public sealed class MovementRoutePlannerTests
    {
        [Test]
        public void ConfirmCreatesImmutableRecordWithCostDestinationAndFacing()
        {
            GameplayActorSnapshot actor = CreateActorSnapshot(8f);
            var planner = new MovementRoutePlanner(actor, new AllowAllValidator());
            Assert.That(
                planner.TryAppend(
                    new GameplayPosition(0f, 0f, 3f),
                    out RoutePlanFailure firstFailure),
                Is.True);
            Assert.That(firstFailure, Is.EqualTo(RoutePlanFailure.None));
            Assert.That(
                planner.TryAppend(
                    new GameplayPosition(4f, 0f, 3f),
                    out RoutePlanFailure secondFailure),
                Is.True);
            Assert.That(secondFailure, Is.EqualTo(RoutePlanFailure.None));

            MovementRouteRecord route = planner.Confirm();

            Assert.That(route.Points.Count, Is.EqualTo(3));
            Assert.That(route.TotalCost, Is.EqualTo(7f));
            Assert.That(route.Destination.X, Is.EqualTo(4f));
            Assert.That(route.Destination.Z, Is.EqualTo(3f));
            Assert.That(route.FinalFacingDegrees, Is.EqualTo(90f));

            planner.Cancel();
            Assert.That(route.Points.Count, Is.EqualTo(3));
            Assert.That(route.TotalCost, Is.EqualTo(7f));
        }

        [Test]
        public void AppendBeyondBudgetDoesNotMutatePlan()
        {
            var planner = new MovementRoutePlanner(
                CreateActorSnapshot(5f),
                new AllowAllValidator());
            planner.TryAppend(
                new GameplayPosition(0f, 0f, 3f),
                out RoutePlanFailure ignoredFailure);

            bool appended = planner.TryAppend(
                new GameplayPosition(4f, 0f, 3f),
                out RoutePlanFailure failure);

            Assert.That(appended, Is.False);
            Assert.That(failure, Is.EqualTo(RoutePlanFailure.ExceedsMovementBudget));
            Assert.That(planner.Points.Count, Is.EqualTo(2));
            Assert.That(planner.TotalCost, Is.EqualTo(3f));
            Assert.That(ignoredFailure, Is.EqualTo(RoutePlanFailure.None));
        }

        [Test]
        public void RejectedSegmentDoesNotMutatePlanAndPreservesReason()
        {
            var planner = new MovementRoutePlanner(
                CreateActorSnapshot(8f),
                new RejectAllValidator());

            bool appended = planner.TryAppend(
                new GameplayPosition(0f, 0f, 2f),
                out RoutePlanFailure failure);

            Assert.That(appended, Is.False);
            Assert.That(failure, Is.EqualTo(RoutePlanFailure.SegmentBlocked));
            Assert.That(planner.Points.Count, Is.EqualTo(1));
            Assert.That(planner.LastFailureReason, Is.EqualTo("blocked for test"));
        }

        [Test]
        public void UndoAndCancelReviseOnlyTheProvisionalPlan()
        {
            var planner = new MovementRoutePlanner(
                CreateActorSnapshot(8f),
                new AllowAllValidator());
            planner.TryAppend(
                new GameplayPosition(0f, 0f, 2f),
                out RoutePlanFailure firstFailure);
            planner.TryAppend(
                new GameplayPosition(3f, 0f, 2f),
                out RoutePlanFailure secondFailure);

            Assert.That(planner.UndoLastSegment(), Is.True);
            Assert.That(planner.TotalCost, Is.EqualTo(2f));
            Assert.That(planner.Destination.Z, Is.EqualTo(2f));

            planner.Cancel();

            Assert.That(planner.CanConfirm, Is.False);
            Assert.That(planner.TotalCost, Is.EqualTo(0f));
            Assert.That(planner.Destination.Z, Is.EqualTo(0f));
            Assert.That(firstFailure, Is.EqualTo(RoutePlanFailure.None));
            Assert.That(secondFailure, Is.EqualTo(RoutePlanFailure.None));
        }

        [Test]
        public void CommittedMovementSpendsOnlyMovementAndResolvesAtomically()
        {
            GameplaySession session = CreateSession();
            session.EnterTurnMode();
            GameplayActorSnapshot startingActor = session.GetActor("player");
            var planner = new MovementRoutePlanner(
                startingActor,
                new AllowAllValidator());
            planner.TryAppend(
                new GameplayPosition(0f, 0f, 3f),
                out RoutePlanFailure firstFailure);
            planner.TryAppend(
                new GameplayPosition(4f, 0f, 3f),
                out RoutePlanFailure secondFailure);
            MovementRouteRecord route = planner.Confirm();

            session.CommitMovementRoute(route);
            GameplayActorSnapshot resolvingActor = session.GetActor("player");

            Assert.That(
                session.Operation,
                Is.EqualTo(GameplaySessionOperation.ResolvingMovement));
            Assert.That(session.PendingMovementRoute, Is.SameAs(route));
            Assert.That(resolvingActor.TurnBudget.ActionPoints, Is.EqualTo(4));
            Assert.That(resolvingActor.TurnBudget.MovementOpportunity, Is.EqualTo(1f));
            Assert.That(resolvingActor.Pose.Position.X, Is.EqualTo(0f));
            Assert.That(
                session.TryExitTurnMode(out TurnModeExitFailure failure),
                Is.False);
            Assert.That(failure, Is.EqualTo(TurnModeExitFailure.OperationInProgress));

            session.CompleteMovementResolution();
            GameplayActorSnapshot completedActor = session.GetActor("player");

            Assert.That(session.Operation, Is.EqualTo(GameplaySessionOperation.None));
            Assert.That(session.PendingMovementRoute, Is.Null);
            Assert.That(completedActor.Pose.Position.X, Is.EqualTo(4f));
            Assert.That(completedActor.Pose.Position.Z, Is.EqualTo(3f));
            Assert.That(completedActor.Pose.FacingDegrees, Is.EqualTo(90f));
            Assert.That(session.TryExitTurnMode(out failure), Is.True);
            Assert.That(firstFailure, Is.EqualTo(RoutePlanFailure.None));
            Assert.That(secondFailure, Is.EqualTo(RoutePlanFailure.None));
        }

        [Test]
        public void StaleRouteCannotSpendBudgetOrBeginResolution()
        {
            GameplaySession session = CreateSession();
            var planner = new MovementRoutePlanner(
                session.GetActor("player"),
                new AllowAllValidator());
            planner.TryAppend(
                new GameplayPosition(0f, 0f, 2f),
                out RoutePlanFailure failure);
            MovementRouteRecord route = planner.Confirm();
            session.UpdateExplorationPose(
                "player",
                new GameplayActorPose(new GameplayPosition(1f, 0f, 0f), 0f));
            session.EnterTurnMode();

            Assert.Throws<InvalidOperationException>(() =>
                session.CommitMovementRoute(route));
            Assert.That(
                session.GetActor("player").TurnBudget.MovementOpportunity,
                Is.EqualTo(8f));
            Assert.That(session.Operation, Is.EqualTo(GameplaySessionOperation.None));
            Assert.That(failure, Is.EqualTo(RoutePlanFailure.None));
        }

        [Test]
        public void ValidatorCanResolveRequestedPositionBeforeCosting()
        {
            var planner = new MovementRoutePlanner(
                CreateActorSnapshot(8f),
                new FlattenHeightValidator());

            planner.TryAppend(
                new GameplayPosition(0f, 5f, 3f),
                out RoutePlanFailure failure);

            Assert.That(planner.Destination.Y, Is.EqualTo(0f));
            Assert.That(planner.TotalCost, Is.EqualTo(3f));
            Assert.That(failure, Is.EqualTo(RoutePlanFailure.None));
        }

        [Test]
        public void TraversalSegmentFreezesIdentityCostsArcAndPlayback()
        {
            var planner = new MovementRoutePlanner(
                CreateActorSnapshot(8f),
                new TraversalValidator());

            Assert.That(
                planner.TryAppend(
                    new GameplayPosition(0f, 0f, 0.25f),
                    out RoutePlanFailure failure),
                Is.True);
            MovementRouteRecord route = planner.Confirm();
            MovementRouteSegmentRecord segment = route.Segments[0];

            Assert.That(failure, Is.EqualTo(RoutePlanFailure.None));
            Assert.That(segment.Kind, Is.EqualTo(MovementRouteSegmentKind.Jump));
            Assert.That(segment.TraversalLinkId, Is.EqualTo("jump.demo"));
            Assert.That(segment.ActionId, Is.EqualTo("traversal.jump"));
            Assert.That(route.TotalCost, Is.EqualTo(2.5f));
            Assert.That(route.TotalActionPointCost, Is.EqualTo(1));
            Assert.That(route.TotalPlaybackDurationSeconds, Is.EqualTo(0.8f));
            Assert.That(route.PreviousBudget.ActionPoints, Is.EqualTo(4));
            Assert.That(route.HasFrozenBudget, Is.True);
            Assert.That(route.HasTraversal, Is.True);
            Assert.That(segment.Sample(0.5f).Y, Is.EqualTo(1.25f));
        }

        [Test]
        public void TraversalActionPointCostCannotExceedBudget()
        {
            GameplayActorSnapshot actor = new GameplayActorSnapshot(
                "player",
                new GameplayActorPose(
                    new GameplayPosition(0f, 0f, 0f),
                    0f),
                new TurnBudget(0, 8f));
            var planner = new MovementRoutePlanner(
                actor,
                new TraversalValidator());

            Assert.That(
                planner.TryAppend(
                    new GameplayPosition(0f, 0f, 0.25f),
                    out RoutePlanFailure failure),
                Is.False);
            Assert.That(
                failure,
                Is.EqualTo(RoutePlanFailure.ExceedsActionPointBudget));
            Assert.That(planner.CanConfirm, Is.False);
        }

        [Test]
        public void BudgetChangeMakesFrozenRouteStaleBeforeCommit()
        {
            GameplaySession session = CreateSession();
            session.EnterTurnMode();
            var planner = new MovementRoutePlanner(
                session.GetActor("player"),
                new AllowAllValidator());
            planner.TryAppend(
                new GameplayPosition(0f, 0f, 2f),
                out RoutePlanFailure failure);
            MovementRouteRecord route = planner.Confirm();
            session.SpendMovement("player", 1f);

            Assert.Throws<InvalidOperationException>(() =>
                session.CommitMovementRoute(route));
            Assert.That(session.Operation, Is.EqualTo(GameplaySessionOperation.None));
            Assert.That(
                session.GetActor("player").TurnBudget.MovementOpportunity,
                Is.EqualTo(7f));
            Assert.That(failure, Is.EqualTo(RoutePlanFailure.None));
        }

        private static GameplayActorSnapshot CreateActorSnapshot(float movement)
        {
            return new GameplayActorSnapshot(
                "player",
                new GameplayActorPose(new GameplayPosition(0f, 0f, 0f), 0f),
                new TurnBudget(4, movement));
        }

        private static GameplaySession CreateSession()
        {
            var actor = new ScenarioActorDefinition(
                "player",
                10,
                new GameplayActorPose(new GameplayPosition(0f, 0f, 0f), 0f),
                new TurnBudget(4, 8f));
            return new GameplaySession(
                new ScenarioDefinition(
                    "depot-training",
                    new ScenarioTimingDefinition(1.25f),
                    new[] { actor },
                    Array.Empty<ScenarioObjectiveDefinition>()));
        }

        private sealed class AllowAllValidator : IMovementRouteSegmentValidator
        {
            public MovementRouteSegmentValidation Validate(
                string actorId,
                GameplayPosition from,
                GameplayPosition requestedDestination)
            {
                return MovementRouteSegmentValidation.Accepted(
                    requestedDestination);
            }
        }

        private sealed class RejectAllValidator : IMovementRouteSegmentValidator
        {
            public MovementRouteSegmentValidation Validate(
                string actorId,
                GameplayPosition from,
                GameplayPosition requestedDestination)
            {
                return MovementRouteSegmentValidation.Rejected("blocked for test");
            }
        }

        private sealed class FlattenHeightValidator : IMovementRouteSegmentValidator
        {
            public MovementRouteSegmentValidation Validate(
                string actorId,
                GameplayPosition from,
                GameplayPosition requestedDestination)
            {
                return MovementRouteSegmentValidation.Accepted(
                    new GameplayPosition(
                        requestedDestination.X,
                        0f,
                        requestedDestination.Z));
            }
        }

        private sealed class TraversalValidator : IMovementRouteSegmentValidator
        {
            public MovementRouteSegmentValidation Validate(
                string actorId,
                GameplayPosition from,
                GameplayPosition requestedDestination)
            {
                return MovementRouteSegmentValidation.Accepted(
                    new MovementRouteSegmentRecord(
                        from,
                        new GameplayPosition(0f, 0f, 2f),
                        MovementRouteSegmentKind.Jump,
                        "jump.demo",
                        "traversal.jump",
                        2.5f,
                        1,
                        1.25f,
                        0.8f));
            }
        }
    }
}
