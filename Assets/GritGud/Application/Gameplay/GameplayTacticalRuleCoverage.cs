using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;

namespace GritGud.Application.Gameplay
{
    [Flags]
    public enum GameplayTacticalRuleSupportStage
    {
        None = 0,
        LiveEvidence = 1 << 0,
        HeadlessEvidence = 1 << 1,
        PredicateEvaluation = 1 << 2,
        ReducerConsequences = 1 << 3,
        ReplayEncoding = 1 << 4,
        DiagnosticProjection = 1 << 5,
        OutcomeProjection = 1 << 6,
        Complete = LiveEvidence | HeadlessEvidence | PredicateEvaluation
            | ReducerConsequences | ReplayEncoding | DiagnosticProjection
            | OutcomeProjection,
    }

    public sealed class GameplayTacticalRuleRoute
    {
        private readonly HashSet<TacticalContextFeature> predicateFeatures =
            new HashSet<TacticalContextFeature>();
        private readonly HashSet<string> consequenceFeatures =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> outcomeFeatures =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<GameplayTacticalRuleSupportStage, string>
            implementations = new Dictionary<
                GameplayTacticalRuleSupportStage,
                string>();

        internal GameplayTacticalRuleRoute(
            string ruleId,
            string capabilitySignature,
            GameplaySemanticSubjectKind subjectKind)
        {
            RuleId = GameplayContentIdentity.RequireText(ruleId, nameof(ruleId));
            CapabilitySignature = GameplayContentIdentity.RequireText(
                capabilitySignature,
                nameof(capabilitySignature));
            if (!Enum.IsDefined(
                    typeof(GameplaySemanticSubjectKind),
                    subjectKind))
                throw new ArgumentOutOfRangeException(nameof(subjectKind));
            SubjectKind = subjectKind;
        }

        public string RuleId { get; }
        public string CapabilitySignature { get; }
        public GameplaySemanticSubjectKind SubjectKind { get; }
        public GameplayTacticalRuleSupportStage Stages { get; private set; }
        public IReadOnlyDictionary<GameplayTacticalRuleSupportStage, string>
            Implementations => implementations;

        internal void Register(
            GameplayTacticalRuleSupportStage stage,
            string implementationId,
            IEnumerable<TacticalContextFeature> predicates,
            IEnumerable<string> consequences,
            IEnumerable<string> outcomes)
        {
            int value = (int)stage;
            if (value <= 0 || (value & (value - 1)) != 0)
                throw new ArgumentException(
                    "Tactical support stages must be registered individually.",
                    nameof(stage));
            if (implementations.ContainsKey(stage))
                throw new InvalidOperationException(
                    $"Tactical route '{RuleId}' already registered '{stage}'.");
            implementations.Add(
                stage,
                GameplayContentIdentity.RequireText(
                    implementationId,
                    nameof(implementationId)));
            Stages |= stage;
            AddPredicates(predicateFeatures, predicates);
            AddText(consequenceFeatures, consequences);
            AddText(outcomeFeatures, outcomes);
        }

        internal bool SupportsPredicate(TacticalContextFeature feature) =>
            predicateFeatures.Contains(feature);
        internal bool SupportsConsequence(string feature) =>
            consequenceFeatures.Contains(feature);
        internal bool SupportsOutcome(string feature) =>
            outcomeFeatures.Contains(feature);

        private static void AddPredicates(
            ISet<TacticalContextFeature> destination,
            IEnumerable<TacticalContextFeature> values)
        {
            foreach (TacticalContextFeature value in
                values ?? Array.Empty<TacticalContextFeature>())
            {
                if (!Enum.IsDefined(typeof(TacticalContextFeature), value))
                    throw new ArgumentOutOfRangeException(nameof(values));
                destination.Add(value);
            }
        }

