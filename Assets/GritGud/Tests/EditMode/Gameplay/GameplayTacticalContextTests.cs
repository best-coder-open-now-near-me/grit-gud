using System;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using NUnit.Framework;

namespace GritGud.Domain.Tests.Gameplay
{
    public sealed class GameplayTacticalContextTests
    {
        private const string AttackSignature =
            "DirectAttack@v1;subject=Actor";

        [Test]
        public void AmbushAppliesToUnawareTargetWithAsymmetricVisibility()
        {
            ResolvedTacticalContext result = CreateEvaluator().Evaluate(
                CreateContext(
                    TacticalAwarenessBand.Unaware,
                    TacticalVisibilityRelation.AttackerOnly));

            Assert.That(result.Modifiers, Has.Count.EqualTo(1));
            Assert.That(
                result.Modifiers[0].RuleId,
                Is.EqualTo("rule.ambush.direct-attack.actor"));
            Assert.That(result.AccuracyDeltaPercent, Is.EqualTo(15));
            Assert.That(
                result.OutcomeFeatureIds,
                Is.EqualTo(new[] { "outcome.ambush" }));
        }

        [TestCase(TacticalAwarenessBand.Suspicious)]
        [TestCase(TacticalAwarenessBand.Alert)]
        [TestCase(TacticalAwarenessBand.Unknown)]
        public void AmbushDoesNotApplyUnlessTargetIsUnaware(
            TacticalAwarenessBand awareness)
        {
            ResolvedTacticalContext result = CreateEvaluator().Evaluate(
                CreateContext(
                    awareness,
                    TacticalVisibilityRelation.AttackerOnly));

            Assert.That(result.Modifiers, Is.Empty);
            Assert.That(result.AccuracyDeltaPercent, Is.Zero);
        }

        [TestCase(TacticalVisibilityRelation.TargetOnly)]
        [TestCase(TacticalVisibilityRelation.Mutual)]
        [TestCase(TacticalVisibilityRelation.Unknown)]
        public void AmbushDoesNotApplyWhenTargetCanSeeOrVisibilityIsUnknown(
            TacticalVisibilityRelation visibility)
        {
            ResolvedTacticalContext result = CreateEvaluator().Evaluate(
                CreateContext(TacticalAwarenessBand.Unaware, visibility));

            Assert.That(result.Modifiers, Is.Empty);
        }

        [Test]
        public void RuleOrderIsDeterministicAcrossCatalogInputOrder()
        {
            TacticalContextRuleDefinition first = CreateRule(
                "rule.zulu",
                order: 10,
                accuracy: 2,
                feature: "outcome.zulu");
            TacticalContextRuleDefinition second = CreateRule(
                "rule.alpha",
                order: 10,
                accuracy: 3,
                feature: "outcome.alpha");
            TacticalContextSnapshot context = CreateContext(
                TacticalAwarenessBand.Unaware,
                TacticalVisibilityRelation.AttackerOnly);

            ResolvedTacticalContext left =
                new GameplayTacticalContextEvaluator(new[] { first, second })
                    .Evaluate(context);
            ResolvedTacticalContext right =
                new GameplayTacticalContextEvaluator(new[] { second, first })
                    .Evaluate(context);

            Assert.That(
                RuleIds(left),
                Is.EqualTo(new[] { "rule.alpha", "rule.zulu" }));
            Assert.That(RuleIds(right), Is.EqualTo(RuleIds(left)));
            Assert.That(left.AccuracyDeltaPercent, Is.EqualTo(5));
        }

        [Test]
        public void UnsupportedConsequencesFailClosedAtCatalogConstruction()
        {
            TacticalContextRuleDefinition unsupported = new(
                "rule.unsupported",
                "Unsupported",
                order: 0,
                new[] { AttackSignature },
                new[] { GameplaySemanticSubjectKind.Actor },
                AmbushPredicates(),
                new TacticalModifierConsequences(woundDelta: 1),
                new[] { "outcome.unsupported" });

            Assert.Throws<NotSupportedException>(() =>
                new GameplayTacticalContextEvaluator(
                    new[] { unsupported }));
        }

        [Test]
        public void SnapshotRejectsDefaultSemanticSubject()
        {
            Assert.Throws<ArgumentException>(() => CreateContext(
                TacticalAwarenessBand.Unaware,
                TacticalVisibilityRelation.AttackerOnly,
                default));
        }

        [Test]
        public void PredicatesRejectValuesOutsideTheirFeatureVocabulary()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new TacticalContextPredicate(
                    TacticalContextFeature.VisibilityRelation,
                    TacticalPredicateOperator.Equal,
                    value: 99));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new TacticalContextPredicate(
                    TacticalContextFeature.TargetSuppressed,
                    TacticalPredicateOperator.Equal,
                    value: 2));
        }

        [Test]
        public void CatalogRejectsDuplicateRuleIds()
        {
            TacticalContextRuleDefinition first = CreateRule(
                "rule.duplicate",
                order: 0,
                accuracy: 1,
                feature: "outcome.first");
            TacticalContextRuleDefinition second = CreateRule(
                "rule.duplicate",
                order: 1,
                accuracy: 2,
                feature: "outcome.second");

            Assert.Throws<ArgumentException>(() =>
                new GameplayTacticalContextEvaluator(
                    new[] { first, second }));
        }

        private static GameplayTacticalContextEvaluator CreateEvaluator() =>
            new(new[]
            {
                CreateRule(
                    "rule.ambush.direct-attack.actor",
                    order: 0,
                    accuracy: 15,
                    feature: "outcome.ambush"),
            });

        private static TacticalContextRuleDefinition CreateRule(
            string id,
            int order,
            int accuracy,
            string feature) => new(
                id,
                id,
                order,
                new[] { AttackSignature },
                new[] { GameplaySemanticSubjectKind.Actor },
                AmbushPredicates(),
                new TacticalModifierConsequences(
                    accuracyDeltaPercent: accuracy),
                new[] { feature });

        private static TacticalContextPredicate[] AmbushPredicates() =>
            new[]
            {
                new TacticalContextPredicate(
                    TacticalContextFeature.TargetAwareness,
                    TacticalPredicateOperator.Equal,
                    (int)TacticalAwarenessBand.Unaware),
                new TacticalContextPredicate(
                    TacticalContextFeature.VisibilityRelation,
                    TacticalPredicateOperator.Equal,
                    (int)TacticalVisibilityRelation.AttackerOnly),
            };

        private static TacticalContextSnapshot CreateContext(
            TacticalAwarenessBand awareness,
            TacticalVisibilityRelation visibility,
            GameplaySubjectReference? subject = null) => new(
                "actor.player",
                subject ?? new GameplaySubjectReference(
                    GameplaySemanticSubjectKind.Actor,
                    "actor.rifleman"),
                AttackSignature,
                stateRevision: 8,
                awareness,
                visibility,
                ActorStance.Crouched,
                ActorStance.Standing,
                TacticalRangeBand.Effective,
                TacticalExposureBand.Exposed,
                TacticalIsolationBand.Isolated,
                nearbyAttackerAllies: 1,
                nearbyTargetAllies: 0,
                attackerSuppressed: false,
                targetSuppressed: false,
                targetDisplaced: false,
                soundSignature: 0.8f,
                attackerActionPoints: 4,
                targetActionPoints: 4);

        private static string[] RuleIds(ResolvedTacticalContext context)
        {
            var result = new string[context.Modifiers.Count];
            for (int index = 0; index < result.Length; index++)
                result[index] = context.Modifiers[index].RuleId;
            return result;
        }
    }
}
