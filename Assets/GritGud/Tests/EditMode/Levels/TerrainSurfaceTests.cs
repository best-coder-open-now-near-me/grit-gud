using System.Linq;
using GritGud.Application.Levels;
using GritGud.Domain.Levels;
using NUnit.Framework;

namespace GritGud.Domain.Tests.Levels
{
    public sealed class TerrainSurfaceTests
    {
        [Test]
        public void TerrainPatchUndoAndRedoRestoreExactSamples()
        {
            LevelDocument document = CreateDocument();
            var session = new LevelSession(document);
            var command = new SetTerrainHeightsCommand(
                "ground",
                1,
                0,
                2,
                2,
                new[] { 10, 11, 12, 13 });

            session.Execute(command);
            Assert.That(session.CreateSnapshot().terrainSurfaces[0].heightSamples,
                Is.EqualTo(new[] { 0, 10, 11, 0, 12, 13, 0, 0, 0 }));

            session.Undo();
            Assert.That(session.CreateSnapshot().terrainSurfaces[0].heightSamples,
                Is.EqualTo(new int[9]));

            session.Redo();
            Assert.That(session.CreateSnapshot().terrainSurfaces[0].heightSamples,
                Is.EqualTo(new[] { 0, 10, 11, 0, 12, 13, 0, 0, 0 }));
        }

        [Test]
        public void TerrainPatchReportsItsAffectedRegion()
        {
            var command = new SetTerrainHeightsCommand(
                "ground",
                2,
                3,
                4,
                5,
                Enumerable.Repeat(0, 20));

            Assert.That(command.SurfaceId, Is.EqualTo("ground"));
            Assert.That(command.StartX, Is.EqualTo(2));
            Assert.That(command.StartZ, Is.EqualTo(3));
            Assert.That(command.Width, Is.EqualTo(4));
            Assert.That(command.Depth, Is.EqualTo(5));
            Assert.That(command.AffectedEntityIds, Is.Empty);
            Assert.That(command.RequiresFullProjection, Is.False);
        }

        [Test]
        public void TerrainPatchOutsideSurfaceDoesNotMutateDocument()
        {
            LevelDocument document = CreateDocument();
            var session = new LevelSession(document);
            var command = new SetTerrainHeightsCommand(
                "ground",
                2,
                2,
                2,
                1,
                new[] { 1, 2 });

            Assert.Throws<System.InvalidOperationException>(() => session.Execute(command));
            Assert.That(session.CreateSnapshot().terrainSurfaces[0].heightSamples,
                Is.EqualTo(new int[9]));
            Assert.That(session.HistoryPosition, Is.Zero);
        }

        [Test]
        public void TerrainSnapshotDoesNotShareHeightSamples()
        {
            LevelDocument document = CreateDocument();

            LevelDocument copy = document.DeepCopy();
            copy.terrainSurfaces[0].heightSamples[0] = 99;

            Assert.That(document.terrainSurfaces[0].heightSamples[0], Is.Zero);
        }

        [Test]
        public void WorkspaceTerrainLookupReturnsDetachedSurface()
        {
            using var workspace = new LevelEditorWorkspace(CreateDocument());

            TerrainSurfaceData result = workspace.FindTerrainSurfaceSnapshot("ground");
            result.heightSamples[0] = 42;

            Assert.That(workspace.FindTerrainSurfaceSnapshot("ground").heightSamples[0], Is.Zero);
            Assert.That(workspace.FindTerrainSurfaceSnapshot("missing"), Is.Null);
        }

        [Test]
        public void TerrainValidationRejectsMismatchedSampleCount()
        {
            LevelDocument document = CreateDocument();
            document.terrainSurfaces[0].heightSamples.RemoveAt(0);

            var issues = LevelValidator.Validate(document);

            Assert.That(issues.Any(issue => issue.Code == "terrain.samples"), Is.True);
        }

        [Test]
        public void ValidTerrainPassesDefaultValidation()
        {
            var issues = LevelValidator.Validate(CreateDocument());

            Assert.That(LevelValidator.HasErrors(issues), Is.False);
        }

        private static LevelDocument CreateDocument()
        {
            LevelDocument document = LevelDocumentFactory.CreateEmpty("Terrain Test");
            document.terrainSurfaces.Add(new TerrainSurfaceData
            {
                id = "ground",
                origin = new Float3Data(-1f, 0f, -1f),
                sampleCountX = 3,
                sampleCountZ = 3,
                sampleSpacing = 1f,
                minimumElevation = 0f,
                elevationIncrement = 0.1f,
                heightSamples = Enumerable.Repeat(0, 9).ToList(),
            });
            return document;
        }
    }
}
