using System;
using System.Collections.Generic;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    [Serializable]
    public sealed class EnemyPresentationDefinition
    {
        [SerializeField]
        private string presentationId = string.Empty;

        [SerializeField, Min(0f)]
        private float postDecisionDelaySeconds = 0.15f;

        [SerializeField, Min(0f)]
        private float postAttackDelaySeconds = 0.7f;

        [SerializeField]
        private Vector3 incapacitationLocalRotationEuler =
            new Vector3(0f, 0f, 78f);

        [SerializeField]
        private Vector3 incapacitationLocalOffset =
            new Vector3(0f, 0.15f, 0f);

        public EnemyPresentationDefinition(
            string actorPresentationId,
            float decisionDelaySeconds,
            float attackDelaySeconds,
            Vector3 incapacitationRotationEuler,
            Vector3 incapacitationOffset)
        {
            presentationId = actorPresentationId ?? string.Empty;
            postDecisionDelaySeconds = Mathf.Max(0f, decisionDelaySeconds);
            postAttackDelaySeconds = Mathf.Max(0f, attackDelaySeconds);
            incapacitationLocalRotationEuler = incapacitationRotationEuler;
            incapacitationLocalOffset = incapacitationOffset;
        }

        public string PresentationId => presentationId;

        public float PostDecisionDelaySeconds =>
            Mathf.Max(0f, postDecisionDelaySeconds);

        public float PostAttackDelaySeconds =>
            Mathf.Max(0f, postAttackDelaySeconds);

        public Quaternion IncapacitationLocalRotation =>
            Quaternion.Euler(incapacitationLocalRotationEuler);

        public Vector3 IncapacitationLocalOffset =>
            incapacitationLocalOffset;
    }

    [CreateAssetMenu(
        fileName = "EnemyPresentationCatalog",
        menuName = "Grit Gud/Enemy Presentation Catalog")]
    public sealed class EnemyPresentationCatalog : ScriptableObject
    {
        internal const string DefaultResourceName =
            "Gameplay/EnemyPresentationCatalog";

        [SerializeField, Min(0.01f)]
        private float detectionIntervalSeconds = 0.15f;

        [SerializeField]
        private EnemyPresentationDefinition[] entries =
            Array.Empty<EnemyPresentationDefinition>();

        private Dictionary<string, EnemyPresentationDefinition> index;

        public float DetectionIntervalSeconds =>
            Mathf.Max(0.01f, detectionIntervalSeconds);

        public static EnemyPresentationCatalog LoadDefault()
        {
            EnemyPresentationCatalog catalog =
                Resources.Load<EnemyPresentationCatalog>(
                    DefaultResourceName);
            if (catalog == null)
                throw new InvalidOperationException(
                    $"The Resources catalog '{DefaultResourceName}' could not be loaded.");
            return catalog;
        }

        public EnemyPresentationDefinition Get(string presentationId)
        {
            EnsureIndex();
            if (!index.TryGetValue(
                    presentationId ?? string.Empty,
                    out EnemyPresentationDefinition definition))
                throw new KeyNotFoundException(
                    $"Enemy presentation '{presentationId}' is not defined.");
            return definition;
        }

        internal static EnemyPresentationCatalog CreateRuntime(
            float detectionInterval,
            params EnemyPresentationDefinition[] definitions)
        {
            var catalog = CreateInstance<EnemyPresentationCatalog>();
            catalog.detectionIntervalSeconds = detectionInterval;
            catalog.entries = definitions
                ?? Array.Empty<EnemyPresentationDefinition>();
            return catalog;
        }

        private void OnEnable()
        {
            index = null;
        }

        private void EnsureIndex()
        {
            if (index != null)
                return;
            index = new Dictionary<string, EnemyPresentationDefinition>(
                StringComparer.Ordinal);
            foreach (EnemyPresentationDefinition entry in entries)
            {
                if (entry == null
                    || string.IsNullOrWhiteSpace(entry.PresentationId))
                    continue;
                if (!index.TryAdd(entry.PresentationId, entry))
                    throw new InvalidOperationException(
                        $"Enemy presentation '{entry.PresentationId}' is duplicated.");
            }
        }
    }
}
