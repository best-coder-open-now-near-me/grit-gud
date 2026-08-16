using System;
using GritGud.Presentation.Actors;
using GritGud.Presentation.Actors.Animation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using static GritGud.Editor.DefaultActorAssetRecipe;

namespace GritGud.Editor
{
    internal static class DefaultActorAssetValidator
    {
        internal static bool GeneratedAssetsExist()
        {
            AnimatorController controller =
                AssetDatabase.LoadAssetAtPath<AnimatorController>(
                    ControllerPath);
            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            return controller != null &&
                DefaultActorControllerValidator.HasRequiredTurnLayer(
                    controller) &&
                DefaultActorControllerValidator.HasRequiredRecoilLayer(
                    controller) &&
                DefaultActorControllerValidator.HasRequiredActionLayer(
                    controller) &&
                DefaultActorControllerValidator.HasRequiredTraversalLayer(
                    controller) &&
                DefaultActorControllerValidator.HasRequiredReactionLayer(
                    controller) &&
                AssetDatabase.LoadAssetAtPath<AvatarMask>(
                    LowerBodyMaskPath) != null &&
                AssetDatabase.LoadAssetAtPath<ActorAnimationProfile>(
                    ProfilePath) != null &&
                AssetDatabase.LoadAssetAtPath<ActorMotionProfile>(
                    MotionProfilePath) != null &&
                prefab != null &&
                prefab.GetComponent<AnimatorDriver>() != null &&
                prefab.GetComponent<ActorAnimationCoordinator>() != null &&
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    DefaultWeaponRigAssetGenerator.RifleRigPath) != null &&
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    DefaultWeaponRigAssetGenerator.LauncherRigPath) != null &&
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    DefaultWeaponRigAssetGenerator.KnifeRigPath) != null;
        }

        internal static void ValidateGeneratedAssets()
        {
            AnimatorController controller =
                AssetDatabase.LoadAssetAtPath<AnimatorController>(
                    ControllerPath);
            ActorAnimationProfile profile =
                AssetDatabase.LoadAssetAtPath<ActorAnimationProfile>(
                    ProfilePath);
            ActorMotionProfile motionProfile =
                AssetDatabase.LoadAssetAtPath<ActorMotionProfile>(
                    MotionProfilePath);
            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (controller == null || profile == null ||
                motionProfile == null || prefab == null)
            {
                throw new InvalidOperationException(
                    "The default player controller, profile, and prefab "
                    + "must all exist.");
            }

            DefaultActorControllerValidator.Validate(controller);
            DefaultActorProfileValidator.Validate(profile, controller);
            DefaultActorMotionProfileValidator.Validate(motionProfile);
            GameObject prefabContents =
                PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                DefaultActorPrefabValidator.Validate(
                    prefabContents,
                    profile,
                    motionProfile,
                    controller);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabContents);
            }
            DefaultWeaponRigAssetGenerator.Validate();
        }

        internal static void ValidateHumanoidVisual(GameObject sourceVisual) =>
            DefaultActorPrefabValidator.ValidateHumanoidVisual(sourceVisual);
    }
}
