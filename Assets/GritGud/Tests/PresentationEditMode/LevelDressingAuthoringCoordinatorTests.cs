using System.Linq;
using GritGud.Application.Levels;
using GritGud.Domain.Levels;
using GritGud.Presentation.LevelEditing;
using GritGud.Presentation.LevelEditing.Core;
using NUnit.Framework;
using UnityEngine;

namespace GritGud.Presentation.Tests
{
    public sealed class LevelDressingAuthoringCoordinatorTests
    {
        [Test]
        public void AddsEveryDressingTypeAtCameraFocusThroughUndoableHistory()
        {
            using var workspace = new LevelEditorWorkspace(
                LevelDocumentFactory.CreateEmpty("Dressing"));
            LevelDressingAuthoringCoordinator coordinator = CreateCoordinator(workspace);
            LevelDressingTargetKind focusedKind = default;
            string focusedId = string.Empty;
            coordinator.FocusRequested += (kind, id) =>
            {
                focusedKind = kind;
                focusedId = id;
            };

            coordinator.AddDecal();
            coordinator.AddAmbientVfx();
            coordinator.AddAudioZone();

            LevelDressingData dressing = workspace.CreateSnapshot().dressing;
            Assert.That(dressing.decals.Single().position.x, Is.EqualTo(8f));
            Assert.That(dressing.ambientVfx.Single().position.z, Is.EqualTo(-2f));
            Assert.That(dressing.audioZones.Single().center.y, Is.EqualTo(3f));
            Assert.That(focusedKind, Is.EqualTo(LevelDressingTargetKind.AudioZone));
            Assert.That(focusedId, Is.EqualTo(dressing.audioZones.Single().id));
            workspace.Undo();
            Assert.That(workspace.CreateSnapshot().dressing.audioZones, Is.Empty);
        }

        [Test]
        public void AppliesPortableDecalVfxAndAudioValues()
        {
            using var workspace = new LevelEditorWorkspace(
                LevelDocumentFactory.CreateEmpty("Dressing"));
            LevelDressingAuthoringCoordinator coordinator = CreateCoordinator(workspace);
            coordinator.AddDecal();
            coordinator.AddAmbientVfx();
            coordinator.AddAudioZone();
            LevelDressingData added = workspace.CreateSnapshot().dressing;

            coordinator.ApplyDecal(new LevelDecalAuthoringRequest
            {
                id = added.decals.Single().id,
                displayName = "Bay Arrow",
                styleId = "arrow",
                position = Vector("1", "0.02", "2"),
                rotation = Vector("-90", "0", "15"),
                size = Vector("2", "3", "1"),
                color = Color("0.2", "0.8", "1"),
                alpha = "0.7",
            });
            coordinator.ApplyAmbientVfx(new LevelAmbientVfxAuthoringRequest
            {
                id = added.ambientVfx.Single().id,
                displayName = "Bay Haze",
                effectId = "ground-haze",
                position = Vector("1", "0.2", "2"),
                rotation = Vector("0", "30", "0"),
                scale = Vector("2", "1", "2"),
            });
            coordinator.ApplyAudioZone(new LevelAudioZoneAuthoringRequest
            {
                id = added.audioZones.Single().id,
                displayName = "Bay Wind",
                soundId = "wind",
                center = Vector("1", "2", "2"),
                size = Vector("8", "4", "8"),
                volume = "0.2",
                fadeDistance = "4",
            });

            LevelDressingData result = workspace.CreateSnapshot().dressing;
            Assert.That(result.decals.Single().styleId, Is.EqualTo("arrow"));
            Assert.That(result.decals.Single().color.a, Is.EqualTo(0.7f));
            Assert.That(result.ambientVfx.Single().effectId, Is.EqualTo("ground-haze"));
            Assert.That(result.audioZones.Single().soundId, Is.EqualTo("wind"));
        }

        [Test]
        public void InvalidEditLeavesWorkspaceUnchangedAndReportsStatus()
        {
            using var workspace = new LevelEditorWorkspace(
                LevelDocumentFactory.CreateEmpty("Dressing"));
            LevelDressingAuthoringCoordinator coordinator = CreateCoordinator(workspace);
            coordinator.AddDecal();
            int revision = workspace.Revision;
            string status = string.Empty;
            coordinator.StatusChanged += message => status = message;
            string id = workspace.CreateSnapshot().dressing.decals.Single().id;

            coordinator.ApplyDecal(new LevelDecalAuthoringRequest
            {
                id = id,
                displayName = "Invalid",
                styleId = "grime",
                position = Vector("0", "0", "0"),
                rotation = Vector("-90", "0", "0"),
                size = Vector("0", "2", "1"),
                color = Color("0.1", "0.1", "0.1"),
                alpha = "0.5",
            });

            Assert.That(workspace.Revision, Is.EqualTo(revision));
            Assert.That(status, Does.Contain("invalid transform"));
        }

        private static LevelDressingAuthoringCoordinator CreateCoordinator(
            LevelEditorWorkspace workspace) =>
            new LevelDressingAuthoringCoordinator(
                workspace,
                () => new LevelEditorCameraState
                {
                    target = new Vector3(8f, 3f, -2f),
                    yaw = 20f,
                    pitch = 55f,
                    distance = 12f,
                });

        private static LevelColorAuthoringText Color(string r, string g, string b) =>
            new LevelColorAuthoringText { r = r, g = g, b = b };

        private static LevelVectorAuthoringText Vector(string x, string y, string z) =>
            new LevelVectorAuthoringText { x = x, y = y, z = z };
    }
}
