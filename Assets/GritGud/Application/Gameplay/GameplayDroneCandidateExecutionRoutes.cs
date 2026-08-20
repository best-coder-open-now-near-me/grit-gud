using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;

namespace GritGud.Application.Gameplay
{
    public sealed class GameplayDroneMoveCandidateExecutionRoute :
        IGameplayCandidateExecutionRoute
    {
        public const string Id = "drone-move.v1";

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
            DroneSnapshot drone = FindDrone(
                context.State.Drones,
                intent.DroneId);
            GameplayActorSnapshot controller = session.GetActor(
                drone.Definition.ControllerActorId);
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
                                    controller.ActorId,
                                    StringComparison.Ordinal)
                                    ? "controller-not-active"
                                    : controller.IsIncapacitated
                                        ? "controller-incapacitated"
                                        : drone.Position.DistanceTo(
                                            intent.Origin) != 0f
                                            ? "drone-origin-stale"
                                            : drone.Position.DistanceTo(
                                                intent.Destination)
                                                > drone.Definition
                                                    .MaximumMoveDistance
                                                ? "drone-destination-out-of-range"
                                                : controller.TurnBudget
                                                    .ActionPoints
                                                    < drone.Definition.MoveCost
                                                        .ActionPoints
                                                    ? "insufficient-action-points"
                                                    : controller.TurnBudget
                                                        .MovementOpportunity
                                                        < drone.Definition
                                                            .MoveCost
                                                            .MovementOpportunity
                                                        ? "insufficient-movement-opportunity"
                                                        : string.Empty;
            bool legal = failure.Length == 0;
            DroneMoveRecord movement = legal
                ? new DroneMoveRecord(
                    controller.ActorId,
                    drone.DroneId,
                    drone.Position,
                    intent.Destination,
                    intent.FacingDegrees,
                    drone.Definition.MoveCost,
                    controller.TurnBudget,
                    controller.TurnBudget.SpendAction(
                        drone.Definition.MoveCost))
                : null;
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

        private static DroneSnapshot FindDrone(
            IEnumerable<DroneSnapshot> drones,
            string droneId)
        {
            foreach (DroneSnapshot drone in drones)
                if (string.Equals(
                    drone.DroneId,
                    droneId,
                    StringComparison.Ordinal)) return drone;
            throw new KeyNotFoundException(
                $"Drone '{droneId}' is absent from canonical state.");
        }
    }
}
