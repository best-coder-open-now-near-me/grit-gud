using GritGud.Presentation.Levels.Runtime;
using UnityEngine;

namespace GritGud.Presentation.LevelEditing
{
    public sealed class RuntimeBoundsOutline : MonoBehaviour
    {
        private const float EdgeWidth = 0.035f;
        private readonly Transform[] edges = new Transform[12];
        private Material material;

        public void Initialize(Color color)
        {
            material = RuntimeMaterialFactory.CreateColor(color, "Runtime Bounds Outline Material");

            for (int index = 0; index < edges.Length; index++)
            {
                GameObject edge = GameObject.CreatePrimitive(PrimitiveType.Cube);
                edge.name = $"Edge {index + 1}";
                edge.transform.SetParent(transform, false);
                DestroyObject(edge.GetComponent<Collider>());
                edge.GetComponent<Renderer>().sharedMaterial = material;
                edges[index] = edge.transform;
            }
        }

        public void SetBounds(Bounds bounds)
        {
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            var corners = new[]
            {
                new Vector3(min.x, min.y, min.z),
                new Vector3(max.x, min.y, min.z),
                new Vector3(max.x, min.y, max.z),
                new Vector3(min.x, min.y, max.z),
                new Vector3(min.x, max.y, min.z),
                new Vector3(max.x, max.y, min.z),
                new Vector3(max.x, max.y, max.z),
                new Vector3(min.x, max.y, max.z),
            };

            SetEdge(0, corners[0], corners[1]);
            SetEdge(1, corners[1], corners[2]);
            SetEdge(2, corners[2], corners[3]);
            SetEdge(3, corners[3], corners[0]);
            SetEdge(4, corners[4], corners[5]);
            SetEdge(5, corners[5], corners[6]);
            SetEdge(6, corners[6], corners[7]);
            SetEdge(7, corners[7], corners[4]);
            SetEdge(8, corners[0], corners[4]);
            SetEdge(9, corners[1], corners[5]);
            SetEdge(10, corners[2], corners[6]);
            SetEdge(11, corners[3], corners[7]);
        }

        private void OnDestroy()
        {
            if (material != null)
            {
                DestroyObject(material);
            }
        }

        private static void DestroyObject(Object target)
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

        private void SetEdge(int index, Vector3 start, Vector3 end)
        {
            Transform edge = edges[index];
            Vector3 direction = end - start;
            edge.position = (start + end) * 0.5f;
            edge.rotation = Quaternion.FromToRotation(Vector3.right, direction);
            edge.localScale = new Vector3(direction.magnitude, EdgeWidth, EdgeWidth);
        }
    }
}
