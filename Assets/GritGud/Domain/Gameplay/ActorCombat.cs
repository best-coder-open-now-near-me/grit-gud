using System;
using System.Collections.Generic;

namespace GritGud.Domain.Gameplay
{
    public sealed class EnemyBehaviorDefinition
    {
        public EnemyBehaviorDefinition(
            string behaviorId,
            float perceptionRange,
            float viewAngleDegrees,
            float preferredEngagementRange,
            float movementSearchRadius,
            int maximumAttacksPerTurn,
            int minimumAttackHitChancePercent = 25,
            EncounterAwarenessPolicyDefinition awarenessPolicy = null,
            PatrolRouteDefinition patrolRoute = null,
            IEnumerable<string> reinforcementActorIds = null)
        {
            if (string.IsNullOrWhiteSpace(behaviorId))
                throw new ArgumentException(
                    "Enemy behaviors require a stable identifier.",
                    nameof(behaviorId));
            RequireFinitePositive(perceptionRange, nameof(perceptionRange));
            RequireFinitePositive(viewAngleDegrees, nameof(viewAngleDegrees));
            if (viewAngleDegrees > 360f)
                throw new ArgumentOutOfRangeException(nameof(viewAngleDegrees));
            RequireFinitePositive(
                preferredEngagementRange,
                nameof(preferredEngagementRange));
            RequireFinitePositive(
                movementSearchRadius,
                nameof(movementSearchRadius));
            if (preferredEngagementRange > perceptionRange)
                throw new ArgumentException(
                    "Preferred engagement range cannot exceed perception range.",
                    nameof(preferredEngagementRange));
            if (maximumAttacksPerTurn <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(maximumAttacksPerTurn));
            if (minimumAttackHitChancePercent < 0
                || minimumAttackHitChancePercent > 100)
                throw new ArgumentOutOfRangeException(
                    nameof(minimumAttackHitChancePercent));

            BehaviorId = behaviorId;
            PerceptionRange = perceptionRange;
            ViewAngleDegrees = viewAngleDegrees;
            PreferredEngagementRange = preferredEngagementRange;
            MovementSearchRadius = movementSearchRadius;
            MaximumAttacksPerTurn = maximumAttacksPerTurn;
            MinimumAttackHitChancePercent = minimumAttackHitChancePercent;
            AwarenessPolicy = awarenessPolicy
                ?? EncounterAwarenessPolicyDefinition.CreateLegacyDefault();
            PatrolRoute = patrolRoute;
            ReinforcementActorIds = CopyReinforcements(reinforcementActorIds);
        }

        public string BehaviorId { get; }

        public float PerceptionRange { get; }

        public float ViewAngleDegrees { get; }

        public float PreferredEngagementRange { get; }

        public float MovementSearchRadius { get; }

        public int MaximumAttacksPerTurn { get; }

        public int MinimumAttackHitChancePercent { get; }

        public EncounterAwarenessPolicyDefinition AwarenessPolicy { get; }

        public PatrolRouteDefinition PatrolRoute { get; }

        public IReadOnlyList<string> ReinforcementActorIds { get; }

        private static IReadOnlyList<string> CopyReinforcements(
            IEnumerable<string> values)
        {
            var copy = new List<string>();
            var unique = new HashSet<string>(StringComparer.Ordinal);
            foreach (string value in values ?? Array.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(value) || !unique.Add(value))
                {
                    throw new ArgumentException(
                        "Enemy reinforcement IDs must be unique and non-empty.",
                        nameof(values));
                }
                copy.Add(value);
            }
            return copy.AsReadOnly();
        }

