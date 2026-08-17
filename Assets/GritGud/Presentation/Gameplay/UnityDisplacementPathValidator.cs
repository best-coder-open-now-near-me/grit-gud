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
                    if (resultingPropState != null
                        && TryResolveSubjectId(
                            hitTransform,
                            out _)
                        && Vector3.Distance(hit.point, to)
                            <= radius * 2f)
                    {
                        continue;
                    }
                    return DisplacementPathValidation.Blocked(
                        "displacement.path-blocked");
                }
            }

            Collider[] destinationOverlaps;
            Vector3 destinationCenter;
            float destinationBottom;
            if (resultingPropState != null)
            {
                UnityDisplacementOrientedBounds bounds =
                    UnityDisplacementGeometry.ResolveOrientedBounds(
                        subjectRoot,
                        resultingPropState.Pose);
                destinationBottom = bounds.Center.y
                    - UnityDisplacementGeometry.ProjectedVerticalExtent(
                        bounds.HalfExtents,
                        bounds.Rotation);
                destinationCenter = bounds.Center;
                destinationOverlaps = Physics.OverlapBox(
                    bounds.Center,
                    UnityDisplacementGeometry.Shrink(bounds.HalfExtents),
                    bounds.Rotation,
                    Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore);
            }
            else
            {
                destinationBottom = to.y;
                destinationCenter = to + castOffset;
                destinationOverlaps = Physics.OverlapSphere(
                    to + castOffset,
                    radius,
                    Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore);
            }
            var contacts = new Dictionary<string, DisplacementContactEvidence>(
                StringComparer.Ordinal);
            foreach (Collider overlap in destinationOverlaps)
            {
                Transform overlapTransform = overlap != null
                    ? overlap.transform
                    : null;
                if (BelongsTo(overlapTransform, actorRoot))
                {
                    if (request.ActionKind == DisplacementActionKind.PushOff)
                    {
                        return DisplacementPathValidation.Blocked(
                            DisplacementPathValidation
                                .GetUpSpaceBlockedFailureCode);
                    }
                    continue;
                }
                if (!BelongsTo(overlapTransform, subjectRoot)
                    && !BelongsTo(overlapTransform, actorRoot))
                {
                    if (resultingPropState != null
                        && TryResolveSubjectId(
                            overlapTransform,
                            out string contactEntityId))
                    {
                        DisplacementContactEvidence contact = CreateContact(
                            contactEntityId,
                            overlap,
                            destinationCenter);
                        if (!contacts.TryGetValue(
                                contactEntityId,
                                out DisplacementContactEvidence current)
                            || contact.OverlapDepth > current.OverlapDepth)
                        {
                            contacts[contactEntityId] = contact;
                        }
                        continue;
                    }
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

            if (request.ActionKind == DisplacementActionKind.PushOff
                && !HasGetUpClearance(actorRoot, subjectRoot))
            {
                return DisplacementPathValidation.Blocked(
                    DisplacementPathValidation.GetUpSpaceBlockedFailureCode);
            }

            return contacts.Count == 0
                ? DisplacementPathValidation.Allowed()
                : DisplacementPathValidation.Allowed(contacts.Values);
        }

        private bool TryResolveSubjectId(
            Transform candidate,
            out string subjectId)
        {
            foreach (KeyValuePair<string, Transform> entry in subjectRoots)
            {
                if (BelongsTo(candidate, entry.Value))
                {
                    subjectId = entry.Key;
                    return true;
                }
            }

            subjectId = null;
            return false;
        }

        private static DisplacementContactEvidence CreateContact(
            string entityId,
            Collider collider,
            Vector3 destinationCenter)
        {
            Vector3 point = collider.ClosestPoint(destinationCenter);
            Vector3 offset = destinationCenter - point;
            float depth = offset.magnitude;
            Vector3 normal = depth > 0.0001f
                ? offset / depth
                : (destinationCenter - collider.bounds.center).normalized;
            if (normal.sqrMagnitude <= 0.0001f)
                normal = Vector3.up;
            return new DisplacementContactEvidence(
                entityId,
                ToGameplayPosition(point),
                ToGameplayPosition(normal),
                depth > 0.0001f ? depth : GroundClearance);
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

        private static bool HasGetUpClearance(
            Transform actorRoot,
            Transform movingSubjectRoot)
        {
            CharacterController controller =
                actorRoot.GetComponent<CharacterController>();
            if (controller == null)
                return true;

            Vector3 scale = actorRoot.lossyScale;
            float radiusScale = Mathf.Max(
                Mathf.Abs(scale.x),
                Mathf.Abs(scale.z));
            float heightScale = Mathf.Abs(scale.y);
            float radius = controller.radius * radiusScale;
            float height = Mathf.Max(
                controller.height * heightScale,
                radius * 2f);
            float collisionRadius = Mathf.Max(
                radius * 0.75f,
                radius - (controller.skinWidth * 0.5f));
            Vector3 center = actorRoot.position
                + (actorRoot.rotation
                    * Vector3.Scale(controller.center, scale));
            float halfCylinder = Mathf.Max(
                0f,
                (height * 0.5f) - radius);
            Vector3 top = center + (Vector3.up * halfCylinder);
            Vector3 bottom = center - (Vector3.up * halfCylinder);
            float standingBottom = bottom.y - collisionRadius;
            Collider[] overlaps = Physics.OverlapCapsule(
                top,
                bottom,
                collisionRadius,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            foreach (Collider overlap in overlaps)
            {
                Transform overlapTransform = overlap != null
                    ? overlap.transform
                    : null;
                if (BelongsTo(overlapTransform, actorRoot)
                    || BelongsTo(overlapTransform, movingSubjectRoot))
                {
                    continue;
                }

                if (overlap != null
                    && overlap.bounds.max.y
                        <= standingBottom + GroundClearance)
                    continue;
                return false;
            }

            return true;
        }

        private static bool BelongsTo(Transform candidate, Transform root) =>
            candidate != null
            && root != null
            && (candidate == root || candidate.IsChildOf(root));

        private static Vector3 ToVector3(GameplayPosition position) =>
            new Vector3(position.X, position.Y, position.Z);

        private static GameplayPosition ToGameplayPosition(Vector3 value) =>
            new GameplayPosition(value.x, value.y, value.z);
    }

    internal readonly struct UnityDisplacementOrientedBounds
    {
        public UnityDisplacementOrientedBounds(
            Vector3 center,
            Vector3 halfExtents,
            Quaternion rotation)
        {
            Center = center;
            HalfExtents = halfExtents;
            Rotation = rotation;
        }

        public Vector3 Center { get; }
        public Vector3 HalfExtents { get; }
        public Quaternion Rotation { get; }
    }

    internal static class UnityDisplacementGeometry
    {
        private const float MinimumRadius = 0.15f;
        private const float GroundClearance = 0.04f;

        public static UnityDisplacementOrientedBounds ResolveOrientedBounds(
            Transform subjectRoot,
            GameplayPropPose pose)
        {
            if (subjectRoot == null)
                throw new ArgumentNullException(nameof(subjectRoot));
            Quaternion rotation = Quaternion.Euler(
                pose.PitchDegrees,
                pose.YawDegrees,
                pose.RollDegrees);
            return ResolveOrientedBounds(
                subjectRoot,
                new Vector3(
                    pose.Position.X,
                    pose.Position.Y,
                    pose.Position.Z),
                rotation);
        }

        public static UnityDisplacementOrientedBounds ResolveOrientedBounds(
            Transform subjectRoot,
            Vector3 rootPosition,
            Quaternion rotation)
        {
            if (subjectRoot == null)
                throw new ArgumentNullException(nameof(subjectRoot));
            Vector3 scale = Abs(subjectRoot.lossyScale);
            Bounds localBounds = ResolveLocalBounds(subjectRoot);
            Vector3 halfExtents = Vector3.Scale(localBounds.extents, scale);
            Vector3 center = rootPosition
                + (rotation * Vector3.Scale(localBounds.center, scale));
            return new UnityDisplacementOrientedBounds(
                center,
                halfExtents,
                rotation);
        }

        public static float ProjectedVerticalExtent(
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

        public static Vector3 Shrink(Vector3 halfExtents) =>
            new Vector3(
                Mathf.Max(0.01f, halfExtents.x - GroundClearance),
                Mathf.Max(0.01f, halfExtents.y - GroundClearance),
                Mathf.Max(0.01f, halfExtents.z - GroundClearance));

        private static Bounds ResolveLocalBounds(Transform subjectRoot)
        {
            Collider[] colliders =
                subjectRoot.GetComponentsInChildren<Collider>();
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
                                localBounds = new Bounds(
                                    localPoint,
                                    Vector3.zero);
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
                : new Bounds(
                    Vector3.up * MinimumRadius,
                    Vector3.one * MinimumRadius * 2f);
        }

        private static Vector3 Abs(Vector3 value) =>
            new Vector3(
                Mathf.Abs(value.x),
                Mathf.Abs(value.y),
                Mathf.Abs(value.z));
    }
}
