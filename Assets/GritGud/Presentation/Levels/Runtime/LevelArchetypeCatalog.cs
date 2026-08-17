using System;
using System.Collections.Generic;
using GritGud.Domain.Levels;
using UnityEngine;

namespace GritGud.Presentation.Levels.Runtime
{
    [Flags]
    public enum LevelArchetypeCapabilities
    {
        None = 0,
        PlacementSurface = 1 << 0,
        Cover = 1 << 1,
        Destructible = 1 << 2,
        Vehicle = 1 << 3,
    }

    public readonly struct LevelArchetypePlacementRules
    {
        public LevelArchetypePlacementRules(float positionSnap, float angleSnap)
        {
            PositionSnap = Mathf.Max(0.01f, positionSnap);
            AngleSnap = Mathf.Max(1f, angleSnap);
        }

        public float PositionSnap { get; }

        public float AngleSnap { get; }
    }

    public readonly struct LevelArchetypePresentation
    {
        public LevelArchetypePresentation(GameObject prefab, Bounds localBounds)
        {
            Prefab = prefab;
            LocalBounds = localBounds;
        }

        public GameObject Prefab { get; }

        public Bounds LocalBounds { get; }
    }

    public readonly struct LevelArchetypeGameplayDefaults
    {
        private readonly bool providesCover;
        private readonly Float3Data coverCenter;
        private readonly Float3Data coverSize;
        private readonly bool destructible;
        private readonly string destructibleState;
        private readonly float integrity;

        public LevelArchetypeGameplayDefaults(
            bool providesCover,
            Float3Data coverCenter,
            Float3Data coverSize,
            bool destructible,
            string destructibleState,
            float integrity)
        {
            this.providesCover = providesCover;
            this.coverCenter = coverCenter;
            this.coverSize = coverSize;
            this.destructible = destructible;
            this.destructibleState = destructibleState;
            this.integrity = integrity;
        }

        public void ApplyTo(LevelEntity entity)
        {
            if (destructible)
            {
                entity.destructible = new DestructibleInstanceData
                {
                    enabled = true,
                    initialState = destructibleState,
                    integrity = integrity,
                };
            }
        }
    }

    [Serializable]
    public sealed class LevelArchetypeDefinition
    {
        [SerializeField] private string archetypeId = string.Empty;
        [SerializeField] private string displayName = string.Empty;
        [SerializeField] private string category = string.Empty;
        [SerializeField] private string surfacePresentationId =
            GritGud.Presentation.Gameplay.SurfacePresentationCatalog.DefaultSurfaceId;
        [SerializeField] private GameObject prefab;
        [SerializeField] private Vector3 localBoundsCenter;
        [SerializeField] private Vector3 localBoundsSize = Vector3.one;
        [SerializeField] private float positionSnap = 0.25f;
        [SerializeField] private float angleSnap = 15f;
        [SerializeField] private bool placementSurface;
        [SerializeField] private bool providesCover;
        [SerializeField] private Vector3 defaultCoverCenter;
        [SerializeField] private Vector3 defaultCoverSize = Vector3.one;
        [SerializeField] private bool destructible;
        [SerializeField] private bool vehicle;
        [SerializeField] private string initialDestructibleState = "intact";
        [SerializeField] private float initialIntegrity = 10f;
        [SerializeField] private GritGud.Presentation.Gameplay.DestructibleFractureProfile
            fractureProfile;

        public string ArchetypeId => archetypeId;

        public string DisplayName => displayName;

        public string Category => category;

        public string SurfacePresentationId =>
            string.IsNullOrWhiteSpace(surfacePresentationId)
                ? GritGud.Presentation.Gameplay.SurfacePresentationCatalog.DefaultSurfaceId
                : surfacePresentationId;

        public GameObject Prefab => prefab;

        public GritGud.Presentation.Gameplay.DestructibleFractureProfile
            FractureProfile => fractureProfile;

        public Bounds LocalBounds => new Bounds(localBoundsCenter, localBoundsSize);

        public float PositionSnap => Mathf.Max(0.01f, positionSnap);

        public float AngleSnap => Mathf.Max(1f, angleSnap);

        public bool IsPlacementSurface => placementSurface;

        public LevelArchetypePlacementRules PlacementRules =>
            new LevelArchetypePlacementRules(positionSnap, angleSnap);

        public LevelArchetypePresentation Presentation =>
            new LevelArchetypePresentation(prefab, new Bounds(localBoundsCenter, localBoundsSize));

        public LevelArchetypeCapabilities Capabilities =>
            (placementSurface ? LevelArchetypeCapabilities.PlacementSurface : LevelArchetypeCapabilities.None)
            | (providesCover ? LevelArchetypeCapabilities.Cover : LevelArchetypeCapabilities.None)
            | (destructible ? LevelArchetypeCapabilities.Destructible : LevelArchetypeCapabilities.None)
            | (vehicle ? LevelArchetypeCapabilities.Vehicle : LevelArchetypeCapabilities.None);

        public LevelArchetypeGameplayDefaults GameplayDefaults => new LevelArchetypeGameplayDefaults(
            providesCover,
            ToData(defaultCoverCenter),
            ToData(defaultCoverSize),
            destructible,
            initialDestructibleState,
            initialIntegrity);

        public LevelEntity CreateEntity(Vector3 position, float yawDegrees)
        {
            var entity = new LevelEntity
            {
                archetypeId = archetypeId,
                transform = new LevelTransformData(
                    new Float3Data(position.x, position.y, position.z),
                    yawDegrees),
            };

            GameplayDefaults.ApplyTo(entity);

            return entity;
        }

        private static Float3Data ToData(Vector3 value)
        {
            return new Float3Data(value.x, value.y, value.z);
        }
    }

    [CreateAssetMenu(fileName = "LevelArchetypeCatalog", menuName = "Grit Gud/Level Archetype Catalog")]
    public sealed class LevelArchetypeCatalog : ScriptableObject
    {
        public const string DefaultResourceName = "DefaultLevelArchetypeCatalog";

        [SerializeField] private LevelArchetypeDefinition[] entries = Array.Empty<LevelArchetypeDefinition>();

        private Dictionary<string, LevelArchetypeDefinition> index;

        public IReadOnlyList<LevelArchetypeDefinition> Entries => entries;

        public static LevelArchetypeCatalog LoadDefault()
        {
            LevelArchetypeCatalog catalog = Resources.Load<LevelArchetypeCatalog>(DefaultResourceName);
            if (catalog == null)
            {
                throw new InvalidOperationException(
                    $"The Resources catalog '{DefaultResourceName}' could not be loaded.");
            }

            return catalog;
        }

        public bool TryGet(string archetypeId, out LevelArchetypeDefinition definition)
        {
            EnsureIndex();
            return index.TryGetValue(archetypeId ?? string.Empty, out definition);
        }

        public ISet<string> CreateKnownIdSet()
        {
            EnsureIndex();
            return new HashSet<string>(index.Keys, StringComparer.Ordinal);
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

            index = new Dictionary<string, LevelArchetypeDefinition>(StringComparer.Ordinal);
            foreach (LevelArchetypeDefinition entry in entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.ArchetypeId))
                {
                    continue;
                }

                if (!index.TryAdd(entry.ArchetypeId, entry))
                {
                    throw new InvalidOperationException(
                        $"The archetype ID '{entry.ArchetypeId}' occurs more than once in the catalog.");
                }
            }
        }
    }
}
