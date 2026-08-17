using GritGud.Application.Gameplay;
using GritGud.Presentation.Gameplay;
using NUnit.Framework;

namespace GritGud.Presentation.Tests
{
    public sealed class GameplayHudBindingsTests
    {
        [Test]
        public void WarningSourcesAreDeduplicatedAndUnboundSymmetrically()
        {
            var bindings = new GameplayHudBindings();
            var source = new StubWarningSource();

            bindings.BindWarningHintSource(source);
            bindings.BindWarningHintSource(source);

            Assert.That(bindings.WarningHintSources, Has.Count.EqualTo(1));

            bindings.UnbindWarningHintSource(source);

            Assert.That(bindings.WarningHintSources, Is.Empty);
        }

        [Test]
        public void CommandAndInputBindingsStopDispatchingAfterUnbind()
        {
            var bindings = new GameplayHudBindings();
            int toggles = 0;
            bindings.BindTurnModeToggle(() => toggles++);
            bindings.BindInputSource(new StubInputSource());

            bindings.RequestTurnModeToggle();

            Assert.That(toggles, Is.EqualTo(1));
            Assert.That(
                bindings.GetBindingDisplay(GameplayControl.EndTurn),
                Is.EqualTo("BOUND EndTurn"));

            bindings.UnbindTurnModeToggle();
            bindings.UnbindInputSource();
            bindings.RequestTurnModeToggle();

            Assert.That(toggles, Is.EqualTo(1));
            Assert.That(
                bindings.GetBindingDisplay(GameplayControl.EndTurn),
                Is.Empty);
        }

        [Test]
        public void BugReportModalOwnsOneExplicitExportLifecycle()
        {
            var bindings = new GameplayHudBindings();
            string exported = null;
            bindings.BindBugReportExport(note => exported = note);

            bindings.OpenBugReportNote();
            bindings.BugReportNote = "repro note";
            bindings.SubmitBugReportNote(bindings.BugReportNote);

            Assert.That(bindings.BugReportNoteOpen, Is.False);
            Assert.That(bindings.BugReportNote, Is.Empty);
            Assert.That(exported, Is.EqualTo("repro note"));

            bindings.UnbindBugReportExport();
            bindings.OpenBugReportNote();

            Assert.That(bindings.BugReportNoteOpen, Is.False);
        }

        private sealed class StubWarningSource : IGameplayWarningHintSource
        {
            public GameplayWarningHintModel CurrentWarningHint => null;
        }

        private sealed class StubInputSource : IGameplayInputSource
        {
            public GameplayInputFrame CurrentFrame => default;

            public string GetBindingDisplay(GameplayControl control) =>
                "BOUND " + control;
        }
    }
}
