using GritGud.Application.Levels;
using GritGud.Domain.Levels;
using GritGud.Presentation.LevelEditing.Core;
using GritGud.Presentation.Levels.Runtime;
using UnityEngine;

namespace GritGud.Presentation.LevelEditing.Tools
{
    public sealed class PlacementLevelEditorTool : ILevelEditorTool
    {
        public const string ToolId = "place";

        private readonly Plane basePlacementPlane = new Plane(Vector3.up, Vector3.zero);
        private LevelEditorToolContext context;
        private LevelArchetypeDefinition archetype;
        private Vector3 pointerWorld;
        private float yaw;

        public string Id => ToolId;

        public string DisplayName => "Place";

        public LevelArchetypeDefinition Archetype => archetype;

        public bool HasPreview { get; private set; }

        public void SelectArchetype(LevelArchetypeDefinition definition)
        {
            archetype = definition;
            yaw = 0f;
            HasPreview = false;
        }

        public void Activate(LevelEditorToolContext context)
        {
            this.context = context;
            context.Selection.Clear();
        }

        public void Deactivate()
        {
            HasPreview = false;
            context = null;
        }

        public void Tick(LevelEditorInputState input)
        {
            if (context == null || archetype == null)
            {
                HasPreview = false;
                return;
            }

            if (input.RotatePressed)
            {
                yaw = NormalizeYaw(yaw + archetype.PlacementRules.AngleSnap);
            }

            HasPreview = context.SceneQuery.TryGetPlacementPoint(
                input.PointerPosition,
                basePlacementPlane,
                out Vector3 placementPoint);
            if (HasPreview)
            {
                pointerWorld = context.SnapSettings.SnapPosition(
                    placementPoint,
                    archetype.PlacementRules.PositionSnap);
            }

            if (!input.PrimaryPressed || input.PointerBlocked || !HasPreview)
            {
                return;
            }

            LevelEntity entity = archetype.CreateEntity(pointerWorld, yaw);
            entity.id = LevelDocumentFactory.NewStableId();
            context.Workspace.Execute(new AddEntityCommand(entity));
            context.Selection.SetSingle(entity.id);
            context.SetStatus($"Placed {archetype.DisplayName}.");
        }

        public bool Cancel()
        {
            SelectArchetype(null);
            context?.Selection.Clear();
            return false;
        }

        public bool TryGetPreviewBounds(out Bounds bounds)
        {
            if (!HasPreview || archetype == null)
            {
                bounds = default;
                return false;
            }

            Bounds local = archetype.Presentation.LocalBounds;
            Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
            bounds = new Bounds(
                pointerWorld + rotation * local.center,
                RotatedBoundsSize(local.size, rotation));
            return true;
        }

        private static float NormalizeYaw(float value)
        {
            return Mathf.Repeat(value + 180f, 360f) - 180f;
        }

        private static Vector3 RotatedBoundsSize(Vector3 size, Quaternion rotation)
        {
            Matrix4x4 matrix = Matrix4x4.Rotate(rotation);
            return new Vector3(
                Mathf.Abs(matrix.m00) * size.x + Mathf.Abs(matrix.m01) * size.y + Mathf.Abs(matrix.m02) * size.z,
                Mathf.Abs(matrix.m10) * size.x + Mathf.Abs(matrix.m11) * size.y + Mathf.Abs(matrix.m12) * size.z,
                Mathf.Abs(matrix.m20) * size.x + Mathf.Abs(matrix.m21) * size.y + Mathf.Abs(matrix.m22) * size.z);
        }
    }
}
