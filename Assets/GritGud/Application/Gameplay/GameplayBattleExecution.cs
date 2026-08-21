using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Levels;

namespace GritGud.Application.Gameplay
{
    public enum GameplayBattleTerminalKind
    {
        PartyVictory,
        HostileVictory,
        ObjectiveVictory,
        MutualDefeat,
        ExecutionFailure,
        Stalemate,
    }

    public sealed class GameplayBattleTerminalResult
    {
        public GameplayBattleTerminalResult(
            GameplayBattleTerminalKind kind,
            long transitionSequence,
            string finalStateHash,
            IEnumerable<string> capablePartyActorIds,
            IEnumerable<string> capableHostileActorIds,
            GameplayDecisionFailureKind? failureKind = null,
            string failureMessage = null)
        {
            if (!Enum.IsDefined(typeof(GameplayBattleTerminalKind), kind))
                throw new ArgumentOutOfRangeException(nameof(kind));
            if (transitionSequence < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(transitionSequence));
            Kind = kind;
            TransitionSequence = transitionSequence;
            FinalStateHash = GameplayContentIdentity.RequireDigest(
                finalStateHash,
                nameof(finalStateHash));
            CapablePartyActorIds = CopyIds(capablePartyActorIds);
            CapableHostileActorIds = CopyIds(capableHostileActorIds);
            bool isExecutionFailure = kind
                == GameplayBattleTerminalKind.ExecutionFailure;
            if (isExecutionFailure != failureKind.HasValue)
                throw new ArgumentException(
                    "Execution failures require exactly one typed failure kind.",
                    nameof(failureKind));
            FailureKind = failureKind;
            FailureMessage = failureMessage?.Trim() ?? string.Empty;
        }

        public GameplayBattleTerminalKind Kind { get; }
        public long TransitionSequence { get; }
        public string FinalStateHash { get; }
        public IReadOnlyList<string> CapablePartyActorIds { get; }
        public IReadOnlyList<string> CapableHostileActorIds { get; }
        public GameplayDecisionFailureKind? FailureKind { get; }
        public string FailureMessage { get; }
        public bool IsSuccessful => Kind
            != GameplayBattleTerminalKind.ExecutionFailure;

        private static IReadOnlyList<string> CopyIds(
            IEnumerable<string> values)
        {
            var copy = new List<string>(values ?? Array.Empty<string>());
            copy.Sort(StringComparer.Ordinal);
            for (int index = 0; index < copy.Count; index++)
            {
                copy[index] = GameplayContentIdentity.RequireText(
                    copy[index],
                    nameof(values));
                if (index > 0 && string.Equals(
                    copy[index - 1],
                    copy[index],
                    StringComparison.Ordinal))
                    throw new ArgumentException(
                        "Battle terminal actor identifiers must be unique.",
                        nameof(values));
            }
            return copy.AsReadOnly();
        }
    }

    public sealed class GameplayBattleDecisionRecord
    {
        public GameplayBattleDecisionRecord(
            int decisionIndex,
            string policyId,
            int policyVersion,
            GameplayDecisionExecutionResult result)
        {
            if (decisionIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(decisionIndex));
            if (result == null)
                throw new ArgumentNullException(nameof(result));
            DecisionIndex = decisionIndex;
            PolicyId = GameplayContentIdentity.RequireText(
                policyId,
                nameof(policyId));
            if (policyVersion <= 0)
                throw new ArgumentOutOfRangeException(nameof(policyVersion));
            PolicyVersion = policyVersion;
            ActorId = GameplayContentIdentity.RequireText(
                result.Diagnostic.ActorId,
                nameof(result));
            PreviousStateHash = GameplayContentIdentity.RequireDigest(
                result.Diagnostic.StateHash,
                nameof(result));
            CandidateIds = CopyIds(result.Diagnostic.CandidateIds);
            LegalCandidateIds = CopyIds(
                result.Diagnostic.LegalCandidateIds);
            SelectedCandidateId = GameplayContentIdentity.RequireText(
                result.Diagnostic.SelectedCandidateId,
                nameof(result));
            if (!Contains(LegalCandidateIds, SelectedCandidateId))
                throw new ArgumentException(
                    "The selected policy candidate was not legal.",
                    nameof(result));
            CandidateSetDigest = CalculateCandidateSetDigest(
                CandidateIds,
                LegalCandidateIds);
            SelectionReason = result.SelectionReason;
            Score = result.Selection.Value;
            ScoreComponents = new List<GameplayPolicyScoreComponent>(
                result.Selection.Components).AsReadOnly();
            Diagnostic = result.Diagnostic;
            TransitionSequence = result.Transition.Identity.Sequence;
            TransitionPayloadDigest = GameplayTransitionPayloadDigest
                .Calculate(result.Transition);
            ResultingStateHash = result.Reduction.Resulting.CanonicalHash;
        }

