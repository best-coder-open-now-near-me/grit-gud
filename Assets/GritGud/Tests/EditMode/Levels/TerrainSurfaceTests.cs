using System.Linq;
using GritGud.Application.Levels;
using GritGud.Domain.Levels;
using NUnit.Framework;

namespace GritGud.Domain.Tests.Levels
{
    public sealed class TerrainSurfaceTests
    {
        [Test]
        public void NewLevelFactoryCreatesFlatTerrainAcrossItsBounds()
        {
            LevelDocument document = LevelDocumentFactory.CreateNew("Fresh Terrain");

            Assert.That(document.terrainSurfaces, Has.Count.EqualTo(1));
            TerrainSurfaceData terrain = document.terrainSurfaces[0];
            Assert.That(terrain.id, Is.EqualTo("ground"));
            Assert.That(terrain.origin.x, Is.EqualTo(-25f));
            Assert.That(terrain.origin.z, Is.EqualTo(-25f));
            Assert.That(terrain.sampleCountX, Is.EqualTo(26));
            Assert.That(terrain.sampleCountZ, Is.EqualTo(26));
            Assert.That(terrain.sampleSpacing, Is.EqualTo(2f));
            Assert.That(terrain.heightSamples, Has.Count.EqualTo(26 * 26));
            Assert.That(terrain.heightSamples.All(sample => sample == 0), Is.True);
            Assert.That(
                document.scenario.FindInitiallySelectedPlayer().transform.position.y,
                Is.EqualTo(2f));
            Assert.That(LevelValidator.HasErrors(LevelValidator.Validate(document)), Is.False);
        }

        [Test]
        public void ResizeTerrainPreservesCenterAndResamplesAuthoredShape()
        {
            TerrainSurfaceData source = CreateDocument().terrainSurfaces[0];
            source.heightSamples = Enumerable.Range(0, 9).ToList();

            TerrainSurfaceData resized = TerrainSurfaceAuthoring.Resize(source, 4f, 2f, 1f);

            Assert.That(resized.origin.x, Is.EqualTo(-2f));
            Assert.That(resized.origin.z, Is.EqualTo(-1f));
            Assert.That(resized.sampleCountX, Is.EqualTo(5));
            Assert.That(resized.sampleCountZ, Is.EqualTo(3));
            Assert.That(resized.heightSamples, Has.Count.EqualTo(15));
            Assert.That(resized.heightSamples[0], Is.EqualTo(0));
            Assert.That(resized.heightSamples[4], Is.EqualTo(2));
            Assert.That(resized.heightSamples[10], Is.EqualTo(6));
            Assert.That(resized.heightSamples[14], Is.EqualTo(8));
            Assert.That(source.sampleCountX, Is.EqualTo(3));
            Assert.That(source.heightSamples, Is.EqualTo(Enumerable.Range(0, 9)));
        }

        [Test]
        public void ResizeTerrainDoesNotNormalizeItsSource()
        {
            TerrainSurfaceData source = CreateDocument().terrainSurfaces[0];
            source.id = null;

            TerrainSurfaceData resized = TerrainSurfaceAuthoring.Resize(
                source,
                4f,
                2f,
                1f);

            Assert.That(source.id, Is.Null);
            Assert.That(resized.id, Is.Empty);
        }

        [Test]
        public void ValidationLimitsTerrainSurfaceCount()
        {
            LevelDocument document = LevelDocumentFactory.CreateEmpty("Surface Limit");
            for (int index = 0; index <= LevelTerrainValidationRule.MaximumSurfaceCount; index++)
            {
                TerrainSurfaceData surface = CreateDocument().terrainSurfaces[0];
                surface.id = "surface-" + index;
                document.terrainSurfaces.Add(surface);
            }

            var issues = LevelValidator.Validate(document);

            Assert.That(
                issues,
                Has.Some.Matches<LevelValidationIssue>(issue =>
                    issue.Code == "terrain.surface-limit"));
        }

        [Test]
        public void ValidationLimitsTotalTerrainSamples()
        {
            LevelDocument document = LevelDocumentFactory.CreateEmpty("Sample Limit");
            for (int index = 0; index < 4; index++)
            {
                document.terrainSurfaces.Add(new TerrainSurfaceData
                {
                    id = "surface-" + index,
                    sampleCountX = LevelTerrainValidationRule.MaximumSamplesPerAxis,
                    sampleCountZ = LevelTerrainValidationRule.MaximumSamplesPerAxis,
                    sampleSpacing = 1f,
                    elevationIncrement = 1f,
                    heightSamples = Enumerable.Repeat(
                            0,
                            LevelTerrainValidationRule.MaximumSamplesPerSurface)
                        .ToList(),
                });
            }

            var issues = LevelValidator.Validate(document);

            Assert.That(
                issues,
                Has.Some.Matches<LevelValidationIssue>(issue =>
                    issue.Code == "terrain.document-sample-limit"));
        }

        [Test]
        public void ResizeTerrainCommandIsUndoableAndRequiresFullProjection()
        {
            LevelDocument document = CreateDocument();
            TerrainSurfaceData before = document.terrainSurfaces[0];
            TerrainSurfaceData after = TerrainSurfaceAuthoring.Resize(before, 4f, 2f, 1f);
            var command = new SetTerrainSurfaceCommand("ground", before, after);
            var session = new LevelSession(document);

            session.Execute(command);
            Assert.That(session.CreateSnapshot().terrainSurfaces[0].sampleCountX, Is.EqualTo(5));
            Assert.That(command.RequiresFullProjection, Is.True);

            session.Undo();
            Assert.That(session.CreateSnapshot().terrainSurfaces[0].sampleCountX, Is.EqualTo(3));

            session.Redo();
            Assert.That(session.CreateSnapshot().terrainSurfaces[0].sampleCountX, Is.EqualTo(5));
        }

        [Test]
        public void AddFlatTerrainCommandIsUndoableForTerrainlessLevels()
        {
            LevelDocument document = LevelDocumentFactory.CreateEmpty("Legacy Terrainless");
            TerrainSurfaceData terrain = TerrainSurfaceAuthoring.CreateFlat(
                "ground",
                document.bounds,
                TerrainSurfaceAuthoring.DefaultSampleSpacing);
            var command = new AddTerrainSurfaceCommand(terrain);
            var session = new LevelSession(document);

            session.Execute(command);
            Assert.That(session.CreateSnapshot().terrainSurfaces, Has.Count.EqualTo(1));
            Assert.That(command.RequiresFullProjection, Is.True);

            session.Undo();
            Assert.That(session.CreateSnapshot().terrainSurfaces, Is.Empty);

            session.Redo();
            Assert.That(session.CreateSnapshot().terrainSurfaces, Has.Count.EqualTo(1));
        }

        [Test]
        public void ResizeTerrainRejectsDimensionsThatDoNotAlignToTheGrid()
        {
            TerrainSurfaceData source = CreateDocument().terrainSurfaces[0];

            Assert.Throws<System.ArgumentException>(() =>
                TerrainSurfaceAuthoring.Resize(source, 3.5f, 2f, 1f));
        }

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
