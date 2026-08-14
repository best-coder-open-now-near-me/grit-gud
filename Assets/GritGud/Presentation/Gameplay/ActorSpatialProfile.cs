using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    [Serializable]
    public sealed class ActorTargetRegionDefinition
    {
        [SerializeField]
        private TargetRegionId id;

        [SerializeField]
        private Vector3 standingLocalCenter;

        [SerializeField]
        private Vector3 crouchedLocalCenter;

        [SerializeField, Min(0.01f)]
        private float sampleRadius = 0.12f;

        public ActorTargetRegionDefinition(
            TargetRegionId regionId,
            Vector3 standingCenter,
            Vector3 crouchedCenter,
            float radius)
        {
            id = regionId;
            standingLocalCenter = standingCenter;
            crouchedLocalCenter = crouchedCenter;
            sampleRadius = Mathf.Max(0.01f, radius);
        }

        public TargetRegionId Id => id;
        public float SampleRadius => Mathf.Max(0.01f, sampleRadius);

        public Vector3 GetLocalCenter(ActorStance stance) =>
            stance == ActorStance.Crouched
                ? crouchedLocalCenter
                : standingLocalCenter;
    }

    public readonly struct ActorTargetRegionSample
    {
        public ActorTargetRegionSample(
            TargetRegionId id,
            Vector3 worldCenter,
            float radius)
        {
            Id = id;
            WorldCenter = worldCenter;
            Radius = radius;
        }

        public TargetRegionId Id { get; }
        public Vector3 WorldCenter { get; }
        public float Radius { get; }
    }

    [Serializable]
    public sealed class ActorSpatialProfile
    {
        [SerializeField, Range(0.4f, 1f)]
        private float crouchedHeightFraction = 0.62f;

        [SerializeField, Min(0f)]
        private float standingCameraPivotHeight = 1.3f;

        [SerializeField, Min(0f)]
        private float crouchedCameraPivotHeight = 0.9f;

        [SerializeField]
        private ActorTargetRegionDefinition[] targetRegions = CreateDefaultRegions();

        public float CrouchedHeightFraction =>
            Mathf.Clamp(crouchedHeightFraction, 0.4f, 1f);

        public float GetCameraPivotHeight(ActorStance stance) =>
            stance == ActorStance.Crouched
                ? Mathf.Max(0f, crouchedCameraPivotHeight)
                : Mathf.Max(0f, standingCameraPivotHeight);

        public IReadOnlyList<ActorTargetRegionDefinition> TargetRegions =>
            targetRegions ?? Array.Empty<ActorTargetRegionDefinition>();

        public Vector3 GetTargetRegionLocalCenter(
            TargetRegionId id,
            ActorStance stance)
        {
            foreach (ActorTargetRegionDefinition region in TargetRegions)
            {
                if (region != null && region.Id == id)
                {
                    return region.GetLocalCenter(stance);
                }
            }

            throw new InvalidOperationException(
                $"Actor spatial profile does not define target region '{id}'.");
        }

        private static ActorTargetRegionDefinition[] CreateDefaultRegions()
        {
            return new[]
            {
                new ActorTargetRegionDefinition(TargetRegionId.Head,
                    new Vector3(0f, 1.62f, 0f), new Vector3(0f, 1.08f, 0f), 0.14f),
                new ActorTargetRegionDefinition(TargetRegionId.Torso,
                    new Vector3(0f, 1.18f, 0f), new Vector3(0f, 0.78f, 0f), 0.2f),
                new ActorTargetRegionDefinition(TargetRegionId.LeftArm,
                    new Vector3(-0.32f, 1.2f, 0f), new Vector3(-0.3f, 0.8f, 0f), 0.12f),
                new ActorTargetRegionDefinition(TargetRegionId.RightArm,
                    new Vector3(0.32f, 1.2f, 0f), new Vector3(0.3f, 0.8f, 0f), 0.12f),
                new ActorTargetRegionDefinition(TargetRegionId.LeftLeg,
                    new Vector3(-0.16f, 0.55f, 0f), new Vector3(-0.18f, 0.38f, 0f), 0.15f),
                new ActorTargetRegionDefinition(TargetRegionId.RightLeg,
                    new Vector3(0.16f, 0.55f, 0f), new Vector3(0.18f, 0.38f, 0f), 0.15f),
            };
        }
    }
}
