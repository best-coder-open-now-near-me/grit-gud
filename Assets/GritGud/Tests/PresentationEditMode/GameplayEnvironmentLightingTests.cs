using System.Linq;
using GritGud.Domain.Levels;
using GritGud.Presentation.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace GritGud.Presentation.Tests
{
    public sealed class GameplayEnvironmentLightingTests
    {
        [Test]
        public void PortableEnvironmentProjectsAtmosphereKeyAndPracticalLights()
        {
            var parent = new GameObject("Lighting Test Parent");
            GameplayEnvironmentLighting lighting = null;
            try
            {
                var environment = new LevelEnvironmentData();
                environment.atmosphere.fogEnabled = false;
                environment.atmosphere.ambientIntensity = 1.15f;
                environment.keyLight.intensity = 1.35f;
                environment.practicalLights.Add(new LevelPracticalLightData
                {
                    id = "test-light",
                    displayName = "Test Flood",
                    position = new Float3Data(2f, 6f, -3f),
                    target = new Float3Data(2f, 0f, 1f),
                    color = new FloatColorData(1.2f, 0.3f, 0.1f),
                    intensity = 4f,
                    range = 17f,
                    spotAngle = 52f,
                    innerSpotFraction = 0.6f,
                });

                lighting = GameplayEnvironmentLighting.Create(parent.transform, environment);

                Light practical = parent.GetComponentsInChildren<Light>(true)
                    .Single(light => light.type == LightType.Spot);
                Assert.That(practical.name, Is.EqualTo("Test Flood"));
                Assert.That(practical.intensity, Is.EqualTo(4f));
                Assert.That(practical.range, Is.EqualTo(17f));
                Assert.That(RenderSettings.ambientIntensity, Is.EqualTo(1.15f));
                Assert.That(RenderSettings.fog, Is.False);
                Assert.That(RenderSettings.sun.intensity, Is.EqualTo(1.35f));
            }
            finally
            {
                lighting?.Dispose();
                Object.DestroyImmediate(parent);
            }
        }
    }
}
