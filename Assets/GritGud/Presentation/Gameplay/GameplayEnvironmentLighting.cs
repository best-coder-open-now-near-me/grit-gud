using System;
using System.Collections.Generic;
using GritGud.Presentation.Levels.Runtime;
using UnityEngine;
using UnityEngine.Rendering;

namespace GritGud.Presentation.Gameplay
{
    internal sealed class GameplayEnvironmentLighting : IDisposable
    {
        private readonly RenderSettingsSnapshot renderSettingsSnapshot;
        private readonly LightSnapshot? sunSnapshot;
        private readonly List<Material> runtimeMaterials;
        private GameObject root;

        private GameplayEnvironmentLighting(
            GameObject lightingRoot,
            RenderSettingsSnapshot originalRenderSettings,
            LightSnapshot? originalSun,
            List<Material> materials)
        {
            root = lightingRoot;
            renderSettingsSnapshot = originalRenderSettings;
            sunSnapshot = originalSun;
            runtimeMaterials = materials;
        }

        public static GameplayEnvironmentLighting Create(
            Transform parent,
            LevelLightingProfile profile)
        {
            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            var renderSettings = new RenderSettingsSnapshot(captureCurrent: true);
            Light sun = FindDirectionalLight();
            LightSnapshot? originalSun = sun != null
                ? new LightSnapshot(sun)
                : null;

            var lightingRoot = new GameObject("Gameplay Environment Lighting");
            lightingRoot.transform.SetParent(parent, false);
            var materials = new List<Material>();
            Material fixtureMaterial = RuntimeMaterialFactory.CreateCelColor(
                profile.FixtureHousingColor,
                "Industrial Floodlight Housing");
            materials.Add(fixtureMaterial);
            if (sun == null)
            {
                sun = CreateLight(lightingRoot.transform, "Moon Key Light");
                sun.type = LightType.Directional;
            }

            ConfigureAtmosphere(sun, profile);
            foreach (PracticalLightPresentationDefinition practical in
                profile.PracticalLights)
            {
                CreateSpotLight(
                    lightingRoot.transform,
                    practical,
                    fixtureMaterial,
                    profile.LensEmissionIntensity,
                    materials);
            }

            foreach (AmbientEffectPlacementDefinition ambient in
                profile.AmbientEffects)
            {
                CreateAmbientEffect(lightingRoot.transform, ambient);
            }

            return new GameplayEnvironmentLighting(
                lightingRoot,
                renderSettings,
                originalSun,
                materials);
        }

        public void Dispose()
        {
            renderSettingsSnapshot.Restore();
            if (sunSnapshot.HasValue)
            {
                sunSnapshot.Value.Restore();
            }

            GameplayObjectLifecycle.Destroy(root);
            root = null;
            foreach (Material material in runtimeMaterials)
            {
                GameplayObjectLifecycle.Destroy(material);
            }

            runtimeMaterials.Clear();
        }

        private static void ConfigureAtmosphere(
            Light sun,
            LevelLightingProfile profile)
        {
            AtmospherePresentationDefinition atmosphere = profile.Atmosphere;
            DirectionalLightPresentationDefinition key = profile.KeyLight;
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = atmosphere.AmbientSky;
            RenderSettings.ambientEquatorColor = atmosphere.AmbientEquator;
            RenderSettings.ambientGroundColor = atmosphere.AmbientGround;
            RenderSettings.ambientIntensity = atmosphere.AmbientIntensity;
            RenderSettings.reflectionIntensity = atmosphere.ReflectionIntensity;
            RenderSettings.subtractiveShadowColor = atmosphere.SubtractiveShadow;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = atmosphere.FogColor;
            RenderSettings.fogStartDistance = atmosphere.FogStartDistance;
            RenderSettings.fogEndDistance = atmosphere.FogEndDistance;

            sun.gameObject.SetActive(true);
            sun.enabled = true;
            sun.type = LightType.Directional;
            sun.color = key.Color;
            sun.intensity = key.Intensity;
            sun.bounceIntensity = key.BounceIntensity;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = key.ShadowStrength;
            sun.shadowBias = key.ShadowBias;
            sun.shadowNormalBias = key.ShadowNormalBias;
            sun.transform.rotation = key.Rotation;
            RenderSettings.sun = sun;
        }

