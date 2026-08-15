using System.Collections.Generic;
using System.Linq;
using GritGud.Application.Levels;
using GritGud.Domain.Levels;
using NUnit.Framework;

namespace GritGud.Domain.Tests.Levels
{
    public sealed class LevelPlayabilityAnalyzerTests
    {
        [Test]
        public void FlatConnectedTerrainReportsWalkableScenarioRoute()
        {
            LevelDocument document = CreateScenario(
                new[]
                {
                    0, 0, 0, 0, 0,
                    0, 0, 0, 0, 0,
                    0, 0, 0, 0, 0,
                },
                playerX: 0f,
                objectiveX: 4f);

            LevelPlayabilityReport report = LevelPlayabilityAnalyzer.Analyze(document);

            Assert.That(report.Surfaces.Count, Is.EqualTo(1));
            Assert.That(report.Surfaces[0].WalkablePercent, Is.EqualTo(100f));
            Assert.That(report.Surfaces[0].ConnectedRegionCount, Is.EqualTo(1));
            Assert.That(report.WarningCount, Is.Zero);
            Assert.That(report.AnchorCount, Is.EqualTo(2));
        }

        [Test]
        public void CliffSeparatingObjectiveProducesTerrainRouteWarning()
        {
            LevelDocument document = CreateScenario(
                new[]
                {
                    0, 0, 10, 10, 10,
                    0, 0, 10, 10, 10,
                    0, 0, 10, 10, 10,
                },
                playerX: 0f,
                objectiveX: 4f);

            LevelPlayabilityReport report = LevelPlayabilityAnalyzer.Analyze(document);

            Assert.That(report.Surfaces[0].ConnectedRegionCount, Is.EqualTo(2));
            Assert.That(report.Diagnostics, Has.Some.Matches<LevelPlayabilityDiagnostic>(item =>
                item.Code == "playability.objective.terrain-disconnected"
                && item.EntityId == "objective-console"));
        }

        [Test]
        public void MissingTerrainAndOverlappingActorsAreReportedWithoutBlockingValidation()
        {
            LevelDocument document = LevelDocumentFactory.CreateEmpty("Indoor Level");
            document.scenario.actors[0].transform = new LevelTransformData(
                new Float3Data(2f, 1f, 2f),
                0f);
            document.scenario.actors.Add(new LevelScenarioActorData
            {
                id = "enemy",
                templateId = "enemy",
                transform = new LevelTransformData(new Float3Data(2.2f, 1f, 2.1f), 0f),
            });

            LevelPlayabilityReport report = LevelPlayabilityAnalyzer.Analyze(document);

            Assert.That(report.UnsupportedAnchorCount, Is.EqualTo(2));
            Assert.That(report.Diagnostics, Has.Some.Matches<LevelPlayabilityDiagnostic>(item =>
                item.Code == "playability.terrain.none"));
            Assert.That(report.Diagnostics, Has.Some.Matches<LevelPlayabilityDiagnostic>(item =>
                item.Code == "playability.actor.overlap"));
        }

        private static LevelDocument CreateScenario(
            IEnumerable<int> heights,
            float playerX,
            float objectiveX)
        {
            List<int> samples = heights.ToList();
            LevelDocument document = LevelDocumentFactory.CreateEmpty("Playability Test");
            document.scenario.actors[0].transform = new LevelTransformData(
                new Float3Data(playerX, samples[5 + (int)playerX], 1f),
                0f);
            document.terrainSurfaces.Add(new TerrainSurfaceData
            {
                id = "ground",
                origin = new Float3Data(0f, 0f, 0f),
                sampleCountX = 5,
                sampleCountZ = 3,
                sampleSpacing = 1f,
                elevationIncrement = 1f,
                heightSamples = samples,
            });
            document.entities.Add(new LevelEntity
            {
                id = "objective-console",
                archetypeId = "prop.crate.standard",
                transform = new LevelTransformData(
                    new Float3Data(objectiveX, samples[5 + (int)objectiveX], 1f),
                    0f),
                interactionPoints = new List<InteractionPointData>
                {
                    new InteractionPointData
                    {
                        id = "use",
                        type = "objective",
                        radius = 0.5f,
                    },
                },
            });
            document.scenario.objectives.Add(new LevelScenarioObjectiveData
            {
                id = "objective",
                entityId = "objective-console",
                interactionPointId = "use",
                displayName = "Console",
                actionId = "use",
            });
            return document;
        }
    }
}
