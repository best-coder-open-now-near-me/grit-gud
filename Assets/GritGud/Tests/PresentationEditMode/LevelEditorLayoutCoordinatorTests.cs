using System.Linq;
using GritGud.Application.Levels;
using GritGud.Domain.Levels;
using GritGud.Presentation.LevelEditing;
using GritGud.Presentation.LevelEditing.Core;
using NUnit.Framework;
using UnityEngine;

namespace GritGud.Presentation.Tests
{
    public sealed class LevelEditorLayoutCoordinatorTests
    {
        private GameObject cameraObject;
        private LevelEditorCameraController camera;

        [SetUp]
        public void SetUp()
        {
            cameraObject = new GameObject("Layout Camera");
            camera = new LevelEditorCameraController(cameraObject.AddComponent<Camera>());
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(cameraObject);
        }

        [Test]
        public void BoundsEditIsValidatedAndUndoable()
        {
            using var workspace = new LevelEditorWorkspace(LevelDocumentFactory.CreateEmpty());
            var selection = new LevelSelectionModel();
            var grid = new LevelEditorGridSettings();
            var coordinator = new LevelEditorLayoutCoordinator(
                workspace, selection, camera, grid);

            coordinator.ApplyBounds(new LevelBoundsAuthoringRequest
            {
                centerX = "4",
                centerY = "3",
                centerZ = "-2",
                sizeX = "80",
                sizeY = "16",
                sizeZ = "60",
            });

            Assert.That(workspace.CreateSnapshot().bounds.center.x, Is.EqualTo(4f));
            Assert.That(workspace.CreateSnapshot().bounds.size.z, Is.EqualTo(60f));
            workspace.Undo();
            Assert.That(workspace.CreateSnapshot().bounds.size.x, Is.EqualTo(50f));
        }

        [Test]
        public void InvalidGridDoesNotChangeLocalSettingsOrWorkspace()
        {
            using var workspace = new LevelEditorWorkspace(LevelDocumentFactory.CreateEmpty());
            var selection = new LevelSelectionModel();
            var grid = new LevelEditorGridSettings();
            var coordinator = new LevelEditorLayoutCoordinator(
                workspace, selection, camera, grid);
            string status = string.Empty;
            coordinator.StatusChanged += message => status = message;

            coordinator.ConfigureGrid(new LevelGridAuthoringRequest
            {
                visible = true,
                spacing = "0",
                elevation = "2",
            });

            Assert.That(grid.Spacing, Is.EqualTo(2.5f));
            Assert.That(workspace.Revision, Is.Zero);
            Assert.That(status, Does.Contain("greater than zero"));
        }

        [Test]
        public void EntityArrayDuplicatesSelectionAsOneUndoStep()
        {
            LevelDocument document = LevelDocumentFactory.CreateEmpty();
            document.entities.Add(new LevelEntity
            {
                id = "source",
                archetypeId = "prop.crate.standard",
                transform = new LevelTransformData(new Float3Data(1f, 0f, 2f), 0f),
            });
            using var workspace = new LevelEditorWorkspace(document);
            var selection = new LevelSelectionModel();
            selection.SetSingle("source");
            var coordinator = new LevelEditorLayoutCoordinator(
                workspace,
                selection,
                camera,
                new LevelEditorGridSettings());

            coordinator.DuplicateArray(new LevelArrayAuthoringRequest
            {
                countX = "3",
                countZ = "2",
                spacingX = "2.5",
                spacingZ = "4",
            });

            LevelDocument result = workspace.CreateSnapshot();
            Assert.That(result.entities, Has.Count.EqualTo(6));
            Assert.That(result.entities.Any(entity =>
                entity.transform.position.x == 6f
                && entity.transform.position.z == 6f), Is.True);
            Assert.That(selection.Targets, Has.Count.EqualTo(5));
            workspace.Undo();
            Assert.That(workspace.CreateSnapshot().entities, Has.Count.EqualTo(1));
        }

        [Test]
        public void EntityArrayRejectsOperationsTooLargeForInteractiveSelection()
        {
            LevelDocument document = LevelDocumentFactory.CreateEmpty();
            document.entities.Add(new LevelEntity
            {
                id = "source",
                archetypeId = "prop.crate.standard",
            });
            using var workspace = new LevelEditorWorkspace(document);
            var selection = new LevelSelectionModel();
            selection.SetSingle("source");
            var coordinator = new LevelEditorLayoutCoordinator(
                workspace,
                selection,
                camera,
                new LevelEditorGridSettings());
            string status = string.Empty;
            coordinator.StatusChanged += message => status = message;

            coordinator.DuplicateArray(new LevelArrayAuthoringRequest
            {
                countX = "32",
                countZ = "32",
                spacingX = "2.5",
                spacingZ = "2.5",
            });

            Assert.That(workspace.Revision, Is.Zero);
            Assert.That(status, Does.Contain("at most 256"));
        }
    }
}
