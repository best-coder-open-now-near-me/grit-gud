using System;
using System.Globalization;
using System.Linq;
using GritGud.Application.Levels;
using GritGud.Domain.Levels;
using GritGud.Presentation.LevelEditing.Core;
using UnityEngine;

namespace GritGud.Presentation.LevelEditing
{
    public sealed class LevelColorAuthoringText
    {
        public string r = string.Empty;
        public string g = string.Empty;
        public string b = string.Empty;
    }

    public sealed class LevelVectorAuthoringText
    {
        public string x = string.Empty;
        public string y = string.Empty;
        public string z = string.Empty;
    }

    public sealed class LevelEnvironmentAuthoringRequest
    {
        public string presetId = string.Empty;
        public LevelColorAuthoringText ambientSky = new LevelColorAuthoringText();
        public LevelColorAuthoringText ambientEquator = new LevelColorAuthoringText();
        public LevelColorAuthoringText ambientGround = new LevelColorAuthoringText();
        public string ambientIntensity = string.Empty;
        public string reflectionIntensity = string.Empty;
        public LevelColorAuthoringText subtractiveShadow = new LevelColorAuthoringText();
        public bool fogEnabled;
        public LevelColorAuthoringText fogColor = new LevelColorAuthoringText();
        public string fogStartDistance = string.Empty;
        public string fogEndDistance = string.Empty;
        public LevelColorAuthoringText keyColor = new LevelColorAuthoringText();
        public string keyIntensity = string.Empty;
        public string keyBounceIntensity = string.Empty;
        public string keyShadowStrength = string.Empty;
        public string keyShadowBias = string.Empty;
        public string keyShadowNormalBias = string.Empty;
        public LevelVectorAuthoringText keyRotation = new LevelVectorAuthoringText();
        public LevelColorAuthoringText fixtureHousingColor = new LevelColorAuthoringText();
        public string lensEmissionIntensity = string.Empty;
    }

    public sealed class LevelPracticalLightAuthoringRequest
    {
        public string id = string.Empty;
        public string displayName = string.Empty;
        public LevelVectorAuthoringText position = new LevelVectorAuthoringText();
        public LevelVectorAuthoringText target = new LevelVectorAuthoringText();
        public LevelColorAuthoringText color = new LevelColorAuthoringText();
        public string intensity = string.Empty;
        public string range = string.Empty;
        public string spotAngle = string.Empty;
        public string innerSpotFraction = string.Empty;
        public string baseHeight = string.Empty;
    }

    public sealed class EnvironmentAuthoringCoordinator
    {
        private static readonly LevelValidationService EnvironmentValidator =
            new LevelValidationService(new ILevelValidationRule[]
            {
                new LevelEnvironmentValidationRule(),
            });
        private readonly LevelEditorWorkspace workspace;
        private readonly Func<LevelEditorCameraState> captureCameraState;

        public EnvironmentAuthoringCoordinator(
            LevelEditorWorkspace workspace,
            Func<LevelEditorCameraState> captureCameraState)
        {
            this.workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
            this.captureCameraState = captureCameraState
                ?? throw new ArgumentNullException(nameof(captureCameraState));
        }

        public event Action<string> StatusChanged;
        public event Action<string> PracticalLightFocusRequested;

        public void ApplyEnvironment(LevelEnvironmentAuthoringRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            if (!TryColor(request.ambientSky, out FloatColorData ambientSky)
                || !TryColor(request.ambientEquator, out FloatColorData ambientEquator)
                || !TryColor(request.ambientGround, out FloatColorData ambientGround)
                || !TryFloat(request.ambientIntensity, out float ambientIntensity)
                || !TryFloat(request.reflectionIntensity, out float reflectionIntensity)
                || !TryColor(request.subtractiveShadow, out FloatColorData subtractiveShadow)
                || !TryColor(request.fogColor, out FloatColorData fogColor)
                || !TryFloat(request.fogStartDistance, out float fogStart)
                || !TryFloat(request.fogEndDistance, out float fogEnd)
                || !TryColor(request.keyColor, out FloatColorData keyColor)
                || !TryFloat(request.keyIntensity, out float keyIntensity)
                || !TryFloat(request.keyBounceIntensity, out float keyBounce)
                || !TryFloat(request.keyShadowStrength, out float shadowStrength)
                || !TryFloat(request.keyShadowBias, out float shadowBias)
                || !TryFloat(request.keyShadowNormalBias, out float shadowNormalBias)
                || !TryVector(request.keyRotation, out Float3Data keyRotation)
                || !TryColor(request.fixtureHousingColor, out FloatColorData fixtureColor)
                || !TryFloat(request.lensEmissionIntensity, out float lensEmission))
            {
                Report("Environment values must be finite numbers.");
                return;
            }

            LevelEnvironmentData before = workspace.CreateSnapshot().environment;
            LevelEnvironmentData after = before.DeepCopy();
            after.presetId = request.presetId?.Trim() ?? string.Empty;
            after.atmosphere.ambientSky = ambientSky;
            after.atmosphere.ambientEquator = ambientEquator;
            after.atmosphere.ambientGround = ambientGround;
            after.atmosphere.ambientIntensity = ambientIntensity;
            after.atmosphere.reflectionIntensity = reflectionIntensity;
            after.atmosphere.subtractiveShadow = subtractiveShadow;
            after.atmosphere.fogEnabled = request.fogEnabled;
            after.atmosphere.fogColor = fogColor;
            after.atmosphere.fogStartDistance = fogStart;
            after.atmosphere.fogEndDistance = fogEnd;
            after.keyLight.color = keyColor;
            after.keyLight.intensity = keyIntensity;
            after.keyLight.bounceIntensity = keyBounce;
            after.keyLight.shadowStrength = shadowStrength;
            after.keyLight.shadowBias = shadowBias;
            after.keyLight.shadowNormalBias = shadowNormalBias;
            after.keyLight.rotationEuler = keyRotation;
            after.fixtureHousingColor = fixtureColor;
            after.lensEmissionIntensity = lensEmission;
            if (!Validate(after))
                return;
            workspace.Execute(new SetLevelEnvironmentCommand(
                before,
                after,
                "Edit atmosphere and key light"));
            Report("Applied the portable environment settings.");
        }

