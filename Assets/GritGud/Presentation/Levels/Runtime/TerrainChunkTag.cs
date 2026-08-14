using UnityEngine;

namespace GritGud.Presentation.Levels.Runtime
{
    public sealed class TerrainChunkTag : MonoBehaviour
    {
        public string SurfaceId { get; private set; }

        public void Initialize(string surfaceId)
        {
            SurfaceId = surfaceId;
        }
    }
}
