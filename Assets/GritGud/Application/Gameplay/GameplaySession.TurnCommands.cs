using System.Collections.Generic;

namespace GritGud.Application.Gameplay
{
    public sealed partial class GameplaySession
    {
        public bool EnterTurnMode()
        {
            return TryEnterTurnMode(out _);
        }

        public bool TryEnterTurnMode(out TurnModeEntryFailure failure)
        {
            RequireLegacyMutationAllowed(nameof(TryEnterTurnMode));
            return turnLifecycle.TryEnterTurnMode(out failure);
        }

        public void AdvanceContinuousTime(float elapsedSeconds)
        {
            RequireLegacyMutationAllowed(nameof(AdvanceContinuousTime));
            turnLifecycle.AdvanceContinuousTime(elapsedSeconds);
        }

        public bool RequestEncounterCompletionAtTurnEnd()
        {
            RequireLegacyMutationAllowed(
                nameof(RequestEncounterCompletionAtTurnEnd));
            return turnLifecycle.RequestEncounterCompletionAtTurnEnd();
        }

        public bool TryExitTurnMode(out TurnModeExitFailure failure)
        {
            RequireLegacyMutationAllowed(nameof(TryExitTurnMode));
            return turnLifecycle.TryExitTurnMode(out failure);
        }

        public bool TryEndTurn(string actorId, out TurnEndFailure failure)
        {
            RequireLegacyMutationAllowed(nameof(TryEndTurn));
            bool encounterWasActive = EncounterActive;
            bool ended = turnLifecycle.TryEndTurn(actorId, out failure);
            if (ended && encounterWasActive && !EncounterActive)
            {
                ReplaceInitiativeScope(allInitiativeOrder);
                encounterState = encounterState.WithParticipants(
                    System.Array.Empty<string>());
            }
            return ended;
        }

        public void BeginEmergencyReaction(
            string attackerId,
            IReadOnlyList<string> responderIds,
            int actionPointAllowance)
        {
            RequireLegacyMutationAllowed(nameof(BeginEmergencyReaction));
            turnLifecycle.BeginEmergencyReaction(
                attackerId,
                responderIds,
                actionPointAllowance);
        }

        public bool TryEndEmergencyTurn(
            string actorId,
            out bool responsePassCompleted,
            out TurnEndFailure failure)
        {
            RequireLegacyMutationAllowed(nameof(TryEndEmergencyTurn));
            return turnLifecycle.TryEndEmergencyTurn(
                actorId,
                out responsePassCompleted,
                out failure);
        }

        public void CompleteEmergencyReaction(string resumeActorId)
        {
            RequireLegacyMutationAllowed(nameof(CompleteEmergencyReaction));
            turnLifecycle.CompleteEmergencyReaction(resumeActorId);
        }

        public bool CompleteVoluntaryWorldTurn()
        {
            RequireLegacyMutationAllowed(nameof(CompleteVoluntaryWorldTurn));
            return turnLifecycle.CompleteVoluntaryWorldTurn();
        }
    }
}