        private static void AddText(
            ISet<string> destination,
            IEnumerable<string> values)
        {
            foreach (string value in values ?? Array.Empty<string>())
                destination.Add(GameplayContentIdentity.RequireText(
                    value,
                    nameof(values)));
        }
    }

    public sealed class GameplayTacticalRuleSupportRegistry
    {
        private readonly Dictionary<string, GameplayTacticalRuleRoute> routes =
            new Dictionary<string, GameplayTacticalRuleRoute>(
                StringComparer.Ordinal);

        public IReadOnlyCollection<GameplayTacticalRuleRoute> Routes =>
            routes.Values;

        public void RegisterStage(
            string ruleId,
            string capabilitySignature,
            GameplaySemanticSubjectKind subjectKind,
            GameplayTacticalRuleSupportStage stage,
            string implementationId,
            IEnumerable<TacticalContextFeature> predicateFeatures = null,
            IEnumerable<string> consequenceFeatures = null,
            IEnumerable<string> outcomeFeatures = null)
        {
            string key = Key(ruleId, capabilitySignature, subjectKind);
            if (!routes.TryGetValue(key, out GameplayTacticalRuleRoute route))
            {
                route = new GameplayTacticalRuleRoute(
                    ruleId,
                    capabilitySignature,
                    subjectKind);
                routes.Add(key, route);
            }
            route.Register(
                stage,
                implementationId,
                predicateFeatures,
                consequenceFeatures,
                outcomeFeatures);
        }

        public bool TryGet(
            string ruleId,
            string capabilitySignature,
            GameplaySemanticSubjectKind subjectKind,
            out GameplayTacticalRuleRoute route) => routes.TryGetValue(
                Key(ruleId, capabilitySignature, subjectKind),
                out route);

        private static string Key(
            string ruleId,
            string signature,
            GameplaySemanticSubjectKind kind) =>
            ruleId + "\n" + signature + "\n" + kind;
    }

    public sealed class GameplayTacticalRuleCoverageIssue
    {
        public GameplayTacticalRuleCoverageIssue(
            string code,
            string ruleId,
            string capabilitySignature,
            GameplaySemanticSubjectKind subjectKind,
            GameplayTacticalRuleSupportStage missingStages,
            IEnumerable<string> missingFeatures = null,
            bool blocking = true)
        {
            Code = GameplayContentIdentity.RequireText(code, nameof(code));
            RuleId = GameplayContentIdentity.RequireText(ruleId, nameof(ruleId));
            CapabilitySignature = GameplayContentIdentity.RequireText(
                capabilitySignature,
                nameof(capabilitySignature));
            SubjectKind = subjectKind;
            MissingStages = missingStages;
            MissingFeatures = new List<string>(
                missingFeatures ?? Array.Empty<string>()).AsReadOnly();
            IsBlocking = blocking;
        }

        public string Code { get; }
        public string RuleId { get; }
        public string CapabilitySignature { get; }
        public GameplaySemanticSubjectKind SubjectKind { get; }
        public GameplayTacticalRuleSupportStage MissingStages { get; }
        public IReadOnlyList<string> MissingFeatures { get; }
        public bool IsBlocking { get; }
    }

    public sealed class GameplayTacticalRuleCoverageReport
    {
        internal GameplayTacticalRuleCoverageReport(
            IEnumerable<GameplayTacticalRuleCoverageIssue> issues)
        {
            Issues = new List<GameplayTacticalRuleCoverageIssue>(issues)
                .AsReadOnly();
        }

        public IReadOnlyList<GameplayTacticalRuleCoverageIssue> Issues { get; }
        public bool IsComplete
        {
            get
            {
                foreach (GameplayTacticalRuleCoverageIssue issue in Issues)
                    if (issue.IsBlocking) return false;
                return true;
            }
        }

