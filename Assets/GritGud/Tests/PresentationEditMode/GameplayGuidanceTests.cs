using System;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;
using GritGud.Presentation.Gameplay;
using NUnit.Framework;

namespace GritGud.Presentation.Tests
{
    public sealed class GameplayGuidanceTests
    {
        [Test]
        public void DefaultCatalogContainsEveryContextualGuidanceEntry()
        {
            GameplayGuidanceCatalog catalog =
                GameplayGuidanceCatalog.LoadDefault();

            Assert.That(catalog.Count, Is.EqualTo(7));
            Assert.That(
                catalog.Require(GameplayGuidanceIds.VoluntaryEntry)
                    .ExpectedBehavior,
                Does.Contain("full AP"));
            Assert.That(
                catalog.Require(GameplayGuidanceIds.VoluntaryActive)
                    .ExpectedBehavior,
                Does.Contain("replenishes AP"));
            Assert.That(
                catalog.Require(GameplayGuidanceIds.RoutePlanning).PlayerTip,
                Does.Contain("Enter"));
            Assert.That(
                catalog.Require(GameplayGuidanceIds.RoutePlayback).Rationale,
                Does.Contain("frame rate"));
            Assert.That(
                catalog.Require(GameplayGuidanceIds.EncounterActive)
                    .ExpectedBehavior,
                Does.Contain("cannot refresh"));
            Assert.That(
                catalog.Require(GameplayGuidanceIds.InteractReady).PlayerTip,
                Does.Contain("Press E"));
            Assert.That(
                catalog.Require(GameplayGuidanceIds.ObjectiveCompleted)
                    .Rationale,
                Does.Contain("resolved action record"));
        }

        [Test]
        public void SelectorDistinguishesVoluntaryTurnsFromEncounters()
        {
            GameplaySession session = CreateSession();

            Assert.That(
                GameplayGuidanceSelector.Select(session, null),
                Is.EqualTo(GameplayGuidanceIds.VoluntaryEntry));

            session.EnterTurnMode();
            Assert.That(
                GameplayGuidanceSelector.Select(session, null),
                Is.EqualTo(GameplayGuidanceIds.VoluntaryActive));

            session.BeginEncounter();
            Assert.That(
                GameplayGuidanceSelector.Select(session, null),
                Is.EqualTo(GameplayGuidanceIds.EncounterActive));
        }

        [Test]
        public void CatalogRejectsDuplicateStableIds()
        {
            const string duplicateJson =
                "{\"entries\":[" +
                "{\"id\":\"same\",\"title\":\"One\"," +
                "\"expectedBehavior\":\"Expected\"," +
                "\"rationale\":\"Because\",\"playerTip\":\"Tip\"}," +
                "{\"id\":\"same\",\"title\":\"Two\"," +
                "\"expectedBehavior\":\"Expected\"," +
                "\"rationale\":\"Because\",\"playerTip\":\"Tip\"}]}";

            Assert.Throws<ArgumentException>(() =>
                GameplayGuidanceCatalog.FromJson(duplicateJson));
        }

        [Test]
        public void GeneralTipsAreReusableByFlyoutTutorialAndLoadingScreens()
        {
            GameplayTipCatalog catalog = GameplayTipCatalog.LoadDefault();

            Assert.That(catalog.Entries, Has.Count.EqualTo(6));
            Assert.That(catalog.Entries[0].TutorialOrder, Is.EqualTo(10));
            Assert.That(catalog.Entries[1].Text, Does.Contain("Right-click"));
            Assert.That(catalog.GetLoadingScreenTip(0), Is.Not.Null);
            Assert.That(catalog.GetLoadingScreenTip(5).Id,
                Is.EqualTo(catalog.GetLoadingScreenTip(0).Id));
        }

        [Test]
        public void GeneralTipsRejectDuplicateIds()
        {
            const string duplicateJson =
                "{\"entries\":[" +
                "{\"id\":\"same\",\"category\":\"A\",\"title\":\"Later\"," +
                "\"text\":\"Text\",\"tutorialOrder\":20}," +
                "{\"id\":\"same\",\"category\":\"B\",\"title\":\"Sooner\"," +
                "\"text\":\"Text\",\"tutorialOrder\":10}]}";

            Assert.Throws<ArgumentException>(() =>
                GameplayTipCatalog.FromJson(duplicateJson));
        }

        [Test]
        public void GeneralTipsSortForTutorialDelivery()
        {
            const string json =
                "{\"entries\":[" +
                "{\"id\":\"later\",\"category\":\"A\",\"title\":\"Later\"," +
                "\"text\":\"Text\",\"tutorialOrder\":20}," +
                "{\"id\":\"sooner\",\"category\":\"B\",\"title\":\"Sooner\"," +
                "\"text\":\"Text\",\"tutorialOrder\":10}]}";

            GameplayTipCatalog catalog = GameplayTipCatalog.FromJson(json);
            Assert.That(catalog.Entries[0].Id, Is.EqualTo("sooner"));
        }

        [Test]
        public void SelectorPrioritizesActionAndObjectiveState()
        {
            GameplaySession interaction = CreateSessionWithNearbyObjective();
            Assert.That(
                GameplayGuidanceSelector.Select(interaction, null),
                Is.EqualTo(GameplayGuidanceIds.InteractReady));

            var resolver = new GameplayActionResolver(interaction);
            Assert.That(resolver.TryResolveInteraction(
                "player",
                "objective",
                out _,
                out _), Is.True);
            Assert.That(
                GameplayGuidanceSelector.Select(interaction, null),
                Is.EqualTo(GameplayGuidanceIds.ObjectiveCompleted));
        }

        private static GameplaySession CreateSession()
        {
            var actor = new ScenarioActorDefinition(
                "player",
                initiative: 10,
                new GameplayActorPose(
                    new GameplayPosition(0f, 0f, 0f),
                    0f),
                new TurnBudget(4, 8f));
            var scenario = new ScenarioDefinition(
                "guidance-test",
                new ScenarioTimingDefinition(1.25f),
                new[] { actor },
                Array.Empty<ScenarioObjectiveDefinition>());
            return new GameplaySession(scenario);
        }

        private static GameplaySession CreateSessionWithNearbyObjective()
        {
            var actor = new ScenarioActorDefinition(
                "player",
                initiative: 10,
                new GameplayActorPose(
                    new GameplayPosition(0f, 0f, 0f),
                    0f),
                new TurnBudget(4, 8f));
            var objective = new ScenarioObjectiveDefinition(
                "objective",
                new GameplayPosition(1f, 0f, 0f),
                interactionRadius: 1.5f,
                new GameplayInteractionDefinition(
                    "objective.use",
                    "Use objective",
                    new ActionCost(1, 1f, ActionMobility.Set)));
            return new GameplaySession(new ScenarioDefinition(
                "guidance-action-test",
                new ScenarioTimingDefinition(1.25f),
                new[] { actor },
                new[] { objective }));
        }
    }
}
