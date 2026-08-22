using System;
using GritGud.Domain.Gameplay;
using GritGud.Presentation.Actors.Animation;
using UnityEditor.Animations;
using UnityEngine;
using static GritGud.Editor.DefaultActorAssetRecipe;

namespace GritGud.Editor
{
    internal static class DefaultActorProfileValidator
    {
        internal static void Validate(
            ActorAnimationProfile profile,
            AnimatorController controller)
        {
            if (profile.AnimatorController != controller ||
                Mathf.Abs(
                    profile.LocomotionReferenceSpeed -
                    LocomotionReferenceSpeed) > 0.001f ||
                Mathf.Abs(
                    profile.TurnReferenceDegreesPerSecond -
                    TurnReferenceDegreesPerSecond) > 0.001f ||
                Mathf.Abs(
                    profile.ParameterDampTime - ParameterDampTime) > 0.001f ||
                Mathf.Abs(
                    profile.MaximumBodyAimCorrectionDegrees -
                    MaximumBodyAimCorrectionDegrees) > 0.001f ||
                Mathf.Abs(
                    profile.BodyAimDegreesPerSecond -
                    BodyAimDegreesPerSecond) > 0.001f ||
                Mathf.Abs(
                    profile.ActorAimTurnDegreesPerSecond -
                    ActorAimTurnDegreesPerSecond) > 0.001f ||
                Mathf.Abs(
                    profile.WeaponAimDegreesPerSecond -
                    WeaponAimDegreesPerSecond) > 0.001f ||
                Mathf.Abs(
                    profile.ShotAlignmentToleranceDegrees -
                    ShotAlignmentToleranceDegrees) > 0.001f ||
                Mathf.Abs(
                    profile.RecoilExitNormalizedTime -
                    RecoilExitNormalizedTime) > 0.001f ||
                Mathf.Abs(
                    profile.RecoilReturnTransitionSeconds -
                    RecoilReturnTransitionSeconds) > 0.001f)
            {
                throw new InvalidOperationException(
                    "The default animation profile must reference its "
                    + "generated controller and authored motion scales.");
            }

            ValidateTurnInPlace(profile.TurnInPlace);

            if (profile.WeaponAnimationSets.Count != 4)
            {
                throw new InvalidOperationException(
                    "The default animation profile requires empty, rifle, "
                    + "launcher, and melee weapon sets.");
            }

            ValidateWeaponSet(
                profile.GetWeaponAnimationSet(ActorAnimationPoseIds.Empty),
                ActorAnimationParameters.EmptyHandsStateName,
                string.Empty,
                EmptyPoseValue,
                poseLayerWeight: 0f,
                recoilPlaybackSpeed: 1f,
                recoilLayerWeight: 0f,
                recoilKickDegrees: 0f,
                recoilHoldSeconds: 0f,
                recoilReturnSeconds: 0.18f);
            ValidateWeaponSet(
                profile.GetWeaponAnimationSet(ActorAnimationPoseIds.Rifle),
                ActorAnimationParameters.RifleAimStateName,
                ActorAnimationParameters.RifleRecoilStateName,
                RiflePoseValue,
                poseLayerWeight: WeaponPoseLayerWeight,
                recoilPlaybackSpeed: RifleRecoilPlaybackSpeed,
                recoilLayerWeight: 0.8f,
                recoilKickDegrees: 9f,
                recoilHoldSeconds: 0.08f,
                recoilReturnSeconds: 0.42f);
            ValidateWeaponSet(
                profile.GetWeaponAnimationSet(ActorAnimationPoseIds.Launcher),
                ActorAnimationParameters.LauncherAimStateName,
                ActorAnimationParameters.LauncherRecoilStateName,
                LauncherPoseValue,
                poseLayerWeight: WeaponPoseLayerWeight,
                recoilPlaybackSpeed: LauncherRecoilPlaybackSpeed,
                recoilLayerWeight: 1f,
                recoilKickDegrees: 14f,
                recoilHoldSeconds: 0.1f,
                recoilReturnSeconds: 0.6f);
            ValidateWeaponSet(
                profile.GetWeaponAnimationSet(ActorAnimationPoseIds.Melee),
                ActorAnimationParameters.KnifeIdleStateName,
                string.Empty,
                MeleePoseValue,
                poseLayerWeight: WeaponPoseLayerWeight,
                recoilPlaybackSpeed: 1f,
                recoilLayerWeight: 0f,
                recoilKickDegrees: 0f,
                recoilHoldSeconds: 0f,
                recoilReturnSeconds: 0.18f);

            if (profile.ActionBindings.Count != 10 ||
                !profile.TryGetActionBinding(
                    ActorAnimationAction.Interact,
                    out ActorAnimationActionBinding interaction) ||
                interaction.TriggerParameterName !=
                    ActorAnimationParameters.InteractName ||
                interaction.UsesState ||
                Mathf.Abs(interaction.TransitionSeconds - 0.1f) > 0.001f ||
                !IsContextualStateBinding(
                    profile,
                    ActorAnimationAction.WeaponFire,
                    ActorAnimationPoseIds.Rifle,
                    ActorAnimationParameters.ActionLayerName,
                    ActorAnimationParameters.RifleFireStateName) ||
                !IsContextualStateBinding(
                    profile,
                    ActorAnimationAction.WeaponFire,
                    ActorAnimationPoseIds.Launcher,
                    ActorAnimationParameters.ActionLayerName,
                    ActorAnimationParameters.LauncherFireStateName) ||
                !profile.TryGetActionBinding(
                    ActorAnimationAction.Throw,
                    out ActorAnimationActionBinding throwing) ||
                throwing.UsesTrigger ||
                throwing.LayerName !=
                    ActorAnimationParameters.ActionLayerName ||
                throwing.StateName !=
                    ActorAnimationParameters.ThrowStateName ||
                !throwing.UsesState ||
                Mathf.Abs(
                    throwing.TransitionSeconds -
                    ActionTransitionSeconds) > 0.001f ||
                !profile.TryGetActionBinding(
                    ActorAnimationAction.Jump,
                    out ActorAnimationActionBinding jump) ||
                jump.UsesTrigger ||
                jump.LayerName !=
                    ActorAnimationParameters.TraversalLayerName ||
                jump.StateName != ActorAnimationParameters.JumpStateName ||
                !jump.UsesState ||
                Mathf.Abs(
                    jump.TransitionSeconds -
                    ActionTransitionSeconds) > 0.001f ||
                !IsStateBinding(
                    profile,
                    ActorAnimationAction.ContactStrike,
                    ActorAnimationParameters.ActionLayerName,
                    ActorAnimationParameters.KnifeStrikeStateName) ||
                !IsStateBinding(
                    profile,
                    ActorAnimationAction.HitReaction,
                    ActorAnimationParameters.ReactionLayerName,
                    ActorAnimationParameters.HitReactionStateName) ||
                !IsStateBinding(
                    profile,
                    ActorAnimationAction.Incapacitate,
                    ActorAnimationParameters.ReactionLayerName,
                    ActorAnimationParameters.FallOverStateName) ||
                !IsStateBinding(
                    profile,
                    ActorAnimationAction.IncapacitateShoulder,
                    ActorAnimationParameters.ReactionLayerName,
                    ActorAnimationParameters.ShoulderFallStateName) ||
                !IsStateBinding(
                    profile,
                    ActorAnimationAction.Push,
                    ActorAnimationParameters.DisplacementLayerName,
                    ActorAnimationParameters.PushStateName))
            {
                throw new InvalidOperationException(
                    "The default animation profile requires its authored "
                    + "firearm, interaction, throw, jump, strike, and reaction "
                    + "bindings.");
            }
        }

