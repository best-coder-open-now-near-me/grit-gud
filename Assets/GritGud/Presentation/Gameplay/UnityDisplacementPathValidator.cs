using System;
using System.Collections.Generic;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    public sealed class UnityDisplacementPathValidator : IDisplacementPathValidator
    {
        private const float MinimumRadius = 0.15f;
        private const float MaximumRadius = 0.5f;
        private const float GroundClearance = 0.04f;

        private readonly IReadOnlyDictionary<string, Transform> subjectRoots;

        public UnityDisplacementPathValidator(
            IReadOnlyDictionary<string, Transform> subjects)
        {
            subjectRoots = subjects ??
                throw new ArgumentNullException(nameof(subjects));
        }

        public DisplacementPathValidation Validate(
            DisplacementRequest request,
            GameplayPosition origin,
            PropDisplacementState resultingPropState)
        {
            Transform subjectRoot = RequireRoot(request.SubjectId);
            Transform actorRoot = RequireRoot(request.ActorId);
            Vector3 from = ToVector3(origin);
            Vector3 to = ToVector3(request.Destination);
            float radius = ResolveRadius(subjectRoot);
            Vector3 castOffset = Vector3.up * (radius + GroundClearance);
            Vector3 direction = to - from;
            float distance = direction.magnitude;
            if (distance <= 0.001f)
            {
                return DisplacementPathValidation.Blocked(
                    "displacement.destination-unchanged");
            }

            RaycastHit[] pathHits = Physics.SphereCastAll(
                from + castOffset,
                radius,
                direction / distance,
                distance,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            foreach (RaycastHit hit in pathHits)
            {
                Transform hitTransform = hit.collider != null
                    ? hit.collider.transform
                    : null;
                if (!BelongsTo(hitTransform, subjectRoot)
                    && !BelongsTo(hitTransform, actorRoot))
                {
                    return DisplacementPathValidation.Blocked(
                        "displacement.path-blocked");
                }
            }

            Collider[] destinationOverlaps;
            float destinationBottom;
            if (resultingPropState != null)
            {
                Bounds localBounds = ResolveLocalBounds(subjectRoot);
                Quaternion resultingRotation = Quaternion.Euler(
                    resultingPropState.Pose.PitchDegrees,
                    resultingPropState.Pose.YawDegrees,
                    resultingPropState.Pose.RollDegrees);
                Vector3 scale = Abs(subjectRoot.lossyScale);
                Vector3 halfExtents = Vector3.Scale(
                    localBounds.extents,
                    scale);
                Vector3 center = to
                    + (resultingRotation
                        * Vector3.Scale(localBounds.center, scale));
                destinationBottom = center.y
                    - ProjectedVerticalExtent(halfExtents, resultingRotation);
                destinationOverlaps = Physics.OverlapBox(
                    center,
                    Shrink(halfExtents),
                    resultingRotation,
                    Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore);
            }
            else
            {
                destinationBottom = to.y;
                destinationOverlaps = Physics.OverlapSphere(
                    to + castOffset,
                    radius,
                    Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore);
            }
            foreach (Collider overlap in destinationOverlaps)
            {
                Transform overlapTransform = overlap != null
                    ? overlap.transform
                    : null;
                if (!BelongsTo(overlapTransform, subjectRoot)
                    && !BelongsTo(overlapTransform, actorRoot))
                {
                    if (resultingPropState != null
                        && overlap.bounds.max.y
                            <= destinationBottom + GroundClearance)
                    {
                        continue;
                    }

                    return DisplacementPathValidation.Blocked(
                        "displacement.destination-blocked");
                }
            }

            return DisplacementPathValidation.Allowed();
        }

        private Transform RequireRoot(string subjectId)
        {
            if (!subjectRoots.TryGetValue(subjectId ?? string.Empty, out var root)
                || root == null)
            {
                throw new InvalidOperationException(
                    $"Displacement subject '{subjectId}' has no scene transform.");
            }

            return root;
        }

        private static float ResolveRadius(Transform subjectRoot)
        {
            Collider[] colliders = subjectRoot.GetComponentsInChildren<Collider>();
            float radius = MinimumRadius;
            foreach (Collider subjectCollider in colliders)
            {
                if (subjectCollider == null || !subjectCollider.enabled)
                {
                    continue;
                }

                Bounds bounds = subjectCollider.bounds;
                radius = Mathf.Max(
                    radius,
                    Mathf.Min(bounds.extents.x, bounds.extents.z));
            }

            return Mathf.Clamp(radius, MinimumRadius, MaximumRadius);
        }

        private static Bounds ResolveLocalBounds(Transform subjectRoot)
        {
            Collider[] colliders = subjectRoot.GetComponentsInChildren<Collider>();
            bool initialized = false;
            Bounds localBounds = default(Bounds);
            foreach (Collider subjectCollider in colliders)
            {
                if (subjectCollider == null || !subjectCollider.enabled)
                    continue;

                Bounds worldBounds = subjectCollider.bounds;
                Vector3 min = worldBounds.min;
                Vector3 max = worldBounds.max;
                for (int x = 0; x < 2; x++)
                {
                    for (int y = 0; y < 2; y++)
                    {
                        for (int z = 0; z < 2; z++)
                        {
                            Vector3 localPoint = subjectRoot.InverseTransformPoint(
                                new Vector3(
                                    x == 0 ? min.x : max.x,
                                    y == 0 ? min.y : max.y,
                                    z == 0 ? min.z : max.z));
                            if (!initialized)
                            {
                                localBounds = new Bounds(localPoint, Vector3.zero);
                                initialized = true;
                            }
                            else
                            {
                                localBounds.Encapsulate(localPoint);
                            }
                        }
                    }
                }
            }

            return initialized
                ? localBounds
                : new Bounds(Vector3.up * MinimumRadius, Vector3.one * MinimumRadius * 2f);
        }

        private static float ProjectedVerticalExtent(
            Vector3 halfExtents,
            Quaternion rotation)
        {
            Vector3 right = rotation * Vector3.right;
            Vector3 up = rotation * Vector3.up;
            Vector3 forward = rotation * Vector3.forward;
            return Mathf.Abs(right.y) * halfExtents.x
                + Mathf.Abs(up.y) * halfExtents.y
                + Mathf.Abs(forward.y) * halfExtents.z;
        }

        private static Vector3 Abs(Vector3 value) =>
            new Vector3(
                Mathf.Abs(value.x),
                Mathf.Abs(value.y),
                Mathf.Abs(value.z));

        private static Vector3 Shrink(Vector3 halfExtents) =>
            new Vector3(
                Mathf.Max(0.01f, halfExtents.x - GroundClearance),
                Mathf.Max(0.01f, halfExtents.y - GroundClearance),
                Mathf.Max(0.01f, halfExtents.z - GroundClearance));

        private static bool BelongsTo(Transform candidate, Transform root) =>
            candidate != null
            && root != null
            && (candidate == root || candidate.IsChildOf(root));

        private static Vector3 ToVector3(GameplayPosition position) =>
            new Vector3(position.X, position.Y, position.Z);
    }
}
