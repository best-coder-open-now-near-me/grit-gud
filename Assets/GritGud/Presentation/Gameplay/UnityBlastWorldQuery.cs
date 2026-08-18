using System;
using System.Collections.Generic;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Presentation.Levels.Runtime;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    internal sealed class UnityBlastWorldQuery : IBlastWorldQuery
    {
        private const float RayOriginOffset = 0.02f;

        private readonly GameplayWorldRegistry registry;
        private readonly Func<long> worldStateRevision;
        private readonly Func<string, bool> includeDestructibleProp;
        private readonly int layerMask;

        public UnityBlastWorldQuery(
            GameplayWorldRegistry worldRegistry,
            Func<long> currentWorldStateRevision,
            Func<string, bool> destructiblePropFilter,
            int physicsLayerMask = Physics.DefaultRaycastLayers)
        {
            registry = worldRegistry ?? throw new ArgumentNullException(
                nameof(worldRegistry));
            worldStateRevision = currentWorldStateRevision ??
                throw new ArgumentNullException(
                    nameof(currentWorldStateRevision));
            includeDestructibleProp = destructiblePropFilter ??
                throw new ArgumentNullException(nameof(destructiblePropFilter));
            layerMask = physicsLayerMask;
        }

        public BlastWorldQueryResult Query(BlastWorldQuery query)
        {
            Vector3 origin = ToVector3(query.Origin);
            var effects = new List<BlastEffectRecord>();
            foreach (GameplayActorView actor in registry.Actors)
            {
                AddActorEffect(effects, query, origin, actor);
            }

            foreach (LevelEntityView entity in registry.LevelEntities)
            {
                if (!includeDestructibleProp(entity.EntityId))
                {
                    continue;
                }

                AddPropEffect(effects, query, origin, entity);
            }

            effects.Sort((left, right) => string.CompareOrdinal(
                left.EntityId,
                right.EntityId));
            return new BlastWorldQueryResult(
                query,
                worldStateRevision(),
                effects);
        }

        private void AddActorEffect(
            ICollection<BlastEffectRecord> effects,
            BlastWorldQuery query,
            Vector3 origin,
            GameplayActorView actor)
        {
            IReadOnlyList<ActorTargetRegionSample> regions =
                actor.TargetProfile.GetTargetRegionSamples();
            ActorTargetRegionSample nearest = default;
            float nearestDistance = float.PositiveInfinity;
            bool foundCandidate = false;
            bool foundExposed = false;
            foreach (ActorTargetRegionSample region in regions)
            {
                float distance = Mathf.Max(
                    0f,
                    Vector3.Distance(origin, region.WorldCenter)
                        - region.Radius);
                if (distance > query.Radius)
                {
                    continue;
                }

                bool exposed = HasClearPath(
                    origin,
                    region.WorldCenter,
                    candidate => registry.TryGetActorContaining(
                            candidate,
                            out GameplayActorView hitActor)
                        && ReferenceEquals(hitActor, actor));
                if ((exposed && !foundExposed)
                    || (exposed == foundExposed
                        && distance < nearestDistance))
                {
                    nearest = region;
                    nearestDistance = distance;
                    foundCandidate = true;
                    foundExposed = exposed;
                }
            }

            if (!foundCandidate)
            {
                return;
            }

            effects.Add(new BlastEffectRecord(
                actor.ActorId,
                BlastSubjectKind.Actor,
                nearestDistance,
                foundExposed ? 1f : 0f,
                EvaluateFalloff(nearestDistance, query.Radius),
                foundExposed ? nearest.Id : (TargetRegionId?)null));
        }

        private void AddPropEffect(
            ICollection<BlastEffectRecord> effects,
            BlastWorldQuery query,
            Vector3 origin,
            LevelEntityView entity)
        {
            Vector3 target = entity.GetWorldBounds().ClosestPoint(origin);
            float distance = Vector3.Distance(origin, target);
            if (distance > query.Radius)
            {
                return;
            }

            bool exposed = HasClearPath(
                origin,
                target,
                candidate => registry.TryGetLevelEntityContaining(
                        candidate,
                        out LevelEntityView hitEntity)
                    && ReferenceEquals(hitEntity, entity));
            effects.Add(new BlastEffectRecord(
                entity.EntityId,
                BlastSubjectKind.DestructibleProp,
                distance,
                exposed ? 1f : 0f,
                EvaluateFalloff(distance, query.Radius)));
        }

        private bool HasClearPath(
            Vector3 origin,
            Vector3 target,
            Func<Transform, bool> belongsToSubject)
        {
            Vector3 ray = target - origin;
            if (ray.sqrMagnitude <= 0.0001f)
            {
                return true;
            }

            Vector3 direction = ray.normalized;
            if (!Physics.Raycast(
                    origin + (direction * RayOriginOffset),
                    direction,
                    out RaycastHit hit,
                    Mathf.Max(0f, ray.magnitude - RayOriginOffset),
                    layerMask,
                    QueryTriggerInteraction.Ignore))
            {
                return true;
            }

            return hit.collider != null
                && belongsToSubject(hit.collider.transform);
        }

        private static float EvaluateFalloff(float distance, float radius) =>
            Mathf.Clamp01(1f - (distance / radius));

        private static Vector3 ToVector3(GameplayPosition value) =>
            new Vector3(value.X, value.Y, value.Z);
    }
}
