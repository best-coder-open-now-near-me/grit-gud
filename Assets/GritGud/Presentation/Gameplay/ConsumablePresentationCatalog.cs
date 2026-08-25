using System;
using System.Collections.Generic;
using GritGud.Application.Gameplay;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    [Serializable]
    public sealed class ThrownExplosivePresentationDefinition
    {
        [SerializeField]
        private string itemId = string.Empty;

        [SerializeField]
        private GameObject projectilePrefab;

        [SerializeField]
        private Vector3 visualRotationEuler = Vector3.zero;

        [SerializeField, Min(0.01f)]
        private float visualScale = 1f;

        [SerializeField]
        private Vector3 spinDegreesPerSecond = new Vector3(310f, 190f, 240f);

        [SerializeField, Min(0f)]
        private float arcHeightPerMeter = 0.2f;

        [SerializeField, Min(0f)]
        private float minimumArcHeight = 0.8f;

        [SerializeField, Min(0f)]
        private float maximumArcHeight = 3f;

        [SerializeField, Min(0f)]
        private float aimPreviewHeight = 0.035f;

        [SerializeField, Min(0.001f)]
        private float uncertaintyRingWidth = 0.035f;

        [SerializeField, Min(0.001f)]
        private float blastRingWidth = 0.018f;

        [SerializeField, ColorUsage(true, true)]
        private Color uncertaintyColor = new Color(1.5f, 0.48f, 0.02f, 1f);

        [SerializeField, ColorUsage(true, true)]
        private Color blastColor = new Color(1f, 0.25f, 0.08f, 0.8f);

        [SerializeField]
        private GameObject impactEffectPrefab;

        [SerializeField]
        private Vector3 impactRotationEuler = Vector3.zero;

        [SerializeField, Min(0f)]
        private float impactScalePerBlastRadius = 0.2f;

        [SerializeField, Min(0.01f)]
        private float impactEffectSeconds =
            GameplayThrownExplosivePresentationTiming
                .DefaultImpactEffectSeconds;

        [SerializeField]
        private GameObject persistentAreaEffectPrefab;

        [SerializeField, Min(0f)]
        private float persistentEffectScalePerRadius;

        [SerializeField, Range(0f, 2f)]
        private float persistentParticleEmissionMultiplier = 0.4f;

        [SerializeField, Min(0.01f)]
        private float persistentEffectFadeSeconds = 1.25f;

        [SerializeField]
        private bool hideParticlesWhenCameraInside = true;

        [SerializeField]
        private Color insideOverlayColor = new Color(
            0.16f,
            0.2f,
            0.24f,
            1f);

        [SerializeField, Range(0f, 0.5f)]
        private float insideOverlayMaximumAlpha = 0.12f;

        [SerializeField]
        private bool persistentParticlesCastShadows;

        [SerializeField]
        private bool persistentParticlesReceiveShadows = true;

        public ThrownExplosivePresentationDefinition(
            string inventoryItemId,
            GameObject visualPrefab,
            Vector3 localRotationEuler,
            float modelScale,
            Vector3 spinEulerPerSecond,
            float arcScalePerMeter,
            float minimumArc,
            float maximumArc,
            float previewHeight,
            float uncertaintyWidth,
            float blastWidth,
            Color previewUncertaintyColor,
            Color previewBlastColor,
            GameObject impactPrefab,
            Vector3 impactEuler,
            float impactScalePerRadius,
            float impactSeconds,
            GameObject persistentPrefab = null,
            float persistentScalePerRadius = 0f,
            float persistentEmissionMultiplier = 0.4f,
            float persistentFadeSeconds = 1.25f,
            bool hideParticlesInside = true,
            Color? cameraInsideColor = null,
            float cameraInsideMaximumAlpha = 0.12f,
            bool persistentCastShadows = false,
            bool persistentReceiveShadows = true)
        {
            itemId = inventoryItemId ?? string.Empty;
            projectilePrefab = visualPrefab;
            visualRotationEuler = localRotationEuler;
            visualScale = modelScale;
            spinDegreesPerSecond = spinEulerPerSecond;
            arcHeightPerMeter = arcScalePerMeter;
            minimumArcHeight = minimumArc;
            maximumArcHeight = maximumArc;
            aimPreviewHeight = previewHeight;
            uncertaintyRingWidth = uncertaintyWidth;
            blastRingWidth = blastWidth;
            uncertaintyColor = previewUncertaintyColor;
            blastColor = previewBlastColor;
            impactEffectPrefab = impactPrefab;
            impactRotationEuler = impactEuler;
            impactScalePerBlastRadius = impactScalePerRadius;
            impactEffectSeconds = impactSeconds;
            persistentAreaEffectPrefab = persistentPrefab;
            persistentEffectScalePerRadius = persistentScalePerRadius;
            persistentParticleEmissionMultiplier =
                persistentEmissionMultiplier;
            persistentEffectFadeSeconds = persistentFadeSeconds;
            hideParticlesWhenCameraInside = hideParticlesInside;
            insideOverlayColor = cameraInsideColor
                ?? new Color(0.16f, 0.2f, 0.24f, 1f);
            insideOverlayMaximumAlpha = cameraInsideMaximumAlpha;
            persistentParticlesCastShadows = persistentCastShadows;
            persistentParticlesReceiveShadows = persistentReceiveShadows;
        }

        public string ItemId => itemId;

        public GameObject ProjectilePrefab => projectilePrefab;

        public Quaternion VisualRotation => Quaternion.Euler(visualRotationEuler);

        public float VisualScale => Mathf.Max(0.01f, visualScale);

        public Vector3 SpinDegreesPerSecond => spinDegreesPerSecond;

        public float ArcHeightPerMeter => Mathf.Max(0f, arcHeightPerMeter);

        public float MinimumArcHeight => Mathf.Max(0f, minimumArcHeight);

        public float MaximumArcHeight => Mathf.Max(
            MinimumArcHeight,
            maximumArcHeight);

        public float AimPreviewHeight => Mathf.Max(0f, aimPreviewHeight);

        public float UncertaintyRingWidth =>
            Mathf.Max(0.001f, uncertaintyRingWidth);

        public float BlastRingWidth => Mathf.Max(0.001f, blastRingWidth);

        public Color UncertaintyColor => uncertaintyColor;

        public Color BlastColor => blastColor;

        public GameObject ImpactEffectPrefab => impactEffectPrefab;

        public Quaternion ImpactRotation => Quaternion.Euler(impactRotationEuler);

        public float ImpactScalePerBlastRadius =>
            Mathf.Max(0f, impactScalePerBlastRadius);

        public float ImpactEffectSeconds => Mathf.Max(0.01f, impactEffectSeconds);

        public GameObject PersistentAreaEffectPrefab =>
            persistentAreaEffectPrefab;

        public float PersistentEffectScalePerRadius =>
            Mathf.Max(0f, persistentEffectScalePerRadius);

        public float PersistentParticleEmissionMultiplier =>
            Mathf.Clamp(persistentParticleEmissionMultiplier, 0f, 2f);

        public float PersistentEffectFadeSeconds =>
            Mathf.Max(0.01f, persistentEffectFadeSeconds);

        public bool HideParticlesWhenCameraInside =>
            hideParticlesWhenCameraInside;

        public Color InsideOverlayColor => insideOverlayColor;

        public float InsideOverlayMaximumAlpha =>
            Mathf.Clamp(insideOverlayMaximumAlpha, 0f, 0.5f);

        public bool PersistentParticlesCastShadows =>
            persistentParticlesCastShadows;

        public bool PersistentParticlesReceiveShadows =>
            persistentParticlesReceiveShadows;
    }

    [CreateAssetMenu(
        fileName = "ConsumablePresentationCatalog",
        menuName = "Grit Gud/Consumable Presentation Catalog")]
    public sealed class ConsumablePresentationCatalog : ScriptableObject
    {
        internal const string DefaultResourceName =
            "Gameplay/ConsumablePresentationCatalog";

        [SerializeField]
        private ThrownExplosivePresentationDefinition[] thrownExplosives =
            Array.Empty<ThrownExplosivePresentationDefinition>();

        private Dictionary<string, ThrownExplosivePresentationDefinition>
            thrownExplosiveIndex;

        public static ConsumablePresentationCatalog LoadDefault()
        {
            ConsumablePresentationCatalog catalog =
                Resources.Load<ConsumablePresentationCatalog>(
                    DefaultResourceName);
            if (catalog == null)
            {
                throw new InvalidOperationException(
                    $"The Resources catalog '{DefaultResourceName}' could not be loaded.");
            }

            return catalog;
        }

        public ThrownExplosivePresentationDefinition GetThrownExplosive(
            string itemId)
        {
            EnsureIndex();
            if (!thrownExplosiveIndex.TryGetValue(
                    itemId ?? string.Empty,
                    out ThrownExplosivePresentationDefinition definition))
            {
                throw new KeyNotFoundException(
                    $"Thrown-explosive presentation '{itemId}' is not defined.");
            }

            return definition;
        }

        internal static ConsumablePresentationCatalog CreateRuntime(
            params ThrownExplosivePresentationDefinition[] definitions)
        {
            var catalog = CreateInstance<ConsumablePresentationCatalog>();
            catalog.thrownExplosives = definitions
                ?? Array.Empty<ThrownExplosivePresentationDefinition>();
            return catalog;
        }

        private void OnEnable()
        {
            thrownExplosiveIndex = null;
        }

        private void EnsureIndex()
        {
            if (thrownExplosiveIndex != null)
            {
                return;
            }

            thrownExplosiveIndex = new Dictionary<
                string,
                ThrownExplosivePresentationDefinition>(StringComparer.Ordinal);
            foreach (ThrownExplosivePresentationDefinition entry
                in thrownExplosives)
            {
                if (entry == null)
                {
                    throw new InvalidOperationException(
                        "Thrown-explosive presentation entries cannot be null.");
                }
                if (string.IsNullOrWhiteSpace(entry.ItemId))
                {
                    throw new InvalidOperationException(
                        "Thrown-explosive presentations require an inventory item identifier.");
                }
                if (entry.ProjectilePrefab == null)
                {
                    throw new InvalidOperationException(
                        $"Thrown-explosive presentation '{entry.ItemId}' requires a projectile prefab.");
                }
                if (entry.ImpactEffectPrefab == null
                    && entry.PersistentAreaEffectPrefab == null)
                {
                    throw new InvalidOperationException(
                        $"Thrown-explosive presentation '{entry.ItemId}' requires an impact or persistent-area effect prefab.");
                }
                if (entry.PersistentAreaEffectPrefab != null
                    && entry.PersistentEffectScalePerRadius <= 0f)
                    throw new InvalidOperationException(
                        $"Thrown-explosive presentation '{entry.ItemId}' persistent effect requires a positive radius scale.");
                if (!thrownExplosiveIndex.TryAdd(entry.ItemId, entry))
                {
                    throw new InvalidOperationException(
                        $"Thrown-explosive presentation '{entry.ItemId}' is duplicated.");
                }
            }
        }
    }
}
