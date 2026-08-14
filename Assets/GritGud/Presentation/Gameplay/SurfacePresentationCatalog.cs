using System;
using System.Collections.Generic;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    [Serializable]
    public sealed class SurfacePresentationDefinition
    {
        [SerializeField] private string surfaceId = string.Empty;
        [SerializeField, Range(0f, 1f)] private float smoothness = 0.15f;
        [SerializeField, Range(0f, 1f)] private float specularStrength = 0.06f;
        [SerializeField] private Color specularColor = Color.white;
        [SerializeField, Range(0f, 1f)] private float edgeSheenStrength;
        [SerializeField] private GameObject impactEffectPrefab;
        [SerializeField] private Vector3 impactRotationEuler;
        [SerializeField, Min(0.01f)] private float impactScale = 0.1f;
        [SerializeField, Min(0.01f)] private float impactLifetimeSeconds = 0.55f;
        [SerializeField] private Color decalColor = new Color(0.01f, 0.015f, 0.02f, 0.6f);
        [SerializeField, Min(0f)] private float decalDiameter = 0.12f;
        [SerializeField, Min(0.01f)] private float decalLifetimeSeconds = 18f;

        public string SurfaceId => surfaceId;
        public float Smoothness => Mathf.Clamp01(smoothness);
        public float SpecularStrength => Mathf.Clamp01(specularStrength);
        public Color SpecularColor => specularColor;
        public float EdgeSheenStrength => Mathf.Clamp01(edgeSheenStrength);
        public GameObject ImpactEffectPrefab => impactEffectPrefab;
        public Quaternion ImpactRotation => Quaternion.Euler(impactRotationEuler);
        public float ImpactScale => Mathf.Max(0.01f, impactScale);
        public float ImpactLifetimeSeconds => Mathf.Max(0.01f, impactLifetimeSeconds);
        public Color DecalColor => decalColor;
        public float DecalDiameter => Mathf.Max(0f, decalDiameter);
        public float DecalLifetimeSeconds => Mathf.Max(0.01f, decalLifetimeSeconds);
    }

    [CreateAssetMenu(
        fileName = "SurfacePresentationCatalog",
        menuName = "Grit Gud/Surface Presentation Catalog")]
    public sealed class SurfacePresentationCatalog : ScriptableObject
    {
        internal const string DefaultResourceName =
            "Gameplay/SurfacePresentationCatalog";
        public const string DefaultSurfaceId = "surface.concrete";
        public const string ActorSurfaceId = "surface.actor";

        [SerializeField] private SurfacePresentationDefinition[] entries =
            Array.Empty<SurfacePresentationDefinition>();
        private Dictionary<string, SurfacePresentationDefinition> index;

        public static SurfacePresentationCatalog LoadDefault()
        {
            SurfacePresentationCatalog catalog =
                Resources.Load<SurfacePresentationCatalog>(DefaultResourceName);
            if (catalog == null)
            {
                throw new InvalidOperationException(
                    $"The Resources surface catalog '{DefaultResourceName}' could not be loaded.");
            }

            return catalog;
        }

        public SurfacePresentationDefinition Get(string surfaceId)
        {
            EnsureIndex();
            string requested = string.IsNullOrWhiteSpace(surfaceId)
                ? DefaultSurfaceId
                : surfaceId;
            if (!index.TryGetValue(requested, out SurfacePresentationDefinition definition))
            {
                throw new KeyNotFoundException(
                    $"Surface presentation '{requested}' is not defined.");
            }

            return definition;
        }

        public bool TryGet(string surfaceId, out SurfacePresentationDefinition definition)
        {
            EnsureIndex();
            return index.TryGetValue(surfaceId ?? string.Empty, out definition);
        }

        private void OnEnable() => index = null;

        private void EnsureIndex()
        {
            if (index != null)
            {
                return;
            }

            index = new Dictionary<string, SurfacePresentationDefinition>(
                StringComparer.Ordinal);
            foreach (SurfacePresentationDefinition entry in entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.SurfaceId))
                {
                    continue;
                }

                if (!index.TryAdd(entry.SurfaceId, entry))
                {
                    throw new InvalidOperationException(
                        $"Surface presentation '{entry.SurfaceId}' is duplicated.");
                }
            }
        }
    }
}
