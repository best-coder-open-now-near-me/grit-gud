using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;

namespace GritGud.Application.Gameplay
{
    public sealed class GameplayDroneSession
    {
        private readonly GameplaySession gameplay;
        private readonly DestructiblePropSession destructibles;
        private readonly Dictionary<string, DroneArchetypeDefinition>
            archetypes = new Dictionary<string, DroneArchetypeDefinition>(
                StringComparer.Ordinal);
        private readonly Dictionary<string, SummonedDroneSnapshot> drones =
            new Dictionary<string, SummonedDroneSnapshot>(StringComparer.Ordinal);
        private bool canonicalProjectionBound;
        private Func<
            GameplayTransitionPayload,
            IEnumerable<GameplayEvidenceRecord>,
            GameplayReductionResult> canonicalExecutor;

        public GameplayDroneSession(
            GameplaySession gameplaySession,
            IEnumerable<DroneArchetypeDefinition> definitions,
            DestructiblePropSession destructibleSession = null)
        {
            gameplay = gameplaySession ?? throw new ArgumentNullException(
                nameof(gameplaySession));
            destructibles = destructibleSession;
            foreach (DroneArchetypeDefinition definition in definitions
                ?? throw new ArgumentNullException(nameof(definitions)))
            {
                if (definition == null || !archetypes.TryAdd(
                    definition.ArchetypeId,
                    definition))
                    throw new ArgumentException(
                        "Drone definitions must be non-null and unique.",
                        nameof(definitions));
            }
        }

        public IReadOnlyList<SummonedDroneSnapshot> CaptureDrones()
        {
            var result = new List<SummonedDroneSnapshot>(drones.Values);
            result.Sort((left, right) => StringComparer.Ordinal.Compare(
                left.DroneId, right.DroneId));
            return result.AsReadOnly();
        }

        internal void BindCanonicalExecutor(
            Func<
                GameplayTransitionPayload,
                IEnumerable<GameplayEvidenceRecord>,
                GameplayReductionResult> executor)
        {
            if (executor == null) throw new ArgumentNullException(nameof(executor));
            if (canonicalExecutor != null || canonicalProjectionBound)
                throw new InvalidOperationException(
                    "Drone semantic executor is already bound or projection binding has started.");
            canonicalExecutor = executor;
        }

        internal void BindCanonicalProjection(
            IReadOnlyList<SummonedDroneSnapshot> snapshots)
        {
            if (canonicalProjectionBound)
                throw new InvalidOperationException(
                    "Drones already have a canonical runtime projection.");
            ValidateCanonicalProjection(snapshots);
            drones.Clear();
            foreach (SummonedDroneSnapshot snapshot in snapshots)
                drones.Add(snapshot.DroneId, snapshot);
            canonicalProjectionBound = true;
        }

