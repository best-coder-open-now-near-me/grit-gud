using GritGud.Domain.Levels;
using NUnit.Framework;

namespace GritGud.Domain.Tests.Levels
{
    public sealed class LevelCopyTests
    {
        [Test]
        public void DeepCopyNormalizesDestinationWithoutMutatingSource()
        {
            var scenario = new LevelScenarioData
            {
                actors = null,
                objectives = null,
                props = null,
                vehicles = null,
            };
            var source = new LevelDocument
            {
                levelId = null,
                displayName = null,
                entities = null,
                terrainSurfaces = null,
                scenario = scenario,
            };

            LevelDocument copy = source.DeepCopy();

            Assert.That(source.levelId, Is.Null);
            Assert.That(source.displayName, Is.Null);
            Assert.That(source.entities, Is.Null);
            Assert.That(source.terrainSurfaces, Is.Null);
            Assert.That(source.scenario.actors, Is.Null);
            Assert.That(copy.levelId, Is.Empty);
            Assert.That(copy.displayName, Is.Empty);
            Assert.That(copy.entities, Is.Empty);
            Assert.That(copy.terrainSurfaces, Is.Empty);
            Assert.That(copy.scenario.actors, Is.Empty);
        }

        [Test]
        public void ValidationDoesNotNormalizeTheSourceDocument()
        {
            var source = new LevelDocument
            {
                levelId = null,
                entities = null,
                scenario = null,
            };

            _ = LevelValidator.Validate(source);

            Assert.That(source.levelId, Is.Null);
            Assert.That(source.entities, Is.Null);
            Assert.That(source.scenario, Is.Null);
        }

        [Test]
        public void InitiallySelectedPlayerLookupDoesNotNormalizeScenario()
        {
            var scenario = new LevelScenarioData { actors = null };

            Assert.That(scenario.FindInitiallySelectedPlayer(), Is.Null);
            Assert.That(scenario.actors, Is.Null);
        }
    }
}
