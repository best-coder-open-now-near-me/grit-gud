using System;
using System.Collections.Generic;
using GritGud.Application.Gameplay;
using UnityEngine;

namespace GritGud.Presentation.Persistence
{
    public sealed class PlayerPrefsGameplayPartySaveStore :
        IGameplayPartySaveStore
    {
        [Serializable]
        private sealed class SaveDocument
        {
            public int schemaVersion;
            public List<CharacterDocument> characters =
                new List<CharacterDocument>();
        }

        [Serializable]
        private sealed class CharacterDocument
        {
            public string identityId;
            public string equippedItemId;
            public List<MagazineDocument> weaponMagazines =
                new List<MagazineDocument>();
            public List<ReserveDocument> ammunitionReserves =
                new List<ReserveDocument>();
        }

        [Serializable]
        private sealed class MagazineDocument
        {
            public string weaponItemId;
            public int loadedRounds;
        }

        [Serializable]
        private sealed class ReserveDocument
        {
            public string ammoTypeId;
            public int rounds;
        }

        public const string StorageKey = "grit-gud.party-save";
        public const int MaximumSaveCharacters = 65536;

        public bool TryLoad(out GameplayPartySave save)
        {
            if (!PlayerPrefs.HasKey(StorageKey))
            {
                save = null;
                return false;
            }

            string serialized = PlayerPrefs.GetString(StorageKey);
            if (string.IsNullOrWhiteSpace(serialized)
                || serialized.Length > MaximumSaveCharacters)
            {
                throw new InvalidOperationException(
                    "The local party save is empty or exceeds its storage limit.");
            }

            SaveDocument document;
            try
            {
                document = JsonUtility.FromJson<SaveDocument>(serialized);
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    "The local party save is not valid JSON.",
                    exception);
            }
            if (document == null || document.characters == null)
                throw new InvalidOperationException(
                    "The local party save has no character collection.");
            if (document.schemaVersion < 1
                || document.schemaVersion > GameplayPartySave.CurrentSchemaVersion)
                throw new InvalidOperationException(
                    $"Party save schema {document.schemaVersion} is unsupported.");

            var characters = new List<GameplayPartyCharacterSave>(
                document.characters.Count);
            foreach (CharacterDocument character in document.characters)
            {
                if (character == null)
                    throw new InvalidOperationException(
                        "The local party save contains an empty character.");

                var magazines = new List<GameplayPartyWeaponMagazineSave>();
                var reserves = new List<GameplayPartyAmmunitionReserveSave>();
                if (document.schemaVersion >= 4)
                {
                    if (character.weaponMagazines == null
                        || character.ammunitionReserves == null)
                        throw new InvalidOperationException(
                            "The local party save has incomplete ammunition state.");
                    foreach (MagazineDocument magazine in
                        character.weaponMagazines)
                    {
                        if (magazine == null)
                            throw new InvalidOperationException(
                                "The local party save contains an empty magazine.");
                        magazines.Add(new GameplayPartyWeaponMagazineSave(
                            magazine.weaponItemId,
                            magazine.loadedRounds));
                    }
                    foreach (ReserveDocument reserve in
                        character.ammunitionReserves)
                    {
                        if (reserve == null)
                            throw new InvalidOperationException(
                                "The local party save contains an empty reserve.");
                        reserves.Add(new GameplayPartyAmmunitionReserveSave(
                            reserve.ammoTypeId,
                            reserve.rounds));
                    }
                }
                characters.Add(new GameplayPartyCharacterSave(
                    character.identityId,
                    NormalizeOptionalId(character.equippedItemId),
                    magazines,
                    reserves));
            }

            save = new GameplayPartySave(
                document.schemaVersion,
                characters);
            return true;
        }

        public void Save(GameplayPartySave save)
        {
            if (save == null)
                throw new ArgumentNullException(nameof(save));
            if (save.SchemaVersion != GameplayPartySave.CurrentSchemaVersion)
            {
                throw new InvalidOperationException(
                    $"Party save schema {save.SchemaVersion} cannot be written.");
            }

            var document = new SaveDocument
            {
                schemaVersion = save.SchemaVersion,
            };
            foreach (GameplayPartyCharacterSave character in
                save.Characters)
            {
                var serializedCharacter = new CharacterDocument
                {
                    identityId = character.IdentityId,
                    equippedItemId = character.EquippedItemId ?? string.Empty,
                };
                foreach (GameplayPartyWeaponMagazineSave magazine in
                    character.WeaponMagazines)
                    serializedCharacter.weaponMagazines.Add(
                        new MagazineDocument
                        {
                            weaponItemId = magazine.WeaponItemId,
                            loadedRounds = magazine.LoadedRounds,
                        });
                foreach (GameplayPartyAmmunitionReserveSave reserve in
                    character.AmmunitionReserves)
                    serializedCharacter.ammunitionReserves.Add(
                        new ReserveDocument
                        {
                            ammoTypeId = reserve.AmmoTypeId,
                            rounds = reserve.Rounds,
                        });
                document.characters.Add(serializedCharacter);
            }

            string serialized = JsonUtility.ToJson(document);
            if (serialized.Length > MaximumSaveCharacters)
            {
                throw new InvalidOperationException(
                    $"The party save exceeds the {MaximumSaveCharacters}-character "
                    + "browser-storage safety limit.");
            }
            PlayerPrefs.SetString(StorageKey, serialized);
            PlayerPrefs.Save();
        }

        public void Delete()
        {
            PlayerPrefs.DeleteKey(StorageKey);
            PlayerPrefs.Save();
        }

        private static string NormalizeOptionalId(string value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
