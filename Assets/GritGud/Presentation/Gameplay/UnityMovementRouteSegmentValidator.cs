using System;
using System.Collections.Generic;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Levels;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    internal sealed class UnityMovementRouteSegmentValidator :
        IMovementRouteSegmentValidator
    {
        private const float GroundClearance = 0.02f;
        private const float ProbePadding = 0.12f;
        private const float TakeoffTolerance = 0.08f;
        private const float MinimumDirectionDot = 0.5f;
        private const int TrajectoryClearanceSteps = 16;
        private readonly CharacterController controller;
        private readonly Transform actorTransform;
        private readonly IReadOnlyList<TraversalLink> traversalLinks;

        public UnityMovementRouteSegmentValidator(
            CharacterController characterController,
            IEnumerable<LevelTraversalLinkData> authoredTraversalLinks = null)
        {
            controller = characterController != null
                ? characterController
                : throw new ArgumentNullException(nameof(characterController));
            actorTransform = characterController.transform;
            var links = new List<TraversalLink>();
            foreach (LevelTraversalLinkData link in authoredTraversalLinks
                ?? Array.Empty<LevelTraversalLinkData>())
            {
                if (link != null)
                    links.Add(new TraversalLink(link));
            }
            links.Sort((left, right) => string.CompareOrdinal(
                left.Id,
                right.Id));
            traversalLinks = links.AsReadOnly();
        }

        public MovementRouteSegmentValidation Validate(
            string actorId,
            GameplayPosition from,
            GameplayPosition requestedDestination)
        {
            Vector3 fromRoot = MovementRouteSampling.ToVector3(from);
            Vector3 requestedRoot =
                MovementRouteSampling.ToVector3(requestedDestination);
            if (TryResolveTraversal(
                    from,
                    fromRoot,
                    requestedRoot,
                    out MovementRouteSegmentValidation traversal))
            {
                return traversal;
            }

            return ValidateGrounded(fromRoot, requestedRoot);
        }

        private MovementRouteSegmentValidation ValidateGrounded(
            Vector3 fromRoot,
            Vector3 requestedRoot)
        {
            float horizontalDistance = Vector2.Distance(
                new Vector2(fromRoot.x, fromRoot.z),
                new Vector2(requestedRoot.x, requestedRoot.z));
            float slopeRise = horizontalDistance * Mathf.Tan(
                controller.slopeLimit * Mathf.Deg2Rad);
            float verticalReach = Mathf.Max(controller.stepOffset, slopeRise)
                + ProbePadding;
            float bottomOffset = controller.center.y -
                (controller.height * 0.5f);
            Vector3 probeOrigin = new Vector3(
                requestedRoot.x,
                fromRoot.y + verticalReach,
                requestedRoot.z);
            float probeDistance = (verticalReach * 2f)
                + controller.stepOffset
                + ProbePadding;
            if (!TryFindGround(probeOrigin, probeDistance, out RaycastHit groundHit))
            {
                return MovementRouteSegmentValidation.Rejected(
                    "No walkable ground was found beneath the route.");
            }

            float groundAngle = Vector3.Angle(groundHit.normal, Vector3.up);
            if (groundAngle > controller.slopeLimit + 0.01f)
            {
                return MovementRouteSegmentValidation.Rejected(
                    $"The route exceeds the {controller.slopeLimit:0.#}\u00b0 slope limit.");
            }

            var resolvedRoot = new Vector3(
                requestedRoot.x,
                groundHit.point.y - bottomOffset + GroundClearance,
                requestedRoot.z);
            if (Mathf.Abs(resolvedRoot.y - fromRoot.y) > verticalReach)
            {
                return MovementRouteSegmentValidation.Rejected(
                    "The route exceeds the actor's step height.");
            }

            if (HasBlockingCollision(
                    fromRoot,
                    resolvedRoot,
                    groundHit.collider))
            {
                return MovementRouteSegmentValidation.Rejected(
                    "An obstacle blocks the actor's capsule path.");
            }

            return MovementRouteSegmentValidation.Accepted(
                new GameplayPosition(
                    resolvedRoot.x,
                    resolvedRoot.y,
                    resolvedRoot.z));
        }

        private bool TryResolveTraversal(
            GameplayPosition from,
            Vector3 fromRoot,
            Vector3 requestedRoot,
            out MovementRouteSegmentValidation validation)
        {
            validation = default;
            Vector3 requestedDirection = requestedRoot - fromRoot;
            requestedDirection.y = 0f;
            if (requestedDirection.sqrMagnitude <= 0.0001f)
                return false;
            requestedDirection.Normalize();

            foreach (TraversalLink link in traversalLinks)
            {
                if (TryResolveTraversalDirection(
                        from,
                        fromRoot,
                        requestedRoot,
                        requestedDirection,
                        link,
                        reverse: false,
                        out validation))
                    return true;
                if (link.Bidirectional
                    && TryResolveTraversalDirection(
                        from,
                        fromRoot,
                        requestedRoot,
                        requestedDirection,
                        link,
                        reverse: true,
                        out validation))
                    return true;
            }
            return false;
        }

        private bool TryResolveTraversalDirection(
            GameplayPosition from,
            Vector3 fromRoot,
            Vector3 requestedRoot,
            Vector3 requestedDirection,
            TraversalLink link,
            bool reverse,
            out MovementRouteSegmentValidation validation)
        {
            validation = default;
            Vector3 takeoff = reverse ? link.Landing : link.Takeoff;
            Vector3 landing = reverse ? link.Takeoff : link.Landing;
            Vector3 linkDirection = landing - takeoff;
            linkDirection.y = 0f;
            if (linkDirection.sqrMagnitude <= 0.0001f)
                return false;
            linkDirection.Normalize();
            if (Vector3.Dot(requestedDirection, linkDirection)
                < MinimumDirectionDot)
                return false;

            Vector3 toTakeoff = takeoff - fromRoot;
            float verticalOffset = Mathf.Abs(toTakeoff.y);
            toTakeoff.y = 0f;
            float takeoffDistance = toTakeoff.magnitude;
            if (takeoffDistance > link.ActivationRadius
                || verticalOffset > link.ActivationRadius)
                return false;

            if (takeoffDistance > TakeoffTolerance)
            {
                if (toTakeoff.sqrMagnitude > 0.0001f
                    && Vector3.Dot(toTakeoff.normalized, requestedDirection)
                        < MinimumDirectionDot)
                    return false;
                MovementRouteSegmentValidation snap = ValidateGrounded(
                    fromRoot,
                    takeoff);
                if (!snap.IsValid)
                    return false;
                validation = snap;
                return true;
            }

            if (!HasLandingSupport(landing))
            {
                validation = MovementRouteSegmentValidation.Rejected(
                    $"Traversal link '{link.Id}' has no walkable landing support.");
                return true;
            }

            var segment = new MovementRouteSegmentRecord(
                from,
                ToGameplayPosition(landing),
                link.Kind,
                link.Id,
                link.ActionId,
                link.MovementCost,
                link.ActionPointCost,
                link.ArcHeight,
                link.PlaybackDurationSeconds);
            if (HasTrajectoryCollision(segment, link.ClearancePadding))
            {
                validation = MovementRouteSegmentValidation.Rejected(
                    $"Traversal link '{link.Id}' does not clear the actor capsule.");
                return true;
            }

            validation = MovementRouteSegmentValidation.Accepted(segment);
            return true;
        }

        private bool HasLandingSupport(Vector3 landingRoot)
        {
            float bottomOffset = controller.center.y
                - (controller.height * 0.5f);
            Vector3 origin = landingRoot + (Vector3.up * ProbePadding);
            if (!TryFindGround(
                    origin,
                    controller.stepOffset + (ProbePadding * 2f),
                    out RaycastHit hit))
                return false;
            if (Vector3.Angle(hit.normal, Vector3.up) > controller.slopeLimit)
                return false;
            float supportedRootY = hit.point.y - bottomOffset + GroundClearance;
            return Mathf.Abs(supportedRootY - landingRoot.y)
                <= ProbePadding;
        }

        private bool HasTrajectoryCollision(
            MovementRouteSegmentRecord segment,
            float clearancePadding)
        {
            Vector3 previous = MovementRouteSampling.ToVector3(
                segment.Sample(0f));
            for (int step = 1; step <= TrajectoryClearanceSteps; step++)
            {
                Vector3 current = MovementRouteSampling.ToVector3(
                    segment.Sample((float)step / TrajectoryClearanceSteps));
                GetCapsule(
                    previous,
                    out Vector3 top,
                    out Vector3 bottom,
                    out float radius);
                // Keep authored padding through the airborne body of the arc.
                // The endpoint sweeps use the real capsule because both poses
                // intentionally sit at their supported ground clearance.
                if (step > 1 && step < TrajectoryClearanceSteps)
                    radius += clearancePadding;
                Vector3 displacement = current - previous;
                float distance = displacement.magnitude;
                if (distance > 0.0001f)
                {
                    foreach (RaycastHit hit in Physics.CapsuleCastAll(
                        top,
                        bottom,
                        radius,
                        displacement / distance,
                        distance,
                        Physics.DefaultRaycastLayers,
                        QueryTriggerInteraction.Ignore))
                    {
                        if (!IsOwnedCollider(hit.collider))
                            return true;
                    }
                }
                previous = current;
            }

            GetCapsule(
                MovementRouteSampling.ToVector3(segment.To),
                out Vector3 landingTop,
                out Vector3 landingBottom,
                out float landingRadius);
            foreach (Collider overlap in Physics.OverlapCapsule(
                landingTop,
                landingBottom,
                landingRadius,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore))
            {
                if (!IsOwnedCollider(overlap))
                    return true;
            }
            return false;
        }

        private bool HasBlockingCollision(
            Vector3 fromRoot,
            Vector3 toRoot,
            Collider destinationSupport)
        {
            GetCapsule(fromRoot, out Vector3 fromTop, out Vector3 fromBottom,
                out float collisionRadius);
            // CharacterController step handling lifts the lower capsule over a
            // short riser. Mirror that clearance for the sweep while the ground
            // probe still limits the accepted elevation change.
            float stepClearance = controller.stepOffset
                * Mathf.Abs(actorTransform.lossyScale.y);
            fromBottom += Vector3.up * stepClearance;
            Vector3 displacement = toRoot - fromRoot;
            float distance = displacement.magnitude;
            if (distance > 0.0001f)
            {
                RaycastHit[] hits = Physics.CapsuleCastAll(
                    fromTop,
                    fromBottom,
                    collisionRadius,
                    displacement / distance,
                    distance,
                    Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore);
                foreach (RaycastHit hit in hits)
                {
                    if (!IsOwnedCollider(hit.collider)
                        && !IsWalkableSupportHit(hit))
                    {
                        return true;
                    }
                }
            }

            GetCapsule(toRoot, out Vector3 toTop, out Vector3 toBottom,
                out collisionRadius);
            Collider[] overlaps = Physics.OverlapCapsule(
                toTop,
                toBottom,
                collisionRadius,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            foreach (Collider overlap in overlaps)
            {
                if (!IsOwnedCollider(overlap)
                    && overlap != destinationSupport)
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsWalkableSupportHit(RaycastHit hit) =>
            hit.normal.sqrMagnitude > 0.0001f
            && Vector3.Angle(hit.normal, Vector3.up)
                <= controller.slopeLimit + 0.01f;

        private bool TryFindGround(
            Vector3 probeOrigin,
            float probeDistance,
            out RaycastHit groundHit)
        {
            RaycastHit[] hits = Physics.RaycastAll(
                probeOrigin,
                Vector3.down,
                probeDistance,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            bool found = false;
            groundHit = default;
            foreach (RaycastHit hit in hits)
            {
                if (IsOwnedCollider(hit.collider) ||
                    (found && hit.distance >= groundHit.distance))
                {
                    continue;
                }

                found = true;
                groundHit = hit;
            }

            return found;
        }

        private void GetCapsule(
            Vector3 rootPosition,
            out Vector3 top,
            out Vector3 bottom,
            out float collisionRadius)
        {
            Vector3 scale = actorTransform.lossyScale;
            float radiusScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            float heightScale = Mathf.Abs(scale.y);
            float radius = controller.radius * radiusScale;
            float height = Mathf.Max(controller.height * heightScale, radius * 2f);
            collisionRadius = Mathf.Max(
                radius * 0.75f,
                radius - (controller.skinWidth * 0.5f));
            Vector3 center = rootPosition +
                (actorTransform.rotation * Vector3.Scale(controller.center, scale));
            float halfCylinder = Mathf.Max(
                0f,
                (height * 0.5f) - radius);
            top = center + (Vector3.up * halfCylinder);
            bottom = center - (Vector3.up * halfCylinder);
        }

        private bool IsOwnedCollider(Collider collider)
        {
            return collider == null ||
                collider.transform == actorTransform ||
                collider.transform.IsChildOf(actorTransform);
        }

        private static GameplayPosition ToGameplayPosition(Vector3 value) =>
            new GameplayPosition(value.x, value.y, value.z);

        private sealed class TraversalLink
        {
            public TraversalLink(LevelTraversalLinkData source)
            {
                Id = source.id?.Trim() ?? string.Empty;
                ActionId = source.actionId?.Trim() ?? string.Empty;
                Kind = ParseKind(source.kind);
                Takeoff = new Vector3(
                    source.takeoff.x,
                    source.takeoff.y,
                    source.takeoff.z);
                Landing = new Vector3(
                    source.landing.x,
                    source.landing.y,
                    source.landing.z);
                Bidirectional = source.bidirectional;
                ActivationRadius = source.activationRadius;
                MovementCost = source.movementCost;
                ActionPointCost = source.actionPointCost;
                ArcHeight = source.arcHeight;
                PlaybackDurationSeconds = source.playbackDurationSeconds;
                ClearancePadding = source.clearancePadding;
            }

            public string Id { get; }
            public string ActionId { get; }
            public MovementRouteSegmentKind Kind { get; }
            public Vector3 Takeoff { get; }
            public Vector3 Landing { get; }
            public bool Bidirectional { get; }
            public float ActivationRadius { get; }
            public float MovementCost { get; }
            public int ActionPointCost { get; }
            public float ArcHeight { get; }
            public float PlaybackDurationSeconds { get; }
            public float ClearancePadding { get; }

            private static MovementRouteSegmentKind ParseKind(string kind)
            {
                switch (kind?.Trim().ToLowerInvariant())
                {
                    case LevelTraversalLinkData.VaultKind:
                        return MovementRouteSegmentKind.Vault;
                    case LevelTraversalLinkData.MantleKind:
                        return MovementRouteSegmentKind.Mantle;
                    default:
                        return MovementRouteSegmentKind.Jump;
                }
            }
        }
    }
}
