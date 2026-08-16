using System;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;
using GritGud.Presentation.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace GritGud.Presentation.Tests
{
    public sealed class GameplayAdvancementHudTests
    {
        [Test]
        public void AdvancementRequiresConfirmationAndPersistsTheCommit()
        {
            var root = new GameObject("Advancement HUD test");
            GameplayPartyControlSession control = null;
            GameplayPartyPersistenceSession persistence = null;
            try
            {
                GameplaySession gameplay = CreateGameplay();
                control = new GameplayPartyControlSession(gameplay);
                var progression = new GameplayPartyProgressionSession(gameplay);
                var store = new MemoryStore();
                persistence = new GameplayPartyPersistenceSession(store);
                persistence.Bind(gameplay, progression);
                GameplayAdvancementHud hud =
                    root.AddComponent<GameplayAdvancementHud>();
                hud.Bind(progression, control, persistence);

                hud.Open("player");
                hud.SelectOption("advance.fieldcraft");

                Assert.That(hud.IsOpen, Is.True);
                Assert.That(hud.PendingOptionId,
                    Is.EqualTo("advance.fieldcraft"));
                Assert.That(progression.GetSnapshot("player").UnspentPoints,
                    Is.EqualTo(1));

                Assert.That(hud.ConfirmPending(), Is.True);
                Assert.That(hud.PendingOptionId, Is.Null);
                Assert.That(progression.GetSnapshot("player").UnspentPoints,
                    Is.Zero);
                Assert.That(store.SaveCount, Is.EqualTo(1));

                hud.Close();
                Assert.That(hud.IsOpen, Is.False);
            }
            finally
            {
                persistence?.Dispose();
                control?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DrawerPanelClampsToSmallCanvases()
        {
            Rect panel = GameplayAdvancementHud.CalculatePanelRectangle(
                canvasWidth: 640f,
                canvasHeight: 360f,
                optionCount: 12,
                hasConfirmation: true);

            Assert.That(panel.xMin, Is.GreaterThanOrEqualTo(14f));
            Assert.That(panel.yMin, Is.GreaterThanOrEqualTo(14f));
            Assert.That(panel.xMax, Is.LessThanOrEqualTo(626f));
            Assert.That(panel.yMax, Is.LessThanOrEqualTo(346f));
        }

        private static GameplaySession CreateGameplay()
        {
            var profile = new CharacterProfileDefinition(
                "character.player",
                "Player",
                "Scout",
                new[]
                {
                    new CharacterRating(CoreAttributeIds.Strength, 2),
                    new CharacterRating(CoreAttributeIds.Dexterity, 4),
                    new CharacterRating(CoreAttributeIds.Grit, 3),
                    new CharacterRating(CoreAttributeIds.Charisma, 3),
                },
                new[] { new CharacterRating("skill.fieldcraft", 2) },
                Array.Empty<string>(),
                1,
                new[]
                {
                    new CharacterAdvancementOption(
                        "advance.fieldcraft",
                        "skill.fieldcraft",
                        1,
                        1),
                });
            var actor = new ScenarioActorDefinition(
                "player",
                10,
                new GameplayActorPose(
                    new GameplayPosition(0f, 0f, 0f),
                    0f),
                new TurnBudget(4, 8f),
                Array.Empty<InventoryItemDefinition>(),
                initiallyEquippedItemId: null,
                characterProfile: profile);
            return new GameplaySession(new ScenarioDefinition(
                "advancement-hud-test",
                new ScenarioTimingDefinition(1f),
                new[] { actor },
                Array.Empty<ScenarioObjectiveDefinition>(),
                playerParty: new PlayerPartyDefinition(
                    new[] { "player" },
                    "player")));
        }

        private sealed class MemoryStore : IGameplayPartySaveStore
        {
            public int SaveCount { get; private set; }

            public bool TryLoad(out GameplayPartySave save)
            {
                save = null;
                return false;
            }

            public void Save(GameplayPartySave save)
            {
                Assert.That(save, Is.Not.Null);
                SaveCount++;
            }

            public void Delete()
            {
            }
        }
    }
}
