using System;
using System.Collections.Generic;
using GritGud.Application.Gameplay;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    [Serializable]
    public sealed class ProjectilePresentationDefinition
    {
        [SerializeField]
        private string projectileId = string.Empty;

        [SerializeField]
        private GameObject prefab;

        [SerializeField]
        private Vector3 visualRotationEuler = Vector3.zero;

        [SerializeField, Min(0.01f)]
        private float visualScale = 1f;

        [SerializeField, Min(0f)]
        private float spinDegreesPerSecond = 360f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip(
            "Fraction of each playback spent accelerating before reaching "
            + "constant travel speed.")]
        private float playbackAccelerationFraction =
            GameplayProjectilePresentationSampler.DefaultAccelerationFraction;

        [SerializeField]
        private GameObject trailEffectPrefab;

        [SerializeField]
        private Vector3 trailLocalPosition = new Vector3(0f, 0f, -0.22f);

        [SerializeField]
        private Vector3 trailLocalRotationEuler = new Vector3(0f, 180f, 0f);

        [SerializeField, Min(0.01f)]
        private float trailScale = 1f;

        [SerializeField]
        private bool emitsTrailWhileHolding = true;

        [SerializeField]
        private GameObject impactEffectPrefab;

        [SerializeField, Min(0f)]
        private float impactScalePerBlastRadius = 0.2f;

        [SerializeField, Min(0.01f)]
        private float impactEffectSeconds = 0.65f;

        [SerializeField, Min(0f)]
        private float ghostEndpointHoldSeconds = 0.45f;

        [SerializeField, Min(0.01f)]
        private float encounterPlaybackSeconds = 0.45f;

        public ProjectilePresentationDefinition(
            string id,
            GameObject modelPrefab,
            Vector3 modelRotationEuler,
            float modelScale,
            float spinSpeed,
            float accelerationFraction = GameplayProjectilePresentationSampler
                .DefaultAccelerationFraction,
            GameObject trailPrefab = null,
            Vector3? trailPosition = null,
            Vector3? trailRotationEuler = null,
            float trailVisualScale = 1f,
            bool trailWhileHolding = true,
            GameObject impactPrefab = null,
            float impactScalePerRadius = 0.2f,
            float impactSeconds = 0.65f,
            float ghostHoldSeconds = 0.45f,
            float encounterSeconds = 0.45f)
        {
            projectileId = id ?? string.Empty;
            prefab = modelPrefab;
            visualRotationEuler = modelRotationEuler;
            visualScale = modelScale;
            spinDegreesPerSecond = spinSpeed;
            playbackAccelerationFraction = Mathf.Clamp01(accelerationFraction);
            trailEffectPrefab = trailPrefab;
            trailLocalPosition = trailPosition ?? new Vector3(0f, 0f, -0.22f);
            trailLocalRotationEuler = trailRotationEuler
                ?? new Vector3(0f, 180f, 0f);
            trailScale = Mathf.Max(0.01f, trailVisualScale);
            emitsTrailWhileHolding = trailWhileHolding;
            impactEffectPrefab = impactPrefab;
            impactScalePerBlastRadius = Mathf.Max(0f, impactScalePerRadius);
            impactEffectSeconds = Mathf.Max(0.01f, impactSeconds);
            ghostEndpointHoldSeconds = Mathf.Max(0f, ghostHoldSeconds);
            encounterPlaybackSeconds = Mathf.Max(0.01f, encounterSeconds);
        }

        public string ProjectileId => projectileId;

        public GameObject Prefab => prefab;

        public Quaternion VisualRotation => Quaternion.Euler(visualRotationEuler);

        public float VisualScale => Mathf.Max(0.01f, visualScale);

        public float SpinDegreesPerSecond => Mathf.Max(0f, spinDegreesPerSecond);

        public float PlaybackAccelerationFraction =>
            Mathf.Clamp01(playbackAccelerationFraction);

        public GameObject TrailEffectPrefab => trailEffectPrefab;

        public Vector3 TrailLocalPosition => trailLocalPosition;

        public Quaternion TrailLocalRotation =>
            Quaternion.Euler(trailLocalRotationEuler);

        public float TrailScale => Mathf.Max(0.01f, trailScale);

        public bool EmitsTrailWhileHolding => emitsTrailWhileHolding;

        public GameObject ImpactEffectPrefab => impactEffectPrefab;

        public float ImpactScalePerBlastRadius =>
            Mathf.Max(0f, impactScalePerBlastRadius);

        public float ImpactEffectSeconds =>
            Mathf.Max(0.01f, impactEffectSeconds);

        public float GhostEndpointHoldSeconds =>
            Mathf.Max(0f, ghostEndpointHoldSeconds);

        public float EncounterPlaybackSeconds =>
            Mathf.Max(0.01f, encounterPlaybackSeconds);
    }

    [CreateAssetMenu(
        fileName = "ProjectilePresentationCatalog",
        menuName = "Grit Gud/Projectile Presentation Catalog")]
    public sealed class ProjectilePresentationCatalog : ScriptableObject
    {
        internal const string DefaultResourceName =
            "Gameplay/ProjectilePresentationCatalog";

        [SerializeField]
        private ProjectilePresentationDefinition[] entries =
            Array.Empty<ProjectilePresentationDefinition>();

        private Dictionary<string, ProjectilePresentationDefinition> index;

        public static ProjectilePresentationCatalog LoadDefault()
        {
            ProjectilePresentationCatalog catalog =
                Resources.Load<ProjectilePresentationCatalog>(
                    DefaultResourceName);
            if (catalog == null)
            {
                throw new InvalidOperationException(
                    $"The Resources catalog '{DefaultResourceName}' could not be loaded.");
            }

            return catalog;
        }

        public ProjectilePresentationDefinition Get(string projectileId)
        {
            EnsureIndex();
            if (!index.TryGetValue(
                    projectileId ?? string.Empty,
                    out ProjectilePresentationDefinition definition))
            {
                throw new KeyNotFoundException(
                    $"Projectile presentation '{projectileId}' is not defined.");
            }

            return definition;
        }

        internal static ProjectilePresentationCatalog CreateRuntime(
            params ProjectilePresentationDefinition[] definitions)
        {
            var catalog = CreateInstance<ProjectilePresentationCatalog>();
            catalog.entries = definitions ??
                Array.Empty<ProjectilePresentationDefinition>();
            return catalog;
        }

        private void OnEnable()
        {
            index = null;
        }

        private void EnsureIndex()
        {
            if (index != null)
            {
                return;
            }

            index = new Dictionary<string, ProjectilePresentationDefinition>(
                StringComparer.Ordinal);
            foreach (ProjectilePresentationDefinition entry in entries)
            {
                if (entry == null
                    || string.IsNullOrWhiteSpace(entry.ProjectileId)
                    || entry.Prefab == null)
                {
                    continue;
                }

                if (!index.TryAdd(entry.ProjectileId, entry))
                {
                    throw new InvalidOperationException(
                        $"Projectile presentation '{entry.ProjectileId}' is duplicated.");
                }
            }
        }
    }
}
