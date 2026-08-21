using System;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Presentation.Actors.Animation;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    internal sealed class WeaponAimPresenter : MonoBehaviour
    {
        private GameplaySession session;
        private TargetAcquisitionPresenter acquisition;
        private Transform actorTransform;
        private string actorId;
        private WeaponPresentationDefinition weaponDefinition;
        private WeaponAimRig rig;
        private ActorAnimationProfile animationProfile;
        private bool replayPresentation;

        internal bool HasRig => rig != null;

        internal void Bind(
            GameplaySession gameplaySession,
            Transform authoritativeActorTransform,
            string authoritativeActorId,
            Animator animator,
            TargetAcquisitionPresenter targetAcquisition,
            ActorAnimationProfile actorAnimationProfile)
        {
            Unbind();
            session = gameplaySession ?? throw new ArgumentNullException(
                nameof(gameplaySession));
            actorTransform = authoritativeActorTransform ??
                throw new ArgumentNullException(
                    nameof(authoritativeActorTransform));
            if (string.IsNullOrWhiteSpace(authoritativeActorId))
            {
                throw new ArgumentException(
                    "Weapon-aim actor identifiers cannot be empty.",
                    nameof(authoritativeActorId));
            }

            actorId = authoritativeActorId;
            acquisition = targetAcquisition;
            animationProfile = actorAnimationProfile ??
                throw new ArgumentNullException(nameof(actorAnimationProfile));
            if (animator != null && animator.isHuman)
            {
                rig = animator.GetComponent<WeaponAimRig>()
                    ?? animator.gameObject.AddComponent<WeaponAimRig>();
            }

            enabled = true;
        }

        internal void SetLocalControl(
            bool controlledLocally,
            TargetAcquisitionPresenter targetAcquisition)
        {
            acquisition = controlledLocally ? targetAcquisition : null;
            if (!controlledLocally)
            {
                rig?.ClearAimPoint();
            }
        }

        internal void BindWeapon(
            WeaponPresentationDefinition definition,
            Transform mountedWeapon,
            WeaponRigSocketSet sockets)
        {
            weaponDefinition = definition ?? throw new ArgumentNullException(
                nameof(definition));
            if (rig == null)
            {
                return;
            }

            if (sockets == null)
            {
                throw new ArgumentNullException(nameof(sockets));
            }

            rig.Bind(
                actorTransform,
                mountedWeapon,
                sockets.Muzzle,
                sockets.SupportHand,
                sockets.SupportElbowHint,
                sockets.SupportPositionWeight,
                sockets.SupportRotationWeight,
                sockets.SupportElbowHintWeight,
                sockets.SupportBlendSeconds,
                weaponDefinition.MaximumAimCorrectionDegrees,
                animationProfile.MaximumBodyAimCorrectionDegrees,
                animationProfile.BodyAimDegreesPerSecond,
                animationProfile.WeaponAimDegreesPerSecond);
        }

        internal void ClearWeapon()
        {
            weaponDefinition = null;
            rig?.ClearTarget();
        }

        internal void BeginReplayPresentation()
        {
            if (replayPresentation)
            {
                throw new InvalidOperationException(
                    "Weapon-aim replay presentation is already active.");
            }

            replayPresentation = true;
            rig?.ClearAimPoint();
        }

        internal void EndReplayPresentation()
        {
            replayPresentation = false;
            rig?.ClearAimPoint();
        }

        internal float SynchronizeForShot(Vector3 destination)
        {
            SynchronizeAuthoritativeFacing();
            return rig?.SynchronizeAimForShot(destination) ?? 0f;
        }

        internal void PresentRecoil(ActorWeaponAnimationSet animationSet)
        {
            if (animationSet == null)
            {
                throw new ArgumentNullException(nameof(animationSet));
            }

            rig?.TriggerRecoil(
                animationSet.RecoilKickDegrees,
                animationSet.RecoilHoldSeconds,
                animationSet.RecoilReturnSeconds);
        }

        internal void SynchronizeAuthoritativeFacing()
        {
            if (session == null || actorTransform == null || actorId == null)
            {
                return;
            }

            GameplayActorPose pose = session.GetActor(actorId).Pose;
            actorTransform.rotation = Quaternion.Euler(
                0f,
                pose.FacingDegrees,
                0f);
        }

        internal void Tick(float deltaTime)
        {
            if (replayPresentation)
                return;

            if (rig?.IsRecoiling == true
                && acquisition?.WeaponTargetingActive != true)
            {
                // Confirmation closes targeting before recoil playback. Keep
                // the solved shot direction until recoil has returned, then
                // resume the ordinary pointer preview.
                return;
            }

            if (weaponDefinition == null
                || acquisition == null
                || rig == null
                || !acquisition.WeaponTargetingActive
                || !acquisition.TryGetPresentationAimPoint(
                    out Vector3 aimPoint)
                || weaponDefinition.AttackPresentation ==
                    WeaponAttackPresentationKind.ContactStrike)
            {
                rig?.ClearAimPointWhenSettled();
                return;
            }

            rig.SetAimPoint(aimPoint);
            RotateActorToward(aimPoint, deltaTime);
        }

        internal void Unbind()
        {
            ClearWeapon();
            session = null;
            acquisition = null;
            actorTransform = null;
            actorId = null;
            rig = null;
            animationProfile = null;
            replayPresentation = false;
            enabled = false;
        }

        private void Update() => Tick(Time.deltaTime);

        private void RotateActorToward(Vector3 aimPoint, float deltaTime)
        {
            Vector3 direction = Vector3.ProjectOnPlane(
                aimPoint - actorTransform.position,
                Vector3.up);
            if (direction.sqrMagnitude <= 0.000001f)
            {
                return;
            }

            Quaternion targetRotation = Quaternion.LookRotation(
                direction.normalized,
                Vector3.up);
            actorTransform.rotation = Quaternion.RotateTowards(
                actorTransform.rotation,
                targetRotation,
                animationProfile.ActorAimTurnDegreesPerSecond *
                    Mathf.Max(0f, deltaTime));
        }
    }
}
