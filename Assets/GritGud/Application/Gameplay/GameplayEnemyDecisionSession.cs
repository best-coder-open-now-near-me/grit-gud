using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;

namespace GritGud.Application.Gameplay
{
    public sealed class EnemyTargetSelection
    {
        public EnemyTargetSelection(
            string targetId,
            TargetExposureSnapshot exposure,
            int hitChancePercent)
        {
            TargetId = targetId ?? throw new ArgumentNullException(nameof(targetId));
            Exposure = exposure ?? throw new ArgumentNullException(nameof(exposure));
            HitChancePercent = hitChancePercent;
        }

        public string TargetId { get; }

        public TargetExposureSnapshot Exposure { get; }

        public int HitChancePercent { get; }
    }

    public sealed class GameplayEnemyDecisionSession
    {
        private readonly GameplaySession gameplay;
        private readonly List<EnemyTacticalDecisionRecord> decisions =
            new List<EnemyTacticalDecisionRecord>();
        private readonly IReadOnlyList<EnemyTacticalDecisionRecord>
            readOnlyDecisions;

        public GameplayEnemyDecisionSession(GameplaySession gameplaySession)
        {
            gameplay = gameplaySession ?? throw new ArgumentNullException(
                nameof(gameplaySession));
            readOnlyDecisions = decisions.AsReadOnly();
        }

        public IReadOnlyList<EnemyTacticalDecisionRecord> Decisions =>
            readOnlyDecisions;

        public EnemyTacticalDecisionRecord EvaluateDetection(
            string actorId,
            string targetId,
            TargetExposureSnapshot exposure)
        {
            EnemyBehaviorDefinition behavior = RequireEnemy(actorId);
            ValidateTarget(actorId, targetId, exposure);
            float distance = gameplay.GetActor(actorId).Pose.Position.DistanceTo(
                gameplay.GetActor(targetId).Pose.Position);
            float viewAngle = CalculateViewAngle(
                gameplay.GetActor(actorId).Pose,
                gameplay.GetActor(targetId).Pose.Position);
            if (gameplay.IsActorIncapacitated(actorId)
                || gameplay.IsActorIncapacitated(targetId)
                || !gameplay.IsHostile(actorId, targetId)
                || distance > behavior.PerceptionRange
                || viewAngle > behavior.ViewAngleDegrees * 0.5f
                || exposure.VisibleSampleCount == 0)
            {
                return null;
            }

            return Create(
                EnemyTacticalDecisionKind.Detect,
                actorId,
                targetId,
                exposure,
                movementRoute: null,
                $"hostile visible at {distance:0.0} m and {viewAngle:0.#}\u00b0 off facing");
        }

        public EnemyTacticalDecisionRecord EvaluateFirstDetection(
            string actorId,
            IReadOnlyList<string> candidateTargetIds,
            Func<string, TargetExposureSnapshot> captureExposure)
        {
            RequireEnemy(actorId);
            if (candidateTargetIds == null)
                throw new ArgumentNullException(nameof(candidateTargetIds));
            if (captureExposure == null)
                throw new ArgumentNullException(nameof(captureExposure));

            foreach (string targetId in candidateTargetIds)
            {
                gameplay.GetActor(targetId);
                if (gameplay.IsActorIncapacitated(targetId)
                    || !gameplay.IsHostile(actorId, targetId))
                {
                    continue;
                }

                TargetExposureSnapshot exposure = captureExposure(targetId);
                EnemyTacticalDecisionRecord detection = EvaluateDetection(
                    actorId,
                    targetId,
                    exposure);
                if (detection != null)
                    return detection;
            }

            return null;
        }

        public EnemyTacticalDecisionRecord EvaluateBestDetection(
            string actorId,
            IReadOnlyList<string> candidateTargetIds,
            Func<string, TargetExposureSnapshot> captureExposure)
        {
            RequireEnemy(actorId);
            if (candidateTargetIds == null)
                throw new ArgumentNullException(nameof(candidateTargetIds));
            if (captureExposure == null)
                throw new ArgumentNullException(nameof(captureExposure));

            EnemyTacticalDecisionRecord best = null;
            float bestVisibleFraction = -1f;
            float bestDistance = float.PositiveInfinity;
            GameplayPosition observer = gameplay.GetActor(actorId).Pose.Position;
            foreach (string targetId in candidateTargetIds)
            {
                gameplay.GetActor(targetId);
                if (gameplay.IsActorIncapacitated(targetId)
                    || !gameplay.IsHostile(actorId, targetId))
                    continue;
                TargetExposureSnapshot exposure = captureExposure(targetId);
                EnemyTacticalDecisionRecord detection = EvaluateDetection(
                    actorId,
                    targetId,
                    exposure);
                if (detection == null)
                    continue;
                float visibleFraction = exposure.VisibleFraction;
                float distance = observer.DistanceTo(
                    gameplay.GetActor(targetId).Pose.Position);
                if (best == null
                    || visibleFraction > bestVisibleFraction
                    || (visibleFraction == bestVisibleFraction
                        && distance < bestDistance))
                {
                    best = detection;
                    bestVisibleFraction = visibleFraction;
                    bestDistance = distance;
                }
            }
            return best;
        }

