using System;
using System.Collections.Generic;
using UnityEngine;

namespace GritGud.Presentation.Actors.Animation
{
    [Serializable]
    public sealed class ActorTurnInPlaceSettings
    {
        [SerializeField, Min(0f)]
        private float activationDegreesPerSecond = 18f;

        [SerializeField, Min(0f)]
        private float sustainDegreesPerSecond = 6f;

        [SerializeField, Range(0.01f, 1f)]
        private float minimumActiveBlend = 0.65f;

        [SerializeField, Min(0f)]
        private float releaseDelaySeconds = 0.12f;

        [SerializeField, Min(0.01f)]
        private float releaseSeconds = 0.16f;

        [SerializeField, Min(0f)]
        private float maximumMovementSpeed = 0.1f;

        [SerializeField, Range(0f, 1f)]
        private float maximumLayerWeight = 1f;

        [SerializeField, Range(0.01f, 1f)]
        private float maximumPoseBlend = 0.75f;

        [SerializeField, Min(0.01f)]
        private float playbackSpeed = 0.65f;

        public ActorTurnInPlaceSettings()
        {
        }

        public ActorTurnInPlaceSettings(
            float activationSpeed,
            float sustainSpeed,
            float activeBlendFloor,
            float releaseDelay,
            float releaseDuration,
            float stationarySpeedLimit,
            float layerWeight,
            float poseBlendLimit,
            float statePlaybackSpeed)
        {
            activationDegreesPerSecond = Mathf.Max(0f, activationSpeed);
            sustainDegreesPerSecond = Mathf.Clamp(
                sustainSpeed,
                0f,
                activationDegreesPerSecond);
            minimumActiveBlend = Mathf.Clamp(activeBlendFloor, 0.01f, 1f);
            releaseDelaySeconds = Mathf.Max(0f, releaseDelay);
            releaseSeconds = Mathf.Max(0.01f, releaseDuration);
            maximumMovementSpeed = Mathf.Max(0f, stationarySpeedLimit);
            maximumLayerWeight = Mathf.Clamp01(layerWeight);
            maximumPoseBlend = Mathf.Clamp(poseBlendLimit, 0.01f, 1f);
            playbackSpeed = Mathf.Max(0.01f, statePlaybackSpeed);
        }

        public float ActivationDegreesPerSecond =>
            Mathf.Max(0f, activationDegreesPerSecond);

        public float SustainDegreesPerSecond => Mathf.Clamp(
            sustainDegreesPerSecond,
            0f,
            ActivationDegreesPerSecond);

        public float MinimumActiveBlend =>
            Mathf.Clamp(minimumActiveBlend, 0.01f, 1f);

        public float ReleaseDelaySeconds =>
            Mathf.Max(0f, releaseDelaySeconds);

        public float ReleaseSeconds => Mathf.Max(0.01f, releaseSeconds);

        public float MaximumMovementSpeed =>
            Mathf.Max(0f, maximumMovementSpeed);

        public float MaximumLayerWeight =>
            Mathf.Clamp01(maximumLayerWeight);

        public float MaximumPoseBlend =>
            Mathf.Clamp(maximumPoseBlend, 0.01f, 1f);

        public float PlaybackSpeed => Mathf.Max(0.01f, playbackSpeed);
    }

    [Serializable]
    public sealed class ActorWeaponAnimationSet
    {
        [SerializeField]
        private string id = ActorAnimationPoseIds.Empty;

        [SerializeField]
        private string poseStateName = ActorAnimationParameters.EmptyHandsStateName;

        [SerializeField]
        private string recoilStateName = string.Empty;

        [SerializeField, Min(0.01f)]
        private float recoilPlaybackSpeed = 1f;

        [SerializeField]
        private int animatorPoseValue;

        [SerializeField, Range(0f, 1f)]
        private float poseLayerWeight = 0.68f;

        [SerializeField, Min(0f)]
        private float poseTransitionSeconds = 0.12f;

        [SerializeField, Range(0f, 1f)]
        private float recoilLayerWeight = 1f;

        [SerializeField, Min(0f)]
        private float recoilTransitionSeconds = 0.025f;

        [SerializeField, Range(0f, 30f)]
        private float recoilKickDegrees = 8f;

        [SerializeField, Min(0f)]
        private float recoilHoldSeconds = 0.06f;

        [SerializeField, Min(0.01f)]
        private float recoilReturnSeconds = 0.18f;