        private static void CreateSpotLight(
            Transform parent,
            PracticalLightPresentationDefinition definition,
            Material fixtureMaterial,
            float lensEmissionIntensity,
            List<Material> materials)
        {
            Light light = CreateLight(parent, definition.Name);
            light.transform.SetPositionAndRotation(
                definition.Position,
                Quaternion.LookRotation(definition.Target - definition.Position));
            light.type = LightType.Spot;
            light.color = definition.Color;
            light.intensity = definition.Intensity;
            light.range = definition.Range;
            light.spotAngle = definition.SpotAngle;
            light.innerSpotAngle = definition.InnerSpotAngle;
            light.bounceIntensity = 0f;
            // Several practical spotlights are visible at once. Let the sun
            // own scene shadows; requesting a shadow map per practical light
            // overflows URP's atlas and emits a full warning stack every frame.
            light.shadows = LightShadows.None;
            light.renderMode = LightRenderMode.ForcePixel;
            CreateFixture(
                parent,
                light.transform,
                definition,
                fixtureMaterial,
                lensEmissionIntensity,
                materials);
        }

        private static void CreateFixture(
            Transform parent,
            Transform lightTransform,
            PracticalLightPresentationDefinition definition,
            Material fixtureMaterial,
            float lensEmissionIntensity,
            List<Material> materials)
        {
            Vector3 position = definition.Position;
            float mastHeight = Mathf.Max(0.5f, position.y - definition.BaseHeight);
            GameObject mast = CreatePrimitive(
                PrimitiveType.Cylinder,
                definition.Name + " Mast",
                parent,
                fixtureMaterial);
            mast.transform.position = new Vector3(
                position.x,
                definition.BaseHeight + (mastHeight * 0.5f),
                position.z);
            mast.transform.localScale = new Vector3(
                definition.MastRadius,
                mastHeight * 0.5f,
                definition.MastRadius);

            GameObject housing = CreatePrimitive(
                PrimitiveType.Cube,
                definition.Name + " Housing",
                lightTransform,
                fixtureMaterial);
            housing.transform.localPosition = new Vector3(0f, 0f, -0.16f);
            housing.transform.localScale = definition.HousingSize;

            Material lensMaterial = CreateEmissionMaterial(
                definition.Name,
                definition.Color,
                lensEmissionIntensity);
            materials.Add(lensMaterial);
            GameObject lens = CreatePrimitive(
                PrimitiveType.Cube,
                definition.Name + " Lens",
                lightTransform,
                lensMaterial);
            lens.transform.localPosition = new Vector3(0f, 0f, 0.105f);
            lens.transform.localScale = definition.LensSize;
            Renderer lensRenderer = lens.GetComponent<Renderer>();
            lensRenderer.shadowCastingMode = ShadowCastingMode.Off;
            lensRenderer.receiveShadows = false;
        }

        private static GameObject CreatePrimitive(
            PrimitiveType primitiveType,
            string name,
            Transform parent,
            Material material)
        {
            GameObject primitive = GameObject.CreatePrimitive(primitiveType);
            primitive.name = name;
            primitive.transform.SetParent(parent, false);
            Collider collider = primitive.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
                GameplayObjectLifecycle.Destroy(collider);
            }

            primitive.GetComponent<Renderer>().sharedMaterial = material;
            return primitive;
        }

        private static Material CreateEmissionMaterial(
            string name,
            Color color,
            float emissionIntensity)
        {
            Shader shader = Shader.Find("GritGud/EmissiveSurface")
                ?? Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                throw new InvalidOperationException(
                    "No compatible emissive fixture shader is available.");
            }

