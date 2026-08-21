using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using GritGud.Domain.Gameplay;

namespace GritGud.Application.Gameplay
{
    public enum TacticalAwarenessBand
    {
        Unknown,
        Unaware,
        Suspicious,
        Alert,
    }

    public enum TacticalVisibilityRelation
    {
        Unknown,
        Neither,
        AttackerOnly,
        TargetOnly,
        Mutual,
    }

    public enum TacticalRangeBand
    {
        Contact,
        Close,
        Effective,
        Long,
        Extreme,
    }

    public enum TacticalExposureBand
    {
        Unknown,
        Hidden,
        Protected,
        Exposed,
    }

    public enum TacticalIsolationBand
    {
        Unknown,
        Isolated,
        Supported,
    }

    public enum TacticalContextFeature
    {
        TargetAwareness,
        VisibilityRelation,
        RangeBand,
        ExposureBand,
        IsolationBand,
        AttackerStance,
        TargetStance,
        AttackerSuppressed,
        TargetSuppressed,
        TargetDisplaced,
        NearbyAttackerAllies,
        NearbyTargetAllies,
        AttackerActionPoints,
        TargetActionPoints,
    }

    public enum TacticalPredicateOperator
    {
        Equal,
        NotEqual,
        LessThan,
        LessThanOrEqual,
        GreaterThan,
        GreaterThanOrEqual,
    }

    public sealed class TacticalContextSnapshot
    {
        public TacticalContextSnapshot(
            string attackerId,
            GameplaySubjectReference subject,
            string capabilitySignature,
            long stateRevision,
            TacticalAwarenessBand targetAwareness,
            TacticalVisibilityRelation visibility,
            ActorStance attackerStance,
            ActorStance targetStance,
            TacticalRangeBand rangeBand,
            TacticalExposureBand exposureBand,
            TacticalIsolationBand isolationBand,
            int nearbyAttackerAllies,
            int nearbyTargetAllies,
            bool attackerSuppressed,
            bool targetSuppressed,
            bool targetDisplaced,
            float soundSignature,
            int attackerActionPoints,
            int targetActionPoints)
        {
            AttackerId = GameplayContentIdentity.RequireText(
                attackerId,
                nameof(attackerId));
            if (string.IsNullOrWhiteSpace(subject.Id)
                || !Enum.IsDefined(
                    typeof(GameplaySemanticSubjectKind),
                    subject.Kind))
                throw new ArgumentException(
                    "Tactical context requires a valid semantic subject.",
                    nameof(subject));
            Subject = subject;
            CapabilitySignature = GameplayContentIdentity.RequireText(
                capabilitySignature,
                nameof(capabilitySignature));
            if (stateRevision < 0L)
                throw new ArgumentOutOfRangeException(nameof(stateRevision));
            RequireDefined(targetAwareness, nameof(targetAwareness));
            RequireDefined(visibility, nameof(visibility));
            RequireDefined(attackerStance, nameof(attackerStance));
            RequireDefined(targetStance, nameof(targetStance));
            RequireDefined(rangeBand, nameof(rangeBand));
            RequireDefined(exposureBand, nameof(exposureBand));
            RequireDefined(isolationBand, nameof(isolationBand));
            if (nearbyAttackerAllies < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(nearbyAttackerAllies));
            if (nearbyTargetAllies < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(nearbyTargetAllies));
            GameplayNumericPolicy.RequireFinite(
                soundSignature,
                nameof(soundSignature));
            if (soundSignature < 0f || soundSignature > 1f)
                throw new ArgumentOutOfRangeException(nameof(soundSignature));
            if (attackerActionPoints < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(attackerActionPoints));
            if (targetActionPoints < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(targetActionPoints));

            StateRevision = stateRevision;
            TargetAwareness = targetAwareness;
            Visibility = visibility;
            AttackerStance = attackerStance;
            TargetStance = targetStance;
            RangeBand = rangeBand;
            ExposureBand = exposureBand;
            IsolationBand = isolationBand;
            NearbyAttackerAllies = nearbyAttackerAllies;
            NearbyTargetAllies = nearbyTargetAllies;
            AttackerSuppressed = attackerSuppressed;
            TargetSuppressed = targetSuppressed;
            TargetDisplaced = targetDisplaced;
            SoundSignature = soundSignature;
            AttackerActionPoints = attackerActionPoints;
            TargetActionPoints = targetActionPoints;
        }

