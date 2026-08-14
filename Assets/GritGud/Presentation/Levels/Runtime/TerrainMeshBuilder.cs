using System;
using System.Linq;
using GritGud.Domain.Levels;
using UnityEngine;
using UnityEngine.Rendering;

namespace GritGud.Presentation.Levels.Runtime
{
    public static class TerrainMeshBuilder
    {
        public static Bounds CalculateBounds(TerrainSurfaceData surface)
        {
            if (surface == null)
            {
                throw new ArgumentNullException(nameof(surface));
            }

            if (surface.heightSamples == null || surface.heightSamples.Count == 0)
            {
                return new Bounds(
                    new Vector3(surface.origin.x, surface.origin.y, surface.origin.z),
                    Vector3.zero);
            }

            int minimumSample = surface.heightSamples.Min();
            int maximumSample = surface.heightSamples.Max();
            float minimumY = surface.origin.y
                + surface.minimumElevation
                + minimumSample * surface.elevationIncrement;
            float maximumY = surface.origin.y
                + surface.minimumElevation
                + maximumSample * surface.elevationIncrement;
            var minimum = new Vector3(surface.origin.x, minimumY, surface.origin.z);
            var maximum = new Vector3(
                surface.origin.x + (surface.sampleCountX - 1) * surface.sampleSpacing,
                maximumY,
                surface.origin.z + (surface.sampleCountZ - 1) * surface.sampleSpacing);
            var bounds = new Bounds();
            bounds.SetMinMax(Vector3.Min(minimum, maximum), Vector3.Max(minimum, maximum));
            return bounds;
        }

        public static Mesh BuildChunk(
            TerrainSurfaceData surface,
            int chunkX,
            int chunkZ,
            int chunkQuadSize)
        {
            if (surface == null)
            {
                throw new ArgumentNullException(nameof(surface));
            }

            int startX = chunkX * chunkQuadSize;
            int startZ = chunkZ * chunkQuadSize;
            int quadCountX = Math.Min(chunkQuadSize, surface.sampleCountX - 1 - startX);
            int quadCountZ = Math.Min(chunkQuadSize, surface.sampleCountZ - 1 - startZ);
            if (chunkX < 0 || chunkZ < 0 || chunkQuadSize <= 0 || quadCountX <= 0 || quadCountZ <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(chunkX), "The terrain chunk is outside the surface.");
            }

            int vertexCountX = quadCountX + 1;
            int vertexCountZ = quadCountZ + 1;
            var vertices = new Vector3[vertexCountX * vertexCountZ];
            var normals = new Vector3[vertices.Length];
            var uvs = new Vector2[vertices.Length];
            for (int z = 0; z < vertexCountZ; z++)
            {
                for (int x = 0; x < vertexCountX; x++)
                {
                    int sampleX = startX + x;
                    int sampleZ = startZ + z;
                    int vertexIndex = z * vertexCountX + x;
                    int quantizedHeight = surface.heightSamples[
                        sampleZ * surface.sampleCountX + sampleX];
                    vertices[vertexIndex] = new Vector3(
                        surface.origin.x + sampleX * surface.sampleSpacing,
                        surface.origin.y + surface.minimumElevation
                            + quantizedHeight * surface.elevationIncrement,
                        surface.origin.z + sampleZ * surface.sampleSpacing);
                    normals[vertexIndex] = CalculateNormal(surface, sampleX, sampleZ);
                    uvs[vertexIndex] = new Vector2(
                        sampleX / (float)(surface.sampleCountX - 1),
                        sampleZ / (float)(surface.sampleCountZ - 1));
                }
            }

            var triangles = new int[quadCountX * quadCountZ * 6];
            int triangleIndex = 0;
            for (int z = 0; z < quadCountZ; z++)
            {
                for (int x = 0; x < quadCountX; x++)
                {
                    int lowerLeft = z * vertexCountX + x;
                    int upperLeft = (z + 1) * vertexCountX + x;
                    triangles[triangleIndex++] = lowerLeft;
                    triangles[triangleIndex++] = upperLeft;
                    triangles[triangleIndex++] = lowerLeft + 1;
                    triangles[triangleIndex++] = lowerLeft + 1;
                    triangles[triangleIndex++] = upperLeft;
                    triangles[triangleIndex++] = upperLeft + 1;
                }
            }

            var mesh = new Mesh
            {
                name = $"Terrain Chunk {chunkX}, {chunkZ}",
                indexFormat = vertices.Length > ushort.MaxValue
                    ? IndexFormat.UInt32
                    : IndexFormat.UInt16,
                vertices = vertices,
                normals = normals,
                uv = uvs,
                triangles = triangles,
            };
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Vector3 CalculateNormal(
            TerrainSurfaceData surface,
            int sampleX,
            int sampleZ)
        {
            int leftX = Math.Max(0, sampleX - 1);
            int rightX = Math.Min(surface.sampleCountX - 1, sampleX + 1);
            int lowerZ = Math.Max(0, sampleZ - 1);
            int upperZ = Math.Min(surface.sampleCountZ - 1, sampleZ + 1);
            float leftHeight = SampleHeight(surface, leftX, sampleZ);
            float rightHeight = SampleHeight(surface, rightX, sampleZ);
            float lowerHeight = SampleHeight(surface, sampleX, lowerZ);
            float upperHeight = SampleHeight(surface, sampleX, upperZ);
            float xDistance = (rightX - leftX) * surface.sampleSpacing;
            float zDistance = (upperZ - lowerZ) * surface.sampleSpacing;
            return new Vector3(
                (leftHeight - rightHeight) * zDistance,
                xDistance * zDistance,
                (lowerHeight - upperHeight) * xDistance).normalized;
        }

        private static float SampleHeight(TerrainSurfaceData surface, int x, int z)
        {
            return surface.origin.y
                + surface.minimumElevation
                + surface.heightSamples[z * surface.sampleCountX + x]
                    * surface.elevationIncrement;
        }
    }
}
