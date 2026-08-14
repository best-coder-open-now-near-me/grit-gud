using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;

namespace GritGud.Application.Gameplay
{
    public sealed class GameplayBodyRegionModel
    {
        public GameplayBodyRegionModel(
            TargetRegionId region,
            string label,
            int woundCount)
        {
            if (!Enum.IsDefined(typeof(TargetRegionId), region))
            {
                throw new ArgumentOutOfRangeException(nameof(region));
            }

            if (string.IsNullOrWhiteSpace(label))
            {
                throw new ArgumentException(
                    "Body-region HUD labels cannot be empty.",
                    nameof(label));
            }

            if (woundCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(woundCount));
            }

            Region = region;
            Label = label;
            WoundCount = woundCount;
        }

        public TargetRegionId Region { get; }

        public string Label { get; }

        public int WoundCount { get; }

        public bool IsWounded => WoundCount > 0;
    }

    public sealed class GameplayBodyStatusModel
    {
        private const int RegionCount = 6;

        public GameplayBodyStatusModel(
            string actorId,
            IEnumerable<GameplayBodyRegionModel> regions,
            int maximumWounds,
            float movementPenalty,
            int unlocalizedWounds = 0)
        {
            if (string.IsNullOrWhiteSpace(actorId))
            {
                throw new ArgumentException(
                    "Body-status HUD models require an actor identifier.",
                    nameof(actorId));
            }

            if (regions == null)
            {
                throw new ArgumentNullException(nameof(regions));
            }

            if (maximumWounds <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumWounds));
            }

            if (float.IsNaN(movementPenalty)
                || float.IsInfinity(movementPenalty)
                || movementPenalty < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(movementPenalty));
            }

            if (unlocalizedWounds < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(unlocalizedWounds));
            }

            var copy = new List<GameplayBodyRegionModel>(RegionCount);
            var indexedRegions = new HashSet<TargetRegionId>();
            int totalWounds = 0;
            foreach (GameplayBodyRegionModel region in regions)
            {
                if (region == null)
                {
                    throw new ArgumentException(
                        "Body-status regions cannot contain null entries.",
                        nameof(regions));
                }

                if (!indexedRegions.Add(region.Region))
                {
                    throw new ArgumentException(
                        $"Body region '{region.Region}' appears more than once.",
                        nameof(regions));
                }

                totalWounds = checked(totalWounds + region.WoundCount);
                copy.Add(region);
            }

            if (copy.Count != RegionCount)
            {
                throw new ArgumentException(
                    $"Body-status HUD models require exactly {RegionCount} regions.",
                    nameof(regions));
            }

            ActorId = actorId;
            Regions = copy.AsReadOnly();
            TotalWounds = checked(totalWounds + unlocalizedWounds);
            MaximumWounds = maximumWounds;
            MovementPenalty = movementPenalty;
            UnlocalizedWounds = unlocalizedWounds;
        }

        public string ActorId { get; }

        public IReadOnlyList<GameplayBodyRegionModel> Regions { get; }

        public int TotalWounds { get; }

        public int MaximumWounds { get; }

        public int UnlocalizedWounds { get; }

        public float MovementPenalty { get; }

        public bool IsIncapacitated => TotalWounds >= MaximumWounds;

        public GameplayBodyRegionModel FindRegion(TargetRegionId region)
        {
            foreach (GameplayBodyRegionModel candidate in Regions)
            {
                if (candidate.Region == region)
                {
                    return candidate;
                }
            }

            throw new KeyNotFoundException(
                $"Body region '{region}' is missing from actor '{ActorId}'.");
        }
    }
}