            var material = new Material(shader)
            {
                name = name + " Emissive Lens",
                hideFlags = HideFlags.HideAndDontSave,
            };
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color * 0.08f);
            }

            if (material.HasProperty("_EmissionColor"))
            {
                material.SetColor("_EmissionColor", color);
            }

            if (material.HasProperty("_EmissionIntensity"))
            {
                material.SetFloat("_EmissionIntensity", emissionIntensity);
            }

            return material;
        }

        private static void CreateAmbientEffect(
            Transform parent,
            AmbientEffectPlacementDefinition definition)
        {
            if (definition?.Prefab == null)
            {
                return;
            }

            GameObject effect = UnityEngine.Object.Instantiate(
                definition.Prefab,
                definition.Position,
                definition.Rotation,
                parent);
            effect.name = definition.Name;
            effect.transform.localScale = Vector3.Scale(
                definition.Prefab.transform.localScale,
                definition.Scale);
            foreach (ParticleSystem particles in
                effect.GetComponentsInChildren<ParticleSystem>(true))
            {
                particles.Play(withChildren: true);
            }
        }

        private static Light CreateLight(Transform parent, string name)
        {
            var lightObject = new GameObject(name);
            lightObject.transform.SetParent(parent, false);
            return lightObject.AddComponent<Light>();
        }

        private static Light FindDirectionalLight()
        {
            if (RenderSettings.sun != null
                && RenderSettings.sun.type == LightType.Directional)
            {
                return RenderSettings.sun;
            }

            Light[] lights = UnityEngine.Object.FindObjectsByType<Light>(
                FindObjectsInactive.Include);
            foreach (Light light in lights)
            {
                if (light.type == LightType.Directional)
                {
                    return light;
                }
            }

            return null;
        }

        private readonly struct LightSnapshot
        {
            private readonly Light light;
            private readonly bool active;
            private readonly bool enabled;
            private readonly LightType type;
            private readonly Color color;
            private readonly float intensity;
            private readonly float bounceIntensity;
            private readonly LightShadows shadows;
            private readonly float shadowStrength;
            private readonly float shadowBias;
            private readonly float shadowNormalBias;
            private readonly Quaternion rotation;

            public LightSnapshot(Light source)
            {
                light = source;
                active = source.gameObject.activeSelf;
                enabled = source.enabled;
                type = source.type;
                color = source.color;
                intensity = source.intensity;
                bounceIntensity = source.bounceIntensity;
                shadows = source.shadows;
                shadowStrength = source.shadowStrength;
                shadowBias = source.shadowBias;
                shadowNormalBias = source.shadowNormalBias;
                rotation = source.transform.rotation;
            }

            public void Restore()
            {
                if (light == null)
                {
                    return;
                }

                light.gameObject.SetActive(active);
                light.enabled = enabled;
                light.type = type;
                light.color = color;
                light.intensity = intensity;
                light.bounceIntensity = bounceIntensity;
                light.shadows = shadows;
                light.shadowStrength = shadowStrength;
                light.shadowBias = shadowBias;
                light.shadowNormalBias = shadowNormalBias;
                light.transform.rotation = rotation;
            }
        }

        private readonly struct RenderSettingsSnapshot
        {
            private readonly Material skybox;
            private readonly AmbientMode ambientMode;
            private readonly Color ambientSkyColor;
            private readonly Color ambientEquatorColor;
            private readonly Color ambientGroundColor;
            private readonly float ambientIntensity;
            private readonly float reflectionIntensity;
            private readonly Color subtractiveShadowColor;
            private readonly bool fog;
            private readonly FogMode fogMode;
            private readonly Color fogColor;
            private readonly float fogDensity;
            private readonly float fogStartDistance;
            private readonly float fogEndDistance;
            private readonly Light sun;

            public RenderSettingsSnapshot(bool captureCurrent)
            {
                skybox = RenderSettings.skybox;
                ambientMode = RenderSettings.ambientMode;
                ambientSkyColor = RenderSettings.ambientSkyColor;
                ambientEquatorColor = RenderSettings.ambientEquatorColor;
                ambientGroundColor = RenderSettings.ambientGroundColor;
                ambientIntensity = RenderSettings.ambientIntensity;
                reflectionIntensity = RenderSettings.reflectionIntensity;
                subtractiveShadowColor = RenderSettings.subtractiveShadowColor;
                fog = RenderSettings.fog;
                fogMode = RenderSettings.fogMode;
                fogColor = RenderSettings.fogColor;
                fogDensity = RenderSettings.fogDensity;
                fogStartDistance = RenderSettings.fogStartDistance;
                fogEndDistance = RenderSettings.fogEndDistance;
                sun = RenderSettings.sun;
            }

            public void Restore()
            {
                RenderSettings.skybox = skybox;
                RenderSettings.ambientMode = ambientMode;
                RenderSettings.ambientSkyColor = ambientSkyColor;
                RenderSettings.ambientEquatorColor = ambientEquatorColor;
                RenderSettings.ambientGroundColor = ambientGroundColor;
                RenderSettings.ambientIntensity = ambientIntensity;
                RenderSettings.reflectionIntensity = reflectionIntensity;
                RenderSettings.subtractiveShadowColor = subtractiveShadowColor;
                RenderSettings.fog = fog;
                RenderSettings.fogMode = fogMode;
                RenderSettings.fogColor = fogColor;
                RenderSettings.fogDensity = fogDensity;
                RenderSettings.fogStartDistance = fogStartDistance;
                RenderSettings.fogEndDistance = fogEndDistance;
                RenderSettings.sun = sun;
            }
        }
    }
}
