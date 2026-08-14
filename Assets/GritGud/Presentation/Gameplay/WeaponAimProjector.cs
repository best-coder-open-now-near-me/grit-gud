using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    internal static class WeaponAimProjector
    {
        private const float DirectionTolerance = 0.0001f;

        public static Quaternion CalculateCorrection(
            Vector3 currentBarrelDirection,
            Vector3 desiredAimDirection,
            float maximumDegrees)
        {
            if (currentBarrelDirection.sqrMagnitude <= DirectionTolerance
                || desiredAimDirection.sqrMagnitude <= DirectionTolerance)
            {
                return Quaternion.identity;
            }

            Quaternion correction = Quaternion.FromToRotation(
                currentBarrelDirection.normalized,
                desiredAimDirection.normalized);
            float angle = Quaternion.Angle(Quaternion.identity, correction);
            float limit = Mathf.Clamp(maximumDegrees, 0f, 180f);
            if (angle <= limit || angle <= Mathf.Epsilon)
            {
                return correction;
            }

            return Quaternion.Slerp(
                Quaternion.identity,
                correction,
                limit / angle);
        }

        public static Quaternion CalculatePitchCorrection(
            Vector3 currentBarrelDirection,
            Vector3 desiredAimDirection,
            Vector3 upDirection,
            float maximumDegrees)
        {
            if (currentBarrelDirection.sqrMagnitude <= DirectionTolerance
                || desiredAimDirection.sqrMagnitude <= DirectionTolerance
                || upDirection.sqrMagnitude <= DirectionTolerance)
            {
                return Quaternion.identity;
            }

            Vector3 up = upDirection.normalized;
            Vector3 currentHorizontal = Vector3.ProjectOnPlane(
                currentBarrelDirection,
                up);
            Vector3 desiredHorizontal = Vector3.ProjectOnPlane(
                desiredAimDirection,
                up);
            if (currentHorizontal.sqrMagnitude <= DirectionTolerance
                || desiredHorizontal.sqrMagnitude <= DirectionTolerance)
            {
                return Quaternion.identity;
            }

            // Unity's positive rotation around the actor's right axis pitches
            // forward down. Use the opposite (left) axis so a positive
            // elevation delta raises the upper chest instead of hunching it.
            Vector3 pitchAxis = Vector3.Cross(
                currentHorizontal.normalized,
                up).normalized;
            float currentElevation = CalculateElevationDegrees(
                currentBarrelDirection,
                up);
            float desiredElevation = CalculateElevationDegrees(
                desiredAimDirection,
                up);
            float correction = Mathf.Clamp(
                desiredElevation - currentElevation,
                -Mathf.Abs(maximumDegrees),
                Mathf.Abs(maximumDegrees));
            return Quaternion.AngleAxis(correction, pitchAxis);
        }

        public static Quaternion CalculateYawCorrection(
            Vector3 currentBarrelDirection,
            Vector3 desiredAimDirection,
            Vector3 upDirection,
            float maximumDegrees = 180f)
        {
            if (currentBarrelDirection.sqrMagnitude <= DirectionTolerance
                || desiredAimDirection.sqrMagnitude <= DirectionTolerance
                || upDirection.sqrMagnitude <= DirectionTolerance)
            {
                return Quaternion.identity;
            }

            Vector3 up = upDirection.normalized;
            Vector3 currentHorizontal = Vector3.ProjectOnPlane(
                currentBarrelDirection,
                up);
            Vector3 desiredHorizontal = Vector3.ProjectOnPlane(
                desiredAimDirection,
                up);
            if (currentHorizontal.sqrMagnitude <= DirectionTolerance
                || desiredHorizontal.sqrMagnitude <= DirectionTolerance)
            {
                return Quaternion.identity;
            }

            float yawDegrees = Vector3.SignedAngle(
                currentHorizontal,
                desiredHorizontal,
                up);
            yawDegrees = Mathf.Clamp(
                yawDegrees,
                -Mathf.Abs(maximumDegrees),
                Mathf.Abs(maximumDegrees));
            return Quaternion.AngleAxis(yawDegrees, up);
        }

        private static float CalculateElevationDegrees(
            Vector3 direction,
            Vector3 up)
        {
            Vector3 normalized = direction.normalized;
            return Mathf.Atan2(
                    Vector3.Dot(normalized, up),
                    Vector3.ProjectOnPlane(normalized, up).magnitude)
                * Mathf.Rad2Deg;
        }
    }
}
