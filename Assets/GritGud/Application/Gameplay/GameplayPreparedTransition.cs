using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;

namespace GritGud.Application.Gameplay
{
    public sealed class GameplayPreparedTransition<TRecord>
    {
        public GameplayPreparedTransition(
            TRecord record,
            GameplayCombatStateSnapshot previous,
            GameplayCombatStateSnapshot predicted)
        {
            Record = record;
            Previous = previous ?? throw new ArgumentNullException(nameof(previous));
            Predicted = predicted ?? throw new ArgumentNullException(nameof(predicted));
            IReadOnlyList<GameplayInvariantViolation> violations =
                GameplayCombatInvariantValidator.Validate(predicted);
            if (violations.Count > 0)
                throw new ArgumentException(
                    $"The predicted transition violates '{violations[0].Code}' at "
                    + $"'{violations[0].Path}'.",
                    nameof(predicted));
        }

        public TRecord Record { get; }
        public GameplayCombatStateSnapshot Previous { get; }
        public GameplayCombatStateSnapshot Predicted { get; }
    }

    public sealed class GameplayTransitionCommitResult
    {
        public GameplayTransitionCommitResult(
            GameplayCombatStateSnapshot actual,
            IReadOnlyList<GameplayStateDifference> differences)
        {
            Actual = actual ?? throw new ArgumentNullException(nameof(actual));
            Differences = differences ?? throw new ArgumentNullException(nameof(differences));
        }

        public GameplayCombatStateSnapshot Actual { get; }
        public IReadOnlyList<GameplayStateDifference> Differences { get; }
        public bool MatchesPrediction => Differences.Count == 0;
    }

    public static class GameplayTransitionCoordinator
    {
        public static GameplayTransitionCommitResult Commit<TRecord>(
            GameplayPreparedTransition<TRecord> prepared,
            Func<GameplayCombatStateSnapshot> capture,
            Action<TRecord> commit)
        {
            if (prepared == null) throw new ArgumentNullException(nameof(prepared));
            if (capture == null) throw new ArgumentNullException(nameof(capture));
            if (commit == null) throw new ArgumentNullException(nameof(commit));

            GameplayCombatStateSnapshot current = capture();
            if (!string.Equals(
                    current.CanonicalHash,
                    prepared.Previous.CanonicalHash,
                    StringComparison.Ordinal))
            {
                IReadOnlyList<GameplayStateDifference> staleDifferences =
                    GameplayCombatStateDiffer.Compare(prepared.Previous, current);
                string path = staleDifferences.Count == 0
                    ? "state.hash"
                    : staleDifferences[0].Path;
                throw new InvalidOperationException(
                    $"Prepared transition is stale at '{path}'.");
            }

            commit(prepared.Record);
            GameplayCombatStateSnapshot actual = capture();
            IReadOnlyList<GameplayInvariantViolation> violations =
                GameplayCombatInvariantValidator.Validate(actual);
            if (violations.Count > 0)
                throw new InvalidOperationException(
                    $"Committed transition violates '{violations[0].Code}' at "
                    + $"'{violations[0].Path}'.");
            return new GameplayTransitionCommitResult(
                actual,
                GameplayCombatStateDiffer.Compare(prepared.Predicted, actual));
        }
    }

    public sealed class GameplayReplayVerificationResult
    {
        public GameplayReplayVerificationResult(
            GameplayCombatStateSnapshot expected,
            GameplayCombatStateSnapshot replayed)
        {
            Expected = expected ?? throw new ArgumentNullException(nameof(expected));
            Replayed = replayed ?? throw new ArgumentNullException(nameof(replayed));
            Differences = GameplayCombatStateDiffer.Compare(expected, replayed);
            InvariantViolations = GameplayCombatInvariantValidator.Validate(replayed);
        }

        public GameplayCombatStateSnapshot Expected { get; }
        public GameplayCombatStateSnapshot Replayed { get; }
        public IReadOnlyList<GameplayStateDifference> Differences { get; }
        public IReadOnlyList<GameplayInvariantViolation> InvariantViolations { get; }
        public bool IsVerified => Differences.Count == 0
            && InvariantViolations.Count == 0;
    }

