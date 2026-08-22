using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;

namespace GritGud.Application.Gameplay
{
    public sealed class GameplayStanceCandidateExecutionRoute :
        IGameplayCandidateExecutionRoute
    {
        public const string Id = "stance.v1";

        public string RouteId => Id;

        public bool Supports(GameplayCapabilityProfile profile) =>
            profile != null
            && profile.Equals(GameplayCapabilityProfiles.ChangeStance());

        public GameplayExecutableCandidateEvaluation Evaluate(
            GameplayDecisionContext context,
            GameplayCandidate candidate)
        {
            Require(context, candidate, Supports, Id);
            GameplaySessionStateSnapshot session = context.State.Session;
            GameplayActorSnapshot actor = session.GetActor(candidate.ActorId);
            string failure = session.Operation != GameplaySessionOperation.None
                ? "operation-in-progress"
                : session.Mode == GameplaySessionMode.TurnBased
                    && !string.Equals(
                        session.ActiveActorId,
                        candidate.ActorId,
                        StringComparison.Ordinal)
                    ? "actor-not-active"
                    : actor.IsIncapacitated
                        ? "actor-incapacitated"
                        : actor.IsPinned
                            ? "actor-pinned"
                            : string.Empty;
            bool legal = failure.Length == 0;
            StanceChangeRecord stance = null;
            if (legal)
            {
                ActorStance resulting = actor.Pose.Stance
                    == ActorStance.Standing
                        ? ActorStance.Crouched
                        : ActorStance.Standing;
                if (resulting == ActorStance.Standing
                    && !actor.Capabilities.CanStand)
                {
                    legal = false;
                    failure = "standing-capability-impaired";
                }
                else
                {
                    stance = new StanceChangeRecord(
                        actor.ActorId,
                        actor.Pose,
                        new GameplayActorPose(
                            actor.Pose.Position,
                            actor.Pose.FacingDegrees,
                            resulting));
                }
            }
            return Result(
                Id,
                context,
                candidate,
                legal,
                failure,
                new GameplayCandidateOutcomeEstimate(new[]
                {
                    new GameplayCandidateOutcomeFeature(
                        "stance.crouched",
                        stance?.ResultingPose.Stance == ActorStance.Crouched
                            ? 1f
                            : 0f),
                }),
                stance);
        }

        public GameplayTransitionPayload PreparePayload(
            GameplayDecisionContext context,
            GameplayExecutableCandidateEvaluation evaluation) =>
            new GameplayStanceTransitionPayload(
                evaluation?.FrozenPreparation as StanceChangeRecord
                    ?? throw new ArgumentException(
                        "Stance preparation is missing.",
                        nameof(evaluation)));

        private static void Require(
            GameplayDecisionContext context,
            GameplayCandidate candidate,
            Func<GameplayCapabilityProfile, bool> supports,
            string routeId) => GameplayBasicCandidateRouteUtility.Require(
                context,
                candidate,
                supports,
                routeId);

        private static GameplayExecutableCandidateEvaluation Result(
            string routeId,
            GameplayDecisionContext context,
            GameplayCandidate candidate,
            bool legal,
            string failure,
            GameplayCandidateOutcomeEstimate outcome,
            object preparation) => GameplayBasicCandidateRouteUtility.Result(
                routeId,
                context,
                candidate,
                legal,
                failure,
                outcome,
                preparation);
    }

