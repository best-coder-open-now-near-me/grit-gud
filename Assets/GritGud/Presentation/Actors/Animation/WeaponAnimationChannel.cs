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