        public void RequireComplete(string scenarioId)
        {
            if (IsComplete) return;
            var details = new List<string>();
            foreach (GameplayTacticalRuleCoverageIssue issue in Issues)
            {
                if (!issue.IsBlocking) continue;
                details.Add(
                    $"{issue.Code}: {issue.RuleId} -> {issue.CapabilitySignature}"
                    + $"/{issue.SubjectKind} missing {issue.MissingStages}"
                    + (issue.MissingFeatures.Count == 0
                        ? string.Empty
                        : " [" + string.Join(",", issue.MissingFeatures) + "]"));
            }
            throw new InvalidOperationException(
                $"Scenario '{scenarioId}' has incomplete tactical-rule coverage: "
                + string.Join(" | ", details));
        }
    }

    public static class GameplayTacticalRuleCoverageValidator
    {
        public const string AccuracyDelta = "consequence.accuracy-delta";

        public static GameplayTacticalRuleCoverageReport Validate(
            IEnumerable<TacticalContextRuleDefinition> definitions,
            IEnumerable<GameplayReachableInput> reachableInputs,
            GameplayTacticalRuleSupportRegistry registry)
        {
            if (definitions == null)
                throw new ArgumentNullException(nameof(definitions));
            if (reachableInputs == null)
                throw new ArgumentNullException(nameof(reachableInputs));
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            var reachable = new HashSet<string>(StringComparer.Ordinal);
            foreach (GameplayReachableInput input in reachableInputs)
                reachable.Add(input.Profile.Signature + "\n" + input.SubjectKind);
            var issues = new List<GameplayTacticalRuleCoverageIssue>();
            foreach (TacticalContextRuleDefinition rule in definitions)
            {
                foreach (string signature in rule.ApplicableCapabilitySignatures)
                foreach (GameplaySemanticSubjectKind subject in
                    rule.AcceptedSubjectKinds)
                {
                    bool isReachable = reachable.Contains(
                        signature + "\n" + subject);
                    if (!isReachable)
                    {
                        issues.Add(new GameplayTacticalRuleCoverageIssue(
                            "tactical-rule.unreachable",
                            rule.RuleId,
                            signature,
                            subject,
                            GameplayTacticalRuleSupportStage.None,
                            blocking: false));
                        continue;
                    }
                    ValidateRoute(rule, signature, subject, registry, issues);
                }
            }
            return new GameplayTacticalRuleCoverageReport(issues);
        }

        private static void ValidateRoute(
            TacticalContextRuleDefinition rule,
            string signature,
            GameplaySemanticSubjectKind subject,
            GameplayTacticalRuleSupportRegistry registry,
            ICollection<GameplayTacticalRuleCoverageIssue> issues)
        {
            if (!registry.TryGet(rule.RuleId, signature, subject, out
                    GameplayTacticalRuleRoute route))
            {
                issues.Add(new GameplayTacticalRuleCoverageIssue(
                    "tactical-rule.missing-route",
                    rule.RuleId,
                    signature,
                    subject,
                    GameplayTacticalRuleSupportStage.Complete));
                return;
            }
            GameplayTacticalRuleSupportStage missing =
                GameplayTacticalRuleSupportStage.Complete & ~route.Stages;
            var missingFeatures = new List<string>();
            foreach (TacticalContextPredicate predicate in rule.Predicates)
                if (!route.SupportsPredicate(predicate.Feature))
                    missingFeatures.Add("predicate." + predicate.Feature);
            foreach (string consequence in Consequences(rule.Consequences))
                if (!route.SupportsConsequence(consequence))
                    missingFeatures.Add(consequence);
            foreach (string outcome in rule.OutcomeFeatureIds)
                if (!route.SupportsOutcome(outcome))
                    missingFeatures.Add(outcome);
            if (missing != GameplayTacticalRuleSupportStage.None
                || missingFeatures.Count > 0)
                issues.Add(new GameplayTacticalRuleCoverageIssue(
                    "tactical-rule.incomplete-route",
                    rule.RuleId,
                    signature,
                    subject,
                    missing,
                    missingFeatures));
        }

