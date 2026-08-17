using System;
using System.Collections.Generic;
using GritGud.Presentation.Actors;
using GritGud.Presentation.Actors.Animation;
using GritGud.Presentation.Gameplay;
using UnityEditor.Animations;
using UnityEngine;
using static GritGud.Editor.DefaultActorAssetRecipe;

namespace GritGud.Editor
{
    internal static class DefaultActorPrefabValidator
    {
        internal static void Validate(
            GameObject prefab,
            ActorAnimationProfile profile,
            ActorMotionProfile motionProfile,
            ActorRagdollProfile ragdollProfile,
            AnimatorController controller)
        {
            ThirdPersonMotor motor = prefab.GetComponent<ThirdPersonMotor>();
            ExplorationMovementInput input =
                prefab.GetComponent<ExplorationMovementInput>();
            AnimatorDriver animatorDriver =
                prefab.GetComponent<AnimatorDriver>();
            ActorAnimationCoordinator animationCoordinator =
                prefab.GetComponent<ActorAnimationCoordinator>();
            ActorRagdollPresenter ragdollPresenter =
                prefab.GetComponent<ActorRagdollPresenter>();
            ActorLocomotionAnimationPresenter locomotionPresenter =
                prefab.GetComponent<ActorLocomotionAnimationPresenter>();
            ActorStancePresenter stancePresenter =
                prefab.GetComponent<ActorStancePresenter>();
            ActorCelShadingPresenter celShadingPresenter =
                prefab.GetComponent<ActorCelShadingPresenter>();
            CharacterController characterController =
                prefab.GetComponent<CharacterController>();
            Animator animator = prefab.GetComponentInChildren<Animator>(true);
            if (motor == null || input == null || animatorDriver == null ||
                animationCoordinator == null || ragdollPresenter == null ||
                locomotionPresenter == null ||
                stancePresenter == null || celShadingPresenter == null ||
                characterController == null || animator == null)
            {
                throw new InvalidOperationException(
                    "The default player prefab requires its input, motor, "
                    + "locomotion presenter, stance presenter, cel-shading "
                    + "presenter, animator driver, animation coordinator, "
                    + "ragdoll presenter, character controller, and animator.");
            }

            if ((motor.MovementCommandSource as UnityEngine.Object) != input ||
                motor.MotionProfile != motionProfile ||
                locomotionPresenter.Motor != motor ||
                locomotionPresenter.AnimationCoordinator !=
                    animationCoordinator)
            {
                throw new InvalidOperationException(
                    "The default player prefab must bind its input, motor, "
                    + "motion profile, and locomotion presenter.");
            }

            var animatorBindingErrors = new List<string>();
            if (animatorDriver.TargetAnimator != animator)
            {
                animatorBindingErrors.Add("AnimatorDriver.targetAnimator");
            }
            if (animationCoordinator.Driver != animatorDriver)
            {
                animatorBindingErrors.Add(
                    "ActorAnimationCoordinator.animatorDriver");
            }
            if (animationCoordinator.TargetAnimator != animator)
            {
                animatorBindingErrors.Add(
                    "ActorAnimationCoordinator.targetAnimator");
            }
            if (animationCoordinator.Profile != profile)
            {
                animatorBindingErrors.Add("ActorAnimationCoordinator.profile");
            }
            if (ragdollPresenter.Profile != ragdollProfile)
            {
                animatorBindingErrors.Add("ActorRagdollPresenter.profile");
            }
            if (profile.AnimatorController != controller)
            {
                animatorBindingErrors.Add(
                    "ActorAnimationProfile.animatorController");
            }
            if (animator.runtimeAnimatorController != controller)
            {
                animatorBindingErrors.Add("Animator.runtimeAnimatorController");
            }
            if (animator.applyRootMotion)
            {
                animatorBindingErrors.Add("Animator.applyRootMotion");
            }

            if (animatorBindingErrors.Count > 0)
            {
                throw new InvalidOperationException(
                    "The default player prefab must bind its animator to the "
                    + "project profile with root motion disabled. Failed: "
                    + string.Join(", ", animatorBindingErrors)
                    + ".");
            }

            ActorAnimationContractValidator.Validate(profile, animator);
            ValidateHumanoidVisual(prefab);
        }

        internal static void ValidateHumanoidVisual(GameObject sourceVisual)
        {
            Animator animator =
                sourceVisual.GetComponentInChildren<Animator>(true);
            if (animator == null || animator.avatar == null ||
                !animator.avatar.isValid || !animator.avatar.isHuman)
            {
                throw new InvalidOperationException(
                    $"Default actor source visual '{SourceVisualPath}' "
                    + "requires a valid Humanoid avatar.");
            }
        }
    }
}