        public int DecisionIndex { get; }
        public string PolicyId { get; }
        public int PolicyVersion { get; }
        public string ActorId { get; }
        public string PreviousStateHash { get; }
        public string CandidateSetDigest { get; }
        public IReadOnlyList<string> CandidateIds { get; }
        public IReadOnlyList<string> LegalCandidateIds { get; }
        public string SelectedCandidateId { get; }
        public GameplayPolicySelectionReason SelectionReason { get; }
        public float Score { get; }
        public IReadOnlyList<GameplayPolicyScoreComponent> ScoreComponents
        {
            get;
        }
        /// <summary>
        /// Non-canonical wall-clock diagnostics for profiling this execution.
        /// Artifact construction deliberately copies only deterministic fields.
        /// </summary>
        public GameplayDecisionDiagnostic Diagnostic { get; }
        public long TransitionSequence { get; }
        public string TransitionPayloadDigest { get; }
        public string ResultingStateHash { get; }

        private static IReadOnlyList<string> CopyIds(
            IEnumerable<string> values)
        {
            var copy = new List<string>(values ?? Array.Empty<string>());
            copy.Sort(StringComparer.Ordinal);
            for (int index = 0; index < copy.Count; index++)
            {
                copy[index] = GameplayContentIdentity.RequireText(
                    copy[index],
                    nameof(values));
                if (index > 0 && string.Equals(
                    copy[index - 1],
                    copy[index],
                    StringComparison.Ordinal))
                    throw new ArgumentException(
                        "Decision candidate identifier '" + copy[index]
                            + "' is duplicated.",
                        nameof(values));
            }
            return copy.AsReadOnly();
        }

        private static bool Contains(
            IEnumerable<string> values,
            string expected)
        {
            foreach (string value in values)
                if (string.Equals(value, expected, StringComparison.Ordinal))
                    return true;
            return false;
        }

        private static string CalculateCandidateSetDigest(
            IEnumerable<string> candidateIds,
            IEnumerable<string> legalIds)
        {
            var value = new StringBuilder();
            AppendIds(value, "candidate", candidateIds);
            AppendIds(value, "legal", legalIds);
            using (SHA256 sha = SHA256.Create())
            {
                byte[] digest = sha.ComputeHash(
                    Encoding.UTF8.GetBytes(value.ToString()));
                var result = new StringBuilder(digest.Length * 2);
                foreach (byte item in digest)
                    result.Append(item.ToString("x2"));
                return result.ToString();
            }
        }

        private static void AppendIds(
            StringBuilder value,
            string label,
            IEnumerable<string> ids)
        {
            value.Append(label);
            value.Append(':');
            foreach (string id in ids)
            {
                value.Append(id.Length);
                value.Append(':');
                value.Append(id);
                value.Append(';');
            }
        }
    }

    public sealed class GameplayBattleTransitionRecord
    {
        public GameplayBattleTransitionRecord(
            GameplaySemanticTransition transition,
            GameplayReductionResult reduction,
            int? decisionIndex)
        {
            Transition = transition ?? throw new ArgumentNullException(
                nameof(transition));
            if (reduction == null)
                throw new ArgumentNullException(nameof(reduction));
            if (decisionIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(decisionIndex));
            if (!string.Equals(
                    transition.PreviousStateHash,
                    reduction.Previous.CanonicalHash,
                    StringComparison.Ordinal))
                throw new ArgumentException(
                    "Battle transition does not begin at its reduction root.",
                    nameof(reduction));
            var eventTypes = new List<string>();
            var events = new List<GameplayDomainEvent>();
            var eventDigests = new List<string>();
            foreach (GameplayDomainEvent domainEvent in reduction.DomainEvents)
            {
                if (domainEvent.Transition != transition.Identity)
                    throw new ArgumentException(
                        "Battle transition contains an event for another identity.",
                        nameof(reduction));
                events.Add(domainEvent);
                eventTypes.Add(domainEvent.EventType);
                eventDigests.Add(GameplayCanonicalValueDigest.Calculate(
                    domainEvent));
            }
            Step = new GameplayTrajectoryStep(
                transition,
                reduction.Resulting.CanonicalHash,
                eventTypes);
            DomainEvents = events.AsReadOnly();
            DomainEventPayloadDigests = eventDigests.AsReadOnly();
            DecisionIndex = decisionIndex;
        }

