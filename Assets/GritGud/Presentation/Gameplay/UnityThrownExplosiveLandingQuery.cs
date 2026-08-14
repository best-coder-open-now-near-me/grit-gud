using System;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    internal sealed class UnityThrownExplosiveLandingQuery :
        IThrownExplosiveLandingQuery
    {
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
            Vector3 start = ToVector3(launchOrigin);
            Vector3 requested = ToVector3(sampledLanding);
            Vector3 direction = requested - start;
            Vector3 landing = requested;
            if (direction.sqrMagnitude > 0.0001f
                && Physics.Raycast(
                    start, direction.normalized, out RaycastHit landingHit,
                    direction.magnitude, layerMask, QueryTriggerInteraction.Ignore))
                landing = landingHit.point;

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
