using System;
using System.Collections.Generic;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Presentation.Actors.Animation;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    internal interface IGameplayTurnModeExitConstraint
    {
        bool BlocksTurnModeExit { get; }

        string TurnModeExitBlockedMessage { get; }
    }

    [DisallowMultipleComponent]
    public sealed class GameplayActionController : MonoBehaviour
    {
        private readonly List<IGameplayTurnModeExitConstraint>
            turnModeExitConstraints =
                new List<IGameplayTurnModeExitConstraint>();
        private GameplayActionResolver resolver;
        private GameplaySessionPresenter sessionPresenter;
        private ActorAnimationCoordinator animationCoordinator;
        private string actorId;
        private string objectiveId;
        private GameplayEmergencyCycleSession emergencyCycle;
        private float remainingWorldTurnPresentationSeconds;

        public GameplaySession Session { get; private set; }

        public GameplayActionFailure LastFailure { get; private set; }

        public TurnModeEntryFailure LastTurnModeEntryFailure { get; private set; }

        public TurnEndFailure LastTurnEndFailure { get; private set; }

        public TurnModeExitFailure LastTurnModeExitFailure { get; private set; }

        public GameplayReloadFailure LastReloadFailure { get; private set; }

        public GameplayActionRecord LastResolvedAction { get; private set; }

        public string StatusMessage { get; private set; } = string.Empty;

        public bool CanExitTurnMode => Session != null
            && Session.Mode == GameplaySessionMode.TurnBased
            && Session.Operation == GameplaySessionOperation.None
            && emergencyCycle?.HasPendingOrActiveWindow != true
            && (!Session.EncounterActive
                || !Session.HasCapableHostileActor(actorId))
            && FindBlockingTurnModeExitConstraint() == null;

        internal int TurnModeExitConstraintCount =>
            turnModeExitConstraints.Count;

        public string InteractionDisplayName => Session == null || objectiveId == null
            ? string.Empty
            : Session.GetObjective(objectiveId).Interaction.DisplayName;

        public event Action<GameplayActionRecord> ActionResolved;

        private void Update()
        {
            if (remainingWorldTurnPresentationSeconds <= 0f)
            {
                return;
            }

            if (Session?.Operation != GameplaySessionOperation.ResolvingWorldTurn)
            {
                remainingWorldTurnPresentationSeconds = 0f;
                return;
            }

            remainingWorldTurnPresentationSeconds -= Time.unscaledDeltaTime;
            if (remainingWorldTurnPresentationSeconds > 0f)
            {
                return;
            }

            remainingWorldTurnPresentationSeconds = 0f;
            if (Session.CompleteVoluntaryWorldTurn())
            {
                StatusMessage = "World turn complete. New tactical interval ready.";
                sessionPresenter.RefreshModePresentation();
            }
        }

        public void Bind(
            GameplaySession session,
            GameplaySessionPresenter modePresenter,
            ActorAnimationCoordinator actorAnimationCoordinator,
            string authoritativeActorId,
            string primaryObjectiveId)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            if (modePresenter == null)
            {
                throw new ArgumentNullException(nameof(modePresenter));
            }

            Unbind();
            if (!string.IsNullOrWhiteSpace(primaryObjectiveId))
            {
                session.GetObjective(primaryObjectiveId);
            }

            Session = session;
            sessionPresenter = modePresenter;
            objectiveId = string.IsNullOrWhiteSpace(primaryObjectiveId)
                ? null
                : primaryObjectiveId;
            resolver = new GameplayActionResolver(session);
            ClearFailures();
            LastResolvedAction = null;
            StatusMessage = string.Empty;
            remainingWorldTurnPresentationSeconds = 0f;
            enabled = true;
            SetActor(actorAnimationCoordinator, authoritativeActorId);
        }

        public void SetActor(
            ActorAnimationCoordinator actorAnimationCoordinator,
            string authoritativeActorId)
        {
            if (Session == null)
            {
                throw new InvalidOperationException(
                    "Bind gameplay actions before changing actors.");
            }
            if (actorAnimationCoordinator == null)
                throw new ArgumentNullException(nameof(actorAnimationCoordinator));
            if (string.IsNullOrWhiteSpace(authoritativeActorId))
            {
                throw new ArgumentException(
                    "Action-controller actor identifiers cannot be empty.",
                    nameof(authoritativeActorId));
            }

            Session.GetActor(authoritativeActorId);
            animationCoordinator = actorAnimationCoordinator;
            actorId = authoritativeActorId;
            ClearFailures();
            LastResolvedAction = null;
            StatusMessage = string.Empty;
        }

        public void Unbind()
        {
            Session = null;
            sessionPresenter = null;
            animationCoordinator = null;
            actorId = null;
            objectiveId = null;
            resolver = null;
            emergencyCycle = null;
            turnModeExitConstraints.Clear();
            ClearFailures();
            LastResolvedAction = null;
            StatusMessage = string.Empty;
            remainingWorldTurnPresentationSeconds = 0f;
            ActionResolved = null;
            enabled = false;
        }

        internal void BindEmergencyCycle(GameplayEmergencyCycleSession cycle)
        {
            emergencyCycle = cycle ?? throw new ArgumentNullException(nameof(cycle));
        }

        internal void PresentExternalStatus(string message)
        {
            StatusMessage = message?.Trim() ?? string.Empty;
        }

        internal void RegisterTurnModeExitConstraint(
            IGameplayTurnModeExitConstraint constraint)
        {
            if (constraint == null)
            {
                throw new ArgumentNullException(nameof(constraint));
            }

            if (!turnModeExitConstraints.Contains(constraint))
            {
                turnModeExitConstraints.Add(constraint);
            }
        }

        public GameplayActionFailure EvaluateInteraction()
        {
            return resolver == null || actorId == null || objectiveId == null
                ? GameplayActionFailure.TargetNotFound
                : resolver.EvaluateInteraction(actorId, objectiveId);
        }

        public bool TryInteract()
        {
            if (resolver == null || actorId == null || objectiveId == null)
            {
                LastFailure = GameplayActionFailure.TargetNotFound;
                StatusMessage = DescribeInteractionFailure(LastFailure);
                return false;
            }

            if (!resolver.TryResolveInteraction(
                    actorId,
                    objectiveId,
                    out GameplayActionRecord record,
                    out GameplayActionFailure failure))
            {
                LastFailure = failure;
                StatusMessage = DescribeInteractionFailure(failure);
                return false;
            }

            ClearFailures();
            LastResolvedAction = record;
            StatusMessage = InteractionDisplayName + " complete.";
            animationCoordinator.PresentInteraction();
            ActionResolved?.Invoke(record);
            return true;
        }

        public bool TryReload()
        {
            if (Session == null || actorId == null)
            {
                LastReloadFailure = GameplayReloadFailure.ActorNotActive;
                StatusMessage = DescribeReloadFailure(LastReloadFailure);
                return false;
            }

            if (!new GameplayReloadSession(Session).TryResolve(
                    actorId,
                    out GameplayActionRecord record,
                    out GameplayReloadFailure failure))
            {
                LastReloadFailure = failure;
                StatusMessage = DescribeReloadFailure(failure);
                return false;
            }

            ClearFailures();
            LastResolvedAction = record;
            var reload = (WeaponReloadedActionOutcome)record.Outcomes[0];
            InventoryItemDefinition weapon = Session.GetInventoryItem(
                actorId,
                reload.Change.WeaponItemId);
            LastReloadFailure = GameplayReloadFailure.None;
            StatusMessage = weapon.DisplayName
                + " reloaded: "
                + reload.Change.ResultingLoadedRounds
                + " / "
                + reload.Change.ResultingReserveRounds
                + ".";
            animationCoordinator?.TryRequestAction(
                ActorAnimationAction.Reload);
            ActionResolved?.Invoke(record);
            return true;
        }

        public bool TryEndTurn()
        {
            if (Session == null || actorId == null)
            {
                LastTurnEndFailure = TurnEndFailure.NotInTurnMode;
                StatusMessage = DescribeTurnEndFailure(LastTurnEndFailure);
                return false;
            }

            bool encounterTurn = Session.EncounterActive;
            string endingActorId = Session.TurnPhase ==
                GameplayTurnPhase.EmergencyReaction
                    ? Session.ActiveActorId
                    : actorId;
            TurnEndFailure failure;
            bool ended = emergencyCycle == null
                ? Session.TryEndTurn(endingActorId, out failure)
                : emergencyCycle.TryEndTurn(endingActorId, out failure);
            if (!ended)
            {
                LastTurnEndFailure = failure;
                StatusMessage = DescribeTurnEndFailure(failure);
                return false;
            }

            ClearFailures();
            if (encounterTurn)
            {
                remainingWorldTurnPresentationSeconds = 0f;
                StatusMessage = !Session.EncounterActive
                    ? "Encounter complete. Exploration resumed."
                    : Session.TurnPhase == GameplayTurnPhase.EmergencyReaction
                        ? CreateEmergencyReactionStatus()
                        : "Turn ended. Initiative advanced.";
            }
            else
            {
                remainingWorldTurnPresentationSeconds =
                    Session.Scenario.Timing.MinimumVoluntaryTurnSeconds;
                StatusMessage = "World turn resolving...";
            }

            sessionPresenter.RefreshModePresentation();
            return true;
        }

        private string CreateEmergencyReactionStatus()
        {
            EmergencyReactionWindowRecord window =
                emergencyCycle?.CurrentWindow;
            int actionPointAllowance = window?.ActionPointAllowance
                ?? Session.GetActor(Session.ActiveActorId)
                    .TurnBudget.ActionPoints;
            return $"Emergency reaction: {actionPointAllowance} AP. "
                + "Respond, then end the reaction.";
        }

        public bool TryExitTurnMode()
        {
            if (sessionPresenter == null)
            {
                LastTurnModeExitFailure = TurnModeExitFailure.NotInTurnMode;
                StatusMessage = DescribeTurnModeExitFailure(
                    LastTurnModeExitFailure);
                return false;
            }

            if (emergencyCycle?.HasPendingOrActiveWindow == true)
            {
                LastTurnModeExitFailure = TurnModeExitFailure.EncounterActive;
                StatusMessage = "Resolve the active emergency before leaving turn mode.";
                return false;
            }

            IGameplayTurnModeExitConstraint constraint =
                FindBlockingTurnModeExitConstraint();
            if (constraint != null)
            {
                LastTurnModeExitFailure = TurnModeExitFailure.OperationInProgress;
                StatusMessage = constraint.TurnModeExitBlockedMessage;
                return false;
            }

            if (Session.EncounterActive)
            {
                if (Session.HasCapableHostileActor(actorId))
                {
                    LastTurnModeExitFailure =
                        TurnModeExitFailure.EncounterActive;
                    StatusMessage =
                        "Hostile actors are still capable of responding.";
                    return false;
                }

                Session.CompleteEncounter();
            }

            if (!sessionPresenter.TryExitTurnMode(
                    out TurnModeExitFailure failure))
            {
                LastTurnModeExitFailure = failure;
                StatusMessage = DescribeTurnModeExitFailure(failure);
                return false;
            }

            ClearFailures();
            StatusMessage = "World turn advancing...";
            return true;
        }

        private IGameplayTurnModeExitConstraint
            FindBlockingTurnModeExitConstraint()
        {
            foreach (IGameplayTurnModeExitConstraint constraint in
                turnModeExitConstraints)
            {
                if (constraint.BlocksTurnModeExit)
                {
                    return constraint;
                }
            }

            return null;
        }

        public bool TryEnterTurnMode()
        {
            if (sessionPresenter == null)
            {
                LastTurnModeEntryFailure =
                    TurnModeEntryFailure.AlreadyInTurnMode;
                StatusMessage = DescribeTurnModeEntryFailure(
                    LastTurnModeEntryFailure);
                return false;
            }

            if (!sessionPresenter.TryEnterTurnMode(
                    out TurnModeEntryFailure failure))
            {
                LastTurnModeEntryFailure = failure;
                StatusMessage = DescribeTurnModeEntryFailure(failure);
                return false;
            }

            ClearFailures();
            StatusMessage = "Tactical interval started.";
            return true;
        }

        private void ClearFailures()
        {
            LastFailure = GameplayActionFailure.None;
            LastTurnModeEntryFailure = TurnModeEntryFailure.None;
            LastTurnEndFailure = TurnEndFailure.None;
            LastTurnModeExitFailure = TurnModeExitFailure.None;
            LastReloadFailure = GameplayReloadFailure.None;
        }

        private string DescribeTurnModeEntryFailure(
            TurnModeEntryFailure failure)
        {
            switch (failure)
            {
                case TurnModeEntryFailure.AlreadyInTurnMode:
                    return "Turn mode is already active.";
                case TurnModeEntryFailure.VoluntaryReentryLocked:
                    float seconds = Session == null
                        ? 0f
                        : Session.VoluntaryTurnReentrySecondsRemaining;
                    return $"World turn advancing. Turn mode ready in {seconds:0.0}s.";
                case TurnModeEntryFailure.None:
                    return string.Empty;
                default:
                    throw new ArgumentOutOfRangeException(nameof(failure));
            }
        }

        private static string DescribeInteractionFailure(
            GameplayActionFailure failure)
        {
            switch (failure)
            {
                case GameplayActionFailure.ActorNotActive:
                    return "Only the active actor can interact.";
                case GameplayActionFailure.OperationInProgress:
                    return "Wait for the current movement to resolve.";
                case GameplayActionFailure.TargetNotFound:
                    return "No valid interaction target is available.";
                case GameplayActionFailure.TargetAlreadyCompleted:
                    return "That interaction is already complete.";
                case GameplayActionFailure.TargetOutOfRange:
                    return "Move closer to interact.";
                case GameplayActionFailure.InsufficientActionPoints:
                    return "Not enough AP remains for this interaction.";
                case GameplayActionFailure.InsufficientMovementOpportunity:
                    return "Not enough movement remains for this interaction.";
                case GameplayActionFailure.ActorPinned:
                    return "Push off the pinning prop before interacting.";
                case GameplayActionFailure.None:
                    return string.Empty;
                default:
                    throw new ArgumentOutOfRangeException(nameof(failure));
            }
        }

        private static string DescribeTurnEndFailure(TurnEndFailure failure)
        {
            switch (failure)
            {
                case TurnEndFailure.NotInTurnMode:
                    return "Enter turn mode before ending a turn.";
                case TurnEndFailure.OperationInProgress:
                    return "Wait for the current movement to resolve.";
                case TurnEndFailure.ActorNotActive:
                    return "Only the active actor can end its turn.";
                case TurnEndFailure.None:
                    return string.Empty;
                default:
                    throw new ArgumentOutOfRangeException(nameof(failure));
            }
        }

        private static string DescribeTurnModeExitFailure(
            TurnModeExitFailure failure)
        {
            switch (failure)
            {
                case TurnModeExitFailure.NotInTurnMode:
                    return "Turn mode is not active.";
                case TurnModeExitFailure.OperationInProgress:
                    return "Wait for the current movement to resolve.";
                case TurnModeExitFailure.EncounterActive:
                    return "Finish the encounter before leaving turn mode.";
                case TurnModeExitFailure.None:
                    return string.Empty;
                default:
                    throw new ArgumentOutOfRangeException(nameof(failure));
            }
        }

        private static string DescribeReloadFailure(
            GameplayReloadFailure failure)
        {
            switch (failure)
            {
                case GameplayReloadFailure.ActorNotActive:
                    return "Only the active actor can reload.";
                case GameplayReloadFailure.OperationInProgress:
                    return "Wait for the current action to resolve.";
                case GameplayReloadFailure.ActorIncapacitated:
                    return "An incapacitated actor cannot reload.";
                case GameplayReloadFailure.ActorPinned:
                    return "Push off the pinning prop before reloading.";
                case GameplayReloadFailure.ItemNotFound:
                case GameplayReloadFailure.AmmunitionUnavailable:
                    return "The equipped weapon does not use ammunition.";
                case GameplayReloadFailure.WeaponNotEquipped:
                    return "Equip an ammunition weapon before reloading.";
                case GameplayReloadFailure.ProfileMismatch:
                    return "The reload capability does not match this weapon.";
                case GameplayReloadFailure.MagazineFull:
                    return "The equipped weapon is already fully loaded.";
                case GameplayReloadFailure.ReserveEmpty:
                    return "No compatible reserve ammunition remains.";
                case GameplayReloadFailure.InsufficientActionPoints:
                    return "Not enough AP remains to reload.";
                case GameplayReloadFailure.InsufficientMovementOpportunity:
                    return "Not enough movement remains to reload.";
                case GameplayReloadFailure.None:
                    return string.Empty;
                default:
                    throw new ArgumentOutOfRangeException(nameof(failure));
            }
        }
    }
}
