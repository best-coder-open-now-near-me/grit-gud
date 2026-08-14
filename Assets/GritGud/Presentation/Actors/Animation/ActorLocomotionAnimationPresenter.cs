using GritGud.Presentation.Gameplay;
using UnityEngine;

namespace GritGud.Presentation.Actors.Animation
{
    [DefaultExecutionOrder(ActorAnimationUpdateOrder.LocomotionProjection)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ThirdPersonMotor))]
    [RequireComponent(typeof(ActorAnimationCoordinator))]
    public sealed class ActorLocomotionAnimationPresenter : MonoBehaviour
    {
        [SerializeField]
        private ThirdPersonMotor motor;

        [SerializeField]
        private ActorAnimationCoordinator animationCoordinator;

        private Quaternion lastRenderedRotation;
        private bool hasLastRenderedRotation;
        private ActorTurnInPlaceSignal turnInPlaceSignal;

        public ThirdPersonMotor Motor => motor;

        public ActorAnimationCoordinator AnimationCoordinator =>
            animationCoordinator;

        private void Awake()
        {
            if (motor == null)
            {
                motor = GetComponent<ThirdPersonMotor>();
            }

            if (animationCoordinator == null)
            {
                animationCoordinator = GetComponent<ActorAnimationCoordinator>();
            }

            CaptureRenderedRotation();
        }

        private void OnEnable()
        {
            CaptureRenderedRotation();
        }

        public void Bind(
            ThirdPersonMotor locomotionMotor,
            ActorAnimationCoordinator coordinator)
        {
            motor = locomotionMotor;
            animationCoordinator = coordinator;
            CaptureRenderedRotation();
        }

        private void Update()
        {
            Present(Time.deltaTime);
        }

        public void Present(float deltaTime)
        {
            if (motor == null || animationCoordinator == null ||
                animationCoordinator.Profile == null)
            {
                return;
            }

            ActorLocomotionSnapshot snapshot = motor.CurrentSnapshot;
            Quaternion renderedRotation = transform.rotation;
            float renderedTurnDegreesPerSecond = hasLastRenderedRotation &&
                deltaTime > 0f
                ? ActorRenderedTurnRateProjector.Project(
                    lastRenderedRotation,
                    renderedRotation,
                    deltaTime)
                : snapshot.TurnDegreesPerSecond;
            lastRenderedRotation = renderedRotation;
            hasLastRenderedRotation = true;
            float turnAnimationInput = turnInPlaceSignal.Update(
                renderedTurnDegreesPerSecond,
                animationCoordinator.Profile.TurnReferenceDegreesPerSecond,
                deltaTime,
                animationCoordinator.Profile.TurnInPlace);
            ActorLocomotionAnimationState locomotion =
                ActorLocomotionAnimationProjector.Project(
                snapshot.Velocity,
                renderedRotation,
                snapshot.Grounded,
                turnAnimationInput *
                    animationCoordinator.Profile.TurnReferenceDegreesPerSecond,
                animationCoordinator.Profile.LocomotionReferenceSpeed,
                animationCoordinator.Profile.TurnReferenceDegreesPerSecond);
            animationCoordinator.PresentFrame(
                new ActorAnimationFrame(
                    locomotion,
                    animationCoordinator.CurrentStance),
                deltaTime);
        }

        private void CaptureRenderedRotation()
        {
            lastRenderedRotation = transform.rotation;
            hasLastRenderedRotation = true;
            turnInPlaceSignal.Reset();
        }
    }
}
