using GritGud.Presentation.Gameplay;
using NUnit.Framework;

namespace GritGud.Presentation.Tests
{
    public sealed class GameplayFlyoutMotionProfileTests
    {
        [Test]
        public void AuthoredProfileDrivesSharedRevealTimingAndLaserShape()
        {
            GameplayFlyoutMotionProfile profile =
                GameplayFlyoutMotionProfile.LoadDefault();

            Assert.That(profile.RevealSeconds, Is.EqualTo(0.55f));
            Assert.That(profile.LaserOuterWidth,
                Is.GreaterThan(profile.LaserInnerWidth));
            Assert.That(profile.LaserInnerWidth,
                Is.GreaterThan(profile.LaserCoreWidth));
            Assert.That(profile.Advance(
                0f,
                expanded: true,
                profile.RevealSeconds), Is.EqualTo(1f));
            Assert.That(profile.Advance(
                1f,
                expanded: false,
                profile.RevealSeconds), Is.EqualTo(0f));
            Assert.That(profile.Evaluate(0f), Is.EqualTo(0f));
            Assert.That(profile.Evaluate(1f), Is.EqualTo(1f));
            Assert.That(profile.Evaluate(0.25f), Is.LessThan(0.25f));
        }
    }
}
