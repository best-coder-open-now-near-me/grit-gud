using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;

namespace GritGud.Application.Gameplay
{
    public sealed class GameplayTurnTransitionReducer :
        IGameplaySemanticTransitionReducer
    {
        public bool Supports(GameplayCapabilityProfile profile) =>
            profile != null
            && (profile.Equals(
                    GameplayCapabilityProfiles.EndTurn(emergency: false))
                || profile.Equals(
                    GameplayCapabilityProfiles.EndTurn(emergency: true)));

        public GameplayReductionResult Reduce(
            GameplayCombatStateSnapshot state,
            GameplaySemanticTransition transition)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (transition == null)
                throw new ArgumentNullException(nameof(transition));
            if (!(transition.Payload is GameplayEndTurnTransitionPayload payload)
                || !Supports(payload.Profile))
                throw new ArgumentException(
                    "Turn transition requires a supported end-turn payload.",
                    nameof(transition));
            GameplaySessionStateSnapshot session = state.Session;
            if (session.Mode != GameplaySessionMode.TurnBased
                || session.Operation != GameplaySessionOperation.None
                || !string.Equals(
                    session.ActiveActorId,
                    payload.ActorId,
                    StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Turn completion requires the idle active actor.");

            var mutation = new GameplayCanonicalStateMutation(state);
            TurnEndRecord record = session.TurnPhase
                    == GameplayTurnPhase.EmergencyReaction
                ? ReduceEmergency(session, mutation, payload)
                : ReduceNormal(session, mutation, payload);
            AdvanceSmokeFields(state, mutation);
            mutation.LastTurnSequence = record.Sequence;
            mutation.LastTransitionSequence = transition.Identity.Sequence;
            GameplayCombatStateSnapshot resulting = mutation.Build();
            return new GameplayReductionResult(
                state,
                resulting,
                new GameplayDomainEvent[]
                {
                    new GameplayTransitionReducedEvent(
                        transition.Identity,
                        payload.ActorId,
                        record),
                });
        }

        private static TurnEndRecord ReduceNormal(
            GameplaySessionStateSnapshot session,
            GameplayCanonicalStateMutation mutation,
            GameplayEndTurnTransitionPayload payload)
        {
            if (payload.Emergency)
                throw new InvalidOperationException(
                    "Normal phase cannot use an emergency end-turn profile.");
            long sequence = checked(session.LastTurnSequence + 1L);
            if (!session.EncounterActive)
            {
                var cycle = new VoluntaryTurnCycleRecord(
                    checked(session.LastVoluntaryTurnCycleSequence + 1L),
                    session.Actors);
                mutation.PendingVoluntaryTurnCycle = cycle;
                mutation.Operation = GameplaySessionOperation.ResolvingWorldTurn;
                mutation.JournalSequence = checked(
                    mutation.JournalSequence + 1L);
                mutation.Revision = checked(mutation.Revision + 1L);
                return new TurnEndRecord(
                    sequence,
                    payload.ActorId,
                    payload.ActorId);
            }

            if (session.EncounterCompletionRequested)
            {
                if (payload.MinimumVoluntaryTurnSeconds <= 0f)
                    throw new ArgumentException(
                        "Encounter completion requires the scenario's voluntary-turn reentry duration.",
                        nameof(payload));
                var cycle = new VoluntaryTurnCycleRecord(
                    checked(session.LastVoluntaryTurnCycleSequence + 1L),
                    session.Actors);
                mutation.EncounterActive = false;
                mutation.EncounterCompletionRequested = false;
                mutation.Mode = GameplaySessionMode.Exploration;
                mutation.TurnContext = TurnModeContext.None;
                mutation.VoluntaryTurnReentrySecondsRemaining =
                    payload.MinimumVoluntaryTurnSeconds;
                mutation.LastVoluntaryTurnCycleSequence = cycle.Sequence;
                mutation.JournalSequence = checked(
                    mutation.JournalSequence + 4L);
                mutation.Revision = checked(mutation.Revision + 2L);
                return new TurnEndRecord(
                    sequence,
                    payload.ActorId,
                    payload.ActorId);
            }

            int activeIndex = IndexOf(
                session.InitiativeOrder,
                payload.ActorId);
            string nextActorId = FindNextCapableActor(session, activeIndex)
                ?? payload.ActorId;
            PersonalTurnStartRecord personalTurnStart = RefreshActor(
                mutation,
                mutation.GetActor(nextActorId));
            mutation.ActiveActorId = nextActorId;
            mutation.JournalSequence = checked(mutation.JournalSequence + 1L);
            mutation.Revision = checked(mutation.Revision + 1L);
            return new TurnEndRecord(
                sequence,
                payload.ActorId,
                nextActorId,
                personalTurnStart: personalTurnStart);
        }

        private static TurnEndRecord ReduceEmergency(
            GameplaySessionStateSnapshot session,
            GameplayCanonicalStateMutation mutation,
            GameplayEndTurnTransitionPayload payload)
        {
            if (!payload.Emergency)
                throw new InvalidOperationException(
                    "Emergency phase requires its emergency end-turn profile.");
            int nextIndex = checked(session.EmergencyResponderIndex + 1);
            bool completed = nextIndex >= session.EmergencyResponders.Count;
            mutation.EmergencyResponderIndex = nextIndex;
            string nextActorId = session.EmergencyResumeActorId;
            GameplayActorSnapshot ending = mutation.GetActor(payload.ActorId);
            if (!ending.SuspendedTurnBudget.HasValue)
                throw new InvalidOperationException(
                    "Emergency responder has no suspended normal-turn budget.");
            mutation.ReplaceActor(
                GameplayCanonicalStateMutation.CopyActor(
                    ending,
                    budget: ending.SuspendedTurnBudget.Value,
                    emergencyActionPointAllowance: 0,
                    suspendedTurnBudget: null,
                    replaceSuspendedTurnBudget: true));
            if (!completed)
            {
                nextActorId = session.EmergencyResponders[nextIndex];
                GameplayActorSnapshot next = mutation.GetActor(nextActorId);
                int allowance = ending.EmergencyActionPointAllowance;
                var budget = new TurnBudget(
                    allowance,
                    Math.Max(
                        0f,
                        next.TurnMovementAllowance
                            - next.Wounds.MovementPenalty));
                mutation.ReplaceActor(
                    GameplayCanonicalStateMutation.CopyActor(
                        next,
                        budget: budget,
                        emergencyActionPointAllowance: allowance,
                        suspendedTurnBudget: next.TurnBudget,
                        replaceSuspendedTurnBudget: true));
                mutation.ActiveActorId = nextActorId;
            }
            mutation.JournalSequence = checked(mutation.JournalSequence + 1L);
            mutation.Revision = checked(mutation.Revision + 1L);
            return new TurnEndRecord(
                checked(session.LastTurnSequence + 1L),
                payload.ActorId,
                nextActorId,
                GameplayTurnKind.EmergencyReaction,
                session.EmergencyResumeActorId);
        }

        private static void AdvanceSmokeFields(
            GameplayCombatStateSnapshot state,
            GameplayCanonicalStateMutation mutation)
        {
            if (!state.Covers(GameplayCombatStateCoverage.SmokeFields)) return;
            var remaining = new List<SmokeFieldSnapshot>();
            foreach (SmokeFieldSnapshot smoke in state.SmokeFields)
            {
                float fraction = Math.Max(
                    0f,
                    smoke.RemainingFraction
                        - (1f / smoke.Field.Definition.DurationTurnEnds));
                if (fraction > 0f)
                    remaining.Add(new SmokeFieldSnapshot(
                        smoke.Field,
                        fraction));
            }
            mutation.ReplaceSmokeFields(remaining);
        }

        private static PersonalTurnStartRecord RefreshActor(
            GameplayCanonicalStateMutation mutation,
            GameplayActorSnapshot actor)
        {
            PersonalTurnActionPointGrant grant =
                PersonalTurnActionPointRules.Grant(
                    actor.TurnBudget.ActionPoints,
                    actor.ActionPointEconomy);
            var budget = new TurnBudget(
                grant.ResultingActionPoints,
                Math.Max(
                    0f,
                    actor.TurnMovementAllowance
                        - actor.Wounds.MovementPenalty));
            mutation.ReplaceActor(GameplayCanonicalStateMutation.CopyActor(
                actor,
                budget: budget));
            return new PersonalTurnStartRecord(
                actor.ActorId,
                grant,
                budget.MovementOpportunity);
        }

        private static int IndexOf(
            IReadOnlyList<string> ids,
            string actorId)
        {
            for (int index = 0; index < ids.Count; index++)
                if (string.Equals(ids[index], actorId, StringComparison.Ordinal))
                    return index;
            throw new InvalidOperationException(
                "Active actor is absent from initiative.");
        }

        private static string FindNextCapableActor(
            GameplaySessionStateSnapshot session,
            int activeIndex)
        {
            for (int offset = 1; offset <= session.InitiativeOrder.Count; offset++)
            {
                string actorId = session.InitiativeOrder[
                    (activeIndex + offset) % session.InitiativeOrder.Count];
                if (!session.GetActor(actorId).IsIncapacitated)
                    return actorId;
            }
            return null;
        }
    }
}
