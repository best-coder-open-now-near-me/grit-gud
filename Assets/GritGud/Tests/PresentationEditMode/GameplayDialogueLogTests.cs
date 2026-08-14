using GritGud.Domain.Gameplay;
using GritGud.Presentation.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace GritGud.Presentation.Tests
{
    public sealed class GameplayDialogueLogTests
    {
        [Test]
        public void DialogueTranscriptIncludesChannelsTitlesAndMessages()
        {
            var log = new GameplayDialogueLog();
            log.Append(GameplayDialogueChannel.System, "Impact", "Rocket resolved.");

            string transcript = GameplayDialogueExporter.Format(log);

            StringAssert.Contains("#0001  SYSTEM - IMPACT", transcript);
            StringAssert.Contains("Rocket resolved.", transcript);
        }
        [Test]
        public void LogFiltersFormulaDiagnosticsWithoutLosingTheirDetail()
        {
            var log = new GameplayDialogueLog();

            GameplayDialogueEntry dialogue = log.Append(
                GameplayDialogueChannel.Dialogue,
                "Depot Guard",
                "Keep your head down.");
            log.Append(
                GameplayDialogueChannel.System,
                "Turn Mode",
                "Player turn started.");
            GameplayDialogueEntry diagnostic = log.AppendCombatDiagnostic(
                "Rifle attack against target",
                "Base accuracy 65",
                "Exposure modifier = 4 / 6 samples = -17",
                "Final hit chance = 65 - 17 = 48",
                "Seeded roll 31 <= 48 => HIT");

            Assert.That(dialogue.Sequence, Is.EqualTo(1));
            Assert.That(diagnostic.Sequence, Is.EqualTo(3));
            Assert.That(log.Entries, Has.Count.EqualTo(3));
            Assert.That(
                log.CountVisible(
                    GameplayDialogueChannel.Dialogue
                    | GameplayDialogueChannel.System),
                Is.EqualTo(2));
            Assert.That(
                log.CountVisible(GameplayDialogueChannel.CombatDiagnostics),
                Is.EqualTo(1));
            Assert.That(
                diagnostic.Message,
                Does.Contain("Final hit chance = 65 - 17 = 48"));
            Assert.That(
                diagnostic.Message,
                Does.Contain("Seeded roll 31 <= 48 => HIT"));
        }

        [Test]
        public void DialogueDrawerStartsCollapsedAndOwnsOnlyFilterState()
        {
            var host = new GameObject("Dialogue HUD Test");
            try
            {
                GameplayDialogueDrawer drawer =
                    host.AddComponent<GameplayDialogueDrawer>();
                var log = new GameplayDialogueLog();
                log.Append(
                    GameplayDialogueChannel.Dialogue,
                    "Speaker",
                    "A visible line.");
                log.AppendCombatDiagnostic(
                    "Attack",
                    "Final hit chance = 48");

                drawer.Bind(log);
                drawer.Show();

                Assert.That(drawer.Log, Is.SameAs(log));
                Assert.That(drawer.IsExpanded, Is.False);
                Assert.That(
                    drawer.ActiveFilters,
                    Is.EqualTo(GameplayDialogueChannel.All));
                Assert.That(drawer.VisibleEntryCount, Is.EqualTo(2));

                drawer.Toggle();
                drawer.ToggleFilter(
                    GameplayDialogueChannel.CombatDiagnostics);

                Assert.That(drawer.IsExpanded, Is.True);
                Assert.That(drawer.VisibleEntryCount, Is.EqualTo(1));
                Assert.That(log.Entries, Has.Count.EqualTo(2));

                drawer.Hide();

                Assert.That(drawer.IsExpanded, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void DialogueBodyStatusAndHotbarLayoutShareTheCommandBarMargin()
        {
            Rect commandBar = GameplayHud.CalculateCommandBarRectangle(
                1280f,
                900f);

            Rect dialogueButton = GameplayHud.CalculateDialogueButtonRectangle(
                1280f,
                commandBar);
            Rect commandHints = GameplayHud.CalculateCommandHintsRectangle(
                1280f,
                commandBar);
            Rect bodyStatus = GameplayHud.CalculateBodyStatusRectangle(
                commandBar);
            Rect hotbar = GameplayHud.CalculateHotbarRectangle(
                commandBar,
                commandBar.x + GameplayHud.CommandBarMargin,
                560f);
            Rect laidOutHotbar = GameplayHud.CalculateHotbarLayoutRectangle(
                commandBar);
            Rect displacementSlot = GameplayHud.CalculateHotbarSlotRectangle(
                commandBar,
                4);
            Rect displacementFlyout =
                GameplayHud.CalculateActorAbilityFlyoutRectangle(
                    displacementSlot,
                    3);
            Rect equipmentFlyout = GameplayHud.CalculateEquipmentFlyoutRectangle(
                commandBar);
            Rect warningHint =
                GameplayHud.CalculateWarningHintRectangle(
                    commandBar);
            Rect dialoguePanel = GameplayDialogueDrawer.CalculatePanelRectangle(
                1280f,
                commandBar,
                dialogueButton);

            Assert.That(
                dialogueButton.x - commandBar.xMax,
                Is.EqualTo(GameplayHud.CommandBarMargin));
            Assert.That(
                1280f - dialogueButton.xMax,
                Is.EqualTo(GameplayHud.CommandBarMargin));
            Assert.That(commandBar.x - bodyStatus.xMax,
                Is.EqualTo(GameplayHud.CommandBarMargin));
            Assert.That(bodyStatus.x, Is.EqualTo(GameplayHud.CommandBarMargin));
            Assert.That(commandHints.x, Is.EqualTo(dialogueButton.x));
            Assert.That(commandHints.y, Is.EqualTo(dialogueButton.yMax + 5f));
            Assert.That(
                commandBar.yMax - commandHints.yMax,
                Is.EqualTo(GameplayHud.CommandBarMargin));
            Assert.That(
                dialogueButton.y,
                Is.EqualTo(
                    commandBar.y
                    + 13f
                    - (GameplayHud.CommandBarMargin * 2f)));
            Assert.That(
                commandBar.yMax - hotbar.yMax,
                Is.EqualTo(GameplayHud.CommandBarMargin));
            Assert.That(commandBar.height, Is.EqualTo(118f));
            Assert.That(hotbar.height, Is.EqualTo(86f));
            Assert.That(equipmentFlyout.x,
                Is.EqualTo(GameplayHud.CommandBarMargin));
            Assert.That(equipmentFlyout.y,
                Is.EqualTo(GameplayHud.EquipmentFlyoutTop));
            Assert.That(
                equipmentFlyout.yMax
                    + GameplayHud.WarningHintGap,
                Is.EqualTo(warningHint.y));
            Assert.That(
                warningHint.yMax
                    + GameplayHud.WarningHintGap,
                Is.EqualTo(commandBar.y));
            Assert.That(warningHint.yMax, Is.LessThan(commandBar.y));
            Assert.That(warningHint.x, Is.EqualTo(laidOutHotbar.x));
            Assert.That(warningHint.width, Is.EqualTo(laidOutHotbar.width));
            Assert.That(displacementSlot.y, Is.EqualTo(laidOutHotbar.y));
            Assert.That(displacementSlot.xMin,
                Is.GreaterThanOrEqualTo(laidOutHotbar.xMin));
            Assert.That(displacementSlot.xMax,
                Is.LessThanOrEqualTo(laidOutHotbar.xMax));
            Assert.That(displacementFlyout.yMax,
                Is.LessThan(displacementSlot.yMin));
            Assert.That(displacementFlyout.height, Is.EqualTo(147f));
            Assert.That(
                warningHint.height,
                Is.EqualTo(GameplayHud.WarningHintHeight));
            Assert.That(1280f - dialoguePanel.xMax,
                Is.EqualTo(GameplayHud.CommandBarMargin));
            Assert.That(commandHints.height / 8f, Is.GreaterThan(9f));
            foreach (TargetRegionId region in new[]
            {
                TargetRegionId.Head,
                TargetRegionId.Torso,
                TargetRegionId.LeftArm,
                TargetRegionId.RightArm,
                TargetRegionId.LeftLeg,
                TargetRegionId.RightLeg,
            })
            {
                Rect regionRectangle =
                    GameplayHud.CalculateBodyRegionRectangle(
                        bodyStatus,
                        region);
                Assert.That(regionRectangle.xMin,
                    Is.GreaterThanOrEqualTo(bodyStatus.xMin));
                Assert.That(regionRectangle.xMax,
                    Is.LessThanOrEqualTo(bodyStatus.xMax));
                Assert.That(regionRectangle.yMin,
                    Is.GreaterThanOrEqualTo(bodyStatus.yMin));
                Assert.That(regionRectangle.yMax,
                    Is.LessThanOrEqualTo(bodyStatus.yMax));
            }
        }
    }
}
