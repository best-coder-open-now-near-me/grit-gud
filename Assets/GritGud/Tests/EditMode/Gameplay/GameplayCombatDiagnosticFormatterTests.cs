using System;
using System.Linq;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;
using NUnit.Framework;

namespace GritGud.Domain.Tests.Gameplay
{
    public sealed class GameplayCombatDiagnosticFormatterTests
    {
        [Test]
        public void EveryActionOutcomeHasAnExplicitDiagnosticPolicy()
        {
            Type outcome = typeof(GameplayActionOutcome);
            Type[] concreteOutcomes = outcome.Assembly.GetTypes()
                .Where(type => outcome.IsAssignableFrom(type)
                    && !type.IsAbstract)
                .ToArray();

            Assert.That(concreteOutcomes, Is.Not.Empty);
            foreach (Type outcomeType in concreteOutcomes)
            {
                Assert.DoesNotThrow(() =>
                    GameplayCombatDiagnosticFormatter
                        .GetActionOutcomePolicy(outcomeType),
                    outcomeType.FullName);
            }
        }

        [Test]
        public void EveryJournalKindHasAnExplicitDiagnosticPolicy()
        {
            foreach (GameplayJournalEntryKind kind in Enum.GetValues(
                typeof(GameplayJournalEntryKind)))
            {
                Assert.DoesNotThrow(() =>
                    GameplayCombatDiagnosticFormatter
                        .GetJournalEntryPolicy(kind),
                    kind.ToString());
            }
        }

        [Test]
        public void ThrownBlastDiagnosticUsesRecordedEvidenceAndConsequences()
        {
            var cost = new ActionCost(2, 0f, ActionMobility.Mobile);
            var definition = new ThrownExplosiveDefinition(
                "item.frag",
                cost,
                maximumRange: 12f,
                standingLaunchHeight: 1.2f,
                crouchedLaunchHeight: 0.8f,
                baseUncertaintyRadius: 0.5f,
                uncertaintyPerMeter: 0.1f,
                blastRadius: 5f,
                blastWoundMovementPenalty: 2f,
                blastIntegrityDamage: 4f);
            var record = new ThrownExplosiveRecord(
                1,
                "player",
                definition,
                new GameplayPosition(0f, 0f, 0f),
                new GameplayPosition(0f, 1.2f, 0f),
                new GameplayPosition(4f, 0f, 0f),
                new GameplayPosition(4.5f, 0f, 0f),
                new GameplayPosition(4.25f, 0f, 0f),
                0.9f,
                42,
                new[]
                {
                    new BlastEffectRecord(
                        "enemy",
                        BlastSubjectKind.Actor,
                        1.5f,
                        0.5f,
                        0.8f,
                        TargetRegionId.LeftArm),
                    new BlastEffectRecord(
                        "crate",
                        BlastSubjectKind.DestructibleProp,
                        2f,
                        0.25f,
                        0.5f),
                });
            var action = new GameplayActionRecord(
                1,
                new GameplayActionRequest(
                    "player",
                    definition.Id,
                    definition.Id),
                cost,
                new TurnBudget(4, 8f),
                new TurnBudget(2, 8f),
                new GameplayActionOutcome[]
                {
                    new ThrownExplosiveActionOutcome(record),
                    new InventoryQuantityChangedActionOutcome(
                        new InventoryQuantityChangeRecord(
                            "player",
                            definition.Id,
                            previousQuantity: 3,
                            consumedQuantity: 1,
                            resultingQuantity: 2)),
                });

            Assert.That(
                GameplayCombatDiagnosticFormatter.TryFormatAction(
                    action,
                    out GameplayDiagnosticProjection diagnostic),
                Is.True);
            Assert.That(diagnostic.Lines, Has.Some.EqualTo(
                "BLAST enemy - Actor - DISTANCE 1.5 m - OCCLUSION 0.5 "
                + "x FALLOFF 0.8 = EXPOSURE 0.4 - REGION LeftArm"));
            Assert.That(diagnostic.Lines, Has.Some.EqualTo(
                "ACTOR CONSEQUENCE - 2 x 0.4 = 0.8 movement penalty"));
            Assert.That(diagnostic.Lines, Has.Some.EqualTo(
                "PROP CONSEQUENCE - 4 x 0.125 = 0.5 integrity damage"));
            Assert.That(diagnostic.Lines, Has.Some.EqualTo(
                "INVENTORY - item.frag - 3 - 1 = 2"));

            Assert.That(
                GameplayCombatDiagnosticFormatter.TryFormatJournalEntry(
                    new ActionResolvedJournalEntry(1, action),
                    out GameplayDiagnosticProjection journalDiagnostic),
                Is.True);
            Assert.That(journalDiagnostic.Title,
                Is.EqualTo("player THROWS item.frag"));
        }

        [Test]
        public void SmokeDiagnosticExplainsVolumeLifetimeAndSightFormula()
        {
            var cost = new ActionCost(2, 0f, ActionMobility.Mobile);
            var smoke = new SmokeFieldDefinition(4f, 2.8f, 24f, 4, 0.75f);
            var definition = new ThrownExplosiveDefinition(
                "item.smoke-grenade",
                cost,
                12f,
                1.2f,
                0.82f,
                0.55f,
                0.08f,
                0f,
                smokeField: smoke);
            var landing = new GameplayPosition(4f, 0f, 0f);
            var record = new ThrownExplosiveRecord(
                1,
                "player",
                definition,
                new GameplayPosition(0f, 0f, 0f),
                new GameplayPosition(0f, 1.2f, 0f),
                landing,
                landing,
                landing,
                0.87f,
                42,
                Array.Empty<BlastEffectRecord>(),
                new SmokeFieldRecord(
                    "smoke.player.1",
                    "player",
                    definition.Id,
                    landing,
                    smoke));
            var action = new GameplayActionRecord(
                1,
                new GameplayActionRequest(
                    "player",
                    definition.Id,
                    definition.Id),
                cost,
                new TurnBudget(4, 8f),
                new TurnBudget(2, 8f),
                new GameplayActionOutcome[]
                {
                    new ThrownExplosiveActionOutcome(record),
                });

            Assert.That(
                GameplayCombatDiagnosticFormatter.TryFormatAction(
                    action,
                    out GameplayDiagnosticProjection diagnostic),
                Is.True);
            Assert.That(diagnostic.Lines, Has.Some.EqualTo(
                "SMOKE VOLUME - RADIUS 4 m - HEIGHT 2.8 m"));
            Assert.That(diagnostic.Lines, Has.Some.EqualTo(
                "SMOKE LIFETIME - 24 s exploration / 4 ended turns"));
            Assert.That(diagnostic.Lines, Has.Some.EqualTo(
                "SIGHT BLOCK - 0.75 m traversed smoke"));
        }
    }
}
