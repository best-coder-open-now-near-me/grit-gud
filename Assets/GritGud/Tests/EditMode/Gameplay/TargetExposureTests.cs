using System;
using GritGud.Domain.Gameplay;
using NUnit.Framework;

namespace GritGud.Domain.Tests.Gameplay
{
    public sealed class TargetExposureTests
    {
        [Test]
        public void SnapshotCopiesRegionResultsAndAggregatesSamples()
        {
            var source = new[]
            {
                new TargetRegionExposure(TargetRegionId.Head, 5, 5),
                new TargetRegionExposure(TargetRegionId.Torso, 2, 5),
            };

            var snapshot = new TargetExposureSnapshot("observer", "target", source);

            Assert.That(snapshot.ObserverId, Is.EqualTo("observer"));
            Assert.That(snapshot.TargetId, Is.EqualTo("target"));
            Assert.That(snapshot.VisibleSampleCount, Is.EqualTo(7));
            Assert.That(snapshot.TotalSampleCount, Is.EqualTo(10));
            Assert.That(snapshot.VisibleFraction, Is.EqualTo(0.7f));
            Assert.That(snapshot.GetRegion(TargetRegionId.Head).IsExposed, Is.True);
            Assert.That(snapshot.GetRegion(TargetRegionId.Torso).VisibleFraction,
                Is.EqualTo(0.4f));
        }

        [Test]
        public void SnapshotRejectsDuplicateRegions()
        {
            Assert.Throws<ArgumentException>(() => new TargetExposureSnapshot(
                "observer",
                "target",
                new[]
                {
                    new TargetRegionExposure(TargetRegionId.Head, 1, 5),
                    new TargetRegionExposure(TargetRegionId.Head, 2, 5),
                }));
        }

        [Test]
        public void RegionExposureRejectsImpossibleCounts()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new TargetRegionExposure(TargetRegionId.Head, 6, 5));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new TargetRegionExposure(TargetRegionId.Head, -1, 5));
        }

        [Test]
        public void FullySelfOccludedRegionCanRecordAnEmptyPaintedArea()
        {
            var snapshot = new TargetExposureSnapshot(
                "observer",
                "target",
                new[]
                {
                    new TargetRegionExposure(TargetRegionId.Torso, 8, 8),
                    new TargetRegionExposure(TargetRegionId.LeftArm, 0, 0),
                });

            Assert.That(
                snapshot.GetRegion(TargetRegionId.LeftArm).VisibleFraction,
                Is.Zero);
            Assert.That(snapshot.TotalSampleCount, Is.EqualTo(8));
            Assert.Throws<ArgumentException>(() =>
                new TargetExposureSnapshot(
                    "observer",
                    "target",
                    new[]
                    {
                        new TargetRegionExposure(
                            TargetRegionId.LeftArm,
                            0,
                            0),
                    }));
        }
    }
}
