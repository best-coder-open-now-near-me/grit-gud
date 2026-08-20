using System;
using System.Collections.Generic;

namespace GritGud.Application.Gameplay
{
    public sealed class GameplayReachableIntent
    {
        public GameplayReachableIntent(GameplayReachableInput input)
        {
            Input = input ?? throw new ArgumentNullException(nameof(input));
        }

        public GameplayReachableInput Input { get; }
    }

    public sealed class GameplayReachableCandidateBuilder
    {
        private readonly GameplayCapabilityRegistry capabilities;

        public GameplayReachableCandidateBuilder(
            GameplayCapabilityRegistry capabilityRegistry)
        {
            capabilities = capabilityRegistry ?? throw new ArgumentNullException(
                nameof(capabilityRegistry));
        }

        public GameplayCandidate Build(GameplayReachableInput input)
            => Build(
                input,
                new GameplaySubjectReference(
                    input.SubjectKind,
                    input.SubjectIdHint
                        ?? (input.SubjectKind
                                == GameplaySemanticSubjectKind.Actor
                            ? input.ActorId
                            : input.SourceId)),
                new GameplayReachableIntent(input));

        public GameplayCandidate Build(
            GameplayReachableInput input,
            GameplaySubjectReference subject,
            object intent,
            string candidateId = null)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (subject.Kind != input.SubjectKind)
                throw new ArgumentException(
                    "Candidate subjects must match their reachable input route.",
                    nameof(subject));
            var candidate = new GameplayCandidate(
                candidateId ?? (
                    "reachable." + input.ActorId + "." + input.SourceId + "."
                        + subject.Id),
                input.Profile,
                input.ActorId,
                subject,
                intent ?? throw new ArgumentNullException(nameof(intent)));
            capabilities.RequireCandidateRoute(candidate);
            return candidate;
        }