    public sealed class GameplayEquipmentCandidateExecutionRoute :
        IGameplayCandidateExecutionRoute
    {
        public const string Id = "equipment.v1";
        private static readonly ActionCost ExplorationCost = new ActionCost(
            0,
            0f,
            ActionMobility.Mobile);
        private readonly ScenarioDefinition scenario;

        public GameplayEquipmentCandidateExecutionRoute(
            ScenarioDefinition scenarioDefinition)
        {
            scenario = scenarioDefinition ?? throw new ArgumentNullException(
                nameof(scenarioDefinition));
        }

        public string RouteId => Id;

        public bool Supports(GameplayCapabilityProfile profile) =>
            profile != null
            && profile.Equals(GameplayCapabilityProfiles.Equip());

        public GameplayExecutableCandidateEvaluation Evaluate(
            GameplayDecisionContext context,
            GameplayCandidate candidate)
        {
            GameplayBasicCandidateRouteUtility.Require(
                context,
                candidate,
                Supports,
                Id);
            GameplaySessionStateSnapshot session = context.State.Session;
            GameplayActorSnapshot actor = session.GetActor(candidate.ActorId);
            InventoryItemDefinition item = scenario.GetActor(candidate.ActorId)
                .GetInventoryItem(candidate.SubjectId);
            bool isEquipped = string.Equals(
                actor.EquippedItemId,
                candidate.SubjectId,
                StringComparison.Ordinal);
            ActionCost cost = session.Mode == GameplaySessionMode.TurnBased
                ? item?.EquipmentCost
                    ?? new ActionCost(0, 0f, ActionMobility.Mobile)
                : ExplorationCost;
            string failure = session.Operation != GameplaySessionOperation.None
                ? "operation-in-progress"
                : session.Mode == GameplaySessionMode.TurnBased
                    && !string.Equals(
                        session.ActiveActorId,
                        candidate.ActorId,
                        StringComparison.Ordinal)
                    ? "actor-not-active"
                    : actor.IsIncapacitated
                        ? "actor-incapacitated"
                        : actor.IsPinned
                            ? "actor-pinned"
                            : item == null
                                ? "item-not-found"
                                : !item.IsEquippable
                                    ? "item-not-equippable"
                                    : !isEquipped
                                        && actor.EquippedItemId != null
                                        ? "must-unequip-current-item"
                                        : actor.TurnBudget.ActionPoints
                                            < cost.ActionPoints
                                            ? "insufficient-action-points"
                                            : actor.TurnBudget
                                                .MovementOpportunity
                                                < cost.MovementOpportunity
                                                ? "insufficient-movement-opportunity"
                                                : string.Empty;
            bool legal = failure.Length == 0;
            GameplayResolvedActionTransitionPayload payload = null;
            if (legal)
            {
                bool equip = !isEquipped;
                var change = new EquipmentChangeRecord(
                    actor.ActorId,
                    item.Id,
                    equip
                        ? EquipmentChangeKind.Equip
                        : EquipmentChangeKind.Unequip,
                    actor.EquippedItemId,
                    equip ? item.Id : null);
                var action = new GameplayActionRecord(
                    checked(session.LastActionSequence + 1L),
                    new GameplayActionRequest(
                        actor.ActorId,
                        equip
                            ? EquipmentActionIds.Equip
                            : EquipmentActionIds.Unequip,
                        item.Id),
                    cost,
                    actor.TurnBudget,
                    actor.TurnBudget.SpendAction(cost),
                    new[] { new EquipmentChangedActionOutcome(change) });
                payload = new GameplayResolvedActionTransitionPayload(
                    candidate.Profile,
                    action,
                    equip ? item.EquippedEffects : (EquipmentEffectSet?)null);
            }
            return GameplayBasicCandidateRouteUtility.Result(
                Id,
                context,
                candidate,
                legal,
                failure,
                new GameplayCandidateOutcomeEstimate(new[]
                {
                    new GameplayCandidateOutcomeFeature(
                        "equipment.equip",
                        isEquipped ? 0f : 1f),
                    new GameplayCandidateOutcomeFeature(
                        "equipment.unequip",
                        isEquipped ? 1f : 0f),
                    new GameplayCandidateOutcomeFeature(
                        "cost.action-points",
                        cost.ActionPoints),
                }),
                payload);
        }

        public GameplayTransitionPayload PreparePayload(
            GameplayDecisionContext context,
            GameplayExecutableCandidateEvaluation evaluation) =>
            evaluation?.FrozenPreparation
                as GameplayResolvedActionTransitionPayload
            ?? throw new ArgumentException(
                "Equipment preparation is missing.",
                nameof(evaluation));
    }

