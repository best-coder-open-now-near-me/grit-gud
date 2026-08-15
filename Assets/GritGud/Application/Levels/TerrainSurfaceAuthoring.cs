using System;
using System.Collections.Generic;
using GritGud.Domain.Levels;

namespace GritGud.Application.Levels
{
    public static class TerrainSurfaceAuthoring
    {
        public const float DefaultSampleSpacing = 2f;
        public const float DefaultElevationIncrement = 0.1f;

        public static TerrainSurfaceData CreateFlat(
            string surfaceId,
            LevelBoundsData bounds,
            float sampleSpacing)
        {
            if (string.IsNullOrWhiteSpace(surfaceId))
            {
                throw new ArgumentException("A terrain surface ID is required.", nameof(surfaceId));
            }

            int sampleCountX = CalculateSampleCount(bounds.size.x, sampleSpacing, "width");
            int sampleCountZ = CalculateSampleCount(bounds.size.z, sampleSpacing, "depth");
            ValidateSampleCapacity(sampleCountX, sampleCountZ);
            return new TerrainSurfaceData
            {
                id = surfaceId,
                origin = new Float3Data(
                    bounds.center.x - bounds.size.x * 0.5f,
                    0f,
                    bounds.center.z - bounds.size.z * 0.5f),
                sampleCountX = sampleCountX,
                sampleCountZ = sampleCountZ,
                sampleSpacing = sampleSpacing,
                minimumElevation = 0f,
                elevationIncrement = DefaultElevationIncrement,
                heightSamples = CreateFlatSamples(sampleCountX, sampleCountZ),
            };
        }

        public static TerrainSurfaceData Resize(
            TerrainSurfaceData source,
            float width,
            float depth,
            float sampleSpacing)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            TerrainSurfaceData working = source.DeepCopy();
            ValidateSource(working);
            int sampleCountX = CalculateSampleCount(width, sampleSpacing, "width");
            int sampleCountZ = CalculateSampleCount(depth, sampleSpacing, "depth");
            ValidateSampleCapacity(sampleCountX, sampleCountZ);

            float sourceWidth = (working.sampleCountX - 1) * working.sampleSpacing;
            float sourceDepth = (working.sampleCountZ - 1) * working.sampleSpacing;
            float centerX = working.origin.x + sourceWidth * 0.5f;
            float centerZ = working.origin.z + sourceDepth * 0.5f;
            TerrainSurfaceData resized = working.DeepCopy();
            resized.origin = new Float3Data(
                centerX - width * 0.5f,
                working.origin.y,
                centerZ - depth * 0.5f);
            resized.sampleCountX = sampleCountX;
            resized.sampleCountZ = sampleCountZ;
            resized.sampleSpacing = sampleSpacing;
            resized.heightSamples = Resample(working, sampleCountX, sampleCountZ);
            return resized;
        }

        public static float Width(TerrainSurfaceData surface)
        {
            return surface == null || surface.sampleCountX < 2
                ? 0f
                : (surface.sampleCountX - 1) * surface.sampleSpacing;
        }

        public static float Depth(TerrainSurfaceData surface)
        {
            return surface == null || surface.sampleCountZ < 2
                ? 0f
                : (surface.sampleCountZ - 1) * surface.sampleSpacing;
        }

        private static int CalculateSampleCount(
            float extent,
            float sampleSpacing,
            string extentName)
        {
            RequirePositiveFinite(extent, extentName);
            RequirePositiveFinite(sampleSpacing, "grid spacing");
            double intervals = extent / sampleSpacing;
            double roundedIntervals = Math.Round(intervals);
            if (roundedIntervals < 1d
                || Math.Abs(intervals - roundedIntervals) > 0.0001d)
            {
                throw new ArgumentException(
                    $"Terrain {extentName} must be a whole multiple of grid spacing.");
            }

            if (roundedIntervals + 1d > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(
                    extentName,
                    "The terrain dimension contains too many samples.");
            }

            return (int)roundedIntervals + 1;
        }

        private static void RequirePositiveFinite(float value, string name)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    name,
                    $"Terrain {name} must be a positive finite number.");
            }
        }

        private static void ValidateSampleCapacity(int sampleCountX, int sampleCountZ)
        {
            if (sampleCountX > LevelTerrainValidationRule.MaximumSamplesPerAxis
                || sampleCountZ > LevelTerrainValidationRule.MaximumSamplesPerAxis
                || (long)sampleCountX * sampleCountZ
                    > LevelTerrainValidationRule.MaximumSamplesPerSurface)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(sampleCountX),
                    $"Terrain resolution is limited to "
                    + $"{LevelTerrainValidationRule.MaximumSamplesPerAxis} samples per axis "
                    + "and the configured total-sample limit.");
            }
        }

        private static void ValidateSource(TerrainSurfaceData source)
        {
            if (source.sampleCountX < 2
                || source.sampleCountZ < 2
                || source.heightSamples.Count
                    != source.sampleCountX * source.sampleCountZ
                || float.IsNaN(source.sampleSpacing)
                || float.IsInfinity(source.sampleSpacing)
                || source.sampleSpacing <= 0f)
            {
                throw new InvalidOperationException(
                    $"Terrain surface '{source.id}' is not valid enough to resize.");
            }
        }

        private static List<int> CreateFlatSamples(int sampleCountX, int sampleCountZ)
        {
            int count = sampleCountX * sampleCountZ;
            var samples = new List<int>(count);
            for (int index = 0; index < count; index++)
            {
                samples.Add(0);
            }

            return samples;
        }

        private static List<int> Resample(
            TerrainSurfaceData source,
            int sampleCountX,
            int sampleCountZ)
        {
            var result = new List<int>(sampleCountX * sampleCountZ);
            for (int z = 0; z < sampleCountZ; z++)
            {
                float sourceZ = z * (source.sampleCountZ - 1f) / (sampleCountZ - 1f);
                int lowerZ = (int)Math.Floor(sourceZ);
                int upperZ = Math.Min(source.sampleCountZ - 1, lowerZ + 1);
                float blendZ = sourceZ - lowerZ;
                for (int x = 0; x < sampleCountX; x++)
                {
                    float sourceX = x * (source.sampleCountX - 1f) / (sampleCountX - 1f);
                    int lowerX = (int)Math.Floor(sourceX);
                    int upperX = Math.Min(source.sampleCountX - 1, lowerX + 1);
                    float blendX = sourceX - lowerX;
                    float lower = Lerp(
                        Sample(source, lowerX, lowerZ),
                        Sample(source, upperX, lowerZ),
                        blendX);
                    float upper = Lerp(
                        Sample(source, lowerX, upperZ),
                        Sample(source, upperX, upperZ),
                        blendX);
                    result.Add((int)Math.Round(
                        Lerp(lower, upper, blendZ),
                        MidpointRounding.AwayFromZero));
                }
            }

            return result;
        }

        private static int Sample(TerrainSurfaceData surface, int x, int z)
        {
            return surface.heightSamples[z * surface.sampleCountX + x];
        }

        private static float Lerp(float from, float to, float amount)
        {
            return from + (to - from) * amount;
        }
    }
}
