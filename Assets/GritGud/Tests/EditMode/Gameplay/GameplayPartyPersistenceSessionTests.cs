using System;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;
using NUnit.Framework;

namespace GritGud.Tests.EditMode.Gameplay
{
    public sealed class GameplayPartyPersistenceSessionTests
    {
        [Test]
        public void PartySaveCapturesIdentityAndEquipmentWithoutTransientCombatState()
        {
            GameplaySession gameplay = CreateGameplay();

            GameplayPartySave save = GameplayPartySave.Capture(gameplay);

            Assert.That(save.Characters, Has.Count.EqualTo(2));
            Assert.That(save.TryGetCharacter(
                "character.mara",
                out GameplayPartyCharacterSave mara), Is.True);
            Assert.That(mara.EquippedItemId, Is.EqualTo("weapon.rifle"));
            Assert.That(
                typeof(GameplayPartyCharacterSave).GetProperty("Wounds"),
                Is.Null);
            Assert.That(
                typeof(GameplayPartyCharacterSave).GetProperty(
                    "CurrentActionPoints"),
                Is.Null);
        }

        [Test]
        public void ValidPartySaveRestoresEquipmentOntoFreshCombatState()
        {
            GameplaySession authored = CreateGameplay();
            var save = new GameplayPartySave(
                GameplayPartySave.CurrentSchemaVersion,
                new[]
                {
                    new GameplayPartyCharacterSave(
                        "character.mara",
                        equippedItemId: null),
                    new GameplayPartyCharacterSave(
                        "character.vale",
                        "weapon.rifle"),
                });

            var restored = new GameplaySession(
                authored.Scenario,
                restoredParty: save);

            GameplayActorSnapshot mara = restored.GetActor("mara");
            Assert.That(mara.EquippedItemId, Is.Null);
            Assert.That(mara.Wounds.ActorId, Is.EqualTo("mara"));
            Assert.That(mara.Wounds.WoundCount, Is.Zero);
            Assert.That(mara.TurnBudget.MovementOpportunity,
                Is.EqualTo(8f));
            Assert.That(mara.TurnBudget.ActionPoints, Is.EqualTo(4));
            Assert.That(restored.GetActor("vale").EquippedItemId,
                Is.EqualTo("weapon.rifle"));
        }

        [Test]
        public void EquipmentChangesPersistImmediatelyWithoutAProgressionSession()
        {
            GameplaySession gameplay = CreateGameplay();
            var store = new MemoryPartySaveStore();
            using var persistence = new GameplayPartyPersistenceSession(store);
            persistence.Bind(gameplay);
            var equipment = new GameplayEquipmentSession(gameplay);

            Assert.That(equipment.TryResolve(
                "mara",
                "weapon.rifle",
                equip: false,
                out _,
                out EquipmentChangeFailure failure), Is.True);

            Assert.That(failure, Is.EqualTo(EquipmentChangeFailure.None));
            Assert.That(store.SaveCount, Is.EqualTo(1));
            Assert.That(store.Saved.TryGetCharacter(
                "character.mara",
                out GameplayPartyCharacterSave mara), Is.True);
            Assert.That(mara.EquippedItemId, Is.Null);
            Assert.That(persistence.Status,
                Is.EqualTo("Saved party equipment."));
        }

        [Test]
        public void CombatDamageDoesNotWriteTransientPartyState()
        {
            GameplaySession gameplay = CreateGameplay();
            var store = new MemoryPartySaveStore();
            using var persistence = new GameplayPartyPersistenceSession(store);
            persistence.Bind(gameplay);

            gameplay.ApplyBlastInjury(
                "mara",
                TargetRegionId.Torso,
                woundMovementPenalty: 1f);

            Assert.That(store.SaveCount, Is.Zero);
        }

        [Test]
        public void ThrowingStatusObserverCannotReclassifySuccessfulLoadOrSave()
        {
            GameplaySession gameplay = CreateGameplay();
            var store = new MemoryPartySaveStore
            {
                Saved = GameplayPartySave.Capture(gameplay),
            };
            var persistence = new GameplayPartyPersistenceSession(store);
            persistence.StatusChanged += _ =>
                throw new InvalidOperationException("observer failed");

            GameplayPartySave loaded = persistence.Load(gameplay.Scenario);
            persistence.Bind(gameplay);
            var equipment = new GameplayEquipmentSession(gameplay);

            Assert.That(loaded, Is.SameAs(store.Saved));
            Assert.That(equipment.TryResolve(
                "mara",
                "weapon.rifle",
                equip: false,
                out _,
                out _), Is.True);
            Assert.That(store.SaveCount, Is.EqualTo(1));
            Assert.DoesNotThrow(persistence.Dispose);
            Assert.Throws<ObjectDisposedException>(() => persistence.Flush());
        }

        private sealed class MemoryPartySaveStore : IGameplayPartySaveStore
        {
            public GameplayPartySave Saved { get; set; }

            public int SaveCount { get; private set; }

            public bool TryLoad(out GameplayPartySave save)
            {
                save = Saved;
                return save != null;
            }

            public void Save(GameplayPartySave save)
            {
                Saved = save;
                SaveCount++;
            }

            public void Delete()
            {
                Saved = null;
            }
        }

        private static GameplaySession CreateGameplay()
        {
            CharacterProfileDefinition CreateProfile(string id, string name) =>
                new CharacterProfileDefinition(
                    id,
                    name,
                    "Test Operative",
                    new[]
                    {
                        new CharacterRating(CoreAttributeIds.Strength, 3),
                        new CharacterRating(CoreAttributeIds.Dexterity, 3),
                        new CharacterRating(CoreAttributeIds.Grit, 3),
                        new CharacterRating(CoreAttributeIds.Charisma, 3),
                    },
                    new[] { new CharacterRating("skill.fieldcraft", 2) },
                    Array.Empty<string>());
            var rifle = new InventoryItemDefinition(
                "weapon.rifle",
                "Rifle",
                1,
                InventoryItemKind.Weapon,
                new ActionCost(0, 0f, ActionMobility.Mobile),
                EquipmentEffectSet.None,
                new AttackDefinition(
                    "attack.rifle",
                    "Fire rifle",
                    new ActionCost(1, 0f, ActionMobility.Mobile),
                    1f,
                    accuracyDecay: AccuracyDecayDefinition.None));
            var mara = new ScenarioActorDefinition(
                "mara",
                10,
                new GameplayActorPose(new GameplayPosition(0f, 0f, 0f), 0f),
                new TurnBudget(4, 8f),
                new[] { rifle },
                "weapon.rifle",
                CreateProfile("character.mara", "Mara"));
            var vale = new ScenarioActorDefinition(
                "vale",
                9,
                new GameplayActorPose(new GameplayPosition(2f, 0f, 0f), 0f),
                new TurnBudget(4, 8f),
                new[] { rifle },
                "weapon.rifle",
                CreateProfile("character.vale", "Vale"));
            var party = new PlayerPartyDefinition(
                new[] { "mara", "vale" },
                "mara");
            return new GameplaySession(new ScenarioDefinition(
                "party-persistence-test",
                new ScenarioTimingDefinition(1f),
                new[] { mara, vale },
                Array.Empty<ScenarioObjectiveDefinition>(),
                playerParty: party));
        }
    }
}
