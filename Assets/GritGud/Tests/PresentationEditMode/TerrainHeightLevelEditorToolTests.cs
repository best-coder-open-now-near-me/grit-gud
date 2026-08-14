using System.Linq;
using GritGud.Application.Levels;
using GritGud.Domain.Levels;
using GritGud.Presentation.LevelEditing.Tools;
using NUnit.Framework;
using UnityEngine;

namespace GritGud.Presentation.Tests
{
    public sealed class TerrainHeightLevelEditorToolTests
    {
        [Test]
        public void CircularBrushProducesReversibleQuantizedPatch()
        {
            LevelDocument document = LevelDocumentFactory.CreateEmpty("Brush Test");
            var surface = new TerrainSurfaceData
            {
                id = "ground",
                sampleCountX = 3,
                sampleCountZ = 3,
                sampleSpacing = 1f,
                elevationIncrement = 0.1f,
                heightSamples = Enumerable.Repeat(0, 9).ToList(),
            };
            document.terrainSurfaces.Add(surface);
            var command = TerrainBrushCommandFactory.Create(
                surface,
                new Vector3(1f, 0f, 1f),
                1,
                2,
                TerrainBrushMode.Raise);
            command.Apply(document);

            Assert.That(surface.heightSamples,
                Is.EqualTo(new[] { 0, 2, 0, 2, 2, 2, 0, 2, 0 }));
            command.Revert(document);
            Assert.That(surface.heightSamples, Is.EqualTo(new int[9]));
        }

        [Test]
        public void BrushClampsAtTerrainAndQuantizedBounds()
        {
            var surface = new TerrainSurfaceData
            {
                id = "ground",
                sampleCountX = 2,
                sampleCountZ = 2,
                sampleSpacing = 1f,
                elevationIncrement = 0.1f,
                heightSamples = Enumerable
                    .Repeat(LevelTerrainValidationRule.MaximumQuantizedHeight, 4)
                    .ToList(),
            };
            LevelDocument document = LevelDocumentFactory.CreateEmpty("Clamp Test");
            document.terrainSurfaces.Add(surface);
            SetTerrainHeightsCommand command = TerrainBrushCommandFactory.Create(
                surface,
                Vector3.zero,
                4,
                10,
                TerrainBrushMode.Raise);

            Assert.That(command, Is.Null);
            Assert.That(surface.heightSamples.All(value =>
                value == LevelTerrainValidationRule.MaximumQuantizedHeight), Is.True);
        }

        [Test]
        public void FootprintPointsDescribeRequestedWorldRadius()
        {
            var center = new Vector3(4f, 2f, -3f);

            Vector3[] points = TerrainBrushFootprint.BuildPoints(center, 2.5f, 16);

            Assert.That(points, Has.Length.EqualTo(16));
            Assert.That(points.All(point =>
                Mathf.Abs(Vector3.Distance(point, center) - 2.5f) < 0.001f), Is.True);
            Assert.That(points.All(point => Mathf.Approximately(point.y, center.y)), Is.True);
        }

        [Test]
        public void StrokeAccumulatesVisitedSamplesIntoOneCommand()
        {
            var surface = new TerrainSurfaceData
            {
                id = "ground",
                sampleCountX = 5,
                sampleCountZ = 3,
                sampleSpacing = 1f,
                elevationIncrement = 0.1f,
                heightSamples = Enumerable.Repeat(0, 15).ToList(),
            };
            var stroke = new TerrainStrokeAccumulator(
                surface,
                TerrainBrushMode.Raise,
                new Vector3(1f, 0f, 1f));

            Assert.That(stroke.ApplyPoint(new Vector3(1f, 0f, 1f), 1, 1), Is.Not.Null);
            Assert.That(stroke.ApplyPoint(new Vector3(1f, 0f, 1f), 1, 1), Is.Null);
            Assert.That(stroke.ApplyPoint(new Vector3(3f, 0f, 1f), 1, 1), Is.Not.Null);
            SetTerrainHeightsCommand command = stroke.CreateCommand();

            Assert.That(command.StartX, Is.Zero);
            Assert.That(command.Width, Is.EqualTo(5));
            Assert.That(command.Depth, Is.EqualTo(3));
            Assert.That(surface.heightSamples, Is.EqualTo(new int[15]));
        }

        [Test]
        public void SmoothBrushReducesPeakWithoutChangingSamplesOutsideRadius()
        {
            var heights = Enumerable.Repeat(0, 25).ToList();
            heights[12] = 18;
            var surface = new TerrainSurfaceData
            {
                id = "ground",
                sampleCountX = 5,
                sampleCountZ = 5,
                sampleSpacing = 1f,
                elevationIncrement = 0.1f,
                heightSamples = heights,
            };
            LevelDocument document = LevelDocumentFactory.CreateEmpty("Smooth Test");
            document.terrainSurfaces.Add(surface);

            SetTerrainHeightsCommand command = TerrainBrushCommandFactory.Create(
                surface,
                new Vector3(2f, 0f, 2f),
                1,
                4,
                TerrainBrushMode.Smooth);
            command.Apply(document);

            Assert.That(surface.heightSamples[12], Is.EqualTo(14));
            Assert.That(surface.heightSamples[0], Is.Zero);
            command.Revert(document);
            Assert.That(surface.heightSamples[12], Is.EqualTo(18));
        }

        [Test]
        public void FlattenStrokeKeepsInitialTargetAcrossDraggedPoints()
        {
            var heights = Enumerable.Repeat(0, 15).ToList();
            heights[6] = 4;
            heights[8] = 10;
            var surface = new TerrainSurfaceData
            {
                id = "ground",
                sampleCountX = 5,
                sampleCountZ = 3,
                sampleSpacing = 1f,
                elevationIncrement = 0.1f,
                heightSamples = heights,
            };
            var stroke = new TerrainStrokeAccumulator(
                surface,
                TerrainBrushMode.Flatten,
                new Vector3(1f, 0.4f, 1f));

            stroke.ApplyPoint(new Vector3(1f, 0f, 1f), 1, 2);
            stroke.ApplyPoint(new Vector3(3f, 0f, 1f), 1, 2);

            Assert.That(stroke.FlattenTargetHeight, Is.EqualTo(4));
            Assert.That(stroke.PreviewSurface.heightSamples[8], Is.EqualTo(8));
            Assert.That(stroke.CreateCommand(), Is.Not.Null);
        }
    }
}