        public GameplaySemanticTransition Transition { get; }
        public GameplayTrajectoryStep Step { get; }
        public IReadOnlyList<GameplayDomainEvent> DomainEvents { get; }
        public IReadOnlyList<string> DomainEventPayloadDigests { get; }
        public int? DecisionIndex { get; }
    }

    public sealed class GameplayBattleRunResult
    {
        public GameplayBattleRunResult(
            GameplayExecutionIdentity executionIdentity,
            GameplayCombatStateSnapshot initialState,
            IEnumerable<GameplayBattleTransitionRecord> transitions,
            IEnumerable<GameplayBattleDecisionRecord> decisions,
            GameplayCombatStateSnapshot finalState,
            GameplayBattleTerminalResult terminal,
            GameplayDecisionDiagnostic failureDiagnostic = null)
        {
            ExecutionIdentity = executionIdentity
                ?? throw new ArgumentNullException(nameof(executionIdentity));
            InitialState = initialState ?? throw new ArgumentNullException(
                nameof(initialState));
            FinalState = finalState ?? throw new ArgumentNullException(
                nameof(finalState));
            Terminal = terminal ?? throw new ArgumentNullException(
                nameof(terminal));
            Transitions = new List<GameplayBattleTransitionRecord>(
                transitions ?? throw new ArgumentNullException(
                    nameof(transitions))).AsReadOnly();
            Decisions = new List<GameplayBattleDecisionRecord>(
                decisions ?? throw new ArgumentNullException(
                    nameof(decisions))).AsReadOnly();
            FailureDiagnostic = failureDiagnostic;
            if (!string.Equals(
                    terminal.FinalStateHash,
                    finalState.CanonicalHash,
                    StringComparison.Ordinal))
                throw new ArgumentException(
                    "Battle terminal result does not match final state.",
                    nameof(terminal));
            if (terminal.IsSuccessful != (failureDiagnostic == null))
                throw new ArgumentException(
                    "Only failed battles may carry a failure diagnostic.",
                    nameof(failureDiagnostic));
        }

        public GameplayExecutionIdentity ExecutionIdentity { get; }
        public GameplayCombatStateSnapshot InitialState { get; }
        public IReadOnlyList<GameplayBattleTransitionRecord> Transitions
        {
            get;
        }
        public IReadOnlyList<GameplayBattleDecisionRecord> Decisions { get; }
        public GameplayCombatStateSnapshot FinalState { get; }
        public GameplayBattleTerminalResult Terminal { get; }
        public GameplayDecisionDiagnostic FailureDiagnostic { get; }

        public IReadOnlyList<GameplayTrajectoryStep> CreateTrajectory()
        {
            var result = new List<GameplayTrajectoryStep>(Transitions.Count);
            foreach (GameplayBattleTransitionRecord transition in Transitions)
                result.Add(transition.Step);
            return result.AsReadOnly();
        }
    }

    public static class GameplayHeadlessBattleStateFactory
    {
        public static GameplayCombatStateSnapshot Create(
            GameplayScenarioAssembly assembly,
            GameplayStaticSpatialContent spatialContent)
        {
            if (assembly == null)
                throw new ArgumentNullException(nameof(assembly));
            if (spatialContent == null)
                throw new ArgumentNullException(nameof(spatialContent));
            LevelDocument level = spatialContent.Level;
            var gameplay = new GameplaySession(
                assembly.Scenario,
                scenarioSeed: assembly.RandomSeed);
            GameplayCombatStateSnapshot sessionState =
                GameplayCombatStateCapture.Capture(gameplay);
            DestructiblePropSession destructibles =
                DestructiblePropSession.FromLevel(
                    level,
                    gameplay.Journal,
                    spatialContent.ResolveFractureChunkCount);
            var propStates = new List<DestructiblePropSnapshot>();
            foreach (string propId in destructibles.PropIds)
                propStates.Add(destructibles.GetProp(propId));
            var vehicles = new List<VehicleMomentumState>();
            foreach (ScenarioVehicleRuntimeDefinition vehicle in
                assembly.Vehicles)
            {
                LevelEntity entity = FindEntity(level, vehicle.EntityId);
                vehicles.Add(new VehicleMomentumState(
                    vehicle.EntityId,
                    ToPosition(entity.transform.position),
                    entity.transform.yawDegrees,
                    vehicle.StartingSpeed));
            }
            var drones = new List<DroneSnapshot>();
            foreach (DroneDefinition drone in assembly.Drones)
                drones.Add(drone.CreateInitialSnapshot());
            return new GameplayCombatStateSnapshot(
                sessionState.Session,
                propStates,
                vehicles,
                Array.Empty<ProjectileFlightSnapshot>(),
                Array.Empty<SmokeFieldSnapshot>(),
                GameplayCombatStateSnapshot.AllCoverage,
                Array.Empty<FireFieldSnapshot>(),
                drones);
        }

