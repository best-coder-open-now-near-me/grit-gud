using System;
using System.Globalization;
using System.Linq;
using GritGud.Application.Levels;
using GritGud.Domain.Levels;

namespace GritGud.Presentation.LevelEditing
{
    public sealed class TerrainAuthoringCoordinator
    {
        private readonly LevelEditorWorkspace workspace;

        public TerrainAuthoringCoordinator(LevelEditorWorkspace workspace)
        {
            this.workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        }

        public event Action<string> StatusChanged;

        public void CreateFlatTerrain()
        {
            LevelDocument document = workspace.CreateSnapshot();
            if (document.terrainSurfaces.Any(surface => surface != null))
            {
                Report("This level already contains a terrain surface.");
                return;
            }

            TerrainSurfaceData surface = TerrainSurfaceAuthoring.CreateFlat(
                "ground",
                document.bounds,
                TerrainSurfaceAuthoring.DefaultSampleSpacing);
            workspace.Execute(new AddTerrainSurfaceCommand(surface));
            Report("Added flat terrain covering the authored level bounds.");
        }

        public void ResizeTerrain(
            string surfaceId,
            string widthText,
            string depthText,
            string sampleSpacingText)
        {
            if (!TryParse(widthText, out float width)
                || !TryParse(depthText, out float depth)
                || !TryParse(sampleSpacingText, out float sampleSpacing))
            {
                Report("Terrain dimensions and grid spacing must be finite numbers.");
                return;
            }

            TerrainSurfaceData before = workspace.FindTerrainSurfaceSnapshot(surfaceId);
            if (before == null)
            {
                Report("Choose an existing terrain surface to resize.");
                return;
            }

            try
            {
                TerrainSurfaceData after = TerrainSurfaceAuthoring.Resize(
                    before,
                    width,
                    depth,
                    sampleSpacing);
                workspace.Execute(new SetTerrainSurfaceCommand(surfaceId, before, after));
                Report(
                    $"Resized terrain '{surfaceId}' to "
                    + $"{TerrainSurfaceAuthoring.Width(after):0.###} × "
                    + $"{TerrainSurfaceAuthoring.Depth(after):0.###} meters.");
            }
            catch (ArgumentException exception)
            {
                Report(exception.Message);
            }
            catch (InvalidOperationException exception)
            {
                Report(exception.Message);
            }
        }

        private void Report(string message)
        {
            StatusChanged?.Invoke(message ?? string.Empty);
        }

        private static bool TryParse(string text, out float value)
        {
            bool parsed = float.TryParse(
                text,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out value);
            return parsed && !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