    public sealed class GameplayInteractionCandidateExecutionRoute :
        IGameplayCandidateExecutionRoute
    {
        public const string Id = "interaction.v1";
        private static readonly ActionCost ExplorationCost = new ActionCost(
            0,
            0f,
            ActionMobility.Mobile);

        public string RouteId => Id;

        public bool Supports(GameplayCapabilityProfile profile) =>
            profile != null
            && profile.Equals(GameplayCapabilityProfiles.Interact());

        public GameplayExecutableCandidateEvaluation Evaluate(
            GameplayDecisionContext context,
            GameplayCandidate candidate)
        {
            GameplayBasicCandidateRouteUtility.Require(
                context,
                candidate,
                Supports,
                Id);
            GameplaySessionStateSnapshot session = context.State.Session;
            GameplayActorSnapshot actor = session.GetActor(candidate.ActorId);
            bool hasObjective = TryFindObjective(
                session.Objectives,
                candidate.SubjectId,
                out GameplayObjectiveSnapshot objective);
            ActionCost cost = session.Mode == GameplaySessionMode.TurnBased
                ? hasObjective
                    ? objective.Interaction.TurnCost
                    : ExplorationCost
                : ExplorationCost;
            string failure = session.Operation != GameplaySessionOperation.None
                ? "operation-in-progress"
                : session.Mode == GameplaySessionMode.TurnBased
                    && !string.Equals(
                        session.ActiveActorId,
                        candidate.ActorId,
                        StringComparison.Ordinal)
                    ? "actor-not-active"
                    : actor.IsIncapacitated
                        ? "actor-incapacitated"
                        : actor.IsPinned
                            ? "actor-pinned"
                            : !hasObjective
                                ? "objective-not-found"
                                : objective.IsCompleted
                                    ? "objective-completed"
                                    : actor.Pose.Position.DistanceTo(
                                        objective.Position)
                                        > objective.InteractionRadius
                                        ? "objective-out-of-range"
                                        : actor.TurnBudget.ActionPoints
                                            < cost.ActionPoints
                                            ? "insufficient-action-points"
                                            : actor.TurnBudget
                                                .MovementOpportunity
                                                < cost.MovementOpportunity
                                                ? "insufficient-movement-opportunity"
                                                : string.Empty;
            bool legal = failure.Length == 0;
            GameplayResolvedActionTransitionPayload payload = null;
            if (legal)
            {
                var action = new GameplayActionRecord(
                    checked(session.LastActionSequence + 1L),
                    new GameplayActionRequest(
                        actor.ActorId,
                        objective.Interaction.Id,
                        objective.ObjectiveId),
                    cost,
                    actor.TurnBudget,
                    actor.TurnBudget.SpendAction(cost),
                    new[]
                    {
                        new ObjectiveCompletedActionOutcome(
                            objective.ObjectiveId),
                    });
                payload = new GameplayResolvedActionTransitionPayload(
                    candidate.Profile,
                    action);
            }
            return GameplayBasicCandidateRouteUtility.Result(
                Id,
                context,
                candidate,
                legal,
                failure,
                new GameplayCandidateOutcomeEstimate(new[]
                {
                    new GameplayCandidateOutcomeFeature(
                        "objective.complete",
                        legal ? 1f : 0f),
                    new GameplayCandidateOutcomeFeature(
                        "cost.action-points",
                        cost.ActionPoints),
                }),
                payload);
        }

        public GameplayTransitionPayload PreparePayload(
            GameplayDecisionContext context,
            GameplayExecutableCandidateEvaluation evaluation) =>
            evaluation?.FrozenPreparation
                as GameplayResolvedActionTransitionPayload
            ?? throw new ArgumentException(
                "Interaction preparation is missing.",
                nameof(evaluation));

        private static bool TryFindObjective(
            IEnumerable<GameplayObjectiveSnapshot> objectives,
            string objectiveId,
            out GameplayObjectiveSnapshot found)
        {
            foreach (GameplayObjectiveSnapshot objective in objectives)
                if (string.Equals(
                    objective.ObjectiveId,
                    objectiveId,
                    StringComparison.Ordinal))
                {
                    found = objective;
                    return true;
                }
            found = default;
            return false;
        }
    }

    internal static class GameplayBasicCandidateRouteUtility
    {
        public static void Require(
            GameplayDecisionContext context,
            GameplayCandidate candidate,
            Func<GameplayCapabilityProfile, bool> supports,
            string routeId)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (candidate == null)
                throw new ArgumentNullException(nameof(candidate));
            if (!supports(candidate.Profile))
                throw new NotSupportedException(
                    $"Route '{routeId}' cannot evaluate '{candidate.Profile.Signature}'.");
        }

        public static GameplayExecutableCandidateEvaluation Result(
            string routeId,
            GameplayDecisionContext context,
            GameplayCandidate candidate,
            bool legal,
            string failure,
            GameplayCandidateOutcomeEstimate outcome,
            object preparation) => new GameplayExecutableCandidateEvaluation(
                routeId,
                candidate,
                context.State.CanonicalHash,
                legal,
                legal ? string.Empty : failure,
                outcome,
                evidence: null,
                frozenPreparation: legal ? preparation : null);
    }
}
