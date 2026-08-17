using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;

namespace GritGud.Application.Gameplay
{
    internal sealed class DisplacementActionEvaluator
    {
        private readonly GameplaySession gameplay;
        private readonly DestructiblePropSession destructibles;
        private readonly IReadOnlyDictionary<string, DisplacementSubjectDefinition>
            subjects;
        private readonly IReadOnlyDictionary<string, CloseQuartersControlProfile>
            controlProfiles;

        public DisplacementActionEvaluator(
            GameplaySession gameplaySession,
            DestructiblePropSession destructibleSession,
            IReadOnlyDictionary<string, DisplacementSubjectDefinition>
                subjectDefinitions,
            IReadOnlyDictionary<string, CloseQuartersControlProfile>
                authoredControlProfiles)
        {
            gameplay = gameplaySession ??
                throw new ArgumentNullException(nameof(gameplaySession));
            destructibles = destructibleSession ??
                throw new ArgumentNullException(nameof(destructibleSession));
            subjects = subjectDefinitions ??
                throw new ArgumentNullException(nameof(subjectDefinitions));
            controlProfiles = authoredControlProfiles ??
                throw new ArgumentNullException(nameof(authoredControlProfiles));
        }

        public CloseQuartersControlProfile GetControlProfile(string actorId)
        {
            RequireId(actorId, nameof(actorId));
            if (!controlProfiles.TryGetValue(
                    actorId,
                    out CloseQuartersControlProfile profile))
            {
                throw new KeyNotFoundException(
                    $"Actor '{actorId}' has no close-quarters control profile.");
            }

            return profile;
        }

        public DisplacementActionAvailability EvaluateAvailability(
            string actorId,
            string actionId,
            bool startsEncounter)
        {
            RequireId(actorId, nameof(actorId));
            RequireId(actionId, nameof(actionId));

            if (!gameplay.TryGetActor(actorId, out GameplayActorSnapshot actor))
            {
                return CreateAvailability(
                    actorId,
                    actionId,
                    DisplacementActionAvailabilityFailure.ActorUnavailable);
            }

            if (!gameplay.TryGetDisplacementAction(
                    actorId,
                    actionId,
                    out DisplacementActionDefinition action))
            {
                return CreateAvailability(
                    actorId,
                    actionId,
                    DisplacementActionAvailabilityFailure.ActionUnavailable);
            }

            ActionCost cost = ResolveActionCost(action, startsEncounter);
            if (actor.IsPinned
                && action.Intent != DisplacementActionKind.PushOff)
            {
                return CreateAvailability(
                    actorId,
                    actionId,
                    DisplacementActionAvailabilityFailure.ActorPinned,
                    action,
                    cost);
            }
            if (!actor.IsPinned
                && action.Intent == DisplacementActionKind.PushOff)
            {
                return CreateAvailability(
                    actorId,
                    actionId,
                    DisplacementActionAvailabilityFailure.ActorNotPinned,
                    action,
                    cost);
            }

            InventoryItemDefinition equipped = gameplay.GetEquippedItem(actorId);
            string autoStowItemId = null;
            if (!action.HasRequiredFreeHands(equipped?.OccupiedHands ?? 0))
            {
                if (action.AutoStowPolicy == DisplacementAutoStowPolicy.Never)
                {
                    return CreateAvailability(
                        actorId,
                        actionId,
                        DisplacementActionAvailabilityFailure.HandsOccupied,
                        action,
                        cost);
                }

                autoStowItemId = equipped.Id;
                cost = ActionCost.Combine(
                    cost,
                    ResolveEquipmentCost(equipped, startsEncounter));
            }

            if (gameplay.Operation != GameplaySessionOperation.None)
            {
                return CreateAvailability(
                    actorId,
                    actionId,
                    DisplacementActionAvailabilityFailure.OperationInProgress,
                    action,
                    cost,
                    autoStowItemId);
            }

            if (gameplay.Mode == GameplaySessionMode.TurnBased
                && !string.Equals(
                    gameplay.ActiveActorId,
                    actorId,
                    StringComparison.Ordinal))
            {
                return CreateAvailability(
                    actorId,
                    actionId,
                    DisplacementActionAvailabilityFailure.ActorNotActive,
                    action,
                    cost,
                    autoStowItemId);
            }

            if (!actor.TurnBudget.CanAfford(cost))
            {
                return CreateAvailability(
                    actorId,
                    actionId,
                    DisplacementActionAvailabilityFailure.InsufficientTurnBudget,
                    action,
                    cost,
                    autoStowItemId);
            }

            return CreateAvailability(
                actorId,
                actionId,
                DisplacementActionAvailabilityFailure.None,
                action,
                cost,
                autoStowItemId);
        }

