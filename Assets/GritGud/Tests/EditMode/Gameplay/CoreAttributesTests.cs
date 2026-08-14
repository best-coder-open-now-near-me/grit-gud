using System;
using GritGud.Domain.Gameplay;
using NUnit.Framework;

namespace GritGud.Domain.Tests.Gameplay
{
    public sealed class CoreAttributesTests
    {
        [Test]
        public void DerivedStatisticsUseDexterityWithoutCreatingHitPoints()
        {
            var attributes = new CoreAttributeSet(
                strength: 3,
                dexterity: 4,
                grit: 5,
                charisma: 2);

            var derived = new CharacterDerivedStatistics(attributes);

            Assert.That(derived.Initiative, Is.EqualTo(4));
            Assert.That(derived.MovementOpportunity, Is.EqualTo(8f));
            Assert.That(derived.StrengthCheckModifier, Is.EqualTo(3));
            Assert.That(derived.ResistanceCheckModifier, Is.EqualTo(5));
            Assert.That(derived.SocialCheckModifier, Is.EqualTo(2));
        }

        [TestCase(0)]
        [TestCase(6)]
        public void CoreRatingsMustStayWithinOneToFive(int rating)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new CoreAttributeSet(rating, 3, 3, 3));
        }

        [Test]
        public void CharacterProfilesRequireEveryCoreAttributeExactlyOnce()
        {
            Assert.Throws<ArgumentException>(() =>
                CoreAttributeSet.FromRatings(new[]
                {
                    new CharacterRating(CoreAttributeIds.Strength, 3),
                    new CharacterRating(CoreAttributeIds.Dexterity, 3),
                    new CharacterRating(CoreAttributeIds.Grit, 3),
                }));

            Assert.Throws<ArgumentException>(() =>
                CoreAttributeSet.FromRatings(new[]
                {
                    new CharacterRating(CoreAttributeIds.Strength, 3),
                    new CharacterRating(CoreAttributeIds.Dexterity, 3),
                    new CharacterRating(CoreAttributeIds.Grit, 3),
                    new CharacterRating(CoreAttributeIds.Charisma, 3),
                    new CharacterRating("attribute.insight", 3),
                }));
        }
    }
}
