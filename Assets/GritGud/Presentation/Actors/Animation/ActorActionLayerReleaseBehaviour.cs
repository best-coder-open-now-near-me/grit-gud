using UnityEngine;

namespace GritGud.Presentation.Actors.Animation
{
    /// <summary>
    /// Releases the action override after its return transition has settled so
    /// the last authored hand pose cannot leak into weapon presentation.
    /// </summary>
    public sealed class ActorActionLayerReleaseBehaviour : StateMachineBehaviour
    {
        public override void OnStateEnter(
            Animator animator,
            AnimatorStateInfo stateInfo,
            int layerIndex) =>
            ReleaseWhenSettled(animator, layerIndex);

        public override void OnStateUpdate(
            Animator animator,
            AnimatorStateInfo stateInfo,
            int layerIndex) =>
            ReleaseWhenSettled(animator, layerIndex);

        private static void ReleaseWhenSettled(
            Animator animator,
            int layerIndex)
        {
            if (animator != null && layerIndex >= 0 &&
                !animator.IsInTransition(layerIndex))
            {
                animator.SetLayerWeight(layerIndex, 0f);
            }
        }
    }
}