        public DisplacementTargetEvaluation EvaluateTarget(
            string actorId,
            string actionId,
            string candidateId)
        {
            RequireId(actorId, nameof(actorId));
            RequireId(actionId, nameof(actionId));
            RequireId(candidateId, nameof(candidateId));

            if (!gameplay.TryGetActor(actorId, out GameplayActorSnapshot actor))
            {
                return CreateTarget(
                    actorId,
                    actionId,
                    candidateId,
                    DisplacementTargetFailure.ActorUnavailable);
            }

            if (!gameplay.TryGetDisplacementAction(
                    actorId,
                    actionId,
                    out DisplacementActionDefinition action))
            {
                return CreateTarget(
                    actorId,
                    actionId,
                    candidateId,
                    DisplacementTargetFailure.ActionUnavailable);
            }

            if (!subjects.TryGetValue(
                    candidateId,
                    out DisplacementSubjectDefinition subject)
                || !TryGetSubjectPosition(subject, out GameplayPosition position))
            {
                return CreateTarget(
                    actorId,
                    actionId,
                    candidateId,
                    DisplacementTargetFailure.CandidateUnavailable,
                    action: action);
            }

            float distance = actor.Pose.Position.DistanceTo(position);
            if (string.Equals(actorId, candidateId, StringComparison.Ordinal))
            {
                return CreateTarget(
                    actorId,
                    actionId,
                    candidateId,
                    DisplacementTargetFailure.SelfTarget,
                    subject,
                    distance,
                    action);
            }

            if (action.Intent == DisplacementActionKind.PushOff
                && (!actor.IsPinned
                    || !string.Equals(
                        actor.PinState.PropId,
                        candidateId,
                        StringComparison.Ordinal)))
            {
                return CreateTarget(
                    actorId,
                    actionId,
                    candidateId,
                    DisplacementTargetFailure.NotPinningActor,
                    subject,
                    distance,
                    action);
            }

            if (subject.Kind == DisplacementSubjectKind.Combatant
                && gameplay.GetActor(candidateId).IsPinned)
            {
                return CreateTarget(
                    actorId,
                    actionId,
                    candidateId,
                    DisplacementTargetFailure.SubjectPinned,
                    subject,
                    distance,
                    action);
            }

            if (subject.Kind == DisplacementSubjectKind.Prop
                && action.Intent != DisplacementActionKind.PushOff
                && IsPinningProp(candidateId))
            {
                return CreateTarget(
                    actorId,
                    actionId,
                    candidateId,
                    DisplacementTargetFailure.SubjectPinned,
                    subject,
                    distance,
                    action);
            }

            if (!action.Accepts(subject.Kind))
            {
                return CreateTarget(
                    actorId,
                    actionId,
                    candidateId,
                    DisplacementTargetFailure.SubjectKindNotAccepted,
                    subject,
                    distance,
                    action);
            }

            if (subject.Mass > action.MaximumSubjectMass)
            {
                return CreateTarget(
                    actorId,
                    actionId,
                    candidateId,
                    DisplacementTargetFailure.SubjectTooHeavy,
                    subject,
                    distance,
                    action);
            }

            if (subject.Size > action.MaximumSubjectSize)
            {
                return CreateTarget(
                    actorId,
                    actionId,
                    candidateId,
                    DisplacementTargetFailure.SubjectTooLarge,
                    subject,
                    distance,
                    action);
            }

            if (subject.Kind == DisplacementSubjectKind.Combatant
                && (!controlProfiles.ContainsKey(actorId)
                    || !controlProfiles.ContainsKey(candidateId)))
            {
                return CreateTarget(
                    actorId,
                    actionId,
                    candidateId,
                    DisplacementTargetFailure.CandidateUnavailable,
                    subject,
                    distance,
                    action);
            }

            if (distance > action.Reach)
            {
                return CreateTarget(
                    actorId,
                    actionId,
                    candidateId,
                    DisplacementTargetFailure.SubjectOutOfReach,
                    subject,
                    distance,
                    action);
            }

            return CreateTarget(
                actorId,
                actionId,
                candidateId,
                DisplacementTargetFailure.None,
                subject,
                distance,
                action);
        }

