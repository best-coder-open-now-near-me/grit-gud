using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;

namespace GritGud.Application.Gameplay
{
    public sealed class GameplayActorDroneAttackTransitionPayload :
        GameplayTransitionPayload
    {
        public GameplayActorDroneAttackTransitionPayload(
            AttackDefinition attack,
            ActorDroneAttackRecord action)
            : base(
                GameplayCapabilityProfiles.AttackDrone(attack),
                (action ?? throw new ArgumentNullException(nameof(action)))
                    .AttackerId,
                action.DroneId)
        {
            if (!string.Equals(
                    attack.ActionId,
                    action.AttackId,
                    StringComparison.Ordinal))
                throw new ArgumentException(
                    "Actor-drone attack record does not match its attack definition.",
                    nameof(action));
            Action = action;
        }

        public ActorDroneAttackRecord Action { get; }
    }

    public sealed class GameplayActorDroneAttackTransitionReducer :
        IGameplaySemanticTransitionReducer
    {
        public bool Supports(GameplayCapabilityProfile profile)
        {
            if (profile == null
                || profile.Capability != GameplaySemanticCapability.DirectAttack)
                return false;
            try
            {
                return GameplayCapabilityProfiles.GetSubjectKind(profile)
                        == GameplaySemanticSubjectKind.Vehicle
                    && profile.GetTrait("consequence") == "drone-integrity"
                    && profile.GetTrait("delivery") == "immediate-ranged"
                    && profile.GetTrait("resource") == "equipped-weapon"
                    && profile.GetTrait("targeting") == "semantic-subject";
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
                is not GameplayActorDroneAttackTransitionPayload payload
                || !Supports(transition.Profile))
                throw new NotSupportedException(
                    "Actor-drone reducer requires an exact drone integrity profile.");
            state.RequireCoverage(GameplayCombatStateCoverage.Drones);
            GameplaySessionStateSnapshot session = state.Session;
            ActorDroneAttackRecord action = payload.Action;
            if (session.Mode != GameplaySessionMode.TurnBased
                || session.Operation != GameplaySessionOperation.None
                || !string.Equals(session.ActiveActorId, action.AttackerId,
                    StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Only the idle active actor can attack a drone.");
            GameplayActorSnapshot attacker = session.GetActor(action.AttackerId);
            if (attacker.IsIncapacitated
                || attacker.IsPinned
                || !BudgetsMatch(attacker.TurnBudget, action.PreviousBudget)
                || action.Sequence != session.LastActionSequence + 1L)
                throw new InvalidOperationException(
                    "Actor-drone attack starts from stale actor state.");
            uint expectedSeed = GameplayAddressedRandom.SampleUInt32(
                session.RunIdentity,
                new GameplayTransitionIdentity(
                    action.Sequence,
                    GameplaySemanticCapability.DirectAttack.ToString(),
                    action.AttackerId,
                    action.DroneId),
                "resolution");
            if (action.ResolutionSeed != expectedSeed)
                throw new InvalidOperationException(
                    "Actor-drone attack seed does not match its canonical action identity.");
            DroneSnapshot drone = FindDrone(state.Drones, action.DroneId);
            if (!drone.IsOperational)
                throw new InvalidOperationException(
                    "Destroyed drones cannot be targeted as operational threats.");
            if (action.Damage != null
                && !DroneStatesMatch(drone, action.Damage.Previous))
                throw new InvalidOperationException(
                    "Actor-drone attack starts from stale drone integrity.");

            var mutation = new GameplayCanonicalStateMutation(state)
            {
                LastActionSequence = action.Sequence,
                JournalSequence = checked(session.JournalSequence + 1L),
                Revision = checked(session.Revision + 1L),
                LastTransitionSequence = transition.Identity.Sequence,
            };
            mutation.ReplaceActor(GameplayCanonicalStateMutation.CopyActor(
                attacker,
                budget: action.ResultingBudget));
            if (action.Damage != null)
                mutation.ReplaceDrone(action.Damage.Resulting);
            GameplayCombatStateSnapshot resulting = mutation.Build();
            return new GameplayReductionResult(
                state,
                resulting,
                new GameplayDomainEvent[]
                {
                    new GameplayTransitionReducedEvent(
                        transition.Identity,
                        action.DroneId,
                        action),
                });
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
    }
}
