using System;
using System.Collections.Generic;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    public sealed class UnityTargetExposureQuery : ITargetExposureQuery,
        ITargetExposureObstructionQuery
    {
        internal const int RasterLongAxisCellCount =
            GameplayTargetExposureRaster.LongAxisCellCount;

        private const float EndpointTolerance = 0.005f;

        private readonly Transform observerRoot;
        private readonly Transform targetRoot;
        private readonly int layerMask;
        private readonly Func<long> worldStateRevision;
        private readonly ISightObscuranceQuery sightObscurance;
        private readonly RaycastHit[] hitBuffer = new RaycastHit[16];
        private TargetExposureSnapshot cachedSnapshot;
        private TargetRegionSample[] cachedRegions;
        private GameplayPosition cachedObserverOrigin;
        private string cachedObserverId;
        private string cachedTargetId;
        private long cachedWorldStateRevision;
        private long cachedSightObscuranceRevision;

        internal int RasterEvaluationCount { get; private set; }

        internal void Invalidate()
        {
            cachedSnapshot = null;
            cachedRegions = null;
            cachedObserverId = null;
            cachedTargetId = null;
        }

        public UnityTargetExposureQuery(
            Transform observer,
            Transform target,
            int physicsLayerMask = Physics.DefaultRaycastLayers,
            Func<long> currentWorldStateRevision = null,
            ISightObscuranceQuery obscuranceQuery = null)
        {
            observerRoot = observer != null
                ? observer
                : throw new ArgumentNullException(nameof(observer));
            targetRoot = target != null
                ? target
                : throw new ArgumentNullException(nameof(target));
            layerMask = physicsLayerMask;
            worldStateRevision = currentWorldStateRevision;
            sightObscurance = obscuranceQuery;
        }

        public TargetExposureSnapshot Capture(
            string observerId,
            GameplayPosition observerOrigin,
            string targetId,
            IReadOnlyList<TargetRegionSample> targetRegions)
        {
            if (targetRegions == null)
                throw new ArgumentNullException(nameof(targetRegions));
            bool cacheEnabled = worldStateRevision != null
                || sightObscurance != null;
            long revision = worldStateRevision != null
                ? worldStateRevision()
                : 0L;
            long obscuranceRevision = sightObscurance?.Revision ?? 0L;
            if (cacheEnabled && revision < 0L)
                throw new InvalidOperationException(
                    "Target-exposure world revisions cannot be negative.");
            if (cacheEnabled && CanReuseSnapshot(
                    observerId,
                    observerOrigin,
                    targetId,
                    targetRegions,
                    revision,
                    obscuranceRevision))
                return cachedSnapshot;

            TargetExposureSnapshot snapshot = GameplayTargetExposureRaster
                .Capture(
                    observerId,
                    observerOrigin,
                    targetId,
                    targetRegions,
                    this);
            RasterEvaluationCount++;
            if (cacheEnabled)
            {
                cachedSnapshot = snapshot;
                cachedObserverId = observerId;
                cachedObserverOrigin = observerOrigin;
                cachedTargetId = targetId;
                cachedWorldStateRevision = revision;
                cachedSightObscuranceRevision = obscuranceRevision;
                cachedRegions = new TargetRegionSample[targetRegions.Count];
                for (int index = 0; index < targetRegions.Count; index++)
                    cachedRegions[index] = targetRegions[index];
            }
            return snapshot;
        }

        public bool Blocks(
            GameplayPosition origin,
            GameplayPosition targetSurface)
        {
            if (sightObscurance != null
                && sightObscurance.BlocksSight(origin, targetSurface))
                return true;
            Vector3 rayOrigin = ToVector3(origin);
            Vector3 displacement = ToVector3(targetSurface) - rayOrigin;
            float surfaceDistance = displacement.magnitude;
            if (surfaceDistance <= EndpointTolerance) return false;
            Vector3 direction = displacement / surfaceDistance;
            float rayDistance = Mathf.Max(
                0f,
                surfaceDistance - EndpointTolerance);
            int hitCount = Physics.RaycastNonAlloc(
                rayOrigin,
                direction,
                hitBuffer,
                rayDistance,
                layerMask,
                QueryTriggerInteraction.Ignore);
            if (ContainsWorldOccluder(hitBuffer, hitCount)) return true;
            if (hitCount != hitBuffer.Length) return false;
            RaycastHit[] overflowHits = Physics.RaycastAll(
                rayOrigin,
                direction,
                rayDistance,
                layerMask,
                QueryTriggerInteraction.Ignore);
            return ContainsWorldOccluder(
                overflowHits,
                overflowHits.Length);
        }

        private bool CanReuseSnapshot(
            string observerId,
            GameplayPosition observerOrigin,
            string targetId,
            IReadOnlyList<TargetRegionSample> targetRegions,
            long revision,
            long obscuranceRevision)
        {
            if (cachedSnapshot == null
                || cachedWorldStateRevision != revision
                || cachedSightObscuranceRevision != obscuranceRevision
                || !string.Equals(
                    cachedObserverId,
                    observerId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    cachedTargetId,
                    targetId,
                    StringComparison.Ordinal)
                || !PositionsMatch(cachedObserverOrigin, observerOrigin)
                || cachedRegions == null
                || cachedRegions.Length != targetRegions.Count)
                return false;
            for (int index = 0; index < cachedRegions.Length; index++)
            {
                TargetRegionSample cached = cachedRegions[index];
                TargetRegionSample current = targetRegions[index];
                if (cached.Id != current.Id
                    || cached.Radius != current.Radius
                    || !PositionsMatch(cached.Center, current.Center))
                    return false;
            }
            return true;
        }

        private bool ContainsWorldOccluder(RaycastHit[] hits, int hitCount)
        {
            for (int index = 0; index < hitCount; index++)
            {
                Transform hitTransform = hits[index].collider != null
                    ? hits[index].collider.transform
                    : null;
                if (!BelongsTo(hitTransform, observerRoot)
                    && !BelongsTo(hitTransform, targetRoot))
                    return true;
            }
            return false;
        }

        private static bool PositionsMatch(
            GameplayPosition left,
            GameplayPosition right) =>
            left.X == right.X
            && left.Y == right.Y
            && left.Z == right.Z;

        private static bool BelongsTo(Transform candidate, Transform root) =>
            candidate != null
            && root != null
            && (candidate == root || candidate.IsChildOf(root));

        private static Vector3 ToVector3(GameplayPosition position) =>
            new Vector3(position.X, position.Y, position.Z);
    }
}
