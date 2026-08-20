using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;

namespace GritGud.Application.Gameplay
{
    public enum AttackResolutionFailure
    {
        None,
        TurnModeRequired,
        ActorNotActive,
        ActorIncapacitated,
        ActorPinned,
        OperationInProgress,
        AttackUnavailable,
        TargetNotFound,
        TargetIncapacitated,
        ExposureMismatch,
        TargetRequired,
        TargetOutOfReach,
        WorldStateChanged,
        InsufficientActionPoints,
        InsufficientMovementOpportunity,
    }

    public sealed class GameplayAttackSession
    {
        private readonly GameplaySession gameplay;
        private readonly DestructiblePropSession destructibles;
        private readonly ScenarioRunIdentity runIdentity;
        private readonly GameplayActorAttackTransitionPreparer attackPreparer;
        private readonly List<AttackResolutionRecord> records =
            new List<AttackResolutionRecord>();
        private readonly IReadOnlyList<AttackResolutionRecord> readOnlyRecords;
        private readonly List<WeaponDischargeRecord> discharges =
            new List<WeaponDischargeRecord>();
        private readonly IReadOnlyList<WeaponDischargeRecord>
            readOnlyDischarges;

        public GameplayAttackSession(
            GameplaySession gameplaySession,
            DestructiblePropSession destructibleSession = null,
            IGameplayTacticalContextQuery tacticalContextQuery = null,
            GameplayTacticalContextEvaluator tacticalContextEvaluator = null)
        {
            gameplay = gameplaySession ??
                throw new ArgumentNullException(nameof(gameplaySession));
            runIdentity = gameplay.RunIdentity;
            destructibles = destructibleSession;
            attackPreparer = new GameplayActorAttackTransitionPreparer(
                gameplay.Scenario,
                tacticalContextQuery,
                tacticalContextEvaluator);
            if (destructibles != null
                && !ReferenceEquals(gameplay.Journal, destructibles.Journal))
            {
                throw new ArgumentException(
                    "Direct-fire attacks and destructibles must share one gameplay journal.",
                    nameof(destructibleSession));
            }
            readOnlyRecords = records.AsReadOnly();
            readOnlyDischarges = discharges.AsReadOnly();
        }

        public IReadOnlyList<AttackResolutionRecord> Records => readOnlyRecords;

        public IReadOnlyList<WeaponDischargeRecord> Discharges =>
            readOnlyDischarges;

        public AttackResolutionFailure EvaluateResolve(
            string actorId,
            TargetExposureSnapshot exposure)
        {
            GameplayCombatStateSnapshot state = CaptureCombatState();
            return attackPreparer.TryEvaluate(
                state,
                actorId,
                exposure,
                gameplay.CanEnterTurnMode,
                out _,
                out AttackResolutionFailure failure)
                    ? AttackResolutionFailure.None
                    : failure;
        }

        public AttackResolutionFailure EvaluateDischarge(
            string actorId,
            string targetId,
            GameplayPosition aimPoint) =>
            EvaluateDischarge(actorId, targetId, aimPoint, impact: null);

        public AttackResolutionFailure EvaluateDischarge(
            string actorId,
            string targetId,
            GameplayPosition aimPoint,
            DirectFireImpactRecord impact)
        {
            return TryPrepareDischarge(
                actorId,
                targetId,
                aimPoint,
                impact,
                out _,
                out _,
                out _,
                out AttackResolutionFailure failure)
                    ? AttackResolutionFailure.None
                    : failure;
        }

        public bool TryResolve(
            string actorId,
            TargetExposureSnapshot exposure,
            out GameplayActionRecord action,
            out AttackResolutionFailure failure)
        {
            action = null;
            if (!TryPrepareResolve(
                    actorId,
                    exposure,
                    out GameplayPreparedTransition<GameplayActionRecord> prepared,
                    out failure))
                return false;
            action = prepared.Record;
            CommitPrepared(prepared);
            return true;
        }

