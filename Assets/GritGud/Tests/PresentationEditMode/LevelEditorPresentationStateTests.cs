using GritGud.Application.Levels;
using GritGud.Presentation.LevelEditing.UI;
using NUnit.Framework;

namespace GritGud.Presentation.Tests
{
    public sealed class LevelEditorPresentationStateTests
    {
        [Test]
        public void CreateModeAlsoReturnsToCreateWorkspace()
        {
            var state = new LevelEditorPresentationState();
            state.ShowPage(LevelEditorWorkspacePage.Scenario);

            state.ShowCreateMode(LevelEditorCreateMode.Terrain);

            Assert.That(state.Page, Is.EqualTo(LevelEditorWorkspacePage.Create));
            Assert.That(state.CreateMode, Is.EqualTo(LevelEditorCreateMode.Terrain));
        }

        [Test]
        public void ScenarioFocusReplacesWorldFocus()
        {
            var state = new LevelEditorPresentationState();
            state.FocusWorldSelection(new LevelSelectionTarget("crate"));

            state.FocusScenarioActor("player-one");

            Assert.That(
                state.InspectorTarget.Kind,
                Is.EqualTo(LevelEditorInspectorTargetKind.ScenarioActor));
            Assert.That(state.InspectorTarget.TargetId, Is.EqualTo("player-one"));
            Assert.That(state.InspectorTarget.OwnerId, Is.Empty);
            Assert.That(
                state.InspectorPage,
                Is.EqualTo(LevelEditorInspectorPage.Gameplay));
        }

        [Test]
        public void WorldSelectionReturnsInspectorToContextualSelectionPage()
        {
            var state = new LevelEditorPresentationState();
            state.ShowInspectorPage(LevelEditorInspectorPage.Level);

            state.FocusWorldSelection(new LevelSelectionTarget("crate"));

            Assert.That(
                state.InspectorPage,
                Is.EqualTo(LevelEditorInspectorPage.Selection));
            Assert.That(
                state.InspectorTarget.Kind,
                Is.EqualTo(LevelEditorInspectorTargetKind.Entity));
        }

        [Test]
        public void InspectorPagesCanExposeLevelToolsWithoutChangingFocus()
        {
            var state = new LevelEditorPresentationState();
            state.FocusWorldSelection(new LevelSelectionTarget("crate"));

            state.ShowInspectorPage(LevelEditorInspectorPage.Level);

            Assert.That(
                state.InspectorPage,
                Is.EqualTo(LevelEditorInspectorPage.Level));
            Assert.That(state.InspectorTarget.TargetId, Is.EqualTo("crate"));
        }

        [Test]
        public void InteractionPointFocusKeepsOwningEntity()
        {
            var state = new LevelEditorPresentationState();

            state.FocusWorldSelection(new LevelSelectionTarget(
                "console",
                LevelSelectionKind.InteractionPoint,
                "activate"));

            Assert.That(
                state.InspectorTarget.Kind,
                Is.EqualTo(LevelEditorInspectorTargetKind.InteractionPoint));
            Assert.That(state.InspectorTarget.TargetId, Is.EqualTo("activate"));
            Assert.That(state.InspectorTarget.OwnerId, Is.EqualTo("console"));
        }

        [Test]
        public void RepeatingSameStateDoesNotPublishRedundantChange()
        {
            var state = new LevelEditorPresentationState();
            int changes = 0;
            state.Changed += () => changes++;

            state.ShowPage(LevelEditorWorkspacePage.Create);
            state.ShowCreateMode(LevelEditorCreateMode.Select);
            state.ClearInspectorFocus();

            Assert.That(changes, Is.Zero);
        }

        [Test]
        public void ToolSynchronizationDoesNotChangeWorkspacePage()
        {
            var state = new LevelEditorPresentationState();
            state.ShowPage(LevelEditorWorkspacePage.Outline);

            state.SynchronizeCreateMode(LevelEditorCreateMode.Place);

            Assert.That(state.Page, Is.EqualTo(LevelEditorWorkspacePage.Outline));
            Assert.That(state.CreateMode, Is.EqualTo(LevelEditorCreateMode.Place));
        }
    }
}
