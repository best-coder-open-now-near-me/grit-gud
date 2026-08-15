using System;
using System.Collections.Generic;
using System.Linq;

namespace GritGud.Domain.Levels
{
    internal sealed class TerrainPlayabilityAnalysis
    {
        private readonly float[] heights;
        private readonly int[] components;

        public TerrainPlayabilityAnalysis(
            TerrainSurfaceData surface,
            float[] heights,
            int[] components,
            TerrainPlayabilitySurfaceReport report)
        {
            Surface = surface ?? throw new ArgumentNullException(nameof(surface));
            this.heights = heights ?? throw new ArgumentNullException(nameof(heights));
            this.components = components ?? throw new ArgumentNullException(nameof(components));
            Report = report ?? throw new ArgumentNullException(nameof(report));
        }

        public TerrainSurfaceData Surface { get; }
        public TerrainPlayabilitySurfaceReport Report { get; }

        public bool TrySample(float worldX, float worldZ, out float height)
        {
            float x = (worldX - Surface.origin.x) / Surface.sampleSpacing;
            float z = (worldZ - Surface.origin.z) / Surface.sampleSpacing;
            if (x < 0f || z < 0f
                || x > Surface.sampleCountX - 1
                || z > Surface.sampleCountZ - 1)
            {
                height = 0f;
                return false;
            }
            int lowerX = Math.Min(Surface.sampleCountX - 1, Math.Max(0, (int)Math.Floor(x)));
            int lowerZ = Math.Min(Surface.sampleCountZ - 1, Math.Max(0, (int)Math.Floor(z)));
            int upperX = Math.Min(Surface.sampleCountX - 1, lowerX + 1);
            int upperZ = Math.Min(Surface.sampleCountZ - 1, lowerZ + 1);
            float blendX = x - lowerX;
            float blendZ = z - lowerZ;
            float lower = Lerp(
                heights[Index(Surface, lowerX, lowerZ)],
                heights[Index(Surface, upperX, lowerZ)],
                blendX);
            float upper = Lerp(
                heights[Index(Surface, lowerX, upperZ)],
                heights[Index(Surface, upperX, upperZ)],
                blendX);
            height = Lerp(lower, upper, blendZ);
            return true;
        }

        public int ComponentAt(float worldX, float worldZ)
        {
            int x = Math.Min(
                Surface.sampleCountX - 1,
                Math.Max(0, (int)Math.Round((worldX - Surface.origin.x) / Surface.sampleSpacing)));
            int z = Math.Min(
                Surface.sampleCountZ - 1,
                Math.Max(0, (int)Math.Round((worldZ - Surface.origin.z) / Surface.sampleSpacing)));
            return components[Index(Surface, x, z)];
        }

        private static float Lerp(float from, float to, float amount) =>
            from + (to - from) * amount;

        private static int Index(TerrainSurfaceData surface, int x, int z) =>
            z * surface.sampleCountX + x;
    }

    internal static class TerrainPlayabilityAnalysisBuilder
    {
        public static bool CanAnalyze(TerrainSurfaceData surface)
        {
            return surface != null
                && surface.sampleCountX >= 2
                && surface.sampleCountZ >= 2
                && surface.sampleCountX <= LevelTerrainValidationRule.MaximumSamplesPerAxis
                && surface.sampleCountZ <= LevelTerrainValidationRule.MaximumSamplesPerAxis
                && surface.heightSamples != null
                && surface.heightSamples.Count == surface.sampleCountX * surface.sampleCountZ
                && LevelValidationMath.IsFinite(surface.origin)
                && Finite(surface.sampleSpacing)
                && surface.sampleSpacing > 0f
                && Finite(surface.minimumElevation)
                && Finite(surface.elevationIncrement)
                && surface.elevationIncrement > 0f;
        }

