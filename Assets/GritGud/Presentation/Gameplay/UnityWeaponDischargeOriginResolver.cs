using System;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    /// <summary>
    /// Keeps presentation-owned weapon geometry from moving a shot through
    /// world collision. The actor eye remains the stable firing fallback when
    /// an animated muzzle has crossed an obstruction.
    /// </summary>
    internal sealed class UnityWeaponDischargeOriginResolver
    {
        private const float EndpointTolerance = 0.005f;

        private readonly RaycastHit[] hitBuffer = new RaycastHit[16];

        public Vector3 Resolve(GameplayActorView actor, Vector3 muzzlePosition)
        {
            if (actor == null) throw new ArgumentNullException(nameof(actor));
            if (!IsFinite(muzzlePosition))
                throw new ArgumentOutOfRangeException(nameof(muzzlePosition));

            Vector3 eyePosition = actor.Stance.FirstPersonEyePosition;
            Vector3 offset = muzzlePosition - eyePosition;
            float distance = offset.magnitude;
            if (distance <= EndpointTolerance) return muzzlePosition;

            Vector3 direction = offset / distance;
            float rayDistance = Mathf.Max(0f, distance - EndpointTolerance);
            int hitCount = Physics.RaycastNonAlloc(
                eyePosition,
                direction,
                hitBuffer,
                rayDistance,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            if (ContainsWorldObstruction(actor.Transform, hitBuffer, hitCount))
                return eyePosition;
            if (hitCount != hitBuffer.Length) return muzzlePosition;

            RaycastHit[] overflowHits = Physics.RaycastAll(
                eyePosition,
                direction,
                rayDistance,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            return ContainsWorldObstruction(
                    actor.Transform,
                    overflowHits,
                    overflowHits.Length)
                ? eyePosition
                : muzzlePosition;
        }

        private static bool ContainsWorldObstruction(
            Transform actorRoot,
            RaycastHit[] hits,
            int hitCount)
        {
            for (int index = 0; index < hitCount; index++)
            {
                Transform hit = hits[index].collider != null
                    ? hits[index].collider.transform
                    : null;
                if (hit != null
                    && hit != actorRoot
                    && !hit.IsChildOf(actorRoot))
                    return true;
            }
            return false;
        }

        private static bool IsFinite(Vector3 value) =>
            !float.IsNaN(value.x)
            && !float.IsInfinity(value.x)
            && !float.IsNaN(value.y)
            && !float.IsInfinity(value.y)
            && !float.IsNaN(value.z)
            && !float.IsInfinity(value.z);
    }
}
