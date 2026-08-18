using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;

namespace GritGud.Application.Gameplay
{
    public sealed class GameplayResolvedActionTransitionPayload :
        GameplayTransitionPayload
    {
        public GameplayResolvedActionTransitionPayload(
            GameplayCapabilityProfile profile,
            GameplayActionRecord action,
            EquipmentEffectSet? resultingEquipmentEffects = null)
            : base(
                profile,
                (action ?? throw new ArgumentNullException(nameof(action)))
                    .Request.ActorId,
                action.Request.TargetId)
        {
            Action = action;
            ResultingEquipmentEffects = resultingEquipmentEffects;
        }

        public GameplayActionRecord Action { get; }
        public EquipmentEffectSet? ResultingEquipmentEffects { get; }
    }

    public sealed class GameplayResolvedActionTransitionReducer :
        IGameplaySemanticTransitionReducer
    {
        public bool Supports(GameplayCapabilityProfile profile) =>
            profile != null
            && (profile.Equals(GameplayCapabilityProfiles.Equip())
                || profile.Equals(GameplayCapabilityProfiles.Interact()));

        public GameplayReductionResult Reduce(
            GameplayCombatStateSnapshot state,
            GameplaySemanticTransition transition)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (transition == null)
                throw new ArgumentNullException(nameof(transition));
            if (!(transition.Payload
                is GameplayResolvedActionTransitionPayload payload)
                || !Supports(payload.Profile))
                throw new ArgumentException(
                    "Resolved action payload does not match this reducer.",
                    nameof(transition));

            GameplayActionRecord action = payload.Action;
            GameplaySessionStateSnapshot session = state.Session;
            if (action.Sequence != session.LastActionSequence + 1L)
                throw new InvalidOperationException(
                    "Resolved action is not the next action sequence.");
            if (!string.Equals(
                session.ActiveActorId,
                action.Request.ActorId,
                StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Resolved action actor is not active.");
            GameplayActorSnapshot acting = session.GetActor(
                action.Request.ActorId);
            if (!BudgetsMatch(acting.TurnBudget, action.PreviousBudget)
                || !BudgetsMatch(
                    action.PreviousBudget.SpendAction(action.Cost),
                    action.ResultingBudget))
                throw new InvalidOperationException(
                    "Resolved action budget does not match canonical state.");

            var mutation = new GameplayCanonicalStateMutation(state);
            mutation.ReplaceActor(GameplayCanonicalStateMutation.CopyActor(
                acting,
                budget: action.ResultingBudget));
            switch (payload.Profile.Capability)
            {
                case GameplaySemanticCapability.Equip:
                    ApplyEquipment(mutation, payload);
                    break;
                case GameplaySemanticCapability.Interact:
                    ApplyInteraction(mutation, payload);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(transition));
            }

            mutation.LastActionSequence = action.Sequence;
            mutation.JournalSequence = checked(mutation.JournalSequence + 1L);
            mutation.Revision = checked(mutation.Revision + 1L);
            mutation.LastTransitionSequence = transition.Identity.Sequence;
            GameplayCombatStateSnapshot resulting = mutation.Build();
            return new GameplayReductionResult(
                state,
                resulting,
                CreateEvents(transition, action));
        }

        private static void ApplyEquipment(
            GameplayCanonicalStateMutation mutation,
            GameplayResolvedActionTransitionPayload payload)
        {
            if (payload.Action.Outcomes.Count != 1
                || !(payload.Action.Outcomes[0]
                    is EquipmentChangedActionOutcome equipment))
                throw new ArgumentException(
                    "Equip transitions require one equipment outcome.",
                    nameof(payload));
            EquipmentChangeRecord change = equipment.Change;
            GameplayActorSnapshot actor = mutation.GetActor(change.ActorId);
            if (!string.Equals(
                actor.EquippedItemId,
                change.PreviousEquippedItemId,
                StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Equipment change no longer starts from canonical state.");
            EquipmentEffectSet effects =
                change.ResultingEquippedItemId == null
                    ? EquipmentEffectSet.None
                    : payload.ResultingEquipmentEffects
                        ?? throw new ArgumentException(
                            "Equipping an item requires its resulting effects.",
                            nameof(payload));
            mutation.ReplaceActor(GameplayCanonicalStateMutation.CopyActor(
                actor,
                equippedItemId: change.ResultingEquippedItemId,
                equipmentEffects: effects,
                replaceEquipment: true));
        }

        private static void ApplyInteraction(
            GameplayCanonicalStateMutation mutation,
            GameplayResolvedActionTransitionPayload payload)
        {
            if (payload.Action.Outcomes.Count != 1
                || !(payload.Action.Outcomes[0]
                    is ObjectiveCompletedActionOutcome completed))
                throw new ArgumentException(
                    "Interact transitions require one objective outcome.",
                    nameof(payload));
            GameplayObjectiveSnapshot objective = mutation.GetObjective(
                completed.ObjectiveId);
            if (objective.IsCompleted)
                throw new InvalidOperationException(
                    "Objective is already complete.");
            mutation.ReplaceObjective(new GameplayObjectiveSnapshot(
                objective.ObjectiveId,
                objective.Position,
                objective.InteractionRadius,
                objective.Interaction,
                isCompleted: true));
        }

        private static IReadOnlyList<GameplayDomainEvent> CreateEvents(
            GameplaySemanticTransition transition,
            GameplayActionRecord action) => Array.AsReadOnly(
                new GameplayDomainEvent[]
                {
                    new GameplayTransitionReducedEvent(
                        transition.Identity,
                        transition.Payload.SubjectId,
                        action),
                });

        private static bool BudgetsMatch(TurnBudget left, TurnBudget right) =>
            left.ActionPoints == right.ActionPoints
            && left.MovementOpportunity == right.MovementOpportunity;
    }
}
