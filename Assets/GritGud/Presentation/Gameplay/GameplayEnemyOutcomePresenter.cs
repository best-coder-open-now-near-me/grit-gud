using System;
using GritGud.Application.Gameplay;

namespace GritGud.Presentation.Gameplay
{
    internal sealed class GameplayEnemyOutcomePresenter
    {
        private readonly GameplaySession session;
        private readonly GameplaySessionPresenter sessionPresenter;
        private readonly GameplayActionController actionController;
        private readonly GameplayDialogueLog dialogue;
        private bool partyIncapacitationReported;
        private bool partyVictoryExitPending;

        public GameplayEnemyOutcomePresenter(
            GameplaySession session,
            GameplaySessionPresenter sessionPresenter,
            GameplayActionController actionController,
            GameplayDialogueLog dialogue)
        {
            this.session = session ?? throw new ArgumentNullException(
                nameof(session));
            this.sessionPresenter = sessionPresenter
                ?? throw new ArgumentNullException(nameof(sessionPresenter));
            this.actionController = actionController
                ?? throw new ArgumentNullException(nameof(actionController));
            this.dialogue = dialogue ?? throw new ArgumentNullException(
                nameof(dialogue));
        }

        public void ResolvePartyIncapacitation()
        {
            if (!partyIncapacitationReported)
            {
                partyIncapacitationReported = true;
                dialogue.Append(
                    GameplayDialogueChannel.System,
                    "PARTY INCAPACITATED",
                    "No party member can continue. Reload or return to the menu to reset the scenario.");
                actionController.PresentExternalStatus(
                    "Party incapacitated. Reload or return to the menu to reset.");
            }
            CompleteEncounter("The party is incapacitated.");
        }

        public bool BeginPartyVictory()
        {
            if (!session.EncounterActive)
            {
                return false;
            }

            session.CompleteEncounter();
            partyVictoryExitPending = session.Mode
                == GameplaySessionMode.TurnBased;
            const string message =
                "All hostile actors are incapacitated. Encounter complete.";
            dialogue.Append(
                GameplayDialogueChannel.System,
                "ENCOUNTER COMPLETE",
                message);
            actionController.PresentExternalStatus(message);
            sessionPresenter.RefreshModePresentation();
            ContinuePartyVictoryExit();
            return true;
        }

        public bool ContinuePartyVictoryExit()
        {
            if (!partyVictoryExitPending)
            {
                return false;
            }

            if (session.Mode == GameplaySessionMode.Exploration)
            {
                partyVictoryExitPending = false;
                return true;
            }

            if (session.Mode != GameplaySessionMode.TurnBased)
            {
                return true;
            }

            if (actionController.TryExitTurnMode())
            {
                partyVictoryExitPending = false;
                actionController.PresentExternalStatus(
                    "Encounter complete. Exploration resumed.");
                return true;
            }

            actionController.PresentExternalStatus(
                "Encounter complete. Waiting for the current presentation to finish.");
            return true;
        }

        private void CompleteEncounter(string message)
        {
            if (!session.EncounterActive)
                return;
            session.CompleteEncounter();
            dialogue.Append(
                GameplayDialogueChannel.System,
                "ENCOUNTER COMPLETE",
                message);
            actionController.PresentExternalStatus(message);
            sessionPresenter.RefreshModePresentation();
        }

    }
}