    public static class GameplayWeaponActionStateProjector
    {
        public static GameplayCombatStateSnapshot Project(
            GameplayCombatStateSnapshot previous,
            GameplayActionRecord action)
        {
            if (previous == null) throw new ArgumentNullException(nameof(previous));
            if (action == null) throw new ArgumentNullException(nameof(action));
            if (action.Sequence != previous.Session.LastActionSequence + 1L)
                throw new InvalidOperationException(
                    "Weapon projection requires the next action sequence.");

            GameplayActorSnapshot acting = previous.Session.GetActor(
                action.Request.ActorId);
            if (!BudgetsMatch(acting.TurnBudget, action.PreviousBudget))
                throw new InvalidOperationException(
                    "Weapon projection no longer begins at the recorded budget.");
            TurnBudget expectedBudget = action.PreviousBudget.SpendAction(action.Cost);
            if (!BudgetsMatch(expectedBudget, action.ResultingBudget))
                throw new InvalidOperationException(
                    "Weapon projection has an invalid resulting budget.");
            if (action.Outcomes.Count != 1)
                throw new ArgumentException(
                    "Weapon projection requires exactly one outcome.", nameof(action));

            var actors = new List<GameplayActorSnapshot>(
                previous.Session.Actors.Count);
            foreach (GameplayActorSnapshot actor in previous.Session.Actors)
                actors.Add(ProjectActor(previous, actor, action));

            DestructibleDamageRecord directFireDamage =
                action.Outcomes[0] is WeaponDischargedActionOutcome discharged
                    ? discharged.Discharge.Damage
                    : null;

            GameplaySessionStateSnapshot session = previous.Session;
            var resultingSession = new GameplaySessionStateSnapshot(
                session.ScenarioId,
                session.Mode,
                session.Operation,
                session.TurnContext,
                session.EncounterActive,
                session.EncounterCompletionRequested,
                session.ActiveActorId,
                session.TurnPhase,
                actors,
                session.InitiativeOrder,
                session.Objectives,
                session.EmergencyResponders,
                session.EmergencyResponderIndex,
                session.EmergencyResumeActorId,
                action.Sequence,
                session.LastTurnSequence,
                checked(session.JournalSequence
                    + (directFireDamage == null ? 1L : 2L)));
            var projectiles = new List<ProjectileFlightSnapshot>(
                previous.Projectiles);
            if (action.Outcomes[0] is ProjectileLaunchedActionOutcome launched)
            {
                foreach (ProjectileFlightSnapshot existing in projectiles)
                    if (string.Equals(
                        existing.ProjectileId,
                        launched.Launch.ProjectileId,
                        StringComparison.Ordinal))
                        throw new InvalidOperationException(
                            "Weapon projection cannot duplicate a projectile ID.");
                projectiles.Add(new ProjectileFlightSnapshot(
                    launched.Launch,
                    launched.Launch.Origin,
                    distanceTraveled: 0f,
                    elapsedTurnTime: 0f,
                    ProjectileFlightStatus.InFlight));
            }
            var destructibles = new List<DestructiblePropSnapshot>(
                previous.Destructibles);
            if (directFireDamage != null)
            {
                int index = FindDestructible(
                    destructibles,
                    directFireDamage.PropId);
                if (!DestructibleSnapshotsMatch(
                        destructibles[index],
                        directFireDamage.Previous))
                {
                    throw new InvalidOperationException(
                        "Direct-fire damage no longer starts from the projected prop state.");
                }

                destructibles[index] = directFireDamage.Resulting;
            }

            return new GameplayCombatStateSnapshot(
                resultingSession,
                destructibles,
                previous.Vehicles,
                projectiles,
                previous.SmokeFields);
        }

