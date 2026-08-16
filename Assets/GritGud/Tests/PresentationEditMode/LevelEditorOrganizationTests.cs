using System.Linq;
using GritGud.Application.Levels;
using GritGud.Domain.Levels;
using GritGud.Presentation.LevelEditing;
using GritGud.Presentation.Levels.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace GritGud.Presentation.Tests
{
    public sealed class LevelEditorOrganizationTests
    {
        [Test]
        public void GroupLifecycleAndMembershipUseReversibleCommands()
        {
            LevelDocument document = LevelDocumentFactory.CreateEmpty();
            document.entities.Add(Entity("crate", "prop.crate.standard"));
            using var workspace = new LevelEditorWorkspace(document);
            var selection = new LevelSelectionModel();
            selection.SetSingle("crate");
            var model = new LevelEditorOrganizationModel(LevelArchetypeCatalog.LoadDefault());
            model.Synchronize(workspace.CreateSnapshot());
            workspace.Changed += (_, _) => model.Synchronize(workspace.CreateSnapshot());
            var coordinator = new LevelEditorOrganizationCoordinator(
                workspace,
                selection,
                model);
            string groupId = string.Empty;
            coordinator.GroupFocusRequested += id => groupId = id;

            coordinator.CreateGroup("Gameplay Props");
            coordinator.AssignSelection(groupId);
            coordinator.SetGroupLocked(groupId, true);
            coordinator.SetGroupHidden(groupId, true);

            LevelDocument organized = workspace.CreateSnapshot();
            Assert.That(organized.groups.Single().displayName, Is.EqualTo("Gameplay Props"));
            Assert.That(organized.groups.Single().locked, Is.True);
            Assert.That(organized.groups.Single().hidden, Is.True);
            Assert.That(organized.entities.Single().groupId, Is.EqualTo(groupId));
            Assert.That(model.CanSelect("crate"), Is.False);

            coordinator.DeleteGroup(groupId);
            Assert.That(workspace.CreateSnapshot().groups, Is.Empty);
            Assert.That(workspace.FindEntitySnapshot("crate").groupId, Is.Empty);
            workspace.Undo();
            Assert.That(workspace.CreateSnapshot().groups.Single().hidden, Is.True);
            Assert.That(workspace.FindEntitySnapshot("crate").groupId, Is.EqualTo(groupId));
        }

        [Test]
        public void FiltersAndIsolationControlSelectionWithoutChangingDocument()
        {
            LevelDocument document = LevelDocumentFactory.CreateEmpty();
            document.groups.Add(new LevelEntityGroupData
            {
                id = "props",
                displayName = "Props",
            });
            LevelEntity crate = Entity("crate", "prop.crate.standard");
            crate.groupId = "props";
            document.entities.Add(crate);
            document.entities.Add(Entity("floor", "structure.floor.standard"));
            LevelArchetypeCatalog catalog = LevelArchetypeCatalog.LoadDefault();
            var model = new LevelEditorOrganizationModel(catalog);
            model.Synchronize(document);

            Assert.That(catalog.TryGet(
                "prop.crate.standard",
                out LevelArchetypeDefinition crateArchetype), Is.True);
            model.SetCategoryFilter(crateArchetype.Category);
            model.SetGroupFilter("props");
            model.SetIsolation("props");

            Assert.That(model.CanSelect("crate"), Is.True);
            Assert.That(model.CanSelect("floor"), Is.False);
            Assert.That(model.IsVisible(document.entities.Single(entity => entity.id == "floor")),
                Is.False);
            Assert.That(document.groups.Single().hidden, Is.False);
        }

        [Test]
        public void HiddenGroupsDisableOnlyTheirProjectedEntities()
        {
            LevelDocument document = LevelDocumentFactory.CreateEmpty();
            document.groups.Add(new LevelEntityGroupData
            {
                id = "hidden",
                displayName = "Hidden",
                hidden = true,
            });
            LevelEntity hidden = Entity("hidden-crate", "prop.crate.standard");
            hidden.groupId = "hidden";
            document.entities.Add(hidden);
            document.entities.Add(Entity("visible-crate", "prop.crate.standard"));
            var root = new GameObject("Organization Projection");
            try
            {
                using var projector = new LevelWorldProjector(
                    LevelArchetypeCatalog.LoadDefault(),
                    root.transform);
                projector.Replace(document);
                var model = new LevelEditorOrganizationModel(
                    LevelArchetypeCatalog.LoadDefault());
                model.Synchronize(document);

                model.ApplyProjection(projector);

                Assert.That(projector.TryGetEntity("hidden-crate", out LevelEntityView hiddenView),
                    Is.True);
                Assert.That(hiddenView.gameObject.activeSelf, Is.False);
                Assert.That(projector.TryGetEntity("visible-crate", out LevelEntityView visibleView),
                    Is.True);
                Assert.That(visibleView.gameObject.activeSelf, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static LevelEntity Entity(string id, string archetypeId) => new LevelEntity
        {
            id = id,
            archetypeId = archetypeId,
        };
    }
}
