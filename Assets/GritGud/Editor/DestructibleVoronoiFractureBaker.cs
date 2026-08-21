using System;
using System.Collections.Generic;
using System.IO;
using GritGud.Presentation.Gameplay;
using GritGud.Presentation.Levels.Runtime;
using UnityEditor;
using UnityEngine;

namespace GritGud.Editor
{
    public static class DestructibleVoronoiFractureBaker
    {
        private const string CatalogPath =
            "Assets/GritGud/Content/Resources/DefaultLevelArchetypeCatalog.asset";
        private const string OutputRoot =
            "Assets/GritGud/Content/Generated/Fractures";
        private const int DefaultChunkCount = 12;

        [MenuItem("Grit Gud/Content/Rebuild Default Voronoi Fractures")]
        public static void RebuildDefaultProfiles()
        {
            var recipes = new[]
            {
                new FractureRecipe(
                    "prop.crate.standard",
                    "fracture.crate.wood",
                    "CrateWood",
                    "Assets/Synty/PolygonGeneric/Prefabs/Props/SM_Gen_Prop_Crate_01.prefab",
                    DefaultChunkCount,
                    seed: 317,
                    cylindricalSeeds: false,
                    interiorTint: new Color(0.42f, 0.24f, 0.11f, 1f),
                    debrisImpulse: 2.8f),
                new FractureRecipe(
                    "prop.barrel.metal",
                    "fracture.barrel.metal",
                    "BarrelMetal",
                    "Assets/Synty/PolygonGeneric/Prefabs/Props/SM_Gen_Prop_Barrel_Metal_01.prefab",
                    DefaultChunkCount,
                    seed: 911,
                    cylindricalSeeds: true,
                    interiorTint: new Color(0.16f, 0.19f, 0.22f, 1f),
                    debrisImpulse: 2.35f),
            };

            EnsureFolder(OutputRoot);
            try
            {
                foreach (FractureRecipe recipe in recipes)
                {
                    DestructibleFractureProfile profile = BuildProfile(recipe);
                    AssignProfile(recipe.ArchetypeId, profile);
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log(
                    "Rebuilt deterministic Voronoi fracture profiles for "
                    + "the default crate and metal barrel archetypes.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                throw;
            }
        }

        private static DestructibleFractureProfile BuildProfile(
            FractureRecipe recipe)
        {
            EnsureSourceMeshesReadable(recipe.SourcePrefabPath, out var importers);
            try
            {
                GameObject sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    recipe.SourcePrefabPath);
                if (sourcePrefab == null)
                {
                    throw new InvalidOperationException(
                        $"Fracture source prefab is missing: {recipe.SourcePrefabPath}");
                }

                SourceGeometry source = ReadSourceGeometry(sourcePrefab);
                string folder = $"{OutputRoot}/{recipe.AssetName}";
                EnsureFolder(folder);
                Material interior = CreateOrUpdateInteriorMaterial(
                    recipe.InteriorTint,
                    $"{folder}/{recipe.AssetName}Interior.mat");
                Material[] chunkMaterials = new Material[source.Materials.Length + 1];
                Array.Copy(source.Materials, chunkMaterials, source.Materials.Length);
                chunkMaterials[chunkMaterials.Length - 1] = interior;

                Vector3[] seeds = CreateSeeds(
                    source.Bounds,
                    recipe.ChunkCount,
                    recipe.Seed,
                    recipe.CylindricalSeeds);
                var root = new GameObject($"{recipe.AssetName}Fractured");
                var centers = new Vector3[recipe.ChunkCount];
                try
                {
                    for (int index = 0; index < seeds.Length; index++)
                    {
                        Mesh transientMesh = BuildCellMesh(
                            source,
                            seeds,
                            index,
                            source.Materials.Length,
                            out Vector3 center);
                        centers[index] = center;
                        string meshPath =
                            $"{folder}/{recipe.AssetName}Chunk{index:D2}.asset";
                        Mesh mesh = CreateOrUpdateMesh(transientMesh, meshPath);

                        var chunk = new GameObject($"Chunk {index:D2}");
                        chunk.transform.SetParent(root.transform, worldPositionStays: false);
                        chunk.transform.localPosition = center;
                        chunk.AddComponent<MeshFilter>().sharedMesh = mesh;
                        chunk.AddComponent<MeshRenderer>().sharedMaterials = chunkMaterials;
                        var collider = chunk.AddComponent<MeshCollider>();
                        collider.sharedMesh = mesh;
                        collider.convex = true;
                        chunk.AddComponent<DestructibleFractureChunk>().Configure(index);
                    }

                    string prefabPath =
                        $"{folder}/{recipe.AssetName}Fractured.prefab";
                    GameObject prefab = PrefabUtility.SaveAsPrefabAsset(
                        root,
                        prefabPath);
                    if (prefab == null)
                    {
                        throw new InvalidOperationException(
                            $"Could not save fracture prefab '{prefabPath}'.");
                    }

                    string profilePath =
                        $"{folder}/{recipe.AssetName}FractureProfile.asset";
                    DestructibleFractureProfile profile =
                        AssetDatabase.LoadAssetAtPath<DestructibleFractureProfile>(
                            profilePath);
                    if (profile == null)
                    {
                        profile = ScriptableObject.CreateInstance<
                            DestructibleFractureProfile>();
                        AssetDatabase.CreateAsset(profile, profilePath);
                    }

                    profile.Configure(
                        recipe.ProfileId,
                        prefab,
                        centers,
                        recipe.DebrisImpulse,
                        lifetime: 3.5f);
                    EditorUtility.SetDirty(profile);
                    return profile;
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }
            finally
            {
                RestoreSourceMeshReadability(importers);
            }
        }

        private static SourceGeometry ReadSourceGeometry(GameObject prefab)
        {
            MeshFilter[] filters = prefab.GetComponentsInChildren<MeshFilter>(true);
            if (filters.Length != 1)
            {
                throw new InvalidOperationException(
                    $"Voronoi baking currently requires one source mesh; "
                    + $"'{prefab.name}' contains {filters.Length}.");
            }

            MeshFilter filter = filters[0];
            Mesh mesh = filter.sharedMesh;
            MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
            if (mesh == null || renderer == null)
            {
                throw new InvalidOperationException(
                    $"Fracture source '{prefab.name}' requires a mesh renderer.");
            }

            Material[] materials = renderer.sharedMaterials;
            if (materials.Length != mesh.subMeshCount || materials.Length == 0)
            {
                throw new InvalidOperationException(
                    $"Fracture source '{prefab.name}' has mismatched materials.");
            }

            Vector3[] positions = mesh.vertices;
            Vector3[] normals = mesh.normals;
            Vector2[] uvs = mesh.uv;
            Matrix4x4 toRoot = prefab.transform.worldToLocalMatrix
                * filter.transform.localToWorldMatrix;
            Matrix4x4 normalToRoot = toRoot.inverse.transpose;
            var polygons = new List<Polygon>();
            Bounds? bounds = null;
            for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
            {
                int[] triangles = mesh.GetTriangles(submesh);
                for (int index = 0; index < triangles.Length; index += 3)
                {
                    var vertices = new List<Vertex>(3);
                    for (int corner = 0; corner < 3; corner++)
                    {
                        int vertexIndex = triangles[index + corner];
                        Vector3 position = toRoot.MultiplyPoint3x4(
                            positions[vertexIndex]);
                        Vector3 normal = normals.Length == positions.Length
                            ? normalToRoot.MultiplyVector(normals[vertexIndex]).normalized
                            : Vector3.up;
                        Vector2 uv = uvs.Length == positions.Length
                            ? uvs[vertexIndex]
                            : Vector2.zero;
                        vertices.Add(new Vertex(position, normal, uv));
                        if (bounds.HasValue)
                        {
                            Bounds expanded = bounds.Value;
                            expanded.Encapsulate(position);
                            bounds = expanded;
                        }
                        else
                        {
                            bounds = new Bounds(position, Vector3.zero);
                        }
                    }

                    polygons.Add(new Polygon(vertices, submesh));
                }
            }

            if (!bounds.HasValue || polygons.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Fracture source '{prefab.name}' contains no triangles.");
            }

            return new SourceGeometry(polygons, materials, bounds.Value);
        }

        private static Mesh BuildCellMesh(
            SourceGeometry source,
            IReadOnlyList<Vector3> seeds,
            int cellIndex,
            int interiorSubmesh,
            out Vector3 center)
        {
            var polygons = new List<Polygon>(source.Polygons.Count);
            foreach (Polygon sourcePolygon in source.Polygons)
            {
                polygons.Add(sourcePolygon.Copy());
            }

            Vector3 seed = seeds[cellIndex];
            for (int otherIndex = 0; otherIndex < seeds.Count; otherIndex++)
            {
                if (otherIndex == cellIndex)
                {
                    continue;
                }

                Vector3 normal = (seeds[otherIndex] - seed).normalized;
                Vector3 midpoint = (seed + seeds[otherIndex]) * 0.5f;
                float planeDistance = Vector3.Dot(normal, midpoint);
                var clipped = new List<Polygon>(polygons.Count + 1);
                var intersections = new List<Vector3>();
                foreach (Polygon polygon in polygons)
                {
                    Polygon result = ClipPolygon(
                        polygon,
                        normal,
                        planeDistance,
                        intersections);
                    if (result != null)
                    {
                        clipped.Add(result);
                    }
                }

                AddCapPolygon(
                    clipped,
                    intersections,
                    normal,
                    interiorSubmesh);
                polygons = clipped;
                if (polygons.Count == 0)
                {
                    break;
                }
            }

            var meshVertices = new List<Vector3>();
            var meshNormals = new List<Vector3>();
            var meshUvs = new List<Vector2>();
            var submeshTriangles = new List<int>[source.Materials.Length + 1];
            for (int index = 0; index < submeshTriangles.Length; index++)
            {
                submeshTriangles[index] = new List<int>();
            }

            foreach (Polygon polygon in polygons)
            {
                if (polygon.Vertices.Count < 3)
                {
                    continue;
                }

                int firstVertex = meshVertices.Count;
                foreach (Vertex vertex in polygon.Vertices)
                {
                    meshVertices.Add(vertex.Position);
                    meshNormals.Add(vertex.Normal);
                    meshUvs.Add(vertex.Uv);
                }

                for (int corner = 1; corner < polygon.Vertices.Count - 1; corner++)
                {
                    submeshTriangles[polygon.Submesh].Add(firstVertex);
                    submeshTriangles[polygon.Submesh].Add(firstVertex + corner);
                    submeshTriangles[polygon.Submesh].Add(firstVertex + corner + 1);
                }
            }

            if (meshVertices.Count < 4)
            {
                throw new InvalidOperationException(
                    $"Voronoi cell {cellIndex} contains no usable geometry.");
            }

            center = Vector3.zero;
            foreach (Vector3 position in meshVertices)
            {
                center += position;
            }
            center /= meshVertices.Count;
            for (int index = 0; index < meshVertices.Count; index++)
            {
                meshVertices[index] -= center;
            }

            var mesh = new Mesh
            {
                name = $"Voronoi Chunk {cellIndex:D2}",
                indexFormat = meshVertices.Count > ushort.MaxValue
                    ? UnityEngine.Rendering.IndexFormat.UInt32
                    : UnityEngine.Rendering.IndexFormat.UInt16,
            };
            mesh.SetVertices(meshVertices);
            mesh.SetNormals(meshNormals);
            mesh.SetUVs(0, meshUvs);
            mesh.subMeshCount = submeshTriangles.Length;
            for (int submesh = 0; submesh < submeshTriangles.Length; submesh++)
            {
                mesh.SetTriangles(submeshTriangles[submesh], submesh);
            }
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }

        private static Polygon ClipPolygon(
            Polygon polygon,
            Vector3 planeNormal,
            float planeDistance,
            ICollection<Vector3> intersections)
        {
            const float tolerance = 0.00001f;
            var result = new List<Vertex>(polygon.Vertices.Count + 1);
            Vertex previous = polygon.Vertices[polygon.Vertices.Count - 1];
            float previousDistance =
                Vector3.Dot(planeNormal, previous.Position) - planeDistance;
            bool previousInside = previousDistance <= tolerance;
            foreach (Vertex current in polygon.Vertices)
            {
                float currentDistance =
                    Vector3.Dot(planeNormal, current.Position) - planeDistance;
                bool currentInside = currentDistance <= tolerance;
                if (currentInside != previousInside)
                {
                    float denominator = previousDistance - currentDistance;
                    float amount = Mathf.Abs(denominator) <= tolerance
                        ? 0f
                        : Mathf.Clamp01(previousDistance / denominator);
                    Vertex intersection = Vertex.Lerp(previous, current, amount);
                    result.Add(intersection);
                    intersections.Add(intersection.Position);
                }

                if (currentInside)
                {
                    result.Add(current);
                }

                previous = current;
                previousDistance = currentDistance;
                previousInside = currentInside;
            }

            return result.Count >= 3
                ? new Polygon(result, polygon.Submesh)
                : null;
        }

        private static void AddCapPolygon(
            ICollection<Polygon> polygons,
            IEnumerable<Vector3> candidates,
            Vector3 normal,
            int interiorSubmesh)
        {
            const float mergeDistanceSquared = 0.0000001f;
            var unique = new List<Vector3>();
            foreach (Vector3 candidate in candidates)
            {
                bool duplicate = false;
                foreach (Vector3 existing in unique)
                {
                    if ((candidate - existing).sqrMagnitude
                        <= mergeDistanceSquared)
                    {
                        duplicate = true;
                        break;
                    }
                }

                if (!duplicate)
                {
                    unique.Add(candidate);
                }
            }

            if (unique.Count < 3)
            {
                return;
            }

            Vector3 center = Vector3.zero;
            foreach (Vector3 point in unique)
            {
                center += point;
            }
            center /= unique.Count;

            Vector3 reference = Mathf.Abs(normal.y) < 0.9f
                ? Vector3.up
                : Vector3.right;
            Vector3 axisU = Vector3.Cross(normal, reference).normalized;
            Vector3 axisV = Vector3.Cross(normal, axisU).normalized;
            unique.Sort((left, right) =>
            {
                Vector3 leftOffset = left - center;
                Vector3 rightOffset = right - center;
                float leftAngle = Mathf.Atan2(
                    Vector3.Dot(leftOffset, axisV),
                    Vector3.Dot(leftOffset, axisU));
                float rightAngle = Mathf.Atan2(
                    Vector3.Dot(rightOffset, axisV),
                    Vector3.Dot(rightOffset, axisU));
                return leftAngle.CompareTo(rightAngle);
            });

            var vertices = new List<Vertex>(unique.Count);
            foreach (Vector3 point in unique)
            {
                Vector3 offset = point - center;
                vertices.Add(new Vertex(
                    point,
                    normal,
                    new Vector2(
                        Vector3.Dot(offset, axisU),
                        Vector3.Dot(offset, axisV))));
            }
            polygons.Add(new Polygon(vertices, interiorSubmesh));
        }

        private static Vector3[] CreateSeeds(
            Bounds bounds,
            int count,
            int seed,
            bool cylindrical)
        {
            var result = new Vector3[count];
            for (int index = 0; index < count; index++)
            {
                int sample = seed + index + 1;
                float y = Mathf.Lerp(
                    bounds.min.y + (bounds.size.y * 0.08f),
                    bounds.max.y - (bounds.size.y * 0.08f),
                    Halton(sample, 3));
                if (cylindrical)
                {
                    float angle = Halton(sample, 2) * Mathf.PI * 2f;
                    float radius = Mathf.Sqrt(Halton(sample, 5)) * 0.72f;
                    result[index] = new Vector3(
                        bounds.center.x + Mathf.Cos(angle) * bounds.extents.x * radius,
                        y,
                        bounds.center.z + Mathf.Sin(angle) * bounds.extents.z * radius);
                }
                else
                {
                    result[index] = new Vector3(
                        Mathf.Lerp(
                            bounds.min.x + (bounds.size.x * 0.08f),
                            bounds.max.x - (bounds.size.x * 0.08f),
                            Halton(sample, 2)),
                        y,
                        Mathf.Lerp(
                            bounds.min.z + (bounds.size.z * 0.08f),
                            bounds.max.z - (bounds.size.z * 0.08f),
                            Halton(sample, 5)));
                }
            }

            return result;
        }

        private static float Halton(int index, int basis)
        {
            float fraction = 1f;
            float result = 0f;
            while (index > 0)
            {
                fraction /= basis;
                result += fraction * (index % basis);
                index /= basis;
            }

            return result;
        }

        private static Mesh CreateOrUpdateMesh(Mesh source, string assetPath)
        {
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(source, assetPath);
                return source;
            }

            EditorUtility.CopySerialized(source, existing);
            UnityEngine.Object.DestroyImmediate(source);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        private static Material CreateOrUpdateInteriorMaterial(
            Color tint,
            string assetPath)
        {
            Shader interiorShader = Shader.Find("GritGud/CelSurface")
                ?? throw new InvalidOperationException(
                    "Fracture interiors require the project-owned CelSurface shader.");
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (existing == null)
            {
                existing = new Material(interiorShader);
                AssetDatabase.CreateAsset(existing, assetPath);
            }
            else
            {
                existing.shader = interiorShader;
            }

            if (existing.HasProperty("_BaseColor"))
            {
                existing.SetColor("_BaseColor", tint);
            }
            else if (existing.HasProperty("_Color"))
            {
                existing.SetColor("_Color", tint);
            }
            EditorUtility.SetDirty(existing);
            return existing;
        }

        private static void AssignProfile(
            string archetypeId,
            DestructibleFractureProfile profile)
        {
            LevelArchetypeCatalog catalog =
                AssetDatabase.LoadAssetAtPath<LevelArchetypeCatalog>(CatalogPath);
            if (catalog == null)
            {
                throw new InvalidOperationException(
                    $"Default archetype catalog is missing: {CatalogPath}");
            }

            var serialized = new SerializedObject(catalog);
            SerializedProperty entries = serialized.FindProperty("entries");
            for (int index = 0; index < entries.arraySize; index++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(index);
                if (!string.Equals(
                        entry.FindPropertyRelative("archetypeId").stringValue,
                        archetypeId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                entry.FindPropertyRelative("fractureProfile").objectReferenceValue =
                    profile;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(catalog);
                return;
            }

            throw new InvalidOperationException(
                $"Archetype '{archetypeId}' is absent from the default catalog.");
        }

        private static void EnsureSourceMeshesReadable(
            string prefabPath,
            out List<ImporterReadability> changedImporters)
        {
            changedImporters = new List<ImporterReadability>();
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                throw new InvalidOperationException(
                    $"Fracture source prefab is missing: {prefabPath}");
            }

            var visited = new HashSet<string>(StringComparer.Ordinal);
            foreach (MeshFilter filter in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                string meshPath = AssetDatabase.GetAssetPath(filter.sharedMesh);
                if (!visited.Add(meshPath)
                    || AssetImporter.GetAtPath(meshPath) is not ModelImporter importer
                    || importer.isReadable)
                {
                    continue;
                }

                changedImporters.Add(new ImporterReadability(importer, false));
                importer.isReadable = true;
                importer.SaveAndReimport();
            }
        }

        private static void RestoreSourceMeshReadability(
            IEnumerable<ImporterReadability> values)
        {
            foreach (ImporterReadability value in values)
            {
                if (value.Importer == null)
                {
                    continue;
                }
                value.Importer.isReadable = value.WasReadable;
                value.Importer.SaveAndReimport();
            }
        }

        private static void EnsureFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath))
            {
                return;
            }

            string parent = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            string name = Path.GetFileName(assetPath);
            if (string.IsNullOrWhiteSpace(parent)
                || string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException(
                    $"Invalid asset folder '{assetPath}'.",
                    nameof(assetPath));
            }

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        private readonly struct FractureRecipe
        {
            public FractureRecipe(
                string archetypeId,
                string profileId,
                string assetName,
                string sourcePrefabPath,
                int chunkCount,
                int seed,
                bool cylindricalSeeds,
                Color interiorTint,
                float debrisImpulse)
            {
                ArchetypeId = archetypeId;
                ProfileId = profileId;
                AssetName = assetName;
                SourcePrefabPath = sourcePrefabPath;
                ChunkCount = chunkCount;
                Seed = seed;
                CylindricalSeeds = cylindricalSeeds;
                InteriorTint = interiorTint;
                DebrisImpulse = debrisImpulse;
            }

            public string ArchetypeId { get; }
            public string ProfileId { get; }
            public string AssetName { get; }
            public string SourcePrefabPath { get; }
            public int ChunkCount { get; }
            public int Seed { get; }
            public bool CylindricalSeeds { get; }
            public Color InteriorTint { get; }
            public float DebrisImpulse { get; }
        }

        private readonly struct ImporterReadability
        {
            public ImporterReadability(ModelImporter importer, bool wasReadable)
            {
                Importer = importer;
                WasReadable = wasReadable;
            }

            public ModelImporter Importer { get; }
            public bool WasReadable { get; }
        }

        private sealed class SourceGeometry
        {
            public SourceGeometry(
                List<Polygon> polygons,
                Material[] materials,
                Bounds bounds)
            {
                Polygons = polygons;
                Materials = materials;
                Bounds = bounds;
            }

            public List<Polygon> Polygons { get; }
            public Material[] Materials { get; }
            public Bounds Bounds { get; }
        }

        private sealed class Polygon
        {
            public Polygon(List<Vertex> vertices, int submesh)
            {
                Vertices = vertices;
                Submesh = submesh;
            }

            public List<Vertex> Vertices { get; }
            public int Submesh { get; }

            public Polygon Copy() => new Polygon(
                new List<Vertex>(Vertices),
                Submesh);
        }

        private readonly struct Vertex
        {
            public Vertex(Vector3 position, Vector3 normal, Vector2 uv)
            {
                Position = position;
                Normal = normal;
                Uv = uv;
            }

            public Vector3 Position { get; }
            public Vector3 Normal { get; }
            public Vector2 Uv { get; }

            public static Vertex Lerp(Vertex from, Vertex to, float amount) =>
                new Vertex(
                    Vector3.LerpUnclamped(from.Position, to.Position, amount),
                    Vector3.LerpUnclamped(from.Normal, to.Normal, amount).normalized,
                    Vector2.LerpUnclamped(from.Uv, to.Uv, amount));
        }
    }
}
