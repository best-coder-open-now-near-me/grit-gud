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
            int direction)
        {
            if (surface == null)
            {
                throw new ArgumentNullException(nameof(surface));
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
            int strength = Mathf.Clamp(quantizedStrength, 1, 100) * Math.Sign(direction);
            int radiusSquared = radius * radius;
            for (int z = startZ; z <= endZ; z++)
            {
                for (int x = startX; x <= endX; x++)
                {
                    int current = surface.heightSamples[z * surface.sampleCountX + x];
                    int deltaX = x - centerX;
                    int deltaZ = z - centerZ;
                    values.Add(deltaX * deltaX + deltaZ * deltaZ <= radiusSquared
                        ? Mathf.Clamp(
                            current + strength,
                            LevelTerrainValidationRule.MinimumQuantizedHeight,
                            LevelTerrainValidationRule.MaximumQuantizedHeight)
                        : current);
                }
            }

            return new SetTerrainHeightsCommand(
                surface.id,
                startX,
                startZ,
                width,
                depth,
                values);
        }
    }
}