        public ActorWeaponAnimationSet(
            string animationSetId,
            string animatorPoseStateName,
            string animatorRecoilStateName,
            int poseParameterValue,
            float animatorPoseLayerWeight,
            float poseTransitionDurationSeconds,
            float animatorRecoilLayerWeight,
            float recoilTransitionDurationSeconds,
            float proceduralRecoilKickDegrees,
            float proceduralRecoilHoldSeconds,
            float proceduralRecoilReturnSeconds,
            float animatorRecoilPlaybackSpeed = 1f)
        {
            id = animationSetId?.Trim() ?? string.Empty;
            poseStateName = animatorPoseStateName?.Trim() ?? string.Empty;
            recoilStateName = animatorRecoilStateName?.Trim() ?? string.Empty;
            recoilPlaybackSpeed = Mathf.Max(
                0.01f,
                animatorRecoilPlaybackSpeed);
            animatorPoseValue = poseParameterValue;
            poseLayerWeight = Mathf.Clamp01(animatorPoseLayerWeight);
            poseTransitionSeconds = Mathf.Max(
                0f,
                poseTransitionDurationSeconds);
            recoilLayerWeight = Mathf.Clamp01(animatorRecoilLayerWeight);
            recoilTransitionSeconds = Mathf.Max(
                0f,
                recoilTransitionDurationSeconds);
            recoilKickDegrees = Mathf.Clamp(
                proceduralRecoilKickDegrees,
                0f,
                30f);
            recoilHoldSeconds = Mathf.Max(
                0f,
                proceduralRecoilHoldSeconds);
            recoilReturnSeconds = Mathf.Max(
                0.01f,
                proceduralRecoilReturnSeconds);
        }

        public string Id => id;

        public string PoseStateName => poseStateName;

        public string RecoilStateName => recoilStateName;

        public float RecoilPlaybackSpeed =>
            Mathf.Max(0.01f, recoilPlaybackSpeed);

        public int AnimatorPoseValue => animatorPoseValue;

        public float PoseLayerWeight => Mathf.Clamp01(poseLayerWeight);

        public float PoseTransitionSeconds =>
            Mathf.Max(0f, poseTransitionSeconds);

        public float RecoilLayerWeight => Mathf.Clamp01(recoilLayerWeight);

        public float RecoilTransitionSeconds =>
            Mathf.Max(0f, recoilTransitionSeconds);

        public float RecoilKickDegrees => Mathf.Clamp(
            recoilKickDegrees,
            0f,
            30f);

        public float RecoilHoldSeconds => Mathf.Max(0f, recoilHoldSeconds);

        public float RecoilReturnSeconds =>
            Mathf.Max(0.01f, recoilReturnSeconds);
    }

    [Serializable]
    public sealed class ActorAnimationActionBinding
    {
        [SerializeField]
        private ActorAnimationAction action;

        [SerializeField]
        private string triggerParameterName = string.Empty;

        [SerializeField]
        private string layerName = string.Empty;

        [SerializeField]
        private string stateName = string.Empty;

        [SerializeField, Min(0f)]
        private float transitionSeconds = 0.1f;

        public ActorAnimationActionBinding(
            ActorAnimationAction animationAction,
            string triggerParameter,
            string animatorLayerName = null,
            string animatorStateName = null,
            float transitionDurationSeconds = 0.1f)
        {
            action = animationAction;
            triggerParameterName = triggerParameter?.Trim() ?? string.Empty;
            layerName = animatorLayerName?.Trim() ?? string.Empty;
            stateName = animatorStateName?.Trim() ?? string.Empty;
            transitionSeconds = Mathf.Max(0f, transitionDurationSeconds);
        }

        public ActorAnimationAction Action => action;

        public string TriggerParameterName => triggerParameterName;

        public string LayerName => layerName;

        public string StateName => stateName;

        public float TransitionSeconds => Mathf.Max(0f, transitionSeconds);

        public bool UsesTrigger => !string.IsNullOrWhiteSpace(triggerParameterName);

        public bool UsesState => !string.IsNullOrWhiteSpace(stateName);
    }

    [CreateAssetMenu(
        fileName = "ActorAnimationProfile",
        menuName = "Grit Gud/Actors/Animation Profile")]
    public sealed class ActorAnimationProfile : ScriptableObject
    {
        private const float MinimumReferenceValue = 0.01f;

        [SerializeField]
        private RuntimeAnimatorController animatorController = null;

        [SerializeField, Min(MinimumReferenceValue)]
        private float locomotionReferenceSpeed = 5f;

        [SerializeField, Min(MinimumReferenceValue)]
        private float turnReferenceDegreesPerSecond = 360f;

        [SerializeField, Min(0f)]
        private float parameterDampTime = 0.1f;

        [SerializeField, Range(0f, 90f)]
        private float maximumBodyAimCorrectionDegrees = 48f;

        [SerializeField, Min(0f)]
        private float bodyAimDegreesPerSecond = 300f;

        [SerializeField, Min(0f)]
        private float actorAimTurnDegreesPerSecond = 300f;

        [SerializeField, Min(0f)]
        private float weaponAimDegreesPerSecond = 240f;

        [SerializeField, Range(0f, 10f)]
        private float shotAlignmentToleranceDegrees = 1f;

        [SerializeField]
        private ActorTurnInPlaceSettings turnInPlace = new();

        [SerializeField, Range(0f, 1f)]
        private float recoilExitNormalizedTime = 0.9f;

        [SerializeField, Min(0f)]
        private float recoilReturnTransitionSeconds = 0.12f;

