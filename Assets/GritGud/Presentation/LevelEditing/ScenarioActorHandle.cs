using System;
using System.Collections.Generic;
using System.Linq;
using GritGud.Domain.Levels;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GritGud.Presentation.LevelEditing
{
    public sealed class ScenarioActorHandle : MonoBehaviour
    {
        public string ActorId { get; private set; }

        public void Initialize(LevelScenarioActorData actor)
        {
            ActorId = actor.id;
            name = $"Scenario Actor [{actor.id}]";
            transform.SetPositionAndRotation(
                new Vector3(
                    actor.transform.position.x,
                    actor.transform.position.y,
                    actor.transform.position.z),
                Quaternion.Euler(0f, actor.transform.yawDegrees, 0f));
            transform.localScale = new Vector3(0.65f, 1f, 0.65f);
        }
    }

    public sealed class ScenarioActorHandleProjector : IDisposable
    {
        private readonly Dictionary<string, GameObject> handles =
            new Dictionary<string, GameObject>(StringComparer.Ordinal);
        private readonly Transform parent;
        private bool visible = true;

        public ScenarioActorHandleProjector(Transform parent)
        {
            this.parent = parent != null
                ? parent
                : throw new ArgumentNullException(nameof(parent));
        }

        public int HandleCount => handles.Count;

        public bool TryGetHandle(string actorId, out GameObject handle)
        {
            return handles.TryGetValue(actorId ?? string.Empty, out handle) && handle != null;
        }

        public void Refresh(LevelDocument document)
        {
            var retainedIds = new HashSet<string>(StringComparer.Ordinal);
            IEnumerable<LevelScenarioActorData> actors = document?.scenario?.actors
                ?? Enumerable.Empty<LevelScenarioActorData>();
            foreach (LevelScenarioActorData actor in actors)
            {
                if (actor == null || string.IsNullOrWhiteSpace(actor.id))
                    continue;
                retainedIds.Add(actor.id);
                if (!handles.TryGetValue(actor.id, out GameObject handle) || handle == null)
                {
                    handle = CreateHandle(actor);
                    handles[actor.id] = handle;
                }
                UpdateHandle(handle, actor);
            }

            foreach (string actorId in handles.Keys.Where(id => !retainedIds.Contains(id)).ToArray())
                DestroyHandle(actorId);
        }

        public void SetVisible(bool value)
        {
            visible = value;
            foreach (GameObject handle in handles.Values)
            {
                if (handle != null)
                    handle.SetActive(visible);
            }
        }

        public void Dispose() => Clear();

        private GameObject CreateHandle(LevelScenarioActorData actor)
        {
            GameObject handle = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            handle.transform.SetParent(parent, false);
            Collider collider = handle.GetComponent<Collider>();
            collider.isTrigger = true;
            var marker = handle.AddComponent<ScenarioActorHandle>();

            GameObject facing = GameObject.CreatePrimitive(PrimitiveType.Cube);
            facing.name = "Facing";
            facing.transform.SetParent(handle.transform, false);
            facing.transform.localPosition = new Vector3(0f, 0.45f, 0.72f);
            facing.transform.localScale = new Vector3(0.18f, 0.18f, 0.75f);
            facing.GetComponent<Collider>().enabled = false;
            handle.SetActive(visible);
            marker.Initialize(actor);
            return handle;
        }

        private static void UpdateHandle(GameObject handle, LevelScenarioActorData actor)
        {
            handle.GetComponent<ScenarioActorHandle>().Initialize(actor);
            Color color = actor.playerControlled
                ? LevelEditorTheme.PlayerActor
                : actor.primaryTarget
                    ? LevelEditorTheme.PrimaryTargetActor
                    : LevelEditorTheme.EnemyActor;
            var materialProperties = new MaterialPropertyBlock();
            materialProperties.SetColor("_Color", color);
            materialProperties.SetColor("_BaseColor", color);
            foreach (Renderer renderer in handle.GetComponentsInChildren<Renderer>())
                renderer.SetPropertyBlock(materialProperties);
        }

        private void DestroyHandle(string actorId)
        {
            if (!handles.TryGetValue(actorId, out GameObject handle))
                return;
            handles.Remove(actorId);
            DestroyObject(handle);
        }

        private void Clear()
        {
            foreach (GameObject handle in handles.Values)
                DestroyObject(handle);

            handles.Clear();
        }

        private static void DestroyObject(GameObject handle)
        {
            if (handle == null)
                return;
            handle.SetActive(false);
            if (UnityEngine.Application.isPlaying)
                Object.Destroy(handle);
            else
                Object.DestroyImmediate(handle);
        }
    }
}
