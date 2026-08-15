using System;
using System.Collections.Generic;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
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
        [SerializeField] private AmbientEffectPlacementDefinition[] ambientEffects =
            Array.Empty<AmbientEffectPlacementDefinition>();

        public string LevelId => levelId;
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
