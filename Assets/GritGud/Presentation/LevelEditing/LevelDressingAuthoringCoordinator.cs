using System;
using System.Globalization;
using System.Linq;
using GritGud.Application.Levels;
using GritGud.Domain.Levels;
using GritGud.Presentation.LevelEditing.Core;
using UnityEngine;

namespace GritGud.Presentation.LevelEditing
{
    public enum LevelDressingTargetKind
    {
        Decal,
        AmbientVfx,
        AudioZone,
    }

    public sealed class LevelDecalAuthoringRequest
    {
        public string id = string.Empty;
        public string displayName = string.Empty;
        public string styleId = string.Empty;
        public LevelVectorAuthoringText position = new LevelVectorAuthoringText();
        public LevelVectorAuthoringText rotation = new LevelVectorAuthoringText();
        public LevelVectorAuthoringText size = new LevelVectorAuthoringText();
        public LevelColorAuthoringText color = new LevelColorAuthoringText();
        public string alpha = string.Empty;
    }

    public sealed class LevelAmbientVfxAuthoringRequest
    {
        public string id = string.Empty;
        public string displayName = string.Empty;
        public string effectId = string.Empty;
        public LevelVectorAuthoringText position = new LevelVectorAuthoringText();
        public LevelVectorAuthoringText rotation = new LevelVectorAuthoringText();
        public LevelVectorAuthoringText scale = new LevelVectorAuthoringText();
    }

    public sealed class LevelAudioZoneAuthoringRequest
    {
        public string id = string.Empty;
        public string displayName = string.Empty;
        public string soundId = string.Empty;
        public LevelVectorAuthoringText center = new LevelVectorAuthoringText();
        public LevelVectorAuthoringText size = new LevelVectorAuthoringText();
        public string volume = string.Empty;
        public string fadeDistance = string.Empty;
    }

    public sealed class LevelDressingAuthoringCoordinator
    {
        private static readonly LevelValidationService DressingValidator =
            new LevelValidationService(new ILevelValidationRule[]
            {
                new LevelDressingValidationRule(),
            });
        private readonly LevelEditorWorkspace workspace;
        private readonly Func<LevelEditorCameraState> captureCameraState;

        public LevelDressingAuthoringCoordinator(
            LevelEditorWorkspace workspace,
            Func<LevelEditorCameraState> captureCameraState)
        {
            this.workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
            this.captureCameraState = captureCameraState
                ?? throw new ArgumentNullException(nameof(captureCameraState));
        }

        public event Action<string> StatusChanged;
        public event Action<LevelDressingTargetKind, string> FocusRequested;

        public void AddDecal()
        {
            LevelEditorCameraState camera = captureCameraState();
            AddDecalAt(new Vector3(camera.target.x, camera.target.y, camera.target.z));
        }

        public void AddDecalAt(Vector3 position)
        {
            LevelDressingData before = workspace.CreateSnapshot().dressing;
            if (before.decals.Count >= LevelDressingData.MaximumDecalCount)
            {
                Report($"A level supports at most {LevelDressingData.MaximumDecalCount} decals.");
                return;
            }

            LevelDressingData after = before.DeepCopy();
            string id = "decal-" + LevelDocumentFactory.NewStableId();
            after.decals.Add(new LevelDecalData
            {
                id = id,
                displayName = $"Decal {after.decals.Count + 1}",
                position = new Float3Data(position.x, position.y + 0.02f, position.z),
            });
            Execute(before, after, "Add decal", "Added a decal at the camera focus.");
            FocusRequested?.Invoke(LevelDressingTargetKind.Decal, id);
        }

        public void ApplyDecal(LevelDecalAuthoringRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            if (!TryVector(request.position, out Float3Data position)
                || !TryVector(request.rotation, out Float3Data rotation)
                || !TryVector(request.size, out Float3Data size)
                || !TryColor(request.color, request.alpha, out FloatColorData color))
            {
                Report("Decal values must be finite numbers.");
                return;
            }

            string displayName = request.displayName?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(displayName)
                || !LevelDressingIds.IsDecalStyle(request.styleId))
            {
                Report("A decal needs a name and supported style.");
                return;
            }

