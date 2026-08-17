using System;
using System.Collections.Generic;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
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
            public WoundDocument wounds = new WoundDocument();
        }

        [Serializable]
        private sealed class WoundDocument
        {
            public int head;
            public int torso;
            public int leftArm;
            public int rightArm;
            public int leftLeg;
            public int rightLeg;
            public int unlocalized;
            public float movementPenalty;
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

            var characters = new List<CharacterPersistenceSnapshot>(
                document.characters.Count);
            foreach (CharacterDocument character in document.characters)
            {
                if (character == null)
                    throw new InvalidOperationException(
                        "The local party save contains an empty character.");

                WoundDocument wounds = character.wounds
                    ?? throw new InvalidOperationException(
                        "The local party save contains no wound state.");
                var woundSnapshot = new ActorWoundSnapshot(
                    character.identityId,
                    wounds.head,
                    wounds.torso,
                    wounds.leftArm,
                    wounds.rightArm,
                    wounds.leftLeg,
                    wounds.rightLeg,
                    wounds.unlocalized,
                    wounds.movementPenalty);
                characters.Add(new CharacterPersistenceSnapshot(
                    character.identityId,
                    NormalizeOptionalId(character.equippedItemId),
                    woundSnapshot));
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

            var document = new SaveDocument
            {
                schemaVersion = save.SchemaVersion,
            };
            foreach (CharacterPersistenceSnapshot character in
                save.Characters)
            {
                var serializedCharacter = new CharacterDocument
                {
                    identityId = character.IdentityId,
                    equippedItemId = character.EquippedItemId ?? string.Empty,
                    wounds = new WoundDocument
                    {
                        head = character.Wounds.HeadWounds,
                        torso = character.Wounds.TorsoWounds,
                        leftArm = character.Wounds.LeftArmWounds,
                        rightArm = character.Wounds.RightArmWounds,
                        leftLeg = character.Wounds.LeftLegWounds,
                        rightLeg = character.Wounds.RightLegWounds,
                        unlocalized = character.Wounds.UnlocalizedWounds,
                        movementPenalty = character.Wounds.MovementPenalty,
                    },
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
