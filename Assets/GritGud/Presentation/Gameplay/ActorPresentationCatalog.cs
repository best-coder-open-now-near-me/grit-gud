using System;
using System.Collections.Generic;
using GritGud.Presentation.Actors;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    public static class ActorPresentationIds
    {
        public const string DefaultPlayer = "actor.player.default";
        public const string RiflemanEnemy = "actor.enemy.rifleman";
    }

    [Serializable]
    public sealed class ActorPresentationDefinition
    {
        [SerializeField]
        private string presentationId = string.Empty;

        [SerializeField]
        private GameObject prefab;

        [SerializeField]
        [Tooltip(
            "Whether exploration movement input starts enabled for this "
            + "presentation. Party selection may override it later.")]
        private bool movementInputEnabled;

        public ActorPresentationDefinition(
            string actorPresentationId,
            GameObject actorPrefab,
            bool enableMovementInput)
        {
            presentationId = actorPresentationId?.Trim() ?? string.Empty;
            prefab = actorPrefab;
            movementInputEnabled = enableMovementInput;
        }

        public string PresentationId =>
            presentationId?.Trim() ?? string.Empty;

        public GameObject Prefab => prefab;

        public bool MovementInputEnabled => movementInputEnabled;
    }

    [CreateAssetMenu(
        fileName = "ActorPresentationCatalog",
        menuName = "Grit Gud/Actor Presentation Catalog")]
    public sealed class ActorPresentationCatalog : ScriptableObject
    {
        internal const string DefaultResourceName =
            "Gameplay/ActorPresentationCatalog";

        [SerializeField]
        private ActorPresentationDefinition[] entries =
            Array.Empty<ActorPresentationDefinition>();

        private Dictionary<string, ActorPresentationDefinition> index;

        public static ActorPresentationCatalog LoadDefault()
        {
            ActorPresentationCatalog catalog =
                Resources.Load<ActorPresentationCatalog>(DefaultResourceName);
            if (catalog == null)
            {
                throw new InvalidOperationException(
                    $"The Resources catalog '{DefaultResourceName}' could "
                    + "not be loaded.");
            }

            catalog.Validate();
            return catalog;
        }

        public ActorPresentationDefinition Get(string presentationId)
        {
            EnsureIndex();
            if (!index.TryGetValue(
                    presentationId ?? string.Empty,
                    out ActorPresentationDefinition definition))
            {
                throw new KeyNotFoundException(
                    $"Actor presentation '{presentationId}' is not defined.");
            }

            return definition;
        }

        public void Validate() => EnsureIndex();

        internal static ActorPresentationCatalog CreateRuntime(
            params ActorPresentationDefinition[] definitions)
        {
            var catalog = CreateInstance<ActorPresentationCatalog>();
            catalog.entries = definitions
                ?? Array.Empty<ActorPresentationDefinition>();
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

            index = new Dictionary<string, ActorPresentationDefinition>(
                StringComparer.Ordinal);
            foreach (ActorPresentationDefinition entry in entries)
            {
                if (entry == null ||
                    string.IsNullOrWhiteSpace(entry.PresentationId) ||
                    entry.Prefab == null)
                {
                    throw new InvalidOperationException(
                        "Actor presentation entries require an ID and prefab.");
                }

                if (entry.Prefab.GetComponent<ThirdPersonMotor>() == null ||
                    entry.Prefab.GetComponent<ExplorationMovementInput>() ==
                        null ||
                    entry.Prefab.GetComponent<ActorStancePresenter>() == null ||
                    entry.Prefab.GetComponent<ActorCelShadingPresenter>() ==
                        null)
                {
                    throw new InvalidOperationException(
                        $"Actor presentation '{entry.PresentationId}' prefab "
                        + $"must contain {nameof(ThirdPersonMotor)}, "
                        + $"{nameof(ExplorationMovementInput)}, "
                        + $"{nameof(ActorStancePresenter)}, and "
                        + $"{nameof(ActorCelShadingPresenter)} components.");
                }

                if (entry.Prefab.GetComponent<ThirdPersonMotor>()
                        .MotionProfile == null)
                {
                    throw new InvalidOperationException(
                        $"Actor presentation '{entry.PresentationId}' prefab "
                        + $"requires an authored {nameof(ActorMotionProfile)}.");
                }

                if (!index.TryAdd(entry.PresentationId, entry))
                {
                    throw new InvalidOperationException(
                        $"Actor presentation '{entry.PresentationId}' is "
                        + "duplicated.");
                }
            }
        }
    }
}
