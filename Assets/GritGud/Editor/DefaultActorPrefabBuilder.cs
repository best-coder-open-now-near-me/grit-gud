using System;
using GritGud.Presentation.Actors;
using GritGud.Presentation.Actors.Animation;
using GritGud.Presentation.Gameplay;
using UnityEditor;
using UnityEngine;
using static GritGud.Editor.DefaultActorAssetRecipe;

namespace GritGud.Editor
{
    internal static class DefaultActorPrefabBuilder
    {
        internal static void Build(
            GameObject sourceVisual,
            ActorAnimationProfile profile,
            ActorMotionProfile motionProfile,
            ActorRagdollProfile ragdollProfile)
        {
            var root = new GameObject("Default Player Actor");
            try
            {
                CharacterController characterController =
                    root.AddComponent<CharacterController>();
                characterController.radius = 0.35f;
                characterController.height = 1.8f;
                characterController.center = new Vector3(0f, 0.9f, 0f);
                characterController.stepOffset = 0.35f;
                characterController.slopeLimit = 50f;

                root.AddComponent<ActorStancePresenter>();
                var input = root.AddComponent<ExplorationMovementInput>();
                ThirdPersonMotor motor = root.AddComponent<ThirdPersonMotor>();
                motor.BindMotionProfile(motionProfile);
                var animatorDriver = root.AddComponent<AnimatorDriver>();
                var animationCoordinator =
                    root.AddComponent<ActorAnimationCoordinator>();
                var ragdollPresenter =
                    root.AddComponent<ActorRagdollPresenter>();
                var locomotionPresenter =
                    root.AddComponent<ActorLocomotionAnimationPresenter>();
                GameObject visual = PrefabUtility.InstantiatePrefab(
                    sourceVisual) as GameObject;
                if (visual == null)
                {
                    throw new InvalidOperationException(
                        $"Could not instantiate actor source visual "
                        + $"'{SourceVisualPath}'.");
                }

                visual.name = "Player Visual";
                visual.transform.SetParent(root.transform, false);
                foreach (Collider collider in
                    visual.GetComponentsInChildren<Collider>(true))
                {
                    collider.enabled = false;
                }

                root.AddComponent<ActorCelShadingPresenter>();
                Animator animator =
                    visual.GetComponentInChildren<Animator>(true);
                animatorDriver.Bind(animator, profile.AnimatorController);
                animationCoordinator.Bind(animator, profile);
                ragdollPresenter.BindProfile(ragdollProfile);
                motor.BindCommandSource(input);
                locomotionPresenter.Bind(motor, animationCoordinator);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
