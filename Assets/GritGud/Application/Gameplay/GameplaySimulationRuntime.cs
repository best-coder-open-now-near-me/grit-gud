using System;
using System.Collections.Generic;

namespace GritGud.Application.Gameplay
{
    /// <summary>
    /// Owns the immutable authoritative root used by live semantic execution.
    /// Presentation observes domain events only after the new root is installed.
    /// </summary>
    public sealed class GameplaySimulationRuntime
    {
        private readonly GameplayTransitionReducerRegistry reducers;
        private readonly GameplayCapabilityRegistry capabilities;
        private readonly GameplayAtomicCombatStateStore stateStore;
        private readonly List<GameplayTrajectoryStep> trajectory =
            new List<GameplayTrajectoryStep>();

        public GameplaySimulationRuntime(
            GameplayExecutionIdentity executionIdentity,
            GameplayCombatStateSnapshot initialState,
            GameplayTransitionReducerRegistry reducerRegistry,
            GameplayCapabilityRegistry capabilityRegistry)
        {
            ExecutionIdentity = executionIdentity
                ?? throw new ArgumentNullException(nameof(executionIdentity));
            if (initialState == null)
                throw new ArgumentNullException(nameof(initialState));
            if (!ExecutionIdentity.Run.HasSameIdentity(
                    initialState.Session.RunIdentity))
                throw new ArgumentException(
                    "Simulation execution and canonical state run identities differ.",
                    nameof(initialState));
            reducers = reducerRegistry ?? throw new ArgumentNullException(
                nameof(reducerRegistry));
            capabilities = capabilityRegistry ?? throw new ArgumentNullException(
                nameof(capabilityRegistry));
            InitialState = initialState;
            stateStore = new GameplayAtomicCombatStateStore(initialState);
            stateStore.DomainEventPublished += PublishDomainEvent;
        }

        public GameplayExecutionIdentity ExecutionIdentity { get; }
        public GameplayCombatStateSnapshot InitialState { get; }
        public GameplayCombatStateSnapshot CurrentState => stateStore.Current;
        public IReadOnlyList<GameplayTrajectoryStep> Trajectory =>
            trajectory.AsReadOnly();

        public event Action<GameplayDomainEvent> DomainEventPublished;

        public GameplayReductionResult Execute(
            GameplaySemanticTransition transition)
        {
            if (transition == null)
                throw new ArgumentNullException(nameof(transition));
            capabilities.RequireCompleteRoute(transition.Profile);
            GameplayReductionResult reduction = reducers.Reduce(
                stateStore.Current,
                transition);
            var eventTypes = new List<string>(reduction.DomainEvents.Count);
            foreach (GameplayDomainEvent domainEvent in reduction.DomainEvents)
                eventTypes.Add(domainEvent.EventType);
            var step = new GameplayTrajectoryStep(
                transition,
                reduction.Resulting.CanonicalHash,
                eventTypes);
            stateStore.Install(
                reduction,
                _ => trajectory.Add(step));
            return reduction;
        }

        public GameplaySimulationBranch Fork(string branchId) =>
            new GameplaySimulationBranch(
                branchId,
                CurrentState,
                reducers);

        public GameplayReproBundle CreateRepro(string label) =>
            new GameplayReproBundle(
                ExecutionIdentity,
                InitialState,
                trajectory,
                label);

        private void PublishDomainEvent(GameplayDomainEvent domainEvent) =>
            DomainEventPublished?.Invoke(domainEvent);
    }
}
