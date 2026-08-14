using System;
using System.Collections.Generic;
using GritGud.Application.Levels;
using GritGud.Domain.Levels;
using UnityEngine;

namespace GritGud.Presentation.LevelEditing.Tools
{
    public sealed class TerrainStrokeAccumulator
    {
        private readonly LevelDocument previewDocument;
        private int lastCenterX = int.MinValue;
        private int lastCenterZ = int.MinValue;
        private int startX = int.MaxValue;
        private int startZ = int.MaxValue;
        private int endX = int.MinValue;
        private int endZ = int.MinValue;

        public TerrainStrokeAccumulator(TerrainSurfaceData surface, int direction)
        {
            PreviewSurface = surface?.DeepCopy()
                ?? throw new ArgumentNullException(nameof(surface));
            Direction = Math.Sign(direction);
            if (Direction == 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(direction),
                    "A terrain stroke must raise or lower terrain.");
            }

            previewDocument = LevelDocumentFactory.CreateEmpty("Terrain Stroke Preview");
            previewDocument.terrainSurfaces.Add(PreviewSurface);
        }

        public string SurfaceId => PreviewSurface.id;

        public int Direction { get; }

        public TerrainSurfaceData PreviewSurface { get; }

        public SetTerrainHeightsCommand ApplyPoint(
            Vector3 point,
            int radiusInSamples,
            int quantizedStrength)
        {
            int centerX = Mathf.RoundToInt(
                (point.x - PreviewSurface.origin.x) / PreviewSurface.sampleSpacing);
            int centerZ = Mathf.RoundToInt(
                (point.z - PreviewSurface.origin.z) / PreviewSurface.sampleSpacing);
            if (centerX == lastCenterX && centerZ == lastCenterZ)
            {
                return null;
            }

            lastCenterX = centerX;
            lastCenterZ = centerZ;
            SetTerrainHeightsCommand patch = TerrainBrushCommandFactory.Create(
                PreviewSurface,
                point,
                radiusInSamples,
                quantizedStrength,
                Direction);
            if (patch == null)
            {
                return null;
            }

            patch.Apply(previewDocument);
            startX = Math.Min(startX, patch.StartX);
            startZ = Math.Min(startZ, patch.StartZ);
            endX = Math.Max(endX, patch.StartX + patch.Width - 1);
            endZ = Math.Max(endZ, patch.StartZ + patch.Depth - 1);
            return patch;
        }

        public SetTerrainHeightsCommand CreateCommand()
        {
            if (startX == int.MaxValue)
            {
                return null;
            }

            int width = endX - startX + 1;
            int depth = endZ - startZ + 1;
            var values = new List<int>(width * depth);
            for (int z = startZ; z <= endZ; z++)
            {
                for (int x = startX; x <= endX; x++)
                {
                    values.Add(PreviewSurface.heightSamples[
                        z * PreviewSurface.sampleCountX + x]);
                }
            }

            return new SetTerrainHeightsCommand(
                PreviewSurface.id,
                startX,
                startZ,
                width,
                depth,
                values);
        }
    }
}