        public static IReadOnlyList<string> Consequences(
            TacticalModifierConsequences consequences)
        {
            if (consequences == null)
                throw new ArgumentNullException(nameof(consequences));
            var result = new List<string>();
            if (consequences.AccuracyDeltaPercent != 0)
                result.Add(AccuracyDelta);
            if (consequences.WoundDelta != 0)
                result.Add("consequence.wound-delta");
            if (consequences.ReactionsAllowed.HasValue)
                result.Add("consequence.reactions-allowed");
            if (consequences.SoundMultiplier != 1f)
                result.Add("consequence.sound-multiplier");
            if (consequences.ActionPointCostDelta != 0)
                result.Add("consequence.action-point-cost-delta");
            return result.AsReadOnly();
        }
    }

    public static class GameplayCurrentTacticalRuleSupport
    {
        public static GameplayTacticalRuleSupportRegistry Create(
            IEnumerable<TacticalContextRuleDefinition> definitions,
            string liveEvidenceImplementationId)
        {
            if (definitions == null)
                throw new ArgumentNullException(nameof(definitions));
            string live = GameplayContentIdentity.RequireText(
                liveEvidenceImplementationId,
                nameof(liveEvidenceImplementationId));
            var registry = new GameplayTacticalRuleSupportRegistry();
            foreach (TacticalContextRuleDefinition rule in definitions)
            {
                var predicates = new List<TacticalContextFeature>();
                foreach (TacticalContextPredicate predicate in rule.Predicates)
                    if (!predicates.Contains(predicate.Feature))
                        predicates.Add(predicate.Feature);
                IReadOnlyList<string> consequences =
                    GameplayTacticalRuleCoverageValidator.Consequences(
                        rule.Consequences);
                foreach (string signature in rule.ApplicableCapabilitySignatures)
                foreach (GameplaySemanticSubjectKind subject in
                    rule.AcceptedSubjectKinds)
                {
                    registry.RegisterStage(
                        rule.RuleId, signature, subject,
                        GameplayTacticalRuleSupportStage.LiveEvidence,
                        live,
                        predicateFeatures: predicates);
                    registry.RegisterStage(
                        rule.RuleId, signature, subject,
                        GameplayTacticalRuleSupportStage.HeadlessEvidence,
                        nameof(GameplayHeadlessTacticalContextQuery),
                        predicateFeatures: predicates);
                    registry.RegisterStage(
                        rule.RuleId, signature, subject,
                        GameplayTacticalRuleSupportStage.PredicateEvaluation,
                        nameof(GameplayTacticalContextEvaluator),
                        predicateFeatures: predicates);
                    registry.RegisterStage(
                        rule.RuleId, signature, subject,
                        GameplayTacticalRuleSupportStage.ReducerConsequences,
                        nameof(AttackResolutionRules),
                        consequenceFeatures: consequences);
                    registry.RegisterStage(
                        rule.RuleId, signature, subject,
                        GameplayTacticalRuleSupportStage.ReplayEncoding,
                        nameof(GameplayTransitionPayloadDigest),
                        predicateFeatures: predicates,
                        consequenceFeatures: consequences,
                        outcomeFeatures: rule.OutcomeFeatureIds);
                    registry.RegisterStage(
                        rule.RuleId, signature, subject,
                        GameplayTacticalRuleSupportStage.DiagnosticProjection,
                        nameof(AttackDiagnosticFormatter),
                        predicateFeatures: predicates,
                        consequenceFeatures: consequences,
                        outcomeFeatures: rule.OutcomeFeatureIds);
                    registry.RegisterStage(
                        rule.RuleId, signature, subject,
                        GameplayTacticalRuleSupportStage.OutcomeProjection,
                        nameof(GameplayTacticalOutcomeProjector),
                        outcomeFeatures: rule.OutcomeFeatureIds);
                }
            }
            return registry;
        }
    }
}
