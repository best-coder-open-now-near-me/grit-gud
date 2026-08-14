using GritGud.Application.Levels;
using GritGud.Domain.Levels;
using NUnit.Framework;

namespace GritGud.Domain.Tests.Levels
{
    public sealed class LevelEditorWorkspaceTests
    {
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
        public void SelectionSupportsFutureSubElementsAndMultipleTargets()
        {
            var selection = new LevelSelectionModel();
            selection.Set(new[]
            {
                new LevelSelectionTarget("entity-1"),
                new LevelSelectionTarget("entity-2", LevelSelectionKind.CoverVolume, "primary"),
            });

            Assert.That(selection.Targets, Has.Count.EqualTo(2));
            Assert.That(selection.PrimaryEntityId, Is.EqualTo("entity-1"));

            selection.Toggle(new LevelSelectionTarget(
                "entity-2",
                LevelSelectionKind.CoverVolume,
                "primary"));

            Assert.That(selection.Targets, Has.Count.EqualTo(1));
        }
    }
}
