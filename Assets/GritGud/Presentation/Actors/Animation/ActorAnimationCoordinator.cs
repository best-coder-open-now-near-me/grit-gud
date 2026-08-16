using System;
using GritGud.Domain.Gameplay;
using UnityEngine;

namespace GritGud.Presentation.Actors.Animation
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AnimatorDriver))]
    public sealed class ActorAnimationCoordinator : MonoBehaviour
    {
        [SerializeField]
        private AnimatorDriver animatorDriver;

        [SerializeField]
        private ActorAnimationProfile profile;

        private readonly ActorLocomotionAnimationChannel locomotionChannel = new();
        private readonly WeaponAnimationChannel weaponChannel = new();
        private readonly RecoilAnimationChannel recoilChannel = new();
        private ActorStance currentStance = ActorStance.Standing;
        private ActorAnimatorPresentationSnapshot replayAnimatorSnapshot;
        private ActorStance replayOriginalStance;
        private string replayOriginalWeaponAnimationSetId;
        private ActorAnimationAction? replayOriginalLastRequestedAction;
        private int replayOriginalActionSequence;

        public AnimatorDriver Driver => animatorDriver;

        public Animator TargetAnimator => animatorDriver?.TargetAnimator;

        public ActorAnimationProfile Profile => profile;

        public ActorStance CurrentStance => currentStance;

        public string CurrentWeaponAnimationSetId =>
            weaponChannel.CurrentAnimationSetId;

        public ActorAnimationAction? LastRequestedAction { get; private set; }

        public int ActionSequence { get; private set; }

        internal bool IsPresentingReplay => replayAnimatorSnapshot != null;

        internal ActorAnimationAction? ReplayAction { get; private set; }

        internal float ReplayActionProgress { get; private set; }

        private void Awake()
        {
            animatorDriver ??= GetComponent<AnimatorDriver>();
            ConfigureDriver();
        }

        public void Bind(
            Animator animator,
            ActorAnimationProfile animationProfile)
        {
            profile = animationProfile ?? throw new ArgumentNullException(
                nameof(animationProfile));
            animatorDriver ??= GetComponent<AnimatorDriver>();
            animatorDriver.Bind(animator, profile.AnimatorController);
            ActorAnimationContractValidator.Validate(profile, animator);
            locomotionChannel.Reset();
            ResetActionLayer();
        }

        public void PresentFrame(ActorAnimationFrame frame, float deltaTime)
        {
            RequireProfile();
            currentStance = frame.Stance;
            animatorDriver.SetInteger(
                ActorAnimationParameters.Stance,
                (int)currentStance);
            locomotionChannel.Present(
                frame,
                profile,
                deltaTime,
                animatorDriver);
        }

        public void PresentStance(ActorStance stance)
        {
            if (!Enum.IsDefined(typeof(ActorStance), stance))
            {
                throw new ArgumentOutOfRangeException(nameof(stance));
            }

            currentStance = stance;
            animatorDriver?.SetInteger(
                ActorAnimationParameters.Stance,
                (int)stance);
        }

        public void PresentInteraction() =>
            TryRequestAction(ActorAnimationAction.Interact);

        public void PresentWeaponPose(string animationSetId)
        {
            RequireProfile();
            recoilChannel.Reset(animatorDriver);
            if (IsPresentingReplay)
                weaponChannel.PresentReplayPose(
                    profile,
                    animatorDriver,
                    animationSetId);
            else
                weaponChannel.PresentPose(
                    profile,
                    animatorDriver,
                    animationSetId);
        }

        internal bool CanPresentWeaponPose(string animationSetId)
        {
            return profile != null && animatorDriver != null &&
                animatorDriver.CanWrite &&
                weaponChannel.CanPresentPose(
                    profile,
                    animatorDriver,
                    animationSetId);
        }

        public void PresentWeaponFire() => TryPresentWeaponFire();

        public bool TryPresentWeaponFire() =>
            TryRequestAction(ActorAnimationAction.WeaponFire);

        public bool TryPresentThrow() =>
            TryRequestAction(ActorAnimationAction.Throw);

        public bool TryRequestAction(ActorAnimationAction action)
        {
            if (IsPresentingReplay)
                return false;
            if (profile == null || animatorDriver == null ||
                !animatorDriver.CanWrite)
            {
                return false;
            }

            bool presented = action == ActorAnimationAction.WeaponFire
                ? recoilChannel.TryPresent(
                    profile,
                    animatorDriver,
                    weaponChannel.CurrentAnimationSetId)
                : TryPresentBoundAction(action);
            if (!presented)
            {
                return false;
            }

            LastRequestedAction = action;
            ActionSequence++;
            return true;
        }

        internal void BeginReplayPresentation()
        {
            if (IsPresentingReplay)
            {
                throw new InvalidOperationException(
                    "Actor replay presentation is already active.");
            }
            RequireProfile();
            replayAnimatorSnapshot =
                ActorAnimatorPresentationSnapshot.Capture(TargetAnimator);
            replayOriginalStance = currentStance;
            replayOriginalWeaponAnimationSetId =
                weaponChannel.CurrentAnimationSetId;
            replayOriginalLastRequestedAction = LastRequestedAction;
            replayOriginalActionSequence = ActionSequence;
            ReplayAction = null;
            ReplayActionProgress = 0f;
            if (TargetAnimator != null)
            {
                TargetAnimator.enabled = true;
                TargetAnimator.fireEvents = false;
                TargetAnimator.speed = 0f;
            }
            ResetReplayActionLayers();
        }

        internal void PresentReplayAction(
            ActorStance stance,
            ActorAnimationAction? action,
            float normalizedProgress)
        {
            if (!IsPresentingReplay)
            {
                throw new InvalidOperationException(
                    "Begin actor replay presentation before projecting actions.");
            }
            if (float.IsNaN(normalizedProgress)
                || float.IsInfinity(normalizedProgress))
                throw new ArgumentOutOfRangeException(nameof(normalizedProgress));
            PresentStance(stance);
            ResetReplayActionLayers();
            ReplayAction = action;
            ReplayActionProgress = Mathf.Clamp01(normalizedProgress);
            if (action == ActorAnimationAction.WeaponFire)
            {
                PresentReplayWeaponFire(ReplayActionProgress);
            }
            else if (action.HasValue
                && profile.TryGetActionBinding(
                    action.Value,
                    out ActorAnimationActionBinding binding)
                && binding.UsesState)
            {
                animatorDriver.SetLayerWeight(binding.LayerName, 1f);
                animatorDriver.PlayState(
                    binding.LayerName,
                    Animator.StringToHash(binding.StateName),
                    ReplayActionProgress);
            }
            TargetAnimator?.Update(0f);
        }

        internal void EndReplayPresentation()
        {
            if (!IsPresentingReplay)
                return;
            ActorAnimatorPresentationSnapshot snapshot =
                replayAnimatorSnapshot;
            replayAnimatorSnapshot = null;
            currentStance = replayOriginalStance;
            weaponChannel.RestoreCurrentAnimationSet(
                replayOriginalWeaponAnimationSetId);
            LastRequestedAction = replayOriginalLastRequestedAction;
            ActionSequence = replayOriginalActionSequence;
            ReplayAction = null;
            ReplayActionProgress = 0f;
            snapshot.Restore(TargetAnimator);
        }

        public void PresentIncapacitation(
            Quaternion visualLocalRotation,
            Vector3 visualLocalOffset)
        {
            if (!TryRequestAction(ActorAnimationAction.Incapacitate))
            {
                animatorDriver?.DisableAndOffset(
                    visualLocalRotation,
                    visualLocalOffset);
            }
        }

        private void ConfigureDriver()
        {
            if (animatorDriver == null || profile == null ||
                profile.AnimatorController == null)
            {
                return;
            }

            Animator animator = animatorDriver.TargetAnimator
                ?? GetComponentInChildren<Animator>();
            if (animator != null)
            {
                animatorDriver.Bind(animator, profile.AnimatorController);
                ActorAnimationContractValidator.Validate(profile, animator);
            }

            locomotionChannel.Reset();
            ResetActionLayer();
        }

        private bool TryPresentBoundAction(ActorAnimationAction action)
        {
            if (!profile.TryGetActionBinding(
                    action,
                    out ActorAnimationActionBinding binding))
            {
                return false;
            }

            if (binding.UsesTrigger)
            {
                animatorDriver.PulseTrigger(binding.TriggerParameterName);
            }

            if (binding.UsesState)
            {
                animatorDriver.CrossFadeState(
                    binding.LayerName,
                    binding.StateName,
                    binding.TransitionSeconds);
                if (string.Equals(
                        binding.LayerName,
                        ActorAnimationParameters.ActionLayerName,
                        StringComparison.Ordinal))
                {
                    animatorDriver.SetLayerWeight(binding.LayerName, 1f);
                }
            }

            return binding.UsesTrigger || binding.UsesState;
        }

        private void ResetActionLayer()
        {
            if (animatorDriver != null && animatorDriver.HasLayer(
                    ActorAnimationParameters.ActionLayerName))
            {
                animatorDriver.SetLayerWeight(
                    ActorAnimationParameters.ActionLayerName,
                    0f);
            }
        }

        private void ResetReplayActionLayers()
        {
            if (animatorDriver == null)
                return;
            string recoilLayer = ActorAnimationParameters.RecoilLayerName;
            if (animatorDriver.HasLayer(recoilLayer))
            {
                animatorDriver.PlayState(
                    recoilLayer,
                    ActorAnimationParameters.NoRecoilState,
                    0f);
                animatorDriver.SetLayerWeight(recoilLayer, 0f);
            }
            string actionLayer = ActorAnimationParameters.ActionLayerName;
            if (animatorDriver.HasLayer(actionLayer))
            {
                animatorDriver.PlayState(
                    actionLayer,
                    ActorAnimationParameters.NoActionState,
                    0f);
                animatorDriver.SetLayerWeight(actionLayer, 0f);
            }
        }

        private void PresentReplayWeaponFire(float normalizedProgress)
        {
            ActorWeaponAnimationSet animationSet =
                profile.GetWeaponAnimationSet(
                    weaponChannel.CurrentAnimationSetId);
            if (string.IsNullOrWhiteSpace(animationSet.RecoilStateName))
                return;
            string layer = ActorAnimationParameters.RecoilLayerName;
            animatorDriver.SetLayerWeight(
                layer,
                animationSet.RecoilLayerWeight);
            animatorDriver.PlayState(
                layer,
                Animator.StringToHash(animationSet.RecoilStateName),
                normalizedProgress);
        }

        private void RequireProfile()
        {
            if (profile == null)
            {
                throw new InvalidOperationException(
                    "An actor animation profile must be bound before presenting animation.");
            }

            if (animatorDriver == null)
            {
                throw new InvalidOperationException(
                    "An AnimatorDriver must be bound before presenting animation.");
            }
        }
    }
}
