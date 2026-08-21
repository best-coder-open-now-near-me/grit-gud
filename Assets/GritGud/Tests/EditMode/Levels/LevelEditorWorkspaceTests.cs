using System;
using GritGud.Application.Levels;
using GritGud.Domain.Levels;
using NUnit.Framework;

namespace GritGud.Domain.Tests.Levels
{
    public sealed class LevelEditorWorkspaceTests
    {
        [Test]
        public void WorkspaceCanRepresentAnUnsavedInitialDocument()
        {
            using var workspace = new LevelEditorWorkspace(
                LevelDocumentFactory.CreateEmpty(),
                initiallySaved: false);

            Assert.That(workspace.IsDirty, Is.True);

            workspace.MarkSaved();

            Assert.That(workspace.IsDirty, Is.False);
        }

        [Test]
        public void LevelDisplayNameParticipatesInUndoAndRedo()
        {
            LevelDocument document = LevelDocumentFactory.CreateEmpty();
            document.displayName = "Before";
            using var workspace = new LevelEditorWorkspace(document);

            workspace.Execute(new SetLevelDisplayNameCommand("Before", "  Night Shift  "));

            Assert.That(workspace.CreateSnapshot().displayName, Is.EqualTo("Night Shift"));
            Assert.That(workspace.Undo(), Is.True);
            Assert.That(workspace.CreateSnapshot().displayName, Is.EqualTo("Before"));
            Assert.That(workspace.Redo(), Is.True);
            Assert.That(workspace.CreateSnapshot().displayName, Is.EqualTo("Night Shift"));
        }

        [Test]
        public void EnvironmentEditParticipatesInUndoAndRedo()
        {
            LevelDocument document = LevelDocumentFactory.CreateEmpty();
            using var workspace = new LevelEditorWorkspace(document);
            LevelEnvironmentData before = document.environment.DeepCopy();
            LevelEnvironmentData after = before.DeepCopy();
            after.atmosphere.fogEndDistance = 80f;
            after.practicalLights.Add(new LevelPracticalLightData
            {
                id = "light-1",
                displayName = "Gate light",
                position = new Float3Data(0f, 6f, -4f),
                target = new Float3Data(0f, 0f, 0f),
            });

            workspace.Execute(new SetLevelEnvironmentCommand(before, after));

            Assert.That(workspace.CreateSnapshot().environment.atmosphere.fogEndDistance,
                Is.EqualTo(80f));
            Assert.That(workspace.CreateSnapshot().environment.practicalLights,
                Has.Count.EqualTo(1));
            Assert.That(workspace.Undo(), Is.True);
            Assert.That(workspace.CreateSnapshot().environment.atmosphere.fogEndDistance,
                Is.EqualTo(before.atmosphere.fogEndDistance));
            Assert.That(workspace.Redo(), Is.True);
            Assert.That(workspace.CreateSnapshot().environment.practicalLights[0].id,
                Is.EqualTo("light-1"));
        }

        [Test]
        public void LevelBoundsParticipateInUndoAndRedo()
        {
            LevelDocument document = LevelDocumentFactory.CreateEmpty();
            using var workspace = new LevelEditorWorkspace(document);
            LevelBoundsData before = document.bounds;
            var after = new LevelBoundsData(
                new Float3Data(5f, 4f, -3f),
                new Float3Data(80f, 20f, 60f));

            workspace.Execute(new SetLevelBoundsCommand(before, after));

            Assert.That(workspace.CreateSnapshot().bounds.center.x, Is.EqualTo(5f));
            Assert.That(workspace.CreateSnapshot().bounds.size.z, Is.EqualTo(60f));
            workspace.Undo();
            Assert.That(workspace.CreateSnapshot().bounds.size.x, Is.EqualTo(before.size.x));
            workspace.Redo();
            Assert.That(workspace.CreateSnapshot().bounds.center.z, Is.EqualTo(-3f));
        }

