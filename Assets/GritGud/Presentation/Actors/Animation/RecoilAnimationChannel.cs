namespace GritGud.Presentation.Actors.Animation
{
    internal sealed class RecoilAnimationChannel
    {
        public void Reset(AnimatorDriver driver)
        {
            string layerName =
                ActorAnimationChannelPlan.Recoil.AnimatorLayerName;
            if (driver.HasLayer(layerName))
            {
                driver.SetLayerWeight(layerName, 0f);
            }
        }

        public bool TryPresent(
            ActorAnimationProfile profile,
            AnimatorDriver driver,
            string animationSetId)
        {
            ActorWeaponAnimationSet animationSet =
                profile.GetWeaponAnimationSet(animationSetId);
            if (string.IsNullOrWhiteSpace(animationSet.RecoilStateName))
            {
                return false;
            }

            string layerName =
                ActorAnimationChannelPlan.Recoil.AnimatorLayerName;
            driver.SetLayerWeight(
                layerName,
                animationSet.RecoilLayerWeight);
            driver.RestartState(
                layerName,
                animationSet.RecoilStateName,
                animationSet.RecoilTransitionSeconds);
            return true;
        }
    }
}
