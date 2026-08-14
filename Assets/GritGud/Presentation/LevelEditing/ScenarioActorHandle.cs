using System;
using System.Collections.Generic;
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
        private readonly List<GameObject> handles = new List<GameObject>();
        private readonly Transform parent;
        private bool visible = true;

        public ScenarioActorHandleProjector(Transform parent)
        {
            this.parent = parent != null
                ? parent
                : throw new ArgumentNullException(nameof(parent));
        }

        public void Refresh(LevelDocument document)
        {
            Clear();
            if (document?.scenario == null)
                return;

            foreach (LevelScenarioActorData actor in document.scenario.actors)
            {
                if (actor != null)
                    CreateHandle(actor);
            }
        }

        public void SetVisible(bool value)
        {
            visible = value;
            foreach (GameObject handle in handles)
            {
                if (handle != null)
                    handle.SetActive(visible);
            }
        }

        public void Dispose() => Clear();

        private void CreateHandle(LevelScenarioActorData actor)
        {
            GameObject handle = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            handle.transform.SetParent(parent, false);
            Collider collider = handle.GetComponent<Collider>();
            collider.isTrigger = true;
            var marker = handle.AddComponent<ScenarioActorHandle>();
            marker.Initialize(actor);
            Renderer renderer = handle.GetComponent<Renderer>();
            var materialProperties = new MaterialPropertyBlock();
            Color color = actor.playerControlled
                ? new Color(0.15f, 0.65f, 1f)
                : actor.primaryTarget
                    ? new Color(1f, 0.22f, 0.15f)
                    : new Color(1f, 0.62f, 0.15f);
            materialProperties.SetColor("_Color", color);
            materialProperties.SetColor("_BaseColor", color);
            renderer.SetPropertyBlock(materialProperties);

            GameObject facing = GameObject.CreatePrimitive(PrimitiveType.Cube);
            facing.name = "Facing";
            facing.transform.SetParent(handle.transform, false);
            facing.transform.localPosition = new Vector3(0f, 0.45f, 0.72f);
            facing.transform.localScale = new Vector3(0.18f, 0.18f, 0.75f);
            facing.GetComponent<Collider>().enabled = false;
            facing.GetComponent<Renderer>().SetPropertyBlock(materialProperties);

            handle.SetActive(visible);
            handles.Add(handle);
        }

        private void Clear()
        {
            foreach (GameObject handle in handles)
            {
                if (handle == null)
                    continue;
                handle.SetActive(false);
                if (UnityEngine.Application.isPlaying)
                    Object.Destroy(handle);
                else
                    Object.DestroyImmediate(handle);
            }

            handles.Clear();
        }
    }
}
