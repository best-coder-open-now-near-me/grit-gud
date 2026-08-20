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

                characters.Add(new GameplayPartyCharacterSave(
                    character.identityId,
                    NormalizeOptionalId(character.equippedItemId)));
            }

            save = new GameplayPartySave(
                GameplayPartySave.CurrentSchemaVersion,
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