        [SerializeField]
        private ActorWeaponAnimationSet[] weaponAnimationSets =
            Array.Empty<ActorWeaponAnimationSet>();

        [SerializeField]
        private ActorAnimationActionBinding[] actionBindings =
            Array.Empty<ActorAnimationActionBinding>();

        public RuntimeAnimatorController AnimatorController => animatorController;

        public float LocomotionReferenceSpeed =>
            Mathf.Max(MinimumReferenceValue, locomotionReferenceSpeed);

        public float TurnReferenceDegreesPerSecond =>
            Mathf.Max(MinimumReferenceValue, turnReferenceDegreesPerSecond);

        public float ParameterDampTime => Mathf.Max(0f, parameterDampTime);

        public float MaximumBodyAimCorrectionDegrees => Mathf.Clamp(
            maximumBodyAimCorrectionDegrees,
            0f,
            90f);

        public float BodyAimDegreesPerSecond =>
            Mathf.Max(0f, bodyAimDegreesPerSecond);

        public float ActorAimTurnDegreesPerSecond =>
            Mathf.Max(0f, actorAimTurnDegreesPerSecond);

        public float WeaponAimDegreesPerSecond =>
            Mathf.Max(0f, weaponAimDegreesPerSecond);

        public float ShotAlignmentToleranceDegrees => Mathf.Clamp(
            shotAlignmentToleranceDegrees,
            0f,
            10f);

        public ActorTurnInPlaceSettings TurnInPlace =>
            turnInPlace ??= new ActorTurnInPlaceSettings();

        public float RecoilExitNormalizedTime =>
            Mathf.Clamp01(recoilExitNormalizedTime);

        public float RecoilReturnTransitionSeconds =>
            Mathf.Max(0f, recoilReturnTransitionSeconds);

        public IReadOnlyList<ActorWeaponAnimationSet> WeaponAnimationSets =>
            weaponAnimationSets ?? Array.Empty<ActorWeaponAnimationSet>();

        public IReadOnlyList<ActorAnimationActionBinding> ActionBindings =>
            actionBindings ?? Array.Empty<ActorAnimationActionBinding>();

        public ActorWeaponAnimationSet GetWeaponAnimationSet(string animationSetId)
        {
            if (string.IsNullOrWhiteSpace(animationSetId))
            {
                throw new ArgumentException(
                    "Actor animation-set identifiers cannot be empty.",
                    nameof(animationSetId));
            }

            ActorWeaponAnimationSet match = null;
            foreach (ActorWeaponAnimationSet candidate in WeaponAnimationSets)
            {
                if (candidate == null
                    || !string.Equals(
                        candidate.Id,
                        animationSetId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                if (match != null)
                {
                    throw new InvalidOperationException(
                        $"Actor animation set '{animationSetId}' is duplicated in "
                        + $"profile '{name}'.");
                }

                match = candidate;
            }

            return match ?? throw new KeyNotFoundException(
                $"Actor animation set '{animationSetId}' is not defined by "
                + $"profile '{name}'.");
        }

        public bool TryGetActionBinding(
            ActorAnimationAction action,
            out ActorAnimationActionBinding binding)
        {
            binding = null;
            foreach (ActorAnimationActionBinding candidate in ActionBindings)
            {
                if (candidate == null || candidate.Action != action)
                {
                    continue;
                }

                if (binding != null)
                {
                    throw new InvalidOperationException(
                        $"Actor animation action '{action}' is duplicated in "
                        + $"profile '{name}'.");
                }

                binding = candidate;
            }

            return binding != null;
        }

        private void OnValidate()
        {
            locomotionReferenceSpeed = Mathf.Max(
                MinimumReferenceValue,
                locomotionReferenceSpeed);
            turnReferenceDegreesPerSecond = Mathf.Max(
                MinimumReferenceValue,
                turnReferenceDegreesPerSecond);
            parameterDampTime = Mathf.Max(0f, parameterDampTime);
            maximumBodyAimCorrectionDegrees = Mathf.Clamp(
                maximumBodyAimCorrectionDegrees,
                0f,
                90f);
            bodyAimDegreesPerSecond = Mathf.Max(
                0f,
                bodyAimDegreesPerSecond);
            actorAimTurnDegreesPerSecond = Mathf.Max(
                0f,
                actorAimTurnDegreesPerSecond);
            weaponAimDegreesPerSecond = Mathf.Max(
                0f,
                weaponAimDegreesPerSecond);
            shotAlignmentToleranceDegrees = Mathf.Clamp(
                shotAlignmentToleranceDegrees,
                0f,
                10f);
            turnInPlace ??= new ActorTurnInPlaceSettings();
            recoilExitNormalizedTime = Mathf.Clamp01(
                recoilExitNormalizedTime);
            recoilReturnTransitionSeconds = Mathf.Max(
                0f,
                recoilReturnTransitionSeconds);
            weaponAnimationSets ??= Array.Empty<ActorWeaponAnimationSet>();
            actionBindings ??= Array.Empty<ActorAnimationActionBinding>();
        }
    }
}
