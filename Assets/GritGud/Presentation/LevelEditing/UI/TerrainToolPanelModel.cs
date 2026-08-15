using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GritGud.Application.Levels;
using GritGud.Domain.Levels;
using GritGud.Presentation.LevelEditing.Tools;
using UnityEngine;

namespace GritGud.Presentation.LevelEditing.UI
{
    public readonly struct TerrainMaterialPaintOption
    {
        public TerrainMaterialPaintOption(TerrainMaterialKind material, string label)
        {
            Material = material;
            Label = label ?? throw new ArgumentNullException(nameof(label));
        }

        public TerrainMaterialKind Material { get; }

        public string Label { get; }
    }

    public sealed class TerrainToolPanelModel
    {
        private readonly LevelEditorToolManager toolManager;
        private readonly TerrainHeightLevelEditorTool tool;
        private readonly TerrainAuthoringCoordinator authoring;
        private readonly Action frameTerrain;
        private string[] surfaceIds = Array.Empty<string>();
        private string selectedSurfaceId = string.Empty;
        private string synchronizedSurfaceState = string.Empty;

        public TerrainToolPanelModel(
            LevelEditorToolManager toolManager,
            TerrainHeightLevelEditorTool tool,
            TerrainAuthoringCoordinator authoring,
            Action frameTerrain)
        {
            this.toolManager = toolManager ?? throw new ArgumentNullException(nameof(toolManager));
            this.tool = tool ?? throw new ArgumentNullException(nameof(tool));
            this.authoring = authoring ?? throw new ArgumentNullException(nameof(authoring));
            this.frameTerrain = frameTerrain ?? throw new ArgumentNullException(nameof(frameTerrain));
        }

        public IReadOnlyList<string> SurfaceIds => surfaceIds;

        public bool HasTerrain => surfaceIds.Length > 0;

        public string SelectedSurfaceId => selectedSurfaceId;

        public int SelectedSurfaceIndex
        {
            get
            {
                int index = Array.IndexOf(surfaceIds, selectedSurfaceId);
                return index < 0 ? 0 : index;
            }
            set
            {
                if (surfaceIds.Length == 0)
                    return;
                int index = Mathf.Clamp(value, 0, surfaceIds.Length - 1);
                string next = surfaceIds[index];
                if (string.Equals(next, selectedSurfaceId, StringComparison.Ordinal))
                    return;
                selectedSurfaceId = next;
                synchronizedSurfaceState = string.Empty;
            }
        }

        public string WidthText { get; set; } = "50";

        public string DepthText { get; set; } = "50";

        public string SampleSpacingText { get; set; } = "2";

        public IReadOnlyList<string> AppearancePresetIds =>
            TerrainAuthoringCoordinator.AppearancePresetIds;

        public string AppearancePresetId { get; set; } = "slate";

        public string PendingAppearancePresetId { get; set; } = "slate";

        public LevelColorAuthoringText BaseColor { get; } = new LevelColorAuthoringText();

        public LevelColorAuthoringText SteepColor { get; } = new LevelColorAuthoringText();

        public string SlopeBlendStartText { get; set; } = "32";

        public string SlopeBlendEndText { get; set; } = "58";

        public string SmoothnessText { get; set; } = "0.1";

        public string SpecularStrengthText { get; set; } = "0.03";

        public bool IsRaiseActive => IsModeActive(TerrainBrushMode.Raise);

        public bool IsLowerActive => IsModeActive(TerrainBrushMode.Lower);

        public bool IsSmoothActive => IsModeActive(TerrainBrushMode.Smooth);

        public bool IsFlattenActive => IsModeActive(TerrainBrushMode.Flatten);

        public bool IsPaintActive => IsModeActive(TerrainBrushMode.Paint);

        public int PaintMaterialIndex
        {
            get
            {
                for (int index = 0; index < PaintMaterials.Count; index++)
                    if (PaintMaterials[index].Material == tool.PaintMaterial)
                        return index;
                return 0;
            }
        }

        public static IReadOnlyList<TerrainMaterialPaintOption> PaintMaterials { get; } = new[]
        {
            new TerrainMaterialPaintOption(TerrainMaterialKind.Surface, "SURFACE"),
            new TerrainMaterialPaintOption(TerrainMaterialKind.Slate, "SLATE"),
            new TerrainMaterialPaintOption(TerrainMaterialKind.Grass, "GRASS"),
            new TerrainMaterialPaintOption(TerrainMaterialKind.Sand, "SAND"),
            new TerrainMaterialPaintOption(TerrainMaterialKind.Snow, "SNOW"),
            new TerrainMaterialPaintOption(TerrainMaterialKind.Concrete, "CONCRETE"),
        };

        public int RadiusInSamples
        {
            get => tool.RadiusInSamples;
            set => tool.RadiusInSamples = Mathf.Clamp(value, 1, 16);
        }

        public int QuantizedStrength
        {
            get => tool.QuantizedStrength;
            set => tool.QuantizedStrength = Mathf.Clamp(value, 1, 20);
        }

        public void ActivateRaise()
        {
            ActivateMode(TerrainBrushMode.Raise);
        }

        public void Activate()
        {
            toolManager.Activate(TerrainHeightLevelEditorTool.ToolId);
        }

        public void ActivateLower()
        {
            ActivateMode(TerrainBrushMode.Lower);
        }

        public void ActivateSmooth()
        {
            ActivateMode(TerrainBrushMode.Smooth);
        }

        public void ActivateFlatten()
        {
            ActivateMode(TerrainBrushMode.Flatten);
        }

