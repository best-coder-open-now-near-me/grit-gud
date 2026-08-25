using System;
using System.Collections.Generic;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class GameplaySessionPresenter : MonoBehaviour
    {
        private const float ExplorationSimulationStepSeconds = 0.1f;
        private const float DefaultShotPresentationSeconds = 0.18f;

        private ExplorationMovementInput explorationInput;
        private ThirdPersonMotor motor;
        private ActorStancePresenter stancePresenter;
        private StanceChangeResolver stanceResolver;
        private Transform actorTransform;
        private string actorId;
        private float explorationElapsedSeconds;
        private GameplayDialogueLog encounterDialogue;
        private WeaponPresentationCatalog encounterWeapons;
        private GameplayInputController encounterInput;
        private GameplayActionRecord pendingEncounterAction;
        private IReadOnlyList<string> pendingEncounterParticipantIds;
        private float pendingEncounterSecondsRemaining;

        public GameplaySession Session { get; private set; }

        public StanceChangeFailure LastStanceFailure { get; private set; }

        public string LastStanceFailureCode { get; private set; } = string.Empty;

        internal bool EncounterStartPending => pendingEncounterAction != null;

        internal float PendingEncounterSecondsRemaining =>
            pendingEncounterSecondsRemaining;

        internal void BindEncounterPresentation(
            GameplayDialogueLog dialogue,
            WeaponPresentationCatalog weaponCatalog = null)
        {
            if (Session == null)
            {
                throw new InvalidOperationException(
                    "Bind the gameplay session presenter before encounter presentation.");
            }

            encounterDialogue = dialogue ?? throw new ArgumentNullException(
                nameof(dialogue));
            encounterWeapons = weaponCatalog;
        }

        internal void BindEncounterInput(GameplayInputController input)
        {
            encounterInput = input ?? throw new ArgumentNullException(
                nameof(input));
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
            if (Session == null
                || Session.EncounterActive
                || pendingEncounterAction != null)
            {
                return false;
            }

            SynchronizeExplorationPose();
            return PresentEncounterStart(Session.BeginEncounter(participantIds));
        }

        internal void Tick(float deltaTime)
        {
            if (pendingEncounterAction == null)
            {
                return;
            }

            if (Session == null || Session.EncounterActive)
            {
                ClearPendingEncounterStart();
                ApplyMode();
                return;
            }

            pendingEncounterSecondsRemaining -= Mathf.Max(0f, deltaTime);
            if (pendingEncounterSecondsRemaining > 0f)
                return;

            IReadOnlyList<string> participantIds =
                pendingEncounterParticipantIds;
            ClearPendingEncounterStart();
            if (!PresentEncounterStart(
                    Session.BeginEncounter(participantIds)))
            {
                ApplyMode();
            }
        }

        private void Update() => Tick(Time.unscaledDeltaTime);

        public bool TryBeginEncounterFromAction(GameplayActionRecord action)
        {
            if (Session == null || action == null)
                return false;
            if (ReferenceEquals(pendingEncounterAction, action))
                return true;
            if (Session.EncounterActive
                || pendingEncounterAction != null
                || !Session.ActionStartsEncounter(action))
            {
                return false;
            }

            return QueueEncounterFromCommittedAction(
                action,
                Session.CreateEncounterScope(
                    action.Request.ActorId,
                    action.Request.TargetId));
        }

        internal bool TryBeginEncounterFromCommittedAction(
            GameplayActionRecord action,
            IReadOnlyList<string> participantIds)
        {
            if (Session == null || action == null || participantIds == null)
                return false;
            if (ReferenceEquals(pendingEncounterAction, action))
                return true;
            if (Session.EncounterActive || pendingEncounterAction != null)
                return false;

            return QueueEncounterFromCommittedAction(action, participantIds);
        }

        private bool QueueEncounterFromCommittedAction(
            GameplayActionRecord action,
            IEnumerable<string> participantIds)
        {
            SynchronizeExplorationPose();
            var scope = new List<string>(participantIds).AsReadOnly();
            float delaySeconds = ResolveEncounterStartDelay(action);
            if (delaySeconds <= 0f)
            {
                return PresentEncounterStart(
                    Session.BeginEncounter(scope));
            }

            pendingEncounterAction = action;
            pendingEncounterParticipantIds = scope;
            pendingEncounterSecondsRemaining = delaySeconds;
            encounterInput?.SetSuppressed(true);
            motor?.StopPlanarMovement();
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
            encounterWeapons = null;
            ClearPendingEncounterStart();
            encounterInput = null;
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
                && pendingEncounterAction == null
                && !Session.IsActorIncapacitated(actorId)
                && !Session.GetActor(actorId).IsPinned);
        }

        private void PresentEncounterStarted()
        {
            string activeActor = GetActorDisplayName(Session.ActiveActorId);
            encounterDialogue?.Append(
                GameplayDialogueChannel.System,
                "COMBAT",
                activeActor
                    + " has initiative. "
                    + Session.InitiativeOrder.Count
                    + " combatants engaged.");
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

        private float ResolveEncounterStartDelay(GameplayActionRecord action)
        {
            foreach (GameplayActionOutcome outcome in action.Outcomes)
            {
                if (outcome is AttackResolvedActionOutcome attack)
                {
                    AttackResolutionRecord resolution = attack.Attack;
                    float impactDelay = ResolveContactImpactDelay(resolution);
                    float responseSeconds = resolution.Hit
                        ? ActorInjuryAnimationOverlayProjector.HitReactionSeconds
                        : ResolveShotPresentationSeconds(
                            resolution.AttackerId);
                    return impactDelay + responseSeconds;
                }

                if (outcome is WeaponDischargedActionOutcome discharge)
                {
                    return ResolveShotPresentationSeconds(
                        discharge.Discharge.AttackerId);
                }
            }

            return 0f;
        }

        private float ResolveContactImpactDelay(
            AttackResolutionRecord resolution)
        {
            if (!resolution.IsContactAttack)
                return 0f;

            WeaponPresentationDefinition weapon = ResolveWeaponPresentation(
                resolution.AttackerId);
            return weapon != null
                && weapon.AttackPresentation
                    == WeaponAttackPresentationKind.ContactStrike
                ? weapon.ContactStrikeSeconds
                    * weapon.ContactImpactNormalizedTime
                : GameplayCloseQuartersPresentationTiming.ContactStrikeSeconds
                    * GameplayCloseQuartersPresentationTiming
                        .ContactImpactNormalizedTime;
        }

        private float ResolveShotPresentationSeconds(string attackerId)
        {
            WeaponPresentationDefinition weapon = ResolveWeaponPresentation(
                attackerId);
            return weapon?.ShotEffectSeconds
                ?? DefaultShotPresentationSeconds;
        }

        private WeaponPresentationDefinition ResolveWeaponPresentation(
            string attackerId)
        {
            if (Session == null || string.IsNullOrWhiteSpace(attackerId))
                return null;

            string itemId = Session.GetActor(attackerId).EquippedItemId;
            if (string.IsNullOrWhiteSpace(itemId))
                return null;

            if (encounterWeapons == null)
            {
                try
                {
                    encounterWeapons = WeaponPresentationCatalog.LoadDefault();
                }
                catch (InvalidOperationException)
                {
                    return null;
                }
            }

            return encounterWeapons.TryGet(
                itemId,
                out WeaponPresentationDefinition weapon)
                ? weapon
                : null;
        }

        private void ClearPendingEncounterStart()
        {
            bool wasPending = pendingEncounterAction != null;
            pendingEncounterAction = null;
            pendingEncounterParticipantIds = null;
            pendingEncounterSecondsRemaining = 0f;
            if (wasPending)
                encounterInput?.SetSuppressed(false);
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
