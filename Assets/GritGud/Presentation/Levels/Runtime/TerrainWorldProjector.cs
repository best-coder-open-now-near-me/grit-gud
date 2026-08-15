using System;
using System.Collections.Generic;
using System.Linq;
using GritGud.Application.Levels;
using GritGud.Domain.Levels;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GritGud.Presentation.Levels.Runtime
{
    public readonly struct TerrainNavigationInvalidation
    {
        internal TerrainNavigationInvalidation(
            string surfaceId,
            int startX,
            int startZ,
            int width,
            int depth,
            bool requiresFullRefresh)
        {
            SurfaceId = surfaceId ?? string.Empty;
            StartX = startX;
            StartZ = startZ;
            Width = width;
            Depth = depth;
            RequiresFullRefresh = requiresFullRefresh;
        }

        public string SurfaceId { get; }
        public int StartX { get; }
        public int StartZ { get; }
        public int Width { get; }
        public int Depth { get; }
        public bool RequiresFullRefresh { get; }

        internal static TerrainNavigationInvalidation FullRefresh =>
            new TerrainNavigationInvalidation(string.Empty, 0, 0, 0, 0, true);
    }

    public sealed class TerrainWorldProjector : IDisposable
    {
        public const int ChunkQuadSize = 32;

        private readonly Transform parent;
        private readonly Dictionary<string, TerrainSurfaceView> surfaces =
            new Dictionary<string, TerrainSurfaceView>(StringComparer.Ordinal);
        private GameObject root;

        public event Action<TerrainNavigationInvalidation> NavigationInvalidated;

        public TerrainWorldProjector(Transform parent)
        {
            this.parent = parent;
        }

        public void Replace(LevelDocument document)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            var replacementRoot = new GameObject("Terrain Surfaces");
            replacementRoot.SetActive(false);
            replacementRoot.transform.SetParent(parent, false);
            var replacementSurfaces = new Dictionary<string, TerrainSurfaceView>(
                StringComparer.Ordinal);
            try
            {
                foreach (TerrainSurfaceData surface in document.terrainSurfaces)
                {
                    if (replacementSurfaces.ContainsKey(surface.id))
                    {
                        throw new InvalidOperationException(
                            $"Terrain surface ID '{surface.id}' is duplicated.");
                    }

                    replacementSurfaces.Add(
                        surface.id,
                        new TerrainSurfaceView(surface, replacementRoot.transform));
                }
            }
            catch
            {
                DisposeSurfaces(replacementSurfaces.Values);
                Destroy(replacementRoot);
                throw;
            }

            bool replacementVisible = root == null || root.activeSelf;
            GameObject previousRoot = root;
            TerrainSurfaceView[] previousSurfaces = surfaces.Values.ToArray();
            previousRoot?.SetActive(false);
            root = replacementRoot;
            surfaces.Clear();
            foreach (KeyValuePair<string, TerrainSurfaceView> pair in replacementSurfaces)
            {
                surfaces.Add(pair.Key, pair.Value);
            }

            root.SetActive(replacementVisible);
            DisposeSurfaces(previousSurfaces);
            Destroy(previousRoot);
        }

        public void SetVisible(bool visible)
        {
            if (root != null)
            {
                root.SetActive(visible);
            }
        }

        public void Apply(LevelDocument document, LevelSessionChangedEventArgs change)
        {
            if (change == null || change.RequiresFullProjection || root == null)
            {
                Replace(document);
                NavigationInvalidated?.Invoke(TerrainNavigationInvalidation.FullRefresh);
                return;
            }

            ITerrainLevelEditCommand[] terrainChanges = EnumerateTerrainChanges(
                    change.Command)
                .ToArray();
            if (terrainChanges.Length == 0)
            {
                return;
            }

            var projections = new List<(ITerrainLevelEditCommand Change,
                TerrainSurfaceData Surface, TerrainSurfaceView View)>(terrainChanges.Length);
            foreach (ITerrainLevelEditCommand terrainChange in terrainChanges)
            {
                TerrainSurfaceData surface = document.terrainSurfaces.FirstOrDefault(candidate =>
                    string.Equals(candidate?.id, terrainChange.SurfaceId, StringComparison.Ordinal));
                if (surface == null
                    || !surfaces.TryGetValue(
                        terrainChange.SurfaceId,
                        out TerrainSurfaceView view))
                {
                    Replace(document);
                    NavigationInvalidated?.Invoke(TerrainNavigationInvalidation.FullRefresh);
                    return;
                }

                projections.Add((terrainChange, surface, view));
            }

            foreach ((ITerrainLevelEditCommand terrainChange,
                         TerrainSurfaceData surface,
                         TerrainSurfaceView view) in projections)
            {
                view.ApplyPatch(
                    surface,
                    terrainChange.StartX,
                    terrainChange.StartZ,
                    terrainChange.Width,
                    terrainChange.Depth);
                NavigationInvalidated?.Invoke(new TerrainNavigationInvalidation(
                    terrainChange.SurfaceId,
                    terrainChange.StartX,
                    terrainChange.StartZ,
                    terrainChange.Width,
                    terrainChange.Depth,
                    false));
            }
        }

        public void PreviewPatch(
            TerrainSurfaceData surface,
            int startX,
            int startZ,
            int width,
            int depth)
        {
            if (surface == null)
            {
                throw new ArgumentNullException(nameof(surface));
            }

            if (!surfaces.TryGetValue(surface.id, out TerrainSurfaceView view))
            {
                throw new InvalidOperationException(
                    $"Terrain surface '{surface.id}' is not projected.");
            }

            view.ApplyPatch(surface, startX, startZ, width, depth);
        }

        public void Dispose()
        {
            DisposeRoot();
            NavigationInvalidated = null;
        }

        private void DisposeRoot()
        {
            root?.SetActive(false);
            DisposeSurfaces(surfaces.Values);

            surfaces.Clear();
            Destroy(root);
            root = null;
        }

        private static IEnumerable<ITerrainLevelEditCommand> EnumerateTerrainChanges(
            ILevelEditCommand command)
        {
            if (command is ITerrainLevelEditCommand terrainChange)
            {
                yield return terrainChange;
            }

            if (!(command is ILevelEditCommandGroup group))
            {
                yield break;
            }

            foreach (ILevelEditCommand child in group.Commands)
            {
                foreach (ITerrainLevelEditCommand nested in EnumerateTerrainChanges(child))
                {
                    yield return nested;
                }
            }
        }

        private static void DisposeSurfaces(IEnumerable<TerrainSurfaceView> values)
        {
            foreach (TerrainSurfaceView surface in values)
            {
                surface.Dispose();
            }
        }

        private static void Destroy(Object target)
        {
            if (target == null)
            {
                return;
            }

            if (UnityEngine.Application.isPlaying)
            {
                Object.Destroy(target);
            }
            else
            {
                Object.DestroyImmediate(target);
            }
        }

        private sealed class TerrainSurfaceView : IDisposable
        {
            private readonly GameObject root;
            private readonly Material material;
            private readonly Dictionary<Vector2Int, TerrainChunkView> chunks =
                new Dictionary<Vector2Int, TerrainChunkView>();

            public TerrainSurfaceView(TerrainSurfaceData surface, Transform parent)
            {
                root = new GameObject($"Terrain - {surface.id}");
                root.transform.SetParent(parent, false);
                material = null;
                try
                {
                    material = RuntimeMaterialFactory.CreateCelColor(
                        new Color(0.18f, 0.24f, 0.27f),
                        "Terrain Surface Material");
                    RebuildAll(surface);
                }
                catch
                {
                    Dispose();
                    Destroy(root);
                    throw;
                }
            }

            public void ApplyPatch(
                TerrainSurfaceData surface,
                int startX,
                int startZ,
                int width,
                int depth)
            {
                int minimumChunkX = Mathf.Max(0, (startX - 1) / ChunkQuadSize);
                int minimumChunkZ = Mathf.Max(0, (startZ - 1) / ChunkQuadSize);
                int maximumChunkX = Mathf.Min(
                    (surface.sampleCountX - 2) / ChunkQuadSize,
                    (startX + width - 1) / ChunkQuadSize);
                int maximumChunkZ = Mathf.Min(
                    (surface.sampleCountZ - 2) / ChunkQuadSize,
                    (startZ + depth - 1) / ChunkQuadSize);
                for (int chunkZ = minimumChunkZ; chunkZ <= maximumChunkZ; chunkZ++)
                {
                    for (int chunkX = minimumChunkX; chunkX <= maximumChunkX; chunkX++)
                    {
                        RebuildChunk(surface, chunkX, chunkZ);
                    }
                }
            }

            public void Dispose()
            {
                foreach (TerrainChunkView chunk in chunks.Values)
                {
                    chunk.Dispose();
                }

                chunks.Clear();
                Destroy(material);
            }

            private void RebuildAll(TerrainSurfaceData surface)
            {
                int chunkCountX = Mathf.CeilToInt((surface.sampleCountX - 1f) / ChunkQuadSize);
                int chunkCountZ = Mathf.CeilToInt((surface.sampleCountZ - 1f) / ChunkQuadSize);
                for (int chunkZ = 0; chunkZ < chunkCountZ; chunkZ++)
                {
                    for (int chunkX = 0; chunkX < chunkCountX; chunkX++)
                    {
                        RebuildChunk(surface, chunkX, chunkZ);
                    }
                }
            }

            private void RebuildChunk(TerrainSurfaceData surface, int chunkX, int chunkZ)
            {
                var key = new Vector2Int(chunkX, chunkZ);
                if (!chunks.TryGetValue(key, out TerrainChunkView chunk))
                {
                    chunk = new TerrainChunkView(
                        root.transform,
                        surface.id,
                        chunkX,
                        chunkZ,
                        material);
                    chunks.Add(key, chunk);
                }

                chunk.ReplaceMesh(TerrainMeshBuilder.BuildChunk(surface, chunkX, chunkZ, ChunkQuadSize));
            }
        }

        private sealed class TerrainChunkView : IDisposable
        {
            private readonly GameObject root;
            private readonly MeshFilter filter;
            private readonly MeshCollider collider;

            public TerrainChunkView(
                Transform parent,
                string surfaceId,
                int chunkX,
                int chunkZ,
                Material material)
            {
                root = new GameObject($"Chunk {chunkX}, {chunkZ}");
                root.transform.SetParent(parent, false);
                root.AddComponent<TerrainChunkTag>().Initialize(surfaceId);
                filter = root.AddComponent<MeshFilter>();
                root.AddComponent<MeshRenderer>().sharedMaterial = material;
                collider = root.AddComponent<MeshCollider>();
            }

            public void ReplaceMesh(Mesh mesh)
            {
                Mesh previous = filter.sharedMesh;
                filter.sharedMesh = mesh;
                collider.sharedMesh = null;
                collider.sharedMesh = mesh;
                Destroy(previous);
            }

            public void Dispose()
            {
                Destroy(filter.sharedMesh);
            }
        }
    }
}