        private static LevelEntity FindEntity(LevelDocument level, string id)
        {
            foreach (LevelEntity entity in level.entities)
                if (string.Equals(entity.id, id, StringComparison.Ordinal))
                    return entity;
            throw new KeyNotFoundException(
                $"Headless battle level is missing entity '{id}'.");
        }

        private static GameplayPosition ToPosition(Float3Data value) =>
            new GameplayPosition(value.x, value.y, value.z);
    }

    public sealed class GameplayBattleRunner
    {
        private readonly GameplayScenarioAssembly assembly;
        private readonly GameplayExecutionIdentity executionIdentity;
        private readonly GameplayTransitionReducerRegistry reducers;
        private readonly GameplayCapabilityRegistry capabilities;
        private readonly GameplayPolicyDecisionRunner decisionRunner;
        private readonly IGameplayIdentifiedCandidatePolicy policy;
        private readonly GameplayExecutionLogicalGuardPolicy guardPolicy;

        public GameplayBattleRunner(
            GameplayScenarioAssembly scenarioAssembly,
            GameplayStaticSpatialContent spatialContent,
            GameplayExecutionIdentity identity,
            IGameplayCandidatePolicy candidatePolicy = null,
            GameplayExecutionDeadlinePolicy deadlinePolicy = null,
            GameplayExecutionLogicalGuardPolicy logicalGuardPolicy = null,
            IGameplayDecisionWorkerBoundary workerBoundary = null)
        {
            assembly = scenarioAssembly ?? throw new ArgumentNullException(
                nameof(scenarioAssembly));
            if (spatialContent == null)
                throw new ArgumentNullException(nameof(spatialContent));
            LevelDocument level = spatialContent.Level;
            executionIdentity = identity ?? throw new ArgumentNullException(
                nameof(identity));
            if (!string.Equals(
                    identity.Gameplay.ScenarioId,
                    assembly.Scenario.Id,
                    StringComparison.Ordinal)
                || !string.Equals(
                    identity.Spatial.LevelId,
                    level.levelId,
                    StringComparison.Ordinal))
                throw new ArgumentException(
                    "Battle execution identity does not match assembled content.",
                    nameof(identity));
            IGameplayCandidatePolicy selectedPolicy = candidatePolicy
                ?? GameplayBaselineCombatPolicy.Create(assembly.Scenario);
            policy = selectedPolicy as IGameplayIdentifiedCandidatePolicy
                ?? throw new ArgumentException(
                    "Artifact-producing battle policies require stable identity.",
                    nameof(candidatePolicy));
            reducers = GameplaySimulationReducers.CreateCurrent();
            IReadOnlyList<GameplayReachableInput> reachable =
                GameplayReachableInputEnumerator.Enumerate(assembly, level);
            capabilities = GameplayCurrentCapabilityCatalog.Create(
                reducers,
                reachable);
            GameplayCapabilityCoverageValidator.Validate(
                    reachable,
                    capabilities)
                .RequireComplete(assembly.Scenario.Id);
            if (!identity.Spatial.HasSameIdentity(spatialContent.Identity))
                throw new ArgumentException(
                    "Battle execution identity does not match static spatial "
                    + "content.",
                    nameof(identity));
            GameplayHeadlessSpatialEvidence spatial =
                spatialContent.CreateEvidence();
            GameplayCandidateExecutionRouteRegistry routes =
                GameplayCurrentCandidateExecutionRoutes.Create(
                    assembly,
                    spatial,
                    capabilities);
            GameplayExecutableRouteCoverageValidator.Validate(
                    reachable,
                    routes)
                .RequireComplete();
            decisionRunner = new GameplayPolicyDecisionRunner(
                new GameplayHeadlessDecisionCandidateSource(
                    new GameplayHeadlessCandidateBuilder(
                        capabilities,
                        spatial,
                        scenarioDefinition: assembly.Scenario,
                        authoredTraversalLinks: level.traversalLinks),
                    reachable,
                    routes),
                routes,
                selectedPolicy,
                workerBoundary: workerBoundary,
                deadlinePolicy: deadlinePolicy);
            guardPolicy = logicalGuardPolicy
                ?? new GameplayExecutionLogicalGuardPolicy();
        }

