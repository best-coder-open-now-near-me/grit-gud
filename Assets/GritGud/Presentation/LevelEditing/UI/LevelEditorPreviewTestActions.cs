using System;
using GritGud.Domain.Levels;

namespace GritGud.Presentation.LevelEditing.UI
{
    internal sealed class LevelEditorPreviewTestActions :
        ILevelEditorPreviewTestActions
    {
        private readonly LevelEditorPlayabilityCoordinator playability;
        private readonly Action returnToMenu;
        private readonly Action togglePreview;
        private readonly Action startTestPlay;

        public LevelEditorPreviewTestActions(
            LevelEditorPlayabilityCoordinator playability,
            Action returnToMenu,
            Action togglePreview,
            Action startTestPlay)
        {
            this.playability = playability ?? throw new ArgumentNullException(
                nameof(playability));
            this.returnToMenu = returnToMenu ?? throw new ArgumentNullException(
                nameof(returnToMenu));
            this.togglePreview = togglePreview ?? throw new ArgumentNullException(
                nameof(togglePreview));
            this.startTestPlay = startTestPlay ?? throw new ArgumentNullException(
                nameof(startTestPlay));
        }

        public LevelPlayabilityReport PlayabilityReport => playability.Report;
        public bool PlayabilityReportIsStale => playability.IsStale;
        public bool SlopeOverlayEnabled => playability.SlopeOverlayEnabled;

        public void ReturnToMenu() => returnToMenu();
        public void TogglePreview() => togglePreview();
        public void StartTestPlay() => startTestPlay();
        public void RunPlayabilityDiagnostics() => playability.Run();
        public void SetSlopeOverlayEnabled(bool enabled) =>
            playability.SetSlopeOverlay(enabled);
    }
}
