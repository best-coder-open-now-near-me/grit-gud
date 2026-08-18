using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Levels;
using GritGud.Presentation.Levels.Runtime;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    public static class GameplaySpatialContentAssembler
    {
        public static IReadOnlyDictionary<string, GameplayFractureSpatialProfile>
            AssembleFractureProfiles(
                LevelDocument level,
                LevelArchetypeCatalog archetypes)
        {
            if (level == null) throw new ArgumentNullException(nameof(level));
            if (archetypes == null)
                throw new ArgumentNullException(nameof(archetypes));
            var profiles = new Dictionary<string, GameplayFractureSpatialProfile>(
                StringComparer.Ordinal);
            foreach (LevelEntity entity in level.entities)
            {
                if (entity?.destructible?.enabled != true
                    || entity.coverVolumes == null
                    || entity.coverVolumes.Count == 0)
                    continue;
                if (!archetypes.TryGet(
                        entity.archetypeId,
                        out LevelArchetypeDefinition archetype))
                {
                    throw new InvalidOperationException(
                        $"Tactical destructible '{entity.id}' uses unknown "
                        + $"archetype '{entity.archetypeId}'.");
                }

                DestructibleFractureProfile fracture = archetype.FractureProfile;
                if (fracture == null || profiles.ContainsKey(entity.archetypeId))
                    continue;
                profiles.Add(
                    entity.archetypeId,
                    fracture.CreateSpatialProfile());
            }

            return new ReadOnlyDictionary<
                string,
                GameplayFractureSpatialProfile>(profiles);
        }
    }

    [CreateAssetMenu(
        fileName = "DestructibleFractureProfile",
        menuName = "Grit Gud/Destructible Fracture Profile")]
    public sealed class DestructibleFractureProfile : ScriptableObject
    {
        [SerializeField] private string profileId = string.Empty;
        [SerializeField] private GameObject fracturedPrefab;
        [SerializeField] private Vector3[] chunkCenters = Array.Empty<Vector3>();
        [SerializeField] private float debrisImpulse = 2.4f;
        [SerializeField] private float debrisLifetime = 3.5f;

        public string ProfileId => profileId;

        public GameObject FracturedPrefab => fracturedPrefab;

        public IReadOnlyList<Vector3> ChunkCenters => chunkCenters;

        public int ChunkCount => chunkCenters?.Length ?? 0;

        public float DebrisImpulse => Mathf.Max(0f, debrisImpulse);

        public float DebrisLifetime => Mathf.Max(0.1f, debrisLifetime);

        public GameplayFractureSpatialProfile CreateSpatialProfile()
        {
            if (fracturedPrefab == null)
            {
                throw new InvalidOperationException(
                    $"Fracture profile '{profileId}' has no fractured prefab.");
            }

            DestructibleFractureChunk[] chunks =
                fracturedPrefab.GetComponentsInChildren<
                    DestructibleFractureChunk>(true);
            if (chunks.Length != ChunkCount)
            {
                throw new InvalidOperationException(
                    $"Fracture profile '{profileId}' declares {ChunkCount} chunks "
                    + $"but its prefab contains {chunks.Length}.");
            }

            var volumes = new GameplayLocalSpatialVolume[chunks.Length];
            var assigned = new bool[chunks.Length];
            foreach (DestructibleFractureChunk chunk in chunks)
            {
                int index = chunk.ChunkIndex;
                if (index < 0 || index >= chunks.Length || assigned[index])
                {
                    throw new InvalidOperationException(
                        $"Fracture profile '{profileId}' has an invalid or "
                        + $"duplicate chunk index {index}.");
                }

                Bounds bounds = CalculateRootLocalBounds(chunk.transform);
                volumes[index] = new GameplayLocalSpatialVolume(
                    ToPosition(bounds.center),
                    ToPosition(bounds.size));
                assigned[index] = true;
            }

            return new GameplayFractureSpatialProfile(profileId, volumes);
        }

        public int FindClosestChunkIndex(Vector3 localPoint)
        {
            if (chunkCenters == null || chunkCenters.Length == 0)
            {
                return -1;
            }

            int closestIndex = 0;
            float closestDistance = float.PositiveInfinity;
            for (int index = 0; index < chunkCenters.Length; index++)
            {
                float distance = (chunkCenters[index] - localPoint).sqrMagnitude;
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestIndex = index;
                }
            }

            return closestIndex;
        }

        public void Configure(
            string id,
            GameObject prefab,
            Vector3[] centers,
            float impulse,
            float lifetime)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException(
                    "Fracture profiles require a stable identifier.",
                    nameof(id));
            }

            if (prefab == null)
            {
                throw new ArgumentNullException(nameof(prefab));
            }

            if (centers == null || centers.Length < 2)
            {
                throw new ArgumentException(
                    "Fracture profiles require at least two baked chunks.",
                    nameof(centers));
            }

            profileId = id;
            fracturedPrefab = prefab;
            chunkCenters = (Vector3[])centers.Clone();
            debrisImpulse = Mathf.Max(0f, impulse);
            debrisLifetime = Mathf.Max(0.1f, lifetime);
        }

        private Bounds CalculateRootLocalBounds(Transform chunk)
        {
            Collider[] colliders = chunk.GetComponentsInChildren<Collider>(true);
            if (colliders.Length == 0)
            {
                throw new InvalidOperationException(
                    $"Fracture profile '{profileId}' chunk "
                    + $"{chunk.GetComponent<DestructibleFractureChunk>().ChunkIndex} "
                    + "has no collision volume.");
            }

            Bounds? combined = null;
            Matrix4x4 rootFromWorld = fracturedPrefab.transform.worldToLocalMatrix;
            foreach (Collider collider in colliders)
            {
                Bounds local = GetColliderLocalBounds(collider);
                Matrix4x4 rootFromCollider =
                    rootFromWorld * collider.transform.localToWorldMatrix;
                Vector3 minimum = local.min;
                Vector3 maximum = local.max;
                for (int x = 0; x < 2; x++)
                {
                    for (int y = 0; y < 2; y++)
                    {
                        for (int z = 0; z < 2; z++)
                        {
                            Vector3 point = rootFromCollider.MultiplyPoint3x4(
                                new Vector3(
                                    x == 0 ? minimum.x : maximum.x,
                                    y == 0 ? minimum.y : maximum.y,
                                    z == 0 ? minimum.z : maximum.z));
                            if (combined.HasValue)
                            {
                                Bounds expanded = combined.Value;
                                expanded.Encapsulate(point);
                                combined = expanded;
                            }
                            else
                            {
                                combined = new Bounds(point, Vector3.zero);
                            }
                        }
                    }
                }
            }

            return combined.Value;
        }

        private Bounds GetColliderLocalBounds(Collider collider)
        {
            switch (collider)
            {
                case MeshCollider mesh when mesh.sharedMesh != null:
                    return mesh.sharedMesh.bounds;
                case BoxCollider box:
                    return new Bounds(box.center, box.size);
                case SphereCollider sphere:
                    return new Bounds(
                        sphere.center,
                        Vector3.one * (sphere.radius * 2f));
                case CapsuleCollider capsule:
                    Vector3 size = Vector3.one * (capsule.radius * 2f);
                    if (capsule.direction == 0) size.x = capsule.height;
                    else if (capsule.direction == 1) size.y = capsule.height;
                    else size.z = capsule.height;
                    return new Bounds(capsule.center, size);
                default:
                    throw new InvalidOperationException(
                        $"Fracture profile '{profileId}' uses unsupported "
                        + $"collider '{collider.GetType().Name}'.");
            }
        }

        private static GameplayPosition ToPosition(Vector3 value) =>
            new GameplayPosition(value.x, value.y, value.z);
    }
}
