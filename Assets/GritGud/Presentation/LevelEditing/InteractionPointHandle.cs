using System;
using System.Collections.Generic;
using GritGud.Application.Levels;
using GritGud.Domain.Levels;
using GritGud.Presentation.Levels.Runtime;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GritGud.Presentation.LevelEditing
{
    public sealed class InteractionPointHandle : MonoBehaviour
    {
        public string EntityId { get; private set; }

        public string PointId { get; private set; }

        public void Initialize(string entityId, InteractionPointData point)
        {
            EntityId = entityId;
            PointId = point.id;
            name = $"Interaction Point [{point.id}]";
            transform.localPosition = new Vector3(
                point.localPosition.x,
                point.localPosition.y,
                point.localPosition.z);
            float diameter = Mathf.Clamp(point.radius * 0.18f, 0.14f, 0.45f);
            transform.localScale = Vector3.one * diameter;
        }
    }

    public sealed class InteractionPointHandleProjector : IDisposable
    {
        private readonly List<GameObject> handles = new List<GameObject>();
        private readonly HashSet<string> renderedEntityIds = new HashSet<string>(StringComparer.Ordinal);

        public void Refresh(LevelDocument document, LevelSelectionModel selection, LevelWorldProjector projector)
        {
            Clear();
            if (document == null || selection == null || projector == null)
            {
                return;
            }

            foreach (LevelSelectionTarget target in selection.Targets)
            {
                if ((target.Kind != LevelSelectionKind.Entity
                     && target.Kind != LevelSelectionKind.InteractionPoint)
                    || !renderedEntityIds.Add(target.EntityId)
                    || !projector.TryGetEntity(target.EntityId, out LevelEntityView view))
                {
                    continue;
                }

                LevelEntity entity = document.entities.Find(candidate =>
                    string.Equals(candidate?.id, target.EntityId, StringComparison.Ordinal));
                if (entity == null)
                {
                    continue;
                }

                foreach (InteractionPointData point in entity.interactionPoints)
                {
                    if (point == null)
                    {
                        continue;
                    }

                    CreateHandle(view.transform, entity.id, point);
                }
            }
        }

        public void Dispose() => Clear();

        private void CreateHandle(Transform parent, string entityId, InteractionPointData point)
        {
            GameObject handle = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            handle.transform.SetParent(parent, false);
            Collider collider = handle.GetComponent<Collider>();
            collider.isTrigger = true;
            var marker = handle.AddComponent<InteractionPointHandle>();
            marker.Initialize(entityId, point);
            Renderer renderer = handle.GetComponent<Renderer>();
            var materialProperties = new MaterialPropertyBlock();
            Color color = new Color(1f, 0.35f, 0.9f);
            materialProperties.SetColor("_Color", color);
            materialProperties.SetColor("_BaseColor", color);
            renderer.SetPropertyBlock(materialProperties);
            handles.Add(handle);
        }

        private void Clear()
        {
            foreach (GameObject handle in handles)
            {
                if (handle == null)
                {
                    continue;
                }

                if (UnityEngine.Application.isPlaying)
                {
                    Object.Destroy(handle);
                }
                else
                {
                    Object.DestroyImmediate(handle);
                }
            }

            handles.Clear();
            renderedEntityIds.Clear();
        }
    }
}