        public string SelectNearestCapableHostile(string actorId)
        {
            RequireEnemy(actorId);
            GameplayPosition observer = gameplay.GetActor(actorId).Pose.Position;
            string nearest = null;
            float nearestDistance = float.PositiveInfinity;
            foreach (string candidateId in gameplay.InitiativeOrder)
            {
                if (gameplay.IsActorIncapacitated(candidateId)
                    || !gameplay.IsHostile(actorId, candidateId))
                {
                    continue;
                }

                float distance = observer.DistanceTo(
                    gameplay.GetActor(candidateId).Pose.Position);
                if (distance < nearestDistance)
                {
                    nearest = candidateId;
                    nearestDistance = distance;
                }
            }

            return nearest;
        }

        public EnemyTargetSelection SelectBestTarget(
            string actorId,
            IReadOnlyList<string> candidateTargetIds,
            Func<string, TargetExposureSnapshot> captureExposure)
        {
            RequireEnemy(actorId);
            if (candidateTargetIds == null)
                throw new ArgumentNullException(nameof(candidateTargetIds));
            if (captureExposure == null)
                throw new ArgumentNullException(nameof(captureExposure));
            GameplayActorSnapshot observer = gameplay.GetActor(actorId);
            AttackDefinition attack = gameplay.GetEquippedAttack(actorId);
            EnemyTargetSelection best = null;
            int bestWounds = -1;
            float bestDistance = float.PositiveInfinity;
            foreach (string targetId in candidateTargetIds)
            {
                GameplayActorSnapshot target = gameplay.GetActor(targetId);
                if (gameplay.IsActorIncapacitated(targetId)
                    || !gameplay.IsHostile(actorId, targetId))
                    continue;
                TargetExposureSnapshot exposure = captureExposure(targetId);
                ValidateTarget(actorId, targetId, exposure);
                float distance = observer.Pose.Position.DistanceTo(
                    target.Pose.Position);
                int hitChance = exposure.VisibleSampleCount == 0
                    ? 0
                    : CalculateHitChance(attack, exposure, distance);
                int wounds = target.Wounds.WoundCount;
                if (best == null
                    || hitChance > best.HitChancePercent
                    || (hitChance == best.HitChancePercent && wounds > bestWounds)
                    || (hitChance == best.HitChancePercent && wounds == bestWounds
                        && distance < bestDistance))
                {
                    best = new EnemyTargetSelection(targetId, exposure, hitChance);
                    bestWounds = wounds;
                    bestDistance = distance;
                }
            }
            return best;
        }

