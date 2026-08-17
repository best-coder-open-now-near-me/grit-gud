using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;

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
    public sealed class CharacterRatingData
    {
        public string id = string.Empty;
        public int rating;

        public CharacterRatingData DeepCopy() => new CharacterRatingData
        {
            id = id ?? string.Empty,
            rating = rating,
        };
    }

    [Serializable]
    public sealed class CharacterBuildData
    {
        public string archetype = "field-operative";
        public List<CharacterRatingData> attributes = CreateDefaultAttributes();
        public List<CharacterRatingData> skills = new List<CharacterRatingData>
        {
            new CharacterRatingData { id = CharacterSkillIds.CloseQuarters, rating = 1 },
        };
        public List<string> talentIds = new List<string>();

        public static List<CharacterRatingData> CreateDefaultAttributes() =>
            new List<CharacterRatingData>
            {
                new CharacterRatingData { id = CoreAttributeIds.Strength, rating = 3 },
                new CharacterRatingData { id = CoreAttributeIds.Dexterity, rating = 3 },
                new CharacterRatingData { id = CoreAttributeIds.Grit, rating = 3 },
                new CharacterRatingData { id = CoreAttributeIds.Charisma, rating = 3 },
            };

        public void Normalize()
        {
            archetype = archetype?.Trim() ?? string.Empty;
            attributes = attributes ?? CreateDefaultAttributes();
            skills = skills ?? new List<CharacterRatingData>();
            talentIds = talentIds ?? new List<string>();
            for (int index = 0; index < talentIds.Count; index++)
                talentIds[index] = talentIds[index]?.Trim() ?? string.Empty;
        }

        public int GetRating(IReadOnlyList<CharacterRatingData> ratings, string id)
        {
            foreach (CharacterRatingData value in ratings
                ?? Array.Empty<CharacterRatingData>())
            {
                if (value != null && string.Equals(value.id, id, StringComparison.Ordinal))
                    return value.rating;
            }
            return 0;
        }

        public void SetRating(List<CharacterRatingData> ratings, string id, int rating)
        {
            if (ratings == null)
                throw new ArgumentNullException(nameof(ratings));
            CharacterRatingData existing = ratings.Find(
                value => value != null && string.Equals(value.id, id, StringComparison.Ordinal));
            if (existing != null)
                existing.rating = rating;
            else
                ratings.Add(new CharacterRatingData { id = id, rating = rating });
        }

        public CharacterBuildData DeepCopy()
        {
            var copy = new CharacterBuildData
            {
                archetype = archetype ?? string.Empty,
                attributes = new List<CharacterRatingData>(),
                skills = new List<CharacterRatingData>(),
                talentIds = talentIds == null ? new List<string>() : new List<string>(talentIds),
            };
            foreach (CharacterRatingData value in attributes ?? new List<CharacterRatingData>())
                copy.attributes.Add(value?.DeepCopy());
            foreach (CharacterRatingData value in skills ?? new List<CharacterRatingData>())
                copy.skills.Add(value?.DeepCopy());
            return copy;
        }
    }

    [Serializable]
    public sealed class CharacterLoadoutItemData
    {
        public string itemId = string.Empty;
        public int quantity = 1;
        public int hotbarSlot;

        public CharacterLoadoutItemData DeepCopy() => new CharacterLoadoutItemData
        {
            itemId = itemId ?? string.Empty,
            quantity = quantity,
            hotbarSlot = hotbarSlot,
        };
    }

    [Serializable]
    public sealed class CharacterLoadoutData
    {
        public string initiallyEquippedItemId = string.Empty;
        public List<CharacterLoadoutItemData> items = new List<CharacterLoadoutItemData>();

        public void Normalize()
        {
            initiallyEquippedItemId = initiallyEquippedItemId?.Trim() ?? string.Empty;
            items = items ?? new List<CharacterLoadoutItemData>();
            foreach (CharacterLoadoutItemData item in items)
            {
                if (item != null)
                    item.itemId = item.itemId?.Trim() ?? string.Empty;
            }
        }

        public CharacterLoadoutData DeepCopy()
        {
            var copy = new CharacterLoadoutData
            {
                initiallyEquippedItemId = initiallyEquippedItemId ?? string.Empty,
            };
            foreach (CharacterLoadoutItemData item in items
                ?? new List<CharacterLoadoutItemData>())
                copy.items.Add(item?.DeepCopy());
            return copy;
        }
    }

    [Serializable]
    public sealed class CharacterDocument
    {
        public const int CurrentSchemaVersion = 2;

        public int schemaVersion = CurrentSchemaVersion;
        public string characterId = string.Empty;
        public string displayName = "New Character";
        public CharacterAppearanceData appearance = new CharacterAppearanceData();
        public CharacterBuildData build = new CharacterBuildData();
        public CharacterLoadoutData startingLoadout = new CharacterLoadoutData();

        public void Normalize()
        {
            characterId = characterId?.Trim() ?? string.Empty;
            displayName = displayName?.Trim() ?? string.Empty;
            appearance = appearance ?? new CharacterAppearanceData();
            appearance.Normalize();
            build = build ?? new CharacterBuildData();
            build.Normalize();
            startingLoadout = startingLoadout ?? new CharacterLoadoutData();
            startingLoadout.Normalize();
        }

        public CharacterDocument DeepCopy() => new CharacterDocument
        {
            schemaVersion = schemaVersion,
            characterId = characterId ?? string.Empty,
            displayName = displayName ?? string.Empty,
            appearance = appearance?.DeepCopy() ?? new CharacterAppearanceData(),
            build = build?.DeepCopy() ?? new CharacterBuildData(),
            startingLoadout = startingLoadout?.DeepCopy() ?? new CharacterLoadoutData(),
        };
    }
}
