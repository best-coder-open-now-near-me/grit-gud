using System;
using System.Collections.Generic;
using GritGud.Application.Gameplay;

namespace GritGud.Presentation.Gameplay
{
    internal sealed class GameplayEnemyOutcomePresenter
    {
        private readonly GameplaySession session;
        private readonly GameplayEnemyRuntimeRegistry enemies;
        private readonly GameplaySessionPresenter sessionPresenter;
        private readonly GameplayActionController actionController;
        private readonly GameplayPartyControlSession partyControl;
        private readonly GameplayDialogueLog dialogue;
        private readonly HashSet<string> reportedPartyIncapacitations =
            new HashSet<string>(StringComparer.Ordinal);
        private bool partyIncapacitationReported;
        private bool encounterWasActive;
        private bool partyVictoryExitPending;

        public GameplayEnemyOutcomePresenter(
            GameplaySession session,
            GameplayEnemyRuntimeRegistry enemies,
            GameplaySessionPresenter sessionPresenter,
            GameplayActionController actionController,
            GameplayPartyControlSession partyControl,
            GameplayDialogueLog dialogue)
        {
            this.session = session ?? throw new ArgumentNullException(
                nameof(session));
            this.enemies = enemies ?? throw new ArgumentNullException(
                nameof(enemies));
            this.sessionPresenter = sessionPresenter
                ?? throw new ArgumentNullException(nameof(sessionPresenter));
            this.actionController = actionController
                ?? throw new ArgumentNullException(nameof(actionController));
            this.partyControl = partyControl ?? throw new ArgumentNullException(
                nameof(partyControl));
            this.dialogue = dialogue ?? throw new ArgumentNullException(
                nameof(dialogue));
        }

        public void PresentNewIncapacitations()
        {
            foreach (GameplayEnemyRuntimeRegistry.Entry enemy in
                enemies.Entries)
            {
                if (!session.IsActorIncapacitated(enemy.Definition.Id)
                    || !enemy.Presentation.PresentIncapacitation())
                {
                    continue;
                }
                dialogue.Append(
                    GameplayDialogueChannel.System,
                    "HOSTILE INCAPACITATED",
                    $"{enemy.Definition.Id} can no longer act or respond.");
            }

            foreach (string actorId in partyControl.ActorIds)
            {
                if (!session.IsActorIncapacitated(actorId)
                    || !reportedPartyIncapacitations.Add(actorId))
                {
                    continue;
                }

                dialogue.Append(
                    GameplayDialogueChannel.System,
                    "PARTY MEMBER INCAPACITATED",
                    $"{GetActorDisplayName(actorId)} can no longer act or respond.");
            }
        }

        public void PresentEncounterStarted()
        {
            bool encounterIsActive = session.EncounterActive;
            if (encounterIsActive == encounterWasActive)
            {
                return;
            }

            encounterWasActive = encounterIsActive;
            if (!encounterIsActive)
            {
                return;
            }

            string message = BuildEncounterRosterMessage();
            dialogue.Append(
                GameplayDialogueChannel.System,
                "ENCOUNTER STARTED",
                message);
            actionController.PresentExternalStatus(message);
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

        private string GetActorDisplayName(string actorId) =>
            session.Scenario.GetActor(actorId).CharacterProfile?.DisplayName
            ?? actorId;

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

        private string BuildEncounterRosterMessage()
        {
            var combatants = new List<string>();
            foreach (string actorId in session.InitiativeOrder)
                combatants.Add(GetActorDisplayName(actorId));
            return combatants.Count == 0
                ? "Combat started. No combatants were registered."
                : "Combat started. Roster ("
                    + combatants.Count
                    + "): "
                    + string.Join(", ", combatants)
                    + ".";
        }
    }
}
