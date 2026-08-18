using System;
using GritGud.Presentation.Levels.Runtime;

namespace GritGud.Presentation.LevelEditing.UI
{
    internal sealed class LevelEditorEnvironmentDressingActions :
        ILevelEditorEnvironmentDressingActions
    {
        private readonly EnvironmentAuthoringCoordinator environment;
        private readonly LevelDressingAuthoringCoordinator dressing;
        private readonly LevelDressingProjector projector;
        private readonly Func<bool> isPreviewMode;
        private readonly Action<string> setStatus;

        public LevelEditorEnvironmentDressingActions(
            EnvironmentAuthoringCoordinator environment,
            LevelDressingAuthoringCoordinator dressing,
            LevelDressingProjector projector,
            Func<bool> isPreviewMode,
            Action<string> setStatus)
        {
            this.environment = environment ?? throw new ArgumentNullException(
                nameof(environment));
            this.dressing = dressing ?? throw new ArgumentNullException(
                nameof(dressing));
            this.projector = projector ?? throw new ArgumentNullException(
                nameof(projector));
            this.isPreviewMode = isPreviewMode ?? throw new ArgumentNullException(
                nameof(isPreviewMode));
            this.setStatus = setStatus ?? throw new ArgumentNullException(
                nameof(setStatus));
        }

        public bool AudioZonePreviewEnabled { get; private set; }

        public void ApplyEnvironment(LevelEnvironmentAuthoringRequest request) =>
            environment.ApplyEnvironment(request);
        public void AddPracticalLight() => environment.AddPracticalLight();
        public void ApplyPracticalLight(
            LevelPracticalLightAuthoringRequest request) =>
            environment.ApplyPracticalLight(request);
        public void DeletePracticalLight(string lightId) =>
            environment.DeletePracticalLight(lightId);
        public void AddDecal() => dressing.AddDecal();
        public void ApplyDecal(LevelDecalAuthoringRequest request) =>
            dressing.ApplyDecal(request);
        public void DeleteDecal(string decalId) => dressing.DeleteDecal(decalId);
        public void AddAmbientVfx() => dressing.AddAmbientVfx();
        public void ApplyAmbientVfx(LevelAmbientVfxAuthoringRequest request) =>
            dressing.ApplyAmbientVfx(request);
        public void DeleteAmbientVfx(string effectId) =>
            dressing.DeleteAmbientVfx(effectId);
        public void AddAudioZone() => dressing.AddAudioZone();
        public void ApplyAudioZone(LevelAudioZoneAuthoringRequest request) =>
            dressing.ApplyAudioZone(request);
        public void DeleteAudioZone(string zoneId) => dressing.DeleteAudioZone(zoneId);

        public void SetAudioZonePreviewEnabled(bool enabled)
        {
            AudioZonePreviewEnabled = enabled;
            bool previewMode = isPreviewMode();
            projector.SetEditorPresentation(
                showZoneGizmos: !previewMode,
                playAudio: previewMode || enabled);
            setStatus(enabled
                ? "Ambient audio preview enabled."
                : "Ambient audio preview muted.");
        }
    }
}
