using System;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    internal sealed class UnityMovementRouteSegmentValidator :
        IMovementRouteSegmentValidator
    {
        private const float GroundClearance = 0.02f;
        private const float ProbePadding = 0.12f;
        private readonly CharacterController controller;
        private readonly Transform actorTransform;

        public UnityMovementRouteSegmentValidator(
            CharacterController characterController)
        {
            controller = characterController != null
                ? characterController
                : throw new ArgumentNullException(nameof(characterController));
            actorTransform = characterController.transform;
        }

        public MovementRouteSegmentValidation Validate(
            string actorId,
            GameplayPosition from,
            GameplayPosition requestedDestination)
        {
            Vector3 fromRoot = MovementRouteSampling.ToVector3(from);
            Vector3 requestedRoot =
                MovementRouteSampling.ToVector3(requestedDestination);
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
            if (groundAngle > controller.slopeLimit)
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

            if (HasBlockingCollision(fromRoot, resolvedRoot))
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

        private bool HasBlockingCollision(Vector3 fromRoot, Vector3 toRoot)
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
                    if (!IsOwnedCollider(hit.collider))
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
                if (!IsOwnedCollider(overlap))
                {
                    return true;
                }
            }

            return false;
        }

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
    }
}
