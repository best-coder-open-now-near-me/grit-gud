using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;

namespace GritGud.Application.Gameplay
{
    public interface ITargetExposureObstructionQuery
    {
        bool Blocks(
            GameplayPosition origin,
            GameplayPosition targetSurface);
    }

    /// <summary>
    /// Portable projected-silhouette raster shared by Unity and headless
    /// evidence. Spatial backends answer only whether a generated sight
    /// segment is obstructed; silhouette sampling and region ownership have a
    /// single implementation.
    /// </summary>
    public static class GameplayTargetExposureRaster
    {
        public const int LongAxisCellCount = 32;

        private const float EndpointTolerance = 0.005f;
        private const float MinimumProjectionSpan = 0.0001f;

        public static TargetExposureSnapshot Capture(
            string observerId,
            GameplayPosition observerOrigin,
            string targetId,
            IReadOnlyList<TargetRegionSample> targetRegions,
            ITargetExposureObstructionQuery obstruction)
        {
            if (targetRegions == null)
                throw new ArgumentNullException(nameof(targetRegions));
            if (targetRegions.Count == 0)
                throw new ArgumentException(
                    "Target exposure requires at least one body region.",
                    nameof(targetRegions));
            if (obstruction == null)
                throw new ArgumentNullException(nameof(obstruction));

            Vector origin = Vector.From(observerOrigin);
            ProjectedRegion[] regions = BuildProjection(
                origin,
                targetRegions,
                out Vector forward,
                out Vector horizontal,
                out Vector vertical,
                out Bounds projection,
                out int rasterWidth,
                out int rasterHeight);
            var visibleCounts = new int[regions.Length];
            var totalCounts = new int[regions.Length];
            for (int row = 0; row < rasterHeight; row++)
            {
                float projectedY = Lerp(
                    projection.MinimumY,
                    projection.MaximumY,
                    (row + 0.5f) / rasterHeight);
                for (int column = 0; column < rasterWidth; column++)
                {
                    float projectedX = Lerp(
                        projection.MinimumX,
                        projection.MaximumX,
                        (column + 0.5f) / rasterWidth);
                    Vector direction = (forward
                        + (horizontal * projectedX)
                        + (vertical * projectedY)).Normalized;
                    int paintedRegion = FindNearestRegion(
                        origin,
                        direction,
                        regions,
                        out float surfaceDistance);
                    if (paintedRegion < 0) continue;
                    totalCounts[paintedRegion]++;
                    GameplayPosition surface = (origin
                        + (direction * surfaceDistance)).ToPosition();
                    if (!obstruction.Blocks(observerOrigin, surface))
                        visibleCounts[paintedRegion]++;
                }
            }

            var exposures = new List<TargetRegionExposure>(regions.Length);
            for (int index = 0; index < regions.Length; index++)
                exposures.Add(new TargetRegionExposure(
                    regions[index].Id,
                    visibleCounts[index],
                    totalCounts[index]));
            return new TargetExposureSnapshot(
                observerId,
                targetId,
                exposures);
        }

        private static ProjectedRegion[] BuildProjection(
            Vector origin,
            IReadOnlyList<TargetRegionSample> targetRegions,
            out Vector forward,
            out Vector horizontal,
            out Vector vertical,
            out Bounds projection,
            out int rasterWidth,
            out int rasterHeight)
        {
            var identifiers = new HashSet<TargetRegionId>();
            var regions = new ProjectedRegion[targetRegions.Count];
            Vector targetCenter = default;
            for (int index = 0; index < targetRegions.Count; index++)
            {
                TargetRegionSample source = targetRegions[index];
                if (!identifiers.Add(source.Id))
                    throw new ArgumentException(
                        $"Target exposure cannot repeat region '{source.Id}'.",
                        nameof(targetRegions));
                Vector center = Vector.From(source.Center);
                targetCenter += center;
                regions[index] = new ProjectedRegion(
                    source.Id,
                    center,
                    source.Radius);
            }

            targetCenter /= regions.Length;
            forward = targetCenter - origin;
            if (forward.LengthSquared
                <= EndpointTolerance * EndpointTolerance)
                throw new ArgumentException(
                    "Observer and target centers cannot occupy the same point.",
                    nameof(targetRegions));
            forward = forward.Normalized;
            horizontal = Vector.Cross(Vector.Up, forward);
            if (horizontal.LengthSquared <= MinimumProjectionSpan)
                horizontal = Vector.Cross(Vector.Forward, forward);
            horizontal = horizontal.Normalized;
            vertical = Vector.Cross(forward, horizontal).Normalized;

            float minimumX = float.PositiveInfinity;
            float maximumX = float.NegativeInfinity;
            float minimumY = float.PositiveInfinity;
            float maximumY = float.NegativeInfinity;
            for (int index = 0; index < regions.Length; index++)
            {
                ProjectedRegion region = regions[index];
                Vector offset = region.Center - origin;
                float depth = Vector.Dot(offset, forward);
                if (depth <= region.Radius + EndpointTolerance)
                    throw new ArgumentException(
                        $"Observer is inside or behind target region '{region.Id}'.",
                        nameof(targetRegions));
                float projectedX = Vector.Dot(offset, horizontal) / depth;
                float projectedY = Vector.Dot(offset, vertical) / depth;
                float projectedRadius = region.Radius
                    / Math.Max(EndpointTolerance, depth - region.Radius);
                minimumX = Math.Min(minimumX, projectedX - projectedRadius);
                maximumX = Math.Max(maximumX, projectedX + projectedRadius);
                minimumY = Math.Min(minimumY, projectedY - projectedRadius);
                maximumY = Math.Max(maximumY, projectedY + projectedRadius);
            }

            float spanX = Math.Max(
                MinimumProjectionSpan,
                maximumX - minimumX);
            float spanY = Math.Max(
                MinimumProjectionSpan,
                maximumY - minimumY);
            if (spanX >= spanY)
            {
                rasterWidth = LongAxisCellCount;
                rasterHeight = Math.Max(
                    1,
                    (int)Math.Ceiling(LongAxisCellCount * (spanY / spanX)));
            }
            else
            {
                rasterHeight = LongAxisCellCount;
                rasterWidth = Math.Max(
                    1,
                    (int)Math.Ceiling(LongAxisCellCount * (spanX / spanY)));
            }
            projection = new Bounds(
                minimumX,
                minimumY,
                maximumX,
                maximumY);
            return regions;
        }

