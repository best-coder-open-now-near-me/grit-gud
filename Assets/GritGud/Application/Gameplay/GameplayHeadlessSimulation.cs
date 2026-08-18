using System;
using System.Collections.Generic;

namespace GritGud.Application.Gameplay
{
    public sealed class GameplayTrajectoryStep
    {
        public GameplayTrajectoryStep(
            GameplaySemanticTransition transition,
            string resultingStateHash,
            IEnumerable<string> domainEventTypes)
        {
            Transition = transition ?? throw new ArgumentNullException(
                nameof(transition));
            ResultingStateHash = GameplayContentIdentity.RequireDigest(
                resultingStateHash,
                nameof(resultingStateHash));
            var eventTypes = new List<string>();
            foreach (string eventType in domainEventTypes
                ?? throw new ArgumentNullException(nameof(domainEventTypes)))
                eventTypes.Add(GameplayContentIdentity.RequireText(
                    eventType,
                    nameof(domainEventTypes)));
            if (eventTypes.Count == 0)
                throw new ArgumentException(
                    "Trajectory steps require domain-event production.",
                    nameof(domainEventTypes));
            DomainEventTypes = eventTypes.AsReadOnly();
        }

        public GameplaySemanticTransition Transition { get; }
        public string ResultingStateHash { get; }
        public IReadOnlyList<string> DomainEventTypes { get; }
    }

    public sealed class GameplaySimulationBranch
    {
        private readonly GameplayTransitionReducerRegistry reducers;
        private readonly List<GameplayTrajectoryStep> steps;

        public GameplaySimulationBranch(
            string branchId,
            GameplayCombatStateSnapshot initialState,
            GameplayTransitionReducerRegistry reducerRegistry)
            : this(
                branchId,
                parentBranchId: string.Empty,
                initialState,
                initialState,
                reducerRegistry,
                Array.Empty<GameplayTrajectoryStep>())
        {
        }

        private GameplaySimulationBranch(
            string branchId,
            string parentBranchId,
            GameplayCombatStateSnapshot initialState,
            GameplayCombatStateSnapshot currentState,
            GameplayTransitionReducerRegistry reducerRegistry,
            IEnumerable<GameplayTrajectoryStep> existingSteps)
        {
            BranchId = GameplayContentIdentity.RequireText(
                branchId,
                nameof(branchId));
            ParentBranchId = parentBranchId ?? string.Empty;
            InitialState = initialState ?? throw new ArgumentNullException(
                nameof(initialState));
            CurrentState = currentState ?? throw new ArgumentNullException(
                nameof(currentState));
            reducers = reducerRegistry ?? throw new ArgumentNullException(
                nameof(reducerRegistry));
            steps = new List<GameplayTrajectoryStep>(existingSteps);
        }

        public string BranchId { get; }
        public string ParentBranchId { get; }
        public GameplayCombatStateSnapshot InitialState { get; }
        public GameplayCombatStateSnapshot CurrentState { get; private set; }
        public IReadOnlyList<GameplayTrajectoryStep> Steps => steps.AsReadOnly();

        public GameplayReductionResult Apply(
            GameplaySemanticTransition transition)
        {
            GameplayReductionResult result = reducers.Reduce(
                CurrentState,
                transition);
            var eventTypes = new List<string>(result.DomainEvents.Count);
            foreach (GameplayDomainEvent domainEvent in result.DomainEvents)
                eventTypes.Add(domainEvent.EventType);
            steps.Add(new GameplayTrajectoryStep(
                transition,
                result.Resulting.CanonicalHash,
                eventTypes));
            CurrentState = result.Resulting;
            return result;
        }

        public GameplaySimulationBranch Fork(string branchId) =>
            new GameplaySimulationBranch(
                branchId,
                BranchId,
                CurrentState,
                CurrentState,
                reducers,
                Array.Empty<GameplayTrajectoryStep>());
    }

