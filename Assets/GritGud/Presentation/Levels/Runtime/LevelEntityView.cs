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
            return TransformBounds(archetype.Presentation.LocalBounds, transform);
        }

        public static Bounds TransformBounds(Bounds localBounds, Transform source)
        {
            Vector3 extents = localBounds.extents;
            Vector3 axisX = source.TransformVector(extents.x, 0f, 0f);
            Vector3 axisY = source.TransformVector(0f, extents.y, 0f);
            Vector3 axisZ = source.TransformVector(0f, 0f, extents.z);
            var worldExtents = new Vector3(
                Mathf.Abs(axisX.x) + Mathf.Abs(axisY.x) + Mathf.Abs(axisZ.x),
                Mathf.Abs(axisX.y) + Mathf.Abs(axisY.y) + Mathf.Abs(axisZ.y),
                Mathf.Abs(axisX.z) + Mathf.Abs(axisY.z) + Mathf.Abs(axisZ.z));
            return new Bounds(source.TransformPoint(localBounds.center), worldExtents * 2f);
        }

        private static float NormalizeYaw(float yaw)
        {
            return Mathf.Repeat(yaw + 180f, 360f) - 180f;
        }
    }
}