        public bool TryGetSubjectPosition(
            DisplacementSubjectDefinition subject,
            out GameplayPosition position)
        {
            if (subject.Kind == DisplacementSubjectKind.Prop)
            {
                if (destructibles.TryGetProp(
                        subject.Id,
                        out DestructiblePropSnapshot prop))
                {
                    position = prop.Position;
                    return true;
                }
            }
            else if (gameplay.TryGetActor(
                subject.Id,
                out GameplayActorSnapshot actor))
            {
                position = actor.Pose.Position;
                return true;
            }

            position = default(GameplayPosition);
            return false;
        }

        public DisplacementSizeClass GetSubjectSize(string subjectId) =>
            subjects.TryGetValue(
                subjectId,
                out DisplacementSubjectDefinition subject)
                    ? subject.Size
                    : DisplacementSizeClass.Medium;

        private bool IsPinningProp(string propId)
        {
            foreach (ScenarioActorDefinition definition in
                gameplay.Scenario.Actors)
            {
                ActorPinState pin = gameplay.GetActor(definition.Id).PinState;
                if (pin != null
                    && string.Equals(
                        pin.PropId,
                        propId,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        private ActionCost ResolveActionCost(
            DisplacementActionDefinition definition,
            bool startsEncounter) =>
            gameplay.Mode == GameplaySessionMode.TurnBased
                || startsEncounter
                ? definition.Cost
                : new ActionCost(0, 0f, definition.Cost.Mobility);

        private ActionCost ResolveEquipmentCost(
            InventoryItemDefinition item,
            bool startsEncounter) =>
            gameplay.Mode == GameplaySessionMode.TurnBased
                || startsEncounter
                ? item.EquipmentCost
                : new ActionCost(0, 0f, item.EquipmentCost.Mobility);

        private static DisplacementActionAvailability CreateAvailability(
            string actorId,
            string actionId,
            DisplacementActionAvailabilityFailure failure,
            DisplacementActionDefinition action = null,
            ActionCost resolvedCost = default(ActionCost),
            string autoStowItemId = null) =>
            new DisplacementActionAvailability(
                actorId,
                actionId,
                failure,
                action,
                resolvedCost,
                autoStowItemId);

        private static DisplacementTargetEvaluation CreateTarget(
            string actorId,
            string actionId,
            string candidateId,
            DisplacementTargetFailure failure,
            DisplacementSubjectDefinition subject = null,
            float distance = 0f,
            DisplacementActionDefinition action = null) =>
            new DisplacementTargetEvaluation(
                actorId,
                actionId,
                candidateId,
                failure,
                subject,
                distance,
                action);

        private static void RequireId(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    "Displacement identifiers cannot be empty.",
                    parameterName);
            }
        }
    }
}
