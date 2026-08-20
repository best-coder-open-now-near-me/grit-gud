using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    /// <summary>
    /// Schedules exploration observation and patrol transitions through the
    /// same permanent candidate runner used headlessly. This class owns only
    /// scan cadence, combat-entry presentation, and route playback.
    /// </summary>
    internal sealed class GameplayEnemyExplorationCoordinator : IDisposable
    {
        private enum ExplorationDecisionKind
        {
            Observation,
            Patrol,
        }

        private readonly GameplaySession session;
        private readonly GameplayEnemyRuntimeRegistry enemies;
        private readonly GameplaySessionPresenter sessionPresenter;
        private readonly GameplayActionController actionController;
        private readonly GameplayDialogueLog dialogue;
        private readonly Func<IReadOnlyList<string>, bool> beginEncounter;
        private readonly GameplayTacticalTransitionPresenter tacticalTransition;
        private readonly float detectionIntervalSeconds;
        private readonly List<GameplayEnemyRuntimeRegistry.Entry> scan =
            new List<GameplayEnemyRuntimeRegistry.Entry>();
        private readonly CancellationTokenSource lifetime =
            new CancellationTokenSource();
        private GameplayLiveSessionRuntime runtime;
        private GameplayPolicyDecisionRunner observationRunner;
        private GameplayPolicyDecisionRunner patrolRunner;
        private Task<GameplayDecisionExecutionResult> pendingDecision;
        private GameplayEnemyRuntimeRegistry.Entry currentEnemy;
        private GameplayEnemyRuntimeRegistry.Entry playbackEnemy;
        private ExplorationDecisionKind pendingKind;
        private int scanIndex;
        private float detectionDelaySeconds;
        private IReadOnlyList<string> pendingEncounterScope;
        private string pendingEncounterMessage = string.Empty;
        private bool failureLatched;
        private bool disposed;
        private int staleRetryCount;

        public GameplayEnemyExplorationCoordinator(
            GameplaySession session,
            GameplayEnemyRuntimeRegistry enemies,
            GameplaySessionPresenter sessionPresenter,
            GameplayActionController actionController,
            GameplayDialogueLog dialogue,
            Func<IReadOnlyList<string>, bool> beginEncounter,
            GameplayTacticalTransitionPresenter tacticalTransition,
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
            this.dialogue = dialogue ?? throw new ArgumentNullException(
                nameof(dialogue));
            this.beginEncounter = beginEncounter
                ?? throw new ArgumentNullException(nameof(beginEncounter));
            this.tacticalTransition = tacticalTransition
                ?? throw new ArgumentNullException(nameof(tacticalTransition));
            if (float.IsNaN(detectionIntervalSeconds)
                || float.IsInfinity(detectionIntervalSeconds)
                || detectionIntervalSeconds <= 0f)
                throw new ArgumentOutOfRangeException(
                    nameof(detectionIntervalSeconds));
            this.detectionIntervalSeconds = detectionIntervalSeconds;
        }

        public void BindSemanticRuntime(
            GameplayLiveSessionRuntime liveRuntime,
            GameplayPolicyDecisionRunner observeRunner,
            GameplayPolicyDecisionRunner authoredPatrolRunner)
        {
            if (runtime != null)
                throw new InvalidOperationException(
                    "Exploration semantic runtime is already bound.");
            runtime = liveRuntime ?? throw new ArgumentNullException(
                nameof(liveRuntime));
            observationRunner = observeRunner ?? throw new ArgumentNullException(
                nameof(observeRunner));
            patrolRunner = authoredPatrolRunner ?? throw new ArgumentNullException(
                nameof(authoredPatrolRunner));
        }

        public void Tick(float unscaledDeltaTime)
        {
            if (disposed || runtime == null || failureLatched) return;
            if (tacticalTransition.CombatEntryReady)
            {
                IReadOnlyList<string> scope = pendingEncounterScope;
                pendingEncounterScope = null;
                if (scope != null && beginEncounter(scope))
                {
                    actionController.PresentExternalStatus(
                        pendingEncounterMessage + " Combat initiated.");
                    sessionPresenter.RefreshModePresentation();
                }
                pendingEncounterMessage = string.Empty;
                tacticalTransition.CompleteCombatEntry();
                ClearScan();
                return;
            }
            if (pendingEncounterScope != null) return;

            if (playbackEnemy != null)
            {
                TickPatrolPlayback(unscaledDeltaTime);
                return;
            }
            if (pendingDecision != null)
            {
                if (!pendingDecision.IsCompleted) return;
                CompletePendingDecision();
                return;
            }
            if (scan.Count > 0)
            {
                StartNextObservation();
                return;
            }

            detectionDelaySeconds -= Math.Max(0f, unscaledDeltaTime);
            if (detectionDelaySeconds > 0f) return;
            detectionDelaySeconds = detectionIntervalSeconds;
            BeginScan();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            lifetime.Cancel();
            if (pendingDecision != null)
                _ = pendingDecision.ContinueWith(
                    completed => _ = completed.Exception,
                    CancellationToken.None,
                    TaskContinuationOptions.OnlyOnFaulted
                        | TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
            lifetime.Dispose();
            ClearScan();
        }

        private void BeginScan()
        {
            scan.Clear();
            foreach (GameplayEnemyRuntimeRegistry.Entry enemy in
                enemies.OrderedEntries)
                if (!session.IsActorIncapacitated(enemy.Definition.Id))
                    scan.Add(enemy);
            scanIndex = 0;
            StartNextObservation();
        }

        private void StartNextObservation()
        {
            while (scanIndex < scan.Count
                && session.IsActorIncapacitated(
                    scan[scanIndex].Definition.Id))
                scanIndex++;
            if (scanIndex >= scan.Count)
            {
                ClearScan();
                return;
            }
            currentEnemy = scan[scanIndex];
            StartDecision(
                currentEnemy.Definition.Id,
                observationRunner,
                ExplorationDecisionKind.Observation);
        }

        private void StartPatrol()
        {
            StartDecision(
                currentEnemy.Definition.Id,
                patrolRunner,
                ExplorationDecisionKind.Patrol);
        }

        private void StartDecision(
            string actorId,
            GameplayPolicyDecisionRunner runner,
            ExplorationDecisionKind kind)
        {
            GameplayCombatStateSnapshot state = runtime.CurrentState;
            var scope = new GameplayExecutionDeadlineScope();
            scope.BeginTurn();
            pendingKind = kind;
            pendingDecision = runtime.ExecuteDecisionAsync(
                runner,
                GameplayObservationSnapshot.FullState(actorId, state),
                scope,
                logicalGuard: null,
                lifetime.Token);
        }

        private void CompletePendingDecision()
        {
            Task<GameplayDecisionExecutionResult> completed = pendingDecision;
            pendingDecision = null;
            try
            {
                GameplayDecisionExecutionResult result = completed
                    .GetAwaiter()
                    .GetResult();
                staleRetryCount = 0;
                AppendDecisionDiagnostic(result);
                object record = RequireSemanticRecord(result.Reduction);
                if (pendingKind == ExplorationDecisionKind.Observation)
                    CompleteObservation(record);
                else
                    CompletePatrol(record);
            }
            catch (GameplayDecisionFailureException failure)
            {
                if (failure.Kind == GameplayDecisionFailureKind.Cancelled
                    && disposed)
                    return;
                if (pendingKind == ExplorationDecisionKind.Patrol
                    && failure.Kind
                        == GameplayDecisionFailureKind.NoLegalCandidate)
                {
                    FinishEnemy();
                    return;
                }
                if (failure.Kind
                        == GameplayDecisionFailureKind.StaleDecisionState
                    && staleRetryCount < 8
                    && currentEnemy != null)
                {
                    staleRetryCount++;
                    StartDecision(
                        currentEnemy.Definition.Id,
                        pendingKind == ExplorationDecisionKind.Observation
                            ? observationRunner
                            : patrolRunner,
                        pendingKind);
                    return;
                }
                FailClosed(failure.Kind.ToString(), failure.Message,
                    failure.Diagnostic.ActiveStage?.ToString());
            }
            catch (Exception exception)
            {
                FailClosed(
                    exception.GetType().Name,
                    exception.Message,
                    stage: null);
            }
        }

        private void CompleteObservation(object semanticRecord)
        {
            if (!(semanticRecord is EnemyAwarenessTransitionRecord transition))
                throw new InvalidOperationException(
                    "Exploration observation produced a non-awareness record.");
            if (transition.Resulting.State == EncounterAwarenessState.Alert)
            {
                pendingEncounterScope = session.CreateDetectionEncounterScope(
                    currentEnemy.Definition.Id,
                    transition.Resulting.LastKnownHostileId);
                pendingEncounterMessage = $"{currentEnemy.Definition.Id} detected "
                    + $"{transition.Resulting.LastKnownHostileId}.";
                tacticalTransition.BeginCombatEntry(
                    currentEnemy.Definition.Id,
                    transition.Resulting.LastKnownHostileId);
                ClearScan();
                return;
            }

            PatrolRouteDefinition patrol = currentEnemy.Definition.Combat
                .EnemyBehavior.PatrolRoute;
            if (transition.Resulting.State == EncounterAwarenessState.Unaware
                && patrol != null
                && patrol.GetNextWaypointIndex(
                    transition.Resulting.PatrolWaypointIndex)
                    != transition.Resulting.PatrolWaypointIndex)
            {
                StartPatrol();
                return;
            }
            FinishEnemy();
        }

        private void CompletePatrol(object semanticRecord)
        {
            if (!(semanticRecord is PatrolAdvanceRecord patrol))
                throw new InvalidOperationException(
                    "Exploration patrol produced a non-patrol record.");
            playbackEnemy = currentEnemy;
            currentEnemy = null;
            playbackEnemy.Playback.Begin(patrol.Route);
            actionController.PresentExternalStatus(
                $"{patrol.ActorId} is patrolling.");
        }

        private void TickPatrolPlayback(float deltaTime)
        {
            if (!playbackEnemy.Playback.Tick(deltaTime)) return;
            GameplayActorSnapshot actor = runtime.CurrentState.Session.GetActor(
                playbackEnemy.Definition.Id);
            playbackEnemy.View.Transform.SetPositionAndRotation(
                MovementRouteSampling.ToVector3(actor.Pose.Position),
                Quaternion.Euler(0f, actor.Pose.FacingDegrees, 0f));
            playbackEnemy = null;
            FinishEnemy();
        }

        private void FinishEnemy()
        {
            currentEnemy = null;
            staleRetryCount = 0;
            scanIndex++;
            if (scanIndex >= scan.Count) ClearScan();
        }

        private void ClearScan()
        {
            scan.Clear();
            scanIndex = 0;
            currentEnemy = null;
            playbackEnemy = null;
            staleRetryCount = 0;
        }

        private void FailClosed(string kind, string message, string stage)
        {
            failureLatched = true;
            ClearScan();
            actionController.PresentExternalStatus(
                "Exploration decision stopped: " + kind + ".");
            AppendFailureDiagnostic(kind, message, stage);
        }

        private void AppendDecisionDiagnostic(
            GameplayDecisionExecutionResult result)
        {
            GameplayDecisionDiagnostic diagnostic = result.Diagnostic;
            dialogue.AppendCombatDiagnostic(
                "EXPLORATION POLICY DECISION",
                "ACTOR - " + diagnostic.ActorId,
                "KIND - " + pendingKind,
                "CANDIDATES - " + diagnostic.CandidateIds.Count,
                "LEGAL - " + diagnostic.LegalCandidateIds.Count,
                "SELECTED - " + diagnostic.SelectedCandidateId,
                "STATE - " + diagnostic.StateHash);
        }

        private void AppendFailureDiagnostic(
            string kind,
            string message,
            string stage)
        {
            dialogue.AppendCombatDiagnostic(
                "EXPLORATION DECISION STOPPED",
                "KIND - " + kind,
                "STAGE - " + (stage ?? "unknown"),
                "MESSAGE - " + message);
        }

        private static object RequireSemanticRecord(
            GameplayReductionResult reduction)
        {
            object record = null;
            foreach (GameplayDomainEvent domainEvent in reduction.DomainEvents)
                if (domainEvent is GameplayTransitionReducedEvent reduced)
                {
                    if (record != null)
                        throw new InvalidOperationException(
                            "Exploration decision produced multiple semantic records.");
                    record = reduced.SemanticRecord;
                }
            return record ?? throw new InvalidOperationException(
                "Exploration decision produced no semantic record.");
        }
    }
}
