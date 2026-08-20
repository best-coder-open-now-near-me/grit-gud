using System;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    internal sealed class UnityEnemyTacticalQuery
    {
        private readonly GameplaySession session;
        private readonly GameplayWorldRegistry registry;
        private readonly ScenarioActorDefinition definition;
        private readonly GameplayActorView view;

        public UnityEnemyTacticalQuery(
            GameplaySession gameplaySession,
            GameplayWorldRegistry worldRegistry,
            ScenarioActorDefinition actorDefinition,
            GameplayActorView actorView)
        {
            session = gameplaySession ?? throw new ArgumentNullException(
                nameof(gameplaySession));
            registry = worldRegistry ?? throw new ArgumentNullException(
                nameof(worldRegistry));
            definition = actorDefinition ?? throw new ArgumentNullException(
                nameof(actorDefinition));
            view = actorView ?? throw new ArgumentNullException(nameof(actorView));
        }

        /// <summary>
        /// Freezes how audible a recorded world sound is to this observer.
        /// Distance attenuation is calculated here; the canonical rule still
        /// validates the authored hearing range while reducing the observation.
        /// </summary>
        public EncounterSoundEvidence CaptureSound(
            string sourceId,
            GameplayPosition origin,
            float loudness)
        {
            if (string.IsNullOrWhiteSpace(sourceId))
                throw new ArgumentException(
                    "Sound evidence requires a source actor.",
                    nameof(sourceId));
            if (float.IsNaN(loudness) || float.IsInfinity(loudness)
                || loudness < 0f || loudness > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(loudness));
            }

            EncounterAwarenessPolicyDefinition policy = definition.Combat
                .EnemyBehavior.AwarenessPolicy;
            return GameplaySoundEvidenceRules.Capture(
                definition.Id,
                session.GetActor(definition.Id).Pose.Position,
                sourceId,
                origin,
                loudness,
                policy.HearingRange,
                IsSoundObstructed(sourceId, origin));
        }

        private bool IsSoundObstructed(
            string sourceId,
            GameplayPosition sourcePosition)
        {
            Vector3 source = MovementRouteSampling.ToVector3(sourcePosition)
                + (Vector3.up * 1.2f);
            Vector3 listener = view.Stance.FirstPersonEyePosition;
            Vector3 offset = listener - source;
            float distance = offset.magnitude;
            if (distance <= 0.0001f)
                return false;

            GameplayActorView sourceView = null;
            registry.TryGetActor(sourceId, out sourceView);
            foreach (RaycastHit hit in Physics.RaycastAll(
                source,
                offset / distance,
                distance,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore))
            {
                Transform transform = hit.collider?.transform;
                if (transform == null
                    || IsPartOf(transform, view.Transform)
                    || (sourceView != null
                        && IsPartOf(transform, sourceView.Transform)))
                {
                    continue;
                }
                return true;
            }

            return false;
        }

        private static bool IsPartOf(Transform candidate, Transform root) =>
            candidate == root || candidate.IsChildOf(root);

    }
}