        private static void RequireFinitePositive(float value, string name)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f)
                throw new ArgumentOutOfRangeException(name);
        }
    }

    public sealed class ActorCombatDefinition
    {
        private readonly IReadOnlyList<string> hostileAllegiances;

        public ActorCombatDefinition(
            string allegianceId,
            IEnumerable<string> hostileAllegianceIds,
            int maximumWounds,
            EnemyBehaviorDefinition enemyBehavior = null)
        {
            if (string.IsNullOrWhiteSpace(allegianceId))
                throw new ArgumentException(
                    "Combatants require an allegiance identifier.",
                    nameof(allegianceId));
            if (maximumWounds <= 0)
                throw new ArgumentOutOfRangeException(nameof(maximumWounds));

            var copy = new List<string>();
            var unique = new HashSet<string>(StringComparer.Ordinal);
            foreach (string hostileId in hostileAllegianceIds
                ?? Array.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(hostileId))
                    throw new ArgumentException(
                        "Hostile allegiance identifiers cannot be empty.",
                        nameof(hostileAllegianceIds));
                if (string.Equals(hostileId, allegianceId, StringComparison.Ordinal))
                    throw new ArgumentException(
                        "An allegiance cannot be hostile to itself.",
                        nameof(hostileAllegianceIds));
                if (!unique.Add(hostileId))
                    throw new ArgumentException(
                        $"Hostile allegiance '{hostileId}' is duplicated.",
                        nameof(hostileAllegianceIds));
                copy.Add(hostileId);
            }

            AllegianceId = allegianceId;
            hostileAllegiances = copy.AsReadOnly();
            MaximumWounds = maximumWounds;
            EnemyBehavior = enemyBehavior;
        }

        public string AllegianceId { get; }

        public IReadOnlyList<string> HostileAllegianceIds =>
            hostileAllegiances;

        public int MaximumWounds { get; }

        public EnemyBehaviorDefinition EnemyBehavior { get; }

        public bool IsHostileTo(string allegianceId)
        {
            foreach (string hostileId in hostileAllegiances)
                if (string.Equals(
                    hostileId,
                    allegianceId,
                    StringComparison.Ordinal))
                    return true;
            return false;
        }

        internal static ActorCombatDefinition CreateLegacyNeutral() =>
            new ActorCombatDefinition(
                "neutral",
                Array.Empty<string>(),
                int.MaxValue);
    }

    public enum EnemyTacticalDecisionKind
    {
        Detect,
        Attack,
        Move,
        PushOff,
        EndTurn,
    }

    public sealed class EnemyMovementOption
    {
        public EnemyMovementOption(
            MovementRouteRecord route,
            TargetExposureSnapshot resultingExposure,
            float resultingTargetDistance)
        {
            Route = route ?? throw new ArgumentNullException(nameof(route));
            ResultingExposure = resultingExposure ?? throw new ArgumentNullException(
                nameof(resultingExposure));
            if (float.IsNaN(resultingTargetDistance)
                || float.IsInfinity(resultingTargetDistance)
                || resultingTargetDistance < 0f)
                throw new ArgumentOutOfRangeException(
                    nameof(resultingTargetDistance));
            if (!string.Equals(
                route.ActorId,
                resultingExposure.ObserverId,
                StringComparison.Ordinal))
                throw new ArgumentException(
                    "Movement and exposure observers must match.",
                    nameof(resultingExposure));
            ResultingTargetDistance = resultingTargetDistance;
        }

        public MovementRouteRecord Route { get; }

        public TargetExposureSnapshot ResultingExposure { get; }

        public float ResultingTargetDistance { get; }
    }

    public sealed class EnemyTacticalDecisionRecord
    {
        public EnemyTacticalDecisionRecord(
            long sequence,
            EnemyTacticalDecisionKind kind,
            string actorId,
            string targetId,
            TargetExposureSnapshot exposure,
            MovementRouteRecord movementRoute,
            string rationale)
        {
            if (sequence <= 0)
                throw new ArgumentOutOfRangeException(nameof(sequence));
            if (!Enum.IsDefined(typeof(EnemyTacticalDecisionKind), kind))
                throw new ArgumentOutOfRangeException(nameof(kind));
            if (string.IsNullOrWhiteSpace(actorId))
                throw new ArgumentException(
                    "Enemy decisions require an actor identifier.",
                    nameof(actorId));
            if (string.IsNullOrWhiteSpace(targetId))
                throw new ArgumentException(
                    "Enemy decisions require a target identifier.",
                    nameof(targetId));
            if (string.IsNullOrWhiteSpace(rationale))
                throw new ArgumentException(
                    "Enemy decisions require a rationale.",
                    nameof(rationale));
            if (exposure != null
                && (!string.Equals(
                        exposure.ObserverId,
                        actorId,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        exposure.TargetId,
                        targetId,
                        StringComparison.Ordinal)))
                throw new ArgumentException(
                    "Enemy decision exposure does not match its actors.",
                    nameof(exposure));
            if ((kind == EnemyTacticalDecisionKind.Attack
                    || kind == EnemyTacticalDecisionKind.Detect)
                && exposure == null)
                throw new ArgumentException(
                    "Detection and attack decisions require exposure evidence.",
                    nameof(exposure));
            if ((kind == EnemyTacticalDecisionKind.Move) !=
                (movementRoute != null))
                throw new ArgumentException(
                    "Only movement decisions carry a route.",
                    nameof(movementRoute));
            if (movementRoute != null
                && !string.Equals(
                    movementRoute.ActorId,
                    actorId,
                    StringComparison.Ordinal))
                throw new ArgumentException(
                    "Enemy decision routes must belong to the deciding actor.",
                    nameof(movementRoute));

            Sequence = sequence;
            Kind = kind;
            ActorId = actorId;
            TargetId = targetId;
            Exposure = exposure;
            MovementRoute = movementRoute;
            Rationale = rationale;
        }

        public long Sequence { get; }

        public EnemyTacticalDecisionKind Kind { get; }

        public string ActorId { get; }

        public string TargetId { get; }

        public TargetExposureSnapshot Exposure { get; }

        public MovementRouteRecord MovementRoute { get; }

        public string Rationale { get; }
    }
}
