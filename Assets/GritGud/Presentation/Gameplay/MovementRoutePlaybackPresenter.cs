using System;
using GritGud.Domain.Gameplay;
using GritGud.Presentation.Actors.Animation;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    internal sealed class MovementRoutePlaybackPresenter
    {
        private readonly Transform actorTransform;
        private readonly ThirdPersonMotor motor;
        private readonly CharacterController characterController;
        private readonly ActorAnimationCoordinator animationCoordinator;
        private readonly ActorLocomotionAnimationPresenter locomotionPresenter;
        private MovementRouteRecord route;
        private float elapsedSeconds;
        private int presentedTraversalSegment = -1;
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
            elapsedSeconds = 0f;
            presentedTraversalSegment = -1;
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

            elapsedSeconds = Mathf.Min(
                route.TotalPlaybackDurationSeconds,
                elapsedSeconds + Mathf.Max(0f, deltaTime));
            MovementRouteSampling.TrySample(
                route,
                elapsedSeconds,
                out Vector3 position,
                out Vector3 direction,
                out int segmentIndex,
                out _);
            MovementRouteSegmentRecord segment = route.Segments[segmentIndex];
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
                if (segment.IsTraversal
                    && presentedTraversalSegment != segmentIndex)
                {
                    animationCoordinator.TryRequestAction(
                        ActorAnimationAction.Jump);
                    presentedTraversalSegment = segmentIndex;
                }
                ActorLocomotionAnimationState locomotion =
                    ActorLocomotionAnimationProjector.Project(
                    planarDirection.normalized * 4f,
                    rotation,
                    !segment.IsTraversal,
                    0f,
                    animationCoordinator.Profile.LocomotionReferenceSpeed,
                    animationCoordinator.Profile.TurnReferenceDegreesPerSecond);
                animationCoordinator.PresentFrame(
                    new ActorAnimationFrame(
                        locomotion,
                        animationCoordinator.CurrentStance),
                    deltaTime);
            }

            if (elapsedSeconds < route.TotalPlaybackDurationSeconds)
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
            elapsedSeconds = 0f;
            presentedTraversalSegment = -1;
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
