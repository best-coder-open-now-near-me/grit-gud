using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;

namespace GritGud.Application.Gameplay
{
    public sealed class GameplayDroneAttackTransitionPayload :
        GameplayTransitionPayload
    {
        public GameplayDroneAttackTransitionPayload(
            GameplaySemanticSubjectKind targetKind,
            AttackDefinition attack,
            DroneAttackRecord action)
            : base(
                GameplayCapabilityProfiles.DroneAttack(attack, targetKind),
                (action ?? throw new ArgumentNullException(nameof(action)))
                    .ControllerActorId,
                action.TargetId)
        {
            if (!string.Equals(
                    action.TargetKind,
                    targetKind.ToString(),
                    StringComparison.Ordinal))
                throw new ArgumentException(
                    "Drone attack target kind does not match its capability.",
                    nameof(action));
            Action = action;
        }

        public DroneAttackRecord Action { get; }
    }

    public sealed class GameplayDroneAttackTransitionReducer :
        IGameplaySemanticTransitionReducer
    {
        public bool Supports(GameplayCapabilityProfile profile)
        {
            if (profile == null
                || profile.Capability != GameplaySemanticCapability.DirectAttack)
                return false;
            try
            {
                return profile.GetTrait("delivery") == "immediate-ranged"
                    && profile.GetTrait("targeting") == "semantic-subject"
                    && profile.GetTrait("resource") == "controller-drone-weapon"
                    && (profile.GetTrait("consequence") == "actor-wound"
                        || profile.GetTrait("consequence")
                            == "destructible-damage"
                        || profile.GetTrait("consequence")
                            == "vehicle-integrity");
            }
            catch (KeyNotFoundException)
            {
                return false;
            }
        }

        public GameplayReductionResult Reduce(
            GameplayCombatStateSnapshot state,
            GameplaySemanticTransition transition)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (transition?.Payload
                is not GameplayDroneAttackTransitionPayload payload
                || !Supports(transition.Profile))
                throw new NotSupportedException(
                    "Drone attack reducer requires an exact drone weapon profile.");
            state.RequireCoverage(GameplayCombatStateCoverage.Drones);
            GameplaySessionStateSnapshot session = state.Session;
            DroneAttackRecord action = payload.Action;
            DroneSnapshot drone = FindDrone(state.Drones, action.DroneId);
            GameplayActorSnapshot controller = session.GetActor(
                action.ControllerActorId);
            if (!drone.IsOperational
                || drone.Definition.InitiativeBinding
                    != DroneInitiativeBinding.ControllerTurn
                || !string.Equals(
                    drone.Definition.ControllerActorId,
                    controller.ActorId,
                    StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Drone weapon use requires its operational bound controller.");
            if (session.Mode != GameplaySessionMode.TurnBased
                || session.Operation != GameplaySessionOperation.None
                || !string.Equals(
                    session.ActiveActorId,
                    controller.ActorId,
                    StringComparison.Ordinal)
                || controller.IsIncapacitated)
                throw new InvalidOperationException(
                    "A drone can attack only during its capable controller's idle turn.");
            if (!CostsMatch(action.Cost, drone.Definition.Attack.TurnCost)
                || !BudgetsMatch(controller.TurnBudget, action.PreviousBudget))
                throw new InvalidOperationException(
                    "Drone attack was prepared against stale cost or budget evidence.");

            var mutation = new GameplayCanonicalStateMutation(state)
            {
                JournalSequence = checked(
                    session.JournalSequence
                        + (action.Consequence is DestructibleDamageRecord
                            ? 2L
                            : 1L)),
                Revision = checked(session.Revision + 1L),
                LastTransitionSequence = transition.Identity.Sequence,
            };
            mutation.ReplaceActor(GameplayCanonicalStateMutation.CopyActor(
                controller,
                budget: action.ResultingBudget));
            ApplyConsequence(state, mutation, payload.SubjectKind, action);
            GameplayCombatStateSnapshot resulting = mutation.Build();
            return new GameplayReductionResult(
                state,
                resulting,
                new GameplayDomainEvent[]
                {
                    new GameplayTransitionReducedEvent(
                        transition.Identity,
                        action.TargetId,
                        action),
                });
        }

        private static void ApplyConsequence(
            GameplayCombatStateSnapshot state,
            GameplayCanonicalStateMutation mutation,
            GameplaySemanticSubjectKind targetKind,
            DroneAttackRecord action)
        {
            switch (targetKind)
            {
                case GameplaySemanticSubjectKind.Actor
                    when action.Consequence is ActorWoundRecord wound:
                    GameplayActorSnapshot target = state.Session.GetActor(
                        action.TargetId);
                    if (!target.Wounds.HasSameState(wound.Previous)
                        || !string.Equals(
                            wound.ActorId,
                            action.TargetId,
                            StringComparison.Ordinal))
                        throw new InvalidOperationException(
                            "Drone attack actor consequence is stale.");
                    mutation.ReplaceActor(
                        GameplayCanonicalStateMutation.CopyActor(
                            target,
                            budget: new TurnBudget(
                                target.TurnBudget.ActionPoints,
                                Math.Min(
                                    target.TurnBudget.MovementOpportunity,
                                    Math.Max(
                                        0f,
                                        target.TurnMovementAllowance
                                            - wound.Resulting.MovementPenalty))),
                            wounds: wound.Resulting));
                    return;
                case GameplaySemanticSubjectKind.DestructibleProp
                    when action.Consequence is DestructibleDamageRecord damage:
                    state.RequireCoverage(
                        GameplayCombatStateCoverage.Destructibles);
                    DestructiblePropSnapshot prop = mutation.GetDestructible(
                        action.TargetId);
                    if (!PropStatesMatch(prop, damage.Previous))
                        throw new InvalidOperationException(
                            "Drone attack destructible consequence is stale.");
                    mutation.ReplaceDestructible(damage.Resulting);
                    return;
                case GameplaySemanticSubjectKind.Vehicle
                    when action.Consequence is DroneIntegrityDamageRecord damage:
                    DroneSnapshot targetDrone = mutation.GetDrone(action.TargetId);
                    if (!DroneStatesMatch(targetDrone, damage.Previous))
                        throw new InvalidOperationException(
                            "Drone attack integrity consequence is stale.");
                    mutation.ReplaceDrone(damage.Resulting);
                    return;
                default:
                    throw new ArgumentException(
                        "Drone attack consequence does not match its target kind.",
                        nameof(action));
            }
        }

        private static DroneSnapshot FindDrone(
            IEnumerable<DroneSnapshot> drones,
            string droneId)
        {
            foreach (DroneSnapshot drone in drones)
                if (string.Equals(drone.DroneId, droneId,
                    StringComparison.Ordinal)) return drone;
            throw new KeyNotFoundException(
                $"Drone '{droneId}' is absent from canonical state.");
        }

        private static bool CostsMatch(ActionCost left, ActionCost right) =>
            left.ActionPoints == right.ActionPoints
            && left.MovementOpportunity == right.MovementOpportunity
            && left.Mobility == right.Mobility;

        private static bool BudgetsMatch(TurnBudget left, TurnBudget right) =>
            left.ActionPoints == right.ActionPoints
            && left.MovementOpportunity == right.MovementOpportunity;

        private static bool DroneStatesMatch(
            DroneSnapshot left,
            DroneSnapshot right) =>
            string.Equals(left.DroneId, right.DroneId, StringComparison.Ordinal)
            && left.Position.DistanceTo(right.Position) == 0f
            && left.FacingDegrees == right.FacingDegrees
            && left.RemainingIntegrity == right.RemainingIntegrity;

        private static bool PropStatesMatch(
            DestructiblePropSnapshot left,
            DestructiblePropSnapshot right) =>
            string.Equals(left.PropId, right.PropId, StringComparison.Ordinal)
            && left.State == right.State
            && left.RemainingIntegrity == right.RemainingIntegrity
            && left.Pose.Position.DistanceTo(right.Pose.Position) == 0f
            && left.DetachedFractureChunks
                == right.DetachedFractureChunks;
    }
}
