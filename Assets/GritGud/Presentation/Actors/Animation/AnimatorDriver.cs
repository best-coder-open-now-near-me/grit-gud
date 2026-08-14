using System;
using UnityEngine;

namespace GritGud.Presentation.Actors.Animation
{
    [DisallowMultipleComponent]
    public sealed class AnimatorDriver : MonoBehaviour
    {
        [SerializeField]
        private Animator targetAnimator;

        public Animator TargetAnimator => targetAnimator;

        public bool CanWrite => targetAnimator != null &&
            targetAnimator.runtimeAnimatorController != null;

        private void Awake()
        {
            targetAnimator ??= GetComponentInChildren<Animator>();
            DisableRootMotion();
        }

        public void Bind(
            Animator animator,
            RuntimeAnimatorController controller)
        {
            targetAnimator = animator ?? throw new ArgumentNullException(
                nameof(animator));
            targetAnimator.runtimeAnimatorController = controller;
            DisableRootMotion();
        }

        public void SetFloat(
            int parameter,
            float value,
            float dampTime,
            float deltaTime)
        {
            if (!CanWrite)
            {
                return;
            }

            if (dampTime > 0f && deltaTime > 0f)
            {
                targetAnimator.SetFloat(parameter, value, dampTime, deltaTime);
                return;
            }

            targetAnimator.SetFloat(parameter, value);
        }

        public void SetBool(int parameter, bool value)
        {
            if (CanWrite)
            {
                targetAnimator.SetBool(parameter, value);
            }
        }

        public void SetInteger(int parameter, int value)
        {
            if (CanWrite)
            {
                targetAnimator.SetInteger(parameter, value);
            }
        }

        public void SetLayerWeight(string layerName, float weight)
        {
            if (!CanWrite)
            {
                return;
            }

            targetAnimator.SetLayerWeight(
                RequireLayer(layerName),
                Mathf.Clamp01(weight));
        }

        public bool HasLayer(string layerName) =>
            CanWrite && targetAnimator.GetLayerIndex(layerName) >= 0;

        public void PlayState(
            string layerName,
            int stateHash,
            float normalizedTime)
        {
            if (!CanWrite)
            {
                return;
            }

            int layerIndex = RequireLayer(layerName);
            RequireState(layerIndex, stateHash);
            targetAnimator.Play(stateHash, layerIndex, normalizedTime);
        }

        public void CrossFadeState(
            string layerName,
            string stateName,
            float transitionSeconds)
        {
            if (!CanWrite)
            {
                return;
            }

            int layerIndex = RequireLayer(layerName);
            int stateHash = Animator.StringToHash(stateName);
            RequireState(layerIndex, stateHash, stateName);
            targetAnimator.CrossFadeInFixedTime(
                stateHash,
                Mathf.Max(0f, transitionSeconds),
                layerIndex);
        }

        public void RestartState(
            string layerName,
            string stateName,
            float transitionSeconds)
        {
            if (!CanWrite)
            {
                return;
            }

            int layerIndex = RequireLayer(layerName);
            int stateHash = Animator.StringToHash(stateName);
            RequireState(layerIndex, stateHash, stateName);
            targetAnimator.CrossFadeInFixedTime(
                stateHash,
                Mathf.Max(0f, transitionSeconds),
                layerIndex,
                fixedTimeOffset: 0f);
        }

        public void PulseTrigger(string parameterName)
        {
            if (!CanWrite)
            {
                return;
            }

            int trigger = RequireParameter(
                parameterName,
                AnimatorControllerParameterType.Trigger);
            targetAnimator.ResetTrigger(trigger);
            targetAnimator.SetTrigger(trigger);
        }

        public void DisableAndOffset(
            Quaternion visualLocalRotation,
            Vector3 visualLocalOffset)
        {
            if (targetAnimator == null)
            {
                return;
            }

            targetAnimator.enabled = false;
            Transform visual = targetAnimator.transform;
            visual.localRotation = visualLocalRotation * visual.localRotation;
            visual.localPosition += visualLocalOffset;
        }

        private void DisableRootMotion()
        {
            if (targetAnimator != null)
            {
                targetAnimator.applyRootMotion = false;
            }
        }

        private int RequireLayer(string layerName)
        {
            if (string.IsNullOrWhiteSpace(layerName))
            {
                throw new InvalidOperationException(
                    "Animator layer names cannot be empty.");
            }

            int layerIndex = targetAnimator.GetLayerIndex(layerName);
            if (layerIndex < 0)
            {
                throw new InvalidOperationException(
                    $"Animator controller '{targetAnimator.runtimeAnimatorController.name}' "
                    + $"does not contain layer '{layerName}'.");
            }

            return layerIndex;
        }

        private void RequireState(
            int layerIndex,
            int stateHash,
            string stateName = null)
        {
            if (!targetAnimator.HasState(layerIndex, stateHash))
            {
                throw new InvalidOperationException(
                    $"Animator controller '{targetAnimator.runtimeAnimatorController.name}' "
                    + $"does not contain state '{stateName ?? stateHash.ToString()}' "
                    + $"on layer '{targetAnimator.GetLayerName(layerIndex)}'.");
            }
        }

        private int RequireParameter(
            string parameterName,
            AnimatorControllerParameterType parameterType)
        {
            foreach (AnimatorControllerParameter parameter in targetAnimator.parameters)
            {
                if (parameter.name == parameterName && parameter.type == parameterType)
                {
                    return parameter.nameHash;
                }
            }

            throw new InvalidOperationException(
                $"Animator controller '{targetAnimator.runtimeAnimatorController.name}' "
                + $"does not contain {parameterType} parameter '{parameterName}'.");
        }
    }
}
