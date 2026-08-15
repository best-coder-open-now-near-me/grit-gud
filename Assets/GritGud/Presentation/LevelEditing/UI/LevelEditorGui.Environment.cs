using System;
using System.Globalization;
using System.Linq;
using GritGud.Domain.Levels;
using UnityEngine;

namespace GritGud.Presentation.LevelEditing.UI
{
    public sealed partial class LevelEditorGui
    {
        private LevelEnvironmentAuthoringRequest environmentFields =
            new LevelEnvironmentAuthoringRequest();
        private LevelPracticalLightAuthoringRequest practicalLightFields =
            new LevelPracticalLightAuthoringRequest();
        private string selectedPracticalLightId = string.Empty;
        private string synchronizedEnvironmentIdentity = string.Empty;

        public void SyncEnvironmentFields(LevelDocument document, bool force = false)
        {
            LevelEnvironmentData environment = document?.environment;
            if (environment == null)
                return;
            string identity = BuildEnvironmentIdentity(environment);
            if (!force && string.Equals(
                    identity,
                    synchronizedEnvironmentIdentity,
                    StringComparison.Ordinal))
            {
                return;
            }

            synchronizedEnvironmentIdentity = identity;
            environmentFields = ToRequest(environment);
            LevelPracticalLightData selected = environment.practicalLights.FirstOrDefault(light =>
                string.Equals(light?.id, selectedPracticalLightId, StringComparison.Ordinal));
            if (selected == null)
            {
                selected = environment.practicalLights.FirstOrDefault(light => light != null);
                selectedPracticalLightId = selected?.id ?? string.Empty;
            }
            practicalLightFields = selected == null
                ? new LevelPracticalLightAuthoringRequest()
                : ToRequest(selected);
        }

        public void SelectPracticalLight(string lightId, LevelDocument document)
        {
            selectedPracticalLightId = lightId ?? string.Empty;
            synchronizedEnvironmentIdentity = string.Empty;
            SyncEnvironmentFields(document, force: true);
            presentationState.ShowPage(LevelEditorWorkspacePage.Environment);
        }

        private void DrawEnvironment(LevelDocument document)
        {
            SyncEnvironmentFields(document);
            GUILayout.Space(LevelEditorGuiMetrics.SpaceSection);
            DrawSectionHeader("ATMOSPHERE");
            DrawLabeledField("Preset", ref environmentFields.presetId);
            DrawColorFields("Sky RGB", environmentFields.ambientSky);
            DrawColorFields("Horizon RGB", environmentFields.ambientEquator);
            DrawColorFields("Ground RGB", environmentFields.ambientGround);
            DrawLabeledField("Ambient", ref environmentFields.ambientIntensity);
            DrawLabeledField("Reflection", ref environmentFields.reflectionIntensity);
            DrawColorFields("Shadow RGB", environmentFields.subtractiveShadow);
            environmentFields.fogEnabled = GUILayout.Toggle(
                environmentFields.fogEnabled,
                "Enable linear fog");
            DrawColorFields("Fog RGB", environmentFields.fogColor);
            DrawLabeledField("Fog start", ref environmentFields.fogStartDistance);
            DrawLabeledField("Fog end", ref environmentFields.fogEndDistance);

            GUILayout.Space(LevelEditorGuiMetrics.SpaceSection);
            DrawSectionHeader("DIRECTIONAL KEY");
            DrawColorFields("Color RGB", environmentFields.keyColor);
            DrawLabeledField("Intensity", ref environmentFields.keyIntensity);
            DrawLabeledField("Bounce", ref environmentFields.keyBounceIntensity);
            DrawLabeledField("Shadow", ref environmentFields.keyShadowStrength);
            DrawLabeledField("Bias", ref environmentFields.keyShadowBias);
            DrawLabeledField("Normal bias", ref environmentFields.keyShadowNormalBias);
            DrawVectorFields("Rotation", environmentFields.keyRotation);

            GUILayout.Space(LevelEditorGuiMetrics.SpaceSection);
            DrawSectionHeader("FIXTURES");
            DrawColorFields("Housing", environmentFields.fixtureHousingColor);
            DrawLabeledField("Emission", ref environmentFields.lensEmissionIntensity);
            GUILayout.Label("Use the anchored Apply button below to update this view and Test Play.");

            GUILayout.Space(LevelEditorGuiMetrics.SpaceSection);
            DrawSectionHeader(
                $"PRACTICAL LIGHTS ({document.environment.practicalLights.Count}/"
                + $"{LevelEnvironmentData.MaximumPracticalLights})");
            foreach (LevelPracticalLightData light in document.environment.practicalLights
                .Where(light => light != null))
            {
                Color previous = GUI.backgroundColor;
                if (string.Equals(light.id, selectedPracticalLightId, StringComparison.Ordinal))
                    GUI.backgroundColor = LevelEditorTheme.Active;
                if (GUILayout.Button(light.displayName, PanelCompactButtonLayout()))
                {
                    selectedPracticalLightId = light.id;
                    practicalLightFields = ToRequest(light);
                }
                GUI.backgroundColor = previous;
            }

            GUI.enabled = document.environment.practicalLights.Count
                < LevelEnvironmentData.MaximumPracticalLights;
            if (GUILayout.Button("+ ADD AT VIEW", PanelButtonLayout()))
                actions.AddPracticalLight();
            GUI.enabled = true;

            LevelPracticalLightData selected = document.environment.practicalLights
                .FirstOrDefault(light => string.Equals(
                    light?.id,
                    selectedPracticalLightId,
                    StringComparison.Ordinal));
            if (selected == null)
                return;

            GUILayout.Space(LevelEditorGuiMetrics.SpaceGroup);
            DrawLabeledField("Name", ref practicalLightFields.displayName);
            DrawVectorFields("Position", practicalLightFields.position);
            DrawVectorFields("Target", practicalLightFields.target);
            DrawColorFields("Color RGB", practicalLightFields.color);
            DrawLabeledField("Intensity", ref practicalLightFields.intensity);
            DrawLabeledField("Range", ref practicalLightFields.range);
            DrawLabeledField("Outer angle", ref practicalLightFields.spotAngle);
            DrawLabeledField("Inner %", ref practicalLightFields.innerSpotFraction);
            DrawLabeledField("Mast base", ref practicalLightFields.baseHeight);
            if (GUILayout.Button("APPLY PRACTICAL LIGHT", PanelApplyButtonLayout()))
                actions.ApplyPracticalLight(practicalLightFields);
            Color deleteColor = GUI.backgroundColor;
            GUI.backgroundColor = LevelEditorTheme.Warning;
            if (GUILayout.Button("DELETE PRACTICAL LIGHT", PanelButtonLayout()))
                actions.DeletePracticalLight(selectedPracticalLightId);
            GUI.backgroundColor = deleteColor;
        }

