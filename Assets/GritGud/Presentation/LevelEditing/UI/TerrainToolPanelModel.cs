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

        public bool IsRaiseActive => IsActive && !tool.LowerTerrain;

        public bool IsLowerActive => IsActive && tool.LowerTerrain;

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
            tool.LowerTerrain = false;
            toolManager.Activate(TerrainHeightLevelEditorTool.ToolId);
        }

        public void ActivateLower()
        {
            tool.LowerTerrain = true;
            toolManager.Activate(TerrainHeightLevelEditorTool.ToolId);
        }

        public void FrameTerrain()
        {
            frameTerrain();
        }

        private bool IsActive => ReferenceEquals(toolManager.ActiveTool, tool);
    }
}
