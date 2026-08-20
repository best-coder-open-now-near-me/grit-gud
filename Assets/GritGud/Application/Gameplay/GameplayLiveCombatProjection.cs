using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;

namespace GritGud.Application.Gameplay
{
    /// <summary>
    /// Two-phase projection of one immutable combat root into the mutable
    /// objects consumed by live presentation. Every subsystem validates
    /// before any projection is installed; notifications publish only after
    /// the complete projected root matches the reducer result.
    /// </summary>
    public sealed class GameplayLiveCombatProjection
    {
        private readonly GameplaySession session;
        private readonly DestructiblePropSession destructibles;
        private readonly IReadOnlyList<VehicleMomentumSession> vehicles;
        private readonly Dictionary<string, VehicleMomentumSession>
            vehiclesById;
        private readonly GameplayProjectileSession projectiles;
        private readonly GameplaySmokeFieldSession smokeFields;
        private readonly GameplayFireFieldSession fireFields;
        private readonly GameplayDroneSession drones;
        private bool bound;

        public GameplayLiveCombatProjection(
            GameplaySession gameplay,
            DestructiblePropSession destructibleSession = null,
            IEnumerable<VehicleMomentumSession> vehicleSessions = null,
            GameplayProjectileSession projectileSession = null,
            GameplaySmokeFieldSession smokeFieldSession = null,
            GameplayFireFieldSession fireFieldSession = null,
            GameplayDroneSession droneSession = null)
        {
            session = gameplay ?? throw new ArgumentNullException(
                nameof(gameplay));
            destructibles = destructibleSession;
            projectiles = projectileSession;
            smokeFields = smokeFieldSession;
            fireFields = fireFieldSession;
            drones = droneSession;
            if (vehicleSessions != null)
            {
                var copy = new List<VehicleMomentumSession>();
                vehiclesById = new Dictionary<string, VehicleMomentumSession>(
                    StringComparer.Ordinal);
                foreach (VehicleMomentumSession vehicle in vehicleSessions)
                {
                    if (vehicle == null
                        || !vehiclesById.TryAdd(
                            vehicle.State.VehicleId,
                            vehicle))
                        throw new ArgumentException(
                            "Vehicle projections must be non-null and unique.",
                            nameof(vehicleSessions));
                    copy.Add(vehicle);
                }
                copy.Sort((left, right) => StringComparer.Ordinal.Compare(
                    left.State.VehicleId,
                    right.State.VehicleId));
                vehicles = copy.AsReadOnly();
            }
        }

        public GameplayCombatStateCoverage Coverage
        {
            get
            {
                GameplayCombatStateCoverage coverage =
                    GameplayCombatStateCoverage.Session;
                if (destructibles != null)
                    coverage |= GameplayCombatStateCoverage.Destructibles;
                if (vehicles != null)
                    coverage |= GameplayCombatStateCoverage.Vehicles;
                if (projectiles != null)
                    coverage |= GameplayCombatStateCoverage.Projectiles;
                if (smokeFields != null)
                    coverage |= GameplayCombatStateCoverage.SmokeFields;
                if (fireFields != null)
                    coverage |= GameplayCombatStateCoverage.FireFields;
                if (drones != null)
                    coverage |= GameplayCombatStateCoverage.Drones;
                return coverage;
            }
        }

        public GameplayCombatStateSnapshot Capture() =>
            GameplayCombatStateCapture.Capture(
                session,
                destructibles,
                vehicles,
                projectiles,
                smokeFields,
                fireFields,
                drones);

