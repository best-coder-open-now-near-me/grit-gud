using System;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;

namespace GritGud.Presentation.Gameplay
{
    internal sealed class GameplayEnemyExplorationCoordinator
    {
        private readonly GameplaySession session;
        private readonly GameplayEnemyRuntimeRegistry enemies;
        private readonly GameplaySessionPresenter sessionPresenter;
        private readonly GameplayActionController actionController;
        private readonly GameplayPartyControlSession partyControl;
        private readonly GameplayEnemyDecisionSession decisions;
        private readonly GameplayDialogueLog dialogue;
        private readonly Func<bool> beginEncounter;
        private readonly float detectionIntervalSeconds;
        private float detectionDelaySeconds;

        public GameplayEnemyExplorationCoordinator(
            GameplaySession session,
            GameplayEnemyRuntimeRegistry enemies,
            GameplaySessionPresenter sessionPresenter,
            GameplayActionController actionController,
            GameplayPartyControlSession partyControl,
            GameplayEnemyDecisionSession decisions,
            GameplayDialogueLog dialogue,
            Func<bool> beginEncounter,
            float detectionIntervalSeconds)
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
            this.decisions = decisions ?? throw new ArgumentNullException(
                nameof(decisions));
            this.dialogue = dialogue ?? throw new ArgumentNullException(
                nameof(dialogue));
            this.beginEncounter = beginEncounter
                ?? throw new ArgumentNullException(nameof(beginEncounter));
            this.detectionIntervalSeconds = detectionIntervalSeconds;
        }

        public void Tick(float unscaledDeltaTime)
        {
            detectionDelaySeconds -= unscaledDeltaTime;
            if (detectionDelaySeconds > 0f)
                return;
            detectionDelaySeconds = detectionIntervalSeconds;

            foreach (GameplayEnemyRuntimeRegistry.Entry enemy in
                enemies.OrderedEntries)
            {
                if (session.IsActorIncapacitated(enemy.Definition.Id))
                    continue;
                EnemyTacticalDecisionRecord detection =
                    decisions.EvaluateBestDetection(
                        enemy.Definition.Id,
                        partyControl.ActorIds,
                        enemy.TacticalQuery.CaptureExposure);
                if (detection == null)
                    continue;

                decisions.Commit(detection);
                dialogue.AppendCombatDiagnostic(
                    GameplayCombatDiagnosticFormatter.FormatEnemyDecision(
                        detection));
                if (beginEncounter())
                {
                    actionController.PresentExternalStatus(
                        $"{enemy.Definition.Id} detected {detection.TargetId}. Combat initiated.");
                    sessionPresenter.RefreshModePresentation();
                }
                return;
            }
        }
    }
}
