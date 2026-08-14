using System;
using GritGud.Application.Levels;
using UnityEngine;

namespace GritGud.Presentation.Levels.Persistence
{
    public sealed class PlayerPrefsLevelDraftStore : ILevelDraftStore
    {
        public const int MaximumDraftCharacters = 750000;
        private const string KeyPrefix = "grit-gud.level-draft.";

        public bool HasDraft(string slot)
        {
            return PlayerPrefs.HasKey(GetKey(slot));
        }

        public string LoadDraft(string slot)
        {
            string key = GetKey(slot);
            if (!PlayerPrefs.HasKey(key))
            {
                throw new InvalidOperationException($"Draft slot '{slot}' does not exist.");
            }

            return PlayerPrefs.GetString(key);
        }

        public void SaveDraft(string slot, string serializedLevel)
        {
            if (serializedLevel == null)
            {
                throw new ArgumentNullException(nameof(serializedLevel));
            }

            if (serializedLevel.Length > MaximumDraftCharacters)
            {
                throw new InvalidOperationException(
                    $"The draft exceeds the {MaximumDraftCharacters}-character browser-storage safety limit. Export it instead.");
            }

            PlayerPrefs.SetString(GetKey(slot), serializedLevel);
            PlayerPrefs.Save();
        }

        public void DeleteDraft(string slot)
        {
            PlayerPrefs.DeleteKey(GetKey(slot));
            PlayerPrefs.Save();
        }

        private static string GetKey(string slot)
        {
            if (string.IsNullOrWhiteSpace(slot))
            {
                throw new ArgumentException("A draft slot is required.", nameof(slot));
            }

            return KeyPrefix + slot.Trim();
        }
    }
}
