using System;
using System.Collections.Generic;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
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
    }
}