        private static GameplayActorSnapshot ProjectActor(
            GameplayCombatStateSnapshot previous,
            GameplayActorSnapshot actor,
            GameplayActionRecord action)
        {
            GameplayActorPose pose = actor.Pose;
            TurnBudget budget = actor.TurnBudget;
            ActorWoundSnapshot wounds = actor.Wounds;
            GameplayActionOutcome outcome = action.Outcomes[0];
            if (string.Equals(
                    actor.ActorId,
                    action.Request.ActorId,
                    StringComparison.Ordinal))
            {
                budget = action.ResultingBudget;
                GameplayPosition facingTarget;
                if (outcome is AttackResolvedActionOutcome attack)
                    facingTarget = previous.Session.GetActor(
                        attack.TargetId).Pose.Position;
                else if (outcome is WeaponDischargedActionOutcome discharge)
                    facingTarget = discharge.Discharge.AimPoint;
                else if (outcome is ProjectileLaunchedActionOutcome projectile)
                    facingTarget = projectile.Launch.AimPoint;
                else
                    throw new ArgumentException(
                        "Weapon projection received a non-weapon outcome.",
                        nameof(action));
                pose = FaceToward(pose, facingTarget);
            }

            if (outcome is AttackResolvedActionOutcome resolved
                && string.Equals(
                    actor.ActorId,
                    resolved.TargetId,
                    StringComparison.Ordinal))
            {
                wounds = resolved.Attack.TargetWoundsAfter;
                float woundedAllowance = Math.Max(
                    0f,
                    actor.TurnMovementAllowance - wounds.MovementPenalty);
                budget = new TurnBudget(
                    budget.ActionPoints,
                    Math.Min(budget.MovementOpportunity, woundedAllowance));
            }

            return new GameplayActorSnapshot(
                actor.ActorId,
                pose,
                budget,
                wounds,
                actor.EquippedItemId,
                actor.EquipmentEffects,
                actor.MaximumWounds,
                actor.Inventory,
                actor.TurnActionPointAllowance,
                actor.TurnMovementAllowance,
                actor.PinState);
        }

        private static GameplayActorPose FaceToward(
            GameplayActorPose pose,
            GameplayPosition target)
        {
            double deltaX = (double)target.X - pose.Position.X;
            double deltaZ = (double)target.Z - pose.Position.Z;
            if (Math.Abs(deltaX) <= 0.0001d
                && Math.Abs(deltaZ) <= 0.0001d)
                return pose;
            float facing = (float)(
                Math.Atan2(deltaX, deltaZ) * (180d / Math.PI));
            return new GameplayActorPose(pose.Position, facing, pose.Stance);
        }

        private static bool BudgetsMatch(TurnBudget left, TurnBudget right) =>
            left.ActionPoints == right.ActionPoints
            && Math.Abs(
                left.MovementOpportunity - right.MovementOpportunity) <= 0.0001f;

        private static int FindDestructible(
            IList<DestructiblePropSnapshot> destructibles,
            string propId)
        {
            for (int index = 0; index < destructibles.Count; index++)
            {
                if (string.Equals(
                        destructibles[index].PropId,
                        propId,
                        StringComparison.Ordinal))
                {
                    return index;
                }
            }

            throw new InvalidOperationException(
                $"Direct-fire prop '{propId}' is absent from the combat state.");
        }

        private static bool DestructibleSnapshotsMatch(
            DestructiblePropSnapshot left,
            DestructiblePropSnapshot right) =>
            string.Equals(left.PropId, right.PropId, StringComparison.Ordinal)
            && left.State == right.State
            && left.Posture == right.Posture
            && left.FractureChunkCount == right.FractureChunkCount
            && left.DetachedFractureChunks == right.DetachedFractureChunks
            && Math.Abs(left.MaximumIntegrity - right.MaximumIntegrity) <= 0.0001f
            && Math.Abs(left.RemainingIntegrity - right.RemainingIntegrity) <= 0.0001f
            && left.Pose.Position.DistanceTo(right.Pose.Position) <= 0.0001f
            && Math.Abs(left.Pose.PitchDegrees - right.Pose.PitchDegrees) <= 0.0001f
            && Math.Abs(left.Pose.YawDegrees - right.Pose.YawDegrees) <= 0.0001f
            && Math.Abs(left.Pose.RollDegrees - right.Pose.RollDegrees) <= 0.0001f;
    }
}
