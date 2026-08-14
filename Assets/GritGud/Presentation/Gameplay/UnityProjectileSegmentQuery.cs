using System;
using System.Collections.Generic;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Presentation.Levels.Runtime;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    internal sealed class UnityProjectileSegmentQuery : IProjectileSegmentQuery
    {
        internal const string UnregisteredWorldGeometryId = "world.geometry";

        private readonly GameplayWorldRegistry registry;
        private readonly Func<long> worldStateRevision;
        private readonly IBlastWorldQuery blast;
        private readonly int layerMask;

        public UnityProjectileSegmentQuery(
            GameplayWorldRegistry worldRegistry,
            Func<long> currentWorldStateRevision,
            IBlastWorldQuery blastQuery,
            int physicsLayerMask = Physics.DefaultRaycastLayers)
        {
            registry = worldRegistry ?? throw new ArgumentNullException(
                nameof(worldRegistry));
            worldStateRevision = currentWorldStateRevision ??
                throw new ArgumentNullException(nameof(currentWorldStateRevision));
            blast = blastQuery ?? throw new ArgumentNullException(
                nameof(blastQuery));
            layerMask = physicsLayerMask;
        }

        public ProjectileSegmentQueryResult Query(ProjectileSegmentQuery query)
        {
            long revision = worldStateRevision();
            if (revision < 0)
            {
                throw new InvalidOperationException(
                    "Projectile world-state revisions cannot be negative.");
            }

            Vector3 start = ToVector3(query.SegmentStart);
            Vector3 direction = ToVector3(query.SegmentEnd) - start;
            float distance = direction.magnitude;
            RaycastHit[] hits = Physics.SphereCastAll(
                start,
                query.Radius,
                direction / distance,
                distance,
                layerMask,
                QueryTriggerInteraction.Ignore);

            float nearestDistance = float.PositiveInfinity;
            string nearestEntityId = null;
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider == null || hit.distance >= nearestDistance)
                {
                    continue;
                }

                Transform hitTransform = hit.collider.transform;
                string entityId = ResolveEntityId(hitTransform);
                if (string.Equals(
                    entityId,
                    query.Flight.Launch.AttackerId,
                    StringComparison.Ordinal))
                {
                    continue;
                }

                nearestDistance = hit.distance;
                nearestEntityId = entityId;
            }

            if (nearestEntityId == null)
            {
                return ProjectileSegmentQueryResult.Clear(revision);
            }

            float collisionFraction = Mathf.Clamp01(nearestDistance / distance);
            Vector3 impactPosition = Vector3.Lerp(start, start + direction, collisionFraction);
            IReadOnlyList<BlastEffectRecord> blastEffects =
                Array.Empty<BlastEffectRecord>();
            float blastRadius = query.Flight.Launch.Definition.BlastRadius;
            if (blastRadius > 0f)
            {
                BlastWorldQueryResult blastResult = blast.Query(
                    new BlastWorldQuery(
                        ToGameplayPosition(impactPosition),
                        blastRadius));
                if (blastResult.WorldStateRevision != revision)
                {
                    throw new InvalidOperationException(
                        "Projectile collision and blast evidence must describe one world revision.");
                }

                blastEffects = blastResult.Effects;
            }

            return ProjectileSegmentQueryResult.Collision(
                revision,
                nearestEntityId,
                collisionFraction,
                blastEffects);
        }

        private string ResolveEntityId(Transform hitTransform)
        {
            if (registry.TryGetActorContaining(
                hitTransform,
                out GameplayActorView actor))
            {
                return actor.ActorId;
            }

            LevelEntityView levelEntity = hitTransform != null
                ? hitTransform.GetComponentInParent<LevelEntityView>()
                : null;
            return levelEntity != null
                && !string.IsNullOrWhiteSpace(levelEntity.EntityId)
                    ? levelEntity.EntityId
                    : UnregisteredWorldGeometryId;
        }

        private static Vector3 ToVector3(GameplayPosition position) =>
            new Vector3(position.X, position.Y, position.Z);

        private static GameplayPosition ToGameplayPosition(Vector3 position) =>
            new GameplayPosition(position.x, position.y, position.z);
    }
}
