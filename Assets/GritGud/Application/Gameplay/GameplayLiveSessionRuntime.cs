using System;
using System.Collections.Generic;

namespace GritGud.Application.Gameplay
{
    /// <summary>
    /// Permanent live adapter over the semantic runtime. The immutable combat
    /// root remains authoritative; GameplaySession is only its UI-facing
    /// projection while this adapter is bound.
    /// </summary>
    public sealed class GameplayLiveSessionRuntime : IDisposable
    {
        private readonly GameplaySession session;
        private readonly GameplaySimulationRuntime runtime;
        private bool disposed;

        public GameplayLiveSessionRuntime(
            GameplaySession gameplay,
            GameplayExecutionIdentity executionIdentity,
            GameplayCombatStateSnapshot initialState,
            GameplayTransitionReducerRegistry reducers,
            GameplayCapabilityRegistry capabilities)
        {
            session = gameplay ?? throw new ArgumentNullException(
                nameof(gameplay));
            runtime = new GameplaySimulationRuntime(
                executionIdentity,
                initialState,
                reducers,
                capabilities);
            session.BindCanonicalProjection(initialState.Session);
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

        public void Dispose()
        {
            if (disposed) return;
            runtime.StateInstalled -= ProjectInstalledState;
            disposed = true;
        }

        private void ProjectInstalledState(GameplayReductionResult reduction)
        {
            GameplayTransitionReducedEvent semanticEvent = null;
            foreach (GameplayDomainEvent domainEvent in reduction.DomainEvents)
            {
                if (!(domainEvent is GameplayTransitionReducedEvent reduced))
                    continue;
                if (semanticEvent != null)
                    throw new InvalidOperationException(
                        "A semantic transition produced more than one authoritative record.");
                semanticEvent = reduced;
            }
            if (semanticEvent == null)
                throw new InvalidOperationException(
                    "A semantic transition produced no authoritative record.");
            session.InstallCanonicalProjection(
                reduction.Resulting.Session,
                semanticEvent.SemanticRecord);
        }

        private void RequireProjectionMatchesAuthority()
        {
            GameplayCombatStateSnapshot projected =
                GameplayCombatStateCapture.Capture(session);
            GameplayCombatStateSnapshot authoritative =
                new GameplayCombatStateSnapshot(runtime.CurrentState.Session);
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
