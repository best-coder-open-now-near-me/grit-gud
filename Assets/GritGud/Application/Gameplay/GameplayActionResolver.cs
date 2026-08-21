using System;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;

namespace GritGud.Application.Gameplay
{
    public enum GameplayActionFailure
    {
        None,
        ActorNotActive,
        OperationInProgress,
        TargetNotFound,
        TargetAlreadyCompleted,
        TargetOutOfRange,
        InsufficientActionPoints,
        InsufficientMovementOpportunity,
        ActorPinned,
    }

    public sealed class GameplayActionResolver
    {
        private static readonly ActionCost ExplorationCost =
            new ActionCost(0, 0f, ActionMobility.Mobile);

        private readonly GameplaySession session;

        public GameplayActionResolver(GameplaySession session)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
        }

        public GameplayActionFailure EvaluateInteraction(
            string actorId,
            string targetId)
        {
            TryPrepareInteraction(
                actorId,
                targetId,
                out _,
                out GameplayActionFailure failure);
            return failure;
        }

        public bool TryResolveInteraction(
            string actorId,
            string targetId,
            out GameplayActionRecord record,
            out GameplayActionFailure failure)
        {
            if (!TryPrepareInteraction(actorId, targetId, out record, out failure))
            {
                return false;
            }

            session.CommitAction(record);
            return true;
        }

        private bool TryPrepareInteraction(
            string actorId,
            string targetId,
            out GameplayActionRecord record,
            out GameplayActionFailure failure)
        {
            record = null;
            if (session.Operation != GameplaySessionOperation.None)
            {
                failure = GameplayActionFailure.OperationInProgress;
                return false;
            }

            if (session.Mode == GameplaySessionMode.TurnBased
                && !string.Equals(
                    session.ActiveActorId,
                    actorId,
                    StringComparison.Ordinal))
            {
                failure = GameplayActionFailure.ActorNotActive;
                return false;
            }

            if (!session.TryGetActor(actorId, out GameplayActorSnapshot actor))
            {
                failure = GameplayActionFailure.ActorNotActive;
                return false;
            }
            if (actor.IsPinned)
            {
                failure = GameplayActionFailure.ActorPinned;
                return false;
            }

            if (!session.TryGetObjective(
                    targetId,
                    out GameplayObjectiveSnapshot objective))
            {
                failure = GameplayActionFailure.TargetNotFound;
                return false;
            }

            if (objective.IsCompleted)
            {
                failure = GameplayActionFailure.TargetAlreadyCompleted;
                return false;
            }

            if (actor.Pose.Position.DistanceTo(objective.Position) >
                objective.InteractionRadius)
            {
                failure = GameplayActionFailure.TargetOutOfRange;
                return false;
            }

            ActionCost cost = session.Mode == GameplaySessionMode.TurnBased
                ? objective.Interaction.TurnCost
                : ExplorationCost;
            if (actor.TurnBudget.ActionPoints < cost.ActionPoints)
            {
                failure = GameplayActionFailure.InsufficientActionPoints;
                return false;
            }

            if (actor.TurnBudget.MovementOpportunity < cost.MovementOpportunity)
            {
                failure = GameplayActionFailure.InsufficientMovementOpportunity;
                return false;
            }

            TurnBudget resultingBudget = actor.TurnBudget.SpendAction(cost);
            long sequence = session.LastResolvedAction == null
                ? 1
                : session.LastResolvedAction.Sequence + 1;
            record = new GameplayActionRecord(
                sequence,
                new GameplayActionRequest(
                    actorId,
                    objective.Interaction.Id,
                    objective.ObjectiveId),
                cost,
                actor.TurnBudget,
                resultingBudget,
                new[]
                {
                    new ObjectiveCompletedActionOutcome(objective.ObjectiveId),
                });
            failure = GameplayActionFailure.None;
            return true;
        }
    }
}