        private static int FindNearestRegion(
            Vector origin,
            Vector direction,
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
                    continue;
                nearestRegion = index;
                surfaceDistance = candidateDistance;
            }
            return nearestRegion;
        }

        private static bool TryIntersectSphere(
            Vector origin,
            Vector direction,
            ProjectedRegion region,
            out float distance)
        {
            Vector fromCenter = origin - region.Center;
            float projected = Vector.Dot(fromCenter, direction);
            float discriminant = (projected * projected)
                - (fromCenter.LengthSquared
                    - (region.Radius * region.Radius));
            if (discriminant < 0f)
            {
                distance = 0f;
                return false;
            }
            float root = (float)Math.Sqrt(discriminant);
            distance = -projected - root;
            if (distance <= EndpointTolerance)
                distance = -projected + root;
            return distance > EndpointTolerance;
        }

        private static float Lerp(float from, float to, float progress) =>
            from + ((to - from) * progress);

        private readonly struct Bounds
        {
            public Bounds(
                float minimumX,
                float minimumY,
                float maximumX,
                float maximumY)
            {
                MinimumX = minimumX;
                MinimumY = minimumY;
                MaximumX = maximumX;
                MaximumY = maximumY;
            }

            public float MinimumX { get; }
            public float MinimumY { get; }
            public float MaximumX { get; }
            public float MaximumY { get; }
        }

        private readonly struct ProjectedRegion
        {
            public ProjectedRegion(
                TargetRegionId id,
                Vector center,
                float radius)
            {
                Id = id;
                Center = center;
                Radius = radius;
            }

            public TargetRegionId Id { get; }
            public Vector Center { get; }
            public float Radius { get; }
        }

        private readonly struct Vector
        {
            public Vector(float x, float y, float z)
            {
                X = x;
                Y = y;
                Z = z;
            }

            public float X { get; }
            public float Y { get; }
            public float Z { get; }
            public float LengthSquared => (X * X) + (Y * Y) + (Z * Z);
            public Vector Normalized
            {
                get
                {
                    float length = (float)Math.Sqrt(LengthSquared);
                    if (length <= 0f)
                        throw new InvalidOperationException(
                            "A zero-length evidence vector cannot be normalized.");
                    return this / length;
                }
            }

            public static Vector Up => new Vector(0f, 1f, 0f);
            public static Vector Forward => new Vector(0f, 0f, 1f);
            public static Vector From(GameplayPosition value) => new Vector(
                value.X,
                value.Y,
                value.Z);
            public GameplayPosition ToPosition() => new GameplayPosition(
                X,
                Y,
                Z);
            public static float Dot(Vector left, Vector right) =>
                (left.X * right.X)
                + (left.Y * right.Y)
                + (left.Z * right.Z);
            public static Vector Cross(Vector left, Vector right) => new Vector(
                (left.Y * right.Z) - (left.Z * right.Y),
                (left.Z * right.X) - (left.X * right.Z),
                (left.X * right.Y) - (left.Y * right.X));
            public static Vector operator +(Vector left, Vector right) =>
                new Vector(left.X + right.X, left.Y + right.Y, left.Z + right.Z);
            public static Vector operator -(Vector left, Vector right) =>
                new Vector(left.X - right.X, left.Y - right.Y, left.Z - right.Z);
            public static Vector operator *(Vector value, float scale) =>
                new Vector(value.X * scale, value.Y * scale, value.Z * scale);
            public static Vector operator /(Vector value, float scale) =>
                new Vector(value.X / scale, value.Y / scale, value.Z / scale);
        }
    }

    public static class GameplaySoundEvidenceRules
    {
        public static EncounterSoundEvidence Capture(
            string observerId,
            GameplayPosition observerPosition,
            string sourceId,
            GameplayPosition sourcePosition,
            float loudness,
            float hearingRange,
            bool obstructed)
        {
            GameplayContentIdentity.RequireText(observerId, nameof(observerId));
            GameplayContentIdentity.RequireText(sourceId, nameof(sourceId));
            GameplayNumericPolicy.RequireFinite(loudness, nameof(loudness));
            GameplayNumericPolicy.RequireFinite(
                hearingRange,
                nameof(hearingRange));
            if (loudness < 0f || loudness > 1f)
                throw new ArgumentOutOfRangeException(nameof(loudness));
            if (hearingRange <= 0f)
                throw new ArgumentOutOfRangeException(nameof(hearingRange));
            float distance = observerPosition.DistanceTo(sourcePosition);
            if (distance > hearingRange + 0.0001f)
                return new EncounterSoundEvidence(sourceId, sourcePosition, 0f);
            float rangeFraction = Math.Min(1f, distance / hearingRange);
            float distanceAttenuation = 1f - (0.5f * rangeFraction);
            float obstructionAttenuation = obstructed ? 0.5f : 1f;
            return new EncounterSoundEvidence(
                sourceId,
                sourcePosition,
                GameplayNumericPolicy.Normalize(
                    loudness
                    * distanceAttenuation
                    * obstructionAttenuation));
        }
    }
}
