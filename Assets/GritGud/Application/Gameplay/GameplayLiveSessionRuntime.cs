using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GritGud.Domain.Gameplay;

namespace GritGud.Application.Gameplay
{
    /// <summary>
    /// Permanent live adapter over the semantic runtime. The immutable combat
    /// root remains authoritative; GameplaySession is only its UI-facing
    /// projection while this adapter is bound.
    /// </summary>
    public sealed class GameplayLiveSessionRuntime : IDisposable
    {
        private readonly GameplayLiveCombatProjection projection;
        private readonly GameplaySimulationRuntime runtime;
        private bool disposed;

        public GameplayLiveSessionRuntime(
            GameplaySession gameplay,
            GameplayExecutionIdentity executionIdentity,
            GameplayCombatStateSnapshot initialState,
            GameplayTransitionReducerRegistry reducers,
            GameplayCapabilityRegistry capabilities)
            : this(
                new GameplayLiveCombatProjection(gameplay),
                executionIdentity,
                initialState,
                reducers,
                capabilities)
        {
        }

        public GameplayLiveSessionRuntime(
            GameplayLiveCombatProjection liveProjection,
            GameplayExecutionIdentity executionIdentity,
            GameplayCombatStateSnapshot initialState,
            GameplayTransitionReducerRegistry reducers,
            GameplayCapabilityRegistry capabilities)
        {
            projection = liveProjection ?? throw new ArgumentNullException(
                nameof(liveProjection));
            runtime = new GameplaySimulationRuntime(
                executionIdentity,
                initialState,
                reducers,
                capabilities);
            projection.BindExecutor(ExecutePayload, ExecuteAction);
            projection.Bind(initialState);
            runtime.StateInstalled += ProjectInstalledState;
        }

        public GameplayExecutionIdentity ExecutionIdentity =>
            runtime.ExecutionIdentity;

        public GameplayCombatStateSnapshot InitialState =>
            runtime.InitialState;

        public GameplayCombatStateSnapshot CurrentState =>
            runtime.CurrentState;

        public IReadOnlyList<GameplayTrajectoryStep> Trajectory =>
            runtime.Trajectory;

        public bool HasLastCompletedTurnReplay =>
            runtime.LastCompletedTurnReplayWindow != null;

        public bool HasReplaySinceActorLastTurn(string actorId)
        {
            ThrowIfDisposed();
            return runtime.HasReplaySinceActorLastTurn(actorId);
        }

        public event Action<GameplayDomainEvent> DomainEventPublished
        {
            add => runtime.DomainEventPublished += value;
            remove => runtime.DomainEventPublished -= value;
        }

        public GameplayReductionResult Execute(
            GameplaySemanticTransition transition)
        {
            ThrowIfDisposed();
            RequireProjectionMatchesAuthority();
            return runtime.Execute(transition);
        }

        public GameplayReductionResult ExecutePayload(
            GameplayTransitionPayload payload,
            IEnumerable<GameplayEvidenceRecord> evidence = null)
        {
            ThrowIfDisposed();
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));
            RequireProjectionMatchesAuthority();
            GameplayCombatStateSnapshot state = runtime.CurrentState;
            var validatedEvidence = new List<GameplayEvidenceRecord>();
            foreach (GameplayEvidenceRecord item in evidence
                ?? Array.Empty<GameplayEvidenceRecord>())
            {
                if (item == null)
                    throw new ArgumentException(
                        "Transition evidence cannot contain null entries.",
                        nameof(evidence));
                if (item.WorldRevision > state.Session.Revision)
                    throw new InvalidOperationException(
                        $"Evidence '{item.EvidenceType}' comes from a future revision.");
                validatedEvidence.Add(item);
            }
            var transition = new GameplaySemanticTransition(
                new GameplayTransitionIdentity(
                    checked(state.Session.LastTransitionSequence + 1L),
                    payload.Profile.Capability.ToString(),
                    payload.ActorId,
                    payload.SubjectId),
                state.CanonicalHash,
                payload,
                validatedEvidence);
            return runtime.Execute(transition);
        }

        public GameplayReductionResult ExecuteAction(GameplayActionRecord action)
        {
            ThrowIfDisposed();
            if (action == null) throw new ArgumentNullException(nameof(action));
            RequireProjectionMatchesAuthority();
            return ExecutePayload(projection.CreateActionPayload(
                runtime.CurrentState,
                action));
        }

        public GameplaySimulationBranch Fork(string branchId)
        {
            ThrowIfDisposed();
            RequireProjectionMatchesAuthority();
            return runtime.Fork(branchId);
        }

        public GameplayReproBundle CreateRepro(string label)
        {
            ThrowIfDisposed();
            RequireProjectionMatchesAuthority();
            return runtime.CreateRepro(label);
        }

        public GameplaySemanticReplayTimeline CreateReplayTimeline()
        {
            ThrowIfDisposed();
            RequireProjectionMatchesAuthority();
            return runtime.CreateReplayTimeline();
        }

        public bool TryCreateLastCompletedTurnReplay(
            out GameplaySemanticReplayTimeline replay)
        {
            ThrowIfDisposed();
            RequireProjectionMatchesAuthority();
            return runtime.TryCreateLastCompletedTurnReplay(out replay);
        }

        public bool TryCreateReplaySinceActorLastTurn(
            string actorId,
            out GameplaySemanticReplayTimeline replay,
            out GameplayPlayerAwayReplayInterval interval)
        {
            ThrowIfDisposed();
            RequireProjectionMatchesAuthority();
            return runtime.TryCreateReplaySinceActorLastTurn(
                actorId,
                out replay,
                out interval);
        }

        public async Task<GameplayDecisionExecutionResult>
            ExecuteDecisionAsync(
                GameplayPolicyDecisionRunner runner,
                GameplayObservationSnapshot observation,
                GameplayExecutionDeadlineScope deadlineScope,
                GameplayExecutionLogicalGuard logicalGuard = null,
                CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (runner == null) throw new ArgumentNullException(nameof(runner));
            RequireProjectionMatchesAuthority();
            GameplayDecisionExecutionResult result = await runner.ExecuteAsync(
                    runtime,
                    observation,
                    deadlineScope,
                    logicalGuard,
                    cancellationToken);
            RequireProjectionMatchesAuthority();
            return result;
        }

        public void Dispose()
        {
            if (disposed) return;
            runtime.StateInstalled -= ProjectInstalledState;
            disposed = true;
        }

        private void ProjectInstalledState(GameplayReductionResult reduction)
            => projection.Install(reduction);

        private void RequireProjectionMatchesAuthority()
        {
            GameplayCombatStateSnapshot projected = projection.Capture();
            GameplayCombatStateSnapshot authoritative = runtime.CurrentState;
            if (!string.Equals(
                    projected.CanonicalHash,
                    authoritative.CanonicalHash,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "The live gameplay projection diverged from the immutable canonical root.");
            }
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(
                    nameof(GameplayLiveSessionRuntime));
        }
    }
}
