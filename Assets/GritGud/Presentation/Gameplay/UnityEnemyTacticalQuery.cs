using System;
using System.Collections.Generic;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Levels;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    internal sealed class UnityEnemyTacticalQuery
    {
        private static readonly float[] CandidateAngles =
        {
            0f,
            30f,
            -30f,
            60f,
            -60f,
            90f,
            -90f,
            180f,
        };

        private readonly GameplaySession session;
        private readonly GameplayWorldRegistry registry;
        private readonly ScenarioActorDefinition definition;
        private readonly GameplayActorView view;
        private readonly UnityMovementRouteSegmentValidator movementValidator;
        private readonly ISightObscuranceQuery sightObscurance;

        public UnityEnemyTacticalQuery(
            GameplaySession gameplaySession,
            GameplayWorldRegistry worldRegistry,
            ScenarioActorDefinition actorDefinition,
            GameplayActorView actorView,
            ISightObscuranceQuery obscuranceQuery = null,
            IEnumerable<LevelTraversalLinkData> traversalLinks = null)
        {
            session = gameplaySession ?? throw new ArgumentNullException(
                nameof(gameplaySession));
            registry = worldRegistry ?? throw new ArgumentNullException(
                nameof(worldRegistry));
            definition = actorDefinition ?? throw new ArgumentNullException(
                nameof(actorDefinition));
            view = actorView ?? throw new ArgumentNullException(nameof(actorView));
            sightObscurance = obscuranceQuery;
            CharacterController controller =
                view.Root.GetComponent<CharacterController>();
            movementValidator = new UnityMovementRouteSegmentValidator(
                controller,
                traversalLinks);
        }

        public TargetExposureSnapshot CaptureExposure(string targetId) =>
            CaptureExposure(targetId, view.Stance.FirstPersonEyePosition);

        /// <summary>
        /// Produces a presentation-validated patrol route without spending a
        /// combat turn budget. The canonical patrol reducer later verifies that
        /// its destination is the authored next waypoint.
        /// </summary>
        public bool TryBuildPatrolRoute(
            GameplayPosition authoredDestination,
            out MovementRouteRecord route)
        {
            GameplayActorSnapshot actor = session.GetActor(definition.Id);
            if (actor.Pose.Position.DistanceTo(authoredDestination) <= 0.001f)
            {
                route = null;
                return false;
            }

            var planner = new MovementRoutePlanner(actor, movementValidator);
            if (!planner.TryAppend(authoredDestination, out _))
            {
                route = null;
                return false;
            }

            route = planner.Confirm();
            if (route.Destination.DistanceTo(authoredDestination) > 0.001f)
            {
                route = null;
                return false;
            }

            return true;
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
            float distance = session.GetActor(definition.Id).Pose.Position
                .DistanceTo(origin);
            if (distance > policy.HearingRange + 0.0001f)
                return new EncounterSoundEvidence(sourceId, origin, 0f);
            float rangeFraction = policy.HearingRange <= 0.001f
                ? 1f
                : Mathf.Clamp01(distance / policy.HearingRange);
            // A source at the edge of hearing remains meaningful, but weaker
            // than one nearby. A current physical obstruction muffles the
            // frozen evidence rather than making it disappear outright.
            float occlusion = IsSoundObstructed(sourceId, origin) ? 0.5f : 1f;
            float audibility = loudness * occlusion
                * Mathf.Lerp(1f, 0.5f, rangeFraction);
            return new EncounterSoundEvidence(sourceId, origin, audibility);
        }

        public IReadOnlyList<EnemyMovementOption> BuildMovementOptions(
            string targetId)
        {
            GameplayActorSnapshot actor = session.GetActor(definition.Id);
            GameplayActorSnapshot target = session.GetActor(targetId);
            float maximumDistance = Mathf.Min(
                actor.TurnBudget.MovementOpportunity,
                definition.Combat.EnemyBehavior.MovementSearchRadius);
            if (maximumDistance <= 0.001f)
                return Array.Empty<EnemyMovementOption>();

            Vector3 origin = MovementRouteSampling.ToVector3(
                actor.Pose.Position);
            Vector3 towardTarget = MovementRouteSampling.ToVector3(
                target.Pose.Position) - origin;
            towardTarget.y = 0f;
            if (towardTarget.sqrMagnitude <= 0.0001f)
                towardTarget = view.Transform.forward;
            towardTarget.Normalize();
            float[] distances =
            {
                maximumDistance,
                maximumDistance * 0.5f,
            };
            var options = new List<EnemyMovementOption>();
            var destinations = new HashSet<string>(StringComparer.Ordinal);
            foreach (float distance in distances)
            foreach (float angle in CandidateAngles)
            {
                Vector3 direction = Quaternion.AngleAxis(angle, Vector3.up)
                    * towardTarget;
                Vector3 requested = origin + (direction * distance);
                var planner = new MovementRoutePlanner(
                    actor,
                    movementValidator);
                if (!planner.TryAppend(
                        new GameplayPosition(
                            requested.x,
                            requested.y,
                            requested.z),
                        out _))
                    continue;

                MovementRouteRecord route = planner.Confirm();
                string key = $"{route.Destination.X:0.###}|"
                    + $"{route.Destination.Y:0.###}|"
                    + $"{route.Destination.Z:0.###}";
                if (!destinations.Add(key))
                    continue;

                Vector3 eyeOffset = view.Stance.FirstPersonEyePosition
                    - view.Transform.position;
                Vector3 candidateEye = MovementRouteSampling.ToVector3(
                    route.Destination) + eyeOffset;
                TargetExposureSnapshot exposure = CaptureExposure(
                    targetId,
                    candidateEye);
                options.Add(new EnemyMovementOption(
                    route,
                    exposure,
                    route.Destination.DistanceTo(target.Pose.Position)));
            }

            return options.AsReadOnly();
        }

        private TargetExposureSnapshot CaptureExposure(
            string targetId,
            Vector3 observerOrigin)
        {
            GameplayActorView target = registry.GetActor(targetId);
            IReadOnlyList<ActorTargetRegionSample> presented =
                target.TargetProfile.GetTargetRegionSamples();
            var regions = new List<TargetRegionSample>(presented.Count);
            foreach (ActorTargetRegionSample region in presented)
                regions.Add(new TargetRegionSample(
                    region.Id,
                    ToGameplayPosition(region.WorldCenter),
                    region.Radius));
            var query = new UnityTargetExposureQuery(
                view.Transform,
                target.Transform,
                obscuranceQuery: sightObscurance);
            return query.Capture(
                definition.Id,
                ToGameplayPosition(observerOrigin),
                targetId,
                regions);
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

        private static GameplayPosition ToGameplayPosition(Vector3 value) =>
            new GameplayPosition(value.x, value.y, value.z);
    }
}
