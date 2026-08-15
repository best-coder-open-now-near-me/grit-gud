using System;
using GritGud.Presentation.LevelEditing.Core;
using UnityEngine;

namespace GritGud.Presentation.LevelEditing.Tools
{
    public enum LevelSpatialPlacementKind
    {
        PracticalLight,
        Decal,
        AmbientVfx,
        AudioZone,
    }

    /// <summary>
    /// Gives non-entity spatial records the same point-and-click placement
    /// interaction as ordinary level props.
    /// </summary>
    public sealed class SpatialRecordPlacementTool : ILevelEditorTool
    {
        public const string ToolId = "spatial-record-place";

        private readonly Plane fallbackPlane = new Plane(Vector3.up, Vector3.zero);
        private readonly Action<LevelSpatialPlacementKind, Vector3> commit;
        private LevelEditorToolContext context;
        private Vector3 pointerWorld;

        public SpatialRecordPlacementTool(
            Action<LevelSpatialPlacementKind, Vector3> commit)
        {
            this.commit = commit ?? throw new ArgumentNullException(nameof(commit));
        }

        public string Id => ToolId;

        public string DisplayName => "Place Dressing";

        public LevelSpatialPlacementKind Kind { get; private set; }

        public bool IsQueued { get; private set; }

        public bool HasPreview { get; private set; }

        public Vector3 PreviewPosition => pointerWorld;

        public void Queue(LevelSpatialPlacementKind kind)
        {
            Kind = kind;
            IsQueued = true;
            HasPreview = false;
        }

        public void Activate(LevelEditorToolContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            context.Selection.Clear();
            context.SetStatus($"Placing {DisplayKind(Kind)}. Click the map to place; Esc cancels.");
        }

        public void Deactivate()
        {
            context = null;
            HasPreview = false;
        }

        public void Tick(LevelEditorInputState input)
        {
            if (context == null || !IsQueued)
            {
                HasPreview = false;
                return;
            }

            HasPreview = context.SceneQuery.TryGetPlacementPoint(
                input.PointerPosition,
                fallbackPlane,
                out pointerWorld);
            if (!input.PrimaryPressed || input.PointerBlocked || !HasPreview)
                return;

            commit(Kind, pointerWorld);
            context.SetStatus(
                $"Placed {DisplayKind(Kind)}. Click again to continue; Esc cancels.");
        }

        public bool Cancel()
        {
            IsQueued = false;
            HasPreview = false;
            return false;
        }

        public Bounds GetPreviewBounds()
        {
            Vector3 size = Kind switch
            {
                LevelSpatialPlacementKind.AudioZone => new Vector3(4f, 2f, 4f),
                LevelSpatialPlacementKind.PracticalLight => new Vector3(0.5f, 3f, 0.5f),
                LevelSpatialPlacementKind.Decal => new Vector3(2f, 0.04f, 2f),
                _ => Vector3.one,
            };
            return new Bounds(pointerWorld + Vector3.up * size.y * 0.5f, size);
        }

        private static string DisplayKind(LevelSpatialPlacementKind kind) => kind switch
        {
            LevelSpatialPlacementKind.PracticalLight => "practical light",
            LevelSpatialPlacementKind.AmbientVfx => "ambient VFX",
            LevelSpatialPlacementKind.AudioZone => "audio zone",
            _ => "decal",
        };
    }
}
