using GritGud.Editor;
using GritGud.Presentation.Actors.Animation;
using NUnit.Framework;
using UnityEngine;

namespace GritGud.Presentation.Tests
{
    public sealed class BodyRegionMaskBuilderTests
    {
        [TestCase(BodyRegion.PelvisAndLegs)]
        [TestCase(BodyRegion.TorsoAndArms)]
        [TestCase(BodyRegion.HeadAndNeck)]
        [TestCase(BodyRegion.LeftArm)]
        [TestCase(BodyRegion.RightArm)]
        [TestCase(BodyRegion.Hands)]
        [TestCase(BodyRegion.WholeBody)]
        public void RegionDefinitionRoundTripsForDefaultHumanoid(BodyRegion region)
        {
            GameObject prefab = Resources.Load<GameObject>(
                "Actors/DefaultPlayerActor");
            GameObject actor = Object.Instantiate(prefab);
            var mask = new AvatarMask();
            try
            {
                Animator animator = actor.GetComponentInChildren<Animator>(true);

                BodyRegionMaskBuilder.Configure(mask, animator, region);

                Assert.That(
                    BodyRegionMaskBuilder.Matches(mask, animator, region),
                    Is.True);
            }
            finally
            {
                Object.DestroyImmediate(mask);
                Object.DestroyImmediate(actor);
            }
        }
    }
}