        public void AddPracticalLight()
        {
            LevelEditorCameraState camera = captureCameraState();
            AddPracticalLight(
                new Vector3(camera.target.x - 3f, camera.target.y + 6f, camera.target.z - 3f),
                new Vector3(camera.target.x, camera.target.y, camera.target.z),
                camera.target.y,
                "Added a practical light aimed at the camera focus.");
        }

        public void AddPracticalLightAt(Vector3 target)
        {
            AddPracticalLight(
                target + Vector3.up * 3f,
                target + Vector3.forward,
                target.y,
                "Added a practical light at the selected map position.");
        }

        private void AddPracticalLight(
            Vector3 position,
            Vector3 target,
            float baseHeight,
            string status)
        {
            LevelEnvironmentData before = workspace.CreateSnapshot().environment;
            if (before.practicalLights.Count >= LevelEnvironmentData.MaximumPracticalLights)
            {
                Report($"A level supports at most {LevelEnvironmentData.MaximumPracticalLights} practical lights.");
                return;
            }

            LevelEnvironmentData after = before.DeepCopy();
            string id = "light-" + LevelDocumentFactory.NewStableId();
            after.practicalLights.Add(new LevelPracticalLightData
            {
                id = id,
                displayName = $"Practical Light {after.practicalLights.Count + 1}",
                position = new Float3Data(position.x, position.y, position.z),
                target = new Float3Data(target.x, target.y, target.z),
                baseHeight = baseHeight,
            });
            workspace.Execute(new SetLevelEnvironmentCommand(
                before,
                after,
                "Add practical light"));
            PracticalLightFocusRequested?.Invoke(id);
            Report(status);
        }

        public void ApplyPracticalLight(LevelPracticalLightAuthoringRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            if (!TryVector(request.position, out Float3Data position)
                || !TryVector(request.target, out Float3Data target)
                || !TryColor(request.color, out FloatColorData color)
                || !TryFloat(request.intensity, out float intensity)
                || !TryFloat(request.range, out float range)
                || !TryFloat(request.spotAngle, out float spotAngle)
                || !TryFloat(request.innerSpotFraction, out float innerSpotFraction)
                || !TryFloat(request.baseHeight, out float baseHeight))
            {
                Report("Practical-light values must be finite numbers.");
                return;
            }

            LevelEnvironmentData before = workspace.CreateSnapshot().environment;
            LevelPracticalLightData current = before.practicalLights.FirstOrDefault(light =>
                string.Equals(light?.id, request.id, StringComparison.Ordinal));
            if (current == null)
            {
                Report("Choose an existing practical light.");
                return;
            }

            string displayName = request.displayName?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(displayName))
            {
                Report("A practical light needs a display name.");
                return;
            }

            LevelEnvironmentData after = before.DeepCopy();
            LevelPracticalLightData replacement = after.practicalLights.First(light =>
                string.Equals(light?.id, request.id, StringComparison.Ordinal));
            replacement.displayName = displayName;
            replacement.position = position;
            replacement.target = target;
            replacement.color = color;
            replacement.intensity = intensity;
            replacement.range = range;
            replacement.spotAngle = spotAngle;
            replacement.innerSpotFraction = innerSpotFraction;
            replacement.baseHeight = baseHeight;
            if (!Validate(after))
                return;
            workspace.Execute(new SetLevelEnvironmentCommand(
                before,
                after,
                "Edit practical light"));
            Report($"Updated '{displayName}'.");
        }

        public void DeletePracticalLight(string lightId)
        {
            LevelEnvironmentData before = workspace.CreateSnapshot().environment;
            LevelEnvironmentData after = before.DeepCopy();
            int removed = after.practicalLights.RemoveAll(light => string.Equals(
                light?.id,
                lightId,
                StringComparison.Ordinal));
            if (removed == 0)
            {
                Report("Choose an existing practical light.");
                return;
            }

            workspace.Execute(new SetLevelEnvironmentCommand(
                before,
                after,
                "Delete practical light"));
            Report("Deleted the practical light.");
        }

        private void Report(string message) => StatusChanged?.Invoke(message ?? string.Empty);

        private bool Validate(LevelEnvironmentData environment)
        {
            LevelDocument candidate = workspace.CreateSnapshot();
            candidate.environment = environment.DeepCopy();
            LevelValidationIssue error = EnvironmentValidator.Validate(candidate)
                .FirstOrDefault(issue => issue.Severity == LevelValidationSeverity.Error);
            if (error == null)
                return true;
            Report(error.Message);
            return false;
        }

        private static bool TryColor(LevelColorAuthoringText text, out FloatColorData value)
        {
            value = default;
            if (text == null
                || !TryFloat(text.r, out float r)
                || !TryFloat(text.g, out float g)
                || !TryFloat(text.b, out float b))
            {
                return false;
            }

            value = new FloatColorData(r, g, b);
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
