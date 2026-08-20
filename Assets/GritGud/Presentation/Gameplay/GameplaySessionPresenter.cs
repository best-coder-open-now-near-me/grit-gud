using System;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class GameplaySessionPresenter : MonoBehaviour
    {
        private const float ExplorationSimulationStepSeconds = 0.1f;

        private ExplorationMovementInput explorationInput;
        private ThirdPersonMotor motor;
        private ActorStancePresenter stancePresenter;
        private StanceChangeResolver stanceResolver;
        private Transform actorTransform;
        private string actorId;
        private float explorationElapsedSeconds;

        public GameplaySession Session { get; private set; }

        public StanceChangeFailure LastStanceFailure { get; private set; }

        public string LastStanceFailureCode { get; private set; } = string.Empty;

        public void Bind(
            GameplaySession session,
            ExplorationMovementInput movementInput,
            Transform authoritativeActorTransform,
            string authoritativeActorId)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            Unbind();
            Session = session;
            Session.EquipmentChanged += HandleEquipmentChanged;
            Session.ActorCapabilityChanged += HandleActorCapabilityChanged;
            SetActor(
                movementInput,
                authoritativeActorTransform,
                authoritativeActorId);
        }

        public void SetActor(
            ExplorationMovementInput movementInput,
            Transform authoritativeActorTransform,
            string authoritativeActorId)
        {
            if (Session == null)
            {
                throw new InvalidOperationException(
                    "Bind the gameplay session presenter before changing actors.");
            }
            if (movementInput == null)
                throw new ArgumentNullException(nameof(movementInput));
            if (authoritativeActorTransform == null)
                throw new ArgumentNullException(nameof(authoritativeActorTransform));
            if (string.IsNullOrWhiteSpace(authoritativeActorId))
            {
                throw new ArgumentException(
                    "Authoritative actor identifiers cannot be empty.",
                    nameof(authoritativeActorId));
            }

            Session.GetActor(authoritativeActorId);
            ThirdPersonMotor nextMotor =
                movementInput.GetComponent<ThirdPersonMotor>();
            if (nextMotor == null)
            {
                throw new InvalidOperationException(
                    "Exploration input must be attached to a third-person motor.");
            }
            ActorStancePresenter nextStancePresenter =
                movementInput.GetComponent<ActorStancePresenter>();
            if (nextStancePresenter == null)
            {
                nextStancePresenter = movementInput.gameObject
                    .AddComponent<ActorStancePresenter>();
            }

            SynchronizeExplorationPose();
            if (explorationInput != null)
                explorationInput.SetInputEnabled(false);
            if (motor != null)
                motor.SetMovementSpeedMultiplier(1f);

            explorationInput = movementInput;
            motor = nextMotor;
            stancePresenter = nextStancePresenter;
            stanceResolver = new StanceChangeResolver(Session, stancePresenter);

            actorTransform = authoritativeActorTransform;
            actorId = authoritativeActorId;
            ApplyEquipmentEffects();
            LastStanceFailure = StanceChangeFailure.None;
            LastStanceFailureCode = string.Empty;
            ApplyMode();
        }

        private void LateUpdate()
        {
            if (Session?.Mode == GameplaySessionMode.Exploration)
            {
                explorationElapsedSeconds += Mathf.Max(
                    0f,
                    Time.unscaledDeltaTime);
                if (explorationElapsedSeconds
                    >= ExplorationSimulationStepSeconds)
                    SynchronizeExplorationPose();
                return;
            }
            explorationElapsedSeconds = 0f;
        }

        public bool EnterTurnMode()
        {
            return TryEnterTurnMode(out _);
        }

        public bool TryEnterTurnMode(out TurnModeEntryFailure failure)
        {
            if (Session == null)
            {
                failure = TurnModeEntryFailure.AlreadyInTurnMode;
                return false;
            }

            SynchronizeExplorationPose();
            bool entered = Session.TryEnterTurnMode(out failure);
            if (!entered)
            {
                return false;
            }

            motor.StopPlanarMovement();
            ApplyMode();
            return true;
        }

        public bool TryBeginEncounter()
        {
            return TryBeginEncounter(Session?.AllInitiativeOrder);
        }

        public bool TryBeginEncounter(
            System.Collections.Generic.IEnumerable<string> participantIds)
        {
            if (Session == null || Session.EncounterActive)
            {
                return false;
            }

            SynchronizeExplorationPose();
            if (!Session.BeginEncounter(participantIds))
            {
                return false;
            }

            motor.StopPlanarMovement();
            ApplyMode();
            return true;
        }

        public bool TryBeginEncounterFromAction(GameplayActionRecord action)
        {
            if (Session == null || Session.EncounterActive)
            {
                return false;
            }

            SynchronizeExplorationPose();
            if (!Session.BeginEncounterFromAction(action))
            {
                return false;
            }

            motor.StopPlanarMovement();
            ApplyMode();
            return true;
        }

        public void RefreshModePresentation()
        {
            ApplyMode();
        }

        public bool TryExitTurnMode(out TurnModeExitFailure failure)
        {
            if (Session == null)
            {
                failure = TurnModeExitFailure.NotInTurnMode;
                return false;
            }

            if (!Session.TryExitTurnMode(out failure))
            {
                return false;
            }

            ApplyMode();
            return true;
        }

        public bool ToggleStance()
        {
            if (Session == null || stancePresenter == null
                || stanceResolver == null || actorId == null)
            {
                return false;
            }

            ActorStance current = Session.GetActor(actorId).Pose.Stance;
            ActorStance requested = current == ActorStance.Standing
                ? ActorStance.Crouched
                : ActorStance.Standing;
            if (!stanceResolver.TryResolve(
                    actorId,
                    requested,
                    out StanceChangeRecord record,
                    out StanceChangeFailure failure,
                    out string failureCode))
            {
                LastStanceFailure = failure;
                LastStanceFailureCode = failureCode;
                return false;
            }

            stancePresenter.ApplyResolved(record.ResultingPose.Stance);
            LastStanceFailure = StanceChangeFailure.None;
            LastStanceFailureCode = string.Empty;
            return true;
        }

        public void Unbind()
        {
            if (Session != null)
            {
                Session.EquipmentChanged -= HandleEquipmentChanged;
                Session.ActorCapabilityChanged -= HandleActorCapabilityChanged;
            }

            if (explorationInput != null)
            {
                explorationInput.SetInputEnabled(true);
            }

            if (motor != null)
            {
                motor.SetMovementSpeedMultiplier(1f);
            }

            explorationInput = null;
            motor = null;
            stancePresenter = null;
            stanceResolver = null;
            actorTransform = null;
            actorId = null;
            explorationElapsedSeconds = 0f;
            Session = null;
            LastStanceFailure = StanceChangeFailure.None;
            LastStanceFailureCode = string.Empty;
        }

        private void ApplyMode()
        {
            if (explorationInput == null || Session == null)
            {
                return;
            }

            explorationInput.SetInputEnabled(
                Session.Mode == GameplaySessionMode.Exploration
                && !Session.IsActorIncapacitated(actorId)
                && !Session.GetActor(actorId).IsPinned);
        }

        private void HandleEquipmentChanged(EquipmentChangeRecord _)
        {
            ApplyEquipmentEffects();
        }

        private void HandleActorCapabilityChanged(string changedActorId)
        {
            if (string.Equals(
                    changedActorId,
                    actorId,
                    StringComparison.Ordinal))
            {
                motor?.StopPlanarMovement();
                ApplyMode();
            }
        }

        private void ApplyEquipmentEffects()
        {
            if (Session == null || motor == null || actorId == null)
            {
                return;
            }

            motor.SetMovementSpeedMultiplier(
                Session.GetEquipmentEffects(actorId).MovementSpeedMultiplier);
        }

        private void SynchronizeExplorationPose()
        {
            if (Session?.Mode != GameplaySessionMode.Exploration ||
                actorTransform == null || actorId == null
                || Session.GetActor(actorId).IsPinned)
            {
                return;
            }

            float elapsed = explorationElapsedSeconds;
            explorationElapsedSeconds = 0f;
            Session.AdvanceExploration(
                actorId,
                GameplayPoseAdapter.FromTransform(
                    actorTransform,
                    Session.GetActor(actorId).Pose.Stance),
                elapsed);
        }
    }
}
