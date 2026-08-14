using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    /// <summary>
    /// Editor-scene helper for authoring a two-handed weapon pose. It solves
    /// the left arm directly in edit mode so moving a weapon grip socket gives
    /// immediate visual feedback without entering Play mode.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class LiveWeaponCalibrationPreview : MonoBehaviour
    {
        private const float Epsilon = 0.00001f;

        [SerializeField]
        private Transform root;

        [SerializeField]
        private Transform mid;

        [SerializeField]
        private Transform tip;

        [SerializeField]
        private Transform target;

        [SerializeField]
        private Transform hint;

        public void Configure(
            Transform upperArm,
            Transform lowerArm,
            Transform hand,
            Transform gripSocket,
            Transform elbowHint)
        {
            root = upperArm;
            mid = lowerArm;
            tip = hand;
            target = gripSocket;
            hint = elbowHint;
        }

        /// <summary>
        /// Applies one edit-mode preview sample on demand. Keeping this out of
        /// ExecuteAlways update loops avoids continuously dirtying the scene
        /// while the user adjusts a socket.
        /// </summary>
        public void PreviewNow()
        {
            if (!UnityEngine.Application.isPlaying)
            {
                Solve();
            }
        }

        private void OnDrawGizmos()
        {
            if (target == null || tip == null)
            {
                return;
            }

            Gizmos.color = new Color(0.1f, 1f, 0.9f, 0.85f);
            Gizmos.DrawLine(tip.position, target.position);
        }

        private void Solve()
        {
            if (root == null || mid == null || tip == null || target == null)
            {
                return;
            }

            Vector3 rootPosition = root.position;
            Vector3 midPosition = mid.position;
            Vector3 tipPosition = tip.position;
            float upperLength = Vector3.Distance(rootPosition, midPosition);
            float lowerLength = Vector3.Distance(midPosition, tipPosition);
            Vector3 toTarget = target.position - rootPosition;
            float targetDistance = toTarget.magnitude;
            if (upperLength < Epsilon || lowerLength < Epsilon
                || targetDistance < Epsilon)
            {
                return;
            }

            Vector3 direction = toTarget / targetDistance;
            float distance = Mathf.Clamp(
                targetDistance,
                Mathf.Abs(upperLength - lowerLength) + Epsilon,
                upperLength + lowerLength - Epsilon);
            float along = (upperLength * upperLength - lowerLength * lowerLength
                + distance * distance) / (2f * distance);
            float height = Mathf.Sqrt(Mathf.Max(
                0f,
                upperLength * upperLength - along * along));
            Vector3 hintDirection = hint != null
                ? hint.position - rootPosition
                : midPosition - rootPosition;
            Vector3 bendDirection = Vector3.ProjectOnPlane(
                hintDirection,
                direction);
            if (bendDirection.sqrMagnitude < Epsilon)
            {
                bendDirection = Vector3.Cross(direction, transform.up);
            }

            bendDirection.Normalize();
            Vector3 solvedMidPosition = rootPosition + direction * along
                + bendDirection * height;

            RotateBoneToward(root, midPosition - rootPosition,
                solvedMidPosition - rootPosition);
            RotateBoneToward(mid, tip.position - mid.position,
                target.position - mid.position);
            tip.SetPositionAndRotation(target.position, target.rotation);
        }

        private static void RotateBoneToward(
            Transform bone,
            Vector3 currentDirection,
            Vector3 desiredDirection)
        {
            if (currentDirection.sqrMagnitude < Epsilon
                || desiredDirection.sqrMagnitude < Epsilon)
            {
                return;
            }

            bone.rotation = Quaternion.FromToRotation(
                currentDirection,
                desiredDirection) * bone.rotation;
        }
    }
}
