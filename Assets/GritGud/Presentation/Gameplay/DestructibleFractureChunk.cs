using System;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class DestructibleFractureChunk : MonoBehaviour
    {
        [SerializeField] private int chunkIndex = -1;

        public int ChunkIndex => chunkIndex;

        public Vector3 LocalCenter => transform.localPosition;

        public void Configure(int index)
        {
            if (index < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            chunkIndex = index;
        }
    }
}
