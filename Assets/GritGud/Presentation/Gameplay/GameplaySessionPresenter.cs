using System;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class GameplaySessionPresenter : MonoBehaviour,
        IGameplayWarningHintSource
    {
        private const float ExplorationSimulationStepSeconds = 0.1f;
        private const float EncounterNoticeDurationSeconds = 6f;
        private const int EncounterNoticePriority = 200;

        private ExplorationMovementInput explorationInput;
        private ThirdPersonMotor motor;
        private ActorStancePresenter stancePresenter;
        private StanceChangeResolver stanceResolver;
        private Transform actorTransform;
        private string actorId;
        private float explorationElapsedSeconds;
        private GameplayDialogueLog encounterDialogue;
        private GameplayWarningHintModel encounterWarningHint;
        private float encounterNoticeSecondsRemaining;

        public GameplaySession Session { get; private set; }

        public StanceChangeFailure LastStanceFailure { get; private set; }

        public string LastStanceFailureCode { get; private set; } = string.Empty;

        public GameplayWarningHintModel CurrentWarningHint =>
            encounterWarningHint;

        internal void BindEncounterPresentation(GameplayDialogueLog dialogue)
        {
            if (Session == null)
            {
                throw new InvalidOperationException(
                    "Bind the gameplay session presenter before encounter presentation.");
            }

            encounterDialogue = dialogue ?? throw new ArgumentNullException(
                nameof(dialogue));
        }

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
            {
                motor.SetMovementSpeedMultiplier(1f);
                motor.SetMobilityCapability(CreateHealthyMobility());
            }

            explorationInput = movementInput;
            motor = nextMotor;
            stancePresenter = nextStancePresenter;
            stanceResolver = new StanceChangeResolver(Session, stancePresenter);

            actorTransform = authoritativeActorTransform;
            actorId = authoritativeActorId;
            ApplyMovementEffects();
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
            return PresentEncounterStart(Session.BeginEncounter(participantIds));
        }

        private void Update()
        {
            if (encounterNoticeSecondsRemaining <= 0f)
            {
                return;
            }

            encounterNoticeSecondsRemaining -= Mathf.Max(
                0f,
                Time.unscaledDeltaTime);
            if (encounterNoticeSecondsRemaining <= 0f)
            {
                encounterNoticeSecondsRemaining = 0f;
                encounterWarningHint = null;
            }
        }

        public bool TryBeginEncounterFromAction(GameplayActionRecord action)
        {
            if (Session == null || Session.EncounterActive)
            {
                return false;
            }

            SynchronizeExplorationPose();
            return PresentEncounterStart(Session.BeginEncounterFromAction(action));
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
                motor.SetMobilityCapability(CreateHealthyMobility());
            }

            explorationInput = null;
            motor = null;
            stancePresenter = null;
            stanceResolver = null;
            actorTransform = null;
            actorId = null;
            explorationElapsedSeconds = 0f;
            encounterDialogue = null;
            encounterWarningHint = null;
            encounterNoticeSecondsRemaining = 0f;
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

        private void PresentEncounterStarted()
        {
            var combatants = new System.Collections.Generic.List<string>();
            foreach (string participantId in Session.InitiativeOrder)
            {
                combatants.Add(GetActorDisplayName(participantId));
            }

            string roster = combatants.Count == 0
                ? "No combatants were registered."
                : "Roster ("
                    + combatants.Count
                    + "): "
                    + string.Join(", ", combatants)
                    + ".";
            string activeActor = GetActorDisplayName(Session.ActiveActorId);
            string message = "Combat started. "
                + activeActor
                + " has initiative. "
                + roster;
            encounterDialogue?.Append(
                GameplayDialogueChannel.System,
                "ENCOUNTER STARTED",
                message);
            encounterWarningHint = new GameplayWarningHintModel(
                "encounter.started",
                "COMBAT STARTED\n"
                + activeActor.ToUpperInvariant()
                + " HAS INITIATIVE\n"
                + roster.ToUpperInvariant(),
                EncounterNoticePriority);
            encounterNoticeSecondsRemaining = EncounterNoticeDurationSeconds;
        }

        private string GetActorDisplayName(string participantId) =>
            Session.Scenario.GetActor(participantId).CharacterProfile?.DisplayName
            ?? participantId;

        private bool PresentEncounterStart(bool encounterStarted)
        {
            if (!encounterStarted)
            {
                return false;
            }

            motor.StopPlanarMovement();
            ApplyMode();
            PresentEncounterStarted();
            return true;
        }

        private void HandleEquipmentChanged(EquipmentChangeRecord _)
        {
            ApplyMovementEffects();
        }

        private void HandleActorCapabilityChanged(string changedActorId)
        {
            if (string.Equals(
                    changedActorId,
                    actorId,
                    StringComparison.Ordinal))
            {
                // Exploration pose updates are projected through the same
                // canonical notification stream as real capability changes.
                // Do not restart the motor for a pose-only update: doing so
                // every simulation step repeatedly reset the walking blend.
                if (Session?.Mode != GameplaySessionMode.Exploration
                    || Session.IsActorIncapacitated(actorId)
                    || Session.GetActor(actorId).IsPinned)
                {
                    motor?.StopPlanarMovement();
                }
                ApplyMovementEffects();
                ApplyMode();
            }
        }

        private void ApplyMovementEffects()
        {
            if (Session == null || motor == null || actorId == null)
            {
                return;
            }

            motor.SetMovementSpeedMultiplier(
                Session.GetEquipmentEffects(actorId).MovementSpeedMultiplier);
            motor.SetMobilityCapability(
                Session.GetActor(actorId).Capabilities.Mobility);
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

        private static ActorMobilityCapability CreateHealthyMobility() =>
            new ActorMobilityCapability(
                ActorGait.Normal,
                ActorImpairedSide.None,
                100,
                100,
                canSprint: true,
                canStand: true);
    }
}
