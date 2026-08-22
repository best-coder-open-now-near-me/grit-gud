using GritGud.Domain.Gameplay;
using GritGud.Presentation.Actors;
using GritGud.Presentation.Actors.Animation;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    [DefaultExecutionOrder(ActorAnimationUpdateOrder.Motor)]
    [RequireComponent(typeof(CharacterController))]
    public sealed class ThirdPersonMotor : MonoBehaviour
    {
        private CharacterController controller;
        private ActorStancePresenter stancePresenter;

        [SerializeField]
        private ActorMotionProfile motionProfile;

        [SerializeField]
        private MonoBehaviour movementCommandSource;
        private IActorMovementCommandSource commandSource;
        private Vector3 horizontalVelocity;
        private float verticalVelocity;
        private float turnDegreesPerSecond;
        private float movementSpeedMultiplier = 1f;
        private ActorMobilityCapability mobilityCapability =
            new ActorMobilityCapability(
                ActorGait.Normal,
                ActorImpairedSide.None,
                100,
                100,
                canSprint: true,
                canStand: true);
        private Vector3 respawnPoint;

        public Vector3 Velocity => horizontalVelocity + (Vector3.up * verticalVelocity);

        public bool IsGrounded => controller != null && controller.isGrounded;

        public float TurnDegreesPerSecond => turnDegreesPerSecond;

        public IActorMovementCommandSource MovementCommandSource =>
            commandSource ?? movementCommandSource as IActorMovementCommandSource;

        public ActorLocomotionSnapshot CurrentSnapshot { get; private set; }

        public float MovementSpeedMultiplier => movementSpeedMultiplier;

        public ActorMobilityCapability MobilityCapability =>
            mobilityCapability;

        public float EffectiveMovementSpeedMultiplier =>
            movementSpeedMultiplier
            * mobilityCapability.MovementPercent / 100f;

        public ActorMotionProfile MotionProfile => motionProfile;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            stancePresenter = GetComponent<ActorStancePresenter>();
            commandSource = movementCommandSource as IActorMovementCommandSource;
            if (commandSource == null)
            {
                commandSource = GetComponent<IActorMovementCommandSource>();
            }

            respawnPoint = transform.position;
            CaptureSnapshot();
        }

        public void SetRespawnPoint(Vector3 position)
        {
            respawnPoint = position;
        }

        public void StopPlanarMovement()
        {
            horizontalVelocity = Vector3.zero;
            turnDegreesPerSecond = 0f;
            CaptureSnapshot();
        }

        public void BindCommandSource(IActorMovementCommandSource source)
        {
            commandSource = source;
            movementCommandSource = source as MonoBehaviour;
        }

        public void SetMovementSpeedMultiplier(float multiplier)
        {
            if (float.IsNaN(multiplier)
                || float.IsInfinity(multiplier)
                || multiplier <= 0f)
            {
                throw new System.ArgumentOutOfRangeException(nameof(multiplier));
            }

            movementSpeedMultiplier = multiplier;
        }

        public void SetMobilityCapability(
            ActorMobilityCapability capability)
        {
            mobilityCapability = capability
                ?? throw new System.ArgumentNullException(nameof(capability));
            if (mobilityCapability.MovementPercent == 0)
                StopPlanarMovement();
        }

        private void Update()
        {
            ActorMotionProfile motion = RequireMotionProfile();
            if (transform.position.y <
                respawnPoint.y - motion.FallResetDistance)
            {
                controller.enabled = false;
                transform.position = respawnPoint;
                controller.enabled = true;
                horizontalVelocity = Vector3.zero;
                verticalVelocity = 0f;
                turnDegreesPerSecond = 0f;
            }

            ActorMovementCommand command = commandSource != null
                ? commandSource.ReadMovementCommand()
                : default;
            Vector3 desiredDirection = command.WorldDirection;
            if (stancePresenter == null)
            {
                stancePresenter = GetComponent<ActorStancePresenter>();
            }

            ActorStance stance = stancePresenter != null
                ? stancePresenter.Stance
                : ActorStance.Standing;
            float speed = motion.ResolveMovementSpeed(
                command.Sprint && mobilityCapability.CanSprint,
                stance,
                EffectiveMovementSpeedMultiplier);
            Vector3 desiredVelocity = desiredDirection * speed;
            horizontalVelocity = Vector3.MoveTowards(
                horizontalVelocity,
                desiredVelocity,
                motion.Acceleration * Time.deltaTime);

            float previousYaw = transform.eulerAngles.y;
            if (desiredDirection.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(desiredDirection, Vector3.up);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    targetRotation,
                    1f - Mathf.Exp(-motion.TurnSharpness * Time.deltaTime));
            }

            turnDegreesPerSecond = Time.deltaTime > 0f
                ? Mathf.DeltaAngle(previousYaw, transform.eulerAngles.y) / Time.deltaTime
                : 0f;

            if (controller.isGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = -motion.GroundedDownwardSpeed;
            }
            else
            {
                verticalVelocity -= motion.GravityMagnitude * Time.deltaTime;
            }

            controller.Move(Velocity * Time.deltaTime);
            CaptureSnapshot();
        }

        public void BindMotionProfile(ActorMotionProfile profile)
        {
            motionProfile = profile ?? throw new System.ArgumentNullException(
                nameof(profile));
        }

        private ActorMotionProfile RequireMotionProfile()
        {
            return motionProfile != null
                ? motionProfile
                : throw new System.InvalidOperationException(
                    $"Actor '{name}' requires an authored "
                    + $"{nameof(ActorMotionProfile)}.");
        }

        private void CaptureSnapshot()
        {
            CurrentSnapshot = new ActorLocomotionSnapshot(
                Velocity,
                transform.rotation,
                IsGrounded,
                turnDegreesPerSecond);
        }
    }
}
