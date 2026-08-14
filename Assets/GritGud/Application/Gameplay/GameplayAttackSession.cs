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
        OperationInProgress,
        AttackUnavailable,
        TargetNotFound,
        TargetIncapacitated,
        ExposureMismatch,
        TargetRequired,
        TargetOutOfReach,
        InsufficientActionPoints,
        InsufficientMovementOpportunity,
    }

    public sealed class GameplayAttackSession
    {
        private readonly GameplaySession gameplay;
        private readonly uint scenarioSeed;
        private readonly List<AttackResolutionRecord> records =
            new List<AttackResolutionRecord>();
        private readonly IReadOnlyList<AttackResolutionRecord> readOnlyRecords;
        private readonly List<WeaponDischargeRecord> discharges =
            new List<WeaponDischargeRecord>();
        private readonly IReadOnlyList<WeaponDischargeRecord>
            readOnlyDischarges;

        public GameplayAttackSession(
            GameplaySession gameplaySession,
            uint authoredScenarioSeed)
        {
            gameplay = gameplaySession ??
                throw new ArgumentNullException(nameof(gameplaySession));
            scenarioSeed = authoredScenarioSeed;
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
            GameplayPosition aimPoint)
        {
            return TryPrepareDischarge(
                actorId,
                targetId,
                aimPoint,
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

            long attackSequence = records.Count + 1L;
            uint resolutionSeed = AttackResolutionRules.DeriveResolutionSeed(
                scenarioSeed,
                attackSequence);
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
            long actionSequence = gameplay.LastResolvedAction == null
                ? 1L
                : gameplay.LastResolvedAction.Sequence + 1L;
            action = new GameplayActionRecord(
                actionSequence,
                new GameplayActionRequest(
                    actorId,
                    attack.ActionId,
                    target.ActorId),
                cost,
                actor.TurnBudget,
                resultingBudget,
                new[] { new AttackResolvedActionOutcome(resolution) });
            Commit(action);
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
            out AttackResolutionFailure failure)
        {
            action = null;
            if (!TryPrepareDischarge(
                    actorId,
                    targetId,
                    aimPoint,
                    out AttackDefinition attack,
                    out GameplayActorSnapshot actor,
                    out ActionCost cost,
                    out failure))
            {
                return false;
            }

            var discharge = new WeaponDischargeRecord(
                discharges.Count + 1L,
                actorId,
                attack.ActionId,
                targetId,
                actor.Pose.Position,
                aimPoint);
            TurnBudget resultingBudget = actor.TurnBudget.SpendAction(cost);
            long actionSequence = gameplay.LastResolvedAction == null
                ? 1L
                : gameplay.LastResolvedAction.Sequence + 1L;
            action = new GameplayActionRecord(
                actionSequence,
                new GameplayActionRequest(
                    actorId,
                    attack.ActionId,
                    targetId),
                cost,
                actor.TurnBudget,
                resultingBudget,
                new[] { new WeaponDischargedActionOutcome(discharge) });
            Commit(action);
            failure = AttackResolutionFailure.None;
            return true;
        }

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

            uint expectedSeed = AttackResolutionRules.DeriveResolutionSeed(
                scenarioSeed,
                expectedSequence);
            if (attack.ResolutionSeed != expectedSeed)
            {
                throw new InvalidOperationException(
                    "The attack seed does not match its scenario stream.");
            }

            gameplay.CommitAction(action);
            records.Add(attack);
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

            gameplay.CommitAction(action);
            discharges.Add(discharge);
        }

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
