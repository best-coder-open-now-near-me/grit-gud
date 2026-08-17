using GritGud.Application.Levels;
using GritGud.Domain.Levels;
using GritGud.Presentation.LevelEditing;
using GritGud.Presentation.Levels.Runtime;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GritGud.Presentation.Tests
{
    public sealed class LevelEditorPlayabilityCoordinatorTests
    {
        [Test]
        public void ReportStalenessAndOverlayRemainTransient()
        {
            LevelDocument document = LevelDocumentFactory.CreateNew("Diagnostics");
            using var workspace = new LevelEditorWorkspace(document);
            var root = new GameObject("Playability Coordinator Test");
            var projector = new TerrainWorldProjector(root.transform);
            try
            {
                projector.Replace(document);
                var coordinator = new LevelEditorPlayabilityCoordinator(workspace, projector);

                coordinator.Run();
                coordinator.SetSlopeOverlay(true);

                Assert.That(coordinator.Report, Is.Not.Null);
                Assert.That(coordinator.IsStale, Is.False);
                Assert.That(coordinator.SlopeOverlayEnabled, Is.True);
                Assert.That(root.GetComponentInChildren<MeshRenderer>()
                    .sharedMaterial.GetFloat("_TerrainDiagnosticsEnabled"), Is.EqualTo(1f));

                coordinator.MarkStale();
                coordinator.SetAuthoringProjectionVisible(false);

                Assert.That(coordinator.IsStale, Is.True);
                Assert.That(root.GetComponentInChildren<MeshRenderer>()
                    .sharedMaterial.GetFloat("_TerrainDiagnosticsEnabled"), Is.Zero);
                Assert.That(workspace.CreateSnapshot().terrainSurfaces[0].appearance.presetId,
                    Is.EqualTo("slate"));
            }
            finally
            {
                projector.Dispose();
                Object.DestroyImmediate(root);
            }
        }
    }
}
