using GritGud.Application.Levels;
using GritGud.Domain.Levels;
using GritGud.Presentation.LevelEditing;
using GritGud.Presentation.LevelEditing.Core;
using GritGud.Presentation.LevelEditing.Tools;
using GritGud.Presentation.Levels.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace GritGud.Presentation.Tests
{
    public sealed class SelectionLevelEditorToolTests
    {
        [Test]
        public void DraggingSecondarySelectionUsesClickedEntityAsGestureAnchor()
        {
            LevelDocument document = LevelDocumentFactory.CreateEmpty();
            document.entities.Add(CreateEntity("left", -2f));
            document.entities.Add(CreateEntity("right", 2f));
            var root = new GameObject("Selection Tool Test");
            var cameraObject = new GameObject("Selection Tool Camera");

            try
            {
                using var workspace = new LevelEditorWorkspace(document);
                var selection = new LevelSelectionModel();
                using var projector = new LevelWorldProjector(
                    LevelArchetypeCatalog.LoadDefault(),
                    root.transform);
                using var terrainProjector = new TerrainWorldProjector(root.transform);
                projector.Replace(document);
                var camera = cameraObject.AddComponent<Camera>();
                var context = new LevelEditorToolContext(
                    workspace,
                    selection,
                    projector,
                    terrainProjector,
                    new LevelEditorSceneQuery(camera),
                    new LevelSnapSettings { Enabled = false },
                    _ => { },
                    _ => { });
                var tool = new SelectionLevelEditorTool();
                tool.Activate(context);
                selection.Set(new[]
                {
                    new LevelSelectionTarget("left"),
                    new LevelSelectionTarget("right"),
                });
                Assert.That(projector.TryGetEntity("right", out LevelEntityView right), Is.True);

                tool.BeginDrag(
                    right,
                    new Ray(new Vector3(2f, 10f, 0f), Vector3.down));
                tool.UpdateDrag(
                    new Ray(new Vector3(5f, 10f, 0f), Vector3.down));
                tool.CommitDrag();

                Assert.That(
                    workspace.FindEntitySnapshot("left").transform.position.x,
                    Is.EqualTo(1f).Within(0.001f));
                Assert.That(
                    workspace.FindEntitySnapshot("right").transform.position.x,
                    Is.EqualTo(5f).Within(0.001f));
                Assert.That(workspace.Undo(), Is.True);
                Assert.That(
                    workspace.FindEntitySnapshot("left").transform.position.x,
                    Is.EqualTo(-2f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void RotationWorksAfterReturningFromPlacementTool()
        {
            LevelDocument document = LevelDocumentFactory.CreateEmpty();
            document.entities.Add(CreateEntity("crate", 0f));
            var root = new GameObject("Selection Rotation Test");
            var cameraObject = new GameObject("Selection Rotation Camera");

            try
            {
                using var workspace = new LevelEditorWorkspace(document);
                var selection = new LevelSelectionModel();
                using var projector = new LevelWorldProjector(
                    LevelArchetypeCatalog.LoadDefault(), root.transform);
                using var terrainProjector = new TerrainWorldProjector(root.transform);
                projector.Replace(document);
                var context = new LevelEditorToolContext(
                    workspace,
                    selection,
                    projector,
                    terrainProjector,
                    new LevelEditorSceneQuery(cameraObject.AddComponent<Camera>()),
                    new LevelSnapSettings(),
                    _ => { },
                    _ => { });
                var selectionTool = new SelectionLevelEditorTool();
                var placementTool = new PlacementLevelEditorTool();
                using var manager = new LevelEditorToolManager(
                    context, SelectionLevelEditorTool.ToolId);
                manager.Register(selectionTool);
                manager.Register(placementTool);
                manager.Activate(PlacementLevelEditorTool.ToolId);
                selection.SetSingle("crate");

                manager.ActivateDefault();
                selectionTool.RotateSelection(15f);

                Assert.That(
                    workspace.FindEntitySnapshot("crate").transform.yawDegrees,
                    Is.EqualTo(15f));
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void RotationKeepsCustomPivotFixedInWorldSpace()
        {
            LevelDocument document = LevelDocumentFactory.CreateEmpty();
            LevelEntity entity = CreateEntity("crate", 0f);
            entity.rotationPivot = new LevelRotationPivotData
            {
                localPosition = new Float3Data(1f, 0f, 0f),
            };
            document.entities.Add(entity);
            var root = new GameObject("Pivot Rotation Test");
            var cameraObject = new GameObject("Pivot Rotation Camera");

            try
            {
                using var workspace = new LevelEditorWorkspace(document);
                var selection = new LevelSelectionModel();
                using var projector = new LevelWorldProjector(
                    LevelArchetypeCatalog.LoadDefault(), root.transform);
                using var terrainProjector = new TerrainWorldProjector(root.transform);
                projector.Replace(document);
                var context = new LevelEditorToolContext(
                    workspace,
                    selection,
                    projector,
                    terrainProjector,
                    new LevelEditorSceneQuery(cameraObject.AddComponent<Camera>()),
                    new LevelSnapSettings(),
                    _ => { },
                    _ => { });
                var tool = new SelectionLevelEditorTool();
                tool.Activate(context);
                selection.SetSingle("crate");

                tool.RotateSelection(90f);

                LevelTransformData result = workspace.FindEntitySnapshot("crate").transform;
                Assert.That(result.yawDegrees, Is.EqualTo(90f));
                Assert.That(result.position.x, Is.EqualTo(1f).Within(0.001f));
                Assert.That(result.position.z, Is.EqualTo(1f).Within(0.001f));
                Assert.That(projector.TryGetEntity("crate", out LevelEntityView view), Is.True);
                Assert.That(view.GetRotationPivotWorld().x, Is.EqualTo(1f).Within(0.001f));
                Assert.That(view.GetRotationPivotWorld().z, Is.EqualTo(0f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(root);
            }
        }

        private static LevelEntity CreateEntity(string id, float x)
        {
            return new LevelEntity
            {
                id = id,
                archetypeId = "prop.crate.standard",
                transform = new LevelTransformData(new Float3Data(x, 0f, 0f), 0f),
            };
        }
    }
}
