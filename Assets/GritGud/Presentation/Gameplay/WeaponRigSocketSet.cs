using System;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    /// <summary>
    /// Authored attachment contract carried by every equippable weapon rig.
    /// The prefab root is a calibrated weapon pose; the remaining transforms are
    /// model-space sockets edited on the prefab rather than inferred at runtime.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WeaponRigSocketSet : MonoBehaviour
    {
        private const float MaximumMuzzleGripAngleDegrees = 35f;

        [SerializeField]
        private Transform visualRoot;

        [SerializeField]
        private Transform muzzle;

        [SerializeField]
        private Transform rightHandGrip;

        [SerializeField]
        private Vector3 anchorLocalPosition;

        [SerializeField]
        private Vector3 anchorLocalEulerAngles;

        [SerializeField]
        private Transform supportHand;

        [SerializeField]
        private Transform supportElbowHint;

        [SerializeField, Range(0f, 1f)]
        private float supportPositionWeight = 1f;

        [SerializeField, Range(0f, 1f)]
        private float supportRotationWeight = 1f;

        [SerializeField, Range(0f, 1f)]
        private float supportElbowHintWeight = 0.7f;

        [SerializeField, Min(0.01f)]
        private float supportBlendSeconds = 0.12f;

        /// <summary>Primary hand target. Existing rigs safely fall back to root.</summary>
        public Transform RightHandGrip => rightHandGrip != null
            ? rightHandGrip
            : transform;

        public Vector3 AnchorLocalPosition => anchorLocalPosition;

        public Quaternion AnchorLocalRotation => Quaternion.Euler(
            anchorLocalEulerAngles);

        public Transform VisualRoot => visualRoot != null
            ? visualRoot
            : transform;

        public Transform Muzzle => muzzle;

        public Transform SupportHand => supportHand;

        public Transform SupportElbowHint => supportElbowHint;

        public float SupportPositionWeight =>
            Mathf.Clamp01(supportPositionWeight);

        public float SupportRotationWeight =>
            Mathf.Clamp01(supportRotationWeight);

        public float SupportElbowHintWeight =>
            supportElbowHint != null
                ? Mathf.Clamp01(supportElbowHintWeight)
                : 0f;

        public float SupportBlendSeconds =>
            Mathf.Max(0.01f, supportBlendSeconds);

        /// <summary>
        /// Stores the pose captured in the side/top calibration scene. The
        /// runtime solves the anchor from the right-grip socket, so this pose
        /// remains valid without any model-axis convention.
        /// </summary>
        public void SetAnchorCalibration(Vector3 localPosition, Quaternion localRotation)
        {
            anchorLocalPosition = localPosition;
            anchorLocalEulerAngles = localRotation.eulerAngles;
        }

        public void Validate(string owner)
        {
            string label = string.IsNullOrWhiteSpace(owner)
                ? name
                : owner;
            if (muzzle == null)
            {
                throw new InvalidOperationException(
                    $"Weapon rig '{label}' requires an authored muzzle socket.");
            }

            ValidateDescendant(VisualRoot, "visual root", label);
            ValidateDescendant(muzzle, "muzzle", label);
            ValidateDescendant(RightHandGrip, "right-hand grip socket", label);
            Vector3 gripToMuzzle = muzzle.position - RightHandGrip.position;
            if (gripToMuzzle.sqrMagnitude > 0.0001f &&
                Vector3.Angle(muzzle.forward, gripToMuzzle) >
                    MaximumMuzzleGripAngleDegrees)
            {
                throw new InvalidOperationException(
                    $"Weapon rig '{label}' muzzle forward points away from "
                    + "the primary-grip-to-muzzle direction.");
            }
            if (supportHand != null)
            {
                ValidateDescendant(supportHand, "support-hand socket", label);
            }

            if (supportElbowHint != null)
            {
                ValidateDescendant(
                    supportElbowHint,
                    "support-elbow hint",
                    label);
            }
        }

        private void OnValidate()
        {
            supportPositionWeight = Mathf.Clamp01(supportPositionWeight);
            supportRotationWeight = Mathf.Clamp01(supportRotationWeight);
            supportElbowHintWeight = Mathf.Clamp01(supportElbowHintWeight);
            supportBlendSeconds = Mathf.Max(0.01f, supportBlendSeconds);
        }

        private void OnDrawGizmosSelected()
        {
            DrawSocket(muzzle, new Color(1f, 0.35f, 0.05f), 0.08f);
            DrawSocket(RightHandGrip, new Color(0.2f, 1f, 0.25f), 0.065f);
            DrawSocket(supportHand, new Color(0.05f, 0.9f, 1f), 0.065f);
            if (supportElbowHint != null)
            {
                Gizmos.color = new Color(1f, 0.75f, 0.1f);
                Gizmos.DrawWireSphere(supportElbowHint.position, 0.055f);
                if (supportHand != null)
                {
                    Gizmos.DrawLine(
                        supportHand.position,
                        supportElbowHint.position);
                }
            }
        }

        private void ValidateDescendant(
            Transform candidate,
            string socketName,
            string owner)
        {
            if (candidate == null)
            {
                throw new InvalidOperationException(
                    $"Weapon rig '{owner}' requires its {socketName}.");
            }

            if (candidate != transform && !candidate.IsChildOf(transform))
            {
                throw new InvalidOperationException(
                    $"Weapon rig '{owner}' {socketName} must be inside the rig prefab.");
            }
        }

        private static void DrawSocket(
            Transform socket,
            Color color,
            float size)
        {
            if (socket == null)
            {
                return;
            }

            Gizmos.color = color;
            Gizmos.DrawWireSphere(socket.position, size * 0.35f);
            Gizmos.DrawLine(
                socket.position,
                socket.position + socket.forward * size);
            Gizmos.DrawLine(
                socket.position,
                socket.position + socket.up * size * 0.65f);
        }
    }
}
