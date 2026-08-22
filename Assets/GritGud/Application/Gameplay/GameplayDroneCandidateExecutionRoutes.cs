using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;

namespace GritGud.Application.Gameplay
{
    public sealed class GameplayDroneMoveCandidateExecutionRoute :
        IGameplayCandidateExecutionRoute
    {
        public const string Id = "drone-move.v1";

        private readonly ScenarioDefinition scenario;
        private readonly GameplayHeadlessSpatialEvidence spatial;

        public GameplayDroneMoveCandidateExecutionRoute(
            ScenarioDefinition scenarioDefinition,
            GameplayHeadlessSpatialEvidence spatialEvidence)
        {
            scenario = scenarioDefinition ?? throw new ArgumentNullException(
                nameof(scenarioDefinition));
            spatial = spatialEvidence ?? throw new ArgumentNullException(
                nameof(spatialEvidence));
        }

        public string RouteId => Id;

        public bool Supports(GameplayCapabilityProfile profile) =>
            profile != null
            && profile.Equals(GameplayCapabilityProfiles.AerialDroneMove());

        public GameplayExecutableCandidateEvaluation Evaluate(
            GameplayDecisionContext context,
            GameplayCandidate candidate)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            GameplayHeadlessDroneMoveIntent intent = candidate?.Intent
                    as GameplayHeadlessDroneMoveIntent
                ?? throw new ArgumentException(
                    "Drone movement requires a frozen destination intent.",
                    nameof(candidate));
            if (!Supports(candidate.Profile))
                throw new NotSupportedException(
                    $"Route '{Id}' cannot evaluate '{candidate.Profile.Signature}'.");
            context.State.RequireCoverage(GameplayCombatStateCoverage.Drones);
            GameplaySessionStateSnapshot session = context.State.Session;
            SummonedDroneSnapshot drone = FindDrone(
                context.State.Drones,
                intent.DroneId);
            GameplayActorSnapshot summoner = session.GetActor(
                drone.SummonerActorId);
            string failure = !string.Equals(
                    intent.StateHash,
                    context.State.CanonicalHash,
                    StringComparison.Ordinal)
                ? "drone-evidence-stale"
                : !string.Equals(
                    candidate.SubjectId,
                    drone.DroneId,
                    StringComparison.Ordinal)
                    ? "drone-subject-mismatch"
                    : !drone.IsOperational
                        ? "drone-destroyed"
                        : session.Mode != GameplaySessionMode.TurnBased
                            ? "turn-mode-required"
                            : session.Operation
                                != GameplaySessionOperation.None
                                ? "operation-in-progress"
                                : !string.Equals(
                                    session.ActiveActorId,
                                    summoner.ActorId,
                                    StringComparison.Ordinal)
                                    ? "summoner-partner-not-active"
                                    : summoner.IsIncapacitated
                                        ? "summoner-partner-incapacitated"
                                        : drone.Position.DistanceTo(
                                            intent.Origin) != 0f
                                            ? "drone-origin-stale"
                                            : drone.Position.DistanceTo(
                                                intent.Destination)
                                                > drone.Definition
                                                    .MaximumMoveDistance
                                                ? "drone-destination-out-of-range"
                                                : summoner.TurnBudget
                                                    .ActionPoints
                                                    < drone.Definition.MoveCost
                                                        .ActionPoints
                                                    ? "insufficient-action-points"
                                                    : summoner.TurnBudget
                                                        .MovementOpportunity
                                                        < drone.Definition
                                                            .MoveCost
                                                            .MovementOpportunity
                                                        ? "insufficient-movement-opportunity"
                                                        : string.Empty;
            bool legal = failure.Length == 0;
            DroneMoveRecord movement = legal
                ? new DroneMoveRecord(
                    summoner.ActorId,
                    drone.DroneId,
                    drone.Position,
                    intent.Destination,
                    intent.FacingDegrees,
                    drone.Definition.MoveCost,
                    summoner.TurnBudget,
                    summoner.TurnBudget.SpendAction(
                        drone.Definition.MoveCost))
                : null;
            float visibilityBefore = legal
                ? CaptureHostileVisibility(context.State, drone)
                : 0f;
            float visibilityAfter = legal
                ? CaptureHostileVisibility(
                    ReplaceDrone(
                        context.State,
                        drone.WithPose(
                            intent.Destination,
                            intent.FacingDegrees)),
                    drone.WithPose(
                        intent.Destination,
                        intent.FacingDegrees))
                : 0f;
            float hostileDistanceBefore = legal
                ? NearestHostileDistance(
                    context.State,
                    drone,
                    drone.Position)
                : 0f;
            float hostileDistanceAfter = legal
                ? NearestHostileDistance(
                    context.State,
                    drone,
                    intent.Destination)
                : 0f;
            return new GameplayExecutableCandidateEvaluation(
                Id,
                candidate,
                context.State.CanonicalHash,
                legal,
                failure,
                new GameplayCandidateOutcomeEstimate(new[]
                {
                    new GameplayCandidateOutcomeFeature(
                        "drone.move-distance",
                        drone.Position.DistanceTo(intent.Destination)),
                    new GameplayCandidateOutcomeFeature(
                        "cost.action-points",
                        drone.Definition.MoveCost.ActionPoints),
                    new GameplayCandidateOutcomeFeature(
                        "cost.movement-opportunity",
                        drone.Definition.MoveCost.MovementOpportunity),
                    new GameplayCandidateOutcomeFeature(
                        "drone.visible-hostiles-before",
                        visibilityBefore),
                    new GameplayCandidateOutcomeFeature(
                        "drone.visible-hostiles-after",
                        visibilityAfter),
                    new GameplayCandidateOutcomeFeature(
                        "drone.hostile-visibility-gain",
                        Math.Max(0f, visibilityAfter - visibilityBefore)),
                    new GameplayCandidateOutcomeFeature(
                        "drone.hostile-distance-before",
                        hostileDistanceBefore),
                    new GameplayCandidateOutcomeFeature(
                        "drone.hostile-distance-after",
                        hostileDistanceAfter),
                    new GameplayCandidateOutcomeFeature(
                        "drone.hostile-distance-improvement",
                        hostileDistanceBefore - hostileDistanceAfter),
                }),
                new[] { intent.RouteEvidence },
                movement);
        }

