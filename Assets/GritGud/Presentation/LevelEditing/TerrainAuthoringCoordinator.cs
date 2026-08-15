using System;
using System.Globalization;
using System.Linq;
using GritGud.Application.Levels;
using GritGud.Domain.Levels;

namespace GritGud.Presentation.LevelEditing
{
    public sealed class TerrainAppearanceAuthoringRequest
    {
        public string surfaceId = string.Empty;
        public string presetId = "custom";
        public LevelColorAuthoringText baseColor = new LevelColorAuthoringText();
        public LevelColorAuthoringText steepColor = new LevelColorAuthoringText();
        public string slopeBlendStartDegrees = "32";
        public string slopeBlendEndDegrees = "58";
        public string smoothness = "0.1";
        public string specularStrength = "0.03";
    }

    public sealed class TerrainAuthoringCoordinator
    {
        private readonly LevelEditorWorkspace workspace;

        public TerrainAuthoringCoordinator(LevelEditorWorkspace workspace)
        {
            this.workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        }

        public event Action<string> StatusChanged;

        public static string[] AppearancePresetIds => new[]
        {
            "slate",
            "grass",
            "sand",
            "snow",
            "concrete",
        };

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

        public void ApplyAppearancePreset(string surfaceId, string presetId)
        {
            TerrainSurfaceData surface = workspace.FindTerrainSurfaceSnapshot(surfaceId);
            if (surface == null)
            {
                Report("Choose an existing terrain surface.");
                return;
            }
            if (!TryCreatePreset(presetId, out TerrainAppearanceData appearance))
            {
                Report($"Terrain appearance preset '{presetId}' is not available.");
                return;
            }
            workspace.Execute(new SetTerrainAppearanceCommand(
                surfaceId,
                surface.appearance,
                appearance));
            Report($"Applied the {presetId} terrain appearance.");
        }

        public void ApplyAppearance(TerrainAppearanceAuthoringRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            TerrainSurfaceData surface = workspace.FindTerrainSurfaceSnapshot(request.surfaceId);
            if (surface == null)
            {
                Report("Choose an existing terrain surface.");
                return;
            }
            if (!TryUnitColor(request.baseColor, out FloatColorData baseColor)
                || !TryUnitColor(request.steepColor, out FloatColorData steepColor)
                || !TryParse(request.slopeBlendStartDegrees, out float slopeStart)
                || !TryParse(request.slopeBlendEndDegrees, out float slopeEnd)
                || !TryParse(request.smoothness, out float smoothness)
                || !TryParse(request.specularStrength, out float specular)
                || slopeStart < 0f
                || slopeEnd > 89f
                || slopeEnd <= slopeStart
                || smoothness < 0f
                || smoothness > 1f
                || specular < 0f
                || specular > 1f)
            {
                Report(
                    "Terrain colors and response must be 0-1; the slope blend must increase within 0-89 degrees.");
                return;
            }

            var after = new TerrainAppearanceData
            {
                presetId = string.IsNullOrWhiteSpace(request.presetId)
                    ? "custom"
                    : request.presetId.Trim(),
                baseColor = baseColor,
                steepColor = steepColor,
                slopeBlendStartDegrees = slopeStart,
                slopeBlendEndDegrees = slopeEnd,
                smoothness = smoothness,
                specularStrength = specular,
            };
            workspace.Execute(new SetTerrainAppearanceCommand(
                request.surfaceId,
                surface.appearance,
                after));
            Report("Updated terrain appearance.");
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

        private static bool TryUnitColor(
            LevelColorAuthoringText text,
            out FloatColorData color)
        {
            color = default;
            if (text == null
                || !TryParse(text.r, out float r)
                || !TryParse(text.g, out float g)
                || !TryParse(text.b, out float b)
                || r < 0f || r > 1f
                || g < 0f || g > 1f
                || b < 0f || b > 1f)
            {
                return false;
            }
            color = new FloatColorData(r, g, b);
            return true;
        }

        private static bool TryCreatePreset(
            string presetId,
            out TerrainAppearanceData appearance)
        {
            switch (presetId?.Trim().ToLowerInvariant())
            {
                case "slate":
                    appearance = new TerrainAppearanceData();
                    return true;
                case "grass":
                    appearance = Appearance(
                        "grass",
                        new FloatColorData(0.18f, 0.34f, 0.14f),
                        new FloatColorData(0.24f, 0.22f, 0.17f),
                        30f,
                        54f,
                        0.04f,
                        0.015f);
                    return true;
                case "sand":
                    appearance = Appearance(
                        "sand",
                        new FloatColorData(0.58f, 0.44f, 0.24f),
                        new FloatColorData(0.42f, 0.3f, 0.18f),
                        28f,
                        52f,
                        0.03f,
                        0.01f);
                    return true;
                case "snow":
                    appearance = Appearance(
                        "snow",
                        new FloatColorData(0.72f, 0.8f, 0.84f),
                        new FloatColorData(0.34f, 0.4f, 0.44f),
                        24f,
                        48f,
                        0.16f,
                        0.08f);
                    return true;
                case "concrete":
                    appearance = Appearance(
                        "concrete",
                        new FloatColorData(0.34f, 0.36f, 0.38f),
                        new FloatColorData(0.24f, 0.26f, 0.29f),
                        34f,
                        62f,
                        0.08f,
                        0.025f);
                    return true;
                default:
                    appearance = null;
                    return false;
            }
        }

        private static TerrainAppearanceData Appearance(
            string presetId,
            FloatColorData baseColor,
            FloatColorData steepColor,
            float slopeStart,
            float slopeEnd,
            float smoothness,
            float specular)
        {
            return new TerrainAppearanceData
            {
                presetId = presetId,
                baseColor = baseColor,
                steepColor = steepColor,
                slopeBlendStartDegrees = slopeStart,
                slopeBlendEndDegrees = slopeEnd,
                smoothness = smoothness,
                specularStrength = specular,
            };
        }
    }
}