    public sealed class GameplayExactReplayResult
    {
        internal GameplayExactReplayResult(
            GameplayCombatStateSnapshot finalState,
            int verifiedStepCount,
            int divergentStepIndex,
            string expectedHash,
            string actualHash)
        {
            FinalState = finalState ?? throw new ArgumentNullException(
                nameof(finalState));
            VerifiedStepCount = verifiedStepCount;
            DivergentStepIndex = divergentStepIndex;
            ExpectedHash = expectedHash ?? string.Empty;
            ActualHash = actualHash ?? string.Empty;
        }

        public GameplayCombatStateSnapshot FinalState { get; }
        public int VerifiedStepCount { get; }
        public int DivergentStepIndex { get; }
        public string ExpectedHash { get; }
        public string ActualHash { get; }
        public bool IsExact => DivergentStepIndex < 0;
    }

    public static class GameplayExactReplay
    {
        public static GameplayExactReplayResult Verify(
            GameplayCombatStateSnapshot initialState,
            IEnumerable<GameplayTrajectoryStep> trajectory,
            GameplayTransitionReducerRegistry reducers)
        {
            if (initialState == null)
                throw new ArgumentNullException(nameof(initialState));
            if (trajectory == null)
                throw new ArgumentNullException(nameof(trajectory));
            if (reducers == null) throw new ArgumentNullException(nameof(reducers));
            GameplayCombatStateSnapshot state = initialState;
            int index = 0;
            foreach (GameplayTrajectoryStep step in trajectory)
            {
                if (step == null)
                    throw new ArgumentException(
                        "Replay trajectories cannot contain null steps.",
                        nameof(trajectory));
                GameplayReductionResult reduction = reducers.Reduce(
                    state,
                    step.Transition);
                state = reduction.Resulting;
                if (!string.Equals(
                    state.CanonicalHash,
                    step.ResultingStateHash,
                    StringComparison.Ordinal))
                    return new GameplayExactReplayResult(
                        state,
                        index,
                        index,
                        step.ResultingStateHash,
                        state.CanonicalHash);
                index++;
            }
            return new GameplayExactReplayResult(
                state,
                index,
                divergentStepIndex: -1,
                expectedHash: state.CanonicalHash,
                actualHash: state.CanonicalHash);
        }
    }

    public sealed class GameplayReproBundle
    {
        public GameplayReproBundle(
            GameplayExecutionIdentity executionIdentity,
            GameplayCombatStateSnapshot initialState,
            IEnumerable<GameplayTrajectoryStep> trajectory,
            string label)
        {
            ExecutionIdentity = executionIdentity ?? throw new ArgumentNullException(
                nameof(executionIdentity));
            InitialState = initialState ?? throw new ArgumentNullException(
                nameof(initialState));
            Trajectory = new List<GameplayTrajectoryStep>(
                trajectory ?? throw new ArgumentNullException(
                    nameof(trajectory))).AsReadOnly();
            Label = label?.Trim() ?? string.Empty;
            if (!executionIdentity.Run.HasSameIdentity(
                initialState.Session.RunIdentity))
                throw new ArgumentException(
                    "Repro execution and canonical state run identities differ.",
                    nameof(executionIdentity));
        }

        public GameplayExecutionIdentity ExecutionIdentity { get; }
        public GameplayCombatStateSnapshot InitialState { get; }
        public IReadOnlyList<GameplayTrajectoryStep> Trajectory { get; }
        public string Label { get; }
    }

    public static class GameplaySimulationReducers
    {
        public static GameplayTransitionReducerRegistry CreateCurrent()
        {
            var registry = new GameplayTransitionReducerRegistry();
            registry.Register(new GameplayCoreTransitionReducer());
            registry.Register(new GameplayTurnTransitionReducer());
            registry.Register(new GameplayResolvedActionTransitionReducer());
            registry.Register(new GameplayExplosiveTransitionReducer());
            registry.Register(new GameplayDisplacementTransitionReducer());
            registry.Register(new GameplayWorldTransitionReducer());
            registry.Register(new GameplayLifecycleTransitionReducer());
            return registry;
        }
    }
}