        private static bool IsContextualStateBinding(
            ActorAnimationProfile profile,
            ActorAnimationAction action,
            string contextId,
            string layerName,
            string stateName)
        {
            return profile.TryGetActionBinding(
                    action,
                    contextId,
                    out ActorAnimationActionBinding binding) &&
                binding.ContextId == contextId &&
                !binding.UsesTrigger &&
                binding.UsesState &&
                binding.LayerName == layerName &&
                binding.StateName == stateName &&
                Mathf.Abs(
                    binding.TransitionSeconds -
                    ActionTransitionSeconds) <= 0.001f;
        }

        private static bool IsStateBinding(
            ActorAnimationProfile profile,
            ActorAnimationAction action,
            string layerName,
            string stateName)
        {
            return profile.TryGetActionBinding(action, out var binding) &&
                !binding.UsesTrigger &&
                binding.UsesState &&
                binding.LayerName == layerName &&
                binding.StateName == stateName &&
                Mathf.Abs(
                    binding.TransitionSeconds -
                    ActionTransitionSeconds) <= 0.001f;
        }

        private static void ValidateWeaponSet(
            ActorWeaponAnimationSet set,
            string poseStateName,
            string recoilStateName,
            int animatorPoseValue,
            float poseLayerWeight,
            float recoilPlaybackSpeed,
            float recoilLayerWeight,
            float recoilKickDegrees,
            float recoilHoldSeconds,
            float recoilReturnSeconds)
        {
            if (set.PoseStateName != poseStateName ||
                set.RecoilStateName != recoilStateName ||
                set.AnimatorPoseValue != animatorPoseValue ||
                Mathf.Abs(
                    set.RecoilPlaybackSpeed - recoilPlaybackSpeed) > 0.001f ||
                Mathf.Abs(
                    set.PoseLayerWeight -
                    poseLayerWeight) > 0.001f ||
                Mathf.Abs(
                    set.PoseTransitionSeconds -
                    WeaponPoseTransitionSeconds) > 0.001f ||
                Mathf.Abs(
                    set.RecoilLayerWeight - recoilLayerWeight) > 0.001f ||
                Mathf.Abs(set.RecoilTransitionSeconds) > 0.001f ||
                Mathf.Abs(
                    set.RecoilKickDegrees - recoilKickDegrees) > 0.001f ||
                Mathf.Abs(
                    set.RecoilHoldSeconds - recoilHoldSeconds) > 0.001f ||
                Mathf.Abs(
                    set.RecoilReturnSeconds - recoilReturnSeconds) > 0.001f)
            {
                throw new InvalidOperationException(
                    $"Weapon animation set '{set.Id}' does not match the "
                    + "default actor recipe.");
            }
        }

