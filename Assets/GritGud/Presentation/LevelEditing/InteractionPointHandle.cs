using System;
using System.Collections.Generic;
using System.Linq;
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
        private readonly Dictionary<string, GameObject> handles =
            new Dictionary<string, GameObject>(StringComparer.Ordinal);

        public void Refresh(LevelDocument document, LevelSelectionModel selection, LevelWorldProjector projector)
        {
            if (document == null || selection == null || projector == null)
            {
                Clear();
                return;
            }

            var retainedKeys = new HashSet<string>(StringComparer.Ordinal);
            var renderedEntityIds = new HashSet<string>(StringComparer.Ordinal);
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

                    string key = BuildKey(entity.id, point.id);
                    retainedKeys.Add(key);
                    if (!handles.TryGetValue(key, out GameObject handle) || handle == null)
                    {
                        handle = CreateHandle(view.transform, entity.id, point);
                        handles[key] = handle;
                    }
                    else
                    {
                        handle.transform.SetParent(view.transform, false);
                        handle.GetComponent<InteractionPointHandle>().Initialize(entity.id, point);
                    }
                }
            }

            foreach (string key in handles.Keys.Where(key => !retainedKeys.Contains(key)).ToArray())
                DestroyHandle(key);
        }

        public void Dispose() => Clear();

        private GameObject CreateHandle(
            Transform parent,
            string entityId,
            InteractionPointData point)
        {
            GameObject handle = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            handle.transform.SetParent(parent, false);
            Collider collider = handle.GetComponent<Collider>();
            collider.isTrigger = true;
            var marker = handle.AddComponent<InteractionPointHandle>();
            marker.Initialize(entityId, point);
            Renderer renderer = handle.GetComponent<Renderer>();
            var materialProperties = new MaterialPropertyBlock();
            Color color = LevelEditorTheme.InteractionPoint;
            materialProperties.SetColor("_Color", color);
            materialProperties.SetColor("_BaseColor", color);
            renderer.SetPropertyBlock(materialProperties);
            return handle;
        }

        private void Clear()
        {
            foreach (GameObject handle in handles.Values)
                DestroyObject(handle);

            handles.Clear();
        }

        private void DestroyHandle(string key)
        {
            if (!handles.TryGetValue(key, out GameObject handle))
                return;
            handles.Remove(key);
            DestroyObject(handle);
        }

        private static void DestroyObject(GameObject handle)
        {
            if (handle == null)
                return;
            if (UnityEngine.Application.isPlaying)
                Object.Destroy(handle);
            else
                Object.DestroyImmediate(handle);
        }

        private static string BuildKey(string entityId, string pointId)
        {
            return entityId + ":" + pointId;
        }
    }
}
