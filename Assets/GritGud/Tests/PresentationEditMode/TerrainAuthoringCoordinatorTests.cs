using GritGud.Application.Levels;
using GritGud.Domain.Levels;
using GritGud.Presentation.LevelEditing;
using NUnit.Framework;

namespace GritGud.Presentation.Tests
{
    public sealed class TerrainAuthoringCoordinatorTests
    {
        [Test]
        public void CreateFlatTerrainUsesWorkspaceHistory()
        {
            using var workspace = new LevelEditorWorkspace(
                LevelDocumentFactory.CreateEmpty("Terrain Authoring"));
            var coordinator = new TerrainAuthoringCoordinator(workspace);

            coordinator.CreateFlatTerrain();

            Assert.That(workspace.CreateSnapshot().terrainSurfaces, Has.Count.EqualTo(1));
            Assert.That(workspace.CanUndo, Is.True);
            workspace.Undo();
            Assert.That(workspace.CreateSnapshot().terrainSurfaces, Is.Empty);
        }

        [Test]
        public void InvalidResizeTextLeavesWorkspaceUnchangedAndReportsStatus()
        {
            using var workspace = new LevelEditorWorkspace(
                LevelDocumentFactory.CreateNew("Terrain Authoring"));
            var coordinator = new TerrainAuthoringCoordinator(workspace);
            string status = string.Empty;
            coordinator.StatusChanged += message => status = message;
            int revision = workspace.Revision;

            coordinator.ResizeTerrain("ground", "wide", "50", "2");

            Assert.That(workspace.Revision, Is.EqualTo(revision));
            Assert.That(status, Does.Contain("finite numbers"));
        }

        [Test]
        public void ResizeTerrainParsesInvariantValuesAndIsUndoable()
        {
            using var workspace = new LevelEditorWorkspace(
                LevelDocumentFactory.CreateNew("Terrain Authoring"));
            var coordinator = new TerrainAuthoringCoordinator(workspace);

            coordinator.ResizeTerrain("ground", "40.5", "30", "1.5");

            TerrainSurfaceData resized = workspace.FindTerrainSurfaceSnapshot("ground");
            Assert.That(TerrainSurfaceAuthoring.Width(resized), Is.EqualTo(40.5f));
            Assert.That(TerrainSurfaceAuthoring.Depth(resized), Is.EqualTo(30f));
            Assert.That(resized.sampleSpacing, Is.EqualTo(1.5f));
            Assert.That(workspace.CanUndo, Is.True);
            workspace.Undo();
            Assert.That(
                TerrainSurfaceAuthoring.Width(
                    workspace.FindTerrainSurfaceSnapshot("ground")),
                Is.EqualTo(50f));
        }
    }
}
