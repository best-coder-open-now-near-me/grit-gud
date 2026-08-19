using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    [DisallowMultipleComponent]
    internal sealed class GameplayDroneVisualPresenter : MonoBehaviour
    {
        private Transform[] rotors;
        private Material material;

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
            var collider = gameObject.GetComponent<SphereCollider>()
                ?? gameObject.AddComponent<SphereCollider>();
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

        private void Update()
        {
            if (rotors == null) return;
            float rotation = 720f * Time.deltaTime;
            foreach (Transform rotor in rotors)
                rotor.Rotate(0f, rotation, 0f, Space.Self);
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
            if (collider != null) GameplayObjectLifecycle.Destroy(collider);
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
            if (material != null) GameplayObjectLifecycle.Destroy(material);
        }
    }
}
