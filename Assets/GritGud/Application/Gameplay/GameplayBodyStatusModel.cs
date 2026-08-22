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
            int woundCount,
            int structuralIntegrity = 100,
            int motorFunction = 100,
            int sensoryFunction = 100,
            int bleedRate = 0)
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
            RequirePercent(structuralIntegrity, nameof(structuralIntegrity));
            RequirePercent(motorFunction, nameof(motorFunction));
            RequirePercent(sensoryFunction, nameof(sensoryFunction));
            RequirePercent(bleedRate, nameof(bleedRate));

            Region = region;
            Label = label;
            WoundCount = woundCount;
            StructuralIntegrity = structuralIntegrity;
            MotorFunction = motorFunction;
            SensoryFunction = sensoryFunction;
            BleedRate = bleedRate;
        }

        public TargetRegionId Region { get; }

        public string Label { get; }

        public int WoundCount { get; }

        public bool IsWounded => WoundCount > 0;

        public int StructuralIntegrity { get; }

        public int MotorFunction { get; }

        public int SensoryFunction { get; }

        public int BleedRate { get; }

        public int ConditionPercent => Math.Min(
            StructuralIntegrity,
            Math.Min(MotorFunction, SensoryFunction));

        private static void RequirePercent(int value, string parameter)
        {
            if (value < 0 || value > 100)
                throw new ArgumentOutOfRangeException(parameter);
        }
    }

    public sealed class GameplayBodyStatusModel
    {
        private const int RegionCount = 6;

        public GameplayBodyStatusModel(
            string actorId,
            IEnumerable<GameplayBodyRegionModel> regions,
            int maximumWounds,
            float movementPenalty,
            int unlocalizedWounds = 0,
            ActorLifeState lifeState = ActorLifeState.Active,
            ActorCapabilityState capabilities = null,
            ActorPhysiologyState physiology = null,
            int conditionPercent = 100,
            int systemicTrauma = 0)
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
            if (!Enum.IsDefined(typeof(ActorLifeState), lifeState))
                throw new ArgumentOutOfRangeException(nameof(lifeState));
            if (conditionPercent < 0 || conditionPercent > 100)
                throw new ArgumentOutOfRangeException(nameof(conditionPercent));
            if (systemicTrauma < 0)
                throw new ArgumentOutOfRangeException(nameof(systemicTrauma));

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
            LifeState = lifeState;
            Capabilities = capabilities ?? new ActorCapabilityState(
                100, 100, 100, 100, 100, 100,
                true, true, true, true);
            Physiology = physiology ?? ActorPhysiologyState.Healthy;
            ConditionPercent = conditionPercent;
            SystemicTrauma = systemicTrauma;
        }

        public string ActorId { get; }

        public IReadOnlyList<GameplayBodyRegionModel> Regions { get; }

        public int TotalWounds { get; }

        public int MaximumWounds { get; }

        public int UnlocalizedWounds { get; }

        public float MovementPenalty { get; }

        public ActorLifeState LifeState { get; }

        public ActorCapabilityState Capabilities { get; }

        public ActorPhysiologyState Physiology { get; }

        public int ConditionPercent { get; }

        public int SystemicTrauma { get; }

        public bool IsIncapacitated => LifeState != ActorLifeState.Active;

        public bool IsDead => LifeState == ActorLifeState.Dead;

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
