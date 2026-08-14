using System;
using System.Collections.Generic;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    public sealed class UnityTargetExposureQuery : ITargetExposureQuery
    {
        internal const int RasterLongAxisCellCount = 32;

        private const float EndpointTolerance = 0.005f;
        private const float MinimumProjectionSpan = 0.0001f;

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
            {
                throw new ArgumentNullException(nameof(targetRegions));
            }

            if (targetRegions.Count == 0)
            {
                throw new ArgumentException(
                    "Target exposure requires at least one body region.",
                    nameof(targetRegions));
            }

            bool cacheEnabled = worldStateRevision != null
                || sightObscurance != null;
            long revision = worldStateRevision != null
                ? worldStateRevision()
                : 0L;
            long obscuranceRevision = sightObscurance?.Revision ?? 0L;
            if (cacheEnabled && revision < 0L)
            {
                throw new InvalidOperationException(
                    "Target-exposure world revisions cannot be negative.");
            }

            if (cacheEnabled && CanReuseSnapshot(
                    observerId,
                    observerOrigin,
                    targetId,
                    targetRegions,
                    revision,
                    obscuranceRevision))
            {
                return cachedSnapshot;
            }

            Vector3 origin = ToVector3(observerOrigin);
            ProjectedRegion[] regions = BuildProjection(
                origin,
                targetRegions,
                out Vector3 forward,
                out Vector3 horizontal,
                out Vector3 vertical,
                out Rect projectionBounds,
                out int rasterWidth,
                out int rasterHeight);
            RasterEvaluationCount++;
            var visibleCounts = new int[regions.Length];
            var totalCounts = new int[regions.Length];

            for (int row = 0; row < rasterHeight; row++)
            {
                float projectedY = Mathf.Lerp(
                    projectionBounds.yMin,
                    projectionBounds.yMax,
                    (row + 0.5f) / rasterHeight);
                for (int column = 0; column < rasterWidth; column++)
                {
                    float projectedX = Mathf.Lerp(
                        projectionBounds.xMin,
                        projectionBounds.xMax,
                        (column + 0.5f) / rasterWidth);
                    Vector3 direction = (
                        forward
                        + (horizontal * projectedX)
                        + (vertical * projectedY)).normalized;
                    int paintedRegion = FindNearestRegion(
                        origin,
                        direction,
                        regions,
                        out float surfaceDistance);
                    if (paintedRegion < 0)
                    {
                        continue;
                    }

                    totalCounts[paintedRegion]++;
                    if (IsWorldVisible(
                            origin,
                            direction,
                            surfaceDistance))
                    {
                        visibleCounts[paintedRegion]++;
                    }
                }
            }

            var exposures = new List<TargetRegionExposure>(regions.Length);
            for (int index = 0; index < regions.Length; index++)
            {
                exposures.Add(new TargetRegionExposure(
                    regions[index].Id,
                    visibleCounts[index],
                    totalCounts[index]));
            }

            TargetExposureSnapshot snapshot = new TargetExposureSnapshot(
                observerId,
                targetId,
                exposures);
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
                {
                    cachedRegions[index] = targetRegions[index];
                }
            }

            return snapshot;
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
            {
                return false;
            }

            for (int index = 0; index < cachedRegions.Length; index++)
            {
                TargetRegionSample cached = cachedRegions[index];
                TargetRegionSample current = targetRegions[index];
                if (cached.Id != current.Id
                    || cached.Radius != current.Radius
                    || !PositionsMatch(cached.Center, current.Center))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool PositionsMatch(
            GameplayPosition left,
            GameplayPosition right) =>
            left.X == right.X
            && left.Y == right.Y
            && left.Z == right.Z;

        private static ProjectedRegion[] BuildProjection(
            Vector3 origin,
            IReadOnlyList<TargetRegionSample> targetRegions,
            out Vector3 forward,
            out Vector3 horizontal,
            out Vector3 vertical,
            out Rect projectionBounds,
            out int rasterWidth,
            out int rasterHeight)
        {
            var identifiers = new HashSet<TargetRegionId>();
            var regions = new ProjectedRegion[targetRegions.Count];
            Vector3 targetCenter = Vector3.zero;
            for (int index = 0; index < targetRegions.Count; index++)
            {
                TargetRegionSample source = targetRegions[index];
                if (!identifiers.Add(source.Id))
                {
                    throw new ArgumentException(
                        $"Target exposure cannot repeat region '{source.Id}'.",
                        nameof(targetRegions));
                }

                Vector3 center = ToVector3(source.Center);
                targetCenter += center;
                regions[index] = new ProjectedRegion(
                    source.Id,
                    center,
                    source.Radius);
            }

            targetCenter /= regions.Length;
            forward = targetCenter - origin;
            if (forward.sqrMagnitude <= EndpointTolerance * EndpointTolerance)
            {
                throw new ArgumentException(
                    "Observer and target centers cannot occupy the same point.",
                    nameof(targetRegions));
            }

            forward.Normalize();
            horizontal = Vector3.Cross(Vector3.up, forward);
            if (horizontal.sqrMagnitude <= MinimumProjectionSpan)
            {
                horizontal = Vector3.Cross(Vector3.forward, forward);
            }
            horizontal.Normalize();
            vertical = Vector3.Cross(forward, horizontal).normalized;

            float minimumX = float.PositiveInfinity;
            float maximumX = float.NegativeInfinity;
            float minimumY = float.PositiveInfinity;
            float maximumY = float.NegativeInfinity;
            for (int index = 0; index < regions.Length; index++)
            {
                ProjectedRegion region = regions[index];
                Vector3 offset = region.Center - origin;
                float depth = Vector3.Dot(offset, forward);
                if (depth <= region.Radius + EndpointTolerance)
                {
                    throw new ArgumentException(
                        $"Observer is inside or behind target region '{region.Id}'.",
                        nameof(targetRegions));
                }

                float projectedX = Vector3.Dot(offset, horizontal) / depth;
                float projectedY = Vector3.Dot(offset, vertical) / depth;
                float projectedRadius = region.Radius
                    / Mathf.Max(EndpointTolerance, depth - region.Radius);
                minimumX = Mathf.Min(minimumX, projectedX - projectedRadius);
                maximumX = Mathf.Max(maximumX, projectedX + projectedRadius);
                minimumY = Mathf.Min(minimumY, projectedY - projectedRadius);
                maximumY = Mathf.Max(maximumY, projectedY + projectedRadius);
            }

            float spanX = Mathf.Max(MinimumProjectionSpan, maximumX - minimumX);
            float spanY = Mathf.Max(MinimumProjectionSpan, maximumY - minimumY);
            if (spanX >= spanY)
            {
                rasterWidth = RasterLongAxisCellCount;
                rasterHeight = Mathf.Max(
                    1,
                    Mathf.CeilToInt(
                        RasterLongAxisCellCount * (spanY / spanX)));
            }
            else
            {
                rasterHeight = RasterLongAxisCellCount;
                rasterWidth = Mathf.Max(
                    1,
                    Mathf.CeilToInt(
                        RasterLongAxisCellCount * (spanX / spanY)));
            }

            projectionBounds = Rect.MinMaxRect(
                minimumX,
                minimumY,
                maximumX,
                maximumY);
            return regions;
        }

        private static int FindNearestRegion(
            Vector3 origin,
            Vector3 direction,
            IReadOnlyList<ProjectedRegion> regions,
            out float surfaceDistance)
        {
            int nearestRegion = -1;
            surfaceDistance = float.PositiveInfinity;
            for (int index = 0; index < regions.Count; index++)
            {
                if (!TryIntersectSphere(
                        origin,
                        direction,
                        regions[index],
                        out float candidateDistance)
                    || candidateDistance >= surfaceDistance)
                {
                    continue;
                }

                nearestRegion = index;
                surfaceDistance = candidateDistance;
            }

            return nearestRegion;
        }

        private static bool TryIntersectSphere(
            Vector3 origin,
            Vector3 direction,
            ProjectedRegion region,
            out float distance)
        {
            Vector3 fromCenter = origin - region.Center;
            float projected = Vector3.Dot(fromCenter, direction);
            float discriminant = (projected * projected)
                - (fromCenter.sqrMagnitude - (region.Radius * region.Radius));
            if (discriminant < 0f)
            {
                distance = 0f;
                return false;
            }

            float root = Mathf.Sqrt(discriminant);
            distance = -projected - root;
            if (distance <= EndpointTolerance)
            {
                distance = -projected + root;
            }

            return distance > EndpointTolerance;
        }

        private bool IsWorldVisible(
            Vector3 origin,
            Vector3 direction,
            float surfaceDistance)
        {
            if (sightObscurance != null
                && sightObscurance.BlocksSight(
                    ToGameplayPosition(origin),
                    ToGameplayPosition(
                        origin + (direction * surfaceDistance))))
            {
                return false;
            }

            float rayDistance = Mathf.Max(
                0f,
                surfaceDistance - EndpointTolerance);
            int hitCount = Physics.RaycastNonAlloc(
                origin,
                direction,
                hitBuffer,
                rayDistance,
                layerMask,
                QueryTriggerInteraction.Ignore);
            if (ContainsWorldOccluder(hitBuffer, hitCount))
            {
                return false;
            }

            if (hitCount == hitBuffer.Length)
            {
                RaycastHit[] overflowHits = Physics.RaycastAll(
                    origin,
                    direction,
                    rayDistance,
                    layerMask,
                    QueryTriggerInteraction.Ignore);
                return !ContainsWorldOccluder(
                    overflowHits,
                    overflowHits.Length);
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
                {
                    return true;
                }
            }

            return false;
        }

        private static bool BelongsTo(Transform candidate, Transform root) =>
            candidate != null
            && root != null
            && (candidate == root || candidate.IsChildOf(root));

        private static Vector3 ToVector3(GameplayPosition position) =>
            new Vector3(position.X, position.Y, position.Z);

        private static GameplayPosition ToGameplayPosition(Vector3 position) =>
            new GameplayPosition(position.x, position.y, position.z);

        private readonly struct ProjectedRegion
        {
            public ProjectedRegion(
                TargetRegionId id,
                Vector3 center,
                float radius)
            {
                Id = id;
                Center = center;
                Radius = radius;
            }

            public TargetRegionId Id { get; }

            public Vector3 Center { get; }

            public float Radius { get; }
        }
    }
}
