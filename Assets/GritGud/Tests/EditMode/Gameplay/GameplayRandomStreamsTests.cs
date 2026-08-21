using System;
using GritGud.Application.Gameplay;
using NUnit.Framework;

namespace GritGud.Domain.Tests.Gameplay
{
    public sealed class GameplayRandomStreamsTests
    {
        [Test]
        public void NamedStreamsDeriveStableIndependentSeeds()
        {
            const uint scenarioSeed = 12648430u;

            uint attack = GameplayRandomStreams.DeriveSeed(
                scenarioSeed,
                GameplayRandomStreams.AttackResolution);
            uint displacement = GameplayRandomStreams.DeriveSeed(
                scenarioSeed,
                GameplayRandomStreams.DisplacementControl);
            uint thrownExplosive = GameplayRandomStreams.DeriveSeed(
                scenarioSeed,
                GameplayRandomStreams.ThrownExplosiveUncertainty);

            Assert.That(attack, Is.EqualTo(325546531u));
            Assert.That(displacement, Is.EqualTo(1310517933u));
            Assert.That(thrownExplosive, Is.EqualTo(1248124387u));
            Assert.That(attack, Is.Not.EqualTo(displacement));
            Assert.That(attack, Is.Not.EqualTo(thrownExplosive));
            Assert.That(displacement, Is.Not.EqualTo(thrownExplosive));
        }

        [Test]
        public void DerivationIncludesScenarioSeedAndRejectsUnnamedStreams()
        {
            uint first = GameplayRandomStreams.DeriveSeed(
                1u,
                GameplayRandomStreams.AttackResolution);
            uint repeated = GameplayRandomStreams.DeriveSeed(
                1u,
                GameplayRandomStreams.AttackResolution);
            uint anotherScenario = GameplayRandomStreams.DeriveSeed(
                2u,
                GameplayRandomStreams.AttackResolution);

            Assert.That(first, Is.Not.Zero);
            Assert.That(repeated, Is.EqualTo(first));
            Assert.That(anotherScenario, Is.Not.EqualTo(first));
            Assert.Throws<ArgumentException>(() =>
                GameplayRandomStreams.DeriveSeed(1u, " "));
        }
    }
}
