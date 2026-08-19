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
                            movementPenalty: 1.25f),
                        currentActionPoints: 2),
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
            Assert.That(character.Wounds.TorsoWounds, Is.EqualTo(1));
            Assert.That(character.Wounds.MovementPenalty,
                Is.EqualTo(1.25f));
            Assert.That(character.CurrentActionPoints, Is.EqualTo(2));
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

        [Test]
        public void LegacyPointFieldsAreIgnoredAndRemovedOnTheNextSave()
        {
            const string legacy =
                "{\"schemaVersion\":1,\"characters\":[{"
                + "\"identityId\":\"character.mara-vance\","
                + "\"unspentPoints\":4,"
                + "\"bonuses\":[{\"skillId\":\"skill.demolitions\","
                + "\"value\":2}],\"equippedItemId\":\"\","
                + "\"wounds\":{\"head\":0,\"torso\":1,"
                + "\"leftArm\":0,\"rightArm\":0,\"leftLeg\":0,"
                + "\"rightLeg\":0,\"unlocalized\":0,"
                + "\"movementPenalty\":1.25}}]}";
            PlayerPrefs.SetString(
                PlayerPrefsGameplayPartySaveStore.StorageKey,
                legacy);
            var store = new PlayerPrefsGameplayPartySaveStore();

            Assert.That(store.TryLoad(out GameplayPartySave restored), Is.True);
            store.Save(restored);
            string normalized = PlayerPrefs.GetString(
                PlayerPrefsGameplayPartySaveStore.StorageKey);

            Assert.That(normalized, Does.Not.Contain("unspentPoints"));
            Assert.That(normalized, Does.Not.Contain("bonuses"));
            Assert.That(restored.Characters[0].Wounds.TorsoWounds, Is.EqualTo(1));
        }
    }
}
