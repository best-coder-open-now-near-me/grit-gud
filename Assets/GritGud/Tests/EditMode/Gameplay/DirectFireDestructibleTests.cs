using System;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;
using NUnit.Framework;

namespace GritGud.Domain.Tests.Gameplay
{
    public sealed class DirectFireDestructibleTests
    {
        [Test]
        public void SurfaceModifiersProduceAuthoredIntegrityDamage()
        {
            var damage = new DirectFireDamageDefinition(
                "damage.ballistic.rifle",
                2f,
                new[]
                {
                    new SurfaceIntegrityDamageModifier("surface.wood", 2f),
                    new SurfaceIntegrityDamageModifier("surface.metal", 1f),
                    new SurfaceIntegrityDamageModifier("surface.concrete", 0f),
                });

            Assert.That(
                damage.EvaluateIntegrityDamage("surface.wood"),
                Is.EqualTo(4f));
            Assert.That(
                damage.EvaluateIntegrityDamage("surface.metal"),
                Is.EqualTo(2f));
            Assert.That(
                damage.EvaluateIntegrityDamage("surface.concrete"),
                Is.Zero);
            Assert.That(
                damage.EvaluateIntegrityDamage("surface.unknown"),
                Is.EqualTo(2f));
        }

        [Test]
        public void PreparedRifleImpactAtomicallyCommitsActionAndPropDamage()
        {
            GameplaySession gameplay = CreateGameplaySession();
            var destructibles = new DestructiblePropSession(
                new[]
                {
                    new DestructiblePropDefinition(
                        "crate",
                        10f,
                        DestructiblePropState.Intact,
                        new GameplayPropPose(
                            new GameplayPosition(0f, 0f, 5f),
                            0f,
                            0f,
                            0f),
                        DestructiblePropPosture.Upright,
                        fractureChunkCount: 12),
                },
                gameplay.Journal);
            gameplay.EnterTurnMode();
            var attacks = new GameplayAttackSession(
                gameplay,
                19u,
                destructibles);
            DirectFireImpactRecord impact = CreateImpact(
                gameplay,
                "surface.wood");

            Assert.That(attacks.TryPrepareDischarge(
                "player",
                "crate",
                impact.Point,
                impact,
                out GameplayPreparedTransition<GameplayActionRecord> prepared,
                out AttackResolutionFailure failure), Is.True);

            Assert.That(failure, Is.EqualTo(AttackResolutionFailure.None));
            Assert.That(
                destructibles.GetProp("crate").State,
                Is.EqualTo(DestructiblePropState.Intact));
            Assert.That(
                prepared.Predicted.Destructibles[0].RemainingIntegrity,
                Is.EqualTo(6f));
            Assert.That(
                (prepared.Predicted.Destructibles[0].DetachedFractureChunks
                    & (1UL << 7)) != 0UL,
                Is.True);
            Assert.That(
                prepared.Predicted.Session.JournalSequence,
                Is.EqualTo(prepared.Previous.Session.JournalSequence + 2L));

            GameplayTransitionCommitResult committed =
                attacks.CommitPrepared(prepared);
            WeaponDischargeRecord discharge = attacks.Discharges[0];

            Assert.That(committed.MatchesPrediction, Is.True);
            Assert.That(discharge.Impact, Is.SameAs(impact));
            Assert.That(discharge.Damage, Is.Not.Null);
            Assert.That(discharge.Damage.AppliedDamage, Is.EqualTo(4f));
            Assert.That(
                discharge.Damage.PreferredFractureChunkIndex,
                Is.EqualTo(7));
            Assert.That(
                destructibles.GetProp("crate").RemainingIntegrity,
                Is.EqualTo(6f));
            Assert.That(
                destructibles.GetProp("crate").State,
                Is.EqualTo(DestructiblePropState.Damaged));
            Assert.That(gameplay.ResolvedActions, Has.Count.EqualTo(1));
            Assert.That(destructibles.DamageRecords, Has.Count.EqualTo(1));
        }

        [Test]
        public void DischargeRejectsStaleImpactEvidenceWithoutMutation()
        {
            GameplaySession gameplay = CreateGameplaySession();
            var destructibles = new DestructiblePropSession(
                new[]
                {
                    new DestructiblePropDefinition(
                        "crate",
                        10f,
                        DestructiblePropState.Intact),
                },
                gameplay.Journal);
            gameplay.EnterTurnMode();
            var attacks = new GameplayAttackSession(gameplay, 19u, destructibles);
            var stale = new DirectFireImpactRecord(
                "crate",
                "surface.wood",
                new GameplayPosition(0f, 1f, 5f),
                0f,
                0f,
                -1f,
                (gameplay.Journal.LastEntry?.Sequence ?? 0L) + 1L);

            Assert.That(attacks.TryDischarge(
                "player",
                "crate",
                stale.Point,
                stale,
                out _,
                out AttackResolutionFailure failure), Is.False);
            Assert.That(
                failure,
                Is.EqualTo(AttackResolutionFailure.WorldStateChanged));
            Assert.That(gameplay.ResolvedActions, Is.Empty);
            Assert.That(destructibles.DamageRecords, Is.Empty);
            Assert.That(
                destructibles.GetProp("crate").RemainingIntegrity,
                Is.EqualTo(10f));
        }

        private static GameplaySession CreateGameplaySession()
        {
            var attack = new AttackDefinition(
                "attack.rifle",
                "Fire rifle",
                new ActionCost(1, 0f, ActionMobility.Set),
                woundMovementPenalty: 2f,
                accuracyDecay: AccuracyDecayDefinition.None,
                directFireDamage: new DirectFireDamageDefinition(
                    "damage.ballistic.rifle",
                    2f,
                    new[]
                    {
                        new SurfaceIntegrityDamageModifier(
                            "surface.wood",
                            2f),
                        new SurfaceIntegrityDamageModifier(
                            "surface.metal",
                            1f),
                    }));
            var player = new ScenarioActorDefinition(
                "player",
                10,
                new GameplayActorPose(
                    new GameplayPosition(0f, 0f, 0f),
                    0f),
                new TurnBudget(4, 8f),
                attack);
            return new GameplaySession(new ScenarioDefinition(
                "direct-fire-test",
                new ScenarioTimingDefinition(1.25f),
                new[] { player },
                Array.Empty<ScenarioObjectiveDefinition>()),
                scenarioSeed: 19u);
        }

        private static DirectFireImpactRecord CreateImpact(
            GameplaySession gameplay,
            string surfaceId) =>
            new DirectFireImpactRecord(
                "crate",
                surfaceId,
                new GameplayPosition(0f, 1f, 5f),
                0f,
                0f,
                -1f,
                gameplay.Journal.LastEntry?.Sequence ?? 0L,
                preferredFractureChunkIndex: 7);
    }
}
