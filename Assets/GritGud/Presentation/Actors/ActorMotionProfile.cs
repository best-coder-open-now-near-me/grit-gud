using System;
using GritGud.Domain.Gameplay;
using UnityEngine;

namespace GritGud.Presentation.Actors
{
    [CreateAssetMenu(
        fileName = "ActorMotionProfile",
        menuName = "Grit Gud/Actors/Motion Profile")]
    public sealed class ActorMotionProfile : ScriptableObject
    {
        private const float MinimumPositiveValue = 0.01f;

        [SerializeField, Min(MinimumPositiveValue)]
        private float walkSpeed = 4f;

        [SerializeField, Min(MinimumPositiveValue)]
        private float sprintSpeed = 6.5f;

        [SerializeField, Min(MinimumPositiveValue)]
        private float crouchedSpeed = 2.5f;

        [SerializeField, Min(MinimumPositiveValue)]
        private float acceleration = 24f;

        [SerializeField, Min(MinimumPositiveValue)]
        private float gravityMagnitude = 25f;

        [SerializeField, Min(MinimumPositiveValue)]
        private float groundedDownwardSpeed = 2f;

        [SerializeField, Min(MinimumPositiveValue)]
        private float turnSharpness = 18f;

        [SerializeField, Min(MinimumPositiveValue)]
        private float fallResetDistance = 10f;

        public float WalkSpeed => Mathf.Max(MinimumPositiveValue, walkSpeed);

        public float SprintSpeed =>
            Mathf.Max(MinimumPositiveValue, sprintSpeed);

        public float CrouchedSpeed =>
            Mathf.Max(MinimumPositiveValue, crouchedSpeed);

        public float Acceleration =>
            Mathf.Max(MinimumPositiveValue, acceleration);

        public float GravityMagnitude =>
            Mathf.Max(MinimumPositiveValue, gravityMagnitude);

        public float GroundedDownwardSpeed =>
            Mathf.Max(MinimumPositiveValue, groundedDownwardSpeed);

        public float TurnSharpness =>
            Mathf.Max(MinimumPositiveValue, turnSharpness);

        public float FallResetDistance =>
            Mathf.Max(MinimumPositiveValue, fallResetDistance);

        public float ResolveMovementSpeed(
            bool sprint,
            ActorStance stance,
            float movementSpeedMultiplier = 1f)
        {
            if (float.IsNaN(movementSpeedMultiplier) ||
                float.IsInfinity(movementSpeedMultiplier) ||
                movementSpeedMultiplier <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(movementSpeedMultiplier));
            }

            float baseSpeed = stance == ActorStance.Crouched
                ? CrouchedSpeed
                : sprint ? SprintSpeed : WalkSpeed;
            return baseSpeed * movementSpeedMultiplier;
        }

        private void OnValidate()
        {
            walkSpeed = Mathf.Max(MinimumPositiveValue, walkSpeed);
            sprintSpeed = Mathf.Max(MinimumPositiveValue, sprintSpeed);
            crouchedSpeed = Mathf.Max(MinimumPositiveValue, crouchedSpeed);
            acceleration = Mathf.Max(MinimumPositiveValue, acceleration);
            gravityMagnitude = Mathf.Max(
                MinimumPositiveValue,
                gravityMagnitude);
            groundedDownwardSpeed = Mathf.Max(
                MinimumPositiveValue,
                groundedDownwardSpeed);
            turnSharpness = Mathf.Max(
                MinimumPositiveValue,
                turnSharpness);
            fallResetDistance = Mathf.Max(
                MinimumPositiveValue,
                fallResetDistance);
        }
    }
}