        public IReadOnlyList<GameplayCandidate> BuildAll(
            IEnumerable<GameplayReachableInput> inputs)
        {
            if (inputs == null) throw new ArgumentNullException(nameof(inputs));
            var result = new List<GameplayCandidate>();
            foreach (GameplayReachableInput input in inputs)
                result.Add(Build(input));
            return result.AsReadOnly();
        }
    }

    public sealed class GameplayCandidateEvaluation
    {
        internal GameplayCandidateEvaluation(
            GameplayCandidate candidate,
            IEnumerable<string> requiredEvidenceTypes)
        {
            Candidate = candidate ?? throw new ArgumentNullException(
                nameof(candidate));
            RequiredEvidenceTypes = Copy(requiredEvidenceTypes);
        }

        public GameplayCandidate Candidate { get; }
        public IReadOnlyList<string> RequiredEvidenceTypes { get; }

        private static IReadOnlyList<string> Copy(IEnumerable<string> values)
        {
            var result = new List<string>(values ?? Array.Empty<string>());
            result.Sort(StringComparer.Ordinal);
            return result.AsReadOnly();
        }
    }

    public sealed class GameplayCandidateRouteEvaluator
    {
        private readonly GameplayCapabilityRegistry capabilities;

        public GameplayCandidateRouteEvaluator(
            GameplayCapabilityRegistry capabilityRegistry)
        {
            capabilities = capabilityRegistry ?? throw new ArgumentNullException(
                nameof(capabilityRegistry));
        }

        public GameplayCandidateEvaluation Evaluate(
            GameplayDecisionContext context,
            GameplayCandidate candidate)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (candidate == null)
                throw new ArgumentNullException(nameof(candidate));
            capabilities.RequireCandidateRoute(candidate);
            if (!string.Equals(
                    context.ActorId,
                    candidate.ActorId,
                    StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "A decision context cannot evaluate another actor's candidate.");
            _ = context.State.Session.GetActor(candidate.ActorId);
            return new GameplayCandidateEvaluation(
                candidate,
                RequiredEvidence(candidate.Profile));
        }

        private static IEnumerable<string> RequiredEvidence(
            GameplayCapabilityProfile profile)
        {
            switch (profile.Capability)
            {
                case GameplaySemanticCapability.Move:
                    yield return "movement-route";
                    break;
                case GameplaySemanticCapability.DirectAttack:
                case GameplaySemanticCapability.LaunchProjectile:
                    yield return "target-exposure";
                    break;
                case GameplaySemanticCapability.ThrowExplosive:
                    yield return "ballistic-landing";
                    yield return "blast-subjects";
                    break;
                case GameplaySemanticCapability.Displace:
                    yield return "displacement-path";
                    yield return "displacement-subject";
                    break;
                case GameplaySemanticCapability.AdvanceProjectile:
                    yield return "projectile-segment";
                    break;
                case GameplaySemanticCapability.VehicleMove:
                    yield return "vehicle-path";
                    break;
                case GameplaySemanticCapability.ObserveEncounter:
                    yield return "encounter-sight";
                    yield return "encounter-sound";
                    break;
                case GameplaySemanticCapability.Patrol:
                    yield return "patrol-route";
                    break;
            }
        }
    }

    public sealed class GameplaySemanticTransitionPreparer
    {
        private readonly GameplayCapabilityRegistry capabilities;

        public GameplaySemanticTransitionPreparer(
            GameplayCapabilityRegistry capabilityRegistry)
        {
            capabilities = capabilityRegistry ?? throw new ArgumentNullException(
                nameof(capabilityRegistry));
        }

        public GameplaySemanticTransition Prepare(
            GameplayDecisionContext context,
            GameplayCandidate candidate,
            GameplayTransitionPayload resolvedPayload,
            IEnumerable<GameplayEvidenceRecord> evidence = null)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (candidate == null)
                throw new ArgumentNullException(nameof(candidate));
            if (resolvedPayload == null)
                throw new ArgumentNullException(nameof(resolvedPayload));
            capabilities.RequireCandidateRoute(candidate);
            if (!candidate.Profile.Equals(resolvedPayload.Profile)
                || !string.Equals(
                    candidate.ActorId,
                    resolvedPayload.ActorId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    candidate.SubjectId,
                    resolvedPayload.SubjectId,
                    StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Resolved payload identity does not match its candidate.");
            var identity = new GameplayTransitionIdentity(
                context.State.Session.LastTransitionSequence + 1L,
                resolvedPayload.Profile.Capability.ToString(),
                resolvedPayload.ActorId,
                resolvedPayload.SubjectId);
            return new GameplaySemanticTransition(
                identity,
                context.State.CanonicalHash,
                resolvedPayload,
                ValidateEvidence(context.State, evidence));
        }

        private static IReadOnlyList<GameplayEvidenceRecord> ValidateEvidence(
            GameplayCombatStateSnapshot state,
            IEnumerable<GameplayEvidenceRecord> evidence)
        {
            var result = new List<GameplayEvidenceRecord>(
                evidence ?? Array.Empty<GameplayEvidenceRecord>());
            foreach (GameplayEvidenceRecord item in result)
            {
                if (item == null)
                    throw new ArgumentException(
                        "Transition evidence cannot contain null entries.",
                        nameof(evidence));
                if (item.WorldRevision > state.Session.Revision)
                    throw new InvalidOperationException(
                        $"Evidence '{item.EvidenceType}' comes from a future world revision.");
            }
            return result.AsReadOnly();
        }
    }

    public sealed class GameplayAtomicCombatStateStore
    {
        private GameplayCombatStateSnapshot current;

        public GameplayAtomicCombatStateStore(GameplayCombatStateSnapshot initial)
        {
            current = initial ?? throw new ArgumentNullException(nameof(initial));
            RequireValid(initial);
        }

        public GameplayCombatStateSnapshot Current => current;

        public event Action<GameplayDomainEvent> DomainEventPublished;

        public void Install(GameplayReductionResult reduction)
        {
            Install(reduction, afterRootSwap: null);
        }

        internal void Install(
            GameplayReductionResult reduction,
            Action<GameplayReductionResult> afterRootSwap)
        {
            if (reduction == null)
                throw new ArgumentNullException(nameof(reduction));
            if (!string.Equals(
                    current.CanonicalHash,
                    reduction.Previous.CanonicalHash,
                    StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "A reduced state cannot be installed over a different live state.");
            RequireValid(reduction.Resulting);

            current = reduction.Resulting;
            afterRootSwap?.Invoke(reduction);
            PublishAll(reduction.DomainEvents);
        }

        private void PublishAll(IReadOnlyList<GameplayDomainEvent> events)
        {
            Delegate[] listeners = DomainEventPublished?.GetInvocationList();
            if (listeners == null) return;
            var failures = new List<Exception>();
            foreach (GameplayDomainEvent domainEvent in events)
                foreach (Delegate listener in listeners)
                    try
                    {
                        ((Action<GameplayDomainEvent>)listener)(domainEvent);
                    }
                    catch (Exception exception)
                    {
                        failures.Add(exception);
                    }
            if (failures.Count > 0)
                throw new AggregateException(
                    "Authoritative state was installed, but presentation projection failed.",
                    failures);
        }

        private static void RequireValid(GameplayCombatStateSnapshot state)
        {
            IReadOnlyList<GameplayInvariantViolation> violations =
                GameplayCombatInvariantValidator.Validate(state);
            if (violations.Count > 0)
                throw new InvalidOperationException(
                    $"Canonical state violates '{violations[0].Code}' at "
                    + $"'{violations[0].Path}'.");
        }
    }

    public sealed class GameplaySemanticExecutionPipeline
    {
        private readonly GameplayTransitionReducerRegistry reducers;
        private readonly GameplayCapabilityRegistry capabilities;
        private readonly GameplayAtomicCombatStateStore stateStore;

        public GameplaySemanticExecutionPipeline(
            GameplayTransitionReducerRegistry reducerRegistry,
            GameplayCapabilityRegistry capabilityRegistry,
            GameplayAtomicCombatStateStore store)
        {
            reducers = reducerRegistry ?? throw new ArgumentNullException(
                nameof(reducerRegistry));
            capabilities = capabilityRegistry ?? throw new ArgumentNullException(
                nameof(capabilityRegistry));
            stateStore = store ?? throw new ArgumentNullException(nameof(store));
        }

        public GameplayReductionResult Execute(
            GameplaySemanticTransition transition)
        {
            if (transition == null)
                throw new ArgumentNullException(nameof(transition));
            capabilities.RequireCompleteRoute(transition.Profile);
            GameplayReductionResult result = reducers.Reduce(
                stateStore.Current,
                transition);
            stateStore.Install(result);
            return result;
        }
    }

    public static class GameplayCurrentCapabilityCatalog
    {
        public static GameplayCapabilityRegistry Create(
            GameplayTransitionReducerRegistry reducers,
            IEnumerable<GameplayReachableInput> reachableInputs)
        {
            if (reducers == null) throw new ArgumentNullException(nameof(reducers));
            if (reachableInputs == null)
                throw new ArgumentNullException(nameof(reachableInputs));
            var registry = new GameplayCapabilityRegistry(reducers);
            var signatures = new HashSet<string>(StringComparer.Ordinal);
            foreach (GameplayCapabilityProfile profile in FixedProfiles())
                Register(registry, signatures, profile);
            foreach (GameplayReachableInput input in reachableInputs)
            {
                if (input == null)
                    throw new ArgumentException(
                        "Reachable inputs cannot contain null entries.",
                        nameof(reachableInputs));
                Register(registry, signatures, input.Profile);
            }
            return registry;
        }

        public static GameplayCapabilityRegistry Create(
            GameplayTransitionReducerRegistry reducers,
            GameplayScenarioAssembly assembly,
            GritGud.Domain.Levels.LevelDocument level) => Create(
                reducers,
                GameplayReachableInputEnumerator.Enumerate(assembly, level));

        private static void RegisterComplete(
            GameplayCapabilityRegistry registry,
            GameplayCapabilityProfile profile)
        {
            registry.RegisterStage(profile,
                GameplayCapabilitySupportStage.CandidateConstruction,
                nameof(GameplayReachableCandidateBuilder));
            registry.RegisterStage(profile,
                GameplayCapabilitySupportStage.LegalityAndEvidence,
                nameof(GameplaySemanticTransitionPreparer));
            registry.RegisterStage(profile,
                GameplayCapabilitySupportStage.PureStateReduction,
                nameof(GameplayTransitionReducerRegistry));
            registry.RegisterStage(profile,
                GameplayCapabilitySupportStage.DomainEventProduction,
                nameof(GameplayReductionResult));
            registry.RegisterStage(profile,
                GameplayCapabilitySupportStage.ReplayEncodingAndReduction,
                nameof(GameplayExactReplay));
            registry.RegisterStage(profile,
                GameplayCapabilitySupportStage.HeadlessExecution,
                nameof(GameplaySimulationBranch));
            registry.RegisterStage(profile,
                GameplayCapabilitySupportStage.LiveInstallation,
                nameof(GameplayAtomicCombatStateStore));
        }

        private static void Register(
            GameplayCapabilityRegistry registry,
            ISet<string> signatures,
            GameplayCapabilityProfile profile)
        {
            if (!signatures.Add(profile.Signature)) return;
            RegisterComplete(registry, profile);
        }

        private static IEnumerable<GameplayCapabilityProfile> FixedProfiles()
        {
            yield return GameplayCapabilityProfiles.GroundedMove();
            yield return GameplayCapabilityProfiles.TraversalMove();
            yield return GameplayCapabilityProfiles.AerialDroneMove();
            yield return GameplayCapabilityProfiles.ChangeStance();
            yield return GameplayCapabilityProfiles.Equip();
            yield return GameplayCapabilityProfiles.Interact();
            yield return GameplayCapabilityProfiles.EndTurn(emergency: false);
            yield return GameplayCapabilityProfiles.EndTurn(emergency: true);
            yield return GameplayCapabilityProfiles.AdvanceProjectile();
            yield return GameplayCapabilityProfiles.VehicleMove();
            yield return GameplayCapabilityProfiles.AdvanceWorld(
                "continuous-time");
            yield return GameplayCapabilityProfiles.AdvanceWorld(
                "voluntary-cycle");
            yield return GameplayCapabilityProfiles.EmergencyReaction("begin");
            yield return GameplayCapabilityProfiles.EmergencyReaction(
                "complete");
            yield return GameplayCapabilityProfiles.ChangeTurnMode("enter");
            yield return GameplayCapabilityProfiles.ChangeTurnMode("exit");
            yield return GameplayCapabilityProfiles.ChangeEncounter("begin");
            yield return GameplayCapabilityProfiles.ChangeEncounter(
                "request-completion");
            yield return GameplayCapabilityProfiles.ChangeEncounter("complete");
            yield return GameplayCapabilityProfiles.ObserveEncounter();
            yield return GameplayCapabilityProfiles.Patrol();
        }
    }
}
