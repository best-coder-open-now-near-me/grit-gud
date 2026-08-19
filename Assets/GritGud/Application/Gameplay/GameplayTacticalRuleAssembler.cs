using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Levels;

namespace GritGud.Application.Gameplay
{
    internal static class GameplayTacticalRuleAssembler
    {
        public static IReadOnlyList<TacticalContextRuleDefinition> Create(
            IEnumerable<ScenarioTacticalRuleData> authoredRules,
            ScenarioDefinition scenario,
            LevelDocument level)
        {
            if (scenario == null) throw new ArgumentNullException(nameof(scenario));
            if (level == null) throw new ArgumentNullException(nameof(level));
            IReadOnlyList<GameplayReachableInput> reachable =
                GameplayReachableInputEnumerator.Enumerate(scenario, level);
            var result = new List<TacticalContextRuleDefinition>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (ScenarioTacticalRuleData data in
                authoredRules ?? Array.Empty<ScenarioTacticalRuleData>())
            {
                if (data == null)
                    throw new InvalidOperationException(
                        "Scenario tactical rules cannot contain null entries.");
                string id = RequireText(data.id, "Tactical rule ID");
                if (!ids.Add(id))
                    throw new InvalidOperationException(
                        $"Tactical rule '{id}' is defined more than once.");
                GameplaySemanticCapability capability = ParseEnum<
                    GameplaySemanticCapability>(
                        data.capability,
                        $"Tactical rule '{id}' capability");
                IReadOnlyList<GameplaySemanticSubjectKind> subjects =
                    ParseSubjects(id, data.subjectKinds);
                IReadOnlyList<string> signatures = ResolveSignatures(
                    id,
                    capability,
                    subjects,
                    reachable);
                result.Add(new TacticalContextRuleDefinition(
                    id,
                    RequireText(
                        data.displayName,
                        $"Tactical rule '{id}' display name"),
                    data.order,
                    signatures,
                    subjects,
                    ParsePredicates(id, data.predicates),
                    ParseConsequences(id, data.consequences),
                    data.outcomeFeatureIds
                        ?? new List<string>()));
            }
            return result.AsReadOnly();
        }

        private static IReadOnlyList<string> ResolveSignatures(
            string ruleId,
            GameplaySemanticCapability capability,
            IReadOnlyList<GameplaySemanticSubjectKind> subjects,
            IEnumerable<GameplayReachableInput> reachable)
        {
            var signatures = new List<string>();
            var unique = new HashSet<string>(StringComparer.Ordinal);
            foreach (GameplayReachableInput input in reachable)
            {
                if (input.Profile.Capability != capability
                    || !Contains(subjects, input.SubjectKind)
                    || !unique.Add(input.Profile.Signature))
                    continue;
                signatures.Add(input.Profile.Signature);
            }
            if (signatures.Count == 0)
                throw new InvalidOperationException(
                    $"Tactical rule '{ruleId}' matches no reachable capability and subject route.");
            signatures.Sort(StringComparer.Ordinal);
            return signatures.AsReadOnly();
        }

        private static IReadOnlyList<GameplaySemanticSubjectKind> ParseSubjects(
            string ruleId,
            IEnumerable<string> values)
        {
            var result = new List<GameplaySemanticSubjectKind>();
            var unique = new HashSet<GameplaySemanticSubjectKind>();
            foreach (string value in values ?? Array.Empty<string>())
            {
                GameplaySemanticSubjectKind subject = ParseEnum<
                    GameplaySemanticSubjectKind>(
                        value,
                        $"Tactical rule '{ruleId}' subject kind");
                if (!unique.Add(subject))
                    throw new InvalidOperationException(
                        $"Tactical rule '{ruleId}' repeats subject kind '{subject}'.");
                result.Add(subject);
            }
            if (result.Count == 0)
                throw new InvalidOperationException(
                    $"Tactical rule '{ruleId}' requires a subject kind.");
            return result.AsReadOnly();
        }

