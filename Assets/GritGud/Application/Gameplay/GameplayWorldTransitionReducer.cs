using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;

namespace GritGud.Application.Gameplay
{
    public sealed class GameplayProjectileAdvanceTransitionPayload :
        GameplayTransitionPayload
    {
        public GameplayProjectileAdvanceTransitionPayload(
            string actorId,
            ProjectileAdvanceRecord advance,
            bool destructiblesShareGameplayJournal)
            : base(
                GameplayCapabilityProfiles.AdvanceProjectile(),
                actorId,
                (advance ?? throw new ArgumentNullException(nameof(advance)))
                    .ProjectileId)
        {
            Advance = advance;
            DestructiblesShareGameplayJournal =
                destructiblesShareGameplayJournal;
        }

        public ProjectileAdvanceRecord Advance { get; }
        public bool DestructiblesShareGameplayJournal { get; }
    }

    public sealed class GameplayVehicleMoveTransitionPayload :
        GameplayTransitionPayload
    {
        public GameplayVehicleMoveTransitionPayload(
            string actorId,
            VehicleMomentumRecord movement)
            : base(
                GameplayCapabilityProfiles.VehicleMove(),
                actorId,
                (movement ?? throw new ArgumentNullException(nameof(movement)))
                    .Resulting.VehicleId)
        {
            Movement = movement;
        }

        public VehicleMomentumRecord Movement { get; }
    }

    public sealed class GameplayDroneMoveTransitionPayload :
        GameplayTransitionPayload
    {
        public GameplayDroneMoveTransitionPayload(DroneMoveRecord movement)
            : base(
                GameplayCapabilityProfiles.AerialDroneMove(),
                (movement ?? throw new ArgumentNullException(nameof(movement)))
                    .ControllerActorId,
                movement.DroneId)
        {
            Movement = movement;
        }

        public DroneMoveRecord Movement { get; }
    }

    public sealed class GameplayWorldTransitionReducer :
        IGameplaySemanticTransitionReducer
    {
        public bool Supports(GameplayCapabilityProfile profile) =>
            profile != null
            && (profile.Equals(GameplayCapabilityProfiles.AdvanceProjectile())
                || profile.Equals(GameplayCapabilityProfiles.VehicleMove())
                || profile.Equals(GameplayCapabilityProfiles.AerialDroneMove()));

        public GameplayReductionResult Reduce(
            GameplayCombatStateSnapshot state,
            GameplaySemanticTransition transition)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (transition == null)
                throw new ArgumentNullException(nameof(transition));
            switch (transition.Payload)
            {
                case GameplayProjectileAdvanceTransitionPayload projectile:
                    return ReduceProjectile(state, transition, projectile);
                case GameplayVehicleMoveTransitionPayload vehicle:
                    return ReduceVehicle(state, transition, vehicle);
                case GameplayDroneMoveTransitionPayload drone:
                    return ReduceDrone(state, transition, drone);
                default:
                    throw new ArgumentException(
                        "World transition payload is unsupported.",
                        nameof(transition));
            }
        }

        private static GameplayReductionResult ReduceProjectile(
            GameplayCombatStateSnapshot state,
            GameplaySemanticTransition transition,
            GameplayProjectileAdvanceTransitionPayload payload)
        {
            state.RequireCoverage(GameplayCombatStateCoverage.Projectiles);
            if (payload.Advance.Sequence != transition.Identity.Sequence)
                throw new InvalidOperationException(
                    "Projectile advance must share its canonical transition sequence.");
            GameplayCombatStateSnapshot projected =
                GameplayProjectileAdvanceStateProjector.Project(
                    state,
                    payload.Advance,
                    payload.DestructiblesShareGameplayJournal);
            var mutation = new GameplayCanonicalStateMutation(projected)
            {
                LastTransitionSequence = transition.Identity.Sequence,
            };
            GameplayCombatStateSnapshot resulting = mutation.Build();
            return Result(state, resulting, transition, payload.Advance);
        }

        private static GameplayReductionResult ReduceVehicle(
            GameplayCombatStateSnapshot state,
            GameplaySemanticTransition transition,
            GameplayVehicleMoveTransitionPayload payload)
        {
            state.RequireCoverage(GameplayCombatStateCoverage.Vehicles);
            VehicleMomentumRecord movement = payload.Movement;
            if (movement.Sequence != transition.Identity.Sequence)
                throw new InvalidOperationException(
                    "Vehicle movement must share its canonical transition sequence.");
            VehicleMomentumState current = FindVehicle(
                state.Vehicles,
                movement.Resulting.VehicleId);
            if (!StatesMatch(current, movement.Previous))
                throw new InvalidOperationException(
                    "Vehicle transition starts from stale momentum state.");
            var mutation = new GameplayCanonicalStateMutation(state)
            {
                JournalSequence = checked(state.Session.JournalSequence + 1L),
                LastTransitionSequence = transition.Identity.Sequence,
            };
            mutation.ReplaceVehicle(movement.Resulting);
            return Result(state, mutation.Build(), transition, movement);
        }

