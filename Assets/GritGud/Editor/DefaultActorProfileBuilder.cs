using GritGud.Domain.Gameplay;
using GritGud.Presentation.Actors.Animation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using static GritGud.Editor.DefaultActorAssetRecipe;

namespace GritGud.Editor
{
    internal static class DefaultActorProfileBuilder
    {
        internal static ActorAnimationProfile Build(
            AnimatorController controller)
        {
            ActorAnimationProfile profile =
                AssetDatabase.LoadAssetAtPath<ActorAnimationProfile>(
                    ProfilePath);
            if (profile == null)
            {
                profile =
                    ScriptableObject.CreateInstance<ActorAnimationProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }

            var serializedProfile = new SerializedObject(profile);
            serializedProfile.FindProperty("animatorController")
                .objectReferenceValue = controller;
            serializedProfile.FindProperty("locomotionReferenceSpeed")
                .floatValue = LocomotionReferenceSpeed;
            serializedProfile.FindProperty("turnReferenceDegreesPerSecond")
                .floatValue = TurnReferenceDegreesPerSecond;
            serializedProfile.FindProperty("parameterDampTime")
                .floatValue = ParameterDampTime;
            serializedProfile.FindProperty("maximumBodyAimCorrectionDegrees")
                .floatValue = MaximumBodyAimCorrectionDegrees;
            serializedProfile.FindProperty("bodyAimDegreesPerSecond")
                .floatValue = BodyAimDegreesPerSecond;
            serializedProfile.FindProperty("actorAimTurnDegreesPerSecond")
                .floatValue = ActorAimTurnDegreesPerSecond;
            serializedProfile.FindProperty("weaponAimDegreesPerSecond")
                .floatValue = WeaponAimDegreesPerSecond;
            serializedProfile.FindProperty("shotAlignmentToleranceDegrees")
                .floatValue = ShotAlignmentToleranceDegrees;
            serializedProfile.FindProperty("recoilExitNormalizedTime")
                .floatValue = RecoilExitNormalizedTime;
            serializedProfile.FindProperty("recoilReturnTransitionSeconds")
                .floatValue = RecoilReturnTransitionSeconds;
            ConfigureTurnInPlace(
                serializedProfile.FindProperty("turnInPlace"));
            ConfigureWeaponAnimationSets(
                serializedProfile.FindProperty("weaponAnimationSets"));
            ConfigureActionBindings(
                serializedProfile.FindProperty("actionBindings"));
            serializedProfile.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static void ConfigureWeaponAnimationSets(
            SerializedProperty sets)
        {
            sets.arraySize = 4;
            ConfigureWeaponAnimationSet(
                sets.GetArrayElementAtIndex(0),
                ActorAnimationPoseIds.Empty,
                ActorAnimationParameters.EmptyHandsStateName,
                string.Empty,
                EmptyPoseValue,
                recoilPlaybackSpeed: 1f,
                recoilLayerWeight: 0f,
                recoilKickDegrees: 0f,
                recoilHoldSeconds: 0f,
                recoilReturnSeconds: 0.18f);
            ConfigureWeaponAnimationSet(
                sets.GetArrayElementAtIndex(1),
                ActorAnimationPoseIds.Rifle,
                ActorAnimationParameters.RifleAimStateName,
                ActorAnimationParameters.RifleRecoilStateName,
                RiflePoseValue,
                recoilPlaybackSpeed: RifleRecoilPlaybackSpeed,
                recoilLayerWeight: 0.8f,
                recoilKickDegrees: 9f,
                recoilHoldSeconds: 0.08f,
                recoilReturnSeconds: 0.42f);
            ConfigureWeaponAnimationSet(
                sets.GetArrayElementAtIndex(2),
                ActorAnimationPoseIds.Launcher,
                ActorAnimationParameters.LauncherAimStateName,
                ActorAnimationParameters.LauncherRecoilStateName,
                LauncherPoseValue,
                recoilPlaybackSpeed: LauncherRecoilPlaybackSpeed,
                recoilLayerWeight: 1f,
                recoilKickDegrees: 14f,
                recoilHoldSeconds: 0.1f,
                recoilReturnSeconds: 0.6f);
            ConfigureWeaponAnimationSet(
                sets.GetArrayElementAtIndex(3),
                ActorAnimationPoseIds.Melee,
                ActorAnimationParameters.KnifeIdleStateName,
                string.Empty,
                MeleePoseValue,
                recoilPlaybackSpeed: 1f,
                recoilLayerWeight: 0f,
                recoilKickDegrees: 0f,
                recoilHoldSeconds: 0f,
                recoilReturnSeconds: 0.18f);
        }

        private static void ConfigureWeaponAnimationSet(
            SerializedProperty set,
            string id,
            string poseStateName,
            string recoilStateName,
            int animatorPoseValue,
            float recoilPlaybackSpeed,
            float recoilLayerWeight,
            float recoilKickDegrees,
            float recoilHoldSeconds,
            float recoilReturnSeconds)
        {
            set.FindPropertyRelative("id").stringValue = id;
            set.FindPropertyRelative("poseStateName").stringValue =
                poseStateName;
            set.FindPropertyRelative("recoilStateName").stringValue =
                recoilStateName;
            set.FindPropertyRelative("recoilPlaybackSpeed").floatValue =
                recoilPlaybackSpeed;
            set.FindPropertyRelative("animatorPoseValue").intValue =
                animatorPoseValue;
            set.FindPropertyRelative("poseLayerWeight").floatValue =
                WeaponPoseLayerWeight;
            set.FindPropertyRelative("poseTransitionSeconds").floatValue =
                WeaponPoseTransitionSeconds;
            set.FindPropertyRelative("recoilLayerWeight").floatValue =
                recoilLayerWeight;
            set.FindPropertyRelative("recoilTransitionSeconds").floatValue =
                0f;
            set.FindPropertyRelative("recoilKickDegrees").floatValue =
                recoilKickDegrees;
            set.FindPropertyRelative("recoilHoldSeconds").floatValue =
                recoilHoldSeconds;
            set.FindPropertyRelative("recoilReturnSeconds").floatValue =
                recoilReturnSeconds;
        }

        private static void ConfigureTurnInPlace(SerializedProperty turn)
        {
            turn.FindPropertyRelative("activationDegreesPerSecond")
                .floatValue = TurnActivationDegreesPerSecond;
            turn.FindPropertyRelative("sustainDegreesPerSecond")
                .floatValue = TurnSustainDegreesPerSecond;
            turn.FindPropertyRelative("minimumActiveBlend").floatValue =
                TurnMinimumActiveBlend;
            turn.FindPropertyRelative("releaseDelaySeconds").floatValue =
                TurnReleaseDelaySeconds;
            turn.FindPropertyRelative("releaseSeconds").floatValue =
                TurnReleaseSeconds;
            turn.FindPropertyRelative("maximumMovementSpeed").floatValue =
                TurnMaximumMovementSpeed;
            turn.FindPropertyRelative("maximumLayerWeight").floatValue =
                TurnMaximumLayerWeight;
            turn.FindPropertyRelative("maximumPoseBlend").floatValue =
                TurnMaximumPoseBlend;
            turn.FindPropertyRelative("playbackSpeed").floatValue =
                TurnPlaybackSpeed;
        }

        private static void ConfigureActionBindings(
            SerializedProperty bindings)
        {
            bindings.arraySize = 7;
            SerializedProperty interaction =
                bindings.GetArrayElementAtIndex(0);
            interaction.FindPropertyRelative("action").enumValueIndex =
                (int)ActorAnimationAction.Interact;
            interaction.FindPropertyRelative("triggerParameterName")
                .stringValue = ActorAnimationParameters.InteractName;
            interaction.FindPropertyRelative("layerName").stringValue =
                string.Empty;
            interaction.FindPropertyRelative("stateName").stringValue =
                string.Empty;
            interaction.FindPropertyRelative("transitionSeconds")
                .floatValue = 0.1f;

            SerializedProperty throwing = bindings.GetArrayElementAtIndex(1);
            throwing.FindPropertyRelative("action").enumValueIndex =
                (int)ActorAnimationAction.Throw;
            throwing.FindPropertyRelative("triggerParameterName")
                .stringValue = string.Empty;
            throwing.FindPropertyRelative("layerName").stringValue =
                ActorAnimationParameters.ActionLayerName;
            throwing.FindPropertyRelative("stateName").stringValue =
                ActorAnimationParameters.ThrowStateName;
            throwing.FindPropertyRelative("transitionSeconds").floatValue =
                ActionTransitionSeconds;

            SerializedProperty jump = bindings.GetArrayElementAtIndex(2);
            jump.FindPropertyRelative("action").enumValueIndex =
                (int)ActorAnimationAction.Jump;
            jump.FindPropertyRelative("triggerParameterName").stringValue =
                string.Empty;
            jump.FindPropertyRelative("layerName").stringValue =
                ActorAnimationParameters.TraversalLayerName;
            jump.FindPropertyRelative("stateName").stringValue =
                ActorAnimationParameters.JumpStateName;
            jump.FindPropertyRelative("transitionSeconds").floatValue =
                ActionTransitionSeconds;

            ConfigureStateBinding(
                bindings.GetArrayElementAtIndex(3),
                ActorAnimationAction.ContactStrike,
                ActorAnimationParameters.ActionLayerName,
                ActorAnimationParameters.KnifeStrikeStateName);
            ConfigureStateBinding(
                bindings.GetArrayElementAtIndex(4),
                ActorAnimationAction.HitReaction,
                ActorAnimationParameters.ReactionLayerName,
                ActorAnimationParameters.HitReactionStateName);
            ConfigureStateBinding(
                bindings.GetArrayElementAtIndex(5),
                ActorAnimationAction.Incapacitate,
                ActorAnimationParameters.ReactionLayerName,
                ActorAnimationParameters.FallOverStateName);
            ConfigureStateBinding(
                bindings.GetArrayElementAtIndex(6),
                ActorAnimationAction.IncapacitateShoulder,
                ActorAnimationParameters.ReactionLayerName,
                ActorAnimationParameters.ShoulderFallStateName);
        }

        private static void ConfigureStateBinding(
            SerializedProperty binding,
            ActorAnimationAction action,
            string layerName,
            string stateName)
        {
            binding.FindPropertyRelative("action").enumValueIndex =
                (int)action;
            binding.FindPropertyRelative("triggerParameterName").stringValue =
                string.Empty;
            binding.FindPropertyRelative("layerName").stringValue = layerName;
            binding.FindPropertyRelative("stateName").stringValue = stateName;
            binding.FindPropertyRelative("transitionSeconds").floatValue =
                ActionTransitionSeconds;
        }
    }
}