        private static IReadOnlyList<TacticalContextPredicate> ParsePredicates(
            string ruleId,
            IEnumerable<ScenarioTacticalPredicateData> values)
        {
            var result = new List<TacticalContextPredicate>();
            foreach (ScenarioTacticalPredicateData data in
                values ?? Array.Empty<ScenarioTacticalPredicateData>())
            {
                if (data == null)
                    throw new InvalidOperationException(
                        $"Tactical rule '{ruleId}' contains a null predicate.");
                TacticalContextFeature feature = ParseEnum<TacticalContextFeature>(
                    data.feature,
                    $"Tactical rule '{ruleId}' predicate feature");
                TacticalPredicateOperator comparison = ParseEnum<
                    TacticalPredicateOperator>(
                        data.comparison,
                        $"Tactical rule '{ruleId}' predicate comparison");
                result.Add(new TacticalContextPredicate(
                    feature,
                    comparison,
                    ParsePredicateValue(ruleId, feature, data.value)));
            }
            if (result.Count == 0)
                throw new InvalidOperationException(
                    $"Tactical rule '{ruleId}' requires a predicate.");
            return result.AsReadOnly();
        }

        private static TacticalModifierConsequences ParseConsequences(
            string ruleId,
            ScenarioTacticalConsequencesData data)
        {
            if (data == null)
                throw new InvalidOperationException(
                    $"Tactical rule '{ruleId}' requires consequences.");
            return new TacticalModifierConsequences(
                data.accuracyDeltaPercent,
                data.woundDelta,
                data.hasReactionsAllowed
                    ? data.reactionsAllowed
                    : (bool?)null,
                data.soundMultiplier,
                data.actionPointCostDelta);
        }

        private static int ParsePredicateValue(
            string ruleId,
            TacticalContextFeature feature,
            string value)
        {
            string label = $"Tactical rule '{ruleId}' predicate value";
            switch (feature)
            {
                case TacticalContextFeature.TargetAwareness:
                    return (int)ParseEnum<TacticalAwarenessBand>(value, label);
                case TacticalContextFeature.VisibilityRelation:
                    return (int)ParseEnum<TacticalVisibilityRelation>(value, label);
                case TacticalContextFeature.RangeBand:
                    return (int)ParseEnum<TacticalRangeBand>(value, label);
                case TacticalContextFeature.ExposureBand:
                    return (int)ParseEnum<TacticalExposureBand>(value, label);
                case TacticalContextFeature.IsolationBand:
                    return (int)ParseEnum<TacticalIsolationBand>(value, label);
                case TacticalContextFeature.AttackerStance:
                case TacticalContextFeature.TargetStance:
                    return (int)ParseEnum<ActorStance>(value, label);
                case TacticalContextFeature.AttackerSuppressed:
                case TacticalContextFeature.TargetSuppressed:
                case TacticalContextFeature.TargetDisplaced:
                    if (bool.TryParse(value, out bool boolean))
                        return boolean ? 1 : 0;
                    break;
                default:
                    if (int.TryParse(
                        value,
                        System.Globalization.NumberStyles.Integer,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out int number))
                        return number;
                    break;
            }
            throw new InvalidOperationException(
                $"{label} '{value}' is invalid for '{feature}'.");
        }

        private static T ParseEnum<T>(string value, string label)
            where T : struct
        {
            string normalized = RequireText(value, label)
                .Replace("-", string.Empty);
            if (!Enum.TryParse(normalized, ignoreCase: true, out T result)
                || !Enum.IsDefined(typeof(T), result))
                throw new InvalidOperationException(
                    $"{label} '{value}' is unsupported.");
            return result;
        }

        private static string RequireText(string value, string label)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new InvalidOperationException($"{label} cannot be empty.");
            return value.Trim();
        }

        private static bool Contains<T>(IEnumerable<T> values, T expected)
        {
            foreach (T value in values)
                if (EqualityComparer<T>.Default.Equals(value, expected))
                    return true;
            return false;
        }
    }
}