        [Test]
        public void EntityGroupCommandsPreserveMembershipAcrossUndo()
        {
            LevelDocument document = LevelDocumentFactory.CreateEmpty();
            document.entities.Add(new LevelEntity
            {
                id = "crate",
                archetypeId = "prop.crate.standard",
            });
            using var workspace = new LevelEditorWorkspace(document);
            var group = new LevelEntityGroupData
            {
                id = "props",
                displayName = "Props",
            };

            workspace.Execute(new AddLevelGroupCommand(group));
            workspace.Execute(new SetEntityGroupCommand("crate", string.Empty, "props"));

            Assert.That(workspace.CreateSnapshot().groups, Has.Count.EqualTo(1));
            Assert.That(workspace.FindEntitySnapshot("crate").groupId, Is.EqualTo("props"));
            workspace.Undo();
            Assert.That(workspace.FindEntitySnapshot("crate").groupId, Is.Empty);
            workspace.Undo();
            Assert.That(workspace.CreateSnapshot().groups, Is.Empty);
        }

        [Test]
        public void WorkspacePublishesHistoryAndValidationTogether()
        {
            using var workspace = new LevelEditorWorkspace(LevelDocumentFactory.CreateEmpty());
            LevelEditorWorkspaceChangedEventArgs observed = null;
            workspace.Changed += (_, args) => observed = args;

            workspace.Execute(new AddEntityCommand(new LevelEntity
            {
                id = "entity-1",
                archetypeId = "prop.crate.standard",
            }));

            Assert.That(observed, Is.Not.Null);
            Assert.That(observed.SessionChange.AffectedEntityIds, Is.EquivalentTo(new[] { "entity-1" }));
            Assert.That(LevelValidator.HasErrors(observed.ValidationIssues), Is.False);
        }

        [Test]
        public void ThrowingWorkspaceObserverCannotInterruptLaterProjections()
        {
            using var workspace = new LevelEditorWorkspace(
                LevelDocumentFactory.CreateEmpty());
            bool laterObserverRan = false;
            EventHandler<LevelEditorWorkspaceChangedEventArgs> failingObserver =
                (_, __) => throw new InvalidOperationException(
                    "workspace projection failed");
            workspace.Changed += failingObserver;
            workspace.Changed += (_, __) => laterObserverRan = true;

            InvalidOperationException exception =
                Assert.Throws<InvalidOperationException>(() =>
                    workspace.Execute(new AddEntityCommand(new LevelEntity
                    {
                        id = "entity-1",
                        archetypeId = "prop.crate.standard",
                    })));

            Assert.That(
                exception.Message,
                Is.EqualTo("workspace projection failed"));
            Assert.That(workspace.FindEntitySnapshot("entity-1"), Is.Not.Null);
            Assert.That(workspace.Revision, Is.EqualTo(1));
            Assert.That(laterObserverRan, Is.True);
            workspace.Changed -= failingObserver;
            Assert.That(workspace.Undo(), Is.True);
            Assert.That(workspace.FindEntitySnapshot("entity-1"), Is.Null);
        }

        [Test]
        public void SelectionSupportsInteractionPointsAndMultipleTargets()
        {
            var selection = new LevelSelectionModel();
            selection.Set(new[]
            {
                new LevelSelectionTarget("entity-1"),
                new LevelSelectionTarget("entity-2", LevelSelectionKind.InteractionPoint, "primary"),
            });

            Assert.That(selection.Targets, Has.Count.EqualTo(2));
            Assert.That(selection.PrimaryEntityId, Is.EqualTo("entity-1"));

            selection.Toggle(new LevelSelectionTarget(
                "entity-2",
                LevelSelectionKind.InteractionPoint,
                "primary"));

            Assert.That(selection.Targets, Has.Count.EqualTo(1));
        }

