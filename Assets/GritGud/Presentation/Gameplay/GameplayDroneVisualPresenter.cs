using System.Collections.Generic;
using GritGud.Domain.Gameplay;
using GritGud.Presentation.Levels.Runtime;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    [DisallowMultipleComponent]
    internal sealed class GameplayDroneVisualPresenter : MonoBehaviour
    {
        private sealed class TransientVisual
        {
            public TransientVisual(
                GameObject root,
                Material ownedMaterial,
                float remainingSeconds)
            {
                Root = root;
                OwnedMaterial = ownedMaterial;
                RemainingSeconds = remainingSeconds;
            }

            public GameObject Root { get; }
            public Material OwnedMaterial { get; }
            public float RemainingSeconds { get; set; }
        }

        private const float ReplayDischargeSeconds = 0.12f;
        private readonly List<TransientVisual> replayTransients =
            new List<TransientVisual>();
        private Transform[] rotors;
        private Material material;

        internal int ReplayTransientVisualCount => replayTransients.Count;

        internal void Build()
        {
            if (rotors != null) return;
            material = new Material(Shader.Find("Standard"));
            material.color = new Color(0.12f, 0.18f, 0.2f, 1f);
            CreatePart("Body", PrimitiveType.Sphere, Vector3.zero,
                new Vector3(0.8f, 0.32f, 0.65f));
            CreatePart("Sensor", PrimitiveType.Sphere,
                new Vector3(0f, -0.12f, 0.36f),
                new Vector3(0.24f, 0.2f, 0.2f),
                new Color(0.08f, 0.65f, 0.85f, 1f));
            CreatePart("ArmX", PrimitiveType.Cube, Vector3.zero,
                new Vector3(1.6f, 0.08f, 0.12f));
            CreatePart("ArmZ", PrimitiveType.Cube, Vector3.zero,
                new Vector3(0.12f, 0.08f, 1.6f));
            rotors = new Transform[4];
            Vector3[] positions =
            {
                new Vector3(0.72f, 0.04f, 0.72f),
                new Vector3(-0.72f, 0.04f, 0.72f),
                new Vector3(0.72f, 0.04f, -0.72f),
                new Vector3(-0.72f, 0.04f, -0.72f),
            };
            for (int index = 0; index < rotors.Length; index++)
                rotors[index] = CreatePart(
                    "Rotor" + index,
                    PrimitiveType.Cylinder,
                    positions[index],
                    new Vector3(0.42f, 0.018f, 0.42f)).transform;
            SphereCollider collider = gameObject.GetComponent<SphereCollider>();
            // Unity reports destroyed native components through its overloaded
            // null check, which the CLR null-coalescing operator bypasses.
            if (collider == null)
                collider = gameObject.AddComponent<SphereCollider>();
            collider.radius = 0.8f;
            collider.center = Vector3.zero;
        }

        internal void SetOperational(bool operational)
        {
            if (material != null)
                material.color = operational
                    ? new Color(0.12f, 0.18f, 0.2f, 1f)
                    : new Color(0.16f, 0.08f, 0.06f, 1f);
        }

        internal void PresentReplayDischarge(
            string presentationId,
            GameplayPosition origin,
            GameplayPosition destination)
        {
            Vector3 from = ToVector3(origin);
            Vector3 to = ToVector3(destination);
            string effectName = string.IsNullOrWhiteSpace(presentationId)
                ? "Drone Weapon"
                : presentationId;

            var lightRoot = new GameObject(effectName + " Replay Muzzle Light");
            lightRoot.transform.position = from;
            Light light = lightRoot.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = GameplayVisualPalette.SignalOrangeGlow;
            light.intensity = 2.5f;
            light.range = 3.5f;
            light.shadows = LightShadows.None;
            replayTransients.Add(new TransientVisual(
                lightRoot,
                ownedMaterial: null,
                ReplayDischargeSeconds));

            var tracerRoot = new GameObject(effectName + " Replay Tracer");
            LineRenderer tracer = tracerRoot.AddComponent<LineRenderer>();
            tracer.useWorldSpace = true;
            tracer.positionCount = 2;
            tracer.SetPosition(0, from);
            tracer.SetPosition(1, to);
            tracer.startWidth = 0.045f;
            tracer.endWidth = 0.012f;
            tracer.numCapVertices = 3;
            Color color = new Color(0.08f, 0.75f, 1f, 1f);
            tracer.startColor = color;
            tracer.endColor = new Color(color.r, color.g, color.b, 0.35f);
            Material tracerMaterial = RuntimeMaterialFactory.CreateColor(
                color,
                effectName + " Replay Tracer Material");
            tracer.sharedMaterial = tracerMaterial;
            replayTransients.Add(new TransientVisual(
                tracerRoot,
                tracerMaterial,
                ReplayDischargeSeconds));
        }

        internal void ClearReplayTransients()
        {
            foreach (TransientVisual visual in replayTransients)
            {
                GameplayObjectLifecycle.Destroy(visual.Root);
                GameplayObjectLifecycle.Destroy(visual.OwnedMaterial);
            }
            replayTransients.Clear();
        }

        private void Update()
        {
            if (rotors != null)
            {
                float rotation = 720f * Time.unscaledDeltaTime;
                foreach (Transform rotor in rotors)
                    rotor.Rotate(0f, rotation, 0f, Space.Self);
            }
            TickReplayTransients(Time.unscaledDeltaTime);
        }

        private void TickReplayTransients(float deltaTime)
        {
            float elapsed = Mathf.Max(0f, deltaTime);
            for (int index = replayTransients.Count - 1; index >= 0; index--)
            {
                TransientVisual visual = replayTransients[index];
                visual.RemainingSeconds -= elapsed;
                if (visual.RemainingSeconds > 0f) continue;
                GameplayObjectLifecycle.Destroy(visual.Root);
                GameplayObjectLifecycle.Destroy(visual.OwnedMaterial);
                replayTransients.RemoveAt(index);
            }
        }

        private GameObject CreatePart(
            string partName,
            PrimitiveType primitive,
            Vector3 localPosition,
            Vector3 localScale,
            Color? overrideColor = null)
        {
            GameObject part = GameObject.CreatePrimitive(primitive);
            part.name = partName;
            part.transform.SetParent(transform, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
            Collider collider = part.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
                GameplayObjectLifecycle.Destroy(collider);
            }
            Renderer renderer = part.GetComponent<Renderer>();
            if (overrideColor.HasValue)
            {
                var partMaterial = new Material(material)
                {
                    color = overrideColor.Value,
                };
                renderer.sharedMaterial = partMaterial;
            }
            else
            {
                renderer.sharedMaterial = material;
            }
            return part;
        }

        private void OnDestroy()
        {
            ClearReplayTransients();
            if (material != null) GameplayObjectLifecycle.Destroy(material);
        }

        private static Vector3 ToVector3(GameplayPosition value) =>
            new Vector3(value.X, value.Y, value.Z);
    }
}