        public async Task<GameplayBattleRunResult> RunAsync(
            GameplayCombatStateSnapshot initialState,
            CancellationToken cancellationToken = default)
        {
            if (initialState == null)
                throw new ArgumentNullException(nameof(initialState));
            if (!executionIdentity.Run.HasSameIdentity(
                    initialState.Session.RunIdentity))
                throw new ArgumentException(
                    "Battle state and execution run identities differ.",
                    nameof(initialState));
            initialState.RequireCoverage(
                GameplayCombatStateSnapshot.AllCoverage);
            var runtime = new GameplaySimulationRuntime(
                executionIdentity,
                initialState,
                reducers,
                capabilities);
            var transitions = new List<GameplayBattleTransitionRecord>();
            var decisions = new List<GameplayBattleDecisionRecord>();
            if (!initialState.Session.EncounterActive)
                BeginEncounter(runtime, transitions);

            var deadlines = new GameplayExecutionDeadlineScope();
            var guard = new GameplayExecutionLogicalGuard(
                runtime.CurrentState,
                guardPolicy);
            string turnActorId = string.Empty;
            long turnSequence = -1L;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (TryResolveTerminal(
                        runtime.CurrentState,
                        out GameplayBattleTerminalResult terminal))
                    return new GameplayBattleRunResult(
                        executionIdentity,
                        initialState,
                        transitions,
                        decisions,
                        runtime.CurrentState,
                        terminal);
                GameplaySessionStateSnapshot session = runtime.CurrentState
                    .Session;
                if (!string.Equals(
                        turnActorId,
                        session.ActiveActorId,
                        StringComparison.Ordinal)
                    || turnSequence != session.LastTurnSequence)
                {
                    turnActorId = session.ActiveActorId;
                    turnSequence = session.LastTurnSequence;
                    deadlines.BeginTurn();
                    guard.BeginTurn(turnActorId, runtime.CurrentState);
                }
                try
                {
                    GameplayDecisionExecutionResult decision =
                        await decisionRunner.ExecuteAsync(
                            runtime,
                            GameplayObservationSnapshot.FullState(
                                turnActorId,
                                runtime.CurrentState),
                            deadlines,
                            guard,
                            cancellationToken).ConfigureAwait(false);
                    int decisionIndex = decisions.Count;
                    decisions.Add(new GameplayBattleDecisionRecord(
                        decisionIndex,
                        policy.PolicyId,
                        policy.PolicyVersion,
                        decision));
                    transitions.Add(new GameplayBattleTransitionRecord(
                        decision.Transition,
                        decision.Reduction,
                        decisionIndex));
                }
                catch (GameplayDecisionFailureException failure)
                {
                    if (failure.Kind
                        == GameplayDecisionFailureKind.NoProgressTurn)
                    {
                        GameplayBattleTerminalResult stalemate =
                            CreateTerminal(
                                runtime.CurrentState,
                                GameplayBattleTerminalKind.Stalemate);
                        return new GameplayBattleRunResult(
                            executionIdentity,
                            initialState,
                            transitions,
                            decisions,
                            runtime.CurrentState,
                            stalemate);
                    }
                    GameplayBattleTerminalResult failed = CreateFailureTerminal(
                        runtime.CurrentState,
                        failure);
                    return new GameplayBattleRunResult(
                        executionIdentity,
                        initialState,
                        transitions,
                        decisions,
                        runtime.CurrentState,
                        failed,
                        failure.Diagnostic);
                }
            }
        }

        private void BeginEncounter(
            GameplaySimulationRuntime runtime,
            ICollection<GameplayBattleTransitionRecord> transitions)
        {
            var participants = new List<string>();
            foreach (ScenarioActorDefinition actor in assembly.Scenario.Actors)
                if (!runtime.CurrentState.Session.GetActor(actor.Id)
                    .IsIncapacitated)
                    participants.Add(actor.Id);
            string actorId = assembly.PlayerParty?.InitiallySelectedActorId
                ?? participants[0];
            var payload = new GameplaySessionControlTransitionPayload(
                actorId,
                GameplaySemanticCapability.ChangeEncounter,
                "begin",
                encounterParticipantIds: participants);
            GameplaySemanticTransition transition = CreateTransition(
                runtime.CurrentState,
                payload);
            GameplayReductionResult reduction = runtime.Execute(transition);
            transitions.Add(new GameplayBattleTransitionRecord(
                transition,
                reduction,
                decisionIndex: null));
        }