        public static TerrainPlayabilityAnalysis Analyze(
            TerrainSurfaceData surface,
            float maximumSlope,
            float maximumStep)
        {
            if (!CanAnalyze(surface))
                throw new ArgumentException("The terrain surface cannot be analyzed.", nameof(surface));
            int sampleCount = surface.sampleCountX * surface.sampleCountZ;
            var heights = new float[sampleCount];
            var walkable = new bool[sampleCount];
            float maximumObservedSlope = 0f;
            int walkableCount = 0;
            for (int z = 0; z < surface.sampleCountZ; z++)
            {
                for (int x = 0; x < surface.sampleCountX; x++)
                {
                    int index = Index(surface, x, z);
                    heights[index] = Height(surface, x, z);
                    float slope = SlopeDegrees(surface, x, z);
                    maximumObservedSlope = Math.Max(maximumObservedSlope, slope);
                    walkable[index] = slope <= maximumSlope;
                    if (walkable[index])
                        walkableCount++;
                }
            }

            int[] components = Enumerable.Repeat(-1, sampleCount).ToArray();
            int componentCount = LabelConnectedRegions(
                surface,
                heights,
                walkable,
                components,
                maximumSlope,
                maximumStep);
            return new TerrainPlayabilityAnalysis(
                surface,
                heights,
                components,
                new TerrainPlayabilitySurfaceReport(
                    surface.id,
                    sampleCount,
                    walkableCount,
                    componentCount,
                    maximumObservedSlope));
        }

        private static int LabelConnectedRegions(
            TerrainSurfaceData surface,
            IReadOnlyList<float> heights,
            IReadOnlyList<bool> walkable,
            int[] components,
            float maximumSlope,
            float maximumStep)
        {
            int componentCount = 0;
            float allowedNeighborRise = Math.Max(
                maximumStep,
                surface.sampleSpacing * (float)Math.Tan(maximumSlope * Math.PI / 180d));
            var queue = new Queue<int>();
            for (int index = 0; index < components.Length; index++)
            {
                if (!walkable[index] || components[index] >= 0)
                    continue;
                components[index] = componentCount;
                queue.Enqueue(index);
                while (queue.Count > 0)
                {
                    int current = queue.Dequeue();
                    int x = current % surface.sampleCountX;
                    int z = current / surface.sampleCountX;
                    Visit(x - 1, z, current);
                    Visit(x + 1, z, current);
                    Visit(x, z - 1, current);
                    Visit(x, z + 1, current);
                }
                componentCount++;
            }
            return componentCount;

            void Visit(int x, int z, int from)
            {
                if (x < 0 || z < 0 || x >= surface.sampleCountX || z >= surface.sampleCountZ)
                    return;
                int next = Index(surface, x, z);
                if (!walkable[next]
                    || components[next] >= 0
                    || Math.Abs(heights[next] - heights[from]) > allowedNeighborRise)
                {
                    return;
                }
                components[next] = componentCount;
                queue.Enqueue(next);
            }
        }

        private static float SlopeDegrees(TerrainSurfaceData surface, int x, int z)
        {
            int left = Math.Max(0, x - 1);
            int right = Math.Min(surface.sampleCountX - 1, x + 1);
            int lower = Math.Max(0, z - 1);
            int upper = Math.Min(surface.sampleCountZ - 1, z + 1);
            float dx = (Height(surface, right, z) - Height(surface, left, z))
                / ((right - left) * surface.sampleSpacing);
            float dz = (Height(surface, x, upper) - Height(surface, x, lower))
                / ((upper - lower) * surface.sampleSpacing);
            return (float)(Math.Atan(Math.Sqrt(dx * dx + dz * dz)) * 180d / Math.PI);
        }

        private static float Height(TerrainSurfaceData surface, int x, int z)
        {
            return surface.origin.y
                + surface.minimumElevation
                + surface.heightSamples[Index(surface, x, z)] * surface.elevationIncrement;
        }

        private static int Index(TerrainSurfaceData surface, int x, int z) =>
            z * surface.sampleCountX + x;

        private static bool Finite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
