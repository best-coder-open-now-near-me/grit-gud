using GritGud.Application.Levels;
using GritGud.Domain.Levels;
using GritGud.Presentation.LevelEditing;
using GritGud.Presentation.LevelEditing.Core;
using NUnit.Framework;
using UnityEngine;

namespace GritGud.Presentation.Tests
{
    public sealed class EnvironmentAuthoringCoordinatorTests
    {
        [Test]
        public void AppliesPortableEnvironmentThroughUndoableHistory()
        {
            using var workspace = new LevelEditorWorkspace(
                LevelDocumentFactory.CreateEmpty("Environment"));
            var coordinator = CreateCoordinator(workspace);
            LevelEnvironmentAuthoringRequest request = ValidEnvironmentRequest();
            request.ambientIntensity = "1.25";
            request.fogEnabled = false;

            coordinator.ApplyEnvironment(request);

            Assert.That(workspace.CreateSnapshot().environment.atmosphere.ambientIntensity,
                Is.EqualTo(1.25f));
            Assert.That(workspace.CreateSnapshot().environment.atmosphere.fogEnabled, Is.False);
            Assert.That(workspace.CanUndo, Is.True);
            workspace.Undo();
            Assert.That(workspace.CreateSnapshot().environment.atmosphere.ambientIntensity,
                Is.EqualTo(0.76f));
        }

        [Test]
        public void InvalidEnvironmentLeavesWorkspaceUnchangedAndReportsStatus()
        {
            using var workspace = new LevelEditorWorkspace(
                LevelDocumentFactory.CreateEmpty("Environment"));
            var coordinator = CreateCoordinator(workspace);
            string status = string.Empty;
            coordinator.StatusChanged += message => status = message;
            LevelEnvironmentAuthoringRequest request = ValidEnvironmentRequest();
            request.fogStartDistance = "20";
            request.fogEndDistance = "10";

            coordinator.ApplyEnvironment(request);

            Assert.That(workspace.Revision, Is.Zero);
            Assert.That(status, Does.Contain("fog must end after it starts"));
        }

        [Test]
        public void AddEditAndDeletePracticalLightUseCameraFocusAndHistory()
        {
            using var workspace = new LevelEditorWorkspace(
                LevelDocumentFactory.CreateEmpty("Environment"));
            var coordinator = CreateCoordinator(workspace);
            string focusedId = string.Empty;
            coordinator.PracticalLightFocusRequested += id => focusedId = id;

            coordinator.AddPracticalLight();

            LevelPracticalLightData added = workspace.CreateSnapshot()
                .environment.practicalLights[0];
            Assert.That(added.position.x, Is.EqualTo(5f));
            Assert.That(added.position.y, Is.EqualTo(9f));
            Assert.That(added.target.x, Is.EqualTo(8f));
            Assert.That(focusedId, Is.EqualTo(added.id));

            var request = new LevelPracticalLightAuthoringRequest
            {
                id = added.id,
                displayName = "Loading Bay",
                position = Vector("1", "7", "2"),
                target = Vector("1", "0", "2"),
                color = Color("1.2", "0.4", "0.1"),
                intensity = "4",
                range = "16",
                spotAngle = "50",
                innerSpotFraction = "0.6",
                baseHeight = "0",
            };
            coordinator.ApplyPracticalLight(request);
            Assert.That(workspace.CreateSnapshot().environment.practicalLights[0].displayName,
                Is.EqualTo("Loading Bay"));

            coordinator.DeletePracticalLight(added.id);
            Assert.That(workspace.CreateSnapshot().environment.practicalLights, Is.Empty);
            workspace.Undo();
            Assert.That(workspace.CreateSnapshot().environment.practicalLights, Has.Count.EqualTo(1));
        }

        [Test]
        public void AddsPracticalLightAtRequestedMapPosition()
        {
            using var workspace = new LevelEditorWorkspace(
                LevelDocumentFactory.CreateEmpty("Light placement"));
            var coordinator = CreateCoordinator(workspace);

            coordinator.AddPracticalLightAt(new Vector3(2f, 4f, 6f));

            LevelPracticalLightData light = workspace.CreateSnapshot()
                .environment.practicalLights[0];
            Assert.That(light.position.x, Is.EqualTo(2f));
            Assert.That(light.position.y, Is.EqualTo(7f));
            Assert.That(light.target.z, Is.EqualTo(7f));
            Assert.That(light.baseHeight, Is.EqualTo(4f));
        }

        [Test]
        public void MovesPracticalLightBaseAndPreservesAimOffset()
        {
            using var workspace = new LevelEditorWorkspace(
                LevelDocumentFactory.CreateEmpty("Move light"));
            var coordinator = CreateCoordinator(workspace);
            coordinator.AddPracticalLightAt(new Vector3(2f, 4f, 6f));
            LevelPracticalLightData added = workspace.CreateSnapshot()
                .environment.practicalLights[0];

            coordinator.MovePracticalLightAt(added.id, new Vector3(10f, 1f, 12f));

            LevelPracticalLightData moved = workspace.CreateSnapshot()
                .environment.practicalLights[0];
            Assert.That(moved.position.x, Is.EqualTo(10f));
            Assert.That(moved.position.y, Is.EqualTo(4f));
            Assert.That(moved.target.x, Is.EqualTo(10f));
            Assert.That(moved.target.y, Is.EqualTo(1f));
            Assert.That(moved.target.z, Is.EqualTo(13f));
        }

        private static EnvironmentAuthoringCoordinator CreateCoordinator(
            LevelEditorWorkspace workspace)
        {
            return new EnvironmentAuthoringCoordinator(
                workspace,
                () => new LevelEditorCameraState
                {
                    target = new Vector3(8f, 3f, -2f),
                    yaw = 20f,
                    pitch = 55f,
                    distance = 12f,
                });
        }

        private static LevelEnvironmentAuthoringRequest ValidEnvironmentRequest()
        {
            return new LevelEnvironmentAuthoringRequest
            {
                presetId = "test-night",
                ambientSky = Color("0.1", "0.2", "0.3"),
                ambientEquator = Color("0.1", "0.15", "0.2"),
                ambientGround = Color("0.02", "0.03", "0.04"),
                ambientIntensity = "0.8",
                reflectionIntensity = "0.5",
                subtractiveShadow = Color("0.01", "0.02", "0.03"),
                fogEnabled = true,
                fogColor = Color("0.02", "0.05", "0.1"),
                fogStartDistance = "12",
                fogEndDistance = "50",
                keyColor = Color("0.5", "0.7", "1"),
                keyIntensity = "0.9",
                keyBounceIntensity = "0.2",
                keyShadowStrength = "0.85",
                keyShadowBias = "0.05",
                keyShadowNormalBias = "0.3",
                keyRotation = Vector("40", "-25", "0"),
                fixtureHousingColor = Color("0.03", "0.05", "0.1"),
                lensEmissionIntensity = "5",
            };
        }

        private static LevelColorAuthoringText Color(string r, string g, string b) =>
            new LevelColorAuthoringText { r = r, g = g, b = b };

        private static LevelVectorAuthoringText Vector(string x, string y, string z) =>
            new LevelVectorAuthoringText { x = x, y = y, z = z };
    }
}
