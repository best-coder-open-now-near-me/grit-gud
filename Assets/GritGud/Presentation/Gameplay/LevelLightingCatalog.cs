using System;
using System.Collections.Generic;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    [Serializable]
    public sealed class AtmospherePresentationDefinition
    {
        [SerializeField] private Color ambientSky = new Color(0.08f, 0.14f, 0.25f, 1f);
        [SerializeField] private Color ambientEquator = new Color(0.04f, 0.1f, 0.2f, 1f);
        [SerializeField] private Color ambientGround = new Color(0.018f, 0.035f, 0.075f, 1f);
        [SerializeField, Min(0f)] private float ambientIntensity = 0.85f;
        [SerializeField, Min(0f)] private float reflectionIntensity = 0.45f;
        [SerializeField] private Color subtractiveShadow = new Color(0.015f, 0.025f, 0.06f, 1f);
        [SerializeField] private Color fogColor = new Color(0.025f, 0.065f, 0.14f, 1f);
        [SerializeField, Min(0f)] private float fogStartDistance = 18f;
        [SerializeField, Min(0.01f)] private float fogEndDistance = 58f;

        public Color AmbientSky => ambientSky;
        public Color AmbientEquator => ambientEquator;
        public Color AmbientGround => ambientGround;
        public float AmbientIntensity => Mathf.Max(0f, ambientIntensity);
        public float ReflectionIntensity => Mathf.Max(0f, reflectionIntensity);
        public Color SubtractiveShadow => subtractiveShadow;
        public Color FogColor => fogColor;
        public float FogStartDistance => Mathf.Max(0f, fogStartDistance);
        public float FogEndDistance => Mathf.Max(FogStartDistance + 0.01f, fogEndDistance);
    }

    [Serializable]
    public sealed class DirectionalLightPresentationDefinition
    {
        [SerializeField] private Color color = Color.white;
        [SerializeField, Min(0f)] private float intensity = 0.92f;
        [SerializeField, Min(0f)] private float bounceIntensity = 0.25f;
        [SerializeField, Range(0f, 1f)] private float shadowStrength = 0.88f;
        [SerializeField, Min(0f)] private float shadowBias = 0.075f;
        [SerializeField, Min(0f)] private float shadowNormalBias = 0.4f;
        [SerializeField] private Vector3 rotationEuler = new Vector3(42f, -28f, 0f);

        public Color Color => color;
        public float Intensity => Mathf.Max(0f, intensity);
        public float BounceIntensity => Mathf.Max(0f, bounceIntensity);
        public float ShadowStrength => Mathf.Clamp01(shadowStrength);
        public float ShadowBias => Mathf.Max(0f, shadowBias);
        public float ShadowNormalBias => Mathf.Max(0f, shadowNormalBias);
        public Quaternion Rotation => Quaternion.Euler(rotationEuler);
    }

    [Serializable]
    public sealed class PracticalLightPresentationDefinition
    {
        [SerializeField] private string name = string.Empty;
        [SerializeField] private Vector3 position;
        [SerializeField] private Vector3 target;
        [SerializeField] private Color color = Color.white;
        [SerializeField, Min(0f)] private float intensity = 3f;
        [SerializeField, Min(0.01f)] private float range = 14f;
        [SerializeField, Range(1f, 179f)] private float spotAngle = 55f;
        [SerializeField, Range(0.01f, 1f)] private float innerSpotFraction = 0.58f;
        [SerializeField, Min(0f)] private float baseHeight;
        [SerializeField, Range(0f, 1f)] private float shadowStrength = 0.72f;
        [SerializeField, Min(0f)] private float shadowBias = 0.04f;
        [SerializeField, Min(0f)] private float shadowNormalBias = 0.32f;
        [SerializeField, Min(0.01f)] private float mastRadius = 0.09f;
        [SerializeField] private Vector3 housingSize = new Vector3(0.72f, 0.34f, 0.5f);
        [SerializeField] private Vector3 lensSize = new Vector3(0.56f, 0.22f, 0.035f);

        public string Name => string.IsNullOrWhiteSpace(name) ? "Practical Light" : name;
        public Vector3 Position => position;
        public Vector3 Target => target;
        public Color Color => color;
        public float Intensity => Mathf.Max(0f, intensity);
        public float Range => Mathf.Max(0.01f, range);
        public float SpotAngle => Mathf.Clamp(spotAngle, 1f, 179f);
        public float InnerSpotAngle => SpotAngle * Mathf.Clamp(innerSpotFraction, 0.01f, 1f);
        public float BaseHeight => baseHeight;
        public float ShadowStrength => Mathf.Clamp01(shadowStrength);
        public float ShadowBias => Mathf.Max(0f, shadowBias);
        public float ShadowNormalBias => Mathf.Max(0f, shadowNormalBias);
        public float MastRadius => Mathf.Max(0.01f, mastRadius);
        public Vector3 HousingSize => Positive(housingSize);
        public Vector3 LensSize => Positive(lensSize);

        private static Vector3 Positive(Vector3 value) => new Vector3(
            Mathf.Max(0.01f, value.x),
            Mathf.Max(0.01f, value.y),
            Mathf.Max(0.01f, value.z));
    }

    [Serializable]
    public sealed class AmbientEffectPlacementDefinition
    {
        [SerializeField] private string name = string.Empty;
        [SerializeField] private GameObject prefab;
        [SerializeField] private Vector3 position;
        [SerializeField] private Vector3 rotationEuler;
        [SerializeField] private Vector3 scale = Vector3.one;

        public string Name => string.IsNullOrWhiteSpace(name) ? "Ambient Effect" : name;
        public GameObject Prefab => prefab;
        public Vector3 Position => position;
        public Quaternion Rotation => Quaternion.Euler(rotationEuler);
        public Vector3 Scale => new Vector3(
            Mathf.Max(0.01f, scale.x),
            Mathf.Max(0.01f, scale.y),
            Mathf.Max(0.01f, scale.z));
    }

    [Serializable]
    public sealed class LevelLightingProfile
    {
        [SerializeField] private string levelId = string.Empty;
        [SerializeField] private AtmospherePresentationDefinition atmosphere =
            new AtmospherePresentationDefinition();
        [SerializeField] private DirectionalLightPresentationDefinition keyLight =
            new DirectionalLightPresentationDefinition();
        [SerializeField] private Color fixtureHousingColor =
            new Color(0.035f, 0.065f, 0.11f, 1f);
        [SerializeField, Min(0f)] private float lensEmissionIntensity = 5f;
        [SerializeField] private PracticalLightPresentationDefinition[] practicalLights =
            Array.Empty<PracticalLightPresentationDefinition>();
        [SerializeField] private AmbientEffectPlacementDefinition[] ambientEffects =
            Array.Empty<AmbientEffectPlacementDefinition>();

        public string LevelId => levelId;
        public AtmospherePresentationDefinition Atmosphere => atmosphere;
        public DirectionalLightPresentationDefinition KeyLight => keyLight;
        public Color FixtureHousingColor => fixtureHousingColor;
        public float LensEmissionIntensity => Mathf.Max(0f, lensEmissionIntensity);
        public IReadOnlyList<PracticalLightPresentationDefinition> PracticalLights =>
            practicalLights ?? Array.Empty<PracticalLightPresentationDefinition>();
        public IReadOnlyList<AmbientEffectPlacementDefinition> AmbientEffects =>
            ambientEffects ?? Array.Empty<AmbientEffectPlacementDefinition>();
    }

    [CreateAssetMenu(
        fileName = "LevelLightingCatalog",
        menuName = "Grit Gud/Level Lighting Catalog")]
    public sealed class LevelLightingCatalog : ScriptableObject
    {
        internal const string DefaultResourceName = "Gameplay/LevelLightingCatalog";

        [SerializeField] private LevelLightingProfile[] entries =
            Array.Empty<LevelLightingProfile>();
        private Dictionary<string, LevelLightingProfile> index;

        public static LevelLightingCatalog LoadDefault()
        {
            LevelLightingCatalog catalog = Resources.Load<LevelLightingCatalog>(
                DefaultResourceName);
            if (catalog == null)
            {
                throw new InvalidOperationException(
                    $"The Resources lighting catalog '{DefaultResourceName}' could not be loaded.");
            }

            return catalog;
        }

        public LevelLightingProfile Get(string levelId)
        {
            EnsureIndex();
            if (!index.TryGetValue(levelId ?? string.Empty, out LevelLightingProfile profile))
            {
                throw new KeyNotFoundException(
                    $"Level lighting profile '{levelId}' is not defined.");
            }

            return profile;
        }

        public LevelLightingProfile GetOrAny(string levelId)
        {
            EnsureIndex();
            return index.TryGetValue(levelId ?? string.Empty, out LevelLightingProfile profile)
                ? profile
                : GetAny();
        }

        public LevelLightingProfile GetAny()
        {
            EnsureIndex();
            foreach (LevelLightingProfile profile in index.Values)
            {
                return profile;
            }

            throw new InvalidOperationException("The lighting catalog has no profiles.");
        }

        private void OnEnable() => index = null;

        private void EnsureIndex()
        {
            if (index != null)
            {
                return;
            }

            index = new Dictionary<string, LevelLightingProfile>(StringComparer.Ordinal);
            foreach (LevelLightingProfile entry in entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.LevelId))
                {
                    continue;
                }

                if (!index.TryAdd(entry.LevelId, entry))
                {
                    throw new InvalidOperationException(
                        $"Level lighting profile '{entry.LevelId}' is duplicated.");
                }
            }
        }
    }
}
