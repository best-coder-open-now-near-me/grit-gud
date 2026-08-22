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

    public readonly struct ActorHitReactionOverlay
    {
        public ActorHitReactionOverlay(
            float bodyPitchDegrees,
            float bodyRollDegrees,
            float headPitchDegrees,
            float headRollDegrees,
            float leftArmPitchDegrees,
            float rightArmPitchDegrees,
            float leftLegPitchDegrees,
            float rightLegPitchDegrees)
        {
            BodyPitchDegrees = bodyPitchDegrees;
            BodyRollDegrees = bodyRollDegrees;
            HeadPitchDegrees = headPitchDegrees;
            HeadRollDegrees = headRollDegrees;
            LeftArmPitchDegrees = leftArmPitchDegrees;
            RightArmPitchDegrees = rightArmPitchDegrees;
            LeftLegPitchDegrees = leftLegPitchDegrees;
            RightLegPitchDegrees = rightLegPitchDegrees;
        }

        public float BodyPitchDegrees { get; }
        public float BodyRollDegrees { get; }
        public float HeadPitchDegrees { get; }
        public float HeadRollDegrees { get; }
        public float LeftArmPitchDegrees { get; }
        public float RightArmPitchDegrees { get; }
        public float LeftLegPitchDegrees { get; }
        public float RightLegPitchDegrees { get; }

        public float MaximumAbsoluteDegrees => Mathf.Max(
            Mathf.Abs(BodyPitchDegrees),
            Mathf.Abs(BodyRollDegrees),
            Mathf.Abs(HeadPitchDegrees),
            Mathf.Abs(HeadRollDegrees),
            Mathf.Abs(LeftArmPitchDegrees),
            Mathf.Abs(RightArmPitchDegrees),
            Mathf.Abs(LeftLegPitchDegrees),
            Mathf.Abs(RightLegPitchDegrees));
    }

    public static class ActorInjuryAnimationOverlayProjector
    {
        public const float HitReactionSeconds = 0.6f;

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

        public static ActorHitReactionOverlay ProjectHitReaction(
            TargetRegionId region,
            float normalizedProgress)
        {
            if (!Enum.IsDefined(typeof(TargetRegionId), region))
                throw new ArgumentOutOfRangeException(nameof(region));
            if (float.IsNaN(normalizedProgress)
                || float.IsInfinity(normalizedProgress))
                throw new ArgumentOutOfRangeException(
                    nameof(normalizedProgress));
            float weight = CalculateHitReactionWeight(normalizedProgress);
            switch (region)
            {
                case TargetRegionId.Head:
                    return new ActorHitReactionOverlay(
                        -4f * weight, 0f,
                        -14f * weight, 6f * weight,
                        0f, 0f, 0f, 0f);
                case TargetRegionId.Torso:
                    return new ActorHitReactionOverlay(
                        -11f * weight, 5f * weight,
                        -3f * weight, 0f,
                        0f, 0f, 0f, 0f);
                case TargetRegionId.LeftArm:
                    return new ActorHitReactionOverlay(
                        -4f * weight, 7f * weight,
                        0f, 0f,
                        -22f * weight, 0f, 0f, 0f);
                case TargetRegionId.RightArm:
                    return new ActorHitReactionOverlay(
                        -4f * weight, -7f * weight,
                        0f, 0f,
                        0f, -22f * weight, 0f, 0f);
                case TargetRegionId.LeftLeg:
                    return new ActorHitReactionOverlay(
                        -6f * weight, 6f * weight,
                        0f, 0f,
                        0f, 0f, 16f * weight, 0f);
                case TargetRegionId.RightLeg:
                    return new ActorHitReactionOverlay(
                        -6f * weight, -6f * weight,
                        0f, 0f,
                        0f, 0f, 0f, 16f * weight);
                default:
                    throw new ArgumentOutOfRangeException(nameof(region));
            }
        }

        internal static float CalculateHitReactionWeight(float progress)
        {
            float clamped = Mathf.Clamp01(progress);
            if (clamped <= 0f || clamped >= 1f)
                return 0f;
            const float impactPeak = 0.22f;
            return clamped < impactPeak
                ? SmoothStep(clamped / impactPeak)
                : 1f - SmoothStep(
                    (clamped - impactPeak) / (1f - impactPeak));
        }

        private static float SmoothStep(float value)
        {
            float clamped = Mathf.Clamp01(value);
            return clamped * clamped * (3f - (2f * clamped));
        }
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
        private Transform chest;
        private Transform head;
        private Transform leftUpperLeg;
        private Transform rightUpperLeg;
        private Transform leftUpperArm;
        private Transform rightUpperArm;
        private ActorCapabilityState capabilities;
        private ActorCapabilityState replayOriginalCapabilities;
        private TargetRegionId liveHitRegion;
        private float liveHitElapsedSeconds;
        private bool liveHitActive;
        private TargetRegionId replayHitRegion;
        private float replayHitProgress;
        private bool replayHitActive;
        private TargetRegionId replayOriginalHitRegion;
        private float replayOriginalHitElapsedSeconds;
        private bool replayOriginalHitActive;
        private bool replayPresentation;

        public ActorCapabilityState CurrentCapabilities => capabilities;

        internal bool IsPresentingReplay => replayPresentation;

        internal bool HitReactionActive => replayPresentation
            ? replayHitActive
            : liveHitActive;

        internal float HitReactionProgress => replayPresentation
            ? replayHitProgress
            : Mathf.Clamp01(liveHitElapsedSeconds
                / ActorInjuryAnimationOverlayProjector.HitReactionSeconds);

        private void Awake() => BindAnimator(
            GetComponentInChildren<Animator>());

        internal void BindAnimator(Animator value)
        {
            animator = value;
            if (animator == null || !animator.isHuman)
            {
                hips = null;
                chest = null;
                head = null;
                leftUpperLeg = null;
                rightUpperLeg = null;
                leftUpperArm = null;
                rightUpperArm = null;
                return;
            }
            hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            chest = animator.GetBoneTransform(HumanBodyBones.Chest)
                ?? animator.GetBoneTransform(HumanBodyBones.Spine);
            head = animator.GetBoneTransform(HumanBodyBones.Head);
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

        internal bool PresentHitReaction(TargetRegionId region)
        {
            if (replayPresentation || !Enum.IsDefined(
                    typeof(TargetRegionId), region))
                return false;
            liveHitRegion = region;
            liveHitElapsedSeconds = 0f;
            liveHitActive = true;
            return true;
        }

        internal void BeginReplayPresentation()
        {
            if (replayPresentation)
                throw new InvalidOperationException(
                    "Injury-overlay replay presentation is already active.");
            replayOriginalCapabilities = capabilities;
            replayOriginalHitRegion = liveHitRegion;
            replayOriginalHitElapsedSeconds = liveHitElapsedSeconds;
            replayOriginalHitActive = liveHitActive;
            liveHitActive = false;
            replayHitActive = false;
            replayHitProgress = 0f;
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

        internal void PresentReplayHitReaction(
            TargetRegionId? region,
            float normalizedProgress)
        {
            if (!replayPresentation)
                throw new InvalidOperationException(
                    "Begin injury-overlay replay presentation before reactions.");
            if (float.IsNaN(normalizedProgress)
                || float.IsInfinity(normalizedProgress))
                throw new ArgumentOutOfRangeException(
                    nameof(normalizedProgress));
            replayHitActive = region.HasValue;
            replayHitRegion = region ?? TargetRegionId.Torso;
            replayHitProgress = Mathf.Clamp01(normalizedProgress);
        }

        internal void EndReplayPresentation()
        {
            if (!replayPresentation)
                return;
            capabilities = replayOriginalCapabilities;
            replayOriginalCapabilities = null;
            liveHitRegion = replayOriginalHitRegion;
            liveHitElapsedSeconds = replayOriginalHitElapsedSeconds;
            liveHitActive = replayOriginalHitActive;
            replayHitActive = false;
            replayHitProgress = 0f;
            replayPresentation = false;
        }

        private void LateUpdate() => SynchronizeAfterAnimation();

        internal void SynchronizeAfterAnimation()
        {
            if (!replayPresentation && liveHitActive)
            {
                liveHitElapsedSeconds = Mathf.Min(
                    ActorInjuryAnimationOverlayProjector.HitReactionSeconds,
                    liveHitElapsedSeconds + Mathf.Max(
                        0f,
                        Time.unscaledDeltaTime));
                if (liveHitElapsedSeconds >=
                    ActorInjuryAnimationOverlayProjector.HitReactionSeconds)
                    liveHitActive = false;
            }
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
            if (replayHitActive || liveHitActive)
            {
                ActorHitReactionOverlay hit =
                    ActorInjuryAnimationOverlayProjector.ProjectHitReaction(
                        replayPresentation
                            ? replayHitRegion
                            : liveHitRegion,
                        HitReactionProgress);
                ApplyHitReaction(hit);
            }
        }

        private void ApplyHitReaction(ActorHitReactionOverlay hit)
        {
            if (hips != null)
                hips.localRotation *= Quaternion.Euler(
                    hit.BodyPitchDegrees,
                    0f,
                    hit.BodyRollDegrees);
            if (chest != null)
                chest.localRotation *= Quaternion.Euler(
                    hit.BodyPitchDegrees * 0.35f,
                    0f,
                    hit.BodyRollDegrees * 0.35f);
            if (head != null)
                head.localRotation *= Quaternion.Euler(
                    hit.HeadPitchDegrees,
                    0f,
                    hit.HeadRollDegrees);
            if (leftUpperArm != null && hit.LeftArmPitchDegrees != 0f)
                leftUpperArm.localRotation *= Quaternion.Euler(
                    hit.LeftArmPitchDegrees, 0f, 0f);
            if (rightUpperArm != null && hit.RightArmPitchDegrees != 0f)
                rightUpperArm.localRotation *= Quaternion.Euler(
                    hit.RightArmPitchDegrees, 0f, 0f);
            if (leftUpperLeg != null && hit.LeftLegPitchDegrees != 0f)
                leftUpperLeg.localRotation *= Quaternion.Euler(
                    hit.LeftLegPitchDegrees, 0f, 0f);
            if (rightUpperLeg != null && hit.RightLegPitchDegrees != 0f)
                rightUpperLeg.localRotation *= Quaternion.Euler(
                    hit.RightLegPitchDegrees, 0f, 0f);
        }

        private void OnDestroy() => EndReplayPresentation();
    }
}