        public string AttackerId { get; }
        public GameplaySubjectReference Subject { get; }
        public string CapabilitySignature { get; }
        public long StateRevision { get; }
        public TacticalAwarenessBand TargetAwareness { get; }
        public TacticalVisibilityRelation Visibility { get; }
        public ActorStance AttackerStance { get; }
        public ActorStance TargetStance { get; }
        public TacticalRangeBand RangeBand { get; }
        public TacticalExposureBand ExposureBand { get; }
        public TacticalIsolationBand IsolationBand { get; }
        public int NearbyAttackerAllies { get; }
        public int NearbyTargetAllies { get; }
        public bool AttackerSuppressed { get; }
        public bool TargetSuppressed { get; }
        public bool TargetDisplaced { get; }
        public float SoundSignature { get; }
        public int AttackerActionPoints { get; }
        public int TargetActionPoints { get; }

        public int ReadFeature(TacticalContextFeature feature)
        {
            switch (feature)
            {
                case TacticalContextFeature.TargetAwareness:
                    return (int)TargetAwareness;
                case TacticalContextFeature.VisibilityRelation:
                    return (int)Visibility;
                case TacticalContextFeature.RangeBand:
                    return (int)RangeBand;
                case TacticalContextFeature.ExposureBand:
                    return (int)ExposureBand;
                case TacticalContextFeature.IsolationBand:
                    return (int)IsolationBand;
                case TacticalContextFeature.AttackerStance:
                    return (int)AttackerStance;
                case TacticalContextFeature.TargetStance:
                    return (int)TargetStance;
                case TacticalContextFeature.AttackerSuppressed:
                    return AttackerSuppressed ? 1 : 0;
                case TacticalContextFeature.TargetSuppressed:
                    return TargetSuppressed ? 1 : 0;
                case TacticalContextFeature.TargetDisplaced:
                    return TargetDisplaced ? 1 : 0;
                case TacticalContextFeature.NearbyAttackerAllies:
                    return NearbyAttackerAllies;
                case TacticalContextFeature.NearbyTargetAllies:
                    return NearbyTargetAllies;
                case TacticalContextFeature.AttackerActionPoints:
                    return AttackerActionPoints;
                case TacticalContextFeature.TargetActionPoints:
                    return TargetActionPoints;
                default:
                    throw new ArgumentOutOfRangeException(nameof(feature));
            }
        }

