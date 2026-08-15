using GritGud.Domain.Levels;
using UnityEngine;

namespace GritGud.Presentation.Levels.Runtime
{
    public sealed class LevelEntityView : MonoBehaviour
    {
        private LevelArchetypeDefinition archetype;

        public string EntityId { get; private set; }

        public string ArchetypeId { get; private set; }

        public LevelArchetypeDefinition Archetype => archetype;

        public void Initialize(LevelEntity entity, LevelArchetypeDefinition definition)
        {
            EntityId = entity.id;
            ArchetypeId = entity.archetypeId;
            archetype = definition;
            name = $"{definition.DisplayName} [{entity.id}]";
            Apply(entity);
        }

        public void Apply(LevelEntity entity)
        {
            if (entity == null)
            {
                throw new System.ArgumentNullException(nameof(entity));
            }

            EntityId = entity.id;
            ArchetypeId = entity.archetypeId;
            ApplyTransform(entity.transform);
        }

        public void ApplyTransform(LevelTransformData value)
        {
            transform.localPosition = new Vector3(
                value.position.x,
                value.position.y,
                value.position.z);
            transform.localRotation = Quaternion.Euler(0f, value.yawDegrees, 0f);
        }

        public LevelTransformData ReadTransform()
        {
            Vector3 position = transform.localPosition;
            return new LevelTransformData(
                new Float3Data(position.x, position.y, position.z),
                NormalizeYaw(transform.localEulerAngles.y));
        }

        public Bounds GetWorldBounds()
        {
            Bounds localBounds = CalculateVisualLocalBounds(
                archetype.Presentation.Prefab,
                archetype.Presentation.LocalBounds);
            return TransformBounds(localBounds, transform);
        }

        public static Bounds CalculateVisualLocalBounds(GameObject prefab, Bounds fallback)
        {
            if (prefab == null)
                return fallback;

            Transform root = prefab.transform;
            Matrix4x4 worldToRoot = root.worldToLocalMatrix;
            Bounds? combined = null;
            foreach (MeshFilter filter in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null)
                    continue;

                Bounds candidate = TransformBounds(
                    filter.sharedMesh.bounds,
                    worldToRoot * filter.transform.localToWorldMatrix);
                combined = Encapsulate(combined, candidate);
            }

            foreach (SkinnedMeshRenderer renderer in
                prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                Bounds candidate = TransformBounds(
                    renderer.localBounds,
                    worldToRoot * renderer.transform.localToWorldMatrix);
                combined = Encapsulate(combined, candidate);
            }

            return combined ?? fallback;
        }

        public static Bounds TransformBounds(Bounds localBounds, Transform source)
        {
            return TransformBounds(localBounds, source.localToWorldMatrix);
        }

        private static Bounds TransformBounds(Bounds localBounds, Matrix4x4 matrix)
        {
            Vector3 extents = localBounds.extents;
            Vector3 axisX = matrix.MultiplyVector(new Vector3(extents.x, 0f, 0f));
            Vector3 axisY = matrix.MultiplyVector(new Vector3(0f, extents.y, 0f));
            Vector3 axisZ = matrix.MultiplyVector(new Vector3(0f, 0f, extents.z));
            var worldExtents = new Vector3(
                Mathf.Abs(axisX.x) + Mathf.Abs(axisY.x) + Mathf.Abs(axisZ.x),
                Mathf.Abs(axisX.y) + Mathf.Abs(axisY.y) + Mathf.Abs(axisZ.y),
                Mathf.Abs(axisX.z) + Mathf.Abs(axisY.z) + Mathf.Abs(axisZ.z));
            return new Bounds(matrix.MultiplyPoint3x4(localBounds.center), worldExtents * 2f);
        }

        private static Bounds Encapsulate(Bounds? current, Bounds addition)
        {
            if (!current.HasValue)
                return addition;

            Bounds combined = current.Value;
            combined.Encapsulate(addition);
            return combined;
        }

        private static float NormalizeYaw(float yaw)
        {
            return Mathf.Repeat(yaw + 180f, 360f) - 180f;
        }
    }
}
