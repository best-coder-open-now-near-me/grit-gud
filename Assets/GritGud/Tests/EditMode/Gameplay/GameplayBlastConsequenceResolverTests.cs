using System;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;
using NUnit.Framework;

namespace GritGud.Domain.Tests.Gameplay
{
    public sealed class GameplayBlastConsequenceResolverTests
    {
        [Test]
        public void SharedResolverAppliesLocalizedActorAndPropConsequences()
        {
            GameplaySession gameplay = CreateGameplay();
            var destructibles = new DestructiblePropSession(new[]
            {
                new DestructiblePropDefinition(
                    "crate",
                    10f,
                    DestructiblePropState.Intact),
            });
            var resolver = new GameplayBlastConsequenceResolver(
                gameplay,
                destructibles);
            var effects = new[]
            {
                new BlastEffectRecord(
                    "target",
                    BlastSubjectKind.Actor,
                    2f,
                    occlusionExposure: 1f,
                    distanceFalloff: 0.5f,
                    injuryRegion: TargetRegionId.LeftLeg),
                new BlastEffectRecord(
                    "crate",
                    BlastSubjectKind.DestructibleProp,
                    2f,
                    occlusionExposure: 1f,
                    distanceFalloff: 0.5f),
            };

            resolver.Apply(
                effects,
                woundMovementPenalty: 2f,
                integrityDamage: 4f);

            ActorWoundSnapshot wounds = gameplay.GetActor("target").Wounds;
            Assert.That(wounds.LeftLegWounds, Is.EqualTo(1));
            Assert.That(wounds.TorsoWounds, Is.Zero);
            Assert.That(wounds.MovementPenalty, Is.EqualTo(1f));
            Assert.That(
                destructibles.GetProp("crate").RemainingIntegrity,
                Is.EqualTo(8f));
            Assert.That(destructibles.DamageRecords, Has.Count.EqualTo(1));
        }

        [Test]
        public void ExplicitlyUnlocalizedBlastNeverInventsATorsoHit()
        {
            GameplaySession gameplay = CreateGameplay();
            var destructibles = new DestructiblePropSession(
                Array.Empty<DestructiblePropDefinition>());
            var resolver = new GameplayBlastConsequenceResolver(
                gameplay,
                destructibles);

            resolver.Apply(
                new[]
                {
                    new BlastEffectRecord(
                        "target",
                        BlastSubjectKind.Actor,
                        0f,
                        occlusionExposure: 1f,
                        distanceFalloff: 1f),
                },
                woundMovementPenalty: 2f,
                integrityDamage: 0f);

            ActorWoundSnapshot wounds = gameplay.GetActor("target").Wounds;
            Assert.That(wounds.WoundCount, Is.EqualTo(1));
            Assert.That(wounds.UnlocalizedWounds, Is.EqualTo(1));
            Assert.That(wounds.TorsoWounds, Is.Zero);
        }

        private static GameplaySession CreateGameplay()
        {
            var player = new ScenarioActorDefinition(
                "player",
                10,
                new GameplayActorPose(
                    new GameplayPosition(0f, 0f, 0f),
                    0f),
                new TurnBudget(4, 8f));
            var target = new ScenarioActorDefinition(
                "target",
                0,
                new GameplayActorPose(
                    new GameplayPosition(0f, 0f, 3f),
                    180f),
                new TurnBudget(4, 8f));
            return new GameplaySession(new ScenarioDefinition(
                "blast-consequence-test",
                new ScenarioTimingDefinition(1f),
                new[] { player, target },
                Array.Empty<ScenarioObjectiveDefinition>()));
        }
    }
}