        public void ActivatePaint(int materialIndex)
        {
            int index = Mathf.Clamp(materialIndex, 0, PaintMaterials.Count - 1);
            tool.PaintMaterial = PaintMaterials[index].Material;
            ActivateMode(TerrainBrushMode.Paint);
        }

        private void ActivateMode(TerrainBrushMode mode)
        {
            tool.BrushMode = mode;
            toolManager.Activate(TerrainHeightLevelEditorTool.ToolId);
        }

        public void FrameTerrain()
        {
            frameTerrain();
        }

        public void Synchronize(LevelDocument document)
        {
            TerrainSurfaceData[] surfaces = document?.terrainSurfaces?
                .Where(surface => surface != null && !string.IsNullOrWhiteSpace(surface.id))
                .ToArray() ?? Array.Empty<TerrainSurfaceData>();
            surfaceIds = surfaces.Select(surface => surface.id).ToArray();
            TerrainSurfaceData selected = surfaces.FirstOrDefault(surface => string.Equals(
                surface.id,
                selectedSurfaceId,
                StringComparison.Ordinal));
            if (selected == null)
            {
                selected = surfaces.FirstOrDefault();
                selectedSurfaceId = selected?.id ?? string.Empty;
            }

            if (selected == null)
            {
                synchronizedSurfaceState = string.Empty;
                return;
            }

            string state = string.Join(
                "|",
                selected.id,
                selected.sampleCountX.ToString(CultureInfo.InvariantCulture),
                selected.sampleCountZ.ToString(CultureInfo.InvariantCulture),
                selected.sampleSpacing.ToString("R", CultureInfo.InvariantCulture),
                selected.origin.x.ToString("R", CultureInfo.InvariantCulture),
                selected.origin.z.ToString("R", CultureInfo.InvariantCulture),
                selected.appearance?.presetId,
                ColorState(selected.appearance?.baseColor ?? default),
                ColorState(selected.appearance?.steepColor ?? default),
                selected.appearance?.slopeBlendStartDegrees.ToString(
                    "R",
                    CultureInfo.InvariantCulture),
                selected.appearance?.slopeBlendEndDegrees.ToString(
                    "R",
                    CultureInfo.InvariantCulture),
                selected.appearance?.smoothness.ToString("R", CultureInfo.InvariantCulture),
                selected.appearance?.specularStrength.ToString(
                    "R",
                    CultureInfo.InvariantCulture));
            if (string.Equals(state, synchronizedSurfaceState, StringComparison.Ordinal))
                return;

            synchronizedSurfaceState = state;
            WidthText = TerrainSurfaceAuthoring.Width(selected).ToString(
                "0.###",
                CultureInfo.InvariantCulture);
            DepthText = TerrainSurfaceAuthoring.Depth(selected).ToString(
                "0.###",
                CultureInfo.InvariantCulture);
            SampleSpacingText = selected.sampleSpacing.ToString(
                "0.###",
                CultureInfo.InvariantCulture);
            TerrainAppearanceData appearance = selected.appearance ?? new TerrainAppearanceData();
            AppearancePresetId = appearance.presetId;
            PendingAppearancePresetId = AppearancePresetIds.Any(value => string.Equals(
                value,
                appearance.presetId,
                StringComparison.OrdinalIgnoreCase))
                ? appearance.presetId
                : string.Empty;
            SetColor(BaseColor, appearance.baseColor);
            SetColor(SteepColor, appearance.steepColor);
            SlopeBlendStartText = Format(appearance.slopeBlendStartDegrees);
            SlopeBlendEndText = Format(appearance.slopeBlendEndDegrees);
            SmoothnessText = Format(appearance.smoothness);
            SpecularStrengthText = Format(appearance.specularStrength);
        }

        public void CreateFlatTerrain()
        {
            authoring.CreateFlatTerrain();
        }

        public void ResizeTerrain()
        {
            authoring.ResizeTerrain(
                selectedSurfaceId,
                WidthText,
                DepthText,
                SampleSpacingText);
        }

        public void ApplyAppearancePreset()
        {
            authoring.ApplyAppearancePreset(selectedSurfaceId, PendingAppearancePresetId);
        }

        public void ApplyAppearance()
        {
            authoring.ApplyAppearance(new TerrainAppearanceAuthoringRequest
            {
                surfaceId = selectedSurfaceId,
                presetId = "custom",
                baseColor = CopyColor(BaseColor),
                steepColor = CopyColor(SteepColor),
                slopeBlendStartDegrees = SlopeBlendStartText,
                slopeBlendEndDegrees = SlopeBlendEndText,
                smoothness = SmoothnessText,
                specularStrength = SpecularStrengthText,
            });
        }

        private bool IsActive => ReferenceEquals(toolManager.ActiveTool, tool);

        private bool IsModeActive(TerrainBrushMode mode) =>
            IsActive && tool.BrushMode == mode;

        private static string Format(float value) => value.ToString(
            "0.###",
            CultureInfo.InvariantCulture);

        private static string ColorState(FloatColorData color) => string.Join(
            ",",
            color.r.ToString("R", CultureInfo.InvariantCulture),
            color.g.ToString("R", CultureInfo.InvariantCulture),
            color.b.ToString("R", CultureInfo.InvariantCulture),
            color.a.ToString("R", CultureInfo.InvariantCulture));

        private static void SetColor(LevelColorAuthoringText target, FloatColorData source)
        {
            target.r = Format(source.r);
            target.g = Format(source.g);
            target.b = Format(source.b);
        }

        private static LevelColorAuthoringText CopyColor(LevelColorAuthoringText source)
        {
            return new LevelColorAuthoringText
            {
                r = source.r,
                g = source.g,
                b = source.b,
            };
        }
    }
}
