using UnityEngine;

namespace GritGud.Presentation.Actors.Animation
{
    internal sealed class WeaponAnimationChannel
    {
        public string CurrentAnimationSetId { get; private set; } =
            ActorAnimationPoseIds.Empty;

        public void PresentPose(
            ActorAnimationProfile profile,
            AnimatorDriver driver,
            string animationSetId)
        {
            ActorWeaponAnimationSet animationSet =
                profile.GetWeaponAnimationSet(animationSetId);
            CurrentAnimationSetId = animationSet.Id;
            driver.SetInteger(
                ActorAnimationParameters.WeaponPose,
                animationSet.AnimatorPoseValue);
            driver.CrossFadeState(
                ActorAnimationChannelPlan.WeaponPose.AnimatorLayerName,
                animationSet.PoseStateName,
                animationSet.PoseTransitionSeconds);
            driver.SetLayerWeight(
                ActorAnimationChannelPlan.WeaponPose.AnimatorLayerName,
                animationSet.PoseLayerWeight);
        }

        public void PresentReplayPose(
            ActorAnimationProfile profile,
            AnimatorDriver driver,
            string animationSetId)
        {
            ActorWeaponAnimationSet animationSet =
                profile.GetWeaponAnimationSet(animationSetId);
            CurrentAnimationSetId = animationSet.Id;
            driver.SetInteger(
                ActorAnimationParameters.WeaponPose,
                animationSet.AnimatorPoseValue);
            driver.PlayState(
                ActorAnimationChannelPlan.WeaponPose.AnimatorLayerName,
                Animator.StringToHash(animationSet.PoseStateName),
                0f);
            driver.SetLayerWeight(
                ActorAnimationChannelPlan.WeaponPose.AnimatorLayerName,
                animationSet.PoseLayerWeight);
        }

        public void RestoreCurrentAnimationSet(string animationSetId)
        {
            CurrentAnimationSetId = animationSetId;
        }

        public bool CanPresentPose(
            ActorAnimationProfile profile,
            AnimatorDriver driver,
            string animationSetId)
        {
            ActorWeaponAnimationSet animationSet =
                profile.GetWeaponAnimationSet(animationSetId);
            return driver.HasLayer(
                ActorAnimationChannelPlan.WeaponPose.AnimatorLayerName);
        }
    }
}
