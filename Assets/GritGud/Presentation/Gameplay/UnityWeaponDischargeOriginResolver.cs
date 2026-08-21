using System;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    internal readonly struct WeaponDischargeLine
    {
        public WeaponDischargeLine(
            Vector3 antiMuzzlePosition,
            Vector3 muzzlePosition)
        {
            AntiMuzzlePosition = antiMuzzlePosition;
            MuzzlePosition = muzzlePosition;
        }

        public Vector3 AntiMuzzlePosition { get; }

        public Vector3 MuzzlePosition { get; }
    }

    /// <summary>
    /// Extends backward from the animated muzzle to the owning character's
    /// capsule, then finds the first world obstruction crossing that segment.
    /// </summary>
    internal sealed class UnityWeaponDischargeOriginResolver
    {
        private const float MinimumLineLength = 0.0001f;

        private readonly RaycastHit[] hitBuffer = new RaycastHit[16];

        public bool TryResolve(
            GameplayActorView actor,
            Transform muzzle,
            out RaycastHit obstruction)
        {
            if (!TryBuildDischargeLine(actor, muzzle, out WeaponDischargeLine line))
            {
                obstruction = default;
                return false;
            }

            return TryResolve(actor.Transform, line, out obstruction);
        }

        public bool TryBuildDischargeLine(
            GameplayActorView actor,
            Transform muzzle,
            out WeaponDischargeLine line)
        {
            if (actor == null) throw new ArgumentNullException(nameof(actor));
            if (muzzle == null) throw new ArgumentNullException(nameof(muzzle));

            CharacterController capsule = actor.Root.GetComponent<
                CharacterController>();
            Vector3 forward = muzzle.forward;
            if (capsule == null || forward.sqrMagnitude <= MinimumLineLength)
            {
                line = default;
                return false;
            }

            var backwardRay = new Ray(
                muzzle.position,
                -forward.normalized);
            if (!capsule.Raycast(
                    backwardRay,
                    out RaycastHit capsuleHit,
                    Mathf.Infinity))
            {
                line = default;
                return false;
            }

            line = new WeaponDischargeLine(
                capsuleHit.point,
                muzzle.position);
            return (line.MuzzlePosition - line.AntiMuzzlePosition)
                .sqrMagnitude > MinimumLineLength;
        }

        public bool TryResolve(
            Transform actorRoot,
            WeaponDischargeLine line,
            out RaycastHit obstruction)
        {
            if (actorRoot == null)
                throw new ArgumentNullException(nameof(actorRoot));
            if (!IsFinite(line.AntiMuzzlePosition)
                || !IsFinite(line.MuzzlePosition))
            {
                throw new ArgumentOutOfRangeException(nameof(line));
            }

            Vector3 offset = line.MuzzlePosition - line.AntiMuzzlePosition;
            float distance = offset.magnitude;
            if (distance <= MinimumLineLength)
            {
                obstruction = default;
                return false;
            }

            Vector3 direction = offset / distance;
            int hitCount = Physics.RaycastNonAlloc(
                line.AntiMuzzlePosition,
                direction,
                hitBuffer,
                distance,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            if (hitCount != hitBuffer.Length)
            {
                return TryFindNearestWorldHit(
                    actorRoot,
                    hitBuffer,
                    hitCount,
                    out obstruction);
            }

            RaycastHit[] overflowHits = Physics.RaycastAll(
                line.AntiMuzzlePosition,
                direction,
                distance,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            return TryFindNearestWorldHit(
                actorRoot,
                overflowHits,
                overflowHits.Length,
                out obstruction);
        }

        private static bool TryFindNearestWorldHit(
            Transform actorRoot,
            RaycastHit[] hits,
            int hitCount,
            out RaycastHit obstruction)
        {
            obstruction = default;
            float nearestDistance = float.PositiveInfinity;
            for (int index = 0; index < hitCount; index++)
            {
                RaycastHit candidate = hits[index];
                Transform hit = candidate.collider != null
                    ? candidate.collider.transform
                    : null;
                if (hit == null
                    || hit == actorRoot
                    || hit.IsChildOf(actorRoot)
                    || candidate.distance >= nearestDistance)
                {
                    continue;
                }

                obstruction = candidate;
                nearestDistance = candidate.distance;
            }
            return !float.IsPositiveInfinity(nearestDistance);
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
