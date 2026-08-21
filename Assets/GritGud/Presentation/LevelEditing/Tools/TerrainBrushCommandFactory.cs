using System;
using System.Collections.Generic;
using GritGud.Application.Levels;
using GritGud.Domain.Levels;
using UnityEngine;

namespace GritGud.Presentation.LevelEditing.Tools
{
    public static class TerrainBrushCommandFactory
    {
        public static SetTerrainHeightsCommand Create(
            TerrainSurfaceData surface,
            Vector3 worldPoint,
            int radiusInSamples,
            int quantizedStrength,
            TerrainBrushMode mode,
            int? flattenTargetHeight = null)
        {
            if (surface == null)
            {
                throw new ArgumentNullException(nameof(surface));
            }
            if (!Enum.IsDefined(typeof(TerrainBrushMode), mode))
            {
                throw new ArgumentOutOfRangeException(nameof(mode));
            }
            if (mode == TerrainBrushMode.Flatten && !flattenTargetHeight.HasValue)
            {
                throw new ArgumentException(
                    "Flatten brushes require a captured target height.",
                    nameof(flattenTargetHeight));
            }

            int centerX = Mathf.RoundToInt(
                (worldPoint.x - surface.origin.x) / surface.sampleSpacing);
            int centerZ = Mathf.RoundToInt(
                (worldPoint.z - surface.origin.z) / surface.sampleSpacing);
            int radius = Mathf.Clamp(radiusInSamples, 1, 16);
            int startX = Mathf.Max(0, centerX - radius);
            int startZ = Mathf.Max(0, centerZ - radius);
            int endX = Mathf.Min(surface.sampleCountX - 1, centerX + radius);
            int endZ = Mathf.Min(surface.sampleCountZ - 1, centerZ + radius);
            if (startX > endX || startZ > endZ)
            {
                return null;
            }

            int width = endX - startX + 1;
            int depth = endZ - startZ + 1;
            var values = new List<int>(width * depth);
            int strength = Mathf.Clamp(quantizedStrength, 1, 100);
            int radiusSquared = radius * radius;
            bool changed = false;
            for (int z = startZ; z <= endZ; z++)
            {
                for (int x = startX; x <= endX; x++)
                {
                    int current = surface.heightSamples[z * surface.sampleCountX + x];
                    int deltaX = x - centerX;
                    int deltaZ = z - centerZ;
                    int result = current;
                    if (deltaX * deltaX + deltaZ * deltaZ <= radiusSquared)
                    {
                        result = ResolveHeight(
                            surface,
                            x,
                            z,
                            current,
                            strength,
                            mode,
                            flattenTargetHeight);
                    }

                    changed |= result != current;
                    values.Add(result);
                }
            }

            return changed ? new SetTerrainHeightsCommand(
                surface.id,
                startX,
                startZ,
                width,
                depth,
                values) : null;
        }

        public static int QuantizeWorldHeight(
            TerrainSurfaceData surface,
            float worldHeight)
        {
            if (surface == null)
            {
                throw new ArgumentNullException(nameof(surface));
            }
            if (surface.elevationIncrement <= 0f)
            {
                throw new InvalidOperationException(
                    "Terrain elevation increments must be positive.");
            }

            int quantized = Mathf.RoundToInt(
                (worldHeight - surface.origin.y - surface.minimumElevation)
                / surface.elevationIncrement);
            return Mathf.Clamp(
                quantized,
                LevelTerrainValidationRule.MinimumQuantizedHeight,
                LevelTerrainValidationRule.MaximumQuantizedHeight);
        }

        private static int ResolveHeight(
            TerrainSurfaceData surface,
            int x,
            int z,
            int current,
            int strength,
            TerrainBrushMode mode,
            int? flattenTargetHeight)
        {
            int target;
            switch (mode)
            {
                case TerrainBrushMode.Raise:
                    target = current + strength;
                    break;
                case TerrainBrushMode.Lower:
                    target = current - strength;
                    break;
                case TerrainBrushMode.Smooth:
                    target = CalculateNeighborAverage(surface, x, z);
                    target = MoveTowards(current, target, strength);
                    break;
                case TerrainBrushMode.Flatten:
                    target = MoveTowards(
                        current,
                        flattenTargetHeight.GetValueOrDefault(),
                        strength);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode));
            }

            return Mathf.Clamp(
                target,
                LevelTerrainValidationRule.MinimumQuantizedHeight,
                LevelTerrainValidationRule.MaximumQuantizedHeight);
        }

        private static int CalculateNeighborAverage(
            TerrainSurfaceData surface,
            int centerX,
            int centerZ)
        {
            int sum = 0;
            int count = 0;
            for (int z = Math.Max(0, centerZ - 1);
                z <= Math.Min(surface.sampleCountZ - 1, centerZ + 1);
                z++)
            {
                for (int x = Math.Max(0, centerX - 1);
                    x <= Math.Min(surface.sampleCountX - 1, centerX + 1);
                    x++)
                {
                    sum += surface.heightSamples[z * surface.sampleCountX + x];
                    count++;
                }
            }

            return Mathf.RoundToInt(sum / (float)count);
        }

        private static int MoveTowards(int current, int target, int maximumDelta)
        {
            if (current < target)
            {
                return Math.Min(current + maximumDelta, target);
            }

            return current > target
                ? Math.Max(current - maximumDelta, target)
                : current;
        }
    }
}
