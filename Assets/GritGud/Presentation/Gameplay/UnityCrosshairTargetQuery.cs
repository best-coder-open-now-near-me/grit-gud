using System;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    internal sealed class UnityPointerTargetQuery
    {
        private const float MaximumAimDistance = 250f;
        private readonly RaycastHit[] hitBuffer = new RaycastHit[32];
        private readonly Transform observer;
        private readonly GameplayWorldRegistry registry;
        private readonly int layerMask;
        private readonly Func<GameplayActorView, bool> canAcquire;

        public UnityPointerTargetQuery(
            Transform observingActor,
            GameplayWorldRegistry worldRegistry,
            int physicsLayerMask = Physics.DefaultRaycastLayers,
            Func<GameplayActorView, bool> actorEligibility = null)
        {
            observer = observingActor != null
                ? observingActor
                : throw new ArgumentNullException(nameof(observingActor));
            registry = worldRegistry ??
                throw new ArgumentNullException(nameof(worldRegistry));
            layerMask = physicsLayerMask;
            canAcquire = actorEligibility
                ?? (candidate => candidate.Targetable);
        }

        public bool TryAcquire(Ray ray, out GameplayActorView target)
        {
            int hitCount = Physics.RaycastNonAlloc(
                ray,
                hitBuffer,
                MaximumAimDistance,
                layerMask,
                QueryTriggerInteraction.Collide);
            if (hitCount == hitBuffer.Length)
            {
                RaycastHit[] allHits = Physics.RaycastAll(
                    ray,
                    MaximumAimDistance,
                    layerMask,
                    QueryTriggerInteraction.Collide);
                return TryResolveNearestTarget(
                    allHits,
                    allHits.Length,
                    out target);
            }

            return TryResolveNearestTarget(hitBuffer, hitCount, out target);
        }

        private bool TryResolveNearestTarget(
            RaycastHit[] hits,
            int hitCount,
            out GameplayActorView target)
        {
            float nearestDistance = float.PositiveInfinity;
            GameplayActorView nearestTarget = null;
            for (int index = 0; index < hitCount; index++)
            {
                RaycastHit hit = hits[index];
                if (hit.collider == null
                    || !ActorTargetProfilePresenter.IsAcquisitionCollider(
                        hit.collider)
                    || BelongsTo(hit.collider.transform, observer)
                    || hit.distance >= nearestDistance
                    || !registry.TryGetActorContaining(
                        hit.collider.transform,
                        out GameplayActorView candidate)
                    || !canAcquire(candidate)
                    || ReferenceEquals(candidate.Transform, observer))
                {
                    continue;
                }

                nearestDistance = hit.distance;
                nearestTarget = candidate;
            }

            target = nearestTarget;
            return target != null;
        }

        private static bool BelongsTo(Transform candidate, Transform root) =>
            candidate != null
            && root != null
            && (candidate == root || candidate.IsChildOf(root));
    }
}
