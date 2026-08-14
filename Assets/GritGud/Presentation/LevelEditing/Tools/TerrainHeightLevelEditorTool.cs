using System;
using GritGud.Application.Levels;
using GritGud.Domain.Levels;
using GritGud.Presentation.LevelEditing.Core;
using UnityEngine;

namespace GritGud.Presentation.LevelEditing.Tools
{
    public sealed class TerrainHeightLevelEditorTool : ILevelEditorTool
    {
        public const string ToolId = "terrain-height";

        private LevelEditorToolContext context;
        private TerrainBrushFootprint footprint;
        private TerrainStrokeAccumulator stroke;

        public string Id => ToolId;

        public string DisplayName => "Terrain Height";

        public int RadiusInSamples { get; set; } = 2;

        public int QuantizedStrength { get; set; } = 1;

        public bool LowerTerrain { get; set; }

        public void Activate(LevelEditorToolContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            context.Selection.Clear();
            footprint = new TerrainBrushFootprint();
            context.SetStatus("Drag on terrain to sculpt one undoable stroke. Shift lowers.");
        }

        public void Deactivate()
        {
            CancelStroke();
            context = null;
            footprint?.Dispose();
            footprint = null;
        }

        public void Tick(LevelEditorInputState input)
        {
            if (context == null)
            {
                return;
            }

            if (stroke != null && input.PrimaryReleased)
            {
                CommitStroke();
                return;
            }

            if (input.PointerBlocked
                || !context.SceneQuery.TryPickTerrain(
                    input.PointerPosition,
                    out string surfaceId,
                    out Vector3 point))
            {
                footprint.Hide();
                return;
            }

            TerrainSurfaceData surface = context.Workspace.FindTerrainSurfaceSnapshot(surfaceId);
            if (surface == null)
            {
                footprint.Hide();
                context.SetStatus("The selected terrain surface is no longer available.");
                return;
            }

            bool lower = LowerTerrain || input.FastCameraMovement;
            footprint.Show(surface, point, RadiusInSamples, lower);
            if (input.PrimaryPressed)
            {
                stroke = new TerrainStrokeAccumulator(surface, lower ? -1 : 1);
                PreviewPoint(point);
                return;
            }

            if (stroke != null
                && input.PrimaryHeld
                && string.Equals(stroke.SurfaceId, surfaceId, StringComparison.Ordinal))
            {
                PreviewPoint(point);
            }
        }

        public bool Cancel()
        {
            if (stroke != null)
            {
                CancelStroke();
                context.SetStatus("Cancelled terrain stroke.");
                return true;
            }

            if (footprint.IsVisible)
            {
                footprint.Hide();
                return true;
            }

            return false;
        }

        private void PreviewPoint(Vector3 point)
        {
            SetTerrainHeightsCommand patch = stroke.ApplyPoint(
                point,
                RadiusInSamples,
                QuantizedStrength);
            if (patch == null)
            {
                return;
            }

            context.TerrainProjector.PreviewPatch(
                stroke.PreviewSurface,
                patch.StartX,
                patch.StartZ,
                patch.Width,
                patch.Depth);
        }

        private void CommitStroke()
        {
            bool lower = stroke.Direction < 0;
            SetTerrainHeightsCommand command = stroke.CreateCommand();
            stroke = null;
            if (command == null)
            {
                return;
            }

            context.Workspace.Execute(command);
            context.SetStatus(lower ? "Lowered terrain stroke." : "Raised terrain stroke.");
        }

        private void CancelStroke()
        {
            if (stroke == null)
            {
                return;
            }

            stroke = null;
            context?.TerrainProjector.Replace(context.Workspace.CreateSnapshot());
        }
    }
}
