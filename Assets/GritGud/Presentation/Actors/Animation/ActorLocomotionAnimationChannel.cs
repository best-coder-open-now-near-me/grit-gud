using GritGud.Domain.Gameplay;
using UnityEngine;

namespace GritGud.Presentation.Actors.Animation
{
    internal sealed class ActorLocomotionAnimationChannel
    {
        private bool turnLayerWasActive;
        private int turnLayerDirection;

        public void Reset()
        {
            turnLayerWasActive = false;
            turnLayerDirection = 0;
        }

        public void Present(
            ActorAnimationFrame frame,
            ActorAnimationProfile profile,
            float deltaTime,
            AnimatorDriver driver)
        {
            ActorTurnInPlaceSettings turn = profile.TurnInPlace;
            ActorLocomotionAnimationState state = frame.Locomotion;
            driver.SetFloat(
                ActorAnimationParameters.MoveX,
                state.MoveX,
                profile.ParameterDampTime,
                deltaTime);
            driver.SetFloat(
                ActorAnimationParameters.MoveY,
                state.MoveY,
                profile.ParameterDampTime,
                deltaTime);
            driver.SetFloat(
                ActorAnimationParameters.Speed,
                state.Speed,
                profile.ParameterDampTime,
                deltaTime);
            float animatorTurnRate = Mathf.Clamp(
                state.TurnRate,
                -turn.MaximumPoseBlend,
                turn.MaximumPoseBlend);
            driver.SetFloat(
                ActorAnimationParameters.TurnRate,
                animatorTurnRate,
                0f,
                deltaTime);
            driver.SetBool(ActorAnimationParameters.Grounded, state.Grounded);
            bool canTurnInPlace = state.Grounded &&
                state.Speed <= turn.MaximumMovementSpeed &&
                frame.Stance == ActorStance.Standing;
            float turnLayerWeight = canTurnInPlace
                ? turn.MaximumLayerWeight *
                    Mathf.Clamp01(
                        Mathf.Abs(state.TurnRate) /
                        turn.MinimumActiveBlend)
                : 0f;
            PresentTurnInPlace(driver, turnLayerWeight, animatorTurnRate);
        }

        private void PresentTurnInPlace(
            AnimatorDriver driver,
            float weight,
            float turnRate)
        {
            AnimationChannelDefinition channel =
                ActorAnimationChannelPlan.TurnInPlace;
            bool active = weight > 0f;
            int direction = active ? (turnRate > 0f ? 1 : -1) : 0;
            if (active &&
                (!turnLayerWasActive || direction != turnLayerDirection))
            {
                driver.PlayState(
                    channel.AnimatorLayerName,
                    ActorAnimationParameters.TurnInPlaceState,
                    0f);
            }

            driver.SetLayerWeight(channel.AnimatorLayerName, weight);
            turnLayerWasActive = active;
            turnLayerDirection = direction;
        }
    }
}
