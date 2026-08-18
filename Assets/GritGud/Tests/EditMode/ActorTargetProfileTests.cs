using System.Linq;
using GritGud.Domain.Gameplay;
using NUnit.Framework;

namespace GritGud.Domain.Tests
{
    public sealed class ActorTargetProfileTests
    {
        [Test]
        public void PinnedDownOverridesStanceWithHorizontalAcquisitionVolume()
        {
            ActorTargetProfile profile = ActorTargetProfileCatalog.Resolve(
                ActorStance.Crouched,
                pinned: true);

            Assert.That(
                profile.Kind,
                Is.EqualTo(ActorTargetProfileKind.PinnedDown));
            Assert.That(
                profile.AcquisitionVolume.Axis,
                Is.EqualTo(ActorTargetVolumeAxis.Z));
            Assert.That(profile.AcquisitionVolume.Height,
                Is.GreaterThan(profile.AcquisitionVolume.Radius * 2f));
            Assert.That(profile.Regions.Select(region => region.Id).Distinct()
                .Count(), Is.EqualTo(6));
            Assert.That(
                profile.GetRegion(TargetRegionId.Head).LocalCenter.Z,
                Is.LessThan(
                    profile.GetRegion(TargetRegionId.LeftLeg).LocalCenter.Z));
        }

        [Test]
        public void PinnedWorldSamplesFollowCanonicalFacing()
        {
            var pose = new GameplayActorPose(
                new GameplayPosition(10f, 2f, 20f),
                facingDegrees: 90f,
                ActorStance.Standing);
            TargetRegionSample head = ActorTargetProfileCatalog
                .CreateWorldSamples(pose, pinned: true)
                .Single(sample => sample.Id == TargetRegionId.Head);

            Assert.That(head.Center.X, Is.EqualTo(8.48f).Within(0.001f));
            Assert.That(head.Center.Y, Is.EqualTo(2.3f).Within(0.001f));
            Assert.That(head.Center.Z, Is.EqualTo(20f).Within(0.001f));
        }

        [Test]
        public void StandingAndCrouchedReuseTheSameSemanticRegions()
        {
            ActorTargetProfile standing = ActorTargetProfileCatalog.Resolve(
                ActorStance.Standing,
                pinned: false);
            ActorTargetProfile crouched = ActorTargetProfileCatalog.Resolve(
                ActorStance.Crouched,
                pinned: false);

            Assert.That(
                crouched.Regions.Select(region => region.Id),
                Is.EqualTo(standing.Regions.Select(region => region.Id)));
            Assert.That(
                crouched.GetRegion(TargetRegionId.Head).LocalCenter.Y,
                Is.LessThan(
                    standing.GetRegion(TargetRegionId.Head).LocalCenter.Y));
            Assert.That(
                standing.AcquisitionVolume.Axis,
                Is.EqualTo(ActorTargetVolumeAxis.Y));
        }
    }
}
