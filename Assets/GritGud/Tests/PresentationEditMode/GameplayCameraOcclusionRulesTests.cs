using GritGud.Presentation.Gameplay;
using NUnit.Framework;

namespace GritGud.Presentation.Tests
{
    public sealed class GameplayCameraOcclusionRulesTests
    {
        [TestCase("structure.wall.standard")]
        [TestCase("structure.wall.doorway")]
        public void WallArchetypesUsePlayerCutout(string archetypeId)
        {
            Assert.That(
                GameplayCameraOcclusionRules.UsesPlayerCutout(archetypeId),
                Is.True);
        }

        [TestCase("structure.floor.standard")]
        [TestCase("structure.stairs.standard")]
        [TestCase("prop.crate.standard")]
        [TestCase("")]
        [TestCase(null)]
        public void NonWallArchetypesDoNotUsePlayerCutout(string archetypeId)
        {
            Assert.That(
                GameplayCameraOcclusionRules.UsesPlayerCutout(archetypeId),
                Is.False);
        }
    }
}