        internal void Bind(GameplayCombatStateSnapshot initialState)
        {
            if (bound)
                throw new InvalidOperationException(
                    "Live combat projection is already bound.");
            RequireCoverage(initialState);
            GameplayCombatStateSnapshot captured = Capture();
            if (!string.Equals(
                    captured.CanonicalHash,
                    initialState.CanonicalHash,
                    StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Live combat objects do not match the initial canonical root.");

            destructibles?.BindCanonicalProjection(
                initialState.Destructibles);
            if (vehicles != null)
                foreach (VehicleMomentumState state in initialState.Vehicles)
                    vehiclesById[state.VehicleId]
                        .BindCanonicalProjection(state);
            projectiles?.BindCanonicalProjection(initialState.Projectiles);
            smokeFields?.BindCanonicalProjection(initialState.SmokeFields);
            fireFields?.BindCanonicalProjection(initialState.FireFields);
            drones?.BindCanonicalProjection(initialState.Drones);
            session.BindCanonicalProjection(initialState.Session);
            bound = true;
        }

        internal void Install(GameplayReductionResult reduction)
        {
            if (!bound)
                throw new InvalidOperationException(
                    "Live combat projection is not bound.");
            if (reduction == null)
                throw new ArgumentNullException(nameof(reduction));
            GameplayCombatStateSnapshot captured = Capture();
            if (!string.Equals(
                    captured.CanonicalHash,
                    reduction.Previous.CanonicalHash,
                    StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Live projection no longer matches the reducer's previous root.");
            object semanticRecord = RequireSemanticRecord(reduction);
            Validate(reduction.Resulting, semanticRecord);

            var notifications = new GameplayNotificationBatch();
            destructibles?.InstallCanonicalProjection(
                reduction.Resulting.Destructibles,
                reduction.Resulting.Session.LastTransitionSequence,
                notifications);
            if (vehicles != null)
                foreach (VehicleMomentumState state in
                    reduction.Resulting.Vehicles)
                    vehiclesById[state.VehicleId]
                        .InstallCanonicalProjection(state, semanticRecord);
            projectiles?.InstallCanonicalProjection(
                reduction.Resulting.Projectiles,
                semanticRecord);
            smokeFields?.InstallCanonicalProjection(
                reduction.Resulting.SmokeFields,
                notifications);
            fireFields?.InstallCanonicalProjection(
                reduction.Resulting.FireFields,
                notifications);
            drones?.InstallCanonicalProjection(reduction.Resulting.Drones);
            session.InstallCanonicalProjection(
                reduction.Resulting.Session,
                semanticRecord,
                notifications);

            GameplayCombatStateSnapshot installed = Capture();
            if (!string.Equals(
                    installed.CanonicalHash,
                    reduction.Resulting.CanonicalHash,
                    StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "The installed live projection does not match the canonical root.");
            notifications.Publish();
        }

        private void Validate(
            GameplayCombatStateSnapshot state,
            object semanticRecord)
        {
            RequireCoverage(state);
            session.ValidateCanonicalProjection(state.Session, semanticRecord);
            destructibles?.ValidateCanonicalProjection(state.Destructibles);
            if (vehicles != null)
            {
                if (state.Vehicles.Count != vehicles.Count)
                    throw new InvalidOperationException(
                        "Canonical projection changed the vehicle set.");
                foreach (VehicleMomentumState vehicle in state.Vehicles)
                {
                    if (!vehiclesById.TryGetValue(
                            vehicle.VehicleId,
                            out VehicleMomentumSession target))
                        throw new InvalidOperationException(
                            $"Canonical projection contains unknown vehicle '{vehicle.VehicleId}'.");
                    target.ValidateCanonicalProjection(
                        vehicle,
                        semanticRecord);
                }
            }
            projectiles?.ValidateCanonicalProjection(
                state.Projectiles,
                semanticRecord);
            smokeFields?.ValidateCanonicalProjection(state.SmokeFields);
            fireFields?.ValidateCanonicalProjection(state.FireFields);
            drones?.ValidateCanonicalProjection(state.Drones);
        }

        private void RequireCoverage(GameplayCombatStateSnapshot state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));
            if (state.Coverage != Coverage)
                throw new InvalidOperationException(
                    $"Live projection coverage '{Coverage}' does not match canonical coverage '{state.Coverage}'.");
        }

        private static object RequireSemanticRecord(
            GameplayReductionResult reduction)
        {
            GameplayTransitionReducedEvent semantic = null;
            foreach (GameplayDomainEvent domainEvent in reduction.DomainEvents)
            {
                if (!(domainEvent is GameplayTransitionReducedEvent reduced))
                    continue;
                if (semantic != null)
                    throw new InvalidOperationException(
                        "A semantic transition produced more than one authoritative record.");
                semantic = reduced;
            }
            return semantic?.SemanticRecord
                ?? throw new InvalidOperationException(
                    "A semantic transition produced no authoritative record.");
        }
    }
}
