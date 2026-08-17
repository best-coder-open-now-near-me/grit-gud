using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;

namespace GritGud.Domain.Characters
{
    public sealed class CharacterAppearanceValidationContent
    {
        private readonly HashSet<string> bodyIds;
        private readonly Dictionary<string, string> accessorySlots;
        private readonly Dictionary<string, string> accessoryCompatibility;
        private readonly Dictionary<string, string> bodyCompatibility;

        public CharacterAppearanceValidationContent(
            IEnumerable<KeyValuePair<string, string>> bodies,
            IEnumerable<CharacterAccessoryValidationEntry> accessories)
        {
            bodyIds = new HashSet<string>(StringComparer.Ordinal);
            bodyCompatibility = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> body in bodies
                ?? Array.Empty<KeyValuePair<string, string>>())
            {
                if (string.IsNullOrWhiteSpace(body.Key))
                    continue;
                bodyIds.Add(body.Key);
                bodyCompatibility[body.Key] = body.Value ?? string.Empty;
            }

            accessorySlots = new Dictionary<string, string>(StringComparer.Ordinal);
            accessoryCompatibility = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (CharacterAccessoryValidationEntry accessory in accessories
                ?? Array.Empty<CharacterAccessoryValidationEntry>())
            {
                if (accessory == null || string.IsNullOrWhiteSpace(accessory.AccessoryId))
                    continue;
                accessorySlots[accessory.AccessoryId] = accessory.SlotId;
                accessoryCompatibility[accessory.AccessoryId] = accessory.CompatibilityTag;
            }
        }

        public bool ContainsBody(string bodyId) => bodyIds.Contains(bodyId ?? string.Empty);

        public bool TryGetAccessorySlot(string accessoryId, out string slotId) =>
            accessorySlots.TryGetValue(accessoryId ?? string.Empty, out slotId);