        private static GameplayReductionResult ReduceDrone(
            GameplayCombatStateSnapshot state,
            GameplaySemanticTransition transition,
            GameplayDroneMoveTransitionPayload payload)
        {
            state.RequireCoverage(GameplayCombatStateCoverage.Drones);
            GameplaySessionStateSnapshot session = state.Session;
            DroneMoveRecord movement = payload.Movement;
            DroneSnapshot drone = FindDrone(state.Drones, movement.DroneId);
            if (!drone.IsOperational)
                throw new InvalidOperationException(
                    "Destroyed drones cannot receive movement commands.");
            if (drone.Definition.InitiativeBinding
                    != DroneInitiativeBinding.ControllerTurn
                || !string.Equals(
                    drone.Definition.ControllerActorId,
                    movement.ControllerActorId,
                    StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Drone movement does not match its controller binding.");
            if (session.Mode != GameplaySessionMode.TurnBased
                || session.Operation != GameplaySessionOperation.None
                || !string.Equals(
                    session.ActiveActorId,
                    movement.ControllerActorId,
                    StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "A drone can move only during its controller's idle turn.");
            GameplayActorSnapshot controller = session.GetActor(
                movement.ControllerActorId);
            if (controller.IsIncapacitated)
                throw new InvalidOperationException(
                    "An incapacitated actor cannot control a drone.");
            if (drone.Position.DistanceTo(movement.Origin) != 0f)
                throw new InvalidOperationException(
                    "Drone movement starts from a stale position.");
            if (drone.Position.DistanceTo(movement.Destination)
                > drone.Definition.MaximumMoveDistance)
                throw new InvalidOperationException(
                    "Drone movement exceeds its authored maximum distance.");
            if (!CostsMatch(movement.Cost, drone.Definition.MoveCost)
                || !BudgetsMatch(
                    controller.TurnBudget,
                    movement.PreviousBudget))
                throw new InvalidOperationException(
                    "Drone movement was prepared against stale authored costs or budget.");

            var mutation = new GameplayCanonicalStateMutation(state)
            {
                JournalSequence = checked(session.JournalSequence + 1L),
                Revision = checked(session.Revision + 1L),
                LastTransitionSequence = transition.Identity.Sequence,
            };
            mutation.ReplaceActor(GameplayCanonicalStateMutation.CopyActor(
                controller,
                budget: movement.ResultingBudget));
            mutation.ReplaceDrone(new DroneSnapshot(
                drone.Definition,
                movement.Destination,
                movement.ResultingFacingDegrees,
                drone.RemainingIntegrity));
            return Result(state, mutation.Build(), transition, movement);
        }

        private static GameplayReductionResult Result(
            GameplayCombatStateSnapshot previous,
            GameplayCombatStateSnapshot resulting,
            GameplaySemanticTransition transition,
            object record) => new GameplayReductionResult(
                previous,
                resulting,
                new GameplayDomainEvent[]
                {
                    new GameplayTransitionReducedEvent(
                        transition.Identity,
                        transition.Payload.SubjectId,
                        record),
                });

        private static VehicleMomentumState FindVehicle(
            IEnumerable<VehicleMomentumState> vehicles,
            string vehicleId)
        {
            foreach (VehicleMomentumState vehicle in vehicles)
                if (string.Equals(
                    vehicle.VehicleId,
                    vehicleId,
                    StringComparison.Ordinal))
                    return vehicle;
            throw new KeyNotFoundException(
                $"Vehicle '{vehicleId}' is absent from canonical state.");
        }

        private static DroneSnapshot FindDrone(
            IEnumerable<DroneSnapshot> drones,
            string droneId)
        {
            foreach (DroneSnapshot drone in drones)
                if (string.Equals(
                    drone.DroneId,
                    droneId,
                    StringComparison.Ordinal))
                    return drone;
            throw new KeyNotFoundException(
                $"Drone '{droneId}' is absent from canonical state.");
        }

        private static bool CostsMatch(
            GritGud.Domain.Turns.ActionCost left,
            GritGud.Domain.Turns.ActionCost right) =>
            left.ActionPoints == right.ActionPoints
            && left.MovementOpportunity == right.MovementOpportunity
            && left.Mobility == right.Mobility;

        private static bool BudgetsMatch(
            GritGud.Domain.Turns.TurnBudget left,
            GritGud.Domain.Turns.TurnBudget right) =>
            left.ActionPoints == right.ActionPoints
            && left.MovementOpportunity == right.MovementOpportunity;

        private static bool StatesMatch(
            VehicleMomentumState left,
            VehicleMomentumState right) =>
            string.Equals(
                left.VehicleId,
                right.VehicleId,
                StringComparison.Ordinal)
            && left.Position.DistanceTo(right.Position) == 0f
            && left.ForwardDegrees == right.ForwardDegrees
            && left.Speed == right.Speed;
    }
}
