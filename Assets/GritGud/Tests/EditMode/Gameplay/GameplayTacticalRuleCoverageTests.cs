using System;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;
using NUnit.Framework;

namespace GritGud.Domain.Tests.Gameplay
{
    public sealed class GameplayTacticalRuleCoverageTests
    {
        [Test]
        public void CurrentRegistryCompletesExactReachableRuleRoute()
        {
            AttackDefinition attack = CreateAttack();
            GameplayCapabilityProfile profile = GameplayCapabilityProfiles.Attack(
                attack,
                GameplaySemanticSubjectKind.Actor);
            TacticalContextRuleDefinition rule = CreateRule(
                profile.Signature,
                GameplaySemanticSubjectKind.Actor);
            var input = new GameplayReachableInput(
                GameplayReachableInputKind.EquippedAttack,
                attack.ActionId,
                "actor.player",
                profile,
                "actor.target");
            GameplayTacticalRuleSupportRegistry registry =
                GameplayCurrentTacticalRuleSupport.Create(
                    new[] { rule },
                    "UnityTacticalContextQuery");

            GameplayTacticalRuleCoverageReport report =
                GameplayTacticalRuleCoverageValidator.Validate(
                    new[] { rule },
                    new[] { input },
                    registry);

            Assert.That(report.IsComplete, Is.True);
            Assert.That(report.Issues, Is.Empty);
        }

        [Test]
        public void MissingOutcomeProjectionFailsClosed()
        {
            AttackDefinition attack = CreateAttack();
            GameplayCapabilityProfile profile = GameplayCapabilityProfiles.Attack(
                attack,
                GameplaySemanticSubjectKind.Actor);
            TacticalContextRuleDefinition rule = CreateRule(
                profile.Signature,
                GameplaySemanticSubjectKind.Actor);
            var registry = new GameplayTacticalRuleSupportRegistry();
            foreach (GameplayTacticalRuleSupportStage stage in new[]
            {
                GameplayTacticalRuleSupportStage.LiveEvidence,
                GameplayTacticalRuleSupportStage.HeadlessEvidence,
                GameplayTacticalRuleSupportStage.PredicateEvaluation,
                GameplayTacticalRuleSupportStage.ReducerConsequences,
                GameplayTacticalRuleSupportStage.ReplayEncoding,
                GameplayTacticalRuleSupportStage.DiagnosticProjection,
            })
            {
                registry.RegisterStage(
                    rule.RuleId,
                    profile.Signature,
                    GameplaySemanticSubjectKind.Actor,
                    stage,
                    "test." + stage,
                    new[] { TacticalContextFeature.TargetAwareness },
                    new[] { GameplayTacticalRuleCoverageValidator.AccuracyDelta },
                    new[] { "outcome.ambush" });
            }

            GameplayTacticalRuleCoverageReport report =
                GameplayTacticalRuleCoverageValidator.Validate(
                    new[] { rule },
                    new[]
                    {
                        new GameplayReachableInput(
                            GameplayReachableInputKind.EquippedAttack,
                            attack.ActionId,
                            "actor.player",
                            profile),
                    },
                    registry);

            Assert.That(report.IsComplete, Is.False);
            Assert.That(report.Issues, Has.Count.EqualTo(1));
            Assert.That(
                report.Issues[0].MissingStages,
                Is.EqualTo(GameplayTacticalRuleSupportStage.OutcomeProjection));
        }

        [Test]
        public void TargetKindRequiresItsOwnExactRoute()
        {
            AttackDefinition attack = new AttackDefinition(
                "attack.rifle",
                "Rifle",
                new ActionCost(1, 0f, ActionMobility.Set),
                woundMovementPenalty: 2f,
                accuracyDecay: AccuracyDecayDefinition.None,
                directFireDamage: new DirectFireDamageDefinition(
                    "ballistic",
                    1f));
            GameplayCapabilityProfile propProfile =
                GameplayCapabilityProfiles.Attack(
                    attack,
                    GameplaySemanticSubjectKind.DestructibleProp);
            TacticalContextRuleDefinition propRule = CreateRule(
                propProfile.Signature,
                GameplaySemanticSubjectKind.DestructibleProp);

            GameplayTacticalRuleCoverageReport report =
                GameplayTacticalRuleCoverageValidator.Validate(
                    new[] { propRule },
                    new[]
                    {
                        new GameplayReachableInput(
                            GameplayReachableInputKind.EquippedAttack,
                            attack.ActionId,
                            "actor.player",
                            propProfile,
                            "prop.cover"),
                    },
                    new GameplayTacticalRuleSupportRegistry());

            Assert.That(report.IsComplete, Is.False);
            Assert.That(report.Issues[0].Code, Is.EqualTo(
                "tactical-rule.missing-route"));
            Assert.That(report.Issues[0].SubjectKind, Is.EqualTo(
                GameplaySemanticSubjectKind.DestructibleProp));
        }

        private static AttackDefinition CreateAttack() => new AttackDefinition(
            "attack.rifle",
            "Rifle",
            new ActionCost(1, 0f, ActionMobility.Set),
            woundMovementPenalty: 2f,
            accuracyDecay: AccuracyDecayDefinition.None);

        private static TacticalContextRuleDefinition CreateRule(
            string signature,
            GameplaySemanticSubjectKind subject) => new(
                "rule.ambush",
                "Ambush",
                order: 0,
                new[] { signature },
                new[] { subject },
                new[]
                {
                    new TacticalContextPredicate(
                        TacticalContextFeature.TargetAwareness,
                        TacticalPredicateOperator.Equal,
                        (int)TacticalAwarenessBand.Unaware),
                },
                new TacticalModifierConsequences(
                    accuracyDeltaPercent: 15),
                new[] { "outcome.ambush" });
    }
}