        public bool IsCompatible(string bodyId, string accessoryId)
        {
            if (!bodyCompatibility.TryGetValue(bodyId ?? string.Empty, out string bodyTag)
                || !accessoryCompatibility.TryGetValue(
                    accessoryId ?? string.Empty,
                    out string accessoryTag))
            {
                return false;
            }

            return string.IsNullOrEmpty(accessoryTag)
                || string.Equals(bodyTag, accessoryTag, StringComparison.Ordinal);
        }
    }

    public sealed class CharacterAccessoryValidationEntry
    {
        public CharacterAccessoryValidationEntry(
            string accessoryId,
            string slotId,
            string compatibilityTag)
        {
            AccessoryId = accessoryId ?? string.Empty;
            SlotId = slotId ?? string.Empty;
            CompatibilityTag = compatibilityTag ?? string.Empty;
        }

        public string AccessoryId { get; }
        public string SlotId { get; }
        public string CompatibilityTag { get; }
    }

    public static class CharacterValidator
    {
        public static IReadOnlyList<string> Validate(
            CharacterDocument document,
            CharacterAppearanceValidationContent content)
        {
            var issues = new List<string>();
            if (document == null)
            {
                issues.Add("A character document is required.");
                return issues;
            }
            if (document.schemaVersion != CharacterDocument.CurrentSchemaVersion)
                issues.Add($"Character schema {document.schemaVersion} is not supported.");
            if (string.IsNullOrWhiteSpace(document.characterId))
                issues.Add("A character needs a stable ID.");
            if (string.IsNullOrWhiteSpace(document.displayName))
                issues.Add("A character needs a display name.");
            if (document.appearance == null || string.IsNullOrWhiteSpace(document.appearance.bodyId))
            {
                issues.Add("A character needs a body selection.");
                return issues;
            }
            if (content == null || !content.ContainsBody(document.appearance.bodyId))
                issues.Add($"Body '{document.appearance.bodyId}' is unavailable.");

            var occupiedSlots = new HashSet<string>(StringComparer.Ordinal);
            foreach (CharacterAccessorySelectionData selection in
                document.appearance.accessories ?? new List<CharacterAccessorySelectionData>())
            {
                if (selection == null)
                {
                    issues.Add("The appearance contains an empty accessory selection.");
                    continue;
                }
                if (string.IsNullOrWhiteSpace(selection.slotId)
                    || !occupiedSlots.Add(selection.slotId))
                {
                    issues.Add($"Appearance slot '{selection.slotId}' is empty or duplicated.");
                    continue;
                }
                if (content == null
                    || !content.TryGetAccessorySlot(selection.accessoryId, out string actualSlot))
                {
                    issues.Add($"Accessory '{selection.accessoryId}' is unavailable.");
                }
                else if (!string.Equals(actualSlot, selection.slotId, StringComparison.Ordinal))
                {
                    issues.Add($"Accessory '{selection.accessoryId}' does not belong in slot '{selection.slotId}'.");
                }
                else if (!content.IsCompatible(document.appearance.bodyId, selection.accessoryId))
                {
                    issues.Add($"Accessory '{selection.accessoryId}' is incompatible with body '{document.appearance.bodyId}'.");
                }
            }
            ValidateBuild(document.build, issues);
            ValidateLoadout(document.startingLoadout, issues);
            return issues;
        }

        private static void ValidateBuild(
            CharacterBuildData build,
            ICollection<string> issues)
        {
            if (build == null)
            {
                issues.Add("A character needs an authored build.");
                return;
            }
            if (string.IsNullOrWhiteSpace(build.archetype))
                issues.Add("A character needs an archetype.");

            var required = new HashSet<string>(StringComparer.Ordinal)
            {
                CoreAttributeIds.Strength,
                CoreAttributeIds.Dexterity,
                CoreAttributeIds.Grit,
                CoreAttributeIds.Charisma,
            };
            var attributeIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (CharacterRatingData attribute in build.attributes
                ?? new List<CharacterRatingData>())
            {
                if (attribute == null || string.IsNullOrWhiteSpace(attribute.id)
                    || !attributeIds.Add(attribute.id))
                {
                    issues.Add("Character attributes contain an empty or duplicate entry.");
                    continue;
                }
                if (!required.Contains(attribute.id))
                    issues.Add($"Attribute '{attribute.id}' is not a core attribute.");
                if (attribute.rating < CoreAttributeSet.MinimumRating
                    || attribute.rating > CoreAttributeSet.MaximumRating)
                {
                    issues.Add($"Attribute '{attribute.id}' must be rated from 1 to 5.");
                }
            }
            foreach (string attributeId in required)
            {
                if (!attributeIds.Contains(attributeId))
                    issues.Add($"Character build is missing '{attributeId}'.");
            }

            var skillIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (CharacterRatingData skill in build.skills
                ?? new List<CharacterRatingData>())
            {
                if (skill == null || string.IsNullOrWhiteSpace(skill.id)
                    || !skillIds.Add(skill.id))
                {
                    issues.Add("Character skills contain an empty or duplicate entry.");
                    continue;
                }
                if (skill.rating < 0 || skill.rating > 5)
                    issues.Add($"Skill '{skill.id}' must be rated from 0 to 5.");
            }

            var talentIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (string talentId in build.talentIds ?? new List<string>())
            {
                if (string.IsNullOrWhiteSpace(talentId) || !talentIds.Add(talentId))
                    issues.Add("Character talents contain an empty or duplicate ID.");
            }
        }

        private static void ValidateLoadout(
            CharacterLoadoutData loadout,
            ICollection<string> issues)
        {
            if (loadout == null)
            {
                issues.Add("A character needs a starting loadout section.");
                return;
            }
            var itemIds = new HashSet<string>(StringComparer.Ordinal);
            var hotbarSlots = new HashSet<int>();
            foreach (CharacterLoadoutItemData item in loadout.items
                ?? new List<CharacterLoadoutItemData>())
            {
                if (item == null || string.IsNullOrWhiteSpace(item.itemId)
                    || !itemIds.Add(item.itemId))
                {
                    issues.Add("Starting loadout contains an empty or duplicate item.");
                    continue;
                }
                if (item.quantity < 0)
                    issues.Add($"Starting item '{item.itemId}' cannot have a negative quantity.");
                if (item.hotbarSlot < 0 || item.hotbarSlot > GameplayHotbarRules.SlotCount)
                    issues.Add($"Starting item '{item.itemId}' has an invalid hotbar slot.");
                else if (item.hotbarSlot > 0 && !hotbarSlots.Add(item.hotbarSlot))
                    issues.Add($"Starting loadout duplicates hotbar slot {item.hotbarSlot}.");
            }
            if (!string.IsNullOrWhiteSpace(loadout.initiallyEquippedItemId)
                && !itemIds.Contains(loadout.initiallyEquippedItemId))
            {
                issues.Add("The initially equipped item is not in the starting loadout.");
            }
        }
    }
}
