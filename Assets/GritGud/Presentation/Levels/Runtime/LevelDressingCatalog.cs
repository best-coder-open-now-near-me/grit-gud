using System;
using System.Collections.Generic;
using UnityEngine;

namespace GritGud.Presentation.Levels.Runtime
{
    [Serializable]
    public sealed class AmbientVfxDefinition
    {
        [SerializeField] private string effectId = string.Empty;
        [SerializeField] private string displayName = string.Empty;
        [SerializeField] private GameObject prefab;

        public string EffectId => effectId ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName)
            ? effectId
            : displayName;
        public GameObject Prefab => prefab;
    }

    [CreateAssetMenu(
        fileName = "LevelDressingCatalog",
        menuName = "Grit Gud/Level Dressing Catalog")]
    public sealed class LevelDressingCatalog : ScriptableObject
    {
        internal const string DefaultResourceName = "Gameplay/LevelDressingCatalog";

        [SerializeField] private AmbientVfxDefinition[] ambientEffects =
            Array.Empty<AmbientVfxDefinition>();
        private Dictionary<string, AmbientVfxDefinition> index;

        public IReadOnlyList<AmbientVfxDefinition> AmbientEffects =>
            ambientEffects ?? Array.Empty<AmbientVfxDefinition>();

        public static LevelDressingCatalog LoadDefault()
        {
            LevelDressingCatalog catalog = Resources.Load<LevelDressingCatalog>(
                DefaultResourceName);
            if (catalog == null)
            {
                throw new InvalidOperationException(
                    $"The Resources dressing catalog '{DefaultResourceName}' could not be loaded.");
            }
            return catalog;
        }

        public bool TryGetAmbientEffect(string effectId, out AmbientVfxDefinition definition)
        {
            EnsureIndex();
            return index.TryGetValue(effectId ?? string.Empty, out definition);
        }

        private void OnEnable() => index = null;

        private void EnsureIndex()
        {
            if (index != null)
                return;
            index = new Dictionary<string, AmbientVfxDefinition>(StringComparer.Ordinal);
            foreach (AmbientVfxDefinition definition in ambientEffects)
            {
                if (definition == null || string.IsNullOrWhiteSpace(definition.EffectId))
                    continue;
                if (!index.TryAdd(definition.EffectId, definition))
                {
                    throw new InvalidOperationException(
                        $"Ambient VFX definition '{definition.EffectId}' is duplicated.");
                }
            }
        }
    }
}
