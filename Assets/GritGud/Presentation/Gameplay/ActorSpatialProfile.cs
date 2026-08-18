using System;
using GritGud.Domain.Gameplay;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
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

        public float CrouchedHeightFraction =>
            Mathf.Clamp(crouchedHeightFraction, 0.4f, 1f);

        public float GetCameraPivotHeight(ActorStance stance) =>
            stance == ActorStance.Crouched
                ? Mathf.Max(0f, crouchedCameraPivotHeight)
                : Mathf.Max(0f, standingCameraPivotHeight);

        public Vector3 GetTargetRegionLocalCenter(
            TargetRegionId id,
            ActorStance stance)
        {
            ActorLocalPoint center = ActorTargetProfileCatalog
                .Resolve(stance, pinned: false)
                .GetRegion(id)
                .LocalCenter;
            return new Vector3(center.X, center.Y, center.Z);
        }
    }
}
