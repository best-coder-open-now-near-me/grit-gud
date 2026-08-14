using System;
using GritGud.Domain.Gameplay;
using GritGud.Presentation.Actors.Animation;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    internal sealed class MovementRoutePlaybackPresenter
    {
        private const float PlaybackSpeed = 4f;
        private readonly Transform actorTransform;
        private readonly ThirdPersonMotor motor;
        private readonly CharacterController characterController;
        private readonly ActorAnimationCoordinator animationCoordinator;
        private readonly ActorLocomotionAnimationPresenter locomotionPresenter;
        private MovementRouteRecord route;
        private float distance;
        private bool motorWasEnabled;
        private bool controllerWasEnabled;
        private bool locomotionWasEnabled;

        public MovementRoutePlaybackPresenter(ThirdPersonMotor actorMotor)
        {
            motor = actorMotor != null
                ? actorMotor
                : throw new ArgumentNullException(nameof(actorMotor));
            actorTransform = actorMotor.transform;
            characterController = actorMotor.GetComponent<CharacterController>();
            animationCoordinator =
                actorMotor.GetComponent<ActorAnimationCoordinator>();
            locomotionPresenter =
                actorMotor.GetComponent<ActorLocomotionAnimationPresenter>();
        }

        public bool IsPlaying => route != null;

        public float CommittedCost => route?.TotalCost ?? 0f;

        public void Begin(MovementRouteRecord movementRoute)
        {
            if (movementRoute == null)
            {
                throw new ArgumentNullException(nameof(movementRoute));
            }

            if (IsPlaying)
            {
                throw new InvalidOperationException(
                    "A movement route is already playing.");
            }

            route = movementRoute;
            distance = 0f;
            motor.StopPlanarMovement();
            motorWasEnabled = motor.enabled;
            motor.enabled = false;
            if (characterController != null)
            {
                controllerWasEnabled = characterController.enabled;
                characterController.enabled = false;
            }

            if (locomotionPresenter != null)
            {
                locomotionWasEnabled = locomotionPresenter.enabled;
                locomotionPresenter.enabled = false;
            }
        }

        public bool Tick(float deltaTime)
        {
            if (!IsPlaying)
            {
                return false;
            }

            distance = Mathf.Min(
                route.TotalCost,
                distance + (PlaybackSpeed * Mathf.Max(0f, deltaTime)));
            MovementRouteSampling.TrySample(
                route.Points,
                distance,
                out Vector3 position,
                out Vector3 direction);
            Vector3 planarDirection = Vector3.ProjectOnPlane(direction, Vector3.up);
            Quaternion rotation = actorTransform.rotation;
            if (planarDirection.sqrMagnitude > 0.0001f)
            {
                rotation = Quaternion.LookRotation(planarDirection, Vector3.up);
            }

            actorTransform.SetPositionAndRotation(position, rotation);
            if (animationCoordinator != null &&
                animationCoordinator.Profile != null)
            {
                ActorLocomotionAnimationState locomotion =
                    ActorLocomotionAnimationProjector.Project(
                    planarDirection.normalized * PlaybackSpeed,
                    rotation,
                    true,
                    0f,
                    animationCoordinator.Profile.LocomotionReferenceSpeed,
                    animationCoordinator.Profile.TurnReferenceDegreesPerSecond);
                animationCoordinator.PresentFrame(
                    new ActorAnimationFrame(
                        locomotion,
                        animationCoordinator.CurrentStance),
                    deltaTime);
            }

            if (distance < route.TotalCost)
            {
                return false;
            }

            actorTransform.SetPositionAndRotation(
                MovementRouteSampling.ToVector3(route.Destination),
                Quaternion.Euler(0f, route.FinalFacingDegrees, 0f));
            Finish();
            return true;
        }

        public void Cancel()
        {
            if (IsPlaying)
            {
                Finish();
            }
        }

        private void Finish()
        {
            route = null;
            distance = 0f;
            if (characterController != null)
            {
                characterController.enabled = controllerWasEnabled;
            }

            motor.enabled = motorWasEnabled;
            motor.StopPlanarMovement();
            if (locomotionPresenter != null)
            {
                locomotionPresenter.enabled = locomotionWasEnabled;
            }
        }
    }
}