            LevelDressingData before = workspace.CreateSnapshot().dressing;
            LevelDressingData after = before.DeepCopy();
            LevelDecalData replacement = after.decals.FirstOrDefault(value =>
                string.Equals(value?.id, request.id, StringComparison.Ordinal));
            if (replacement == null)
            {
                Report("Choose an existing decal.");
                return;
            }
            replacement.displayName = displayName;
            replacement.styleId = request.styleId;
            replacement.position = position;
            replacement.rotationEuler = rotation;
            replacement.size = size;
            replacement.color = color;
            Execute(before, after, "Edit decal", $"Updated '{displayName}'.");
        }

        public void DeleteDecal(string id) => Delete(
            LevelDressingTargetKind.Decal,
            id,
            "Delete decal",
            "Deleted the decal.");

        public void AddAmbientVfx()
        {
            LevelEditorCameraState camera = captureCameraState();
            AddAmbientVfxAt(new Vector3(camera.target.x, camera.target.y, camera.target.z));
        }

        public void AddAmbientVfxAt(Vector3 position)
        {
            LevelDressingData before = workspace.CreateSnapshot().dressing;
            if (before.ambientVfx.Count >= LevelDressingData.MaximumAmbientVfxCount)
            {
                Report($"A level supports at most {LevelDressingData.MaximumAmbientVfxCount} ambient effects.");
                return;
            }

            LevelDressingData after = before.DeepCopy();
            string id = "vfx-" + LevelDocumentFactory.NewStableId();
            after.ambientVfx.Add(new LevelAmbientVfxData
            {
                id = id,
                displayName = $"Ambient VFX {after.ambientVfx.Count + 1}",
                position = new Float3Data(position.x, position.y, position.z),
            });
            Execute(before, after, "Add ambient VFX", "Added ambient VFX at the camera focus.");
            FocusRequested?.Invoke(LevelDressingTargetKind.AmbientVfx, id);
        }

        public void ApplyAmbientVfx(LevelAmbientVfxAuthoringRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            if (!TryVector(request.position, out Float3Data position)
                || !TryVector(request.rotation, out Float3Data rotation)
                || !TryVector(request.scale, out Float3Data scale))
            {
                Report("Ambient-VFX values must be finite numbers.");
                return;
            }

            string displayName = request.displayName?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(displayName)
                || !LevelDressingIds.IsAmbientEffect(request.effectId))
            {
                Report("Ambient VFX needs a name and supported effect.");
                return;
            }

