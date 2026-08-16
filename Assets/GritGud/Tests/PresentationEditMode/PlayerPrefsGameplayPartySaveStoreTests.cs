using System.Collections.Generic;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Presentation.Persistence;
using NUnit.Framework;
using UnityEngine;

namespace GritGud.Presentation.Tests
{
    public sealed class PlayerPrefsGameplayPartySaveStoreTests
    {
        [SetUp]
        public void SetUp()
        {
            PlayerPrefs.DeleteKey(PlayerPrefsGameplayPartySaveStore.StorageKey);
            PlayerPrefs.Save();
        }

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteKey(PlayerPrefsGameplayPartySaveStore.StorageKey);
            PlayerPrefs.Save();
        }

        [Test]
        public void StoreRoundTripsVersionedIdentityState()
        {
            var store = new PlayerPrefsGameplayPartySaveStore();
            var save = new GameplayPartySave(
                GameplayPartySave.CurrentSchemaVersion,
                new[]
                {
                    new CharacterPersistenceSnapshot(
                        "character.mara-vance",
                        new CharacterProgressionSnapshot(
                            "character.mara-vance",
                            0,
                            new Dictionary<string, int>
                            {
                                ["skill.demolitions"] = 1,
                            }),
                        equippedItemId: null,
                        wounds: new ActorWoundSnapshot(
                            "mara",
                            headWounds: 0,
                            torsoWounds: 1,
                            leftArmWounds: 0,
                            rightArmWounds: 0,
                            leftLegWounds: 0,
                            rightLegWounds: 0,
                            unlocalizedWounds: 0,
                            movementPenalty: 1.25f)),
                });

            store.Save(save);

            Assert.That(store.TryLoad(out GameplayPartySave restored), Is.True);
            Assert.That(restored.SchemaVersion,
                Is.EqualTo(GameplayPartySave.CurrentSchemaVersion));
            Assert.That(restored.Characters, Has.Count.EqualTo(1));
            CharacterPersistenceSnapshot character = restored.Characters[0];
            Assert.That(character.IdentityId,
                Is.EqualTo("character.mara-vance"));
            Assert.That(character.EquippedItemId, Is.Null);
            Assert.That(character.Progression.Bonuses["skill.demolitions"],
                Is.EqualTo(1));
            Assert.That(character.Wounds.TorsoWounds, Is.EqualTo(1));
            Assert.That(character.Wounds.MovementPenalty,
                Is.EqualTo(1.25f));
        }

        [Test]
        public void DeleteRemovesTheDurablePartySlot()
        {
            var store = new PlayerPrefsGameplayPartySaveStore();
            PlayerPrefs.SetString(
                PlayerPrefsGameplayPartySaveStore.StorageKey,
                "saved");

            store.Delete();

            Assert.That(store.TryLoad(out _), Is.False);
        }
    }
}