        private void DrawEnvironmentApplyFooter()
        {
            GUILayout.Space(LevelEditorGuiMetrics.SpaceSection);
            Color previous = GUI.backgroundColor;
            GUI.backgroundColor = LevelEditorTheme.Positive;
            if (GUILayout.Button(
                    "APPLY ENVIRONMENT SETTINGS",
                    PanelPrimaryButtonLayout()))
            {
                actions.ApplyEnvironment(environmentFields);
            }
            GUI.backgroundColor = previous;
            GUILayout.Space(LevelEditorGuiMetrics.SpaceSection);
        }

        private static void DrawColorFields(string label, LevelColorAuthoringText color)
        {
            GUILayout.Label(label);
            GUILayout.BeginHorizontal();
            color.r = GUILayout.TextField(color.r ?? string.Empty);
            color.g = GUILayout.TextField(color.g ?? string.Empty);
            color.b = GUILayout.TextField(color.b ?? string.Empty);
            GUILayout.EndHorizontal();
        }

        private static void DrawVectorFields(string label, LevelVectorAuthoringText vector)
        {
            GUILayout.Label(label + " XYZ");
            GUILayout.BeginHorizontal();
            vector.x = GUILayout.TextField(vector.x ?? string.Empty);
            vector.y = GUILayout.TextField(vector.y ?? string.Empty);
            vector.z = GUILayout.TextField(vector.z ?? string.Empty);
            GUILayout.EndHorizontal();
        }

        private static LevelEnvironmentAuthoringRequest ToRequest(LevelEnvironmentData value)
        {
            return new LevelEnvironmentAuthoringRequest
            {
                presetId = value.presetId,
                ambientSky = ToText(value.atmosphere.ambientSky),
                ambientEquator = ToText(value.atmosphere.ambientEquator),
                ambientGround = ToText(value.atmosphere.ambientGround),
                ambientIntensity = Format(value.atmosphere.ambientIntensity),
                reflectionIntensity = Format(value.atmosphere.reflectionIntensity),
                subtractiveShadow = ToText(value.atmosphere.subtractiveShadow),
                fogEnabled = value.atmosphere.fogEnabled,
                fogColor = ToText(value.atmosphere.fogColor),
                fogStartDistance = Format(value.atmosphere.fogStartDistance),
                fogEndDistance = Format(value.atmosphere.fogEndDistance),
                keyColor = ToText(value.keyLight.color),
                keyIntensity = Format(value.keyLight.intensity),
                keyBounceIntensity = Format(value.keyLight.bounceIntensity),
                keyShadowStrength = Format(value.keyLight.shadowStrength),
                keyShadowBias = Format(value.keyLight.shadowBias),
                keyShadowNormalBias = Format(value.keyLight.shadowNormalBias),
                keyRotation = ToText(value.keyLight.rotationEuler),
                fixtureHousingColor = ToText(value.fixtureHousingColor),
                lensEmissionIntensity = Format(value.lensEmissionIntensity),
            };
        }