            LevelDressingData before = workspace.CreateSnapshot().dressing;
            LevelDressingData after = before.DeepCopy();
            LevelAmbientVfxData replacement = after.ambientVfx.FirstOrDefault(value =>
                string.Equals(value?.id, request.id, StringComparison.Ordinal));
            if (replacement == null)
            {
                Report("Choose existing ambient VFX.");
                return;
            }
            replacement.displayName = displayName;
            replacement.effectId = request.effectId;
            replacement.position = position;
            replacement.rotationEuler = rotation;
            replacement.scale = scale;
            Execute(before, after, "Edit ambient VFX", $"Updated '{displayName}'.");
        }

        public void DeleteAmbientVfx(string id) => Delete(
            LevelDressingTargetKind.AmbientVfx,
            id,
            "Delete ambient VFX",
            "Deleted the ambient VFX.");

        public void AddAudioZone()
        {
            LevelEditorCameraState camera = captureCameraState();
            AddAudioZoneAt(new Vector3(camera.target.x, camera.target.y, camera.target.z));
        }

        public void AddAudioZoneAt(Vector3 position)
        {
            LevelDressingData before = workspace.CreateSnapshot().dressing;
            if (before.audioZones.Count >= LevelDressingData.MaximumAudioZoneCount)
            {
                Report($"A level supports at most {LevelDressingData.MaximumAudioZoneCount} audio zones.");
                return;
            }

            LevelDressingData after = before.DeepCopy();
            string id = "audio-" + LevelDocumentFactory.NewStableId();
            after.audioZones.Add(new LevelAudioZoneData
            {
                id = id,
                displayName = $"Audio Zone {after.audioZones.Count + 1}",
                center = new Float3Data(position.x, position.y, position.z),
            });
            Execute(before, after, "Add audio zone", "Added an audio zone at the camera focus.");
            FocusRequested?.Invoke(LevelDressingTargetKind.AudioZone, id);
        }

        public void ApplyAudioZone(LevelAudioZoneAuthoringRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            if (!TryVector(request.center, out Float3Data center)
                || !TryVector(request.size, out Float3Data size)
                || !TryFloat(request.volume, out float volume)
                || !TryFloat(request.fadeDistance, out float fadeDistance))
            {
                Report("Audio-zone values must be finite numbers.");
                return;
            }

            string displayName = request.displayName?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(displayName)
                || !LevelDressingIds.IsAmbientSound(request.soundId))
            {
                Report("An audio zone needs a name and supported sound.");
                return;
            }

            LevelDressingData before = workspace.CreateSnapshot().dressing;
            LevelDressingData after = before.DeepCopy();
            LevelAudioZoneData replacement = after.audioZones.FirstOrDefault(value =>
                string.Equals(value?.id, request.id, StringComparison.Ordinal));
            if (replacement == null)
            {
                Report("Choose an existing audio zone.");
                return;
            }
            replacement.displayName = displayName;
            replacement.soundId = request.soundId;
            replacement.center = center;
            replacement.size = size;
            replacement.volume = volume;
            replacement.fadeDistance = fadeDistance;
            Execute(before, after, "Edit audio zone", $"Updated '{displayName}'.");
        }

        public void DeleteAudioZone(string id) => Delete(
            LevelDressingTargetKind.AudioZone,
            id,
            "Delete audio zone",
            "Deleted the audio zone.");

        private void Delete(
            LevelDressingTargetKind kind,
            string id,
            string description,
            string status)
        {
            LevelDressingData before = workspace.CreateSnapshot().dressing;
            LevelDressingData after = before.DeepCopy();
            int removed;
            switch (kind)
            {
                case LevelDressingTargetKind.Decal:
                    removed = after.decals.RemoveAll(value => Matches(value?.id, id));
                    break;
                case LevelDressingTargetKind.AmbientVfx:
                    removed = after.ambientVfx.RemoveAll(value => Matches(value?.id, id));
                    break;
                default:
                    removed = after.audioZones.RemoveAll(value => Matches(value?.id, id));
                    break;
            }
            if (removed == 0)
            {
                Report("Choose an existing dressing item.");
                return;
            }
            Execute(before, after, description, status);
        }

        private void Execute(
            LevelDressingData before,
            LevelDressingData after,
            string description,
            string status)
        {
            if (!Validate(after))
                return;
            workspace.Execute(new SetLevelDressingCommand(before, after, description));
            Report(status);
        }

        private bool Validate(LevelDressingData dressing)
        {
            LevelDocument candidate = workspace.CreateSnapshot();
            candidate.dressing = dressing.DeepCopy();
            LevelValidationIssue error = DressingValidator.Validate(candidate)
                .FirstOrDefault(issue => issue.Severity == LevelValidationSeverity.Error);
            if (error == null)
                return true;
            Report(error.Message);
            return false;
        }

        private void Report(string message) => StatusChanged?.Invoke(message ?? string.Empty);

        private static bool Matches(string left, string right) =>
            string.Equals(left, right, StringComparison.Ordinal);

        private static bool TryColor(
            LevelColorAuthoringText text,
            string alphaText,
            out FloatColorData value)
        {
            value = default;
            if (text == null
                || !TryFloat(text.r, out float r)
                || !TryFloat(text.g, out float g)
                || !TryFloat(text.b, out float b)
                || !TryFloat(alphaText, out float alpha))
            {
                return false;
            }
            value = new FloatColorData(r, g, b, alpha);
            return true;
        }

        private static bool TryVector(LevelVectorAuthoringText text, out Float3Data value)
        {
            value = default;
            if (text == null
                || !TryFloat(text.x, out float x)
                || !TryFloat(text.y, out float y)
                || !TryFloat(text.z, out float z))
            {
                return false;
            }
            value = new Float3Data(x, y, z);
            return true;
        }

        private static bool TryFloat(string text, out float value)
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
