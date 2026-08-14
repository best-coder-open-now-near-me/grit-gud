using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class ExplorationMovementInput : MonoBehaviour, IActorMovementCommandSource
    {
        [SerializeField]
        private Transform viewTransform;
        private IGameplayInputSource inputSource;
        private bool inputEnabled = true;

        public Transform ViewTransform => viewTransform;

        public bool InputEnabled => inputEnabled;

        public IGameplayInputSource InputSource => inputSource;

        public void BindInputSource(IGameplayInputSource source)
        {
            inputSource = source;
        }

        public void BindView(Transform view)
        {
            viewTransform = view;
        }

        public void SetInputEnabled(bool value)
        {
            inputEnabled = value;
        }

        public ActorMovementCommand ReadMovementCommand()
        {
            if (!inputEnabled)
            {
                return default;
            }

            return ReadCameraRelativeCommand();
        }

        public ActorMovementCommand ReadCameraRelativeCommand()
        {
            if (!isActiveAndEnabled || inputSource == null || viewTransform == null)
            {
                return default;
            }

            GameplayInputFrame frame = inputSource.CurrentFrame;
            Vector2 input = frame.Movement;

            Vector3 forward = Vector3.ProjectOnPlane(viewTransform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(viewTransform.right, Vector3.up).normalized;
            Vector3 worldDirection = (right * input.x) + (forward * input.y);
            return new ActorMovementCommand(
                worldDirection,
                frame.SprintHeld);
        }
    }
}
