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
        public void StoreRoundTripsVersionedEquipmentAndAmmunitionState()
        {
            var store = new PlayerPrefsGameplayPartySaveStore();
            var save = new GameplayPartySave(
                GameplayPartySave.CurrentSchemaVersion,
                new[]
                {
                    new GameplayPartyCharacterSave(
                        "character.mara-vance",
                        equippedItemId: null,
                        weaponMagazines: new[]
                        {
                            new GameplayPartyWeaponMagazineSave(
                                "weapon.rifle",
                                3),
                        },
                        ammunitionReserves: new[]
                        {
                            new GameplayPartyAmmunitionReserveSave(
                                "ammo.rifle",
                                11),
                        }),
                });

            store.Save(save);

            Assert.That(store.TryLoad(out GameplayPartySave restored), Is.True);
            Assert.That(restored.SchemaVersion,
                Is.EqualTo(GameplayPartySave.CurrentSchemaVersion));
            Assert.That(restored.Characters, Has.Count.EqualTo(1));
            GameplayPartyCharacterSave character = restored.Characters[0];
            Assert.That(character.IdentityId,
                Is.EqualTo("character.mara-vance"));
            Assert.That(character.EquippedItemId, Is.Null);
            Assert.That(character.TryGetMagazine(
                "weapon.rifle",
                out GameplayPartyWeaponMagazineSave magazine), Is.True);
            Assert.That(magazine.LoadedRounds, Is.EqualTo(3));
            Assert.That(character.TryGetReserve(
                "ammo.rifle",
                out GameplayPartyAmmunitionReserveSave reserve), Is.True);
            Assert.That(reserve.Rounds, Is.EqualTo(11));
            string serialized = PlayerPrefs.GetString(
                PlayerPrefsGameplayPartySaveStore.StorageKey);
            Assert.That(serialized, Does.Contain("weaponMagazines"));
            Assert.That(serialized, Does.Contain("ammunitionReserves"));
            Assert.That(serialized, Does.Not.Contain("wounds"));
            Assert.That(serialized,
                Does.Not.Contain("currentActionPoints"));
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
        public void LegacyCombatAndProgressionFieldsAreDiscardedOnMigration()
        {
            const string legacy =
                "{\"schemaVersion\":1,\"characters\":[{"
                + "\"identityId\":\"character.mara-vance\","
                + "\"unspentPoints\":4,"
                + "\"bonuses\":[{\"skillId\":\"skill.demolitions\","
                + "\"value\":2}],\"equippedItemId\":\"\","
                + "\"hasCurrentActionPoints\":true,\"currentActionPoints\":2,"
                + "\"wounds\":{\"head\":0,\"torso\":1,"
                + "\"leftArm\":0,\"rightArm\":0,\"leftLeg\":0,"
                + "\"rightLeg\":0,\"unlocalized\":0,"
                + "\"movementPenalty\":1.25}}]}";
            PlayerPrefs.SetString(
                PlayerPrefsGameplayPartySaveStore.StorageKey,
                legacy);
            var store = new PlayerPrefsGameplayPartySaveStore();

            Assert.That(store.TryLoad(out GameplayPartySave restored), Is.True);
            Assert.That(restored.SchemaVersion, Is.EqualTo(1));
            Assert.That(restored.Characters[0].WeaponMagazines, Is.Empty);
            Assert.That(restored.Characters[0].AmmunitionReserves, Is.Empty);
            Assert.That(
                typeof(GameplayPartyCharacterSave).GetProperty("Wounds"),
                Is.Null);
            Assert.That(() => store.Save(restored),
                Throws.InvalidOperationException);
        }

        [Test]
        public void BuggySchemaThreeCombatFieldsAreAlsoDiscarded()
        {
            const string previousSchemaThree =
                "{\"schemaVersion\":3,\"characters\":[{"
                + "\"identityId\":\"character.mara-vance\","
                + "\"equippedItemId\":\"\","
                + "\"hasCurrentActionPoints\":true,"
                + "\"currentActionPoints\":2,"
                + "\"wounds\":{\"torso\":1,"
                + "\"movementPenalty\":1.25}}]}";
            PlayerPrefs.SetString(
                PlayerPrefsGameplayPartySaveStore.StorageKey,
                previousSchemaThree);
            var store = new PlayerPrefsGameplayPartySaveStore();

            Assert.That(store.TryLoad(out GameplayPartySave restored), Is.True);
            Assert.That(restored.SchemaVersion, Is.EqualTo(3));
            Assert.That(restored.Characters[0].EquippedItemId, Is.Null);
            Assert.That(restored.Characters[0].WeaponMagazines, Is.Empty);
            Assert.That(restored.Characters[0].AmmunitionReserves, Is.Empty);
        }

        [Test]
        public void CurrentSchemaRejectsNegativeAmmunition()
        {
            const string malformed =
                "{\"schemaVersion\":4,\"characters\":[{"
                + "\"identityId\":\"character.mara-vance\","
                + "\"equippedItemId\":\"weapon.rifle\","
                + "\"weaponMagazines\":[{\"weaponItemId\":"
                + "\"weapon.rifle\",\"loadedRounds\":-1}],"
                + "\"ammunitionReserves\":[{\"ammoTypeId\":"
                + "\"ammo.rifle\",\"rounds\":11}]}]}";
            PlayerPrefs.SetString(
                PlayerPrefsGameplayPartySaveStore.StorageKey,
                malformed);
            var store = new PlayerPrefsGameplayPartySaveStore();

            Assert.That(() => store.TryLoad(out _),
                Throws.TypeOf<System.ArgumentOutOfRangeException>());
        }

        [Test]
        public void CurrentSchemaRejectsDuplicateAmmunitionEntries()
        {
            const string malformed =
                "{\"schemaVersion\":4,\"characters\":[{"
                + "\"identityId\":\"character.mara-vance\","
                + "\"equippedItemId\":\"weapon.rifle\","
                + "\"weaponMagazines\":["
                + "{\"weaponItemId\":\"weapon.rifle\",\"loadedRounds\":3},"
                + "{\"weaponItemId\":\"weapon.rifle\",\"loadedRounds\":2}],"
                + "\"ammunitionReserves\":[{\"ammoTypeId\":"
                + "\"ammo.rifle\",\"rounds\":11}]}]}";
            PlayerPrefs.SetString(
                PlayerPrefsGameplayPartySaveStore.StorageKey,
                malformed);
            var store = new PlayerPrefsGameplayPartySaveStore();

            Assert.That(() => store.TryLoad(out _),
                Throws.ArgumentException);
        }
    }
}
