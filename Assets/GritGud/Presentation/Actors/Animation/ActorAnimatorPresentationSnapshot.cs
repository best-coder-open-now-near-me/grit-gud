using System;
using UnityEngine;

namespace GritGud.Presentation.Actors.Animation
{
    internal sealed class ActorAnimatorPresentationSnapshot
    {
        private readonly LayerState[] layers;
        private readonly ParameterState[] parameters;
        private readonly bool enabled;
        private readonly float speed;
        private readonly AnimatorUpdateMode updateMode;
        private readonly AnimatorCullingMode cullingMode;
        private readonly bool applyRootMotion;
        private readonly bool fireEvents;
        private readonly Vector3 visualLocalPosition;
        private readonly Quaternion visualLocalRotation;
        private readonly Vector3 visualLocalScale;

        private ActorAnimatorPresentationSnapshot(Animator animator)
        {
            enabled = animator.enabled;
            speed = animator.speed;
            updateMode = animator.updateMode;
            cullingMode = animator.cullingMode;
            applyRootMotion = animator.applyRootMotion;
            fireEvents = animator.fireEvents;
            Transform visual = animator.transform;
            visualLocalPosition = visual.localPosition;
            visualLocalRotation = visual.localRotation;
            visualLocalScale = visual.localScale;

            layers = new LayerState[animator.layerCount];
            for (int index = 0; index < layers.Length; index++)
            {
                AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(
                    index);
                layers[index] = new LayerState(
                    state.fullPathHash,
                    state.normalizedTime,
                    animator.GetLayerWeight(index));
            }

            AnimatorControllerParameter[] definitions = animator.parameters;
            parameters = new ParameterState[definitions.Length];
            for (int index = 0; index < definitions.Length; index++)
            {
                AnimatorControllerParameter definition = definitions[index];
                parameters[index] = new ParameterState(
                    definition.nameHash,
                    definition.type,
                    definition.type == AnimatorControllerParameterType.Float
                        ? animator.GetFloat(definition.nameHash)
                        : 0f,
                    definition.type == AnimatorControllerParameterType.Int
                        ? animator.GetInteger(definition.nameHash)
                        : 0,
                    definition.type == AnimatorControllerParameterType.Bool
                        ? animator.GetBool(definition.nameHash)
                        : false);
            }
        }

        public static ActorAnimatorPresentationSnapshot Capture(
            Animator animator) => animator == null
                ? null
                : new ActorAnimatorPresentationSnapshot(animator);

        public void Restore(Animator animator)
        {
            if (animator == null)
                return;
            animator.enabled = true;
            animator.speed = 0f;
            animator.fireEvents = false;
            foreach (ParameterState parameter in parameters)
            {
                switch (parameter.Type)
                {
                    case AnimatorControllerParameterType.Float:
                        animator.SetFloat(parameter.Hash, parameter.FloatValue);
                        break;
                    case AnimatorControllerParameterType.Int:
                        animator.SetInteger(parameter.Hash, parameter.IntValue);
                        break;
                    case AnimatorControllerParameterType.Bool:
                        animator.SetBool(parameter.Hash, parameter.BoolValue);
                        break;
                }
            }
            for (int index = 0; index < layers.Length; index++)
            {
                LayerState layer = layers[index];
                if (layer.StateHash != 0 && animator.HasState(
                        index,
                        layer.StateHash))
                {
                    animator.Play(
                        layer.StateHash,
                        index,
                        layer.NormalizedTime);
                }
                animator.SetLayerWeight(index, layer.Weight);
            }
            animator.Update(0f);
            Transform visual = animator.transform;
            visual.localPosition = visualLocalPosition;
            visual.localRotation = visualLocalRotation;
            visual.localScale = visualLocalScale;
            animator.applyRootMotion = applyRootMotion;
            animator.updateMode = updateMode;
            animator.cullingMode = cullingMode;
            animator.fireEvents = fireEvents;
            animator.speed = speed;
            animator.enabled = enabled;
        }

        private readonly struct LayerState
        {
            public LayerState(
                int stateHash,
                float normalizedTime,
                float weight)
            {
                StateHash = stateHash;
                NormalizedTime = normalizedTime;
                Weight = weight;
            }

            public int StateHash { get; }
            public float NormalizedTime { get; }
            public float Weight { get; }
        }

        private readonly struct ParameterState
        {
            public ParameterState(
                int hash,
                AnimatorControllerParameterType type,
                float floatValue,
                int intValue,
                bool boolValue)
            {
                Hash = hash;
                Type = type;
                FloatValue = floatValue;
                IntValue = intValue;
                BoolValue = boolValue;
            }

            public int Hash { get; }
            public AnimatorControllerParameterType Type { get; }
            public float FloatValue { get; }
            public int IntValue { get; }
            public bool BoolValue { get; }
        }
    }
}