        private static void ValidateTurnInPlace(
            ActorTurnInPlaceSettings turn)
        {
            if (Mathf.Abs(
                    turn.ActivationDegreesPerSecond -
                    TurnActivationDegreesPerSecond) > 0.001f ||
                Mathf.Abs(
                    turn.SustainDegreesPerSecond -
                    TurnSustainDegreesPerSecond) > 0.001f ||
                Mathf.Abs(
                    turn.MinimumActiveBlend -
                    TurnMinimumActiveBlend) > 0.001f ||
                Mathf.Abs(
                    turn.ReleaseDelaySeconds -
                    TurnReleaseDelaySeconds) > 0.001f ||
                Mathf.Abs(
                    turn.ReleaseSeconds - TurnReleaseSeconds) > 0.001f ||
                Mathf.Abs(
                    turn.MaximumMovementSpeed -
                    TurnMaximumMovementSpeed) > 0.001f ||
                Mathf.Abs(
                    turn.MaximumLayerWeight -
                    TurnMaximumLayerWeight) > 0.001f ||
                Mathf.Abs(
                    turn.MaximumPoseBlend -
                    TurnMaximumPoseBlend) > 0.001f ||
                Mathf.Abs(
                    turn.PlaybackSpeed - TurnPlaybackSpeed) > 0.001f)
            {
                throw new InvalidOperationException(
                    "The default animation profile turn-in-place settings "
                    + "do not match the actor recipe.");
            }
        }
    }
}
