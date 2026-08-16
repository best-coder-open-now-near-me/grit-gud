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
        public void MissingSelectedPlayerReportsRecoveryGuidanceWithoutThrowing()
        {
            LevelDocument document = LevelDocumentFactory.CreateEmpty();
            document.scenario.actors.Clear();
            using var workspace = new LevelEditorWorkspace(document);
            var coordinator = new ScenarioAuthoringCoordinator(
                workspace,
                ScenarioAuthoringCatalog.LoadDefault(),
                () => new LevelEditorCameraState());
            string status = null;
            coordinator.StatusChanged += message => status = message;

            Assert.DoesNotThrow(() => coordinator.ApplyPlayerStart("0", "0", "0", "0"));

            Assert.That(workspace.CanUndo, Is.False);
            Assert.That(status, Does.Contain("Add or select a player actor"));
        }

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
        public void PropTopplingProfileIsAuthoredInOneUndoableChange()
        {
            using var workspace = new LevelEditorWorkspace(
                LevelDocumentFactory.CreateEmpty());
            var coordinator = new ScenarioAuthoringCoordinator(
                workspace,
                ScenarioAuthoringCatalog.LoadDefault(),
                () => new LevelEditorCameraState());

            coordinator.ApplyProp(
                "crate",
                true,
                "35",
                "medium",
                false,
                true,
                "0",
                "90",
                "0.5");

            LevelScenarioPropData prop = workspace.CreateSnapshot()
                .scenario.props.Single();
            Assert.That(prop.entityId, Is.EqualTo("crate"));
            Assert.That(prop.toppling.enabled, Is.True);
            Assert.That(prop.toppling.rollOffsetDegrees, Is.EqualTo(90f));
            Assert.That(prop.toppling.elevationOffset, Is.EqualTo(0.5f));
            Assert.That(workspace.CanUndo, Is.True);
        }

        [Test]
        public void InvalidTopplingProfileDoesNotEnterHistory()
        {
            using var workspace = new LevelEditorWorkspace(
                LevelDocumentFactory.CreateEmpty());
            var coordinator = new ScenarioAuthoringCoordinator(
                workspace,
                ScenarioAuthoringCatalog.LoadDefault(),
                () => new LevelEditorCameraState());
            int revision = workspace.Revision;
            string status = null;
            coordinator.StatusChanged += message => status = message;

            coordinator.ApplyProp(
                "crate",
                true,
                "35",
                "medium",
                false,
                true,
                "0",
                "0",
                "0.5");

            Assert.That(workspace.Revision, Is.EqualTo(revision));
            Assert.That(status, Does.Contain("non-zero"));
        }

        [Test]
        public void PropPinningRulesAreAuthoredWithToppling()
        {
            using var workspace = new LevelEditorWorkspace(
                LevelDocumentFactory.CreateEmpty());
            var coordinator = new ScenarioAuthoringCoordinator(
                workspace,
                ScenarioAuthoringCatalog.LoadDefault(),
                () => new LevelEditorCameraState());

            coordinator.ApplyProp(
                "crate",
                true,
                "35",
                "medium",
                false,
                true,
                "0",
                "90",
                "0.5",
                pinningEnabled: true,
                maximumPinnedActorMassText: "90",
                minimumPinContactDepthText: "0.05");

            LevelScenarioPropData prop = workspace.CreateSnapshot()
                .scenario.props.Single();
            Assert.That(prop.pinning.enabled, Is.True);
            Assert.That(prop.pinning.maximumActorMass, Is.EqualTo(90f));
            Assert.That(prop.pinning.minimumContactDepth,
                Is.EqualTo(0.05f));
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

        [Test]
        public void ObjectiveAuthoringPreservesTheCompleteActionCost()
        {
            LevelDocument document = LevelDocumentFactory.CreateEmpty();
            document.entities.Add(new LevelEntity
            {
                id = "terminal",
                archetypeId = "prop.crate.standard",
                interactionPoints =
                {
                    new InteractionPointData
                    {
                        id = "activate",
                        type = "objective",
                    },
                },
            });
            using var workspace = new LevelEditorWorkspace(document);
            var coordinator = new ScenarioAuthoringCoordinator(
                workspace,
                ScenarioAuthoringCatalog.LoadDefault(),
                () => new LevelEditorCameraState());

            coordinator.ApplyObjective(
                "terminal",
                "activate",
                true,
                "Activate terminal",
                "ACTIVATE THE TERMINAL",
                "TERMINAL ACTIVE",
                "2",
                "1.5",
                "momentum");

            LevelScenarioObjectiveData objective = workspace.CreateSnapshot()
                .scenario.objectives.Single();
            Assert.That(objective.actionPointCost, Is.EqualTo(2));
            Assert.That(objective.movementOpportunityCost, Is.EqualTo(1.5f));
            Assert.That(objective.mobility, Is.EqualTo("momentum"));
        }
    }
}
