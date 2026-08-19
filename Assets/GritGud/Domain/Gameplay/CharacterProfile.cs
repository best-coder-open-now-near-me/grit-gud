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
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Ratings require an id.", nameof(id));
            if (rating < 0)
                throw new ArgumentOutOfRangeException(nameof(rating));
            Id = id;
            Rating = rating;
        }

        public string Id { get; }

        public int Rating { get; }
    }

    public sealed class CharacterProfileDefinition
    {
        public CharacterProfileDefinition(
            string identityId,
            string displayName,
            string archetype,
            IEnumerable<CharacterRating> attributes,
            IEnumerable<CharacterRating> skills,
            IEnumerable<string> talentIds)
        {
            if (string.IsNullOrWhiteSpace(identityId)
                || string.IsNullOrWhiteSpace(displayName)
                || string.IsNullOrWhiteSpace(archetype))
            {
                throw new ArgumentException(
                    "Character identity fields cannot be empty.");
            }
            IdentityId = identityId;
            DisplayName = displayName;
            Archetype = archetype;
            CoreAttributes = CoreAttributeSet.FromRatings(attributes);
            DerivedStatistics = new CharacterDerivedStatistics(CoreAttributes);
            Attributes = CoreAttributes.Ratings;
            Skills = CopyRatings(skills, nameof(skills));
            TalentIds = CopyIds(talentIds, nameof(talentIds));
        }

        public string IdentityId { get; }

        public string DisplayName { get; }

        public string Archetype { get; }

        public IReadOnlyList<CharacterRating> Attributes { get; }

        public CoreAttributeSet CoreAttributes { get; }

        public CharacterDerivedStatistics DerivedStatistics { get; }

        public IReadOnlyList<CharacterRating> Skills { get; }

        public IReadOnlyList<string> TalentIds { get; }

        public CharacterRating GetSkill(string id)
        {
            foreach (CharacterRating skill in Skills)
            {
                if (string.Equals(skill.Id, id, StringComparison.Ordinal))
                    return skill;
            }
            return null;
        }

        private static IReadOnlyList<CharacterRating> CopyRatings(
            IEnumerable<CharacterRating> source,
            string name)
        {
            if (source == null)
                throw new ArgumentNullException(name);
            var result = new List<CharacterRating>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (CharacterRating value in source)
            {
                if (value == null || !ids.Add(value.Id))
                {
                    throw new ArgumentException(
                        "Character rating ids must be unique.",
                        name);
                }
                result.Add(value);
            }
            return result.AsReadOnly();
        }

        private static IReadOnlyList<string> CopyIds(
            IEnumerable<string> source,
            string name)
        {
            if (source == null)
                throw new ArgumentNullException(name);
            var result = new List<string>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (string value in source)
            {
                if (string.IsNullOrWhiteSpace(value) || !ids.Add(value))
                {
                    throw new ArgumentException(
                        "Talent ids must be nonempty and unique.",
                        name);
                }
                result.Add(value);
            }
            return result.AsReadOnly();
        }
    }

    public sealed class CharacterPersistenceSnapshot
    {
        public CharacterPersistenceSnapshot(
            string identityId,
            string equippedItemId,
            ActorWoundSnapshot wounds,
            int? currentActionPoints = null)
        {
            if (string.IsNullOrWhiteSpace(identityId))
            {
                throw new ArgumentException(
                    "Persistent character state requires an identity.",
                    nameof(identityId));
            }
            IdentityId = identityId;
            EquippedItemId = equippedItemId;
            Wounds = wounds;
            if (currentActionPoints < 0)
                throw new ArgumentOutOfRangeException(nameof(currentActionPoints));
            CurrentActionPoints = currentActionPoints;
        }

        public string IdentityId { get; }

        public string EquippedItemId { get; }

        public ActorWoundSnapshot Wounds { get; }

        public int? CurrentActionPoints { get; }
    }
}
