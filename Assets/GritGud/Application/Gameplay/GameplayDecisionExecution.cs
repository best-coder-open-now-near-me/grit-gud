using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace GritGud.Application.Gameplay
{
    public enum GameplayDecisionStage
    {
        CandidateConstruction,
        LegalityAndEvidence,
        Scoring,
        Preparation,
        Reduction,
        Installation,
    }

    public enum GameplayDecisionFailureKind
    {
        Cancelled,
        DeadlineExceeded,
        NoLegalCandidate,
        CandidateConstructionFailed,
        EvaluationFailed,
        ScoringFailed,
        PreparationFailed,
        ReductionFailed,
        InstallationFailed,
        StaleDecisionState,
        MaximumTransitionsExceeded,
        RepeatedCanonicalState,
        NoProgressTurn,
        UnresolvedMandatoryWork,
    }

    /// <summary>
    /// Wall-clock policy for one execution. These values are diagnostic safety
    /// limits only and never enter canonical state, transition identity, policy
    /// scoring, or artifact equality.
    /// </summary>
    public sealed class GameplayExecutionDeadlinePolicy
    {
        public GameplayExecutionDeadlinePolicy(
            TimeSpan candidateConstruction,
            TimeSpan legalityAndEvidence,
            TimeSpan scoring,
            TimeSpan preparation,
            TimeSpan reduction,
            TimeSpan installation,
            TimeSpan perDecision,
            TimeSpan perTurn,
            TimeSpan wholeBattle)
        {
            CandidateConstruction = RequirePositive(
                candidateConstruction,
                nameof(candidateConstruction));
            LegalityAndEvidence = RequirePositive(
                legalityAndEvidence,
                nameof(legalityAndEvidence));
            Scoring = RequirePositive(scoring, nameof(scoring));
            Preparation = RequirePositive(preparation, nameof(preparation));
            Reduction = RequirePositive(reduction, nameof(reduction));
            Installation = RequirePositive(installation, nameof(installation));
            PerDecision = RequirePositive(perDecision, nameof(perDecision));
            PerTurn = RequirePositive(perTurn, nameof(perTurn));
            WholeBattle = RequirePositive(wholeBattle, nameof(wholeBattle));
        }

        public TimeSpan CandidateConstruction { get; }
        public TimeSpan LegalityAndEvidence { get; }
        public TimeSpan Scoring { get; }
        public TimeSpan Preparation { get; }
        public TimeSpan Reduction { get; }
        public TimeSpan Installation { get; }
        public TimeSpan PerDecision { get; }
        public TimeSpan PerTurn { get; }
        public TimeSpan WholeBattle { get; }

        public TimeSpan ForStage(GameplayDecisionStage stage)
        {
            switch (stage)
            {
                case GameplayDecisionStage.CandidateConstruction:
                    return CandidateConstruction;
                case GameplayDecisionStage.LegalityAndEvidence:
                    return LegalityAndEvidence;
                case GameplayDecisionStage.Scoring:
                    return Scoring;
                case GameplayDecisionStage.Preparation:
                    return Preparation;
                case GameplayDecisionStage.Reduction:
                    return Reduction;
                case GameplayDecisionStage.Installation:
                    return Installation;
                default:
                    throw new ArgumentOutOfRangeException(nameof(stage));
            }
        }

        public static GameplayExecutionDeadlinePolicy Default { get; } =
            new GameplayExecutionDeadlinePolicy(
                TimeSpan.FromSeconds(2),
                // Permanent scenarios evaluate evidence for roughly two
                // thousand candidates. Keep this stage above the profiled
                // Depot peak while retaining the tighter limits elsewhere.
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(8),
                TimeSpan.FromSeconds(30),
                TimeSpan.FromMinutes(5));

        private static TimeSpan RequirePositive(TimeSpan value, string name)
        {
            if (value <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(name);
            return value;
        }
    }

    public sealed class GameplayExecutionLogicalGuardPolicy
    {
        public GameplayExecutionLogicalGuardPolicy(
            int maximumTransitions = 10000,
            int maximumRepeatedMaterialStates = 2,
            int maximumNoProgressTurns = 3)
        {
            if (maximumTransitions <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(maximumTransitions));
            if (maximumRepeatedMaterialStates <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(maximumRepeatedMaterialStates));
            if (maximumNoProgressTurns <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(maximumNoProgressTurns));
            MaximumTransitions = maximumTransitions;
            MaximumRepeatedMaterialStates = maximumRepeatedMaterialStates;
            MaximumNoProgressTurns = maximumNoProgressTurns;
        }

        public int MaximumTransitions { get; }
        public int MaximumRepeatedMaterialStates { get; }
        public int MaximumNoProgressTurns { get; }
    }

    public sealed class GameplayDecisionStageTiming
    {
        internal GameplayDecisionStageTiming(
            GameplayDecisionStage stage,
            TimeSpan elapsed,
            TimeSpan allowed)
        {
            Stage = stage;
            Elapsed = elapsed;
            Allowed = allowed;
        }

        public GameplayDecisionStage Stage { get; }
        public TimeSpan Elapsed { get; }
        public TimeSpan Allowed { get; }
    }

    public sealed class GameplayDecisionDiagnostic
    {
        internal GameplayDecisionDiagnostic(
            string actorId,
            string stateHash,
            GameplayDecisionStage? activeStage,
            IEnumerable<string> candidateIds,
            IEnumerable<string> legalCandidateIds,
            string selectedCandidateId,
            IEnumerable<GameplayDecisionStageTiming> timings)
        {
            ActorId = actorId ?? string.Empty;
            StateHash = stateHash ?? string.Empty;
            ActiveStage = activeStage;
            CandidateIds = Copy(candidateIds);
            LegalCandidateIds = Copy(legalCandidateIds);
            SelectedCandidateId = selectedCandidateId ?? string.Empty;
            Timings = new List<GameplayDecisionStageTiming>(
                timings ?? Array.Empty<GameplayDecisionStageTiming>())
                .AsReadOnly();
        }

        public string ActorId { get; }
        public string StateHash { get; }
        public GameplayDecisionStage? ActiveStage { get; }
        public IReadOnlyList<string> CandidateIds { get; }
        public IReadOnlyList<string> LegalCandidateIds { get; }
        public string SelectedCandidateId { get; }
        public IReadOnlyList<GameplayDecisionStageTiming> Timings { get; }

        private static IReadOnlyList<string> Copy(IEnumerable<string> values)
        {
            var copy = new List<string>(values ?? Array.Empty<string>());
            copy.Sort(StringComparer.Ordinal);
            return copy.AsReadOnly();
        }
    }

    public sealed class GameplayDecisionFailureException : Exception
    {
        public GameplayDecisionFailureException(
            GameplayDecisionFailureKind kind,
            string message,
            GameplayDecisionDiagnostic diagnostic,
            Exception innerException = null)
            : base(message, innerException)
        {
            if (!Enum.IsDefined(typeof(GameplayDecisionFailureKind), kind))
                throw new ArgumentOutOfRangeException(nameof(kind));
            Kind = kind;
            Diagnostic = diagnostic ?? throw new ArgumentNullException(
                nameof(diagnostic));
        }

        public GameplayDecisionFailureKind Kind { get; }
        public GameplayDecisionDiagnostic Diagnostic { get; }
    }

    /// <summary>
    /// Carries whole-battle and current-turn monotonic deadlines. The runner
    /// starts a fresh per-decision stopwatch for each decision.
    /// </summary>
    public sealed class GameplayExecutionDeadlineScope
    {
        private readonly Stopwatch battle = Stopwatch.StartNew();
        private Stopwatch turn;

        public void BeginTurn()
        {
            turn = Stopwatch.StartNew();
        }

        internal TimeSpan RemainingBattle(TimeSpan allowance) =>
            Remaining(allowance, battle.Elapsed);

        internal TimeSpan RemainingTurn(TimeSpan allowance) => turn == null
            ? throw new InvalidOperationException(
                "A turn deadline must begin before decisions execute.")
            : Remaining(allowance, turn.Elapsed);

        private static TimeSpan Remaining(
            TimeSpan allowance,
            TimeSpan elapsed) => elapsed >= allowance
                ? TimeSpan.Zero
                : allowance - elapsed;
    }

    internal sealed class GameplayDecisionDiagnosticBuilder
    {
        private readonly List<string> candidates = new List<string>();
        private readonly List<string> legal = new List<string>();
        private readonly List<GameplayDecisionStageTiming> timings =
            new List<GameplayDecisionStageTiming>();

        public GameplayDecisionDiagnosticBuilder(
            string actorId,
            string stateHash)
        {
            ActorId = actorId;
            StateHash = stateHash;
        }

        public string ActorId { get; }
        public string StateHash { get; }
        public GameplayDecisionStage? ActiveStage { get; set; }
        public string SelectedCandidateId { get; set; }

        public void SetCandidates(IEnumerable<GameplayCandidate> values)
        {
            candidates.Clear();
            foreach (GameplayCandidate value in values) candidates.Add(
                value.CandidateId);
        }

        public void SetLegal(
            IEnumerable<GameplayExecutableCandidateEvaluation> values)
        {
            legal.Clear();
            foreach (GameplayExecutableCandidateEvaluation value in values)
                legal.Add(value.Candidate.CandidateId);
        }

        public void AddTiming(
            GameplayDecisionStage stage,
            TimeSpan elapsed,
            TimeSpan allowed) => timings.Add(
                new GameplayDecisionStageTiming(stage, elapsed, allowed));

        public GameplayDecisionDiagnostic Build() =>
            new GameplayDecisionDiagnostic(
                ActorId,
                StateHash,
                ActiveStage,
                candidates,
                legal,
                SelectedCandidateId,
                timings);
    }
}
