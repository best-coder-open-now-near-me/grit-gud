using System;

namespace GritGud.Presentation.LevelEditing.UI
{
    public readonly struct LevelEditorShellLayout
    {
        private LevelEditorShellLayout(
            float screenWidth,
            float leftPanelWidth,
            float inspectorWidth,
            bool compact)
        {
            ScreenWidth = screenWidth;
            LeftPanelWidth = leftPanelWidth;
            InspectorWidth = inspectorWidth;
            ViewportWidth = Math.Max(0f, screenWidth - leftPanelWidth - inspectorWidth);
            IsCompact = compact;
        }

        public float ScreenWidth { get; }

        public float LeftPanelWidth { get; }

        public float InspectorWidth { get; }

        public float ViewportWidth { get; }

        public bool IsCompact { get; }

        public static LevelEditorShellLayout Calculate(
            float screenWidth,
            bool previewMode,
            bool showLeftPanel,
            bool showInspector)
        {
            if (screenWidth < 0f)
                throw new ArgumentOutOfRangeException(nameof(screenWidth));

            bool compact = screenWidth < LevelEditorGuiMetrics.CompactLayoutWidth;
            float left = !previewMode && showLeftPanel
                ? LevelEditorGuiMetrics.LeftPanelWidth
                : 0f;
            float right = !previewMode && showInspector
                ? LevelEditorGuiMetrics.InspectorWidth
                : 0f;
            return new LevelEditorShellLayout(screenWidth, left, right, compact);
        }
    }
}
