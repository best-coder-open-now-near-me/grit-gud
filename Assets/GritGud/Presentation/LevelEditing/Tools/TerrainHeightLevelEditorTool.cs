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
        private TerrainMaterialStrokeAccumulator materialStroke;

        public string Id => ToolId;

        public string DisplayName => "Terrain Height";

        public int RadiusInSamples { get; set; } = 2;

        public int QuantizedStrength { get; set; } = 1;

        public TerrainBrushMode BrushMode { get; set; } = TerrainBrushMode.Raise;

        public TerrainMaterialKind PaintMaterial { get; set; } = TerrainMaterialKind.Slate;

        public void Activate(LevelEditorToolContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            context.Selection.Clear();
            footprint = new TerrainBrushFootprint();
            context.SetStatus(
                "Drag on terrain for one undoable sculpt or material-paint stroke. Shift temporarily lowers Raise.");
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

            if ((stroke != null || materialStroke != null) && input.PrimaryReleased)
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

            TerrainBrushMode effectiveMode = materialStroke != null
                ? TerrainBrushMode.Paint
                : stroke?.Mode ?? ResolveBrushMode(input.FastCameraMovement);
            footprint.Show(surface, point, RadiusInSamples, effectiveMode);
            if (input.PrimaryPressed)
            {
                if (effectiveMode == TerrainBrushMode.Paint)
                    materialStroke = new TerrainMaterialStrokeAccumulator(surface);
                else
                    stroke = new TerrainStrokeAccumulator(surface, effectiveMode, point);
                PreviewPoint(point);
                return;
            }

            string activeSurfaceId = materialStroke?.SurfaceId ?? stroke?.SurfaceId;
            if ((stroke != null || materialStroke != null)
                && input.PrimaryHeld
                && string.Equals(activeSurfaceId, surfaceId, StringComparison.Ordinal))
            {
                PreviewPoint(point);
            }
        }

        public bool Cancel()
        {
            if (stroke != null || materialStroke != null)
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
            if (materialStroke != null)
            {
                SetTerrainMaterialsCommand materialPatch = materialStroke.ApplyPoint(
                    point,
                    RadiusInSamples,
                    (int)PaintMaterial);
                if (materialPatch != null)
                    context.TerrainProjector.PreviewPatch(
                        materialStroke.PreviewSurface,
                        materialPatch.StartX,
                        materialPatch.StartZ,
                        materialPatch.Width,
                        materialPatch.Depth);
                return;
            }

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
            if (materialStroke != null)
            {
                SetTerrainMaterialsCommand materialCommand = materialStroke.CreateCommand();
                materialStroke = null;
                if (materialCommand != null)
                {
                    context.Workspace.Execute(materialCommand);
                    context.SetStatus("Painted terrain material stroke.");
                }
                return;
            }

            TerrainBrushMode mode = stroke.Mode;
            SetTerrainHeightsCommand command = stroke.CreateCommand();
            stroke = null;
            if (command == null)
            {
                return;
            }

            context.Workspace.Execute(command);
            context.SetStatus(mode switch
            {
                TerrainBrushMode.Raise => "Raised terrain stroke.",
                TerrainBrushMode.Lower => "Lowered terrain stroke.",
                TerrainBrushMode.Smooth => "Smoothed terrain stroke.",
                TerrainBrushMode.Flatten => "Flattened terrain stroke.",
                _ => "Applied terrain stroke.",
            });
        }

        private TerrainBrushMode ResolveBrushMode(bool lowerModifier)
        {
            return lowerModifier && BrushMode == TerrainBrushMode.Raise
                ? TerrainBrushMode.Lower
                : BrushMode;
        }

        private void CancelStroke()
        {
            if (stroke == null && materialStroke == null)
            {
                return;
            }

            stroke = null;
            materialStroke = null;
            context?.TerrainProjector.Replace(context.Workspace.CreateSnapshot());
        }
    }
}
