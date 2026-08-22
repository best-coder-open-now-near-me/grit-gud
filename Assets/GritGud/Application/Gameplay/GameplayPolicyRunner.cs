using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using GritGud.Domain.Gameplay;

namespace GritGud.Application.Gameplay
{
    public interface IGameplayDecisionCandidateSource
    {
        IReadOnlyList<GameplayCandidate> Build(
            GameplayDecisionContext context,
            CancellationToken cancellationToken);
    }

    public sealed class GameplayHeadlessDecisionCandidateSource :
        IGameplayDecisionCandidateSource
    {
        private readonly GameplayHeadlessCandidateBuilder builder;
        private readonly IReadOnlyList<GameplayReachableInput> inputs;

        public GameplayHeadlessDecisionCandidateSource(
            GameplayHeadlessCandidateBuilder candidateBuilder,
            IEnumerable<GameplayReachableInput> reachableInputs,
            GameplayCandidateExecutionRouteRegistry routes)
        {
            builder = candidateBuilder ?? throw new ArgumentNullException(
                nameof(candidateBuilder));
            inputs = new List<GameplayReachableInput>(
                reachableInputs ?? throw new ArgumentNullException(
                    nameof(reachableInputs))).AsReadOnly();
            GameplayExecutableRouteCoverageReport coverage =
                GameplayExecutableRouteCoverageValidator.Validate(
                    inputs,
                    routes ?? throw new ArgumentNullException(nameof(routes)));
            coverage.RequireComplete();
        }

        public IReadOnlyList<GameplayCandidate> Build(
            GameplayDecisionContext context,
            CancellationToken cancellationToken)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyList<GameplayCandidate> result = builder.Build(
                context.State,
                inputs,
                context.ActorId);
            cancellationToken.ThrowIfCancellationRequested();
            return result;
        }
    }

    public sealed class GameplayPolicyScore
    {
        public GameplayPolicyScore(
            GameplayExecutableCandidateEvaluation evaluation,
            float value,
            IEnumerable<GameplayPolicyScoreComponent> components = null)
        {
            Evaluation = evaluation ?? throw new ArgumentNullException(
                nameof(evaluation));
            GameplayNumericPolicy.RequireFinite(value, nameof(value));
            Value = GameplayNumericPolicy.Normalize(value);
            var copied = new List<GameplayPolicyScoreComponent>(
                components ?? Array.Empty<GameplayPolicyScoreComponent>());
            copied.Sort((left, right) => StringComparer.Ordinal.Compare(
                left.FeatureId,
                right.FeatureId));
            for (int index = 0; index < copied.Count; index++)
            {
                if (copied[index] == null)
                    throw new ArgumentException(
                        "Policy score components cannot contain null entries.",
                        nameof(components));
                if (index > 0 && string.Equals(
                    copied[index - 1].FeatureId,
                    copied[index].FeatureId,
                    StringComparison.Ordinal))
                    throw new ArgumentException(
                        $"Policy score component '{copied[index].FeatureId}' is duplicated.",
                        nameof(components));
            }
            Components = copied.AsReadOnly();
        }

        public GameplayExecutableCandidateEvaluation Evaluation { get; }
        public float Value { get; }
        public IReadOnlyList<GameplayPolicyScoreComponent> Components { get; }
    }

    public sealed class GameplayPolicyScoreComponent
    {
        public GameplayPolicyScoreComponent(
            string featureId,
            float featureValue,
            float weight)
        {
            FeatureId = GameplayContentIdentity.RequireText(
                featureId,
                nameof(featureId));
            GameplayNumericPolicy.RequireFinite(
                featureValue,
                nameof(featureValue));
            GameplayNumericPolicy.RequireFinite(weight, nameof(weight));
            FeatureValue = GameplayNumericPolicy.Normalize(featureValue);
            Weight = GameplayNumericPolicy.Normalize(weight);
            Contribution = GameplayNumericPolicy.Normalize(
                FeatureValue * Weight);
        }

        public string FeatureId { get; }
        public float FeatureValue { get; }
        public float Weight { get; }
        public float Contribution { get; }
    }

    public interface IGameplayIdentifiedCandidatePolicy
    {
        string PolicyId { get; }
        int PolicyVersion { get; }
    }

    public interface IGameplayCandidatePolicy
    {
        GameplayPolicyScore Score(
            GameplayDecisionContext context,
            GameplayExecutableCandidateEvaluation evaluation,
            CancellationToken cancellationToken);
    }

    public sealed class GameplayOutcomeFeatureWeight
    {
        public GameplayOutcomeFeatureWeight(string featureId, float weight)
        {
            FeatureId = GameplayContentIdentity.RequireText(
                featureId,
                nameof(featureId));
            GameplayNumericPolicy.RequireFinite(weight, nameof(weight));
            Weight = GameplayNumericPolicy.Normalize(weight);
        }

        public string FeatureId { get; }
        public float Weight { get; }
    }

    /// <summary>
    /// A deterministic baseline policy over reducer-route outcome features.
    /// Feature weights tune valuation without changing authoritative rules.
    /// </summary>
    public sealed class GameplayWeightedOutcomePolicy :
        IGameplayCandidatePolicy,
        IGameplayIdentifiedCandidatePolicy
    {
        private readonly IReadOnlyList<GameplayOutcomeFeatureWeight> weights;

        public GameplayWeightedOutcomePolicy(
            IEnumerable<GameplayOutcomeFeatureWeight> featureWeights,
            string policyId = "policy.weighted-outcome",
            int policyVersion = 1)
        {
            var copy = new List<GameplayOutcomeFeatureWeight>(
                featureWeights ?? throw new ArgumentNullException(
                    nameof(featureWeights)));
            copy.Sort((left, right) => StringComparer.Ordinal.Compare(
                left.FeatureId,
                right.FeatureId));
            for (int index = 0; index < copy.Count; index++)
            {
                if (copy[index] == null)
                    throw new ArgumentException(
                        "Policy weights cannot contain null entries.",
                        nameof(featureWeights));
                if (index > 0 && string.Equals(
                    copy[index - 1].FeatureId,
                    copy[index].FeatureId,
                    StringComparison.Ordinal))
                    throw new ArgumentException(
                        $"Policy feature '{copy[index].FeatureId}' is duplicated.",
                        nameof(featureWeights));
            }
            weights = copy.AsReadOnly();
            PolicyId = GameplayContentIdentity.RequireText(
                policyId,
                nameof(policyId));
            if (policyVersion <= 0)
                throw new ArgumentOutOfRangeException(nameof(policyVersion));
            PolicyVersion = policyVersion;
        }

        public string PolicyId { get; }
        public int PolicyVersion { get; }

        public GameplayPolicyScore Score(
            GameplayDecisionContext context,
            GameplayExecutableCandidateEvaluation evaluation,
            CancellationToken cancellationToken)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (evaluation == null)
                throw new ArgumentNullException(nameof(evaluation));
            if (!evaluation.IsLegal)
                throw new ArgumentException(
                    "Policies may score only legal candidates.",
                    nameof(evaluation));
            cancellationToken.ThrowIfCancellationRequested();
            float value = 0f;
            var components = new List<GameplayPolicyScoreComponent>(
                weights.Count);
            foreach (GameplayOutcomeFeatureWeight weight in weights)
            {
                cancellationToken.ThrowIfCancellationRequested();
                float featureValue = evaluation.ExpectedOutcome.GetValue(
                    weight.FeatureId);
                var component = new GameplayPolicyScoreComponent(
                    weight.FeatureId,
                    featureValue,
                    weight.Weight);
                components.Add(component);
                value += component.Contribution;
            }
            return new GameplayPolicyScore(evaluation, value, components);
        }
    }

    /// <summary>
    /// Permanent deterministic baseline shared by live enemies and initial
    /// headless runs. These are valuation weights only; authoritative rules
    /// and tactical context remain in candidate evaluation and reducers.
    /// </summary>
    public static class GameplayBaselineCombatPolicy
    {
        public static IGameplayCandidatePolicy Create(
            ScenarioDefinition scenario = null)
        {
            var weighted = new GameplayWeightedOutcomePolicy(new[]
            {
                new GameplayOutcomeFeatureWeight(
                    "lifecycle.mandatory", 10000f),
                new GameplayOutcomeFeatureWeight(
                    "displacement.released", 100f),
                new GameplayOutcomeFeatureWeight(
                    "attack.hit-probability", 30f),
                new GameplayOutcomeFeatureWeight(
                    "attack.injury-on-hit", 10f),
                new GameplayOutcomeFeatureWeight(
                    "drone.integrity-damage", 10f),
                new GameplayOutcomeFeatureWeight(
                    "projectile.collision", 20f),
                new GameplayOutcomeFeatureWeight(
                    "projectile.launch", 12f),
                new GameplayOutcomeFeatureWeight(
                    "ammunition.reload-readiness", 100f),
                new GameplayOutcomeFeatureWeight(
                    "ammunition.reload-rounds", 1f),
                new GameplayOutcomeFeatureWeight(
                    "ammunition.reserve-depletion", -2f),
                new GameplayOutcomeFeatureWeight(
                    "displacement.pinned", 8f),
                new GameplayOutcomeFeatureWeight(
                    "displacement.succeeded", 6f),
                new GameplayOutcomeFeatureWeight(
                    "concussive.affected-actors", 2f),
                new GameplayOutcomeFeatureWeight(
                    "concussive.hostile-actors", 45f),
                new GameplayOutcomeFeatureWeight(
                    "concussive.friendly-actors", -40f),
                new GameplayOutcomeFeatureWeight(
                    "blast.hostile-actors", 10f),
                new GameplayOutcomeFeatureWeight(
                    "blast.friendly-actors", -40f),
                new GameplayOutcomeFeatureWeight(
                    "field.fire", 45f),
                new GameplayOutcomeFeatureWeight(
                    "field.fire-hostile-actors", 20f),
                new GameplayOutcomeFeatureWeight(
                    "field.fire-friendly-actors", -50f),
                new GameplayOutcomeFeatureWeight(
                    "attack.source-drone", 35f),
                new GameplayOutcomeFeatureWeight(
                    "drone.hostile-visibility-gain", 100f),
                new GameplayOutcomeFeatureWeight(
                    "drone.hostile-distance-improvement", 8f),
                new GameplayOutcomeFeatureWeight(
                    "hostile.distance-improvement", 2f),
                new GameplayOutcomeFeatureWeight(
                    "hostile.visibility-gain", 50f),
                new GameplayOutcomeFeatureWeight(
                    "concussive.hostile-range-gain", 40f),
                new GameplayOutcomeFeatureWeight(
                    "turn.saved-action-points", 0.5f),
                new GameplayOutcomeFeatureWeight(
                    "target.functional-reserve", -1f),
                new GameplayOutcomeFeatureWeight(
                    "cost.action-points", -1.5f),
                new GameplayOutcomeFeatureWeight(
                    "cost.movement-opportunity", -0.05f),
                new GameplayOutcomeFeatureWeight(
                    "hazard.fire-traversal", -20f),
                new GameplayOutcomeFeatureWeight(
                    "drone.move-distance", -0.01f),
            }, "policy.baseline-combat", policyVersion: 2);
            return scenario == null
                ? weighted
                : new AuthoredEnemyPolicy(scenario, weighted);
        }

        private sealed class AuthoredEnemyPolicy :
            IGameplayCandidatePolicy,
            IGameplayIdentifiedCandidatePolicy
        {
            private readonly ScenarioDefinition scenario;
            private readonly GameplayWeightedOutcomePolicy weighted;

            public AuthoredEnemyPolicy(
                ScenarioDefinition scenarioDefinition,
                GameplayWeightedOutcomePolicy weightedPolicy)
            {
                scenario = scenarioDefinition ?? throw new ArgumentNullException(
                    nameof(scenarioDefinition));
                weighted = weightedPolicy ?? throw new ArgumentNullException(
                    nameof(weightedPolicy));
            }

            public string PolicyId => "policy.baseline-combat";
            public int PolicyVersion => 2;

            public GameplayPolicyScore Score(
                GameplayDecisionContext context,
                GameplayExecutableCandidateEvaluation evaluation,
                CancellationToken cancellationToken)
            {
                GameplayPolicyScore score = weighted.Score(
                    context,
                    evaluation,
                    cancellationToken);
                GameplayActorSnapshot actor = context.State.Session.GetActor(
                    context.ActorId);
                EnemyBehaviorDefinition behavior = scenario.GetActor(
                    context.ActorId).Combat.EnemyBehavior;
                bool shouldCloseTurn = behavior != null
                    && actor.AttacksCommittedThisTurn
                        >= behavior.MaximumAttacksPerTurn
                    && !actor.IsPinned
                    && !GameplayMandatoryWorkRules.HasPending(context.State);
                var additions = new List<GameplayPolicyScoreComponent>();
                if (shouldCloseTurn
                    && evaluation.Candidate.Profile.Capability
                        == GameplaySemanticCapability.EndTurn)
                    additions.Add(new GameplayPolicyScoreComponent(
                        "authored.attack-cap.end-turn",
                        1f,
                        100000f));
                if (evaluation.ExpectedOutcome.GetValue("field.fire") > 0f
                    && context.State.FireFields.Count > 0)
                    additions.Add(new GameplayPolicyScoreComponent(
                        "context.fire-field-already-active",
                        1f,
                        -100f));
                if (additions.Count == 0) return score;
                float adjusted = score.Value;
                foreach (GameplayPolicyScoreComponent addition in additions)
                    adjusted += addition.Contribution;
                return new GameplayPolicyScore(
                    evaluation,
                    adjusted,
                    AddComponents(score.Components, additions));
            }

            private static IEnumerable<GameplayPolicyScoreComponent>
                AddComponents(
                    IEnumerable<GameplayPolicyScoreComponent> existing,
                    IEnumerable<GameplayPolicyScoreComponent> additions)
            {
                var result = new List<GameplayPolicyScoreComponent>(existing);
                result.AddRange(additions);
                return result;
            }
        }
    }

    public interface IGameplayDecisionWorkerBoundary
    {
        Task<T> RunAsync<T>(
            Func<CancellationToken, T> work,
            CancellationToken cancellationToken);
    }

    /// <summary>
    /// Cancellable in-process boundary for live decisions and trusted baseline
    /// policies. Unattended optimizer policies can replace this with a process
    /// boundary capable of terminating non-cooperative work.
    /// </summary>
    public sealed class GameplayTaskDecisionWorkerBoundary :
        IGameplayDecisionWorkerBoundary
    {
        public Task<T> RunAsync<T>(
            Func<CancellationToken, T> work,
            CancellationToken cancellationToken)
        {
            if (work == null) throw new ArgumentNullException(nameof(work));
            return Task.Run(() => work(cancellationToken), cancellationToken);
        }
    }

    /// <summary>
    /// Cooperative scheduling boundary for single-threaded runtimes such as
    /// WebGL. It preserves the permanent decision pipeline while yielding
    /// between measured stages so the host can render and process cancellation.
    /// Trusted work still runs synchronously after each yield; untrusted
    /// optimizer policies require the process boundary described by the runner
    /// contract.
    /// </summary>
    public sealed class GameplayCooperativeDecisionWorkerBoundary :
        IGameplayDecisionWorkerBoundary
    {
        public async Task<T> RunAsync<T>(
            Func<CancellationToken, T> work,
            CancellationToken cancellationToken)
        {
            if (work == null) throw new ArgumentNullException(nameof(work));
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            return work(cancellationToken);
        }
    }

    public interface IGameplayRuntimeInstallationBoundary
    {
        Task<GameplayReductionResult> InstallAsync(
            GameplaySimulationRuntime runtime,
            GameplaySemanticTransition transition,
            GameplayReductionResult reduction,
            CancellationToken cancellationToken);
    }

    public sealed class GameplayImmediateRuntimeInstallationBoundary :
        IGameplayRuntimeInstallationBoundary
    {
        public Task<GameplayReductionResult> InstallAsync(
            GameplaySimulationRuntime runtime,
            GameplaySemanticTransition transition,
            GameplayReductionResult reduction,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            GameplayReductionResult installed = (runtime
                ?? throw new ArgumentNullException(nameof(runtime)))
                .InstallPreparedReduction(transition, reduction);
            return Task.FromResult(installed);
        }
    }

    public sealed class GameplayDecisionExecutionResult
    {
        internal GameplayDecisionExecutionResult(
            GameplayPolicyScore selection,
            GameplayPolicySelectionReason selectionReason,
            GameplaySemanticTransition transition,
            GameplayReductionResult reduction,
            GameplayDecisionDiagnostic diagnostic)
        {
            Selection = selection ?? throw new ArgumentNullException(
                nameof(selection));
            if (!Enum.IsDefined(
                    typeof(GameplayPolicySelectionReason),
                    selectionReason))
                throw new ArgumentOutOfRangeException(nameof(selectionReason));
            SelectionReason = selectionReason;
            Transition = transition ?? throw new ArgumentNullException(
                nameof(transition));
            Reduction = reduction ?? throw new ArgumentNullException(
                nameof(reduction));
            Diagnostic = diagnostic ?? throw new ArgumentNullException(
                nameof(diagnostic));
        }

        public GameplayPolicyScore Selection { get; }
        public GameplayPolicySelectionReason SelectionReason { get; }
        public GameplaySemanticTransition Transition { get; }
        public GameplayReductionResult Reduction { get; }
        public GameplayDecisionDiagnostic Diagnostic { get; }
    }

    public enum GameplayPolicySelectionReason
    {
        HighestScore,
        StableCandidateIdTieBreak,
    }

    internal sealed class GameplayPolicySelection
    {
        public GameplayPolicySelection(
            GameplayPolicyScore score,
            GameplayPolicySelectionReason reason)
        {
            Score = score ?? throw new ArgumentNullException(nameof(score));
            Reason = reason;
        }

        public GameplayPolicyScore Score { get; }
        public GameplayPolicySelectionReason Reason { get; }
    }

    /// <summary>
    /// Permanent policy-neutral decision spine. Every stage is measured and
    /// deadline-guarded; only the installation stage mutates authoritative state.
    /// </summary>
    public sealed class GameplayPolicyDecisionRunner
    {
        private readonly IGameplayDecisionCandidateSource candidateSource;
        private readonly GameplayCandidateExecutionRouteRegistry routes;
        private readonly IGameplayCandidatePolicy policy;
        private readonly IGameplayDecisionWorkerBoundary worker;
        private readonly IGameplayRuntimeInstallationBoundary installer;
        private readonly GameplayExecutionDeadlinePolicy deadlines;

        public GameplayPolicyDecisionRunner(
            IGameplayDecisionCandidateSource source,
            GameplayCandidateExecutionRouteRegistry routeRegistry,
            IGameplayCandidatePolicy candidatePolicy,
            IGameplayDecisionWorkerBoundary workerBoundary = null,
            IGameplayRuntimeInstallationBoundary installationBoundary = null,
            GameplayExecutionDeadlinePolicy deadlinePolicy = null)
        {
            candidateSource = source ?? throw new ArgumentNullException(
                nameof(source));
            routes = routeRegistry ?? throw new ArgumentNullException(
                nameof(routeRegistry));
            policy = candidatePolicy ?? throw new ArgumentNullException(
                nameof(candidatePolicy));
            worker = workerBoundary
                ?? new GameplayTaskDecisionWorkerBoundary();
            installer = installationBoundary
                ?? new GameplayImmediateRuntimeInstallationBoundary();
            deadlines = deadlinePolicy
                ?? GameplayExecutionDeadlinePolicy.Default;
        }

        public async Task<GameplayDecisionExecutionResult> ExecuteAsync(
            GameplaySimulationRuntime runtime,
            GameplayObservationSnapshot observation,
            GameplayExecutionDeadlineScope deadlineScope,
            GameplayExecutionLogicalGuard logicalGuard = null,
            CancellationToken cancellationToken = default)
        {
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));
            if (observation == null)
                throw new ArgumentNullException(nameof(observation));
            if (deadlineScope == null)
            {
                deadlineScope = new GameplayExecutionDeadlineScope();
                deadlineScope.BeginTurn();
            }
            GameplayCombatStateSnapshot state = runtime.CurrentState;
            var context = new GameplayDecisionContext(state, observation);
            var diagnostic = new GameplayDecisionDiagnosticBuilder(
                context.ActorId,
                state.CanonicalHash);
            var decisionClock = Stopwatch.StartNew();
            try
            {
                IReadOnlyList<GameplayCandidate> candidates = await RunWorker(
                    GameplayDecisionStage.CandidateConstruction,
                    token => candidateSource.Build(context, token),
                    diagnostic,
                    decisionClock,
                    deadlineScope,
                    cancellationToken).ConfigureAwait(false);
                diagnostic.SetCandidates(candidates);

                IReadOnlyList<GameplayExecutableCandidateEvaluation> legal =
                    await RunWorker(
                        GameplayDecisionStage.LegalityAndEvidence,
                        token => Evaluate(context, candidates, token),
                        diagnostic,
                        decisionClock,
                        deadlineScope,
                        cancellationToken).ConfigureAwait(false);
                diagnostic.SetLegal(legal);
                if (legal.Count == 0)
                    throw Failure(
                        GameplayDecisionFailureKind.NoLegalCandidate,
                        "Decision produced no legal candidate.",
                        diagnostic);

                GameplayPolicySelection selected = await RunWorker(
                    GameplayDecisionStage.Scoring,
                    token => Select(context, legal, token),
                    diagnostic,
                    decisionClock,
                    deadlineScope,
                    cancellationToken).ConfigureAwait(false);
                GameplayPolicyScore selection = selected.Score;
                diagnostic.SelectedCandidateId = selection.Evaluation
                    .Candidate.CandidateId;

                GameplaySemanticTransition transition = await RunWorker(
                    GameplayDecisionStage.Preparation,
                    token =>
                    {
                        token.ThrowIfCancellationRequested();
                        return routes.Prepare(context, selection.Evaluation);
                    },
                    diagnostic,
                    decisionClock,
                    deadlineScope,
                    cancellationToken).ConfigureAwait(false);

                GameplayReductionResult reduction = await RunWorker(
                    GameplayDecisionStage.Reduction,
                    token =>
                    {
                        token.ThrowIfCancellationRequested();
                        return runtime.PrepareReduction(transition);
                    },
                    diagnostic,
                    decisionClock,
                    deadlineScope,
                    cancellationToken).ConfigureAwait(false);
                if (logicalGuard != null
                    && transition.Profile.Capability
                        == GameplaySemanticCapability.EndTurn)
                {
                    logicalGuard.CompleteTurn(
                        state,
                        GameplayMandatoryWorkRules.HasPending(state));
                }
                logicalGuard?.ValidatePreparedTransition(reduction);

                GameplayReductionResult installed = await RunInstallation(
                    runtime,
                    transition,
                    reduction,
                    diagnostic,
                    decisionClock,
                    deadlineScope,
                    cancellationToken).ConfigureAwait(false);
                diagnostic.ActiveStage = null;
                return new GameplayDecisionExecutionResult(
                    selection,
                    selected.Reason,
                    transition,
                    installed,
                    diagnostic.Build());
            }
            catch (GameplayDecisionFailureException)
            {
                throw;
            }
            catch (GameplayStaleDecisionStateException exception)
            {
                throw new GameplayDecisionFailureException(
                    GameplayDecisionFailureKind.StaleDecisionState,
                    "Decision was prepared from a stale canonical state.",
                    diagnostic.Build(),
                    exception);
            }
            catch (OperationCanceledException exception)
            {
                throw new GameplayDecisionFailureException(
                    GameplayDecisionFailureKind.Cancelled,
                    "Decision execution was cancelled.",
                    diagnostic.Build(),
                    exception);
            }
            catch (Exception exception)
            {
                throw new GameplayDecisionFailureException(
                    FailureFor(diagnostic.ActiveStage),
                    "Decision execution failed during "
                        + diagnostic.ActiveStage + ".",
                    diagnostic.Build(),
                    exception);
            }
        }

        private IReadOnlyList<GameplayExecutableCandidateEvaluation> Evaluate(
            GameplayDecisionContext context,
            IReadOnlyList<GameplayCandidate> candidates,
            CancellationToken cancellationToken)
        {
            var legal = new List<GameplayExecutableCandidateEvaluation>();
            foreach (GameplayCandidate candidate in candidates)
            {
                cancellationToken.ThrowIfCancellationRequested();
                GameplayExecutableCandidateEvaluation evaluation =
                    routes.Evaluate(context, candidate);
                if (evaluation.IsLegal) legal.Add(evaluation);
            }
            legal.Sort((left, right) => StringComparer.Ordinal.Compare(
                left.Candidate.CandidateId,
                right.Candidate.CandidateId));
            return legal.AsReadOnly();
        }

        private GameplayPolicySelection Select(
            GameplayDecisionContext context,
            IReadOnlyList<GameplayExecutableCandidateEvaluation> legal,
            CancellationToken cancellationToken)
        {
            GameplayPolicyScore best = null;
            bool selectedByTieBreak = false;
            foreach (GameplayExecutableCandidateEvaluation evaluation in legal)
            {
                cancellationToken.ThrowIfCancellationRequested();
                GameplayPolicyScore score = policy.Score(
                    context,
                    evaluation,
                    cancellationToken);
                if (!ReferenceEquals(score.Evaluation, evaluation))
                    throw new InvalidOperationException(
                        "Policy returned a score for a different candidate evaluation.");
                if (best == null || score.Value > best.Value)
                {
                    best = score;
                    selectedByTieBreak = false;
                }
                else if (score.Value == best.Value)
                {
                    selectedByTieBreak = true;
                    if (StringComparer.Ordinal.Compare(
                            score.Evaluation.Candidate.CandidateId,
                            best.Evaluation.Candidate.CandidateId) < 0)
                        best = score;
                }
            }
            return new GameplayPolicySelection(
                best ?? throw new InvalidOperationException(
                    "Policy selection requires a legal candidate."),
                selectedByTieBreak
                    ? GameplayPolicySelectionReason
                        .StableCandidateIdTieBreak
                    : GameplayPolicySelectionReason.HighestScore);
        }

        private Task<T> RunWorker<T>(
            GameplayDecisionStage stage,
            Func<CancellationToken, T> work,
            GameplayDecisionDiagnosticBuilder diagnostic,
            Stopwatch decisionClock,
            GameplayExecutionDeadlineScope deadlineScope,
            CancellationToken cancellationToken) => RunStage(
                stage,
                token => worker.RunAsync(work, token),
                diagnostic,
                decisionClock,
                deadlineScope,
                cancellationToken);

        private Task<GameplayReductionResult> RunInstallation(
            GameplaySimulationRuntime runtime,
            GameplaySemanticTransition transition,
            GameplayReductionResult reduction,
            GameplayDecisionDiagnosticBuilder diagnostic,
            Stopwatch decisionClock,
            GameplayExecutionDeadlineScope deadlineScope,
            CancellationToken cancellationToken) => RunStage(
                GameplayDecisionStage.Installation,
                token => installer.InstallAsync(
                    runtime,
                    transition,
                    reduction,
                    token),
                diagnostic,
                decisionClock,
                deadlineScope,
                cancellationToken);

        private async Task<T> RunStage<T>(
            GameplayDecisionStage stage,
            Func<CancellationToken, Task<T>> start,
            GameplayDecisionDiagnosticBuilder diagnostic,
            Stopwatch decisionClock,
            GameplayExecutionDeadlineScope deadlineScope,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            diagnostic.ActiveStage = stage;
            TimeSpan allowed = Minimum(
                deadlines.ForStage(stage),
                Remaining(deadlines.PerDecision, decisionClock.Elapsed),
                deadlineScope.RemainingTurn(deadlines.PerTurn),
                deadlineScope.RemainingBattle(deadlines.WholeBattle));
            if (allowed <= TimeSpan.Zero)
                throw Failure(
                    GameplayDecisionFailureKind.DeadlineExceeded,
                    "Decision deadline was exhausted before " + stage + ".",
                    diagnostic);

            var stageClock = Stopwatch.StartNew();
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken))
            {
                Task<T> work;
                try
                {
                    work = start(linked.Token);
                }
                catch
                {
                    stageClock.Stop();
                    diagnostic.AddTiming(stage, stageClock.Elapsed, allowed);
                    throw;
                }
                Task delay = Task.Delay(allowed, cancellationToken);
                Task completed = await Task.WhenAny(work, delay)
                    .ConfigureAwait(false);
                stageClock.Stop();
                diagnostic.AddTiming(stage, stageClock.Elapsed, allowed);
                if (!ReferenceEquals(completed, work))
                {
                    linked.Cancel();
                    ObserveEventually(work);
                    cancellationToken.ThrowIfCancellationRequested();
                    throw Failure(
                        GameplayDecisionFailureKind.DeadlineExceeded,
                        stage + " exceeded its monotonic deadline.",
                        diagnostic);
                }
                return await work.ConfigureAwait(false);
            }
        }

        private static void ObserveEventually<T>(Task<T> task)
        {
            _ = task.ContinueWith(
                completed => _ = completed.Exception,
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted
                    | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        private static GameplayDecisionFailureException Failure(
            GameplayDecisionFailureKind kind,
            string message,
            GameplayDecisionDiagnosticBuilder diagnostic) =>
            new GameplayDecisionFailureException(
                kind,
                message,
                diagnostic.Build());

        private static GameplayDecisionFailureKind FailureFor(
            GameplayDecisionStage? stage)
        {
            switch (stage)
            {
                case GameplayDecisionStage.CandidateConstruction:
                    return GameplayDecisionFailureKind
                        .CandidateConstructionFailed;
                case GameplayDecisionStage.LegalityAndEvidence:
                    return GameplayDecisionFailureKind.EvaluationFailed;
                case GameplayDecisionStage.Scoring:
                    return GameplayDecisionFailureKind.ScoringFailed;
                case GameplayDecisionStage.Preparation:
                    return GameplayDecisionFailureKind.PreparationFailed;
                case GameplayDecisionStage.Reduction:
                    return GameplayDecisionFailureKind.ReductionFailed;
                case GameplayDecisionStage.Installation:
                    return GameplayDecisionFailureKind.InstallationFailed;
                default:
                    return GameplayDecisionFailureKind.CandidateConstructionFailed;
            }
        }

        private static TimeSpan Remaining(
            TimeSpan allowance,
            TimeSpan elapsed) => elapsed >= allowance
                ? TimeSpan.Zero
                : allowance - elapsed;

        private static TimeSpan Minimum(params TimeSpan[] values)
        {
            TimeSpan result = values[0];
            for (int index = 1; index < values.Length; index++)
                if (values[index] < result) result = values[index];
            return result;
        }
    }
}
