using System;
using System.Collections.Generic;
using GritGud.Application.Gameplay;
using GritGud.Domain.Levels;
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
                GameplayNumericPolicy.FormatCanonical(-0f),
                Is.EqualTo("0"));
            Assert.That(
                GameplayNumericPolicy.FormatCanonical(-0.000001f),
                Is.EqualTo("0"));
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

        [Test]
        public void FractureCatalogParticipatesInStaticSpatialIdentity()
        {
            var level = new LevelDocument
            {
                levelId = "fracture-identity",
                entities = new List<LevelEntity>
                {
                    new LevelEntity
                    {
                        id = "cover",
                        archetypeId = "cover.fracturable",
                        destructible = new DestructibleInstanceData
                        {
                            enabled = true,
                            initialState = "intact",
                            integrity = 10f,
                        },
                    },
                },
            };
            var catalog = new GameplayFractureSpatialCatalogDocument
            {
                profiles = new List<GameplayFractureSpatialProfileData>
                {
                    new GameplayFractureSpatialProfileData
                    {
                        archetypeId = "cover.fracturable",
                        profileId = "fracture.cover",
                        chunks = new List<GameplayFractureSpatialChunkData>
                        {
                            Chunk(0f),
                            Chunk(1f),
                        },
                    },
                },
            };
            var first = new GameplayStaticSpatialContent(level, catalog);
            GameplayFractureSpatialCatalogDocument changed =
                catalog.DeepCopy();
            changed.profiles[0].chunks[0].center.x = 0.25f;
            var second = new GameplayStaticSpatialContent(level, changed);
            LevelDocument presentationChangedLevel = level.DeepCopy();
            presentationChangedLevel.displayName = "Changed presentation label";
            var third = new GameplayStaticSpatialContent(
                presentationChangedLevel,
                catalog);
            LevelDocument geometryChangedLevel = level.DeepCopy();
            geometryChangedLevel.entities[0].transform.position.x = 0.25f;
            var fourth = new GameplayStaticSpatialContent(
                geometryChangedLevel,
                catalog);

            Assert.That(
                first.ResolveFractureChunkCount(level.entities[0]),
                Is.EqualTo(2));
            Assert.That(
                first.Identity.HasSameIdentity(second.Identity),
                Is.False);
            Assert.That(
                first.Identity.HasSameIdentity(third.Identity),
                Is.True);
            Assert.That(
                first.Identity.HasSameIdentity(fourth.Identity),
                Is.False);
        }

        [Test]
        public void SourceSpatialIdentityIsStableAcrossFormattingAndKeyOrder()
        {
            string first = GameplayStaticSpatialContent
                .CalculateCanonicalSourceDigest(
                    "{\"levelId\":\"depot\",\"schemaVersion\":17}",
                    "{\"profiles\":[],\"schemaVersion\":1}");
            string formatted = GameplayStaticSpatialContent
                .CalculateCanonicalSourceDigest(
                    "{ \"schemaVersion\" : 17, \"levelId\" : \"depot\" }",
                    "{\n\"schemaVersion\":1,\n\"profiles\":[]\n}");
            string changed = GameplayStaticSpatialContent
                .CalculateCanonicalSourceDigest(
                    "{\"levelId\":\"depot-variant\",\"schemaVersion\":17}",
                    "{\"profiles\":[],\"schemaVersion\":1}");

            Assert.That(first, Is.EqualTo(formatted));
            Assert.That(first, Is.Not.EqualTo(changed));
        }

        private static GameplayFractureSpatialChunkData Chunk(float x) =>
            new GameplayFractureSpatialChunkData
            {
                center = new Float3Data(x, 0.5f, 0f),
                size = new Float3Data(1f, 1f, 1f),
            };

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
