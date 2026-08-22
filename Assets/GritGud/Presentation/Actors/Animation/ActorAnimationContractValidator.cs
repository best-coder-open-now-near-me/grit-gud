using System;
using System.Collections.Generic;
using UnityEngine;

namespace GritGud.Presentation.Actors.Animation
{
    public static class ActorAnimationContractValidator
    {
        public static void Validate(
            ActorAnimationProfile profile,
            Animator animator)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            if (animator == null)
            {
                throw new ArgumentNullException(nameof(animator));
            }

            if (profile.AnimatorController == null)
            {
                throw new InvalidOperationException(
                    $"Animation profile '{profile.name}' has no controller.");
            }

            if (animator.runtimeAnimatorController !=
                profile.AnimatorController)
            {
                throw new InvalidOperationException(
                    $"Animator '{animator.name}' is not bound to profile "
                    + $"'{profile.name}' controller.");
            }

            ValidateParameter(
                animator,
                ActorAnimationParameters.MoveXName,
                AnimatorControllerParameterType.Float);
            ValidateParameter(
                animator,
                ActorAnimationParameters.MoveYName,
                AnimatorControllerParameterType.Float);
            ValidateParameter(
                animator,
                ActorAnimationParameters.SpeedName,
                AnimatorControllerParameterType.Float);
            ValidateParameter(
                animator,
                ActorAnimationParameters.GroundedName,
                AnimatorControllerParameterType.Bool);
            ValidateParameter(
                animator,
                ActorAnimationParameters.TurnRateName,
                AnimatorControllerParameterType.Float);
            ValidateParameter(
                animator,
                ActorAnimationParameters.StanceName,
                AnimatorControllerParameterType.Int);
            ValidateParameter(
                animator,
                ActorAnimationParameters.WeaponPoseName,
                AnimatorControllerParameterType.Int);

            int weaponLayer = RequireLayer(
                animator,
                ActorAnimationChannelPlan.WeaponPose.AnimatorLayerName);
            int recoilLayer = RequireLayer(
                animator,
                ActorAnimationChannelPlan.Recoil.AnimatorLayerName);
            RequireLayer(
                animator,
                ActorAnimationChannelPlan.Actions.AnimatorLayerName);
            RequireLayer(
                animator,
                ActorAnimationChannelPlan.TurnInPlace.AnimatorLayerName);

            var animationSetIds = new HashSet<string>(StringComparer.Ordinal);
            var poseValues = new HashSet<int>();
            if (profile.WeaponAnimationSets.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Animation profile '{profile.name}' defines no weapon "
                    + "animation sets.");
            }

            foreach (ActorWeaponAnimationSet set in
                profile.WeaponAnimationSets)
            {
                if (set == null || string.IsNullOrWhiteSpace(set.Id))
                {
                    throw new InvalidOperationException(
                        $"Animation profile '{profile.name}' contains an "
                        + "empty weapon animation-set entry.");
                }

                if (!animationSetIds.Add(set.Id))
                {
                    throw new InvalidOperationException(
                        $"Animation profile '{profile.name}' duplicates "
                        + $"animation set '{set.Id}'.");
                }

                if (!poseValues.Add(set.AnimatorPoseValue))
                {
                    throw new InvalidOperationException(
                        $"Animation profile '{profile.name}' duplicates "
                        + $"animator pose value {set.AnimatorPoseValue}.");
                }

                RequireState(animator, weaponLayer, set.PoseStateName);
                if (!string.IsNullOrWhiteSpace(set.RecoilStateName))
                {
                    RequireState(
                        animator,
                        recoilLayer,
                        set.RecoilStateName);
                }
            }

            if (!animationSetIds.Contains(ActorAnimationPoseIds.Empty))
            {
                throw new InvalidOperationException(
                    $"Animation profile '{profile.name}' must define the "
                    + $"'{ActorAnimationPoseIds.Empty}' set.");
            }

            var actions = new HashSet<string>(StringComparer.Ordinal);
            foreach (ActorAnimationActionBinding binding in
                profile.ActionBindings)
            {
                if (binding == null ||
                    (!binding.UsesTrigger && !binding.UsesState))
                {
                    throw new InvalidOperationException(
                        $"Animation profile '{profile.name}' contains an "
                        + "empty action binding.");
                }

                string bindingKey = $"{(int)binding.Action}:{binding.ContextId}";
                if (!actions.Add(bindingKey))
                {
                    throw new InvalidOperationException(
                        $"Animation profile '{profile.name}' duplicates "
                        + $"action '{binding.Action}' in context "
                        + $"'{binding.ContextId}'.");
                }

                if (binding.UsesTrigger)
                {
                    ValidateParameter(
                        animator,
                        binding.TriggerParameterName,
                        AnimatorControllerParameterType.Trigger);
                }

                if (binding.UsesState)
                {
                    int layer = RequireLayer(animator, binding.LayerName);
                    RequireState(animator, layer, binding.StateName);
                }
            }

            foreach (ActorWeaponAnimationSet set in
                profile.WeaponAnimationSets)
            {
                if (string.IsNullOrWhiteSpace(set.RecoilStateName))
                    continue;
                if (!profile.TryGetActionBinding(
                        ActorAnimationAction.WeaponFire,
                        set.Id,
                        out ActorAnimationActionBinding binding) ||
                    !binding.UsesState ||
                    binding.ContextId != set.Id ||
                    binding.LayerName !=
                        ActorAnimationParameters.ActionLayerName)
                {
                    throw new InvalidOperationException(
                        $"Firearm animation set '{set.Id}' in profile "
                        + $"'{profile.name}' requires an explicit WeaponFire "
                        + "state binding on the actor action layer.");
                }
            }
        }

        private static void ValidateParameter(
            Animator animator,
            string parameterName,
            AnimatorControllerParameterType expectedType)
        {
            foreach (AnimatorControllerParameter parameter in
                animator.parameters)
            {
                if (parameter.name == parameterName &&
                    parameter.type == expectedType)
                {
                    return;
                }
            }

            throw new InvalidOperationException(
                $"Animator controller '{animator.runtimeAnimatorController.name}' "
                + $"does not contain {expectedType} parameter "
                + $"'{parameterName}'.");
        }

        private static int RequireLayer(Animator animator, string layerName)
        {
            if (string.IsNullOrWhiteSpace(layerName))
            {
                throw new InvalidOperationException(
                    "Animation layer names cannot be empty.");
            }

            int layer = animator.GetLayerIndex(layerName);
            if (layer < 0)
            {
                throw new InvalidOperationException(
                    $"Animator controller "
                    + $"'{animator.runtimeAnimatorController.name}' does not "
                    + $"contain layer '{layerName}'.");
            }

            return layer;
        }

        private static void RequireState(
            Animator animator,
            int layer,
            string stateName)
        {
            if (string.IsNullOrWhiteSpace(stateName) ||
                !animator.HasState(layer, Animator.StringToHash(stateName)))
            {
                throw new InvalidOperationException(
                    $"Animator controller "
                    + $"'{animator.runtimeAnimatorController.name}' does not "
                    + $"contain state '{stateName}' on layer "
                    + $"'{animator.GetLayerName(layer)}'.");
            }
        }
    }
}
