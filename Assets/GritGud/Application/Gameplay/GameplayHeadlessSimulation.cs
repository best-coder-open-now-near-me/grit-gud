using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace GritGud.Application.Gameplay
{
    public sealed class GameplayTrajectoryStep
    {
        public GameplayTrajectoryStep(
            GameplaySemanticTransition transition,
            string resultingStateHash,
            IEnumerable<string> domainEventTypes,
            string transitionPayloadDigest = null)
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
            TransitionPayloadDigest = transitionPayloadDigest == null
                ? GameplayTransitionPayloadDigest.Calculate(Transition)
                : GameplayContentIdentity.RequireDigest(
                    transitionPayloadDigest,
                    nameof(transitionPayloadDigest));
        }

        public GameplaySemanticTransition Transition { get; }
        public string ResultingStateHash { get; }
        public IReadOnlyList<string> DomainEventTypes { get; }
        public string TransitionPayloadDigest { get; }
    }

    public static class GameplayTransitionPayloadDigest
    {
        public static string Calculate(GameplaySemanticTransition transition)
        {
            if (transition == null)
                throw new ArgumentNullException(nameof(transition));
            string canonical = GameplayReproBundleFormatter
                .FormatCanonicalValue(transition);
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(canonical));
                var text = new StringBuilder(hash.Length * 2);
                foreach (byte value in hash)
                    text.Append(value.ToString("x2"));
                return text.ToString();
            }
        }
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

        public GameplayReproBundle CreateRepro(
            GameplayExecutionIdentity executionIdentity,
            string label) => new GameplayReproBundle(
                executionIdentity,
                InitialState,
                steps,
                label);
    }

    public sealed class GameplayExactReplayResult
    {
        internal GameplayExactReplayResult(
            GameplayCombatStateSnapshot finalState,
            int verifiedStepCount,
            int divergentStepIndex,
            string expectedHash,
            string actualHash,
            string divergenceReason = "")
        {
            FinalState = finalState ?? throw new ArgumentNullException(
                nameof(finalState));
            VerifiedStepCount = verifiedStepCount;
            DivergentStepIndex = divergentStepIndex;
            ExpectedHash = expectedHash ?? string.Empty;
            ActualHash = actualHash ?? string.Empty;
            DivergenceReason = divergenceReason ?? string.Empty;
        }

        public GameplayCombatStateSnapshot FinalState { get; }
        public int VerifiedStepCount { get; }
        public int DivergentStepIndex { get; }
        public string ExpectedHash { get; }
        public string ActualHash { get; }
        public string DivergenceReason { get; }
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
                string actualTransitionDigest =
                    GameplayTransitionPayloadDigest.Calculate(step.Transition);
                if (!string.Equals(
                        step.TransitionPayloadDigest,
                        actualTransitionDigest,
                        StringComparison.Ordinal))
                {
                    return new GameplayExactReplayResult(
                        state,
                        index,
                        index,
                        step.TransitionPayloadDigest,
                        actualTransitionDigest,
                        "transition-payload");
                }
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
                        state.CanonicalHash,
                        "state-hash");
                string expectedEvents = Join(step.DomainEventTypes);
                string actualEvents = Join(reduction.DomainEvents);
                if (!string.Equals(
                        expectedEvents,
                        actualEvents,
                        StringComparison.Ordinal))
                {
                    return new GameplayExactReplayResult(
                        state,
                        index,
                        index,
                        expectedEvents,
                        actualEvents,
                        "domain-events");
                }
                index++;
            }
            return new GameplayExactReplayResult(
                state,
                index,
                divergentStepIndex: -1,
                expectedHash: state.CanonicalHash,
                actualHash: state.CanonicalHash,
                divergenceReason: string.Empty);
        }

        private static string Join(IEnumerable<string> eventTypes)
        {
            var text = new StringBuilder();
            foreach (string eventType in eventTypes)
            {
                if (text.Length > 0) text.Append('|');
                text.Append(eventType);
            }
            return text.ToString();
        }

        private static string Join(
            IEnumerable<GameplayDomainEvent> domainEvents)
        {
            var text = new StringBuilder();
            foreach (GameplayDomainEvent domainEvent in domainEvents)
            {
                if (text.Length > 0) text.Append('|');
                text.Append(domainEvent.EventType);
            }
            return text.ToString();
        }
    }

    public sealed class GameplayReproBundle
    {
        public const int CurrentSchemaVersion = 2;

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
            var steps = new List<GameplayTrajectoryStep>(
                trajectory ?? throw new ArgumentNullException(
                    nameof(trajectory)));
            Label = label?.Trim() ?? string.Empty;
            if (!executionIdentity.Run.HasSameIdentity(
                initialState.Session.RunIdentity))
                throw new ArgumentException(
                    "Repro execution and canonical state run identities differ.",
                    nameof(executionIdentity));
            ValidateTrajectory(initialState, steps);
            SchemaVersion = CurrentSchemaVersion;
            NumericPolicyVersion = GameplayNumericPolicy.CurrentVersion;
            Trajectory = steps.AsReadOnly();
            FinalStateHash = steps.Count == 0
                ? initialState.CanonicalHash
                : steps[steps.Count - 1].ResultingStateHash;
        }

        public int SchemaVersion { get; }
        public int NumericPolicyVersion { get; }
        public GameplayExecutionIdentity ExecutionIdentity { get; }
        public GameplayCombatStateSnapshot InitialState { get; }
        public IReadOnlyList<GameplayTrajectoryStep> Trajectory { get; }
        public string Label { get; }
        public string FinalStateHash { get; }

        public string ToPortableJson() =>
            GameplayReproBundleFormatter.Format(this);

        private static void ValidateTrajectory(
            GameplayCombatStateSnapshot initialState,
            IReadOnlyList<GameplayTrajectoryStep> steps)
        {
            string previousHash = initialState.CanonicalHash;
            long sequence = initialState.Session.LastTransitionSequence;
            for (int index = 0; index < steps.Count; index++)
            {
                GameplayTrajectoryStep step = steps[index]
                    ?? throw new ArgumentException(
                        "Repro trajectories cannot contain null steps.",
                        nameof(steps));
                if (!string.Equals(
                        step.Transition.PreviousStateHash,
                        previousHash,
                        StringComparison.Ordinal))
                    throw new ArgumentException(
                        $"Repro step {index} does not continue the prior state hash.",
                        nameof(steps));
                sequence = checked(sequence + 1L);
                if (step.Transition.Identity.Sequence != sequence)
                    throw new ArgumentException(
                        $"Repro step {index} has a non-contiguous transition sequence.",
                        nameof(steps));
                previousHash = step.ResultingStateHash;
            }
        }
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
            registry.Register(new GameplayEncounterTransitionReducer());
            return registry;
        }
    }
}
