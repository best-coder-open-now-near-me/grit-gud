using System;
using GritGud.Presentation.LevelEditing.Tools;
using UnityEngine;

namespace GritGud.Presentation.LevelEditing.UI
{
    public sealed class TerrainToolPanelModel
    {
        private readonly LevelEditorToolManager toolManager;
        private readonly TerrainHeightLevelEditorTool tool;
        private readonly Action frameTerrain;

        public TerrainToolPanelModel(
            LevelEditorToolManager toolManager,
            TerrainHeightLevelEditorTool tool,
            Action frameTerrain)
        {
            this.toolManager = toolManager ?? throw new ArgumentNullException(nameof(toolManager));
            this.tool = tool ?? throw new ArgumentNullException(nameof(tool));
            this.frameTerrain = frameTerrain ?? throw new ArgumentNullException(nameof(frameTerrain));
        }

        public bool IsRaiseActive => IsModeActive(TerrainBrushMode.Raise);

        public bool IsLowerActive => IsModeActive(TerrainBrushMode.Lower);

        public bool IsSmoothActive => IsModeActive(TerrainBrushMode.Smooth);

        public bool IsFlattenActive => IsModeActive(TerrainBrushMode.Flatten);

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

        private void ActivateMode(TerrainBrushMode mode)
        {
            tool.BrushMode = mode;
            toolManager.Activate(TerrainHeightLevelEditorTool.ToolId);
        }

        public void FrameTerrain()
        {
            frameTerrain();
        }

        private bool IsActive => ReferenceEquals(toolManager.ActiveTool, tool);

        private bool IsModeActive(TerrainBrushMode mode) =>
            IsActive && tool.BrushMode == mode;
    }
}
