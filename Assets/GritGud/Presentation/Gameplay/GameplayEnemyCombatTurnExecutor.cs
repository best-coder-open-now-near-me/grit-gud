using System;
using System.Collections.Generic;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    internal sealed class GameplayEnemyCombatTurnExecutor
    {
        private readonly GameplaySession session;
        private readonly GameplayEnemyRuntimeRegistry enemies;
        private readonly GameplaySessionPresenter sessionPresenter;
        private readonly GameplayActionController actionController;
        private readonly GameplayAttackController attackController;
        private readonly GameplayDisplacementController displacementController;
        private readonly GameplayEmergencyCycleSession emergencyCycle;
        private readonly GameplayPartyControlSession partyControl;
        private readonly GameplayEnemyDecisionSession decisions;
        private readonly GameplayDialogueLog dialogue;
        private string observedActiveActorId;
        private long observedTurnSequence = -1L;
        private float decisionDelaySeconds;

        public GameplayEnemyCombatTurnExecutor(
            GameplaySession session,
            GameplayEnemyRuntimeRegistry enemies,
            GameplaySessionPresenter sessionPresenter,
            GameplayActionController actionController,
            GameplayAttackController attackController,
            GameplayDisplacementController displacementController,
            GameplayEmergencyCycleSession emergencyCycle,
            GameplayPartyControlSession partyControl,
            GameplayEnemyDecisionSession decisions,
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
            this.attackController = attackController
                ?? throw new ArgumentNullException(nameof(attackController));
            this.displacementController = displacementController
                ?? throw new ArgumentNullException(nameof(displacementController));
            this.emergencyCycle = emergencyCycle
                ?? throw new ArgumentNullException(nameof(emergencyCycle));
            this.partyControl = partyControl ?? throw new ArgumentNullException(
                nameof(partyControl));
            this.decisions = decisions ?? throw new ArgumentNullException(
                nameof(decisions));
            this.dialogue = dialogue ?? throw new ArgumentNullException(
                nameof(dialogue));
        }

        public void Tick(float deltaTime, float unscaledDeltaTime)
        {
            RefreshTurnIdentity();
            if (!enemies.TryGet(
                    session.ActiveActorId,
                    out GameplayEnemyRuntimeRegistry.Entry activeEnemy))
            {
                return;
            }

            if (activeEnemy.Playback.IsPlaying)
            {
                TickMovement(activeEnemy, deltaTime);
                return;
            }

            if (session.Operation != GameplaySessionOperation.None)
                return;

            decisionDelaySeconds = Mathf.Max(
                0f,
                decisionDelaySeconds - unscaledDeltaTime);
            if (decisionDelaySeconds > 0f)
                return;

            ExecuteDecision(activeEnemy);
        }

        private void ExecuteDecision(
            GameplayEnemyRuntimeRegistry.Entry enemy)
        {
            if (session.IsActorIncapacitated(enemy.Definition.Id))
            {
                EndActiveTurn(enemy, "incapacitated actor skipped");
                return;
            }

            GameplayActorSnapshot actor = session.GetActor(
                enemy.Definition.Id);
            if (actor.IsPinned)
            {
                ExecutePushOff(enemy, actor.PinState.PropId);
                return;
            }

            EnemyTargetSelection target = decisions.SelectBestTarget(
                enemy.Definition.Id,
                partyControl.ActorIds,
                enemy.TacticalQuery.CaptureExposure);
            if (target == null)
            {
                EndActiveTurn(enemy, "no capable hostile target remains");
                return;
            }

            string targetId = target.TargetId;
            TargetExposureSnapshot exposure = target.Exposure;
            IReadOnlyList<EnemyMovementOption> movementOptions =
                decisions.RequiresMovementSearch(
                    enemy.Definition.Id,
                    targetId,
                    exposure)
                    ? enemy.TacticalQuery.BuildMovementOptions(targetId)
                    : Array.Empty<EnemyMovementOption>();
            EnemyTacticalDecisionRecord decision = decisions.EvaluateTurn(
                enemy.Definition.Id,
                targetId,
                exposure,
                movementOptions,
                enemy.AttacksCommittedThisTurn);
            decisions.Commit(decision);
            AppendDecisionDiagnostic(decision);

            switch (decision.Kind)
            {
                case EnemyTacticalDecisionKind.Attack:
                    ExecuteAttack(enemy, decision);
                    break;
                case EnemyTacticalDecisionKind.Move:
                    session.CommitMovementRoute(decision.MovementRoute);
                    enemy.Playback.Begin(decision.MovementRoute);
                    actionController.PresentExternalStatus(
                        $"{enemy.Definition.Id} is repositioning.");
                    break;
                case EnemyTacticalDecisionKind.PushOff:
                    ExecutePushOff(
                        enemy,
                        decision.TargetId,
                        recordDecision: false);
                    break;
                case EnemyTacticalDecisionKind.EndTurn:
                    EndActiveTurn(enemy, decision.Rationale);
                    break;
                default:
                    throw new InvalidOperationException(
                        $"Decision '{decision.Kind}' cannot execute during a turn.");
            }
        }

        private void ExecutePushOff(
            GameplayEnemyRuntimeRegistry.Entry enemy,
            string propId,
            bool recordDecision = true)
        {
            DisplacementActionDefinition pushOff = null;
            foreach (DisplacementActionDefinition candidate in
                enemy.Definition.DisplacementActions)
            {
                if (candidate.Intent == DisplacementActionKind.PushOff)
                {
                    pushOff = candidate;
                    break;
                }
            }
            if (pushOff == null)
            {
                actionController.PresentExternalStatus(
                    $"{enemy.Definition.Id} has no authored Push Off action.");
                EndActiveTurn(enemy, "no authored push off action");
                return;
            }

            if (recordDecision)
            {
                EnemyTacticalDecisionRecord decision =
                    decisions.EvaluatePushOff(
                        enemy.Definition.Id,
                        propId);
                decisions.Commit(decision);
                AppendDecisionDiagnostic(decision);
            }
            if (displacementController.TryExecuteIntent(
                    enemy.Definition.Id,
                    pushOff.Id,
                    propId,
                    out _,
                    out _,
                    out DisplacementResolutionFailure failure))
            {
                actionController.PresentExternalStatus(
                    $"{enemy.Definition.Id} pushed off the pinning prop.");
                decisionDelaySeconds = enemy.Presentation
                    .PresentationDefinition.PostDecisionDelaySeconds;
                return;
            }

            actionController.PresentExternalStatus(
                $"{enemy.Definition.Id} could not push off: {failure}.");
            EndActiveTurn(enemy, $"push off failed: {failure}");
        }

        private void ExecuteAttack(
            GameplayEnemyRuntimeRegistry.Entry enemy,
            EnemyTacticalDecisionRecord decision)
        {
            if (attackController.TryResolveActorAttack(
                    enemy.Definition.Id,
                    decision.Exposure,
                    out GameplayActionRecord action,
                    out AttackResolutionFailure failure))
            {
                enemy.AttacksCommittedThisTurn++;
                AttackResolutionRecord resolution =
                    ((AttackResolvedActionOutcome)action.Outcomes[0]).Attack;
                actionController.PresentExternalStatus(
                    resolution.Hit
                        ? $"{enemy.Definition.Id} hit {resolution.TargetId}."
                        : $"{enemy.Definition.Id} missed {resolution.TargetId}.");
                decisionDelaySeconds = enemy.Presentation
                    .PresentationDefinition.PostAttackDelaySeconds;
                return;
            }

            actionController.PresentExternalStatus(
                $"{enemy.Definition.Id} attack failed: {failure}.");
            EndActiveTurn(enemy, $"attack failed: {failure}");
        }

        private void TickMovement(
            GameplayEnemyRuntimeRegistry.Entry enemy,
            float deltaTime)
        {
            if (!enemy.Playback.Tick(deltaTime))
                return;
            session.CompleteMovementResolution();
            GameplayActorSnapshot actor = session.GetActor(enemy.Definition.Id);
            enemy.View.Transform.SetPositionAndRotation(
                MovementRouteSampling.ToVector3(actor.Pose.Position),
                Quaternion.Euler(0f, actor.Pose.FacingDegrees, 0f));
            decisionDelaySeconds = enemy.Presentation
                .PresentationDefinition.PostDecisionDelaySeconds;
        }

        private void EndActiveTurn(
            GameplayEnemyRuntimeRegistry.Entry enemy,
            string rationale)
        {
            if (!emergencyCycle.TryEndTurn(
                    enemy.Definition.Id,
                    out TurnEndFailure failure))
            {
                actionController.PresentExternalStatus(
                    $"Enemy turn could not end: {failure}.");
                return;
            }
            actionController.PresentExternalStatus(
                $"{enemy.Definition.Id} ended its turn - {rationale}.");
            sessionPresenter.RefreshModePresentation();
            decisionDelaySeconds = enemy.Presentation
                .PresentationDefinition.PostDecisionDelaySeconds;
        }

        private void RefreshTurnIdentity()
        {
            long turnSequence = session.LastEndedTurn?.Sequence ?? 0L;
            if (string.Equals(
                    observedActiveActorId,
                    session.ActiveActorId,
                    StringComparison.Ordinal)
                && observedTurnSequence == turnSequence)
            {
                return;
            }
            observedActiveActorId = session.ActiveActorId;
            observedTurnSequence = turnSequence;
            decisionDelaySeconds = 0f;
            if (enemies.TryGet(
                    observedActiveActorId,
                    out GameplayEnemyRuntimeRegistry.Entry enemy))
            {
                enemy.AttacksCommittedThisTurn = 0;
                decisionDelaySeconds = enemy.Presentation
                    .PresentationDefinition.PostDecisionDelaySeconds;
            }
        }

        private void AppendDecisionDiagnostic(
            EnemyTacticalDecisionRecord decision)
        {
            dialogue.AppendCombatDiagnostic(
                GameplayCombatDiagnosticFormatter.FormatEnemyDecision(
                    decision));
        }
    }
}
