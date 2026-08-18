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

    public sealed class GameplayWorldTransitionReducer :
        IGameplaySemanticTransitionReducer
    {
        public bool Supports(GameplayCapabilityProfile profile) =>
            profile != null
            && (profile.Equals(GameplayCapabilityProfiles.AdvanceProjectile())
                || profile.Equals(GameplayCapabilityProfiles.VehicleMove()));

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