        public bool TryPrepareResolve(
            string actorId,
            TargetExposureSnapshot exposure,
            out GameplayPreparedTransition<GameplayActionRecord> prepared,
            out AttackResolutionFailure failure)
        {
            prepared = null;
            GameplayCombatStateSnapshot previous = CaptureCombatState();
            if (!attackPreparer.TryEvaluate(
                    previous,
                    actorId,
                    exposure,
                    gameplay.CanEnterTurnMode,
                    out GameplayActorAttackEvaluation evaluation,
                    out failure))
            {
                return false;
            }
            prepared = attackPreparer.Resolve(previous, evaluation);
            failure = AttackResolutionFailure.None;
            return true;
        }

        public bool TryDischarge(
            string actorId,
            GameplayPosition aimPoint,
            out GameplayActionRecord action,
            out AttackResolutionFailure failure) =>
            TryDischarge(
                actorId,
                GameplayTargetIds.WorldAimPoint,
                aimPoint,
                out action,
                out failure);

        public bool TryDischarge(
            string actorId,
            string targetId,
            GameplayPosition aimPoint,
            out GameplayActionRecord action,
            out AttackResolutionFailure failure) =>
            TryDischarge(
                actorId,
                targetId,
                aimPoint,
                impact: null,
                out action,
                out failure);

        public bool TryDischarge(
            string actorId,
            string targetId,
            GameplayPosition aimPoint,
            DirectFireImpactRecord impact,
            out GameplayActionRecord action,
            out AttackResolutionFailure failure)
        {
            action = null;
            if (!TryPrepareDischarge(
                    actorId,
                    targetId,
                    aimPoint,
                    impact,
                    out GameplayPreparedTransition<GameplayActionRecord> prepared,
                    out failure))
                return false;
            action = prepared.Record;
            CommitPrepared(prepared);
            return true;
        }

        public bool TryPrepareDischarge(
            string actorId,
            string targetId,
            GameplayPosition aimPoint,
            out GameplayPreparedTransition<GameplayActionRecord> prepared,
            out AttackResolutionFailure failure) =>
            TryPrepareDischarge(
                actorId,
                targetId,
                aimPoint,
                impact: null,
                out prepared,
                out failure);

        public bool TryPrepareDischarge(
            string actorId,
            string targetId,
            GameplayPosition aimPoint,
            DirectFireImpactRecord impact,
            out GameplayPreparedTransition<GameplayActionRecord> prepared,
            out AttackResolutionFailure failure)
        {
            prepared = null;
            if (!TryPrepareDischarge(
                    actorId,
                    targetId,
                    aimPoint,
                    impact,
                    out AttackDefinition attack,
                    out GameplayActorSnapshot actor,
                    out ActionCost cost,
                    out failure))
            {
                return false;
            }

            GameplayCombatStateSnapshot previous = CaptureCombatState();
            long actionSequence = gameplay.NextActionSequence;
            DestructibleDamageRecord damage = PrepareDirectFireDamage(
                attack,
                targetId,
                impact,
                actionSequence);
            var discharge = new WeaponDischargeRecord(
                actionSequence,
                actorId,
                attack.ActionId,
                targetId,
                actor.Pose.Position,
                aimPoint,
                impact,
                damage);
            TurnBudget resultingBudget = actor.TurnBudget.SpendAction(cost);
            var action = new GameplayActionRecord(
                actionSequence,
                new GameplayActionRequest(
                    actorId,
                    attack.ActionId,
                    targetId),
                cost,
                actor.TurnBudget,
                resultingBudget,
                new[] { new WeaponDischargedActionOutcome(discharge) });
            prepared = new GameplayPreparedTransition<GameplayActionRecord>(
                action,
                previous,
                GameplayWeaponActionStateProjector.Project(previous, action));
            failure = AttackResolutionFailure.None;
            return true;
        }

        public GameplayTransitionCommitResult CommitPrepared(
            GameplayPreparedTransition<GameplayActionRecord> prepared) =>
            GameplayTransitionCoordinator.Commit(
                prepared,
                CaptureCombatState,
                Commit);

        public void Commit(GameplayActionRecord action)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            if (action.Outcomes.Count != 1)
            {
                throw new ArgumentException(
                    "Recorded weapon actions require exactly one outcome.",
                    nameof(action));
            }