        public EnemyTacticalDecisionRecord EvaluateTurn(
            string actorId,
            string targetId,
            TargetExposureSnapshot currentExposure,
            IReadOnlyList<EnemyMovementOption> movementOptions,
            int attacksCommittedThisTurn)
        {
            EnemyBehaviorDefinition behavior = RequireEnemy(actorId);
            ValidateTarget(actorId, targetId, currentExposure);
            if (!string.Equals(
                    gameplay.ActiveActorId,
                    actorId,
                    StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"Enemy '{actorId}' is not the active actor.");
            if (attacksCommittedThisTurn < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(attacksCommittedThisTurn));

            if (gameplay.IsActorIncapacitated(actorId)
                || gameplay.IsActorIncapacitated(targetId)
                || !gameplay.IsHostile(actorId, targetId))
                return CreateEndTurn(
                    actorId,
                    targetId,
                    "no capable hostile target remains");

            GameplayActorSnapshot actor = gameplay.GetActor(actorId);
            GameplayActorSnapshot target = gameplay.GetActor(targetId);
            float distance = actor.Pose.Position.DistanceTo(
                target.Pose.Position);
            AttackDefinition attack = gameplay.GetEquippedAttack(actorId);
            bool attackAffordable = attack != null
                && attack.Projectile == null
                && actor.TurnBudget.ActionPoints
                    >= attack.TurnCost.ActionPoints
                && actor.TurnBudget.MovementOpportunity
                    >= attack.TurnCost.MovementOpportunity;
            bool targetInReach = attack?.Contact == null
                || distance <= attack.Contact.MaximumReach + 0.0001f;
            int currentHitChance = currentExposure.VisibleSampleCount == 0
                ? 0
                : CalculateHitChance(attack, currentExposure, distance);
            if (currentExposure.VisibleSampleCount > 0
                && attackAffordable
                && targetInReach
                && currentHitChance >= behavior.MinimumAttackHitChancePercent
                && attacksCommittedThisTurn
                    < behavior.MaximumAttacksPerTurn)
            {
                return Create(
                    EnemyTacticalDecisionKind.Attack,
                    actorId,
                    targetId,
                    currentExposure,
                    movementRoute: null,
                    $"target exposed at {distance:0.0} m; hit chance {currentHitChance}%");
            }

            if (attacksCommittedThisTurn
                >= behavior.MaximumAttacksPerTurn)
                return CreateEndTurn(
                    actorId,
                    targetId,
                    "authored attack limit reached");

            EnemyMovementOption movement = SelectMovement(
                behavior,
                attack,
                movementOptions,
                targetInReach ? currentHitChance : -1);
            if (movement != null)
            {
                return Create(
                    EnemyTacticalDecisionKind.Move,
                    actorId,
                    targetId,
                    movement.ResultingExposure,
                    movement.Route,
                    $"route improves attack position to {movement.ResultingTargetDistance:0.0} m");
            }

            if (currentExposure.VisibleSampleCount > 0
                && attackAffordable
                && targetInReach)
            {
                return Create(
                    EnemyTacticalDecisionKind.Attack,
                    actorId,
                    targetId,
                    currentExposure,
                    movementRoute: null,
                    $"no better firing position; taking {currentHitChance}% shot");
            }

            string rationale = attackAffordable
                    ? "no traversable firing position found"
                    : "attack unavailable or unaffordable";
            return CreateEndTurn(actorId, targetId, rationale);
        }

        public EnemyTacticalDecisionRecord EvaluatePushOff(
            string actorId,
            string propId)
        {
            RequireEnemy(actorId);
            GameplayActorSnapshot actor = gameplay.GetActor(actorId);
            if (!actor.IsPinned
                || !string.Equals(
                    actor.PinState.PropId,
                    propId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Enemy '{actorId}' is not pinned by prop '{propId}'.");
            }
            return Create(
                EnemyTacticalDecisionKind.PushOff,
                actorId,
                propId,
                exposure: null,
                movementRoute: null,
                "pinned actor must free itself before resuming tactics");
        }

        public bool RequiresMovementSearch(
            string actorId,
            string targetId,
            TargetExposureSnapshot currentExposure)
        {
            EnemyBehaviorDefinition behavior = RequireEnemy(actorId);
            ValidateTarget(actorId, targetId, currentExposure);
            GameplayActorSnapshot actor = gameplay.GetActor(actorId);
            float distance = actor.Pose.Position.DistanceTo(
                gameplay.GetActor(targetId).Pose.Position);
            AttackDefinition attack = gameplay.GetEquippedAttack(actorId);
            if (attack == null || actor.TurnBudget.MovementOpportunity <= 0f)
                return false;
            if (attack.Contact != null
                && distance > attack.Contact.MaximumReach + 0.0001f)
                return true;
            if (currentExposure.VisibleSampleCount == 0)
                return true;
            return CalculateHitChance(attack, currentExposure, distance)
                < behavior.MinimumAttackHitChancePercent;
        }

        public void Commit(EnemyTacticalDecisionRecord decision)
        {
            if (decision == null)
                throw new ArgumentNullException(nameof(decision));
            if (decision.Sequence != decisions.Count + 1L)
                throw new InvalidOperationException(
                    "Enemy decisions must commit in sequence.");
            gameplay.GetActor(decision.ActorId);
            if (decision.Kind != EnemyTacticalDecisionKind.PushOff)
                gameplay.GetActor(decision.TargetId);
            decisions.Add(decision);
            gameplay.Journal.RecordEnemyDecision(decision);
        }

        private EnemyTacticalDecisionRecord CreateEndTurn(
            string actorId,
            string targetId,
            string rationale) =>
            Create(
                EnemyTacticalDecisionKind.EndTurn,
                actorId,
                targetId,
                exposure: null,
                movementRoute: null,
                rationale);

        private EnemyTacticalDecisionRecord Create(
            EnemyTacticalDecisionKind kind,
            string actorId,
            string targetId,
            TargetExposureSnapshot exposure,
            MovementRouteRecord movementRoute,
            string rationale) =>
            new EnemyTacticalDecisionRecord(
                decisions.Count + 1L,
                kind,
                actorId,
                targetId,
                exposure,
                movementRoute,
                rationale);

        private EnemyBehaviorDefinition RequireEnemy(string actorId)
        {
            gameplay.GetActor(actorId);
            EnemyBehaviorDefinition behavior = gameplay.Scenario
                .GetActor(actorId)
                .Combat
                .EnemyBehavior;
            return behavior ?? throw new InvalidOperationException(
                $"Actor '{actorId}' has no enemy behavior.");
        }

        private void ValidateTarget(
            string actorId,
            string targetId,
            TargetExposureSnapshot exposure)
        {
            gameplay.GetActor(targetId);
            if (exposure == null
                || !string.Equals(
                    exposure.ObserverId,
                    actorId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    exposure.TargetId,
                    targetId,
                    StringComparison.Ordinal))
                throw new ArgumentException(
                    "Enemy exposure must describe the deciding actor and target.",
                    nameof(exposure));
        }

        private static EnemyMovementOption SelectMovement(
            EnemyBehaviorDefinition behavior,
            AttackDefinition attack,
            IReadOnlyList<EnemyMovementOption> options,
            int minimumHitChanceExclusive)
        {
            EnemyMovementOption best = null;
            int bestHitChance = -1;
            float bestVisibleFraction = -1f;
            float bestRangeError = float.PositiveInfinity;
            float bestCost = float.PositiveInfinity;
            foreach (EnemyMovementOption option in options
                ?? Array.Empty<EnemyMovementOption>())
            {
                if (option == null
                    || option.Route.TotalCost
                        > behavior.MovementSearchRadius + 0.0001f)
                    continue;
                float visibleFraction = option.ResultingExposure.VisibleFraction;
                int hitChance = attack == null
                    || attack.AccuracyDecay == null
                        ? (int)Math.Round(
                            visibleFraction * 100f,
                            MidpointRounding.AwayFromZero)
                        : AttackHitChanceRules.CalculateFinalHitChancePercent(
                            option.ResultingExposure,
                            attack.AccuracyDecay,
                            option.ResultingTargetDistance);
                if (hitChance <= minimumHitChanceExclusive)
                    continue;
                float rangeError = Math.Abs(
                    option.ResultingTargetDistance
                    - (attack?.Contact?.MaximumReach
                        ?? behavior.PreferredEngagementRange));
                float cost = option.Route.TotalCost;
                if (hitChance > bestHitChance
                    || (hitChance == bestHitChance
                        && visibleFraction > bestVisibleFraction)
                    || (hitChance == bestHitChance
                        && visibleFraction == bestVisibleFraction
                        && rangeError < bestRangeError)
                    || (hitChance == bestHitChance
                        && visibleFraction == bestVisibleFraction
                        && rangeError == bestRangeError
                        && cost < bestCost))
                {
                    best = option;
                    bestHitChance = hitChance;
                    bestVisibleFraction = visibleFraction;
                    bestRangeError = rangeError;
                    bestCost = cost;
                }
            }

            return best;
        }

        private static int CalculateHitChance(
            AttackDefinition attack,
            TargetExposureSnapshot exposure,
            float distance) => attack?.AccuracyDecay == null
                ? TargetExposureRules.CalculateHitChancePercent(exposure)
                : AttackHitChanceRules.CalculateFinalHitChancePercent(
                    exposure,
                    attack.AccuracyDecay,
                    distance);

        private static float CalculateViewAngle(
            GameplayActorPose observer,
            GameplayPosition target)
        {
            float deltaX = target.X - observer.Position.X;
            float deltaZ = target.Z - observer.Position.Z;
            if ((deltaX * deltaX) + (deltaZ * deltaZ) <= 0.000001f)
                return 0f;
            float bearing = (float)(Math.Atan2(deltaX, deltaZ)
                * (180d / Math.PI));
            float delta = ((bearing - observer.FacingDegrees + 540f) % 360f)
                - 180f;
            return Math.Abs(delta);
        }
    }
}
