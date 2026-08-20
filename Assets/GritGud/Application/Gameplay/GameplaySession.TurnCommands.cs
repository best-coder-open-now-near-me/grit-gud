using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;

namespace GritGud.Application.Gameplay
{
    public sealed partial class GameplaySession
    {
        public bool EnterTurnMode()
        {
            return TryEnterTurnMode(out _);
        }

        public bool TryEnterTurnMode(out TurnModeEntryFailure failure)
        {
            if (IsCanonicalProjectionBound)
            {
                if (Mode == GameplaySessionMode.TurnBased)
                {
                    failure = TurnModeEntryFailure.AlreadyInTurnMode;
                    return false;
                }
                if (!EncounterActive
                    && VoluntaryTurnReentrySecondsRemaining > 0f)
                {
                    failure = TurnModeEntryFailure.VoluntaryReentryLocked;
                    return false;
                }
                ExecuteCanonical(new GameplaySessionControlTransitionPayload(
                    CanonicalControlActorId(),
                    GameplaySemanticCapability.ChangeTurnMode,
                    "enter"));
                failure = TurnModeEntryFailure.None;
                return true;
            }
            RequireLegacyMutationAllowed(nameof(TryEnterTurnMode));
            return turnLifecycle.TryEnterTurnMode(out failure);
        }

        public void AdvanceContinuousTime(float elapsedSeconds)
        {
            GameplayNumericPolicy.RequireFinite(
                elapsedSeconds,
                nameof(elapsedSeconds));
            if (elapsedSeconds < 0f)
                throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
            if (IsCanonicalProjectionBound)
            {
                if (Mode != GameplaySessionMode.Exploration
                    || EncounterActive
                    || elapsedSeconds == 0f)
                    return;
                if (VoluntaryTurnReentrySecondsRemaining <= 0f
                    && !HasCanonicalContinuousWorldState())
                    return;
                ExecuteCanonical(new GameplayWorldAdvanceTransitionPayload(
                    CanonicalControlActorId(),
                    "continuous-time",
                    elapsedSeconds));
                return;
            }
            RequireLegacyMutationAllowed(nameof(AdvanceContinuousTime));
            turnLifecycle.AdvanceContinuousTime(elapsedSeconds);
        }

        public void AdvanceExploration(
            string actorId,
            GameplayActorPose pose,
            float elapsedSeconds)
        {
            GameplayNumericPolicy.RequireFinite(
                elapsedSeconds,
                nameof(elapsedSeconds));
            if (elapsedSeconds < 0f)
                throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
            if (Mode != GameplaySessionMode.Exploration || EncounterActive)
                return;
            GameplayActorSnapshot actor = GetActor(actorId);
            if (actor.IsPinned || actor.IsIncapacitated)
                return;
            bool changed = !PosesMatch(actor.Pose, pose);
            bool advanceWorld = elapsedSeconds > 0f
                && (VoluntaryTurnReentrySecondsRemaining > 0f
                    || HasCanonicalContinuousWorldState());
            if (!changed && !advanceWorld) return;
            if (IsCanonicalProjectionBound)
            {
                ExecuteCanonical(new GameplayWorldAdvanceTransitionPayload(
                    actorId,
                    "continuous-time",
                    advanceWorld ? elapsedSeconds : 0f,
                    changed
                        ? new ExplorationPoseRecord(
                            actorId,
                            actor.Pose,
                            pose)
                        : null));
                return;
            }
            if (advanceWorld)
                turnLifecycle.AdvanceContinuousTime(elapsedSeconds);
            if (changed)
                UpdateExplorationPose(actorId, pose);
        }

        public bool RequestEncounterCompletionAtTurnEnd()
        {
            if (IsCanonicalProjectionBound)
            {
                if (!EncounterActive || EncounterCompletionRequested)
                    return false;
                ExecuteCanonical(new GameplaySessionControlTransitionPayload(
                    CanonicalControlActorId(),
                    GameplaySemanticCapability.ChangeEncounter,
                    "request-completion"));
                return true;
            }
            RequireLegacyMutationAllowed(
                nameof(RequestEncounterCompletionAtTurnEnd));
            return turnLifecycle.RequestEncounterCompletionAtTurnEnd();
        }

        public bool TryExitTurnMode(out TurnModeExitFailure failure)
        {
            if (IsCanonicalProjectionBound)
            {
                if (Mode != GameplaySessionMode.TurnBased)
                {
                    failure = TurnModeExitFailure.NotInTurnMode;
                    return false;
                }
                if (Operation != GameplaySessionOperation.None)
                {
                    failure = TurnModeExitFailure.OperationInProgress;
                    return false;
                }
                if (EncounterActive)
                {
                    failure = TurnModeExitFailure.EncounterActive;
                    return false;
                }
                ExecuteCanonical(new GameplaySessionControlTransitionPayload(
                    CanonicalControlActorId(),
                    GameplaySemanticCapability.ChangeTurnMode,
                    "exit",
                    Scenario.Timing.MinimumVoluntaryTurnSeconds));
                failure = TurnModeExitFailure.None;
                return true;
            }
            RequireLegacyMutationAllowed(nameof(TryExitTurnMode));
            return turnLifecycle.TryExitTurnMode(out failure);
        }

