using System;
using System.Collections.Generic;

namespace GritGud.Application.Gameplay
{
    public sealed class GameplayStaleDecisionStateException :
        InvalidOperationException
    {
        public GameplayStaleDecisionStateException(
            string preparedStateHash,
            string currentStateHash)
            : base("Prepared decision state is stale and cannot be installed.")
        {
            PreparedStateHash = preparedStateHash ?? string.Empty;
            CurrentStateHash = currentStateHash ?? string.Empty;
        }

        public string PreparedStateHash { get; }
        public string CurrentStateHash { get; }
    }

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
        private readonly List<GameplayReplayWindow> completedTurnReplayWindows =
            new List<GameplayReplayWindow>();
        private readonly IReadOnlyList<GameplayReplayWindow>
            readOnlyCompletedTurnReplayWindows;
        private GameplayCombatStateSnapshot openReplayWindowInitialState;
        private int openReplayWindowStartTrajectoryIndex;

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
            openReplayWindowInitialState = initialState;
            openReplayWindowStartTrajectoryIndex = 0;
            readOnlyCompletedTurnReplayWindows =
                completedTurnReplayWindows.AsReadOnly();
        }

        public GameplayExecutionIdentity ExecutionIdentity { get; }
        public GameplayCombatStateSnapshot InitialState { get; }
        public GameplayCombatStateSnapshot CurrentState => stateStore.Current;
        public IReadOnlyList<GameplayTrajectoryStep> Trajectory =>
            trajectory.AsReadOnly();

        public IReadOnlyList<GameplayReplayWindow> CompletedTurnReplayWindows =>
            readOnlyCompletedTurnReplayWindows;

        public GameplayReplayWindow LastCompletedTurnReplayWindow =>
            completedTurnReplayWindows.Count == 0
                ? null
                : completedTurnReplayWindows[
                    completedTurnReplayWindows.Count - 1];

        public event Action<GameplayDomainEvent> DomainEventPublished;

        public event Action<GameplayReductionResult> StateInstalled;

        public GameplayReductionResult Execute(
            GameplaySemanticTransition transition)
        {
            GameplayReductionResult reduction = PrepareReduction(transition);
            return InstallPreparedReduction(transition, reduction);
        }

        /// <summary>
        /// Performs the pure reduction half of execution without mutating the
        /// authoritative root. A caller may run this on a cancellable worker and
        /// then marshal installation back to the live owner thread.
        /// </summary>
        public GameplayReductionResult PrepareReduction(
            GameplaySemanticTransition transition)
        {
            if (transition == null)
                throw new ArgumentNullException(nameof(transition));
            capabilities.RequireCompleteRoute(transition.Profile);
            return reducers.Reduce(stateStore.Current, transition);
        }

        /// <summary>
        /// Atomically installs a previously reduced transition and records the
        /// sole semantic trajectory step. The state store rejects stale roots.
        /// </summary>
        public GameplayReductionResult InstallPreparedReduction(
            GameplaySemanticTransition transition,
            GameplayReductionResult reduction)
        {
            if (transition == null)
                throw new ArgumentNullException(nameof(transition));
            if (reduction == null)
                throw new ArgumentNullException(nameof(reduction));
            capabilities.RequireCompleteRoute(transition.Profile);
            if (!string.Equals(
                    stateStore.Current.CanonicalHash,
                    reduction.Previous.CanonicalHash,
                    StringComparison.Ordinal))
                throw new GameplayStaleDecisionStateException(
                    reduction.Previous.CanonicalHash,
                    stateStore.Current.CanonicalHash);
            if (!string.Equals(
                    transition.PreviousStateHash,
                    reduction.Previous.CanonicalHash,
                    StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Prepared reduction does not belong to the supplied transition.");
            if (reduction.Resulting.Session.LastTransitionSequence
                != transition.Identity.Sequence)
                throw new InvalidOperationException(
                    "Prepared reduction has a different canonical transition sequence.");
            var eventTypes = new List<string>(reduction.DomainEvents.Count);
            int reducedEventCount = 0;
            foreach (GameplayDomainEvent domainEvent in reduction.DomainEvents)
            {
                if (!IdentitiesMatch(
                        domainEvent.Transition,
                        transition.Identity))
                    throw new InvalidOperationException(
                        "Prepared reduction contains an event for another transition.");
                if (domainEvent is GameplayTransitionReducedEvent)
                    reducedEventCount++;
                eventTypes.Add(domainEvent.EventType);
            }
            if (reducedEventCount != 1)
                throw new InvalidOperationException(
                    "Prepared reduction must contain exactly one semantic reduced event.");
            var step = new GameplayTrajectoryStep(
                transition,
                reduction.Resulting.CanonicalHash,
                eventTypes);
            stateStore.Install(
                reduction,
                installed =>
                {
                    int trajectoryIndex = trajectory.Count;
                    trajectory.Add(step);
                    RecordReplayWindow(reduction, trajectoryIndex);
                    PublishStateInstalled(installed);
                });
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

        public GameplaySemanticReplayTimeline CreateReplayTimeline() =>
            new GameplaySemanticReplayTimeline(
                InitialState,
                trajectory,
                reducers);

        /// <summary>
        /// Replays exactly the most recently completed personal turn. A turn
        /// has a variable number of semantic transitions, so this window is
        /// recorded at authoritative turn-end installation rather than guessed
        /// from a fixed number of trajectory frames.
        /// </summary>
        public bool TryCreateLastCompletedTurnReplay(
            out GameplaySemanticReplayTimeline replay)
        {
            GameplayReplayWindow window = LastCompletedTurnReplayWindow;
            if (window == null)
            {
                replay = null;
                return false;
            }

            replay = new GameplaySemanticReplayTimeline(
                window.InitialState,
                trajectory.GetRange(
                    window.StartTrajectoryIndex,
                    window.TransitionCount),
                reducers);
            return true;
        }

        private void RecordReplayWindow(
            GameplayReductionResult reduction,
            int trajectoryIndex)
        {
            if (EnteredEncounter(reduction))
            {
                // A runtime may have been alive during exploration before an
                // encounter begins. Do not offer that unrelated history from
                // the live battle replay control.
                completedTurnReplayWindows.Clear();
                openReplayWindowInitialState = reduction.Resulting;
                openReplayWindowStartTrajectoryIndex = checked(
                    trajectoryIndex + 1);
                return;
            }

            TurnEndRecord endedTurn = FindTurnEndRecord(reduction);
            if (endedTurn == null)
                return;

            completedTurnReplayWindows.Add(new GameplayReplayWindow(
                endedTurn.EndingActorId,
                endedTurn.Sequence,
                openReplayWindowInitialState,
                openReplayWindowStartTrajectoryIndex,
                trajectoryIndex));
            openReplayWindowInitialState = reduction.Resulting;
            openReplayWindowStartTrajectoryIndex = checked(
                trajectoryIndex + 1);
        }

        private static bool EnteredEncounter(GameplayReductionResult reduction)
        {
            GameplaySessionStateSnapshot previous = reduction.Previous.Session;
            GameplaySessionStateSnapshot resulting = reduction.Resulting.Session;
            return !previous.EncounterActive
                && resulting.EncounterActive
                && resulting.Mode == GameplaySessionMode.TurnBased;
        }

        private static TurnEndRecord FindTurnEndRecord(
            GameplayReductionResult reduction)
        {
            foreach (GameplayDomainEvent domainEvent in reduction.DomainEvents)
            {
                if (domainEvent is GameplayTransitionReducedEvent reduced
                    && reduced.SemanticRecord is TurnEndRecord turnEnd)
                {
                    return turnEnd;
                }
            }

            return null;
        }

        private void PublishDomainEvent(GameplayDomainEvent domainEvent) =>
            DomainEventPublished?.Invoke(domainEvent);

        private static bool IdentitiesMatch(
            GameplayTransitionIdentity left,
            GameplayTransitionIdentity right) => left.Sequence == right.Sequence
            && string.Equals(left.Kind, right.Kind, StringComparison.Ordinal)
            && string.Equals(
                left.ActorId,
                right.ActorId,
                StringComparison.Ordinal)
            && string.Equals(
                left.SubjectId,
                right.SubjectId,
                StringComparison.Ordinal);

        private void PublishStateInstalled(GameplayReductionResult reduction)
        {
            Delegate[] listeners = StateInstalled?.GetInvocationList();
            if (listeners == null) return;
            var failures = new List<Exception>();
            foreach (Delegate listener in listeners)
                try
                {
                    ((Action<GameplayReductionResult>)listener)(reduction);
                }
                catch (Exception exception)
                {
                    failures.Add(exception);
                }
            if (failures.Count > 0)
                throw new AggregateException(
                    "Canonical state was installed, but a live projection failed.",
                    failures);
        }
    }
}