        private bool TryResolveTerminal(
            GameplayCombatStateSnapshot state,
            out GameplayBattleTerminalResult terminal)
        {
            var capableParty = new List<string>();
            var capableHostiles = new List<string>();
            foreach (ScenarioActorDefinition actor in assembly.Scenario.Actors)
            {
                if (state.Session.GetActor(actor.Id).IsIncapacitated)
                    continue;
                if (assembly.PlayerParty?.Contains(actor.Id) == true)
                    capableParty.Add(actor.Id);
                else if (actor.Combat.EnemyBehavior != null)
                    capableHostiles.Add(actor.Id);
            }
            bool objectiveComplete = false;
            foreach (GameplayObjectiveSnapshot objective in
                state.Session.Objectives)
                objectiveComplete |= objective.IsCompleted;
            GameplayBattleTerminalKind? kind = objectiveComplete
                ? GameplayBattleTerminalKind.ObjectiveVictory
                : capableParty.Count == 0 && capableHostiles.Count == 0
                    ? GameplayBattleTerminalKind.MutualDefeat
                    : capableParty.Count == 0
                        ? GameplayBattleTerminalKind.HostileVictory
                        : capableHostiles.Count == 0
                            ? GameplayBattleTerminalKind.PartyVictory
                            : (GameplayBattleTerminalKind?)null;
            if (!kind.HasValue)
            {
                terminal = null;
                return false;
            }
            terminal = CreateTerminal(
                state,
                kind.Value,
                capableParty,
                capableHostiles);
            return true;
        }

        private GameplayBattleTerminalResult CreateTerminal(
            GameplayCombatStateSnapshot state,
            GameplayBattleTerminalKind kind)
        {
            CollectCapableActors(
                state,
                out List<string> capableParty,
                out List<string> capableHostiles);
            return CreateTerminal(
                state,
                kind,
                capableParty,
                capableHostiles);
        }

        private static GameplayBattleTerminalResult CreateTerminal(
            GameplayCombatStateSnapshot state,
            GameplayBattleTerminalKind kind,
            IEnumerable<string> capableParty,
            IEnumerable<string> capableHostiles) =>
            new GameplayBattleTerminalResult(
                kind,
                state.Session.LastTransitionSequence,
                state.CanonicalHash,
                capableParty,
                capableHostiles);

        private GameplayBattleTerminalResult CreateFailureTerminal(
            GameplayCombatStateSnapshot state,
            GameplayDecisionFailureException failure)
        {
            CollectCapableActors(
                state,
                out List<string> capableParty,
                out List<string> capableHostiles);
            return new GameplayBattleTerminalResult(
                GameplayBattleTerminalKind.ExecutionFailure,
                state.Session.LastTransitionSequence,
                state.CanonicalHash,
                capableParty,
                capableHostiles,
                failure.Kind,
                DescribeFailure(failure));
        }

        private void CollectCapableActors(
            GameplayCombatStateSnapshot state,
            out List<string> capableParty,
            out List<string> capableHostiles)
        {
            capableParty = new List<string>();
            capableHostiles = new List<string>();
            foreach (ScenarioActorDefinition actor in assembly.Scenario.Actors)
            {
                if (state.Session.GetActor(actor.Id).IsIncapacitated)
                    continue;
                if (assembly.PlayerParty?.Contains(actor.Id) == true)
                    capableParty.Add(actor.Id);
                else if (actor.Combat.EnemyBehavior != null)
                    capableHostiles.Add(actor.Id);
            }
        }

        private static string DescribeFailure(Exception failure)
        {
            var result = new StringBuilder();
            for (Exception current = failure;
                current != null;
                current = current.InnerException)
            {
                if (result.Length > 0) result.Append(" -> ");
                result.Append(current.GetType().Name);
                result.Append(": ");
                result.Append(current.Message);
            }
            return result.ToString();
        }

        private static GameplaySemanticTransition CreateTransition(
            GameplayCombatStateSnapshot state,
            GameplayTransitionPayload payload) =>
            new GameplaySemanticTransition(
                new GameplayTransitionIdentity(
                    checked(state.Session.LastTransitionSequence + 1L),
                    payload.Profile.Capability.ToString(),
                    payload.ActorId,
                    payload.SubjectId),
                state.CanonicalHash,
                payload);
    }
}