        private static void RequireDefined<T>(T value, string parameterName)
            where T : struct
        {
            if (!Enum.IsDefined(typeof(T), value))
                throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    public readonly struct TacticalContextPredicate
    {
        public TacticalContextPredicate(
            TacticalContextFeature feature,
            TacticalPredicateOperator comparison,
            int value)
        {
            if (!Enum.IsDefined(typeof(TacticalContextFeature), feature))
                throw new ArgumentOutOfRangeException(nameof(feature));
            if (!Enum.IsDefined(typeof(TacticalPredicateOperator), comparison))
                throw new ArgumentOutOfRangeException(nameof(comparison));
            ValidateValue(feature, value);
            Feature = feature;
            Comparison = comparison;
            Value = value;
        }

        public TacticalContextFeature Feature { get; }
        public TacticalPredicateOperator Comparison { get; }
        public int Value { get; }

        public bool Matches(TacticalContextSnapshot context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            int actual = context.ReadFeature(Feature);
            switch (Comparison)
            {
                case TacticalPredicateOperator.Equal: return actual == Value;
                case TacticalPredicateOperator.NotEqual: return actual != Value;
                case TacticalPredicateOperator.LessThan: return actual < Value;
                case TacticalPredicateOperator.LessThanOrEqual:
                    return actual <= Value;
                case TacticalPredicateOperator.GreaterThan: return actual > Value;
                case TacticalPredicateOperator.GreaterThanOrEqual:
                    return actual >= Value;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private static void ValidateValue(
            TacticalContextFeature feature,
            int value)
        {
            switch (feature)
            {
                case TacticalContextFeature.TargetAwareness:
                    RequireEnum<TacticalAwarenessBand>(value);
                    return;
                case TacticalContextFeature.VisibilityRelation:
                    RequireEnum<TacticalVisibilityRelation>(value);
                    return;
                case TacticalContextFeature.RangeBand:
                    RequireEnum<TacticalRangeBand>(value);
                    return;
                case TacticalContextFeature.ExposureBand:
                    RequireEnum<TacticalExposureBand>(value);
                    return;
                case TacticalContextFeature.IsolationBand:
                    RequireEnum<TacticalIsolationBand>(value);
                    return;
                case TacticalContextFeature.AttackerStance:
                case TacticalContextFeature.TargetStance:
                    RequireEnum<ActorStance>(value);
                    return;
                case TacticalContextFeature.AttackerSuppressed:
                case TacticalContextFeature.TargetSuppressed:
                case TacticalContextFeature.TargetDisplaced:
                    if (value != 0 && value != 1)
                        throw new ArgumentOutOfRangeException(nameof(value));
                    return;
                case TacticalContextFeature.NearbyAttackerAllies:
                case TacticalContextFeature.NearbyTargetAllies:
                case TacticalContextFeature.AttackerActionPoints:
                case TacticalContextFeature.TargetActionPoints:
                    if (value < 0)
                        throw new ArgumentOutOfRangeException(nameof(value));
                    return;
                default:
                    throw new ArgumentOutOfRangeException(nameof(feature));
            }
        }

        private static void RequireEnum<T>(int value) where T : struct
        {
            if (!Enum.IsDefined(typeof(T), value))
                throw new ArgumentOutOfRangeException(nameof(value));
        }
    }

    public sealed class TacticalModifierConsequences
    {
        public static TacticalModifierConsequences Neutral { get; } =
            new TacticalModifierConsequences();

        public TacticalModifierConsequences(
            int accuracyDeltaPercent = 0,
            int woundDelta = 0,
            bool? reactionsAllowed = null,
            float soundMultiplier = 1f,
            int actionPointCostDelta = 0)
        {
            if (accuracyDeltaPercent < -100 || accuracyDeltaPercent > 100)
                throw new ArgumentOutOfRangeException(
                    nameof(accuracyDeltaPercent));
            if (woundDelta < -100 || woundDelta > 100)
                throw new ArgumentOutOfRangeException(nameof(woundDelta));
            GameplayNumericPolicy.RequireFinite(
                soundMultiplier,
                nameof(soundMultiplier));
            if (soundMultiplier < 0f || soundMultiplier > 10f)
                throw new ArgumentOutOfRangeException(nameof(soundMultiplier));
            if (actionPointCostDelta < -100 || actionPointCostDelta > 100)
                throw new ArgumentOutOfRangeException(
                    nameof(actionPointCostDelta));
            AccuracyDeltaPercent = accuracyDeltaPercent;
            WoundDelta = woundDelta;
            ReactionsAllowed = reactionsAllowed;
            SoundMultiplier = soundMultiplier;
            ActionPointCostDelta = actionPointCostDelta;
        }

        public int AccuracyDeltaPercent { get; }
        public int WoundDelta { get; }
        public bool? ReactionsAllowed { get; }
        public float SoundMultiplier { get; }
        public int ActionPointCostDelta { get; }

        public bool HasUnsupportedFirstSliceConsequence =>
            WoundDelta != 0
            || ReactionsAllowed.HasValue
            || SoundMultiplier != 1f
            || ActionPointCostDelta != 0;
    }

    public sealed class TacticalContextRuleDefinition
    {
        private readonly IReadOnlyList<string> capabilitySignatures;
        private readonly IReadOnlyList<GameplaySemanticSubjectKind> subjectKinds;
        private readonly IReadOnlyList<TacticalContextPredicate> predicates;
        private readonly IReadOnlyList<string> outcomeFeatureIds;

        public TacticalContextRuleDefinition(
            string ruleId,
            string displayName,
            int order,
            IEnumerable<string> applicableCapabilitySignatures,
            IEnumerable<GameplaySemanticSubjectKind> acceptedSubjectKinds,
            IEnumerable<TacticalContextPredicate> requiredPredicates,
            TacticalModifierConsequences consequences,
            IEnumerable<string> resultingOutcomeFeatureIds)
        {
            RuleId = GameplayContentIdentity.RequireText(ruleId, nameof(ruleId));
            DisplayName = GameplayContentIdentity.RequireText(
                displayName,
                nameof(displayName));
            if (order < 0) throw new ArgumentOutOfRangeException(nameof(order));
            Order = order;
            capabilitySignatures = CopyUniqueText(
                applicableCapabilitySignatures,
                nameof(applicableCapabilitySignatures));
            subjectKinds = CopySubjectKinds(acceptedSubjectKinds);
            predicates = Array.AsReadOnly(new List<TacticalContextPredicate>(
                requiredPredicates ?? throw new ArgumentNullException(
                    nameof(requiredPredicates))).ToArray());
            if (predicates.Count == 0)
                throw new ArgumentException(
                    "Tactical rules require at least one predicate.",
                    nameof(requiredPredicates));
            Consequences = consequences ?? throw new ArgumentNullException(
                nameof(consequences));
            outcomeFeatureIds = CopyUniqueText(
                resultingOutcomeFeatureIds,
                nameof(resultingOutcomeFeatureIds));
        }

        public string RuleId { get; }
        public string DisplayName { get; }
        public int Order { get; }
        public IReadOnlyList<string> ApplicableCapabilitySignatures =>
            capabilitySignatures;
        public IReadOnlyList<GameplaySemanticSubjectKind> AcceptedSubjectKinds =>
            subjectKinds;
        public IReadOnlyList<TacticalContextPredicate> Predicates => predicates;
        public TacticalModifierConsequences Consequences { get; }
        public IReadOnlyList<string> OutcomeFeatureIds => outcomeFeatureIds;

        public bool AppliesTo(TacticalContextSnapshot context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (!Contains(capabilitySignatures, context.CapabilitySignature)
                || !Contains(subjectKinds, context.Subject.Kind))
                return false;
            foreach (TacticalContextPredicate predicate in predicates)
                if (!predicate.Matches(context)) return false;
            return true;
        }

        private static IReadOnlyList<string> CopyUniqueText(
            IEnumerable<string> values,
            string parameterName)
        {
            if (values == null) throw new ArgumentNullException(parameterName);
            var result = new List<string>();
            var unique = new HashSet<string>(StringComparer.Ordinal);
            foreach (string value in values)
            {
                string normalized = GameplayContentIdentity.RequireText(
                    value,
                    parameterName);
                if (!unique.Add(normalized))
                    throw new ArgumentException(
                        $"Tactical rule value '{normalized}' is duplicated.",
                        parameterName);
                result.Add(normalized);
            }
            if (result.Count == 0)
                throw new ArgumentException(
                    "Tactical rules require at least one value.",
                    parameterName);
            result.Sort(StringComparer.Ordinal);
            return result.AsReadOnly();
        }

        private static IReadOnlyList<GameplaySemanticSubjectKind>
            CopySubjectKinds(IEnumerable<GameplaySemanticSubjectKind> values)
        {
            if (values == null)
                throw new ArgumentNullException(nameof(values));
            var result = new List<GameplaySemanticSubjectKind>();
            var unique = new HashSet<GameplaySemanticSubjectKind>();
            foreach (GameplaySemanticSubjectKind value in values)
            {
                if (!Enum.IsDefined(typeof(GameplaySemanticSubjectKind), value))
                    throw new ArgumentOutOfRangeException(nameof(values));
                if (!unique.Add(value))
                    throw new ArgumentException(
                        $"Tactical subject kind '{value}' is duplicated.",
                        nameof(values));
                result.Add(value);
            }
            if (result.Count == 0)
                throw new ArgumentException(
                    "Tactical rules require at least one subject kind.",
                    nameof(values));
            result.Sort();
            return result.AsReadOnly();
        }

        private static bool Contains(
            IReadOnlyList<string> values,
            string expected)
        {
            foreach (string value in values)
                if (string.Equals(value, expected, StringComparison.Ordinal))
                    return true;
            return false;
        }

        private static bool Contains(
            IReadOnlyList<GameplaySemanticSubjectKind> values,
            GameplaySemanticSubjectKind expected)
        {
            foreach (GameplaySemanticSubjectKind value in values)
                if (value == expected) return true;
            return false;
        }
    }

    public sealed class AppliedTacticalModifier
    {
        public AppliedTacticalModifier(
            string ruleId,
            int ruleOrder,
            TacticalModifierConsequences consequences,
            IEnumerable<string> outcomeFeatureIds)
        {
            RuleId = GameplayContentIdentity.RequireText(ruleId, nameof(ruleId));
            if (ruleOrder < 0)
                throw new ArgumentOutOfRangeException(nameof(ruleOrder));
            RuleOrder = ruleOrder;
            Consequences = consequences ?? throw new ArgumentNullException(
                nameof(consequences));
            var features = new List<string>();
            var unique = new HashSet<string>(StringComparer.Ordinal);
            foreach (string value in outcomeFeatureIds
                ?? throw new ArgumentNullException(nameof(outcomeFeatureIds)))
            {
                string feature = GameplayContentIdentity.RequireText(
                    value,
                    nameof(outcomeFeatureIds));
                if (!unique.Add(feature))
                    throw new ArgumentException(
                        $"Outcome feature '{feature}' is duplicated.",
                        nameof(outcomeFeatureIds));
                features.Add(feature);
            }
            features.Sort(StringComparer.Ordinal);
            OutcomeFeatureIds = features.AsReadOnly();
        }

        public string RuleId { get; }
        public int RuleOrder { get; }
        public TacticalModifierConsequences Consequences { get; }
        public IReadOnlyList<string> OutcomeFeatureIds { get; }
    }

    public sealed class ResolvedTacticalContext : IGameplayActionContext
    {
        public ResolvedTacticalContext(
            TacticalContextSnapshot snapshot,
            IEnumerable<AppliedTacticalModifier> modifiers)
        {
            Snapshot = snapshot ?? throw new ArgumentNullException(
                nameof(snapshot));
            var copy = new List<AppliedTacticalModifier>(
                modifiers ?? throw new ArgumentNullException(nameof(modifiers)));
            copy.Sort((left, right) =>
            {
                if (left == null || right == null) return 0;
                int order = left.RuleOrder.CompareTo(right.RuleOrder);
                return order != 0
                    ? order
                    : StringComparer.Ordinal.Compare(
                        left.RuleId,
                        right.RuleId);
            });
            var ruleIds = new HashSet<string>(StringComparer.Ordinal);
            var features = new HashSet<string>(StringComparer.Ordinal);
            int accuracy = 0;
            foreach (AppliedTacticalModifier modifier in copy)
            {
                if (modifier == null)
                    throw new ArgumentException(
                        "Applied tactical modifiers cannot contain null.",
                        nameof(modifiers));
                if (!ruleIds.Add(modifier.RuleId))
                    throw new ArgumentException(
                        $"Tactical rule '{modifier.RuleId}' was applied twice.",
                        nameof(modifiers));
                accuracy = checked(
                    accuracy + modifier.Consequences.AccuracyDeltaPercent);
                foreach (string feature in modifier.OutcomeFeatureIds)
                    if (!features.Add(feature))
                        throw new ArgumentException(
                            $"Outcome feature '{feature}' was produced twice.",
                            nameof(modifiers));
            }
            if (accuracy < -100 || accuracy > 100)
                throw new ArgumentOutOfRangeException(
                    nameof(modifiers),
                    "Combined tactical accuracy must remain within [-100, 100].");
            Modifiers = copy.AsReadOnly();
            AccuracyDeltaPercent = accuracy;
            var orderedFeatures = new List<string>(features);
            orderedFeatures.Sort(StringComparer.Ordinal);
            OutcomeFeatureIds = orderedFeatures.AsReadOnly();
            CanonicalDigest = BuildCanonicalDigest();
        }

        public TacticalContextSnapshot Snapshot { get; }
        public IReadOnlyList<AppliedTacticalModifier> Modifiers { get; }
        public int AccuracyDeltaPercent { get; }
        public IReadOnlyList<string> OutcomeFeatureIds { get; }
        public string AttackerId => Snapshot.AttackerId;
        public string SubjectId => Snapshot.Subject.Id;
        public string SubjectKind => Snapshot.Subject.Kind.ToString();
        public string CapabilitySignature => Snapshot.CapabilitySignature;
        public long StateRevision => Snapshot.StateRevision;
        public string CanonicalDigest { get; }

        private string BuildCanonicalDigest()
        {
            var text = new StringBuilder();
            Append(text, AttackerId);
            Append(text, SubjectKind);
            Append(text, SubjectId);
            Append(text, CapabilitySignature);
            Append(text, StateRevision);
            Append(text, (int)Snapshot.TargetAwareness);
            Append(text, (int)Snapshot.Visibility);
            Append(text, (int)Snapshot.AttackerStance);
            Append(text, (int)Snapshot.TargetStance);
            Append(text, (int)Snapshot.RangeBand);
            Append(text, (int)Snapshot.ExposureBand);
            Append(text, (int)Snapshot.IsolationBand);
            Append(text, Snapshot.NearbyAttackerAllies);
            Append(text, Snapshot.NearbyTargetAllies);
            Append(text, Snapshot.AttackerSuppressed);
            Append(text, Snapshot.TargetSuppressed);
            Append(text, Snapshot.TargetDisplaced);
            Append(text, Snapshot.SoundSignature);
            Append(text, Snapshot.AttackerActionPoints);
            Append(text, Snapshot.TargetActionPoints);
            foreach (AppliedTacticalModifier modifier in Modifiers)
            {
                Append(text, modifier.RuleId);
                Append(text, modifier.RuleOrder);
                Append(text, modifier.Consequences.AccuracyDeltaPercent);
                Append(text, modifier.Consequences.WoundDelta);
                Append(text, modifier.Consequences.ReactionsAllowed);
                Append(text, modifier.Consequences.SoundMultiplier);
                Append(text, modifier.Consequences.ActionPointCostDelta);
                foreach (string feature in modifier.OutcomeFeatureIds)
                    Append(text, feature);
            }
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(
                    Encoding.UTF8.GetBytes(text.ToString()));
                var result = new StringBuilder(hash.Length * 2);
                foreach (byte value in hash)
                    result.Append(value.ToString("x2"));
                return result.ToString();
            }
        }

        private static void Append(StringBuilder text, object value)
        {
            string serialized = value == null
                ? "null"
                : value is float number
                    ? number.ToString("R", System.Globalization.CultureInfo.InvariantCulture)
                    : value.ToString();
            text.Append(serialized.Length)
                .Append(':')
                .Append(serialized)
                .Append('|');
        }
    }

    public sealed class GameplayTacticalContextEvaluator
    {
        private readonly IReadOnlyList<TacticalContextRuleDefinition> rules;

        public GameplayTacticalContextEvaluator(
            IEnumerable<TacticalContextRuleDefinition> definitions)
        {
            var copy = new List<TacticalContextRuleDefinition>(
                definitions ?? throw new ArgumentNullException(
                    nameof(definitions)));
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (TacticalContextRuleDefinition rule in copy)
            {
                if (rule == null)
                    throw new ArgumentException(
                        "Tactical rule catalogs cannot contain null.",
                        nameof(definitions));
                if (!ids.Add(rule.RuleId))
                    throw new ArgumentException(
                        $"Tactical rule '{rule.RuleId}' is duplicated.",
                        nameof(definitions));
                if (rule.Consequences.HasUnsupportedFirstSliceConsequence)
                    throw new NotSupportedException(
                        $"Tactical rule '{rule.RuleId}' declares a consequence that the first reducer slice does not support.");
            }
            copy.Sort((left, right) =>
            {
                int order = left.Order.CompareTo(right.Order);
                return order != 0
                    ? order
                    : StringComparer.Ordinal.Compare(
                        left.RuleId,
                        right.RuleId);
            });
            rules = copy.AsReadOnly();
        }

        public ResolvedTacticalContext Evaluate(
            TacticalContextSnapshot context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            var applied = new List<AppliedTacticalModifier>();
            foreach (TacticalContextRuleDefinition rule in rules)
            {
                if (!rule.AppliesTo(context)) continue;
                applied.Add(new AppliedTacticalModifier(
                    rule.RuleId,
                    rule.Order,
                    rule.Consequences,
                    rule.OutcomeFeatureIds));
            }
            return new ResolvedTacticalContext(context, applied);
        }
    }
}