        internal void ValidateCanonicalProjection(
            IReadOnlyList<SummonedDroneSnapshot> snapshots)
        {
            if (snapshots == null)
                throw new ArgumentNullException(nameof(snapshots));
            var instanceIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (SummonedDroneSnapshot snapshot in snapshots)
            {
                if (!instanceIds.Add(snapshot.DroneId)
                    || !archetypes.TryGetValue(
                        snapshot.ArchetypeId,
                        out DroneArchetypeDefinition definition)
                    || !string.Equals(
                        GameplayCanonicalValueDigest.Calculate(definition),
                        GameplayCanonicalValueDigest.Calculate(
                            snapshot.Definition),
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Canonical drone '{snapshot.DroneId}' has an unknown or changed archetype.");
                }
            }
        }

        internal void InstallCanonicalProjection(
            IReadOnlyList<SummonedDroneSnapshot> snapshots)
        {
            if (!canonicalProjectionBound)
                throw new InvalidOperationException(
                    "Drones are not bound to a canonical runtime.");
            ValidateCanonicalProjection(snapshots);
            drones.Clear();
            foreach (SummonedDroneSnapshot snapshot in snapshots)
                drones.Add(snapshot.DroneId, snapshot);
        }

        public SummonedDroneSnapshot GetDrone(string droneId) =>
            drones.TryGetValue(droneId ?? string.Empty, out SummonedDroneSnapshot drone)
                ? drone
                : throw new KeyNotFoundException(
                    $"Drone '{droneId}' is not active.");

        public void CommitSummon(SummonDroneRecord record)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));
            if (canonicalProjectionBound)
            {
                canonicalExecutor(
                    new GameplaySummonDroneTransitionPayload(record),
                    null);
                return;
            }
            RequireLegacyMutationAllowed(nameof(CommitSummon));
            if (!archetypes.TryGetValue(
                    record.Archetype.ArchetypeId,
                    out DroneArchetypeDefinition archetype)
                || !string.Equals(
                    GameplayCanonicalValueDigest.Calculate(archetype),
                    GameplayCanonicalValueDigest.Calculate(record.Archetype),
                    StringComparison.Ordinal)
                || drones.ContainsKey(record.DroneInstanceId))
                throw new InvalidOperationException(
                    "Drone summon does not match the live archetype catalog.");
            gameplay.CommitDroneSummon(record);
            drones.Add(record.DroneInstanceId, record.Resulting);
        }

        public void CommitDismiss(DismissDroneRecord record)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));
            if (canonicalProjectionBound)
            {
                canonicalExecutor(
                    new GameplayDismissDroneTransitionPayload(record),
                    null);
                return;
            }
            RequireLegacyMutationAllowed(nameof(CommitDismiss));
            SummonedDroneSnapshot current = GetDrone(record.DroneId);
            if (!StatesMatch(current, record.Previous))
                throw new InvalidOperationException(
                    "Drone dismissal starts from stale live state.");
            gameplay.CommitDroneDismiss(record);
            drones[record.DroneId] = record.Resulting;
        }

        public DroneMoveRecord PrepareMove(
            string droneId,
            GameplayPosition destination,
            float facingDegrees)
        {
            SummonedDroneSnapshot drone = GetDrone(droneId);
            RequireSummonerPartnerTurn(drone);
            if (!drone.IsOperational)
                throw new InvalidOperationException(
                    "Destroyed drones cannot move.");
            if (drone.Position.DistanceTo(destination)
                > drone.Definition.MaximumMoveDistance)
                throw new InvalidOperationException(
                    "Drone destination exceeds its movement range.");
            TurnBudget previous = gameplay.GetActor(
                drone.SummonerActorId).TurnBudget;
            return new DroneMoveRecord(
                drone.SummonerActorId,
                drone.DroneId,
                drone.Position,
                destination,
                facingDegrees,
                drone.Definition.MoveCost,
                previous,
                previous.SpendAction(drone.Definition.MoveCost));
        }

        public void CommitMove(DroneMoveRecord record)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));
            if (canonicalProjectionBound)
            {
                canonicalExecutor(
                    new GameplayDroneMoveTransitionPayload(record),
                    null);
                return;
            }
            RequireLegacyMutationAllowed(nameof(CommitMove));
            SummonedDroneSnapshot drone = GetDrone(record.DroneId);
            RequireSummonerPartnerTurn(drone);
            gameplay.CommitDroneMoveBudget(record);
            drones[drone.DroneId] = drone.WithPose(
                record.Destination,
                record.ResultingFacingDegrees);
        }

        public DroneAttackRecord PrepareActorAttack(
            string droneId,
            AttackResolutionRecord resolution)
        {
            if (resolution == null) throw new ArgumentNullException(
                nameof(resolution));
            SummonedDroneSnapshot drone = GetDrone(droneId);
            RequireSummonerPartnerTurn(drone);
            if (!drone.IsOperational)
                throw new InvalidOperationException(
                    "Destroyed drones cannot attack.");
            if (!string.Equals(
                    resolution.AttackerId,
                    droneId,
                    StringComparison.Ordinal))
                throw new ArgumentException(
                    "Drone attack evidence must originate from the firing drone.",
                    nameof(resolution));
            TurnBudget previous = gameplay.GetActor(
                drone.SummonerActorId).TurnBudget;
            ActionCost cost = drone.Definition.Attack.TurnCost;
            return new DroneAttackRecord(
                drone.SummonerActorId,
                drone.DroneId,
                resolution.TargetId,
                GameplaySemanticSubjectKind.Actor.ToString(),
                cost,
                previous,
                previous.SpendAction(cost),
                resolution);
        }

        public void CommitAttack(DroneAttackRecord record)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));
            if (canonicalProjectionBound)
            {
                if (!Enum.TryParse(
                        record.TargetKind,
                        out GameplaySemanticSubjectKind targetKind))
                    throw new InvalidOperationException(
                        $"Drone attack target kind '{record.TargetKind}' is not semantic.");
                canonicalExecutor(
                    new GameplayDroneAttackTransitionPayload(
                        targetKind,
                        GetDrone(record.DroneId).Definition.Attack,
                        record),
                    null);
                return;
            }
            RequireLegacyMutationAllowed(nameof(CommitAttack));
            SummonedDroneSnapshot drone = GetDrone(record.DroneId);
            RequireSummonerPartnerTurn(drone);
            if (!drone.IsOperational)
                throw new InvalidOperationException(
                    "Destroyed drones cannot attack.");
            switch (record.Consequence)
            {
                case AttackResolutionRecord resolution:
                    gameplay.CommitDroneActorAttack(record, resolution);
                    break;
                case DestructibleDamageRecord damage:
                    if (destructibles == null)
                        throw new InvalidOperationException(
                            "Drone destructible damage requires a bound prop session.");
                    destructibles.ValidateDamage(damage);
                    gameplay.CommitDroneAttackBudget(record);
                    destructibles.CommitDamage(damage);
                    break;
                case DroneIntegrityDamageRecord damage:
                    SummonedDroneSnapshot target = GetDrone(damage.DroneId);
                    if (!StatesMatch(target, damage.Previous))
                        throw new InvalidOperationException(
                            "Drone damage starts from stale integrity state.");
                    gameplay.CommitDroneAttackBudget(record);
                    drones[target.DroneId] = damage.Resulting;
                    break;
                default:
                    throw new NotSupportedException(
                        "Drone attack consequence is not live-installable.");
            }
        }

        public void ApplyIntegrityDamage(DroneIntegrityDamageRecord damage)
        {
            RequireLegacyMutationAllowed(nameof(ApplyIntegrityDamage));
            if (damage == null) throw new ArgumentNullException(nameof(damage));
            SummonedDroneSnapshot current = GetDrone(damage.DroneId);
            if (!StatesMatch(current, damage.Previous))
                throw new InvalidOperationException(
                    "Drone damage starts from stale integrity state.");
            drones[current.DroneId] = damage.Resulting;
        }

        public ActorDroneAttackRecord PrepareActorAttack(
            string attackerId,
            string droneId,
            DroneExposureSnapshot exposure,
            float distance,
            uint resolutionSeed,
            DroneCrashTrajectoryRecord crashTrajectory = null)
        {
            SummonedDroneSnapshot drone = GetDrone(droneId);
            if (!drone.IsOperational)
                throw new InvalidOperationException(
                    "Destroyed drones cannot be attacked again.");
            if (gameplay.Mode != GameplaySessionMode.TurnBased
                || gameplay.Operation != GameplaySessionOperation.None
                || !string.Equals(gameplay.ActiveActorId, attackerId,
                    StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Only the idle active actor can attack a drone.");
            AttackDefinition attack = gameplay.GetEquippedAttack(attackerId)
                ?? throw new InvalidOperationException(
                    "Actor has no equipped attack.");
            GameplayActorSnapshot attacker = gameplay.GetActor(attackerId);
            if (!GameplayInjuryCapabilityProjection.CanUseAttack(
                    attacker.Capabilities,
                    attack))
                throw new InvalidOperationException(
                    "Actor injuries prevent use of the equipped weapon.");
            return DroneDirectAttackRules.Resolve(
                gameplay.LastActionSequence + 1L,
                resolutionSeed,
                attackerId,
                attack,
                attacker.TurnBudget,
                exposure,
                distance,
                drone,
                GameplayInjuryCapabilityProjection
                    .CalculateAccuracyDeltaPercent(attacker.Capabilities),
                crashTrajectory);
        }

        public void CommitCrashImpact(
            string advancingActorId,
            DroneCrashImpactRecord impact,
            IEnumerable<GameplayEvidenceRecord> evidence = null)
        {
            if (impact == null) throw new ArgumentNullException(nameof(impact));
            if (!canonicalProjectionBound)
                throw new InvalidOperationException(
                    "Drone crash impact requires the canonical semantic runtime.");
            canonicalExecutor(
                new GameplayDroneCrashImpactTransitionPayload(
                    advancingActorId,
                    impact),
                evidence);
        }

        public void CommitActorAttack(ActorDroneAttackRecord record)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));
            if (canonicalProjectionBound)
            {
                canonicalExecutor(
                    new GameplayActorDroneAttackTransitionPayload(
                        gameplay.GetEquippedAttack(record.AttackerId),
                        record),
                    null);
                return;
            }
            RequireLegacyMutationAllowed(nameof(CommitActorAttack));
            SummonedDroneSnapshot drone = GetDrone(record.DroneId);
            if (record.Damage != null
                && !StatesMatch(drone, record.Damage.Previous))
                throw new InvalidOperationException(
                    "Actor-drone attack starts from stale drone state.");
            gameplay.CommitActorDroneAttack(record);
            if (record.Damage != null)
                drones[drone.DroneId] = record.Damage.Resulting;
        }

        private void RequireSummonerPartnerTurn(SummonedDroneSnapshot drone)
        {
            if (gameplay.Mode != GameplaySessionMode.TurnBased
                || gameplay.Operation != GameplaySessionOperation.None
                || !string.Equals(
                    gameplay.ActiveActorId,
                    drone.SummonerActorId,
                    StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Drone commands require the summoner partner's idle turn.");
        }

        private static bool StatesMatch(SummonedDroneSnapshot left, SummonedDroneSnapshot right) =>
            string.Equals(left.DroneId, right.DroneId, StringComparison.Ordinal)
            && left.Position.DistanceTo(right.Position) == 0f
            && left.FacingDegrees == right.FacingDegrees
            && left.RemainingIntegrity == right.RemainingIntegrity
            && left.Lifecycle == right.Lifecycle
            && left.RemainingDurationTurns == right.RemainingDurationTurns;

        private void RequireLegacyMutationAllowed(string operation)
        {
            if (canonicalProjectionBound)
                throw new InvalidOperationException(
                    $"Legacy drone mutation '{operation}' is disabled while the semantic runtime owns state.");
        }
    }
}
