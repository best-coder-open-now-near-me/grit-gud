using System;
using UnityEngine;

namespace GritGud.Presentation.Actors.Animation
{
    public readonly struct ActorLocomotionAnimationState
    {
        internal ActorLocomotionAnimationState(
            float moveX,
            float moveY,
            float speed,
            bool grounded,
            float turnRate)
        {
            MoveX = moveX;
            MoveY = moveY;
            Speed = speed;
            Grounded = grounded;
            TurnRate = turnRate;
        }

        public float MoveX { get; }

        public float MoveY { get; }

        public float Speed { get; }

        public bool Grounded { get; }

        public float TurnRate { get; }
    }

    public static class ActorLocomotionAnimationProjector
    {
        public static ActorLocomotionAnimationState Project(
            Vector3 worldVelocity,
            Quaternion actorRotation,
            bool grounded,
            float turnDegreesPerSecond,
            float locomotionReferenceSpeed,
            float turnReferenceDegreesPerSecond)
        {
            ValidatePositiveFinite(
                locomotionReferenceSpeed,
                nameof(locomotionReferenceSpeed));
            ValidatePositiveFinite(
                turnReferenceDegreesPerSecond,
                nameof(turnReferenceDegreesPerSecond));

            Vector3 horizontalVelocity = Vector3.ProjectOnPlane(worldVelocity, Vector3.up);
            Vector3 forward = Vector3.ProjectOnPlane(
                actorRotation * Vector3.forward,
                Vector3.up);
            if (forward.sqrMagnitude < Mathf.Epsilon)
            {
                forward = Vector3.forward;
            }
            else
            {
                forward.Normalize();
            }

            Vector3 right = Vector3.Cross(Vector3.up, forward);
            var normalizedLocalVelocity = new Vector2(
                Vector3.Dot(horizontalVelocity, right),
                Vector3.Dot(horizontalVelocity, forward)) / locomotionReferenceSpeed;
            if (normalizedLocalVelocity.sqrMagnitude > 1f)
            {
                normalizedLocalVelocity.Normalize();
            }

            float normalizedTurnRate = Mathf.Clamp(
                turnDegreesPerSecond / turnReferenceDegreesPerSecond,
                -1f,
                1f);
            return new ActorLocomotionAnimationState(
                normalizedLocalVelocity.x,
                normalizedLocalVelocity.y,
                horizontalVelocity.magnitude,
                grounded,
                normalizedTurnRate);
        }

        private static void ValidatePositiveFinite(float value, string parameterName)
        {
            if (value <= 0f || float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    "Animation reference values must be positive and finite.");
            }
        }
    }

    public static class ActorRenderedTurnRateProjector
    {
        public static float Project(
            Quaternion previousRotation,
            Quaternion currentRotation,
            float deltaTime)
        {
            if (deltaTime <= 0f || float.IsNaN(deltaTime) ||
                float.IsInfinity(deltaTime))
            {
                return 0f;
            }

            return Mathf.DeltaAngle(
                previousRotation.eulerAngles.y,
                currentRotation.eulerAngles.y) / deltaTime;
        }
    }

    public struct ActorTurnInPlaceSignal
    {
        private float value;
        private float releaseDelayRemaining;
        private bool trackingTurn;

        public float Value => value;

        public float Update(
            float measuredDegreesPerSecond,
            float referenceDegreesPerSecond,
            float deltaTime,
            ActorTurnInPlaceSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (referenceDegreesPerSecond <= 0f ||
                float.IsNaN(referenceDegreesPerSecond) ||
                float.IsInfinity(referenceDegreesPerSecond))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(referenceDegreesPerSecond));
            }

            if (deltaTime <= 0f || float.IsNaN(deltaTime) ||
                float.IsInfinity(deltaTime))
            {
                return value;
            }

            float magnitude = Mathf.Abs(measuredDegreesPerSecond);
            float direction = Mathf.Sign(measuredDegreesPerSecond);
            bool continuingDirection = trackingTurn &&
                Mathf.Sign(value) == direction;
            float threshold = continuingDirection
                ? settings.SustainDegreesPerSecond
                : settings.ActivationDegreesPerSecond;
            if (magnitude >= threshold)
            {
                float normalizedMagnitude = Mathf.Clamp01(
                    magnitude / referenceDegreesPerSecond);
                value = direction * Mathf.Lerp(
                    settings.MinimumActiveBlend,
                    1f,
                    normalizedMagnitude);
                releaseDelayRemaining = settings.ReleaseDelaySeconds;
                trackingTurn = true;
                return value;
            }

            trackingTurn = false;

            if (releaseDelayRemaining > 0f)
            {
                releaseDelayRemaining = Mathf.Max(
                    0f,
                    releaseDelayRemaining - deltaTime);
                return value;
            }

            value = Mathf.MoveTowards(
                value,
                0f,
                deltaTime / settings.ReleaseSeconds);
            return value;
        }

        public void Reset()
        {
            value = 0f;
            releaseDelayRemaining = 0f;
            trackingTurn = false;
        }
    }
}
