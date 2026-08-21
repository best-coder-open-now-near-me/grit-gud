using System;
using System.Threading;
using System.Threading.Tasks;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    /// <summary>
    /// Marshals the sole mutating stage of a live policy decision back to the
    /// Unity owner thread. Candidate construction, evidence, scoring,
    /// preparation, and pure reduction remain on the cancellable worker.
    /// </summary>
    internal sealed class GameplaySynchronizationContextRuntimeInstallationBoundary :
        IGameplayRuntimeInstallationBoundary
    {
        private readonly SynchronizationContext ownerContext;
        private readonly int ownerThreadId;

        public GameplaySynchronizationContextRuntimeInstallationBoundary(
            SynchronizationContext synchronizationContext)
        {
            ownerContext = synchronizationContext;
            ownerThreadId = Thread.CurrentThread.ManagedThreadId;
        }

        public Task<GameplayReductionResult> InstallAsync(
            GameplaySimulationRuntime runtime,
            GameplaySemanticTransition transition,
            GameplayReductionResult reduction,
            CancellationToken cancellationToken)
        {
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));
            if (transition == null)
                throw new ArgumentNullException(nameof(transition));
            if (reduction == null)
                throw new ArgumentNullException(nameof(reduction));
            cancellationToken.ThrowIfCancellationRequested();
            if (Thread.CurrentThread.ManagedThreadId == ownerThreadId)
                return Task.FromResult(runtime.InstallPreparedReduction(
                    transition,
                    reduction));
            if (ownerContext == null)
                throw new InvalidOperationException(
                    "Live decision installation requires the Unity synchronization context.");

            var completion = new TaskCompletionSource<GameplayReductionResult>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            CancellationTokenRegistration cancellation = cancellationToken
                .Register(() => completion.TrySetCanceled(cancellationToken));
            _ = completion.Task.ContinueWith(
                _ => cancellation.Dispose(),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            ownerContext.Post(
                _ =>
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        completion.TrySetCanceled(cancellationToken);
                        return;
                    }
                    try
                    {
                        completion.TrySetResult(
                            runtime.InstallPreparedReduction(
                                transition,
                                reduction));
                    }
                    catch (Exception exception)
                    {
                        completion.TrySetException(exception);
                    }
                },
                null);
            return completion.Task;
        }
    }

    /// <summary>
    /// Unity presentation adapter over the permanent policy-neutral runner.
    /// It never chooses or mutates gameplay independently; it only schedules a
    /// decision and presents the reducer-owned semantic record after install.
    /// </summary>
    internal sealed class GameplayEnemyCombatTurnExecutor : IDisposable
    {
        private readonly GameplaySession session;
        private readonly GameplayLiveSessionRuntime runtime;
        private readonly GameplayPolicyDecisionRunner runner;
        private readonly GameplayEnemyRuntimeRegistry enemies;
        private readonly GameplaySessionPresenter sessionPresenter;
        private readonly GameplayActionController actionController;
        private readonly GameplayAttackController attackController;
        private readonly GameplayProjectileController projectileController;
        private readonly GameplayDisplacementController displacementController;
        private readonly GameplayDroneController drones;
        private readonly GameplayDialogueLog dialogue;
        private GameplayExecutionDeadlineScope deadlineScope;
        private readonly GameplayExecutionLogicalGuard logicalGuard;
        private readonly CancellationTokenSource lifetime =
            new CancellationTokenSource();
        private CancellationTokenSource decisionCancellation;
        private Task<GameplayDecisionExecutionResult> pendingDecision;
        private string observedActiveActorId = string.Empty;
        private long observedTurnSequence = -1L;
        private float decisionDelaySeconds;
        private bool failureLatched;
        private bool disposed;
        private bool paused;
        private bool decisionCancelledForPause;

        public GameplayEnemyCombatTurnExecutor(
            GameplaySession session,
            GameplayLiveSessionRuntime liveRuntime,
            GameplayPolicyDecisionRunner decisionRunner,
            GameplayEnemyRuntimeRegistry enemies,
            GameplaySessionPresenter sessionPresenter,
            GameplayActionController actionController,
            GameplayAttackController attackController,
            GameplayProjectileController projectileController,
            GameplayDisplacementController displacementController,
            GameplayDroneController droneController,
            GameplayDialogueLog dialogue)
        {
            this.session = session ?? throw new ArgumentNullException(
                nameof(session));
            runtime = liveRuntime ?? throw new ArgumentNullException(
                nameof(liveRuntime));
            runner = decisionRunner ?? throw new ArgumentNullException(
                nameof(decisionRunner));
            this.enemies = enemies ?? throw new ArgumentNullException(
                nameof(enemies));
            this.sessionPresenter = sessionPresenter
                ?? throw new ArgumentNullException(nameof(sessionPresenter));
            this.actionController = actionController
                ?? throw new ArgumentNullException(nameof(actionController));
            this.attackController = attackController
                ?? throw new ArgumentNullException(nameof(attackController));
            this.projectileController = projectileController
                ?? throw new ArgumentNullException(nameof(projectileController));
            this.displacementController = displacementController
                ?? throw new ArgumentNullException(nameof(displacementController));
            drones = droneController ?? throw new ArgumentNullException(
                nameof(droneController));
            this.dialogue = dialogue ?? throw new ArgumentNullException(
                nameof(dialogue));
            logicalGuard = new GameplayExecutionLogicalGuard(
                runtime.CurrentState);
        }

        public void Tick(float deltaTime, float unscaledDeltaTime)
        {
            if (disposed || paused) return;
            if (pendingDecision != null)
            {
                if (!pendingDecision.IsCompleted) return;
                CompletePendingDecision();
                return;
            }

            RefreshTurnIdentity();
            if (!enemies.TryGet(
                    session.ActiveActorId,
                    out GameplayEnemyRuntimeRegistry.Entry activeEnemy))
                return;

            if (activeEnemy.Playback.IsPlaying)
            {
                TickMovement(activeEnemy, deltaTime);
                return;
            }
            if (failureLatched
                || session.Operation != GameplaySessionOperation.None)
                return;

            decisionDelaySeconds = Math.Max(
                0f,
                decisionDelaySeconds - Math.Max(0f, unscaledDeltaTime));
            if (decisionDelaySeconds > 0f) return;
            StartDecision(activeEnemy.Definition.Id);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            decisionCancellation?.Cancel();
            lifetime.Cancel();
            if (pendingDecision != null)
                _ = pendingDecision.ContinueWith(
                    completed => _ = completed.Exception,
                    CancellationToken.None,
                    TaskContinuationOptions.OnlyOnFaulted
                        | TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
            decisionCancellation?.Dispose();
            decisionCancellation = null;
            lifetime.Dispose();
        }

        public void SetPaused(bool isPaused)
        {
            if (disposed || paused == isPaused) return;
            paused = isPaused;
            if (paused && pendingDecision != null)
            {
                decisionCancelledForPause = true;
                decisionCancellation?.Cancel();
            }
            if (!paused)
            {
                deadlineScope = null;
                observedActiveActorId = string.Empty;
                observedTurnSequence = -1L;
                decisionDelaySeconds = 0f;
            }
        }

        public void ResetBattleScope()
        {
            if (pendingDecision != null || disposed) return;
            deadlineScope = null;
            observedActiveActorId = string.Empty;
            observedTurnSequence = -1L;
            failureLatched = false;
        }

        private void StartDecision(string actorId)
        {
            GameplayCombatStateSnapshot state = runtime.CurrentState;
            decisionCancellation?.Dispose();
            decisionCancellation = CancellationTokenSource
                .CreateLinkedTokenSource(lifetime.Token);
            pendingDecision = runtime.ExecuteDecisionAsync(
                runner,
                GameplayObservationSnapshot.FullState(actorId, state),
                deadlineScope,
                logicalGuard,
                decisionCancellation.Token);
        }

        private void CompletePendingDecision()
        {
            Task<GameplayDecisionExecutionResult> completed = pendingDecision;
            pendingDecision = null;
            CancellationTokenSource completedCancellation =
                decisionCancellation;
            decisionCancellation = null;
            bool cancelledForPause = decisionCancelledForPause;
            decisionCancelledForPause = false;
            try
            {
                GameplayDecisionExecutionResult result = completed
                    .GetAwaiter()
                    .GetResult();
                AppendDecisionDiagnostic(result);
                PresentInstalledRecord(result);
            }
            catch (GameplayDecisionFailureException failure)
            {
                if (failure.Kind == GameplayDecisionFailureKind.Cancelled
                    && cancelledForPause)
                {
                    decisionDelaySeconds = 0f;
                    return;
                }
                if (failure.Kind == GameplayDecisionFailureKind.Cancelled
                    && disposed)
                    return;
                failureLatched = true;
                PresentFailure(failure);
            }
            catch (Exception exception)
            {
                failureLatched = true;
                actionController.PresentExternalStatus(
                    "Enemy decision installation failed: "
                    + exception.Message);
                dialogue.AppendCombatDiagnostic(
                    "ENEMY DECISION FAILURE",
                    exception.GetType().Name + " - " + exception.Message);
            }
            finally
            {
                completedCancellation?.Dispose();
            }
        }

        private void PresentInstalledRecord(
            GameplayDecisionExecutionResult result)
        {
            object record = RequireSemanticRecord(result.Reduction);
            string actorId = result.Transition.Payload.ActorId;
            if (!enemies.TryGet(
                    actorId,
                    out GameplayEnemyRuntimeRegistry.Entry enemy))
                throw new InvalidOperationException(
                    $"Installed enemy decision belongs to unknown actor '{actorId}'.");

            switch (record)
            {
                case MovementRouteRecord route:
                    enemy.Playback.Begin(route);
                    actionController.PresentExternalStatus(
                        $"{actorId} is repositioning.");
                    return;
                case TurnEndRecord turn:
                    actionController.PresentExternalStatus(
                        $"{turn.EndingActorId} ended its turn.");
                    sessionPresenter.RefreshModePresentation();
                    decisionDelaySeconds = enemy.Presentation
                        .PresentationDefinition.PostDecisionDelaySeconds;
                    return;
                case GameplayActionRecord action:
                    PresentAction(enemy, action);
                    return;
                case ProjectileAdvanceRecord advance:
                    projectileController.PresentResolvedAdvance(advance);
                    actionController.PresentExternalStatus(
                        DescribeProjectileAdvance(advance));
                    decisionDelaySeconds = enemy.Presentation
                        .PresentationDefinition.PostAttackDelaySeconds;
                    return;
                case DroneMoveRecord move:
                    drones.RefreshAuthoritativePresentation();
                    actionController.PresentExternalStatus(
                        $"{move.ControllerActorId} moved {move.DroneId}.");
                    decisionDelaySeconds = enemy.Presentation
                        .PresentationDefinition.PostDecisionDelaySeconds;
                    return;
                case DroneAttackRecord attack:
                    drones.RefreshAuthoritativePresentation();
                    actionController.PresentExternalStatus(
                        $"{attack.DroneId} attacked {attack.TargetId}.");
                    decisionDelaySeconds = enemy.Presentation
                        .PresentationDefinition.PostAttackDelaySeconds;
                    return;
                case ActorDroneAttackRecord attack:
                    drones.RefreshAuthoritativePresentation();
                    actionController.PresentExternalStatus(
                        attack.Hit
                            ? $"{attack.AttackerId} hit {attack.DroneId}."
                            : $"{attack.AttackerId} missed {attack.DroneId}.");
                    decisionDelaySeconds = enemy.Presentation
                        .PresentationDefinition.PostAttackDelaySeconds;
                    return;
                case StanceChangeRecord _:
                case EnemyAwarenessTransitionRecord _:
                case PatrolAdvanceRecord _:
                    actionController.PresentExternalStatus(
                        $"{actorId} completed {result.Transition.Profile.Capability}.");
                    decisionDelaySeconds = enemy.Presentation
                        .PresentationDefinition.PostDecisionDelaySeconds;
                    return;
                default:
                    actionController.PresentExternalStatus(
                        $"{actorId} completed {result.Transition.Profile.Capability}.");
                    decisionDelaySeconds = enemy.Presentation
                        .PresentationDefinition.PostDecisionDelaySeconds;
                    return;
            }
        }

        private void PresentAction(
            GameplayEnemyRuntimeRegistry.Entry enemy,
            GameplayActionRecord action)
        {
            string status = $"{action.Request.ActorId} used "
                + action.Request.ActionId + ".";
            bool attackPresented = false;
            bool projectilePresented = false;
            foreach (GameplayActionOutcome outcome in action.Outcomes)
            {
                switch (outcome)
                {
                    case AttackResolvedActionOutcome attack:
                        if (!attackPresented)
                        {
                            attackController.PresentResolvedAction(action);
                            attackPresented = true;
                        }
                        status = attack.Attack.Hit
                            ? $"{attack.Attack.AttackerId} hit {attack.Attack.TargetId}."
                            : $"{attack.Attack.AttackerId} missed {attack.Attack.TargetId}.";
                        break;
                    case ProjectileLaunchedActionOutcome launch:
                        if (!projectilePresented)
                        {
                            projectileController.PresentResolvedAction(
                                action,
                                enemy.Presentation.ProjectileLaunchOrigin);
                            projectilePresented = true;
                        }
                        status = $"{launch.Launch.AttackerId} launched "
                            + launch.Launch.ProjectileId + ".";
                        break;
                    case DisplacementActionOutcome displacement:
                        displacementController.PresentResolved(
                            displacement.Displacement);
                        status = $"{action.Request.ActorId} displaced "
                            + displacement.Displacement.Request.SubjectId + ".";
                        break;
                    case ThrownExplosiveActionOutcome thrown:
                        status = $"{thrown.Record.ThrowerId} threw "
                            + thrown.Record.Definition.Id + ".";
                        break;
                }
            }
            if (!attackPresented
                && !projectilePresented
                && GameplayCombatDiagnosticFormatter.TryFormatAction(
                    action,
                    out GameplayDiagnosticProjection diagnostic))
                dialogue.AppendCombatDiagnostic(diagnostic);
            actionController.PresentExternalStatus(status);
            decisionDelaySeconds = attackPresented || projectilePresented
                ? enemy.Presentation.PresentationDefinition
                    .PostAttackDelaySeconds
                : enemy.Presentation.PresentationDefinition
                    .PostDecisionDelaySeconds;
        }

        private void TickMovement(
            GameplayEnemyRuntimeRegistry.Entry enemy,
            float deltaTime)
        {
            if (!enemy.Playback.Tick(deltaTime)) return;
            GameplayActorSnapshot actor = runtime.CurrentState.Session.GetActor(
                enemy.Definition.Id);
            enemy.View.Transform.SetPositionAndRotation(
                MovementRouteSampling.ToVector3(actor.Pose.Position),
                Quaternion.Euler(0f, actor.Pose.FacingDegrees, 0f));
            decisionDelaySeconds = enemy.Presentation
                .PresentationDefinition.PostDecisionDelaySeconds;
        }

        private void RefreshTurnIdentity()
        {
            GameplaySessionStateSnapshot state = runtime.CurrentState.Session;
            if (string.Equals(
                    observedActiveActorId,
                    state.ActiveActorId,
                    StringComparison.Ordinal)
                && observedTurnSequence == state.LastTurnSequence)
                return;

            observedActiveActorId = state.ActiveActorId;
            observedTurnSequence = state.LastTurnSequence;
            failureLatched = false;
            decisionDelaySeconds = 0f;
            if (enemies.TryGet(
                    observedActiveActorId,
                    out GameplayEnemyRuntimeRegistry.Entry enemy))
            {
                deadlineScope ??= new GameplayExecutionDeadlineScope();
                deadlineScope.BeginTurn();
                logicalGuard.BeginTurn(
                    observedActiveActorId,
                    runtime.CurrentState);
                decisionDelaySeconds = enemy.Presentation
                    .PresentationDefinition.PostDecisionDelaySeconds;
            }
        }

        private void AppendDecisionDiagnostic(
            GameplayDecisionExecutionResult result)
        {
            GameplayDecisionDiagnostic diagnostic = result.Diagnostic;
            dialogue.AppendCombatDiagnostic(
                "ENEMY POLICY DECISION",
                "ACTOR - " + diagnostic.ActorId,
                "CANDIDATES - " + diagnostic.CandidateIds.Count,
                "LEGAL - " + diagnostic.LegalCandidateIds.Count,
                "SELECTED - " + diagnostic.SelectedCandidateId,
                "SCORE - " + GameplayNumericPolicy.FormatCanonical(
                    result.Selection.Value),
                "STATE - " + diagnostic.StateHash);
        }

        private void PresentFailure(GameplayDecisionFailureException failure)
        {
            GameplayDecisionDiagnostic diagnostic = failure.Diagnostic;
            string stage = diagnostic.ActiveStage?.ToString() ?? "logical-guard";
            actionController.PresentExternalStatus(
                $"Enemy decision stopped: {failure.Kind} during {stage}.");
            dialogue.AppendCombatDiagnostic(
                "ENEMY DECISION STOPPED",
                "KIND - " + failure.Kind,
                "STAGE - " + stage,
                "ACTOR - " + diagnostic.ActorId,
                "SELECTED - " + (diagnostic.SelectedCandidateId.Length == 0
                    ? "none"
                    : diagnostic.SelectedCandidateId),
                "STATE - " + diagnostic.StateHash,
                "MESSAGE - " + failure.Message);
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
                            "A decision reduction produced multiple semantic records.");
                    record = reduced.SemanticRecord;
                }
            return record ?? throw new InvalidOperationException(
                "A decision reduction produced no semantic record.");
        }

        private static string DescribeProjectileAdvance(
            ProjectileAdvanceRecord advance) => advance.Resulting.Impact == null
                ? $"{advance.ProjectileId} advanced."
                : $"{advance.ProjectileId} impacted "
                    + advance.Resulting.Impact.HitEntityId + ".";
    }
}
