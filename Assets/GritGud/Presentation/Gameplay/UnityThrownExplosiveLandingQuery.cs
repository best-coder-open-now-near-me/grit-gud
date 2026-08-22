using System;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    internal sealed class UnityThrownExplosiveLandingQuery :
        IThrownExplosiveLandingQuery
    {
        private const float SurfaceProbeHalfHeight = 8f;
        private readonly Func<long> worldStateRevision;
        private readonly int layerMask;

        public UnityThrownExplosiveLandingQuery(
            Func<long> currentWorldStateRevision,
            int physicsLayerMask = Physics.DefaultRaycastLayers)
        {
            worldStateRevision = currentWorldStateRevision
                ?? throw new ArgumentNullException(nameof(currentWorldStateRevision));
            layerMask = physicsLayerMask;
        }

        public ThrownExplosiveLandingResult Resolve(
            GameplayPosition launchOrigin,
            GameplayPosition sampledLanding)
        {
            Vector3 requested = ToVector3(sampledLanding);
            Vector3 landing = requested;
            Vector3 probeOrigin = requested
                + (Vector3.up * SurfaceProbeHalfHeight);
            RaycastHit[] surfaceHits = Physics.RaycastAll(
                probeOrigin,
                Vector3.down,
                SurfaceProbeHalfHeight * 2f,
                layerMask,
                QueryTriggerInteraction.Ignore);
            float closestHeightDelta = float.PositiveInfinity;
            for (int index = 0; index < surfaceHits.Length; index++)
            {
                float heightDelta = Mathf.Abs(
                    surfaceHits[index].point.y - requested.y);
                bool sameHeight = Mathf.Abs(
                    heightDelta - closestHeightDelta) <= 0.0001f;
                if (heightDelta > closestHeightDelta
                    || (sameHeight
                        && surfaceHits[index].point.y <= landing.y))
                    continue;
                closestHeightDelta = heightDelta;
                landing = surfaceHits[index].point;
            }

            return new ThrownExplosiveLandingResult(
                ToGameplayPosition(landing),
                worldStateRevision());
        }

        private static Vector3 ToVector3(GameplayPosition value) =>
            new Vector3(value.X, value.Y, value.Z);

        private static GameplayPosition ToGameplayPosition(Vector3 value) =>
            new GameplayPosition(value.x, value.y, value.z);
    }
}
