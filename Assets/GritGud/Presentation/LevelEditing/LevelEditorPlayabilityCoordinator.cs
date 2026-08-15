using System;
using GritGud.Application.Levels;
using GritGud.Domain.Levels;
using GritGud.Presentation.Levels.Runtime;

namespace GritGud.Presentation.LevelEditing
{
    public sealed class LevelEditorPlayabilityCoordinator
    {
        private readonly LevelEditorWorkspace workspace;
        private readonly TerrainWorldProjector terrainProjector;
        private bool authoringProjectionVisible = true;

        public LevelEditorPlayabilityCoordinator(
            LevelEditorWorkspace workspace,
            TerrainWorldProjector terrainProjector)
        {
            this.workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
            this.terrainProjector = terrainProjector
                ?? throw new ArgumentNullException(nameof(terrainProjector));
        }

        public event Action<string> StatusChanged;
        public event Action Changed;

        public LevelPlayabilityReport Report { get; private set; }
        public bool IsStale { get; private set; } = true;
        public bool SlopeOverlayEnabled { get; private set; }
        public float MaximumWalkableSlopeDegrees { get; private set; } =
            LevelPlayabilityAnalyzer.DefaultMaximumWalkableSlopeDegrees;

        public void MarkStale()
        {
            if (IsStale)
                return;
            IsStale = true;
            Changed?.Invoke();
        }

        public void Run()
        {
            Report = LevelPlayabilityAnalyzer.Analyze(
                workspace.CreateSnapshot(),
                MaximumWalkableSlopeDegrees,
                LevelPlayabilityAnalyzer.DefaultMaximumStepHeight);
            IsStale = false;
            ApplyProjection();
            Changed?.Invoke();
            ReportStatus();
        }

        public void SetSlopeOverlay(bool enabled)
        {
            if (enabled && (Report == null || IsStale))
                Run();
            if (SlopeOverlayEnabled == enabled)
                return;
            SlopeOverlayEnabled = enabled;
            ApplyProjection();
            Changed?.Invoke();
            StatusChanged?.Invoke(enabled
                ? $"Showing terrain slopes above {MaximumWalkableSlopeDegrees:0.#} degrees in red."
                : "Slope heatmap hidden.");
        }

        public void SetAuthoringProjectionVisible(bool visible)
        {
            if (authoringProjectionVisible == visible)
                return;
            authoringProjectionVisible = visible;
            ApplyProjection();
        }

        private void ApplyProjection()
        {
            terrainProjector.SetSlopeDiagnostics(
                authoringProjectionVisible && SlopeOverlayEnabled,
                MaximumWalkableSlopeDegrees);
        }

        private void ReportStatus()
        {
            int warningCount = Report?.WarningCount ?? 0;
            StatusChanged?.Invoke(warningCount == 0
                ? "Playability diagnostics found no warnings."
                : $"Playability diagnostics found {warningCount} warnings.");
        }
    }
}