        private static LevelPracticalLightAuthoringRequest ToRequest(
            LevelPracticalLightData value)
        {
            return new LevelPracticalLightAuthoringRequest
            {
                id = value.id,
                displayName = value.displayName,
                position = ToText(value.position),
                target = ToText(value.target),
                color = ToText(value.color),
                intensity = Format(value.intensity),
                range = Format(value.range),
                spotAngle = Format(value.spotAngle),
                innerSpotFraction = Format(value.innerSpotFraction),
                baseHeight = Format(value.baseHeight),
            };
        }

        private static LevelColorAuthoringText ToText(FloatColorData value) =>
            new LevelColorAuthoringText
            {
                r = Format(value.r),
                g = Format(value.g),
                b = Format(value.b),
            };

        private static LevelVectorAuthoringText ToText(Float3Data value) =>
            new LevelVectorAuthoringText
            {
                x = Format(value.x),
                y = Format(value.y),
                z = Format(value.z),
            };

        private static string Format(float value) =>
            value.ToString("0.###", CultureInfo.InvariantCulture);

        private static string BuildEnvironmentIdentity(LevelEnvironmentData value)
        {
            return value.presetId + "\n"
                + ColorIdentity(value.atmosphere.ambientSky) + "\n"
                + ColorIdentity(value.atmosphere.ambientEquator) + "\n"
                + ColorIdentity(value.atmosphere.ambientGround) + "\n"
                + value.atmosphere.ambientIntensity.ToString("R", CultureInfo.InvariantCulture)
                + "\n" + value.atmosphere.reflectionIntensity.ToString("R", CultureInfo.InvariantCulture)
                + "\n" + ColorIdentity(value.atmosphere.subtractiveShadow)
                + "\n" + value.atmosphere.fogEnabled
                + "\n" + ColorIdentity(value.atmosphere.fogColor)
                + "\n" + value.atmosphere.fogStartDistance.ToString("R", CultureInfo.InvariantCulture)
                + "\n" + value.atmosphere.fogEndDistance.ToString("R", CultureInfo.InvariantCulture)
                + "\n" + ColorIdentity(value.keyLight.color)
                + "\n" + value.keyLight.intensity.ToString("R", CultureInfo.InvariantCulture)
                + "\n" + value.keyLight.bounceIntensity.ToString("R", CultureInfo.InvariantCulture)
                + "\n" + value.keyLight.shadowStrength.ToString("R", CultureInfo.InvariantCulture)
                + "\n" + value.keyLight.shadowBias.ToString("R", CultureInfo.InvariantCulture)
                + "\n" + value.keyLight.shadowNormalBias.ToString("R", CultureInfo.InvariantCulture)
                + "\n" + VectorIdentity(value.keyLight.rotationEuler)
                + "\n" + ColorIdentity(value.fixtureHousingColor)
                + "\n" + value.lensEmissionIntensity.ToString("R", CultureInfo.InvariantCulture)
                + "\n" + value.practicalLights.Count
                + "\n" + string.Join("|", value.practicalLights
                    .Where(light => light != null)
                    .Select(light => light.id + ":" + light.displayName + ":"
                        + VectorIdentity(light.position) + ":"
                        + VectorIdentity(light.target) + ":"
                        + ColorIdentity(light.color) + ":"
                        + light.intensity.ToString("R", CultureInfo.InvariantCulture) + ":"
                        + light.range.ToString("R", CultureInfo.InvariantCulture) + ":"
                        + light.spotAngle.ToString("R", CultureInfo.InvariantCulture) + ":"
                        + light.innerSpotFraction.ToString("R", CultureInfo.InvariantCulture) + ":"
                        + light.baseHeight.ToString("R", CultureInfo.InvariantCulture)));
        }

        private static string ColorIdentity(FloatColorData value) =>
            value.r.ToString("R", CultureInfo.InvariantCulture) + ":"
            + value.g.ToString("R", CultureInfo.InvariantCulture) + ":"
            + value.b.ToString("R", CultureInfo.InvariantCulture) + ":"
            + value.a.ToString("R", CultureInfo.InvariantCulture);

        private static string VectorIdentity(Float3Data value) =>
            value.x.ToString("R", CultureInfo.InvariantCulture) + ":"
            + value.y.ToString("R", CultureInfo.InvariantCulture) + ":"
            + value.z.ToString("R", CultureInfo.InvariantCulture);
    }
}
