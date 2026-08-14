using System.Linq;
using GritGud.Application.Levels;
using GritGud.Domain.Levels;
using GritGud.Presentation.LevelEditing;
using GritGud.Presentation.LevelEditing.Core;
using NUnit.Framework;
using UnityEngine;

namespace GritGud.Presentation.Tests
{
    public sealed class ScenarioAuthoringCoordinatorTests
    {
        [Test]
        public void AddedActorUsesCameraFocusAndRequestsInspectorFocus()
        {
            using var workspace = new LevelEditorWorkspace(LevelDocumentFactory.CreateEmpty());
            ScenarioAuthoringCatalog catalog = ScenarioAuthoringCatalog.LoadDefault();
            ScenarioActorTemplateDefinition opponent = catalog.ActorTemplates
                .First(template => !template.PlayerTemplate);
            var coordinator = new ScenarioAuthoringCoordinator(
                workspace,
                catalog,
                () => new LevelEditorCameraState
                {
                    target = new Vector3(4f, 2f, -3f),
                    yaw = 450f,
                });
            string focusedActorId = null;
            coordinator.ActorFocusRequested += actorId => focusedActorId = actorId;

            coordinator.AddActor(opponent.TemplateId);

            LevelScenarioActorData added = workspace.CreateSnapshot().scenario.actors
                .Single(actor => actor.id == focusedActorId);
            Assert.That(added.transform.position.x, Is.EqualTo(4f));
            Assert.That(added.transform.position.y, Is.EqualTo(2f));
            Assert.That(added.transform.position.z, Is.EqualTo(-3f));
            Assert.That(added.transform.yawDegrees, Is.EqualTo(90f));
            Assert.That(added.primaryTarget, Is.True);
        }

        [Test]
        public void InvalidActorTransformDoesNotEnterHistory()
        {
            using var workspace = new LevelEditorWorkspace(LevelDocumentFactory.CreateEmpty());
            var coordinator = new ScenarioAuthoringCoordinator(
                workspace,
                ScenarioAuthoringCatalog.LoadDefault(),
                () => new LevelEditorCameraState());
            int historyPosition = workspace.Revision;
            string status = null;
            coordinator.StatusChanged += message => status = message;
            LevelScenarioActorData player = workspace.CreateSnapshot().scenario.actors[0];

            coordinator.ApplyActor(
                player.id,
                "not-a-number",
                "0",
                "0",
                "0",
                true,
                true,
                false);

            Assert.That(workspace.Revision, Is.EqualTo(historyPosition));
            Assert.That(status, Does.Contain("finite numbers"));
        }

        [Test]
        public void DeletingActorClearsVehicleOccupantInSameUndoStep()
        {
            LevelDocument document = LevelDocumentFactory.CreateEmpty();
            var opponent = new LevelScenarioActorData
            {
                id = "enemy-driver",
                templateId = "depot-rifleman",
                primaryTarget = true,
            };
            document.scenario.actors.Add(opponent);
            document.scenario.vehicles.Add(new LevelScenarioVehicleData
            {
                entityId = "truck",
                startingOccupantActorId = opponent.id,
            });
            using var workspace = new LevelEditorWorkspace(document);
            var coordinator = new ScenarioAuthoringCoordinator(
                workspace,
                ScenarioAuthoringCatalog.LoadDefault(),
                () => new LevelEditorCameraState());

            coordinator.DeleteActor(opponent.id);

            LevelDocument deleted = workspace.CreateSnapshot();
            Assert.That(deleted.scenario.actors.Any(actor => actor.id == opponent.id), Is.False);
            Assert.That(deleted.scenario.vehicles[0].startingOccupantActorId, Is.Empty);
            Assert.That(workspace.Undo(), Is.True);
            LevelDocument restored = workspace.CreateSnapshot();
            Assert.That(restored.scenario.actors.Any(actor => actor.id == opponent.id), Is.True);
            Assert.That(restored.scenario.vehicles[0].startingOccupantActorId,
                Is.EqualTo(opponent.id));
        }
    }
}
