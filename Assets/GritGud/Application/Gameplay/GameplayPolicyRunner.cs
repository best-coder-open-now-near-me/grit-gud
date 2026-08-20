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
            float value)
        {
            Evaluation = evaluation ?? throw new ArgumentNullException(
                nameof(evaluation));
            GameplayNumericPolicy.RequireFinite(value, nameof(value));
            Value = GameplayNumericPolicy.Normalize(value);
        }

        public GameplayExecutableCandidateEvaluation Evaluation { get; }
        public float Value { get; }
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
    public sealed class GameplayWeightedOutcomePolicy : IGameplayCandidatePolicy
    {
        private readonly IReadOnlyList<GameplayOutcomeFeatureWeight> weights;

        public GameplayWeightedOutcomePolicy(
            IEnumerable<GameplayOutcomeFeatureWeight> featureWeights)
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
        }

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
            foreach (GameplayOutcomeFeatureWeight weight in weights)
            {
                cancellationToken.ThrowIfCancellationRequested();
                value += evaluation.ExpectedOutcome.GetValue(weight.FeatureId)
                    * weight.Weight;
            }
            return new GameplayPolicyScore(evaluation, value);
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
                    "attack.wounds-on-hit", 10f),
                new GameplayOutcomeFeatureWeight(
                    "drone.integrity-damage", 10f),
                new GameplayOutcomeFeatureWeight(
                    "projectile.collision", 20f),
                new GameplayOutcomeFeatureWeight(
                    "projectile.launch", 12f),
                new GameplayOutcomeFeatureWeight(
                    "displacement.pinned", 8f),
                new GameplayOutcomeFeatureWeight(
                    "displacement.succeeded", 6f),
                new GameplayOutcomeFeatureWeight(
                    "concussive.affected-actors", 2f),
                new GameplayOutcomeFeatureWeight(
                    "hostile.distance-improvement", 2f),
                new GameplayOutcomeFeatureWeight(
                    "turn.saved-action-points", 0.5f),
                new GameplayOutcomeFeatureWeight(
                    "target.remaining-wound-capacity", -1f),
                new GameplayOutcomeFeatureWeight(
                    "cost.action-points", -1.5f),
                new GameplayOutcomeFeatureWeight(
                    "cost.movement-opportunity", -0.05f),
                new GameplayOutcomeFeatureWeight(
                    "hazard.fire-traversal", -20f),
                new GameplayOutcomeFeatureWeight(
                    "drone.move-distance", -0.01f),
            });
            return scenario == null
                ? weighted
                : new AuthoredEnemyPolicy(scenario, weighted);
        }

        private sealed class AuthoredEnemyPolicy : IGameplayCandidatePolicy
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
                return shouldCloseTurn
                    && evaluation.Candidate.Profile.Capability
                        == GameplaySemanticCapability.EndTurn
                    ? new GameplayPolicyScore(
                        evaluation,
                        checked(score.Value + 100000f))
                    : score;
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
            GameplaySemanticTransition transition,
            GameplayReductionResult reduction,
            GameplayDecisionDiagnostic diagnostic)
        {
            Selection = selection ?? throw new ArgumentNullException(
                nameof(selection));
            Transition = transition ?? throw new ArgumentNullException(
                nameof(transition));
            Reduction = reduction ?? throw new ArgumentNullException(
                nameof(reduction));
            Diagnostic = diagnostic ?? throw new ArgumentNullException(
                nameof(diagnostic));
        }

        public GameplayPolicyScore Selection { get; }
        public GameplaySemanticTransition Transition { get; }
        public GameplayReductionResult Reduction { get; }
        public GameplayDecisionDiagnostic Diagnostic { get; }
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

                GameplayPolicyScore selection = await RunWorker(
                    GameplayDecisionStage.Scoring,
                    token => Select(context, legal, token),
                    diagnostic,
                    decisionClock,
                    deadlineScope,
                    cancellationToken).ConfigureAwait(false);
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

        private GameplayPolicyScore Select(
            GameplayDecisionContext context,
            IReadOnlyList<GameplayExecutableCandidateEvaluation> legal,
            CancellationToken cancellationToken)
        {
            GameplayPolicyScore best = null;
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
                if (best == null
                    || score.Value > best.Value
                    || (score.Value == best.Value
                        && StringComparer.Ordinal.Compare(
                            score.Evaluation.Candidate.CandidateId,
                            best.Evaluation.Candidate.CandidateId) < 0))
                    best = score;
            }
            return best ?? throw new InvalidOperationException(
                "Policy selection requires a legal candidate.");
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
