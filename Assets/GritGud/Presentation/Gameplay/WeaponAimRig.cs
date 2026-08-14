using System;
using GritGud.Presentation.Actors.Animation;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    /// <summary>
    /// Owns the actor's single ordered post-animation weapon solve. The
    /// Animator supplies the base pose; this component then applies body aim,
    /// weapon aim, recoil, primary-wrist alignment, and support-arm IK in that
    /// order during LateUpdate.
    /// </summary>
    [DefaultExecutionOrder(ActorAnimationUpdateOrder.PostAnimationSolve)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    internal sealed class WeaponAimRig : MonoBehaviour
    {
        private const float DirectionTolerance = 0.000001f;
        private const float ReachTolerance = 0.0001f;
        private const string WeaponAnchorName = "Weapon Anchor";

        private Animator animator;
        private Transform actorRoot;
        private Transform aimBody;
        private Transform leftUpperArm;
        private Transform leftLowerArm;
        private Transform leftHand;
        private Transform rightHand;
        private Transform weaponAnchor;
        private Transform weaponRoot;
        private Transform muzzle;
        private Transform rightGripSocket;
        private Transform leftGripSocket;
        private Transform leftElbowHint;
        private float leftPositionWeight;
        private float leftRotationWeight;
        private float leftHintWeight;
        private float blendSeconds = 0.12f;
        private float blendWeight;
        private float blendTarget;
        private float maximumAimCorrectionDegrees;
        private float maximumBodyAimCorrectionDegrees;
        private float bodyAimDegreesPerSecond;
        private float weaponAimDegreesPerSecond;
        private Vector3 aimPoint;
        private bool hasAimPoint;
        private Quaternion localBodyAimCorrection = Quaternion.identity;
        private Quaternion localAimCorrection = Quaternion.identity;
        private float recoilKickDegrees;
        private float recoilHoldSeconds;
        private float recoilReturnSeconds = 0.01f;
        private float recoilElapsed = -1f;

        internal float SupportBlendWeight => blendWeight;
        internal bool FollowsAnimatedPrimaryGrip => true;
        internal bool HasAimPoint => hasAimPoint;
        internal bool IsRecoiling => recoilElapsed >= 0f;
        internal float AimErrorDegrees { get; private set; }
        internal float RecoilWeight { get; private set; }

        private void Awake() => InitializeRig();

        private void InitializeRig()
        {
            animator ??= GetComponent<Animator>();
            if (weaponAnchor != null || animator == null || !animator.isHuman)
            {
                return;
            }

            aimBody = animator.GetBoneTransform(HumanBodyBones.UpperChest)
                ?? animator.GetBoneTransform(HumanBodyBones.Chest);
            leftUpperArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            leftLowerArm = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            if (aimBody == null || leftUpperArm == null || leftLowerArm == null
                || leftHand == null || rightHand == null)
            {
                return;
            }

            weaponAnchor = aimBody.Find(WeaponAnchorName)
                ?? CreateChild(aimBody, WeaponAnchorName);
        }

        internal void Bind(
            Transform authoritativeActorRoot,
            Transform mountedWeapon,
            Transform weaponMuzzle,
            Transform handSupportSocket,
            Transform elbowHint,
            float handPositionWeight,
            float handRotationWeight,
            float elbowHintWeight,
            float handBlendSeconds,
            float maxAimCorrectionDegrees,
            float maxBodyAimCorrectionDegrees,
            float bodyCorrectionDegreesPerSecond,
            float weaponCorrectionDegreesPerSecond)
        {
            InitializeRig();
            actorRoot = authoritativeActorRoot
                ?? throw new ArgumentNullException(
                    nameof(authoritativeActorRoot));
            weaponRoot = mountedWeapon
                ?? throw new ArgumentNullException(nameof(mountedWeapon));
            muzzle = weaponMuzzle
                ?? throw new ArgumentNullException(nameof(weaponMuzzle));
            WeaponRigSocketSet sockets =
                weaponRoot.GetComponent<WeaponRigSocketSet>();
            rightGripSocket = sockets?.RightHandGrip ?? weaponRoot;
            leftGripSocket = handSupportSocket;
            leftElbowHint = elbowHint;
            leftPositionWeight = Mathf.Clamp01(handPositionWeight);
            leftRotationWeight = Mathf.Clamp01(handRotationWeight);
            leftHintWeight = elbowHint != null
                ? Mathf.Clamp01(elbowHintWeight)
                : 0f;
            blendSeconds = Mathf.Max(0.01f, handBlendSeconds);
            blendTarget = leftGripSocket != null ? 1f : 0f;
            maximumAimCorrectionDegrees = Mathf.Clamp(
                maxAimCorrectionDegrees,
                0f,
                90f);
            maximumBodyAimCorrectionDegrees = Mathf.Clamp(
                maxBodyAimCorrectionDegrees,
                0f,
                90f);
            bodyAimDegreesPerSecond = Mathf.Max(
                0f,
                bodyCorrectionDegreesPerSecond);
            weaponAimDegreesPerSecond = Mathf.Max(
                0f,
                weaponCorrectionDegreesPerSecond);
            localBodyAimCorrection = Quaternion.identity;
            localAimCorrection = Quaternion.identity;
            ClearRecoil();
            AimErrorDegrees = 0f;
            if (weaponAnchor == null)
            {
                enabled = false;
                return;
            }

            weaponRoot.SetParent(weaponAnchor, false);
            weaponRoot.SetLocalPositionAndRotation(
                sockets?.AnchorLocalPosition ?? Vector3.zero,
                sockets?.AnchorLocalRotation ?? Quaternion.identity);
            AlignWeaponToAnimatedPrimaryGrip();
            enabled = true;
        }

        internal void SetAimPoint(Vector3 worldAimPoint)
        {
            aimPoint = worldAimPoint;
            hasAimPoint = true;
        }

        internal float SynchronizeAimForShot(Vector3 worldAimPoint)
        {
            ClearRecoil();
            SetAimPoint(worldAimPoint);
            SynchronizeAfterAnimation(0f, snapAim: true);
            return AimErrorDegrees;
        }

        internal void TriggerRecoil(
            float kickDegrees,
            float holdSeconds,
            float returnSeconds)
        {
            recoilKickDegrees = Mathf.Clamp(kickDegrees, 0f, 30f);
            recoilHoldSeconds = Mathf.Max(0f, holdSeconds);
            recoilReturnSeconds = Mathf.Max(0.01f, returnSeconds);
            recoilElapsed = recoilKickDegrees > 0f ? 0f : -1f;
            RecoilWeight = recoilElapsed >= 0f ? 1f : 0f;
        }

        internal void ClearAimPoint() => hasAimPoint = false;

        internal void ClearAimPointWhenSettled()
        {
            if (!IsRecoiling)
            {
                ClearAimPoint();
            }
        }

        internal void ClearTarget()
        {
            hasAimPoint = false;
            localBodyAimCorrection = Quaternion.identity;
            localAimCorrection = Quaternion.identity;
            ClearRecoil();
            AimErrorDegrees = 0f;
            actorRoot = null;
            weaponRoot = null;
            muzzle = null;
            rightGripSocket = null;
            leftGripSocket = null;
            leftElbowHint = null;
            blendTarget = 0f;
            blendWeight = 0f;
        }

        internal void TickSupportBlend(float deltaTime)
        {
            blendWeight = Mathf.MoveTowards(
                blendWeight,
                blendTarget,
                Mathf.Max(0f, deltaTime) / Mathf.Max(0.01f, blendSeconds));
        }

        private void Update() => TickSupportBlend(Time.deltaTime);

        private void LateUpdate() => SynchronizeAfterAnimation(Time.deltaTime);

        internal void SynchronizeAfterAnimation(float deltaTime) =>
            SynchronizeAfterAnimation(deltaTime, snapAim: false);

        private void SynchronizeAfterAnimation(
            float deltaTime,
            bool snapAim)
        {
            if (weaponRoot == null || weaponAnchor == null)
            {
                return;
            }

            if (IsActionOverrideActive())
            {
                blendWeight = 0f;
                AlignWeaponToAnimatedPrimaryGrip();
                AimErrorDegrees = CalculateAimErrorDegrees();
                return;
            }

            AlignWeaponToAnimatedPrimaryGrip();
            ApplyBodyAimCorrection(Mathf.Max(0f, deltaTime), snapAim);
            AlignWeaponToAnimatedPrimaryGrip();
            ApplyWeaponAimCorrection(Mathf.Max(0f, deltaTime), snapAim);
            ApplyRecoilImpulse(Mathf.Max(0f, deltaTime));
            AlignPrimaryWristRotation();
            SolveSupportHandAfterAnimation();
            AimErrorDegrees = CalculateAimErrorDegrees();
        }

        private bool IsActionOverrideActive()
        {
            if (animator == null)
            {
                return false;
            }

            int layer = animator.GetLayerIndex(
                ActorAnimationParameters.ActionLayerName);
            if (layer < 0)
            {
                return false;
            }
            if (animator.GetLayerWeight(layer) <= 0.001f)
            {
                return false;
            }

            bool currentIsIdle = animator.GetCurrentAnimatorStateInfo(layer)
                .IsName(ActorAnimationParameters.NoActionStateName);
            if (!animator.IsInTransition(layer))
            {
                return !currentIsIdle;
            }

            bool nextIsIdle = animator.GetNextAnimatorStateInfo(layer)
                .IsName(ActorAnimationParameters.NoActionStateName);
            return !currentIsIdle || !nextIsIdle;
        }

        private void ApplyRecoilImpulse(float deltaTime)
        {
            if (recoilElapsed < 0f || recoilKickDegrees <= 0f ||
                weaponAnchor == null || muzzle == null)
            {
                RecoilWeight = 0f;
                return;
            }

            RecoilWeight = EvaluateRecoilWeight(
                recoilElapsed,
                recoilHoldSeconds,
                recoilReturnSeconds);
            Vector3 actorUp = actorRoot != null
                ? actorRoot.up
                : transform.up;
            Vector3 fallbackRight = actorRoot != null
                ? actorRoot.right
                : transform.right;
            Vector3 pitchAxis = CalculateRecoilPitchAxis(
                muzzle.forward,
                actorUp,
                fallbackRight);
            Quaternion recoilRotation = Quaternion.AngleAxis(
                -recoilKickDegrees * RecoilWeight,
                pitchAxis);
            Vector3 gripPivot = rightHand != null
                ? rightHand.position
                : rightGripSocket != null
                    ? rightGripSocket.position
                    : weaponAnchor.position;
            weaponAnchor.position = gripPivot + recoilRotation
                * (weaponAnchor.position - gripPivot);
            weaponAnchor.rotation = recoilRotation * weaponAnchor.rotation;
            recoilElapsed += deltaTime;
            if (recoilElapsed >= recoilHoldSeconds + recoilReturnSeconds)
            {
                recoilElapsed = -1f;
            }
        }

        internal static float EvaluateRecoilWeight(
            float elapsedSeconds,
            float holdSeconds,
            float returnSeconds)
        {
            float recoveryElapsed = Mathf.Max(
                0f,
                elapsedSeconds - Mathf.Max(0f, holdSeconds));
            float progress = Mathf.Clamp01(
                recoveryElapsed /
                Mathf.Max(0.01f, returnSeconds));
            return 1f - Mathf.SmoothStep(0f, 1f, progress);
        }

        internal static Vector3 CalculateRecoilPitchAxis(
            Vector3 barrelDirection,
            Vector3 actorUp,
            Vector3 fallbackRight)
        {
            Vector3 normalizedBarrel = barrelDirection.sqrMagnitude >
                DirectionTolerance
                ? barrelDirection.normalized
                : Vector3.forward;
            Vector3 pitchAxis = Vector3.Cross(
                actorUp.sqrMagnitude > DirectionTolerance
                    ? actorUp.normalized
                    : Vector3.up,
                normalizedBarrel);
            if (pitchAxis.sqrMagnitude <= DirectionTolerance)
            {
                pitchAxis = Vector3.ProjectOnPlane(
                    fallbackRight,
                    normalizedBarrel);
            }

            if (pitchAxis.sqrMagnitude <= DirectionTolerance)
            {
                pitchAxis = Vector3.Cross(Vector3.forward, normalizedBarrel);
            }

            if (pitchAxis.sqrMagnitude <= DirectionTolerance)
            {
                pitchAxis = Vector3.right;
            }

            return pitchAxis.normalized;
        }

        private void ClearRecoil()
        {
            recoilKickDegrees = 0f;
            recoilHoldSeconds = 0f;
            recoilReturnSeconds = 0.01f;
            recoilElapsed = -1f;
            RecoilWeight = 0f;
        }

        private void AlignWeaponToAnimatedPrimaryGrip()
        {
            if (rightGripSocket == null || rightHand == null)
            {
                return;
            }

            Pose gripInAnchor = GetLocalPose(weaponAnchor, rightGripSocket);
            Quaternion anchorRotation = rightHand.rotation
                * Quaternion.Inverse(gripInAnchor.rotation);
            weaponAnchor.SetPositionAndRotation(
                rightHand.position - anchorRotation * gripInAnchor.position,
                anchorRotation);
        }

        private void ApplyBodyAimCorrection(float deltaTime, bool snapAim)
        {
            Quaternion baseRotation = aimBody.rotation;
            Quaternion desiredLocalCorrection = Quaternion.identity;
            if (hasAimPoint && muzzle != null &&
                maximumBodyAimCorrectionDegrees > 0f)
            {
                Vector3 direction = aimPoint - muzzle.position;
                if (direction.sqrMagnitude > DirectionTolerance)
                {
                    float totalDegrees = Vector3.Angle(
                        muzzle.forward,
                        direction);
                    float bodyDegrees = Mathf.Clamp(
                        totalDegrees - maximumAimCorrectionDegrees,
                        0f,
                        maximumBodyAimCorrectionDegrees);
                    Quaternion worldCorrection =
                        WeaponAimProjector.CalculateCorrection(
                            muzzle.forward,
                            direction,
                            bodyDegrees);
                    desiredLocalCorrection = Quaternion.Inverse(baseRotation)
                        * worldCorrection
                        * baseRotation;
                }
            }

            localBodyAimCorrection = snapAim
                ? desiredLocalCorrection
                : Quaternion.RotateTowards(
                    localBodyAimCorrection,
                    desiredLocalCorrection,
                    bodyAimDegreesPerSecond * deltaTime);
            aimBody.rotation = baseRotation * localBodyAimCorrection;
        }

        private void ApplyWeaponAimCorrection(float deltaTime, bool snapAim)
        {
            Quaternion baseRotation = weaponAnchor.rotation;
            Quaternion desiredLocalCorrection = Quaternion.identity;
            if (hasAimPoint && muzzle != null)
            {
                Vector3 direction = aimPoint - muzzle.position;
                if (direction.sqrMagnitude > DirectionTolerance)
                {
                    // The correction is recomputed from the animation-authored
                    // base pose every frame. Clamping here therefore cannot
                    // accumulate beyond the authored limit, and an aim point
                    // outside the cone still receives the closest valid pose.
                    Quaternion worldCorrection =
                        WeaponAimProjector.CalculateCorrection(
                            muzzle.forward,
                            direction,
                            maximumAimCorrectionDegrees);
                    desiredLocalCorrection = Quaternion.Inverse(baseRotation)
                        * worldCorrection
                        * baseRotation;
                }
            }

            localAimCorrection = snapAim
                ? desiredLocalCorrection
                : Quaternion.RotateTowards(
                    localAimCorrection,
                    desiredLocalCorrection,
                    weaponAimDegreesPerSecond * deltaTime);
            Quaternion worldAppliedCorrection = baseRotation
                * localAimCorrection
                * Quaternion.Inverse(baseRotation);
            Vector3 gripPivot = rightHand != null
                ? rightHand.position
                : rightGripSocket.position;
            weaponAnchor.position = gripPivot + worldAppliedCorrection
                * (weaponAnchor.position - gripPivot);
            weaponAnchor.rotation = baseRotation * localAimCorrection;
        }

        private float CalculateAimErrorDegrees()
        {
            if (!hasAimPoint || muzzle == null)
            {
                return 0f;
            }

            Vector3 direction = aimPoint - muzzle.position;
            return direction.sqrMagnitude > DirectionTolerance
                ? Vector3.Angle(muzzle.forward, direction)
                : 0f;
        }

        private void AlignPrimaryWristRotation()
        {
            if (rightHand != null && rightGripSocket != null)
            {
                rightHand.rotation = rightGripSocket.rotation;
            }
        }

        private void SolveSupportHandAfterAnimation()
        {
            float positionWeight = weaponRoot != null && leftGripSocket != null
                ? blendWeight * leftPositionWeight
                : 0f;
            if (positionWeight <= 0f || leftUpperArm == null
                || leftLowerArm == null || leftHand == null)
            {
                return;
            }

            Vector3 rootPosition = leftUpperArm.position;
            Vector3 midPosition = leftLowerArm.position;
            Vector3 tipPosition = leftHand.position;
            Vector3 targetPosition = Vector3.Lerp(
                tipPosition,
                leftGripSocket.position,
                positionWeight);
            float upperLength = Vector3.Distance(rootPosition, midPosition);
            float lowerLength = Vector3.Distance(midPosition, tipPosition);
            Vector3 toTarget = targetPosition - rootPosition;
            float targetDistance = toTarget.magnitude;
            if (upperLength <= DirectionTolerance
                || lowerLength <= DirectionTolerance
                || targetDistance <= DirectionTolerance)
            {
                return;
            }

            Vector3 direction = toTarget / targetDistance;
            float maximumReach = upperLength + lowerLength - ReachTolerance;
            float distance = Mathf.Clamp(
                targetDistance,
                Mathf.Abs(upperLength - lowerLength) + ReachTolerance,
                maximumReach);
            Vector3 reachableTarget = rootPosition + direction * distance;
            float along = (upperLength * upperLength - lowerLength * lowerLength
                + distance * distance) / (2f * distance);
            float height = Mathf.Sqrt(Mathf.Max(
                0f,
                upperLength * upperLength - along * along));
            Vector3 animatedBend = Vector3.ProjectOnPlane(
                midPosition - rootPosition,
                direction);
            Vector3 hintedBend = leftElbowHint != null
                ? Vector3.ProjectOnPlane(
                    leftElbowHint.position - rootPosition,
                    direction)
                : animatedBend;
            Vector3 bendDirection = hintedBend.sqrMagnitude > DirectionTolerance
                ? Vector3.Slerp(
                    animatedBend.sqrMagnitude > DirectionTolerance
                        ? animatedBend.normalized
                        : hintedBend.normalized,
                    hintedBend.normalized,
                    leftHintWeight)
                : animatedBend;
            if (bendDirection.sqrMagnitude <= DirectionTolerance)
            {
                bendDirection = Vector3.Cross(direction, transform.up);
            }

            bendDirection.Normalize();
            Vector3 solvedMidPosition = rootPosition + direction * along
                + bendDirection * height;
            RotateBoneToward(
                leftUpperArm,
                midPosition - rootPosition,
                solvedMidPosition - rootPosition);
            RotateBoneToward(
                leftLowerArm,
                leftHand.position - leftLowerArm.position,
                reachableTarget - leftLowerArm.position);
            if (leftRotationWeight > 0f)
            {
                leftHand.rotation = Quaternion.Slerp(
                    leftHand.rotation,
                    leftGripSocket.rotation,
                    blendWeight * leftRotationWeight);
            }
        }

        private static void RotateBoneToward(
            Transform bone,
            Vector3 currentDirection,
            Vector3 desiredDirection)
        {
            if (currentDirection.sqrMagnitude <= DirectionTolerance
                || desiredDirection.sqrMagnitude <= DirectionTolerance)
            {
                return;
            }

            bone.rotation = Quaternion.FromToRotation(
                currentDirection,
                desiredDirection) * bone.rotation;
        }

        private static Transform CreateChild(Transform parent, string name)
        {
            Transform child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }

        private static Pose GetLocalPose(Transform root, Transform child) =>
            new(
                root.InverseTransformPoint(child.position),
                Quaternion.Inverse(root.rotation) * child.rotation);
    }
}
