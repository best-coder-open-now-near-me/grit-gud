using System;
using System.Collections.Generic;

namespace GritGud.Domain.Gameplay
{
    public enum CoreAttribute
    {
        Strength,
        Dexterity,
        Grit,
        Charisma,
    }

    public static class CoreAttributeIds
    {
        public const string Strength = "attribute.strength";
        public const string Dexterity = "attribute.dexterity";
        public const string Grit = "attribute.grit";
        public const string Charisma = "attribute.charisma";

        public static string GetId(CoreAttribute attribute)
        {
            switch (attribute)
            {
                case CoreAttribute.Strength:
                    return Strength;
                case CoreAttribute.Dexterity:
                    return Dexterity;
                case CoreAttribute.Grit:
                    return Grit;
                case CoreAttribute.Charisma:
                    return Charisma;
                default:
                    throw new ArgumentOutOfRangeException(nameof(attribute));
            }
        }

        public static bool TryParse(string id, out CoreAttribute attribute)
        {
            switch (id)
            {
                case Strength:
                    attribute = CoreAttribute.Strength;
                    return true;
                case Dexterity:
                    attribute = CoreAttribute.Dexterity;
                    return true;
                case Grit:
                    attribute = CoreAttribute.Grit;
                    return true;
                case Charisma:
                    attribute = CoreAttribute.Charisma;
                    return true;
                default:
                    attribute = default;
                    return false;
            }
        }
    }

    public sealed class CoreAttributeSet
    {
        public const int MinimumRating = 1;
        public const int MaximumRating = 5;
        private readonly IReadOnlyList<CharacterRating> ratings;

        public CoreAttributeSet(
            int strength,
            int dexterity,
            int grit,
            int charisma)
        {
            Strength = RequireRating(strength, nameof(strength));
            Dexterity = RequireRating(dexterity, nameof(dexterity));
            Grit = RequireRating(grit, nameof(grit));
            Charisma = RequireRating(charisma, nameof(charisma));
            ratings = Array.AsReadOnly(new[]
            {
                new CharacterRating(CoreAttributeIds.Strength, Strength),
                new CharacterRating(CoreAttributeIds.Dexterity, Dexterity),
                new CharacterRating(CoreAttributeIds.Grit, Grit),
                new CharacterRating(CoreAttributeIds.Charisma, Charisma),
            });
        }

        public int Strength { get; }

        public int Dexterity { get; }

        public int Grit { get; }

        public int Charisma { get; }

        public IReadOnlyList<CharacterRating> Ratings => ratings;

        public int GetRating(CoreAttribute attribute)
        {
            switch (attribute)
            {
                case CoreAttribute.Strength:
                    return Strength;
                case CoreAttribute.Dexterity:
                    return Dexterity;
                case CoreAttribute.Grit:
                    return Grit;
                case CoreAttribute.Charisma:
                    return Charisma;
                default:
                    throw new ArgumentOutOfRangeException(nameof(attribute));
            }
        }

        public static CoreAttributeSet FromRatings(
            IEnumerable<CharacterRating> source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            var values = new Dictionary<CoreAttribute, int>();
            foreach (CharacterRating rating in source)
            {
                if (rating == null
                    || !CoreAttributeIds.TryParse(
                        rating.Id,
                        out CoreAttribute attribute))
                {
                    throw new ArgumentException(
                        "Character attributes must use the four core attribute IDs.",
                        nameof(source));
                }

                if (!values.TryAdd(attribute, rating.Rating))
                {
                    throw new ArgumentException(
                        $"Core attribute '{rating.Id}' is defined more than once.",
                        nameof(source));
                }
            }

            foreach (CoreAttribute attribute in Enum.GetValues(
                typeof(CoreAttribute)))
            {
                if (!values.ContainsKey(attribute))
                {
                    throw new ArgumentException(
                        $"Core attribute '{CoreAttributeIds.GetId(attribute)}' is required.",
                        nameof(source));
                }
            }

            return new CoreAttributeSet(
                values[CoreAttribute.Strength],
                values[CoreAttribute.Dexterity],
                values[CoreAttribute.Grit],
                values[CoreAttribute.Charisma]);
        }

        private static int RequireRating(int rating, string parameterName)
        {
            if (rating < MinimumRating || rating > MaximumRating)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    $"Core attributes must be rated from {MinimumRating} to {MaximumRating}.");
            }

            return rating;
        }
    }

    public readonly struct CharacterDerivedStatistics
    {
        public const float BaseMovementOpportunity = 4f;

        public CharacterDerivedStatistics(CoreAttributeSet attributes)
        {
            Attributes = attributes ?? throw new ArgumentNullException(
                nameof(attributes));
            Initiative = attributes.Dexterity;
            MovementOpportunity =
                BaseMovementOpportunity + attributes.Dexterity;
        }

        public CoreAttributeSet Attributes { get; }

        public int Initiative { get; }

        public float MovementOpportunity { get; }

        public int StrengthCheckModifier => Attributes.Strength;

        public int ResistanceCheckModifier => Attributes.Grit;

        public int SocialCheckModifier => Attributes.Charisma;
    }
}
