using System;
using GritGud.Presentation.LevelEditing;
using GritGud.Presentation.Levels.Runtime;
using UnityEngine;

namespace GritGud.Presentation.LevelEditing.Core
{
    public sealed class LevelEditorSceneQuery
    {
        private readonly Camera camera;

        public LevelEditorSceneQuery(Camera camera)
        {
            this.camera = camera != null
                ? camera
                : throw new ArgumentNullException(nameof(camera));
        }

        public int LayerMask { get; set; } = Physics.DefaultRaycastLayers;

        public Ray CreateRay(Vector2 pointerPosition)
        {
            return camera.ScreenPointToRay(pointerPosition);
        }

        public bool TryPickEntity(
            Vector2 pointerPosition,
            out LevelEntityView view,
            out Ray ray)
        {
            ray = CreateRay(pointerPosition);
            if (Physics.Raycast(
                ray,
                out RaycastHit hit,
                500f,
                LayerMask,
                QueryTriggerInteraction.Ignore))
            {
                view = hit.collider.GetComponentInParent<LevelEntityView>();
                return view != null;
            }

            view = null;
            return false;
        }

        public bool TryPickInteractionPoint(
            Vector2 pointerPosition,
            out InteractionPointHandle handle,
            out Ray ray)
        {
            ray = CreateRay(pointerPosition);
            if (Physics.Raycast(
                ray,
                out RaycastHit hit,
                500f,
                LayerMask,
                QueryTriggerInteraction.Collide))
            {
                handle = hit.collider.GetComponent<InteractionPointHandle>();
                return handle != null;
            }

            handle = null;
            return false;
        }

        public bool TryGetPlacementPoint(
            Vector2 pointerPosition,
            Plane fallbackPlane,
            out Vector3 point)
        {
            Ray ray = CreateRay(pointerPosition);
            if (Physics.Raycast(
                ray,
                out RaycastHit hit,
                500f,
                LayerMask,
                QueryTriggerInteraction.Ignore))
            {
                LevelEntityView hitView = hit.collider.GetComponentInParent<LevelEntityView>();
                if (hitView == null
                    || (hitView.Archetype.Capabilities
                        & LevelArchetypeCapabilities.PlacementSurface) != 0)
                {
                    point = hit.point;
                    return true;
                }
            }

            if (fallbackPlane.Raycast(ray, out float distance))
            {
                point = ray.GetPoint(distance);
                return true;
            }

            point = default;
            return false;
        }

        public bool TryPickTerrain(
            Vector2 pointerPosition,
            out string surfaceId,
            out Vector3 point)
        {
            Ray ray = CreateRay(pointerPosition);
            if (Physics.Raycast(
                ray,
                out RaycastHit hit,
                500f,
                LayerMask,
                QueryTriggerInteraction.Ignore))
            {
                TerrainChunkTag tag = hit.collider.GetComponentInParent<TerrainChunkTag>();
                if (tag != null)
                {
                    surfaceId = tag.SurfaceId;
                    point = hit.point;
                    return true;
                }
            }

            surfaceId = null;
            point = default;
            return false;
        }

        public bool TryProjectToPlane(Ray ray, Plane plane, out Vector3 point)
        {
            if (plane.Raycast(ray, out float distance))
            {
                point = ray.GetPoint(distance);
                return true;
            }

            point = default;
            return false;
        }
    }
}