            if (action.Outcomes[0] is WeaponDischargedActionOutcome discharge)
            {
                CommitDischarge(action, discharge.Discharge);
                return;
            }

            if (!(action.Outcomes[0] is AttackResolvedActionOutcome outcome))
            {
                throw new ArgumentException(
                    "Recorded weapon actions require an attack or discharge outcome.",
                    nameof(action));
            }

            AttackResolutionRecord attack = outcome.Attack;
            ValidateTacticalContext(action, attack);
            if (attack.Sequence != action.Sequence)
            {
                throw new InvalidOperationException(
                    "The attack does not share its authoritative action sequence.");
            }

            uint expectedSeed = GameplayAddressedRandom.SampleUInt32(
                runIdentity,
                new GameplayTransitionIdentity(
                    action.Sequence,
                    GameplaySemanticCapability.DirectAttack.ToString(),
                    action.Request.ActorId,
                    action.Request.TargetId),
                "resolution");
            if (attack.ResolutionSeed != expectedSeed)
            {
                throw new InvalidOperationException(
                    "The attack seed does not match its scenario stream.");
            }

            var notifications = new GameplayNotificationBatch();
            gameplay.CommitAction(action, notifications);
            records.Add(attack);
            notifications.Publish();
        }

        private void ValidateTacticalContext(
            GameplayActionRecord action,
            AttackResolutionRecord attack)
        {
            if (!ReferenceEquals(action.Context, attack.Context))
                throw new InvalidOperationException(
                    "Committed attack context does not match its action record.");
            if (action.Context == null) return;
            if (!(action.Context is ResolvedTacticalContext context))
                throw new InvalidOperationException(
                    "Committed direct attacks require resolved tactical context.");
            if (context.StateRevision != gameplay.Revision)
                throw new InvalidOperationException(
                    "Committed tactical evidence does not match current world revision.");

            AttackDefinition definition = gameplay.GetEquippedAttack(
                action.Request.ActorId);
            string expectedSignature = GameplayCapabilityProfiles.Attack(
                definition,
                GameplaySemanticSubjectKind.Actor).Signature;
            if (!string.Equals(
                    context.CapabilitySignature,
                    expectedSignature,
                    StringComparison.Ordinal)
                || !string.Equals(
                    context.SubjectKind,
                    GameplaySemanticSubjectKind.Actor.ToString(),
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Committed tactical context does not match the direct-attack capability.");
            }

            var reconstructed = new ResolvedTacticalContext(
                context.Snapshot,
                context.Modifiers);
            if (reconstructed.AccuracyDeltaPercent
                    != context.AccuracyDeltaPercent
                || !string.Equals(
                    reconstructed.CanonicalDigest,
                    context.CanonicalDigest,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Committed tactical context consequences are not canonical.");
            }
        }

        private void CommitDischarge(
            GameplayActionRecord action,
            WeaponDischargeRecord discharge)
        {
            if (discharge.Sequence != action.Sequence)
            {
                throw new InvalidOperationException(
                    "The discharge does not share its canonical action sequence.");
            }

            var notifications = new GameplayNotificationBatch();
            if (discharge.Damage != null)
            {
                if (destructibles == null)
                {
                    throw new InvalidOperationException(
                        "Direct-fire prop damage requires a destructible session.");
                }

                destructibles.ValidateDamage(discharge.Damage);
            }
            gameplay.CommitAction(action, notifications);
            if (discharge.Damage != null)
            {
                destructibles.CommitDamage(discharge.Damage, notifications);
            }
            discharges.Add(discharge);
            notifications.Publish();
        }

        private DestructibleDamageRecord PrepareDirectFireDamage(
            AttackDefinition attack,
            string targetId,
            DirectFireImpactRecord impact,
            long actionSequence)
        {
            if (destructibles == null
                || impact == null
                || attack.DirectFireDamage == null
                || !destructibles.TryGetProp(targetId, out _))
            {
                return null;
            }

            float requestedDamage = attack.DirectFireDamage
                .EvaluateIntegrityDamage(impact.SurfaceId);
            return requestedDamage > 0f
                && destructibles.TryPrepareDamage(
                    targetId,
                    requestedDamage,
                    impact.PreferredFractureChunkIndex,
                    actionSequence,
                    out DestructibleDamageRecord damage)
                ? damage
                : null;
        }

        private GameplayCombatStateSnapshot CaptureCombatState() =>
            GameplayCombatStateCapture.Capture(gameplay, destructibles);

        private bool TryPrepareActor(
            string actorId,
            bool startsEncounter,
            out AttackDefinition attack,
            out GameplayActorSnapshot actor,
            out ActionCost cost,
            out AttackResolutionFailure failure)
        {
            attack = null;
            actor = default;
            cost = default;

            if (gameplay.Operation != GameplaySessionOperation.None)
            {
                failure = AttackResolutionFailure.OperationInProgress;
                return false;
            }

            if ((gameplay.Mode == GameplaySessionMode.TurnBased
                    && !string.Equals(
                        gameplay.ActiveActorId,
                        actorId,
                        StringComparison.Ordinal))
                || !gameplay.TryGetActor(actorId, out actor))
            {
                failure = AttackResolutionFailure.ActorNotActive;
                return false;
            }

            if (gameplay.IsActorIncapacitated(actorId))
            {
                failure = AttackResolutionFailure.ActorIncapacitated;
                return false;
            }
            if (actor.IsPinned)
            {
                failure = AttackResolutionFailure.ActorPinned;
                return false;
            }

            if (startsEncounter
                && !gameplay.EncounterActive
                && gameplay.Mode == GameplaySessionMode.Exploration
                && !gameplay.CanEnterTurnMode)
            {
                failure = AttackResolutionFailure.TurnModeRequired;
                return false;
            }

            attack = gameplay.GetEquippedAttack(actorId);
            if (attack == null || attack.Projectile != null)
            {
                failure = AttackResolutionFailure.AttackUnavailable;
                return false;
            }

            cost = gameplay.Mode == GameplaySessionMode.TurnBased
                || startsEncounter
                ? attack.TurnCost
                : new ActionCost(
                    0,
                    0f,
                    attack.TurnCost.Mobility);
            if (actor.TurnBudget.ActionPoints < cost.ActionPoints)
            {
                failure = AttackResolutionFailure.InsufficientActionPoints;
                return false;
            }

            if (actor.TurnBudget.MovementOpportunity < cost.MovementOpportunity)
            {
                failure = AttackResolutionFailure.InsufficientMovementOpportunity;
                return false;
            }

            failure = AttackResolutionFailure.None;
            return true;
        }

        private bool TryPrepareDischarge(
            string actorId,
            string targetId,
            GameplayPosition aimPoint,
            DirectFireImpactRecord impact,
            out AttackDefinition attack,
            out GameplayActorSnapshot actor,
            out ActionCost cost,
            out AttackResolutionFailure failure)
        {
            attack = null;
            actor = default;
            cost = default;
            if (string.IsNullOrWhiteSpace(targetId))
            {
                failure = AttackResolutionFailure.TargetNotFound;
                return false;
            }

            if (impact != null
                && (!string.Equals(
                        impact.TargetId,
                        targetId,
                        StringComparison.Ordinal)
                    || impact.Point.DistanceTo(aimPoint) > 0.0001f))
            {
                failure = AttackResolutionFailure.TargetNotFound;
                return false;
            }

            long currentRevision = gameplay.Journal.LastEntry?.Sequence ?? 0L;
            if (impact != null
                && impact.WorldStateRevision != currentRevision)
            {
                failure = AttackResolutionFailure.WorldStateChanged;
                return false;
            }

            if (!TryPrepareActor(
                    actorId,
                    gameplay.AttackStartsEncounter(targetId),
                    out attack,
                    out actor,
                    out cost,
                    out failure))
            {
                return false;
            }

            if (!attack.CanTargetWorldPoint)
            {
                failure = AttackResolutionFailure.TargetRequired;
                return false;
            }

            if (actor.Pose.Position.DistanceTo(aimPoint) <= 0f)
            {
                failure = AttackResolutionFailure.TargetNotFound;
                return false;
            }

            failure = AttackResolutionFailure.None;
            return true;
        }

    }
}
