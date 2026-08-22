using System;
using GritGud.Domain.Gameplay;
using GritGud.Presentation.Actors.Animation;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    public readonly struct ActorInjuryAnimationOverlay
    {
        public ActorInjuryAnimationOverlay(
            float bodyPitchDegrees,
            float bodyRollDegrees,
            float impairedLegPitchDegrees,
            float impairedLegRollDegrees,
            float leftArmSagDegrees,
            float rightArmSagDegrees)
        {
            BodyPitchDegrees = bodyPitchDegrees;
            BodyRollDegrees = bodyRollDegrees;
            ImpairedLegPitchDegrees = impairedLegPitchDegrees;
            ImpairedLegRollDegrees = impairedLegRollDegrees;
            LeftArmSagDegrees = leftArmSagDegrees;
            RightArmSagDegrees = rightArmSagDegrees;
        }

        public float BodyPitchDegrees { get; }
        public float BodyRollDegrees { get; }
        public float ImpairedLegPitchDegrees { get; }
        public float ImpairedLegRollDegrees { get; }
        public float LeftArmSagDegrees { get; }
        public float RightArmSagDegrees { get; }
    }

    public static class ActorInjuryAnimationOverlayProjector
    {
        public static ActorInjuryAnimationOverlay Project(
            ActorCapabilityState capabilities)
        {
            if (capabilities == null)
                throw new ArgumentNullException(nameof(capabilities));
            ActorMobilityCapability mobility = capabilities.Mobility;
            float side = mobility.ImpairedSide == ActorImpairedSide.Left
                ? -1f
                : mobility.ImpairedSide == ActorImpairedSide.Right
                    ? 1f
                    : 0f;
            float bodyPitch;
            float bodyRoll;
            float legPitch;
            float legRoll;
            switch (mobility.Gait)
            {
                case ActorGait.Normal:
                    bodyPitch = 0f;
                    bodyRoll = 0f;
                    legPitch = 0f;
                    legRoll = 0f;
                    break;
                case ActorGait.MildLimp:
                    bodyPitch = 2f;
                    bodyRoll = side * 4f;
                    legPitch = 7f;
                    legRoll = side * 3f;
                    break;
                case ActorGait.SevereLimp:
                    bodyPitch = 8f;
                    bodyRoll = side * 9f;
                    legPitch = 18f;
                    legRoll = side * 7f;
                    break;
                case ActorGait.Crawling:
                    bodyPitch = 48f;
                    bodyRoll = 0f;
                    legPitch = 28f;
                    legRoll = 0f;
                    break;
                case ActorGait.Immobile:
                    bodyPitch = 55f;
                    bodyRoll = 0f;
                    legPitch = 35f;
                    legRoll = 0f;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(capabilities));
            }

            return new ActorInjuryAnimationOverlay(
                bodyPitch,
                bodyRoll,
                legPitch,
                legRoll,
                CalculateArmSag(capabilities.LeftGripCapacity, -1f),
                CalculateArmSag(capabilities.RightGripCapacity, 1f));
        }

        private static float CalculateArmSag(int grip, float side) =>
            grip >= 70 ? 0f : side * Mathf.Lerp(6f, 28f, (70 - grip) / 70f);
    }

    /// <summary>
    /// Applies deterministic, presentation-only injured-limb overlays after
    /// authored animation and immediately before weapon IK. Root motion never
    /// participates in authoritative movement.
    /// </summary>
    [DefaultExecutionOrder(ActorAnimationUpdateOrder.PostAnimationSolve - 1)]
    [DisallowMultipleComponent]
    public sealed class ActorInjuryAnimationOverlayPresenter : MonoBehaviour
    {
        private Animator animator;
        private Transform hips;
        private Transform leftUpperLeg;
        private Transform rightUpperLeg;
        private Transform leftUpperArm;
        private Transform rightUpperArm;
        private ActorCapabilityState capabilities;
        private ActorCapabilityState replayOriginalCapabilities;
        private bool replayPresentation;

        public ActorCapabilityState CurrentCapabilities => capabilities;

        internal bool IsPresentingReplay => replayPresentation;

        private void Awake() => BindAnimator(
            GetComponentInChildren<Animator>());

        internal void BindAnimator(Animator value)
        {
            animator = value;
            if (animator == null || !animator.isHuman)
            {
                hips = null;
                leftUpperLeg = null;
                rightUpperLeg = null;
                leftUpperArm = null;
                rightUpperArm = null;
                return;
            }
            hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            leftUpperLeg = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            rightUpperLeg = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            leftUpperArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            rightUpperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        }

        internal void PresentAuthoritative(ActorCapabilityState value)
        {
            if (replayPresentation)
                throw new InvalidOperationException(
                    "Authoritative injury overlays are paused during replay.");
            capabilities = value ?? throw new ArgumentNullException(
                nameof(value));
        }

        internal void BeginReplayPresentation()
        {
            if (replayPresentation)
                throw new InvalidOperationException(
                    "Injury-overlay replay presentation is already active.");
            replayOriginalCapabilities = capabilities;
            replayPresentation = true;
        }

        internal void PresentReplay(ActorCapabilityState value)
        {
            if (!replayPresentation)
                throw new InvalidOperationException(
                    "Begin injury-overlay replay presentation before projection.");
            capabilities = value ?? throw new ArgumentNullException(
                nameof(value));
        }

        internal void EndReplayPresentation()
        {
            if (!replayPresentation)
                return;
            capabilities = replayOriginalCapabilities;
            replayOriginalCapabilities = null;
            replayPresentation = false;
        }

        private void LateUpdate() => SynchronizeAfterAnimation();

        internal void SynchronizeAfterAnimation()
        {
            if (capabilities == null || !capabilities.IsActive
                || animator == null
                || !animator.enabled || !animator.isHuman)
                return;
            ActorInjuryAnimationOverlay overlay =
                ActorInjuryAnimationOverlayProjector.Project(capabilities);
            if (hips != null)
                hips.localRotation *= Quaternion.Euler(
                    overlay.BodyPitchDegrees,
                    0f,
                    overlay.BodyRollDegrees);
            Transform impairedLeg = capabilities.Mobility.ImpairedSide
                == ActorImpairedSide.Left
                    ? leftUpperLeg
                    : capabilities.Mobility.ImpairedSide
                        == ActorImpairedSide.Right
                        ? rightUpperLeg
                        : null;
            if (impairedLeg != null
                && capabilities.Mobility.Gait != ActorGait.Crawling
                && capabilities.Mobility.Gait != ActorGait.Immobile)
                impairedLeg.localRotation *= Quaternion.Euler(
                    overlay.ImpairedLegPitchDegrees,
                    0f,
                    overlay.ImpairedLegRollDegrees);
            if (capabilities.Mobility.Gait == ActorGait.Crawling
                || capabilities.Mobility.Gait == ActorGait.Immobile)
            {
                if (leftUpperLeg != null)
                    leftUpperLeg.localRotation *= Quaternion.Euler(
                        overlay.ImpairedLegPitchDegrees, 0f, 0f);
                if (rightUpperLeg != null)
                    rightUpperLeg.localRotation *= Quaternion.Euler(
                        overlay.ImpairedLegPitchDegrees, 0f, 0f);
            }
            if (leftUpperArm != null && overlay.LeftArmSagDegrees != 0f)
                leftUpperArm.localRotation *= Quaternion.Euler(
                    0f, 0f, overlay.LeftArmSagDegrees);
            if (rightUpperArm != null && overlay.RightArmSagDegrees != 0f)
                rightUpperArm.localRotation *= Quaternion.Euler(
                    0f, 0f, overlay.RightArmSagDegrees);
        }

        private void OnDestroy() => EndReplayPresentation();
    }
}
