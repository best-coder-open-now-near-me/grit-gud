using System;
using System.Collections.Generic;

namespace GritGud.Domain.Characters
{
    public static class CharacterAppearanceSlotIds
    {
        public const string Armor = "armor";
        public const string Hair = "hair";
        public const string FacialHair = "facial-hair";
        public const string Face = "face";
        public const string Headwear = "headwear";
        public const string Neck = "neck";
        public const string Back = "back";
        public const string Waist = "waist";
        public const string Patch = "patch";
    }

    [Serializable]
    public sealed class CharacterAccessorySelectionData
    {
        public string slotId = string.Empty;
        public string accessoryId = string.Empty;

        public void Normalize()
        {
            slotId = slotId?.Trim() ?? string.Empty;
            accessoryId = accessoryId?.Trim() ?? string.Empty;
        }

        public CharacterAccessorySelectionData DeepCopy() =>
            new CharacterAccessorySelectionData
            {
                slotId = slotId ?? string.Empty,
                accessoryId = accessoryId ?? string.Empty,
            };
    }

    [Serializable]
    public sealed class CharacterAppearanceData
    {
        public string bodyId = "body.military-male-01";
        public List<CharacterAccessorySelectionData> accessories =
            new List<CharacterAccessorySelectionData>();

        public void Normalize()
        {
            bodyId = bodyId?.Trim() ?? string.Empty;
            accessories = accessories ?? new List<CharacterAccessorySelectionData>();
            foreach (CharacterAccessorySelectionData selection in accessories)
                selection?.Normalize();
        }

        public string GetAccessory(string slotId)
        {
            if (string.IsNullOrWhiteSpace(slotId) || accessories == null)
                return string.Empty;
            foreach (CharacterAccessorySelectionData selection in accessories)
            {
                if (selection != null
                    && string.Equals(selection.slotId, slotId, StringComparison.Ordinal))
                {
                    return selection.accessoryId ?? string.Empty;
                }
            }

            return string.Empty;
        }

        public void SetAccessory(string slotId, string accessoryId)
        {
            if (string.IsNullOrWhiteSpace(slotId))
                throw new ArgumentException("An appearance slot ID is required.", nameof(slotId));
            accessories = accessories ?? new List<CharacterAccessorySelectionData>();
            accessories.RemoveAll(selection => selection != null
                && string.Equals(selection.slotId, slotId, StringComparison.Ordinal));
            if (!string.IsNullOrWhiteSpace(accessoryId))
            {
                accessories.Add(new CharacterAccessorySelectionData
                {
                    slotId = slotId.Trim(),
                    accessoryId = accessoryId.Trim(),
                });
            }
        }

        public CharacterAppearanceData DeepCopy()
        {
            var copy = new CharacterAppearanceData
            {
                bodyId = bodyId ?? string.Empty,
            };
            if (accessories != null)
            {
                foreach (CharacterAccessorySelectionData selection in accessories)
                    copy.accessories.Add(selection?.DeepCopy());
            }
            return copy;
        }
    }

    [Serializable]
    public sealed class CharacterDocument
    {
        public const int CurrentSchemaVersion = 1;

        public int schemaVersion = CurrentSchemaVersion;
        public string characterId = string.Empty;
        public string displayName = "New Character";
        public CharacterAppearanceData appearance = new CharacterAppearanceData();

        public void Normalize()
        {
            characterId = characterId?.Trim() ?? string.Empty;
            displayName = displayName?.Trim() ?? string.Empty;
            appearance = appearance ?? new CharacterAppearanceData();
            appearance.Normalize();
        }

        public CharacterDocument DeepCopy() => new CharacterDocument
        {
            schemaVersion = schemaVersion,
            characterId = characterId ?? string.Empty,
            displayName = displayName ?? string.Empty,
            appearance = appearance?.DeepCopy() ?? new CharacterAppearanceData(),
        };
    }
}
