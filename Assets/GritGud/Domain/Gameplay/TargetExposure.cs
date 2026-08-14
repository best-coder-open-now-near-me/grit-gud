using System;
using System.Collections.Generic;

namespace GritGud.Domain.Gameplay
{
    public readonly struct TargetRegionSample
    {
        public TargetRegionSample(
            TargetRegionId id,
            GameplayPosition center,
            float radius)
        {
            if (!Enum.IsDefined(typeof(TargetRegionId), id))
            {
                throw new ArgumentOutOfRangeException(nameof(id));
            }

            if (float.IsNaN(radius) || float.IsInfinity(radius) || radius <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(radius));
            }

            Id = id;
            Center = center;
            Radius = radius;
        }

        public TargetRegionId Id { get; }

        public GameplayPosition Center { get; }

        public float Radius { get; }
    }

    public readonly struct TargetRegionExposure
    {
        public TargetRegionExposure(
            TargetRegionId id,
            int visibleSampleCount,
            int totalSampleCount)
        {
            if (!Enum.IsDefined(typeof(TargetRegionId), id))
            {
                throw new ArgumentOutOfRangeException(nameof(id));
            }

            if (totalSampleCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(totalSampleCount));
            }

            if (visibleSampleCount < 0 || visibleSampleCount > totalSampleCount)
            {
                throw new ArgumentOutOfRangeException(nameof(visibleSampleCount));
            }

            Id = id;
            VisibleSampleCount = visibleSampleCount;
            TotalSampleCount = totalSampleCount;
        }

        public TargetRegionId Id { get; }

        public int VisibleSampleCount { get; }

        public int TotalSampleCount { get; }

        public bool IsExposed => VisibleSampleCount > 0;

        public float VisibleFraction =>
            TotalSampleCount == 0
                ? 0f
                : (float)VisibleSampleCount / TotalSampleCount;
    }

    public sealed class TargetExposureSnapshot
    {
        private readonly IReadOnlyList<TargetRegionExposure> regions;

        public TargetExposureSnapshot(
            string observerId,
            string targetId,
            IEnumerable<TargetRegionExposure> regions)
        {
            if (string.IsNullOrWhiteSpace(observerId))
            {
                throw new ArgumentException(
                    "Exposure snapshots require an observer identifier.",
                    nameof(observerId));
            }

            if (string.IsNullOrWhiteSpace(targetId))
            {
                throw new ArgumentException(
                    "Exposure snapshots require a target identifier.",
                    nameof(targetId));
            }

            if (string.Equals(observerId, targetId, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "An actor cannot observe its own target exposure.",
                    nameof(targetId));
            }

            if (regions == null)
            {
                throw new ArgumentNullException(nameof(regions));
            }

            var copiedRegions = new List<TargetRegionExposure>();
            var regionIds = new HashSet<TargetRegionId>();
            int visibleSamples = 0;
            int totalSamples = 0;
            foreach (TargetRegionExposure region in regions)
            {
                if (!regionIds.Add(region.Id))
                {
                    throw new ArgumentException(
                        $"Exposure snapshots cannot repeat region '{region.Id}'.",
                        nameof(regions));
                }

                copiedRegions.Add(region);
                visibleSamples += region.VisibleSampleCount;
                totalSamples += region.TotalSampleCount;
            }

            if (copiedRegions.Count == 0)
            {
                throw new ArgumentException(
                    "Exposure snapshots require at least one target region.",
                    nameof(regions));
            }

            if (totalSamples == 0)
            {
                throw new ArgumentException(
                    "Exposure snapshots require a non-empty target silhouette.",
                    nameof(regions));
            }

            ObserverId = observerId;
            TargetId = targetId;
            VisibleSampleCount = visibleSamples;
            TotalSampleCount = totalSamples;
            this.regions = copiedRegions.AsReadOnly();
        }

        public string ObserverId { get; }

        public string TargetId { get; }

        public IReadOnlyList<TargetRegionExposure> Regions => regions;

        public int VisibleSampleCount { get; }

        public int TotalSampleCount { get; }

        public float VisibleFraction =>
            (float)VisibleSampleCount / TotalSampleCount;

        public TargetRegionExposure GetRegion(TargetRegionId id)
        {
            foreach (TargetRegionExposure region in regions)
            {
                if (region.Id == id)
                {
                    return region;
                }
            }

            throw new InvalidOperationException(
                $"Exposure snapshot does not contain region '{id}'.");
        }
    }

    public static class TargetExposureRules
    {
        public static int CalculateHitChancePercent(
            TargetExposureSnapshot exposure)
        {
            if (exposure == null)
            {
                throw new ArgumentNullException(nameof(exposure));
            }

            int chance = (int)Math.Round(
                exposure.VisibleFraction * 100f,
                MidpointRounding.ToEven);
            return Math.Max(0, Math.Min(100, chance));
        }

        public static TargetRegionId SelectVisibleRegion(
            TargetExposureSnapshot exposure,
            int visibleSampleRoll)
        {
            if (exposure == null)
            {
                throw new ArgumentNullException(nameof(exposure));
            }

            if (visibleSampleRoll < 1
                || visibleSampleRoll > exposure.VisibleSampleCount)
            {
                throw new ArgumentOutOfRangeException(nameof(visibleSampleRoll));
            }

            int remaining = visibleSampleRoll;
            foreach (TargetRegionExposure region in exposure.Regions)
            {
                remaining -= region.VisibleSampleCount;
                if (remaining <= 0)
                {
                    return region.Id;
                }
            }

            throw new InvalidOperationException(
                "Visible-region selection exceeded the recorded exposure.");
        }
    }
}
