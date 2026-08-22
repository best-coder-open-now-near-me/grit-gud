using System;
using System.Collections.Generic;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    [Serializable]
    public sealed class DronePresentationDefinition
    {
        [SerializeField]
        private string presentationId = string.Empty;

        [SerializeField]
        private GameObject prefab;

        public DronePresentationDefinition(
            string id,
            GameObject presentationPrefab = null)
        {
            presentationId = id?.Trim() ?? string.Empty;
            prefab = presentationPrefab;
        }

        public string PresentationId =>
            presentationId?.Trim() ?? string.Empty;

        public GameObject Prefab => prefab;
    }

    [CreateAssetMenu(
        fileName = "DronePresentationCatalog",
        menuName = "Grit Gud/Drone Presentation Catalog")]
    public sealed class DronePresentationCatalog : ScriptableObject
    {
        internal const string DefaultResourceName =
            "Gameplay/DronePresentationCatalog";

        [SerializeField]
        private DronePresentationDefinition[] entries =
            Array.Empty<DronePresentationDefinition>();

        private Dictionary<string, DronePresentationDefinition> index;

        public static DronePresentationCatalog LoadDefault()
        {
            DronePresentationCatalog catalog =
                Resources.Load<DronePresentationCatalog>(
                    DefaultResourceName);
            if (catalog == null)
                throw new InvalidOperationException(
                    $"The Resources catalog '{DefaultResourceName}' could not be loaded.");
            catalog.Validate();
            return catalog;
        }

        public DronePresentationDefinition Get(string presentationId)
        {
            EnsureIndex();
            if (!index.TryGetValue(
                    presentationId ?? string.Empty,
                    out DronePresentationDefinition definition))
                throw new KeyNotFoundException(
                    $"Drone presentation '{presentationId}' is not defined.");
            return definition;
        }

        public void Validate() => EnsureIndex();

        internal static DronePresentationCatalog CreateRuntime(
            params DronePresentationDefinition[] definitions)
        {
            var catalog = CreateInstance<DronePresentationCatalog>();
            catalog.entries = definitions
                ?? Array.Empty<DronePresentationDefinition>();
            return catalog;
        }

        private void OnEnable() => index = null;

        private void EnsureIndex()
        {
            if (index != null) return;
            index = new Dictionary<string, DronePresentationDefinition>(
                StringComparer.Ordinal);
            foreach (DronePresentationDefinition entry in entries)
            {
                if (entry == null
                    || string.IsNullOrWhiteSpace(entry.PresentationId)
                    || !index.TryAdd(entry.PresentationId, entry))
                    throw new InvalidOperationException(
                        "Drone presentation entries require unique non-empty IDs.");
            }
        }
    }
}
