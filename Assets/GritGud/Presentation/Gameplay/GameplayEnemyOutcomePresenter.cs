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

        public void RequestEncounterCompletion()
        {
            if (!session.RequestEncounterCompletionAtTurnEnd())
                return;
            const string message =
                "All hostile actors are incapacitated. End the current turn to conclude the encounter.";
            dialogue.Append(
                GameplayDialogueChannel.System,
                "HOSTILES INCAPACITATED",
                message);
            actionController.PresentExternalStatus(message);
            sessionPresenter.RefreshModePresentation();
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
    }
}