        public bool TryEndTurn(string actorId, out TurnEndFailure failure)
        {
            if (IsCanonicalProjectionBound)
            {
                if (Mode != GameplaySessionMode.TurnBased)
                {
                    failure = TurnEndFailure.NotInTurnMode;
                    return false;
                }
                if (Operation != GameplaySessionOperation.None)
                {
                    failure = TurnEndFailure.OperationInProgress;
                    return false;
                }
                if (!string.Equals(
                        ActiveActorId,
                        actorId,
                        StringComparison.Ordinal))
                {
                    failure = TurnEndFailure.ActorNotActive;
                    return false;
                }
                ExecuteCanonical(new GameplayEndTurnTransitionPayload(
                    actorId,
                    TurnPhase == GameplayTurnPhase.EmergencyReaction,
                    Scenario.Timing.MinimumVoluntaryTurnSeconds));
                failure = TurnEndFailure.None;
                return true;
            }
            RequireLegacyMutationAllowed(nameof(TryEndTurn));
            bool encounterWasActive = EncounterActive;
            bool ended = turnLifecycle.TryEndTurn(actorId, out failure);
            if (ended && encounterWasActive && !EncounterActive)
            {
                ReplaceInitiativeScope(allInitiativeOrder);
                encounterState = encounterState.WithParticipants(
                    System.Array.Empty<string>());
            }
            return ended;
        }

        public void BeginEmergencyReaction(
            string attackerId,
            IReadOnlyList<string> responderIds,
            int actionPointAllowance)
        {
            if (IsCanonicalProjectionBound)
            {
                ExecuteCanonical(
                    new GameplayEmergencyReactionTransitionPayload(
                        attackerId,
                        "begin",
                        responderIds,
                        actionPointAllowance));
                return;
            }
            RequireLegacyMutationAllowed(nameof(BeginEmergencyReaction));
            turnLifecycle.BeginEmergencyReaction(
                attackerId,
                responderIds,
                actionPointAllowance);
        }

        public bool TryEndEmergencyTurn(
            string actorId,
            out bool responsePassCompleted,
            out TurnEndFailure failure)
        {
            if (IsCanonicalProjectionBound)
            {
                responsePassCompleted = false;
                if (TurnPhase != GameplayTurnPhase.EmergencyReaction)
                    return TryEndTurn(actorId, out failure);
                if (Operation != GameplaySessionOperation.None)
                {
                    failure = TurnEndFailure.OperationInProgress;
                    return false;
                }
                if (!string.Equals(
                        ActiveActorId,
                        actorId,
                        StringComparison.Ordinal))
                {
                    failure = TurnEndFailure.ActorNotActive;
                    return false;
                }
                ExecuteCanonical(new GameplayEndTurnTransitionPayload(
                    actorId,
                    emergency: true));
                responsePassCompleted = EmergencyResponderIndex
                    >= EmergencyResponders.Count;
                failure = TurnEndFailure.None;
                return true;
            }
            RequireLegacyMutationAllowed(nameof(TryEndEmergencyTurn));
            return turnLifecycle.TryEndEmergencyTurn(
                actorId,
                out responsePassCompleted,
                out failure);
        }

        public void CompleteEmergencyReaction(string resumeActorId)
        {
            if (IsCanonicalProjectionBound)
            {
                if (!string.Equals(
                        resumeActorId,
                        EmergencyResumeActorId,
                        StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        "Emergency reactions must resume their triggering attacker.");
                ExecuteCanonical(
                    new GameplayEmergencyReactionTransitionPayload(
                        resumeActorId,
                        "complete"));
                return;
            }
            RequireLegacyMutationAllowed(nameof(CompleteEmergencyReaction));
            turnLifecycle.CompleteEmergencyReaction(resumeActorId);
        }

        public bool CompleteVoluntaryWorldTurn()
        {
            if (IsCanonicalProjectionBound)
            {
                if (Mode != GameplaySessionMode.TurnBased
                    || EncounterActive
                    || Operation
                        != GameplaySessionOperation.ResolvingWorldTurn
                    || PendingVoluntaryTurnCycle == null)
                    return false;
                ExecuteCanonical(new GameplayWorldAdvanceTransitionPayload(
                    CanonicalControlActorId(),
                    "voluntary-cycle"));
                return true;
            }
            RequireLegacyMutationAllowed(nameof(CompleteVoluntaryWorldTurn));
            return turnLifecycle.CompleteVoluntaryWorldTurn();
        }

        private string CanonicalControlActorId() =>
            !string.IsNullOrWhiteSpace(ActiveActorId)
                ? ActiveActorId
                : InitiativeOrder[0];
    }
}
