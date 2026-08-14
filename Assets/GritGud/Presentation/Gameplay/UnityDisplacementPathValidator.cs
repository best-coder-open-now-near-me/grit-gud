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
            GameplayPosition origin)
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

            Collider[] destinationOverlaps = Physics.OverlapSphere(
                to + castOffset,
                radius,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            foreach (Collider overlap in destinationOverlaps)
            {
                Transform overlapTransform = overlap != null
                    ? overlap.transform
                    : null;
                if (!BelongsTo(overlapTransform, subjectRoot)
                    && !BelongsTo(overlapTransform, actorRoot))
                {
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

        private static bool BelongsTo(Transform candidate, Transform root) =>
            candidate != null
            && root != null
            && (candidate == root || candidate.IsChildOf(root));

        private static Vector3 ToVector3(GameplayPosition position) =>
            new Vector3(position.X, position.Y, position.Z);
    }
}