        public GameplayTransitionPayload PreparePayload(
            GameplayDecisionContext context,
            GameplayExecutableCandidateEvaluation evaluation) =>
            new GameplayDroneMoveTransitionPayload(
                evaluation?.FrozenPreparation as DroneMoveRecord
                    ?? throw new ArgumentException(
                        "Drone movement preparation is missing.",
                        nameof(evaluation)));

        private static SummonedDroneSnapshot FindDrone(
            IEnumerable<SummonedDroneSnapshot> drones,
            string droneId)
        {
            foreach (SummonedDroneSnapshot drone in drones)
                if (string.Equals(
                    drone.DroneId,
                    droneId,
                    StringComparison.Ordinal)) return drone;
            throw new KeyNotFoundException(
                $"Drone '{droneId}' is absent from canonical state.");
        }

        private float CaptureHostileVisibility(
            GameplayCombatStateSnapshot state,
            SummonedDroneSnapshot drone)
        {
            ScenarioActorDefinition summoner = scenario.GetActor(
                drone.SummonerActorId);
            float result = 0f;
            foreach (GameplayActorSnapshot actor in state.Session.Actors)
            {
                if (actor.IsIncapacitated) continue;
                ScenarioActorDefinition target = scenario.GetActor(
                    actor.ActorId);
                if (!summoner.Combat.IsHostileTo(
                        target.Combat.AllegianceId))
                    continue;
                result += GameplayHeadlessEncounterEvidence.CaptureDroneSight(
                    state,
                    spatial,
                    drone.DroneId,
                    actor.ActorId).VisibleFraction;
            }
            return GameplayNumericPolicy.Normalize(result);
        }

        private float NearestHostileDistance(
            GameplayCombatStateSnapshot state,
            SummonedDroneSnapshot drone,
            GameplayPosition position)
        {
            ScenarioActorDefinition summoner = scenario.GetActor(
                drone.SummonerActorId);
            float nearest = 100000f;
            foreach (GameplayActorSnapshot actor in state.Session.Actors)
            {
                if (actor.IsIncapacitated) continue;
                ScenarioActorDefinition target = scenario.GetActor(
                    actor.ActorId);
                if (!summoner.Combat.IsHostileTo(
                        target.Combat.AllegianceId))
                    continue;
                nearest = Math.Min(
                    nearest,
                    position.DistanceTo(actor.Pose.Position));
            }
            return GameplayNumericPolicy.Normalize(nearest);
        }

        private static GameplayCombatStateSnapshot ReplaceDrone(
            GameplayCombatStateSnapshot state,
            SummonedDroneSnapshot replacement)
        {
            var drones = new List<SummonedDroneSnapshot>(state.Drones.Count);
            bool replaced = false;
            foreach (SummonedDroneSnapshot drone in state.Drones)
            {
                if (string.Equals(
                        drone.DroneId,
                        replacement.DroneId,
                        StringComparison.Ordinal))
                {
                    drones.Add(replacement);
                    replaced = true;
                }
                else
                {
                    drones.Add(drone);
                }
            }
            if (!replaced)
                throw new KeyNotFoundException(
                    $"Drone '{replacement.DroneId}' is absent from canonical state.");
            return new GameplayCombatStateSnapshot(
                state.Session,
                state.Destructibles,
                state.Vehicles,
                state.Projectiles,
                state.SmokeFields,
                state.Coverage,
                state.FireFields,
                drones);
        }
    }
}
