using System;
using System.Collections.Generic;

namespace GritGud.Domain.Gameplay
{
    public static class CharacterSkillIds
    {
        public const string CloseQuarters = "skill.close-quarters";
    }

    public sealed class CharacterRating
    {
        public CharacterRating(string id, int rating)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Ratings require an id.", nameof(id));
            if (rating < 0) throw new ArgumentOutOfRangeException(nameof(rating));
            Id = id;
            Rating = rating;
        }
        public string Id { get; }
        public int Rating { get; }
    }

    public sealed class CharacterAdvancementOption
    {
        public CharacterAdvancementOption(string id, string skillId, int pointCost, int maximumBonus)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(skillId))
                throw new ArgumentException("Advancement options require stable identifiers.");
            if (pointCost <= 0 || maximumBonus <= 0) throw new ArgumentOutOfRangeException(nameof(pointCost));
            Id = id;
            SkillId = skillId;
            PointCost = pointCost;
            MaximumBonus = maximumBonus;
        }
        public string Id { get; }
        public string SkillId { get; }
        public int PointCost { get; }
        public int MaximumBonus { get; }
    }

    public sealed class CharacterProfileDefinition
    {
        public CharacterProfileDefinition(
            string identityId,
            string displayName,
            string archetype,
            IEnumerable<CharacterRating> attributes,
            IEnumerable<CharacterRating> skills,
            IEnumerable<string> talentIds,
            int startingProgressionPoints,
            IEnumerable<CharacterAdvancementOption> advancementOptions)
        {
            if (string.IsNullOrWhiteSpace(identityId) || string.IsNullOrWhiteSpace(displayName)
                || string.IsNullOrWhiteSpace(archetype))
                throw new ArgumentException("Character identity fields cannot be empty.");
            if (startingProgressionPoints < 0) throw new ArgumentOutOfRangeException(nameof(startingProgressionPoints));
            IdentityId = identityId;
            DisplayName = displayName;
            Archetype = archetype;
            CoreAttributes = CoreAttributeSet.FromRatings(attributes);
            DerivedStatistics = new CharacterDerivedStatistics(CoreAttributes);
            Attributes = CoreAttributes.Ratings;
            Skills = CopyRatings(skills, nameof(skills));
            TalentIds = CopyIds(talentIds, nameof(talentIds));
            StartingProgressionPoints = startingProgressionPoints;
            AdvancementOptions = CopyOptions(advancementOptions);
            foreach (CharacterAdvancementOption option in AdvancementOptions)
                if (GetSkill(option.SkillId) == null)
                    throw new ArgumentException($"Advancement '{option.Id}' targets unknown skill '{option.SkillId}'.", nameof(advancementOptions));
        }

        public string IdentityId { get; }
        public string DisplayName { get; }
        public string Archetype { get; }
        public IReadOnlyList<CharacterRating> Attributes { get; }
        public CoreAttributeSet CoreAttributes { get; }
        public CharacterDerivedStatistics DerivedStatistics { get; }
        public IReadOnlyList<CharacterRating> Skills { get; }
        public IReadOnlyList<string> TalentIds { get; }
        public int StartingProgressionPoints { get; }
        public IReadOnlyList<CharacterAdvancementOption> AdvancementOptions { get; }

        public CharacterRating GetSkill(string id)
        {
            foreach (CharacterRating skill in Skills)
                if (string.Equals(skill.Id, id, StringComparison.Ordinal)) return skill;
            return null;
        }

        public CharacterAdvancementOption GetAdvancement(string id)
        {
            foreach (CharacterAdvancementOption option in AdvancementOptions)
                if (string.Equals(option.Id, id, StringComparison.Ordinal)) return option;
            return null;
        }

        private static IReadOnlyList<CharacterRating> CopyRatings(IEnumerable<CharacterRating> source, string name)
        {
            if (source == null) throw new ArgumentNullException(name);
            var result = new List<CharacterRating>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (CharacterRating value in source)
            {
                if (value == null || !ids.Add(value.Id)) throw new ArgumentException("Character rating ids must be unique.", name);
                result.Add(value);
            }
            return result.AsReadOnly();
        }

        private static IReadOnlyList<string> CopyIds(IEnumerable<string> source, string name)
        {
            if (source == null) throw new ArgumentNullException(name);
            var result = new List<string>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (string value in source)
            {
                if (string.IsNullOrWhiteSpace(value) || !ids.Add(value)) throw new ArgumentException("Talent ids must be nonempty and unique.", name);
                result.Add(value);
            }
            return result.AsReadOnly();
        }

        private static IReadOnlyList<CharacterAdvancementOption> CopyOptions(IEnumerable<CharacterAdvancementOption> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var result = new List<CharacterAdvancementOption>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (CharacterAdvancementOption value in source)
            {
                if (value == null || !ids.Add(value.Id)) throw new ArgumentException("Advancement ids must be unique.", nameof(source));
                result.Add(value);
            }
            return result.AsReadOnly();
        }
    }

    public sealed class CharacterProgressionSnapshot
    {
        public CharacterProgressionSnapshot(string identityId, int unspentPoints, IReadOnlyDictionary<string, int> bonuses)
        {
            if (string.IsNullOrWhiteSpace(identityId)) throw new ArgumentException("Progression requires an identity.", nameof(identityId));
            if (unspentPoints < 0) throw new ArgumentOutOfRangeException(nameof(unspentPoints));
            IdentityId = identityId;
            UnspentPoints = unspentPoints;
            if (bonuses == null) throw new ArgumentNullException(nameof(bonuses));
            var copy = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, int> bonus in bonuses)
            {
                if (string.IsNullOrWhiteSpace(bonus.Key) || bonus.Value < 0)
                    throw new ArgumentException("Progression bonuses must be nonnegative and identified.", nameof(bonuses));
                copy.Add(bonus.Key, bonus.Value);
            }
            Bonuses = copy;
        }
        public string IdentityId { get; }
        public int UnspentPoints { get; }
        public IReadOnlyDictionary<string, int> Bonuses { get; }
    }

    public sealed class CharacterPersistenceSnapshot
    {
        public CharacterPersistenceSnapshot(string identityId, CharacterProgressionSnapshot progression, string equippedItemId, ActorWoundSnapshot wounds)
        {
            IdentityId = identityId ?? throw new ArgumentNullException(nameof(identityId));
            Progression = progression ?? throw new ArgumentNullException(nameof(progression));
            if (!string.Equals(identityId, progression.IdentityId, StringComparison.Ordinal))
                throw new ArgumentException("Persistence identity must match progression identity.", nameof(progression));
            EquippedItemId = equippedItemId;
            Wounds = wounds;
        }
        public string IdentityId { get; }
        public CharacterProgressionSnapshot Progression { get; }
        public string EquippedItemId { get; }
        public ActorWoundSnapshot Wounds { get; }
    }
}
