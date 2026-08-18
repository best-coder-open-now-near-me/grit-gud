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
        private readonly List<AttackResolutionRecord> records =
            new List<AttackResolutionRecord>();
        private readonly IReadOnlyList<AttackResolutionRecord> readOnlyRecords;
        private readonly List<WeaponDischargeRecord> discharges =
            new List<WeaponDischargeRecord>();
        private readonly IReadOnlyList<WeaponDischargeRecord>
            readOnlyDischarges;

        public GameplayAttackSession(
            GameplaySession gameplaySession,
            uint authoredScenarioSeed,
            DestructiblePropSession destructibleSession = null)
        {
            gameplay = gameplaySession ??
                throw new ArgumentNullException(nameof(gameplaySession));
            if (gameplay.RunIdentity.ScenarioSeed != authoredScenarioSeed)
            {
                throw new ArgumentException(
                    "Attack randomness must use the gameplay run seed.",
                    nameof(authoredScenarioSeed));
            }
            runIdentity = gameplay.RunIdentity;
            destructibles = destructibleSession;
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
            return TryPrepare(
                actorId,
                exposure,
                out _,
                out _,
                out _,
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
            if (!TryPrepare(
                    actorId,
                    exposure,
                    out AttackDefinition attack,
                    out GameplayActorSnapshot actor,
                    out GameplayActorSnapshot target,
                    out ActionCost cost,
                    out failure))
            {
                return false;
            }

            GameplayCombatStateSnapshot previous = CaptureCombatState();
            long attackSequence = records.Count + 1L;
            long actionSequence = gameplay.NextActionSequence;
            var transition = new GameplayTransitionIdentity(
                actionSequence,
                "direct-attack",
                actorId,
                target.ActorId);
            uint resolutionSeed = GameplayAddressedRandom.SampleUInt32(
                runIdentity,
                transition,
                "resolution");
            AttackResolutionRecord resolution = AttackResolutionRules.Resolve(
                attackSequence,
                resolutionSeed,
                exposure,
                attack.AccuracyDecay,
                actor.Pose.Position.DistanceTo(target.Pose.Position),
                target.Wounds,
                attack.WoundMovementPenalty,
                attack.Contact);
            TurnBudget resultingBudget = actor.TurnBudget.SpendAction(cost);
            var action = new GameplayActionRecord(
                actionSequence,
                new GameplayActionRequest(
                    actorId,
                    attack.ActionId,
                    target.ActorId),
                cost,
                actor.TurnBudget,
                resultingBudget,
                new[] { new AttackResolvedActionOutcome(resolution) });
            prepared = new GameplayPreparedTransition<GameplayActionRecord>(
                action,
                previous,
                GameplayWeaponActionStateProjector.Project(previous, action));
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
            DestructibleDamageRecord damage = PrepareDirectFireDamage(
                attack,
                targetId,
                impact);
            var discharge = new WeaponDischargeRecord(
                discharges.Count + 1L,
                actorId,
                attack.ActionId,
                targetId,
                actor.Pose.Position,
                aimPoint,
                impact,
                damage);
            TurnBudget resultingBudget = actor.TurnBudget.SpendAction(cost);
            long actionSequence = gameplay.LastResolvedAction == null
                ? 1L
                : gameplay.LastResolvedAction.Sequence + 1L;
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
            long expectedSequence = records.Count + 1L;
            if (attack.Sequence != expectedSequence)
            {
                throw new InvalidOperationException(
                    "The attack is not the next authoritative attack sequence.");
            }

            uint expectedSeed = GameplayAddressedRandom.SampleUInt32(
                runIdentity,
                new GameplayTransitionIdentity(
                    action.Sequence,
                    "direct-attack",
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

        private void CommitDischarge(
            GameplayActionRecord action,
            WeaponDischargeRecord discharge)
        {
            long expectedSequence = discharges.Count + 1L;
            if (discharge.Sequence != expectedSequence)
            {
                throw new InvalidOperationException(
                    "The discharge is not the next authoritative discharge sequence.");
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
            DirectFireImpactRecord impact)
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
                    out DestructibleDamageRecord damage)
                ? damage
                : null;
        }

        private GameplayCombatStateSnapshot CaptureCombatState() =>
            GameplayCombatStateCapture.Capture(gameplay, destructibles);

        private bool TryPrepare(
            string actorId,
            TargetExposureSnapshot exposure,
            out AttackDefinition attack,
            out GameplayActorSnapshot actor,
            out GameplayActorSnapshot target,
            out ActionCost cost,
            out AttackResolutionFailure failure)
        {
            attack = null;
            actor = default;
            target = default;
            cost = default;
            bool startsEncounter = exposure != null
                && !string.IsNullOrWhiteSpace(exposure.TargetId)
                && gameplay.AttackStartsEncounter(exposure.TargetId);
            if (!TryPrepareActor(
                    actorId,
                    startsEncounter,
                    out attack,
                    out actor,
                    out cost,
                    out failure))
            {
                return false;
            }

            if (exposure == null
                || !string.Equals(
                    exposure.ObserverId,
                    actorId,
                    StringComparison.Ordinal))
            {
                failure = AttackResolutionFailure.ExposureMismatch;
                return false;
            }

            if (string.Equals(actorId, exposure.TargetId, StringComparison.Ordinal)
                || !gameplay.TryGetActor(exposure.TargetId, out target))
            {
                failure = AttackResolutionFailure.TargetNotFound;
                return false;
            }

            if (gameplay.IsActorIncapacitated(exposure.TargetId))
            {
                failure = AttackResolutionFailure.TargetIncapacitated;
                return false;
            }

            float distance = actor.Pose.Position.DistanceTo(
                target.Pose.Position);
            if (attack.Contact != null
                && distance > attack.Contact.MaximumReach + 0.0001f)
            {
                failure = AttackResolutionFailure.TargetOutOfReach;
                return false;
            }

            failure = AttackResolutionFailure.None;
            return true;
        }

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
