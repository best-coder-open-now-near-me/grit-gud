using System;
using GritGud.Application.Gameplay;
using NUnit.Framework;

namespace GritGud.Domain.Tests.Gameplay
{
    public sealed class GameplayExecutionIdentityTests
    {
        private const string DigestA =
            "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
        private const string DigestB =
            "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789";

        [Test]
        public void ExecutionIdentitySeparatesGameplaySpatialAndRunState()
        {
            GameplayExecutionIdentity first = CreateIdentity(DigestA, DigestB, 17u);
            GameplayExecutionIdentity same = CreateIdentity(DigestA, DigestB, 17u);
            GameplayExecutionIdentity changedRun = CreateIdentity(
                DigestA,
                DigestB,
                18u);

            Assert.That(first.HasSameIdentity(same), Is.True);
            Assert.That(first.HasSameIdentity(changedRun), Is.False);
            Assert.That(
                first.Gameplay.HasSameIdentity(changedRun.Gameplay),
                Is.True);
            Assert.That(
                first.Spatial.HasSameIdentity(changedRun.Spatial),
                Is.True);
        }

        [Test]
        public void IdentityRejectsNonCanonicalDigest()
        {
            Assert.Throws<ArgumentException>(() => new GameplayContentIdentity(
                "scenario",
                1,
                1,
                DigestA.ToUpperInvariant()));
        }

        [Test]
        public void NumericPolicyNormalizesAndRejectsNonFiniteValues()
        {
            Assert.That(
                GameplayNumericPolicy.FormatCanonical(1.234567f),
                Is.EqualTo("1.23457"));
            Assert.That(
                GameplayNumericPolicy.AreEquivalent(2f, 2.00005f),
                Is.True);
            Assert.Throws<ArgumentOutOfRangeException>(
                () => GameplayNumericPolicy.Normalize(float.NaN));
        }

        [Test]
        public void AddressedRandomDependsOnExactTransitionAndPurpose()
        {
            var run = new ScenarioRunIdentity("run-1", 123u);
            var transition = new GameplayTransitionIdentity(
                7L,
                "attack",
                "raider",
                "mara");

            uint first = GameplayAddressedRandom.SampleUInt32(
                run,
                transition,
                "hit");
            uint repeated = GameplayAddressedRandom.SampleUInt32(
                run,
                transition,
                "hit");
            uint anotherPurpose = GameplayAddressedRandom.SampleUInt32(
                run,
                transition,
                "region");
            uint anotherTransition = GameplayAddressedRandom.SampleUInt32(
                run,
                new GameplayTransitionIdentity(
                    8L,
                    "attack",
                    "raider",
                    "mara"),
                "hit");

            Assert.That(first, Is.EqualTo(repeated));
            Assert.That(anotherPurpose, Is.Not.EqualTo(first));
            Assert.That(anotherTransition, Is.Not.EqualTo(first));
        }

        [Test]
        public void AddressedRandomProvidesStableGoldenSamples()
        {
            var run = new ScenarioRunIdentity("golden", 0x12345678u);
            var transition = new GameplayTransitionIdentity(
                42L,
                "displacement",
                "oren",
                "guard");

            Assert.That(
                GameplayAddressedRandom.SampleUInt32(
                    run,
                    transition,
                    "attacker-roll"),
                Is.EqualTo(2575517993u));
            Assert.That(
                GameplayAddressedRandom.RollD20(
                    run,
                    transition,
                    "defender-roll"),
                Is.EqualTo(20));
        }

        private static GameplayExecutionIdentity CreateIdentity(
            string gameplayDigest,
            string spatialDigest,
            uint seed) =>
            new GameplayExecutionIdentity(
                new GameplayContentIdentity(
                    "scenario",
                    15,
                    1,
                    gameplayDigest),
                new SpatialContentIdentity(
                    "level",
                    15,
                    1,
                    spatialDigest),
                new ScenarioRunIdentity("run", seed));
    }
}
