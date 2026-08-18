using System;
using System.Collections.Generic;
using System.Reflection;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;
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
            var projector = new GameplayHudModelProjector(bindings);

            bindings.RequestTurnModeToggle();

            Assert.That(toggles, Is.EqualTo(1));
            Assert.That(
                bindings.GetBindingDisplay(GameplayControl.EndTurn),
                Is.EqualTo("BOUND EndTurn"));
            Assert.That(
                projector.FormatCommandHint(
                    new GameplayCommandHintModel(
                        GameplayControl.EndTurn,
                        "END TURN")),
                Is.EqualTo("BOUND EndTurn  END TURN"));

            bindings.UnbindTurnModeToggle();
            bindings.UnbindInputSource();
            bindings.RequestTurnModeToggle();

            Assert.That(toggles, Is.EqualTo(1));
            Assert.That(
                bindings.GetBindingDisplay(GameplayControl.EndTurn),
                Is.Empty);
            Assert.That(
                projector.FormatCommandHint(
                    new GameplayCommandHintModel(
                        GameplayControl.EndTurn,
                        "END TURN")),
                Is.EqualTo("END TURN"));
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

        [Test]
        public void ProjectorReusesModelUntilAnAuthoritativeInputChanges()
        {
            GameplaySession session = CreateSession(out var assembly);
            var bindings = new GameplayHudBindings();
            bindings.BindSession(session, "player", assembly);
            var projector = new GameplayHudModelProjector(bindings);

            GameplayHudModel first = projector.Build();
            GameplayHudModel repeated = projector.Build();

            Assert.That(repeated, Is.SameAs(first));

            session.UpdateExplorationPose(
                "player",
                new GameplayActorPose(
                    new GameplayPosition(2f, 0f, 1f),
                    90f));
            GameplayHudModel moved = projector.Build();

            Assert.That(moved, Is.Not.SameAs(first));
            Assert.That(projector.Build(), Is.SameAs(moved));
        }

        private static GameplaySession CreateSession(
            out GameplayScenarioAssembly assembly)
        {
            var actor = new ScenarioActorDefinition(
                "player",
                10,
                new GameplayActorPose(
                    new GameplayPosition(0f, 0f, 0f),
                    0f),
                new TurnBudget(4, 8f));
            var scenario = new ScenarioDefinition(
                "hud-cache-test",
                new ScenarioTimingDefinition(1f),
                new[] { actor },
                new ScenarioObjectiveDefinition[0]);
            assembly = (GameplayScenarioAssembly)Activator.CreateInstance(
                typeof(GameplayScenarioAssembly),
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                args: new object[]
                {
                    "HUD cache test",
                    null,
                    null,
                    0u,
                    scenario,
                    new Dictionary<string, ScenarioActorRuntimeDefinition>(),
                    new Dictionary<
                        string,
                        ScenarioObjectiveRuntimeDefinition>(),
                    new Dictionary<string, ScenarioVehicleRuntimeDefinition>(),
                    new Dictionary<string, DisplacementSubjectDefinition>(),
                },
                culture: null);
            return new GameplaySession(scenario);
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
