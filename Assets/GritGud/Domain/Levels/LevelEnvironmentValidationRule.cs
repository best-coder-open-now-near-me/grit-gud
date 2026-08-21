using System;
using System.Collections.Generic;
using System.Linq;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;

namespace GritGud.Domain.Levels
{
    public sealed class LevelEnvironmentValidationRule : ILevelValidationRule
    {
        public void Evaluate(LevelValidationContext context)
        {
            LevelEnvironmentData environment = context.Document.environment;
            if (environment == null)
            {
                context.Error("environment.missing", "The level needs environment settings.");
                return;
            }

            if (string.IsNullOrWhiteSpace(environment.presetId))
            {
                context.Error(
                    "environment.preset.missing",
                    "The level environment needs a stable preset ID.");
            }

            LevelAtmosphereData atmosphere = environment.atmosphere;
            if (atmosphere == null
                || !ValidColor(atmosphere.ambientSky)
                || !ValidColor(atmosphere.ambientEquator)
                || !ValidColor(atmosphere.ambientGround)
                || !ValidColor(atmosphere.subtractiveShadow)
                || !ValidColor(atmosphere.fogColor)
                || !NonNegative(atmosphere.ambientIntensity)
                || !NonNegative(atmosphere.reflectionIntensity)
                || !NonNegative(atmosphere.fogStartDistance)
                || !LevelValidationMath.IsFinite(atmosphere.fogEndDistance)
                || (atmosphere.fogEnabled
                    && atmosphere.fogEndDistance <= atmosphere.fogStartDistance))
            {
                context.Error(
                    "environment.atmosphere.invalid",
                    "Environment colors and intensities must be finite and non-negative; fog must end after it starts.");
            }

            LevelDirectionalLightData key = environment.keyLight;
            if (key == null
                || !ValidColor(key.color)
                || !NonNegative(key.intensity)
                || !NonNegative(key.bounceIntensity)
                || !UnitInterval(key.shadowStrength)
                || !NonNegative(key.shadowBias)
                || !NonNegative(key.shadowNormalBias)
                || !LevelValidationMath.IsFinite(key.rotationEuler))
            {
                context.Error(
                    "environment.key-light.invalid",
                    "The environment key light contains invalid color, intensity, shadow, or rotation values.");
            }

            if (!ValidColor(environment.fixtureHousingColor)
                || !NonNegative(environment.lensEmissionIntensity))
            {
                context.Error(
                    "environment.fixture.invalid",
                    "Practical-light fixture color and emission must be finite and non-negative.");
            }

            if (environment.practicalLights.Count > LevelEnvironmentData.MaximumPracticalLights)
            {
                context.Error(
                    "environment.lights.limit",
                    $"The environment contains {environment.practicalLights.Count} practical lights; "
                    + $"the cross-platform limit is {LevelEnvironmentData.MaximumPracticalLights}.");
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (LevelPracticalLightData light in environment.practicalLights)
            {
                if (light == null)
                {
                    context.Error(
                        "environment.light.missing",
                        "The practical-light list contains an empty entry.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(light.id) || !ids.Add(light.id))
                {
                    context.Error(
                        "environment.light.id",
                        "Practical-light IDs must be present and unique.");
                }
                if (string.IsNullOrWhiteSpace(light.displayName)
                    || !LevelValidationMath.IsFinite(light.position)
                    || !LevelValidationMath.IsFinite(light.target)
                    || !ValidColor(light.color)
                    || !NonNegative(light.intensity)
                    || !LevelValidationMath.IsFinite(light.range)
                    || light.range <= 0f
                    || !LevelValidationMath.IsFinite(light.spotAngle)
                    || light.spotAngle < 1f
                    || light.spotAngle > 179f
                    || !LevelValidationMath.IsFinite(light.innerSpotFraction)
                    || light.innerSpotFraction <= 0f
                    || light.innerSpotFraction > 1f
                    || !LevelValidationMath.IsFinite(light.baseHeight))
                {
                    context.Error(
                        "environment.light.invalid",
                        $"Practical light '{light.id}' contains invalid display, transform, color, or projection values.");
                }
            }
        }

        private static bool ValidColor(FloatColorData color)
        {
            return LevelValidationMath.IsFinite(color)
                && color.r >= 0f
                && color.g >= 0f
                && color.b >= 0f
                && color.a >= 0f;
        }

        private static bool NonNegative(float value) =>
            LevelValidationMath.IsFinite(value) && value >= 0f;

        private static bool UnitInterval(float value) =>
            LevelValidationMath.IsFinite(value) && value >= 0f && value <= 1f;
    }
}