        [Test]
        public void ScenarioActorCommandsParticipateInUndoAndRedo()
        {
            using var workspace = new LevelEditorWorkspace(LevelDocumentFactory.CreateEmpty());
            var actor = new LevelScenarioActorData
            {
                id = "guard-a",
                templateId = "depot-rifleman",
                transform = new LevelTransformData(new Float3Data(2f, 1f, 4f), 90f),
            };

            workspace.Execute(new AddScenarioActorCommand(actor));
            LevelScenarioActorData before = workspace.CreateSnapshot().scenario.actors[1];
            LevelScenarioActorData after = before.DeepCopy();
            after.transform = new LevelTransformData(new Float3Data(6f, 1f, 8f), 180f);
            workspace.Execute(new SetScenarioActorCommand(actor.id, before, after));

            Assert.That(workspace.CreateSnapshot().scenario.actors[1].transform.position.x,
                Is.EqualTo(6f));
            Assert.That(workspace.Undo(), Is.True);
            Assert.That(workspace.CreateSnapshot().scenario.actors[1].transform.position.x,
                Is.EqualTo(2f));
            Assert.That(workspace.Undo(), Is.True);
            Assert.That(workspace.CreateSnapshot().scenario.actors, Has.Count.EqualTo(1));
            Assert.That(workspace.Redo(), Is.True);
            Assert.That(workspace.CreateSnapshot().scenario.actors, Has.Count.EqualTo(2));

            workspace.Execute(new DeleteScenarioActorCommand(actor.id));
            Assert.That(workspace.CreateSnapshot().scenario.actors, Has.Count.EqualTo(1));
            Assert.That(workspace.Undo(), Is.True);
            Assert.That(workspace.CreateSnapshot().scenario.actors, Has.Count.EqualTo(2));
        }

        [Test]
        public void DeletingEntityRemovesAndUndoRestoresScenarioLinks()
        {
            LevelDocument document = LevelDocumentFactory.CreateEmpty();
            document.entities.Add(new LevelEntity
            {
                id = "linked",
                archetypeId = "prop.crate.standard",
                interactionPoints =
                {
                    new InteractionPointData { id = "objective", type = "objective" },
                },
            });
            document.scenario.objectives.Add(new LevelScenarioObjectiveData
            {
                id = "goal",
                entityId = "linked",
                interactionPointId = "objective",
            });
            document.scenario.props.Add(new LevelScenarioPropData { entityId = "linked" });
            document.scenario.vehicles.Add(new LevelScenarioVehicleData { entityId = "linked" });
            using var workspace = new LevelEditorWorkspace(document);

            workspace.Execute(new DeleteEntityCommand("linked"));

            LevelDocument deleted = workspace.CreateSnapshot();
            Assert.That(deleted.scenario.objectives, Is.Empty);
            Assert.That(deleted.scenario.props, Is.Empty);
            Assert.That(deleted.scenario.vehicles, Is.Empty);
            Assert.That(workspace.Undo(), Is.True);
            LevelDocument restored = workspace.CreateSnapshot();
            Assert.That(restored.scenario.objectives, Has.Count.EqualTo(1));
            Assert.That(restored.scenario.props, Has.Count.EqualTo(1));
            Assert.That(restored.scenario.vehicles, Has.Count.EqualTo(1));
        }

        [Test]
        public void DeletingInteractionRemovesAndUndoRestoresObjectiveLink()
        {
            LevelDocument document = LevelDocumentFactory.CreateEmpty();
            document.entities.Add(new LevelEntity
            {
                id = "terminal",
                archetypeId = "prop.crate.standard",
                interactionPoints =
                {
                    new InteractionPointData { id = "use", type = "objective" },
                },
            });
            document.scenario.objectives.Add(new LevelScenarioObjectiveData
            {
                id = "goal",
                entityId = "terminal",
                interactionPointId = "use",
            });
            using var workspace = new LevelEditorWorkspace(document);

            workspace.Execute(new DeleteInteractionPointCommand("terminal", "use"));

            Assert.That(workspace.CreateSnapshot().scenario.objectives, Is.Empty);
            Assert.That(workspace.Undo(), Is.True);
            Assert.That(workspace.CreateSnapshot().scenario.objectives, Has.Count.EqualTo(1));
        }
    }
}
