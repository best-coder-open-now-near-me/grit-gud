using System;
using System.Collections.Generic;
using GritGud.Application.Levels;
using GritGud.Domain.Levels;
using UnityEngine;

namespace GritGud.Presentation.LevelEditing.Tools
{
    public sealed class TerrainMaterialStrokeAccumulator
    {
        private readonly LevelDocument previewDocument;
        private int lastX = int.MinValue;
        private int lastZ = int.MinValue;
        private int minX = int.MaxValue;
        private int minZ = int.MaxValue;
        private int maxX = int.MinValue;
        private int maxZ = int.MinValue;

        public TerrainMaterialStrokeAccumulator(TerrainSurfaceData surface)
        {
            PreviewSurface = surface?.DeepCopy() ?? throw new ArgumentNullException(nameof(surface));
            previewDocument = LevelDocumentFactory.CreateEmpty("Terrain Material Preview");
            previewDocument.terrainSurfaces.Add(PreviewSurface);
        }

        public TerrainSurfaceData PreviewSurface { get; }
        public string SurfaceId => PreviewSurface.id;

        public SetTerrainMaterialsCommand ApplyPoint(Vector3 point, int radius, int materialIndex)
        {
            int centerX = Mathf.RoundToInt((point.x - PreviewSurface.origin.x) / PreviewSurface.sampleSpacing);
            int centerZ = Mathf.RoundToInt((point.z - PreviewSurface.origin.z) / PreviewSurface.sampleSpacing);
            if (centerX == lastX && centerZ == lastZ)
                return null;
            lastX = centerX;
            lastZ = centerZ;
            radius = Mathf.Clamp(radius, 1, 16);
            int startX = Mathf.Max(0, centerX - radius);
            int startZ = Mathf.Max(0, centerZ - radius);
            int endX = Mathf.Min(PreviewSurface.sampleCountX - 1, centerX + radius);
            int endZ = Mathf.Min(PreviewSurface.sampleCountZ - 1, centerZ + radius);
            var values = new List<int>((endX - startX + 1) * (endZ - startZ + 1));
            bool changed = false;
            for (int z = startZ; z <= endZ; z++)
            {
                for (int x = startX; x <= endX; x++)
                {
                    int index = z * PreviewSurface.sampleCountX + x;
                    int value = (x - centerX) * (x - centerX) + (z - centerZ) * (z - centerZ) <= radius * radius
                        ? materialIndex
                        : PreviewSurface.materialSamples[index];
                    changed |= value != PreviewSurface.materialSamples[index];
                    values.Add(value);
                }
            }
            if (!changed)
                return null;
            var patch = new SetTerrainMaterialsCommand(
                SurfaceId, startX, startZ, endX - startX + 1, endZ - startZ + 1, values);
            patch.Apply(previewDocument);
            minX = Math.Min(minX, startX); minZ = Math.Min(minZ, startZ);
            maxX = Math.Max(maxX, endX); maxZ = Math.Max(maxZ, endZ);
            return patch;
        }

        public SetTerrainMaterialsCommand CreateCommand()
        {
            if (minX == int.MaxValue)
                return null;
            int width = maxX - minX + 1;
            int depth = maxZ - minZ + 1;
            var values = new List<int>(width * depth);
            for (int z = minZ; z <= maxZ; z++)
                for (int x = minX; x <= maxX; x++)
                    values.Add(PreviewSurface.materialSamples[z * PreviewSurface.sampleCountX + x]);
            return new SetTerrainMaterialsCommand(SurfaceId, minX, minZ, width, depth, values);
        }
    }
}
