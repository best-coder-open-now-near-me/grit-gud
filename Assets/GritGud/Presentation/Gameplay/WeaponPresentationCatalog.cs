using System;
using System.Collections.Generic;
using GritGud.Presentation.Actors.Animation;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    public static class GameplayCloseQuartersPresentationTiming
    {
        public const float ContactStrikeSeconds = 0.8f;
        public const float ContactImpactNormalizedTime = 0.4f;
    }

    public enum WeaponAttackPresentationKind
    {
        Firearm = 0,
        ContactStrike = 1,
    }

    [Serializable]
    public sealed class WeaponPresentationDefinition
    {
        [SerializeField]
        private string itemId = string.Empty;

        [SerializeField]
        [Tooltip(
            "Prefab whose root carries a WeaponRigSocketSet. The prefab root "
            + "is mounted directly to the actor's right hand.")]
        private GameObject prefab;

        [SerializeField]
        [Tooltip(
            "Stable animation-set identifier resolved by the actor's animation profile.")]
        private string animationSetId = string.Empty;

        [SerializeField]
        private GameObject muzzleEffectPrefab;

        [SerializeField]
        private bool instantTracer;

        [SerializeField, Min(0.01f)]
        private float shotEffectSeconds = 0.18f;

        [SerializeField, Min(0.001f)]
        private float tracerWidth = 0.025f;

        [SerializeField, ColorUsage(true, true)]
        private Color muzzleLightColor = new Color(1.4f, 0.5f, 0.08f, 1f);

        [SerializeField, Min(0f)]
        private float muzzleLightIntensity = 3f;

        [SerializeField, Min(0.01f)]
        private float muzzleLightRange = 3f;

        [SerializeField, Min(0.01f)]
        private float muzzleLightSeconds = 0.06f;

        [SerializeField, Min(0f)]
        private float impactEffectScaleMultiplier = 1f;

        [SerializeField, Min(0f)]
        private float impactEffectWidthMultiplier = 1f;

        [SerializeField, Range(1f, 90f)]
        private float maximumAimCorrectionDegrees = 55f;

        [SerializeField]
        private WeaponAttackPresentationKind attackPresentation;

        [SerializeField, Min(0.05f)]
        private float contactStrikeSeconds = 0.34f;

        [SerializeField, Range(0f, 1f)]
        private float contactImpactNormalizedTime =
            GameplayCloseQuartersPresentationTiming
                .ContactImpactNormalizedTime;

        public WeaponPresentationDefinition(
            string inventoryItemId,
            GameObject weaponRigPrefab,
            string actorAnimationSetId,
            GameObject muzzleFxPrefab,
            bool drawsInstantTracer,
            float effectSeconds,
            float lineWidth,
            float maxAimCorrectionDegrees = 55f,
            WeaponAttackPresentationKind attackPresentationKind =
                WeaponAttackPresentationKind.Firearm,
            float contactDurationSeconds = 0.34f,
            Color? shotLightColor = null,
            float shotLightIntensity = 3f,
            float shotLightRange = 3f,
            float shotLightSeconds = 0.06f,
            float contactImpactTime =
                GameplayCloseQuartersPresentationTiming
                    .ContactImpactNormalizedTime,
            float impactScaleMultiplier = 1f,
            float impactWidthMultiplier = 1f)
        {
            itemId = inventoryItemId ?? string.Empty;
            prefab = weaponRigPrefab;
            animationSetId = actorAnimationSetId?.Trim() ?? string.Empty;
            muzzleEffectPrefab = muzzleFxPrefab;
            instantTracer = drawsInstantTracer;
            shotEffectSeconds = effectSeconds;
            tracerWidth = lineWidth;
            maximumAimCorrectionDegrees = maxAimCorrectionDegrees;
            attackPresentation = attackPresentationKind;
            contactStrikeSeconds = contactDurationSeconds;
            contactImpactNormalizedTime = contactImpactTime;
            muzzleLightColor = shotLightColor
                ?? new Color(1.4f, 0.5f, 0.08f, 1f);
            muzzleLightIntensity = Mathf.Max(0f, shotLightIntensity);
            muzzleLightRange = Mathf.Max(0.01f, shotLightRange);
            muzzleLightSeconds = Mathf.Max(0.01f, shotLightSeconds);
            impactEffectScaleMultiplier = Mathf.Max(0f, impactScaleMultiplier);
            impactEffectWidthMultiplier = Mathf.Max(0f, impactWidthMultiplier);
        }

        public string ItemId => itemId;

        public GameObject Prefab => prefab;

        public string AnimationSetId => animationSetId?.Trim() ?? string.Empty;

        public WeaponRigSocketSet RigSockets => prefab != null
            ? prefab.GetComponent<WeaponRigSocketSet>()
            : null;

        public GameObject MuzzleEffectPrefab => muzzleEffectPrefab;

        public bool InstantTracer => instantTracer;

        public float ShotEffectSeconds => Mathf.Max(0.01f, shotEffectSeconds);

        public float TracerWidth => Mathf.Max(0.001f, tracerWidth);

        public Color MuzzleLightColor => muzzleLightColor;

        public float MuzzleLightIntensity => Mathf.Max(0f, muzzleLightIntensity);

        public float MuzzleLightRange => Mathf.Max(0.01f, muzzleLightRange);

        public float MuzzleLightSeconds => Mathf.Max(0.01f, muzzleLightSeconds);

        public float ImpactEffectScaleMultiplier =>
            Mathf.Max(0f, impactEffectScaleMultiplier);

        public float ImpactEffectWidthMultiplier =>
            Mathf.Max(0f, impactEffectWidthMultiplier);

        public float MaximumAimCorrectionDegrees =>
            Mathf.Clamp(maximumAimCorrectionDegrees, 1f, 90f);

        public WeaponAttackPresentationKind AttackPresentation =>
            attackPresentation;

        public float ContactStrikeSeconds =>
            Mathf.Max(0.05f, contactStrikeSeconds);

        public float ContactImpactNormalizedTime =>
            Mathf.Clamp01(contactImpactNormalizedTime);

    }

    [CreateAssetMenu(
        fileName = "WeaponPresentationCatalog",
        menuName = "Grit Gud/Weapon Presentation Catalog")]
    public sealed class WeaponPresentationCatalog : ScriptableObject
    {
        internal const string DefaultResourceName =
            "Gameplay/WeaponPresentationCatalog";

        [SerializeField]
        private WeaponPresentationDefinition[] entries =
            Array.Empty<WeaponPresentationDefinition>();

        private Dictionary<string, WeaponPresentationDefinition> index;

        public static WeaponPresentationCatalog LoadDefault()
        {
            WeaponPresentationCatalog catalog =
                Resources.Load<WeaponPresentationCatalog>(DefaultResourceName);
            if (catalog == null)
            {
                throw new InvalidOperationException(
                    $"The Resources catalog '{DefaultResourceName}' could not be loaded.");
            }

            return catalog;
        }

        public WeaponPresentationDefinition Get(string itemId)
        {
            EnsureIndex();
            if (!index.TryGetValue(
                    itemId ?? string.Empty,
                    out WeaponPresentationDefinition definition))
            {
                throw new KeyNotFoundException(
                    $"Weapon presentation '{itemId}' is not defined.");
            }

            return definition;
        }

        public bool TryGet(
            string itemId,
            out WeaponPresentationDefinition definition)
        {
            EnsureIndex();
            return index.TryGetValue(itemId ?? string.Empty, out definition);
        }

        internal static WeaponPresentationCatalog CreateRuntime(
            params WeaponPresentationDefinition[] definitions)
        {
            var catalog = CreateInstance<WeaponPresentationCatalog>();
            catalog.entries = definitions ??
                Array.Empty<WeaponPresentationDefinition>();
            return catalog;
        }

        private void OnEnable()
        {
            index = null;
        }

        private void OnValidate()
        {
            index = null;
        }

        private void EnsureIndex()
        {
            if (index != null)
            {
                return;
            }

            index = new Dictionary<string, WeaponPresentationDefinition>(
                StringComparer.Ordinal);
            foreach (WeaponPresentationDefinition entry in entries)
            {
                if (entry == null)
                {
                    throw new InvalidOperationException(
                        "Weapon presentation catalogs cannot contain null entries.");
                }

                if (string.IsNullOrWhiteSpace(entry.ItemId) ||
                    entry.Prefab == null ||
                    string.IsNullOrWhiteSpace(entry.AnimationSetId))
                {
                    throw new InvalidOperationException(
                        "Weapon presentation entries require an item ID, "
                        + "prefab, and stable animation-set ID.");
                }

                if (entry.RigSockets == null)
                {
                    throw new InvalidOperationException(
                        $"Weapon presentation '{entry.ItemId}' prefab must carry "
                        + "a WeaponRigSocketSet on its root.");
                }

                entry.RigSockets.Validate(entry.ItemId);

                if (!index.TryAdd(entry.ItemId, entry))
                {
                    throw new InvalidOperationException(
                        $"Weapon presentation '{entry.ItemId}' is duplicated.");
                }
            }
        }
    }
}
