using System;
using System.Collections.Generic;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
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
            ISightObscuranceQuery obscuranceQuery = null)
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
                controller);
        }

        public TargetExposureSnapshot CaptureExposure(string targetId) =>
            CaptureExposure(targetId, view.Stance.FirstPersonEyePosition);

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
                target.Stance.GetTargetRegionSamples();
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

        private static GameplayPosition ToGameplayPosition(Vector3 value) =>
            new GameplayPosition(value.x, value.y, value.z);
    }
}
