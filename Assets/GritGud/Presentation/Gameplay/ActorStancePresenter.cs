using System;
using System.Collections.Generic;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Presentation.Actors.Animation;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class ActorStancePresenter : MonoBehaviour, IStanceTransitionValidator
    {
        [SerializeField]
        private ActorSpatialProfile spatialProfile = new ActorSpatialProfile();

        private CharacterController controller;
        private ActorAnimationCoordinator animationCoordinator;
        private float standingHeight;
        private Vector3 standingCenter;

        public ActorStance Stance { get; private set; } = ActorStance.Standing;

        public float CameraPivotHeight
        {
            get
            {
                EnsureInitialized();
                return spatialProfile.GetCameraPivotHeight(Stance);
            }
        }

        public float ThirdPersonCameraPivotHeight
        {
            get
            {
                EnsureInitialized();
                Vector3 standingHead = spatialProfile.GetTargetRegionLocalCenter(
                    TargetRegionId.Head,
                    ActorStance.Standing);
                Vector3 stanceHead = spatialProfile.GetTargetRegionLocalCenter(
                    TargetRegionId.Head,
                    Stance);
                return spatialProfile.GetCameraPivotHeight(ActorStance.Standing)
                    + stanceHead.y
                    - standingHead.y;
            }
        }

        public Vector3 FirstPersonEyePosition
        {
            get
            {
                EnsureInitialized();
                return transform.TransformPoint(
                    spatialProfile.GetTargetRegionLocalCenter(
                        TargetRegionId.Head,
                        Stance));
            }
        }

        public IReadOnlyList<ActorTargetRegionSample> GetTargetRegionSamples()
        {
            EnsureInitialized();
            var samples = new List<ActorTargetRegionSample>();
            Vector3 scale = transform.lossyScale;
            float radiusScale = Mathf.Max(
                Mathf.Abs(scale.x),
                Mathf.Abs(scale.y),
                Mathf.Abs(scale.z));
            foreach (ActorTargetRegionDefinition region in spatialProfile.TargetRegions)
            {
                if (region == null)
                {
                    continue;
                }

                samples.Add(new ActorTargetRegionSample(
                    region.Id,
                    transform.TransformPoint(region.GetLocalCenter(Stance)),
                    region.SampleRadius * radiusScale));
            }

            return samples.AsReadOnly();
        }

        private void Awake()
        {
            EnsureInitialized();
        }

        private void EnsureInitialized()
        {
            if (spatialProfile == null)
            {
                spatialProfile = new ActorSpatialProfile();
            }

            if (controller == null)
            {
                controller = GetComponent<CharacterController>();
                if (controller == null)
                {
                    throw new InvalidOperationException(
                        "Actor stance presentation requires a character controller.");
                }

                standingHeight = controller.height;
                standingCenter = controller.center;
            }

            if (animationCoordinator == null)
            {
                animationCoordinator =
                    GetComponent<ActorAnimationCoordinator>();
            }
        }

        public StanceTransitionValidation Validate(
            GameplayActorSnapshot actor,
            ActorStance requestedStance)
        {
            EnsureInitialized();
            if (!Enum.IsDefined(typeof(ActorStance), requestedStance))
            {
                throw new ArgumentOutOfRangeException(nameof(requestedStance));
            }

            GetShape(requestedStance, out float height, out Vector3 center);
            return requestedStance != ActorStance.Standing || CanOccupy(height, center)
                ? StanceTransitionValidation.Allowed()
                : StanceTransitionValidation.Blocked("stance.overhead-blocked");
        }

        public void ApplyResolved(ActorStance stance)
        {
            EnsureInitialized();
            if (!Enum.IsDefined(typeof(ActorStance), stance))
            {
                throw new ArgumentOutOfRangeException(nameof(stance));
            }

            GetShape(stance, out float height, out Vector3 center);
            controller.height = height;
            controller.center = center;
            Stance = stance;
            animationCoordinator?.PresentStance(stance);
        }

        private void GetShape(ActorStance stance, out float height, out Vector3 center)
        {
            if (stance == ActorStance.Standing)
            {
                height = standingHeight;
                center = standingCenter;
                return;
            }

            float minimumHeight = controller.radius * 2f;
            height = Mathf.Max(
                minimumHeight,
                standingHeight * spatialProfile.CrouchedHeightFraction);
            float standingBottom = standingCenter.y - (standingHeight * 0.5f);
            center = new Vector3(
                standingCenter.x,
                standingBottom + (height * 0.5f),
                standingCenter.z);
        }

        private bool CanOccupy(float height, Vector3 center)
        {
            Vector3 scale = transform.lossyScale;
            float radiusScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            float radius = controller.radius * radiusScale;
            float scaledHeight = Mathf.Max(height * Mathf.Abs(scale.y), radius * 2f);
            Vector3 worldCenter = transform.TransformPoint(center);
            float halfCylinder = Mathf.Max(0f, (scaledHeight * 0.5f) - radius);
            Collider[] overlaps = Physics.OverlapCapsule(
                worldCenter + (Vector3.up * halfCylinder),
                worldCenter - (Vector3.up * halfCylinder),
                Mathf.Max(0f, radius - controller.skinWidth),
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            foreach (Collider overlap in overlaps)
            {
                if (overlap != null
                    && overlap.transform != transform
                    && !overlap.transform.IsChildOf(transform))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
