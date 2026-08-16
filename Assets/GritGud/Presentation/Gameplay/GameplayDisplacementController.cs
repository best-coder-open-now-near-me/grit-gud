using System;
using System.Collections.Generic;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Presentation.Actors.Animation;
using GritGud.Presentation.Levels.Runtime;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class GameplayDisplacementController : MonoBehaviour,
        IGameplayWarningHintSource
    {
        private enum TargetingPhase
        {
            Subject,
            Destination,
        }

        private readonly Dictionary<string, Transform> subjectRoots =
            new Dictionary<string, Transform>(StringComparer.Ordinal);
        private GameplaySession gameplaySession;
        private GameplayWorldRegistry registry;
        private GameplayDialogueLog dialogue;
        private GameplayScenarioAssembly scenario;
        private Func<GameplayActionRecord, bool> beginEncounter;
        private TargetAcquisitionPresenter targetAcquisition;
        private Transform actorTransform;
        private string controlledActorId;
        private DisplacementPreviewPresenter preview;
        private string selectedActionId;
        private TargetingPhase targetingPhase;
        private string pointerCandidateId;
        private DisplacementTargetEvaluation pointerEvaluation;
        private string lockedSubjectId;
        private Vector3 destination;
        private DisplacementDestinationEvaluation destinationEvaluation;
        private string intentPreviewActionId;
        private string intentPreviewSubjectId;
        private Vector3 intentPreviewActorPosition;
        private Vector3 intentPreviewSubjectPosition;

        public GameplayDisplacementSession Session { get; private set; }

        public bool IsTargeting => selectedActionId != null;

        public string SelectedActionId => selectedActionId;

        public string StatusMessage { get; private set; } = string.Empty;

        public GameplayWarningHintModel CurrentWarningHint => !IsTargeting
            ? null
            : new GameplayWarningHintModel(
                "gameplay.displacement",
                targetingPhase == TargetingPhase.Subject
                    ? UsesIntentDestination()
                        ? "AIM AT A VALID SUBJECT - CLICK TO CONFIRM - SELECT "
                            + GetSelectedActionDisplayName().ToUpperInvariant()
                            + " AGAIN OR ESC TO CANCEL"
                        : "AIM AT A VALID SUBJECT - CLICK TO SELECT - SELECT "
                            + GetSelectedActionDisplayName().ToUpperInvariant()
                            + " AGAIN OR ESC TO CANCEL"
                    : "AIM AT A DESTINATION - CLICK TO CONFIRM - SELECT "
                        + GetSelectedActionDisplayName().ToUpperInvariant()
                        + " AGAIN OR ESC TO CANCEL",
                80);

        public bool IsPointerTargetValid =>
            pointerEvaluation?.IsEligible == true;

        public string PointerCandidateId => pointerCandidateId;

        public string PointerTooltip => !IsTargeting
            ? string.Empty
            : targetingPhase == TargetingPhase.Destination
                ? destinationEvaluation?.IsEligible == true
                    ? string.Empty
                    : FormatDestinationFailure(destinationEvaluation)
            : IsPointerTargetValid
                ? UsesIntentDestination()
                    && destinationEvaluation?.IsEligible != true
                        ? FormatDestinationFailure(destinationEvaluation)
                        : string.Empty
                : FormatTargetFailure(pointerEvaluation);

        public IReadOnlyList<DisplacementActionDefinition> AvailableActions =>
            scenario == null
                ? Array.Empty<DisplacementActionDefinition>()
                : scenario.GetActorDefinition(
                    controlledActorId).DisplacementActions;

        public void BeginTargeting(string actionId)
        {
            if (string.IsNullOrWhiteSpace(actionId))
                throw new ArgumentException(
                    "Displacement targeting requires an authored action ID.",
                    nameof(actionId));
            if (scenario != null)
            {
                RequireDisplacementAction(controlledActorId, actionId);
            }

            selectedActionId = actionId;
            targetAcquisition?.SetFeedbackSuppressed(this, true);
            targetingPhase = TargetingPhase.Subject;
            lockedSubjectId = null;
            ClearDestinationPreview();
            ClearPointerCandidate();
            StatusMessage = "AIMING "
                + GetSelectedActionDisplayName().ToUpperInvariant()
                + (UsesIntentDestination()
                    ? " - CONFIRM A SUBJECT"
                    : " - SELECT A SUBJECT");
        }

        public bool TryToggleTargeting(string actionId)
        {
            RequireSession();
            if (string.Equals(
                    selectedActionId,
                    actionId,
                    StringComparison.Ordinal))
            {
                return CancelTargeting();
            }

            DisplacementActionAvailability availability =
                EvaluateActionAvailability(actionId);
            if (!availability.IsAvailable)
            {
                return false;
            }

            BeginTargeting(actionId);
            return true;
        }

        public DisplacementActionAvailability EvaluateActionAvailability(
            string actionId)
        {
            RequireSession();
            return Session.EvaluateActionAvailability(
                controlledActorId,
                actionId);
        }

        public string GetActionTooltip(string actionId)
        {
            DisplacementActionAvailability availability =
                EvaluateActionAvailability(actionId);
            DisplacementActionDefinition action = availability.Action
                ?? RequireDisplacementAction(controlledActorId, actionId);
            string autoStowItemName = availability.RequiresAutoStow
                ? gameplaySession.GetInventoryItem(
                    controlledActorId,
                    availability.AutoStowItemId)?.DisplayName
                : null;
            CloseQuartersControlProfile? controlProfile =
                action.ContestPolicy ==
                    DisplacementContestPolicy.CloseQuartersControl
                        ? Session.GetControlProfile(controlledActorId)
                        : null;
            return DisplacementActionTooltipFormatter.Format(
                action,
                availability.Failure,
                Session.Mode == GameplaySessionMode.TurnBased,
                availability.ResolvedCost,
                autoStowItemName,
                controlProfile);
        }

        public bool CancelTargeting()
        {
            if (!IsTargeting) return false;
            string actionName = GetSelectedActionDisplayName();
            targetAcquisition?.SetFeedbackSuppressed(this, false);
            selectedActionId = null;
            lockedSubjectId = null;
            ClearDestinationPreview();
            ClearPointerCandidate();
            StatusMessage = actionName + " canceled.";
            return true;
        }

        internal void Bind(
            GameplaySession gameplay,
            GameplayDestructibleController destructibles,
            LevelWorld world,
            GameplayWorldRegistry registry,
            GameplayScenarioAssembly scenarioAssembly,
            uint randomSeed,
            TargetAcquisitionPresenter acquisition,
            GameplayDialogueLog dialogueLog,
            Func<GameplayActionRecord, bool> onEncounterStartRequested = null)
        {
            if (gameplay == null)
            {
                throw new ArgumentNullException(nameof(gameplay));
            }

            if (destructibles?.Session == null)
            {
                throw new ArgumentNullException(nameof(destructibles));
            }

            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }

            if (registry == null)
            {
                throw new ArgumentNullException(nameof(registry));
            }

            Unbind();
            gameplaySession = gameplay;
            dialogue = dialogueLog ?? throw new ArgumentNullException(
                nameof(dialogueLog));
            scenario = scenarioAssembly ??
                throw new ArgumentNullException(nameof(scenarioAssembly));
            this.registry = registry;
            targetAcquisition = acquisition ??
                throw new ArgumentNullException(nameof(acquisition));
            beginEncounter = onEncounterStartRequested
                ?? gameplaySession.BeginEncounterFromAction;
            foreach (GameplayActorView actor in registry.Actors)
            {
                subjectRoots.Add(actor.ActorId, actor.Transform);
            }

            foreach (string propId in destructibles.Session.PropIds)
            {
                if (!world.TryGetEntity(propId, out LevelEntityView view))
                {
                    throw new InvalidOperationException(
                        $"Displaceable prop '{propId}' is missing from the level.");
                }

                subjectRoots.Add(propId, view.transform);
            }

            Session = new GameplayDisplacementSession(
                gameplay,
                destructibles.Session,
                scenario.DisplacementSubjects,
                new UnityDisplacementPathValidator(subjectRoots),
                new SeededD20RollSource(randomSeed),
                CreateControlProfiles(scenario));
            preview = new DisplacementPreviewPresenter(transform);
            StatusMessage = string.Empty;
            enabled = true;
            SetActor(scenario.InitiallySelectedActorId);
        }

        public void SetActor(string authoritativeActorId)
        {
            if (Session == null || gameplaySession == null || registry == null)
            {
                throw new InvalidOperationException(
                    "Bind gameplay displacement before changing actors.");
            }
            if (string.IsNullOrWhiteSpace(authoritativeActorId))
            {
                throw new ArgumentException(
                    "Displacement actor identifiers cannot be empty.",
                    nameof(authoritativeActorId));
            }

            gameplaySession.GetActor(authoritativeActorId);
            CancelTargeting();
            controlledActorId = authoritativeActorId;
            actorTransform = registry.GetActor(controlledActorId).Transform;
            StatusMessage = string.Empty;
        }

        public bool TryConfirmTargeting()
        {
            if (!IsTargeting)
            {
                return false;
            }

            if (targetingPhase == TargetingPhase.Subject)
            {
                AcquirePointerCandidate();
            }
            else
            {
                AcquireDestination();
            }

            if (targetingPhase == TargetingPhase.Subject)
            {
                if (!IsPointerTargetValid)
                {
                    StatusMessage = FormatTargetFailure(pointerEvaluation);
                    return false;
                }

                lockedSubjectId = pointerCandidateId;
                float maximumDistance = pointerEvaluation.Action
                    .GetMaximumDistance(pointerEvaluation.Subject);
                if (UsesIntentDestination())
                {
                    if (destinationEvaluation?.IsEligible != true)
                    {
                        StatusMessage = FormatDestinationFailure(
                            destinationEvaluation);
                        return false;
                    }

                    return TryCommitLockedDisplacement();
                }

                targetingPhase = TargetingPhase.Destination;
                ClearPointerCandidate();
                ClearDestinationPreview();
                AcquireDestination();
                StatusMessage = "SUBJECT SELECTED - AIM AT A DESTINATION - "
                    + maximumDistance.ToString("0.#")
                    + " M MAX";
                return true;
            }

            if (destinationEvaluation?.IsEligible != true)
            {
                StatusMessage = FormatDestinationFailure(
                    destinationEvaluation);
                return false;
            }

            return TryCommitLockedDisplacement();
        }

        internal bool TryExecuteIntent(
            string actorId,
            string actionId,
            string subjectId,
            out GameplayActionRecord action,
            out DisplacementRecord record,
            out DisplacementResolutionFailure failure)
        {
            DisplacementDestinationEvaluation destination =
                Session.EvaluateIntentDestination(
                    actorId,
                    actionId,
                    subjectId);
            if (!destination.IsEligible)
            {
                action = null;
                record = null;
                failure = destination.Failure;
                return false;
            }

            if (!Session.TryDisplaceAction(
                    actorId,
                    actionId,
                    subjectId,
                    destination.Destination,
                    out action,
                    out record,
                    out failure))
            {
                return false;
            }

            Present(record);
            return true;
        }

        private bool TryCommitLockedDisplacement()
        {
            string actionName = GetSelectedActionDisplayName();
            if (!TryDisplaceSubject(
                    controlledActorId,
                    selectedActionId,
                    lockedSubjectId,
                    destination,
                    out GameplayActionRecord action,
                    out DisplacementRecord displacement,
                    out DisplacementResolutionFailure failure))
            {
                StatusMessage = FormatDestinationFailure(failure);
                if (UsesIntentDestination())
                {
                    RefreshIntentDestination(lockedSubjectId, force: true);
                }
                else
                {
                    AcquireDestination();
                }
                return false;
            }

            GameplayEncounterActionTransition.BeginAfterCommittedAction(
                gameplaySession,
                action,
                beginEncounter,
                "displacement");

            SynchronizeAuthoritativeFacing();
            targetAcquisition.SetFeedbackSuppressed(this, false);
            selectedActionId = null;
            lockedSubjectId = null;
            ClearDestinationPreview();
            ClearPointerCandidate();
            StatusMessage = displacement.Succeeded
                ? actionName + " resolved."
                : actionName + " resisted.";
            return true;
        }

        public bool TryDisplaceSubject(
            string actorId,
            string actionId,
            string subjectId,
            Vector3 destination,
            out DisplacementRecord record,
            out DisplacementResolutionFailure failure) =>
            TryDisplaceSubject(
                actorId,
                actionId,
                subjectId,
                destination,
                out _,
                out record,
                out failure);

        private bool TryDisplaceSubject(
            string actorId,
            string actionId,
            string subjectId,
            Vector3 destination,
            out GameplayActionRecord action,
            out DisplacementRecord record,
            out DisplacementResolutionFailure failure)
        {
            RequireSession();

            if (!Session.TryDisplaceAction(
                    actorId,
                    actionId,
                    subjectId,
                    ToGameplayPosition(destination),
                    out action,
                    out record,
                    out failure))
            {
                return false;
            }

            Present(record);
            if (GameplayCombatDiagnosticFormatter.TryFormatAction(
                    action,
                    out GameplayDiagnosticProjection diagnostic))
            {
                dialogue.AppendCombatDiagnostic(diagnostic);
            }
            return true;
        }

        public void Commit(DisplacementRecord record)
        {
            RequireSession();
            Session.Commit(record);
            Present(record);
        }

        public void Unbind()
        {
            CancelTargeting();
            Session = null;
            gameplaySession = null;
            registry = null;
            dialogue = null;
            scenario = null;
            beginEncounter = null;
            targetAcquisition = null;
            actorTransform = null;
            controlledActorId = null;
            preview?.Dispose();
            preview = null;
            subjectRoots.Clear();
            StatusMessage = string.Empty;
            enabled = false;
        }

        private void Update()
        {
            if (!IsTargeting)
            {
                ClearPointerCandidate();
                ClearDestinationPreview();
                return;
            }

            if (targetingPhase == TargetingPhase.Subject)
            {
                AcquirePointerCandidate();
            }
            else
            {
                AcquireDestination();
            }
        }

        private void AcquirePointerCandidate()
        {
            ClearPointerCandidate();
            if (targetAcquisition == null
                || !targetAcquisition.TryGetPointerRay(out Ray pointerRay))
            {
                ClearDestinationPreview();
                return;
            }

            RaycastHit[] hits = Physics.RaycastAll(
                pointerRay,
                250f,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (left, right) =>
                left.distance.CompareTo(right.distance));
            foreach (RaycastHit hit in hits)
            {
                foreach (KeyValuePair<string, Transform> subject in subjectRoots)
                {
                    if ((hit.transform == subject.Value
                            || hit.transform.IsChildOf(subject.Value))
                        && HasCharacterLineOfSight(hit.point, subject.Value))
                    {
                        pointerCandidateId = subject.Key;
                        pointerEvaluation = Session.EvaluateTarget(
                            controlledActorId,
                            selectedActionId,
                            subject.Key);
                        if (pointerEvaluation.IsEligible
                            && UsesIntentDestination())
                        {
                            RefreshIntentDestination(subject.Key);
                        }
                        else
                        {
                            ClearDestinationPreview();
                        }
                        return;
                    }
                }
            }

            ClearDestinationPreview();
        }

        private void RefreshIntentDestination(
            string subjectId,
            bool force = false)
        {
            if (!subjectRoots.TryGetValue(
                    subjectId ?? string.Empty,
                    out Transform subjectRoot)
                || subjectRoot == null
                || actorTransform == null)
            {
                ClearDestinationPreview();
                return;
            }

            if (!force
                && destinationEvaluation != null
                && string.Equals(
                    intentPreviewActionId,
                    selectedActionId,
                    StringComparison.Ordinal)
                && string.Equals(
                    intentPreviewSubjectId,
                    subjectId,
                    StringComparison.Ordinal)
                && intentPreviewActorPosition == actorTransform.position
                && intentPreviewSubjectPosition == subjectRoot.position)
            {
                preview?.Show(
                    ToVector3(destinationEvaluation.Origin),
                    destination,
                    destinationEvaluation.IsEligible);
                return;
            }

            destinationEvaluation = Session.EvaluateIntentDestination(
                controlledActorId,
                selectedActionId,
                subjectId);
            destination = ToVector3(destinationEvaluation.Destination);
            intentPreviewActionId = selectedActionId;
            intentPreviewSubjectId = subjectId;
            intentPreviewActorPosition = actorTransform.position;
            intentPreviewSubjectPosition = subjectRoot.position;
            preview?.Show(
                ToVector3(destinationEvaluation.Origin),
                destination,
                destinationEvaluation.IsEligible);
        }

        private void AcquireDestination()
        {
            ClearIntentPreviewCache();
            destinationEvaluation = null;
            preview?.Hide();
            if (lockedSubjectId == null
                || targetAcquisition == null
                || !subjectRoots.TryGetValue(
                    lockedSubjectId,
                    out Transform subjectRoot)
                || subjectRoot == null)
            {
                return;
            }

            DisplacementActionDefinition action = RequireDisplacementAction(
                controlledActorId,
                selectedActionId);
            Vector3 aimOrigin = actorTransform.position;
            float acquisitionRange = action.Reach + action.MaximumDistance;
            if (!targetAcquisition.TryGetPointerSurfacePoint(
                    aimOrigin,
                    acquisitionRange,
                    out Vector3 aimPoint))
            {
                return;
            }

            destination = new Vector3(
                aimPoint.x,
                subjectRoot.position.y,
                aimPoint.z);
            destinationEvaluation = Session.EvaluateDestination(
                controlledActorId,
                selectedActionId,
                lockedSubjectId,
                ToGameplayPosition(destination));
            preview?.Show(
                ToVector3(destinationEvaluation.Origin),
                destination,
                destinationEvaluation.IsEligible);
        }

        private bool HasCharacterLineOfSight(
            Vector3 targetPoint,
            Transform subjectRoot)
        {
            if (actorTransform == null)
            {
                return false;
            }

            Vector3 origin = actorTransform.position + Vector3.up;
            Vector3 direction = targetPoint - origin;
            float distance = direction.magnitude;
            if (distance <= 0.001f)
            {
                return true;
            }

            RaycastHit[] hits = Physics.RaycastAll(
                origin,
                direction / distance,
                distance,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            foreach (RaycastHit hit in hits)
            {
                Transform hitTransform = hit.collider != null
                    ? hit.collider.transform
                    : null;
                if (!BelongsTo(hitTransform, actorTransform)
                    && !BelongsTo(hitTransform, subjectRoot))
                {
                    return false;
                }
            }

            return true;
        }

        private void ClearPointerCandidate()
        {
            pointerCandidateId = null;
            pointerEvaluation = null;
        }

        private void ClearDestinationPreview()
        {
            destinationEvaluation = null;
            ClearIntentPreviewCache();
            preview?.Hide();
        }

        private void ClearIntentPreviewCache()
        {
            intentPreviewActionId = null;
            intentPreviewSubjectId = null;
            intentPreviewActorPosition = default(Vector3);
            intentPreviewSubjectPosition = default(Vector3);
        }

        private bool UsesIntentDestination()
        {
            if (scenario == null || selectedActionId == null)
            {
                return false;
            }

            DisplacementActionKind intent = RequireDisplacementAction(
                controlledActorId,
                selectedActionId).Intent;
            return intent == DisplacementActionKind.Push
                || intent == DisplacementActionKind.PushOff;
        }

        internal static string FormatTargetFailure(
            DisplacementTargetEvaluation evaluation)
        {
            if (evaluation == null)
            {
                return "INVALID TARGET";
            }

            return FormatTargetFailure(
                evaluation.Failure,
                evaluation.Subject?.Mass ?? 0f,
                evaluation.Action?.MaximumSubjectMass ?? 0f,
                evaluation.Distance,
                evaluation.Action?.Reach ?? 0f,
                evaluation.Action?.DisplayName);
        }

        internal static string FormatTargetFailure(
            DisplacementTargetFailure failure,
            float subjectMass = 0f,
            float maximumSubjectMass = 0f,
            float distance = 0f,
            float reach = 0f,
            string actionDisplayName = null)
        {
            switch (failure)
            {
                case DisplacementTargetFailure.ActorUnavailable:
                    return "INVALID TARGET - ACTOR UNAVAILABLE";
                case DisplacementTargetFailure.ActionUnavailable:
                    return "INVALID TARGET - ACTION UNAVAILABLE";
                case DisplacementTargetFailure.CandidateUnavailable:
                    return "INVALID TARGET - NOT DISPLACEABLE";
                case DisplacementTargetFailure.SelfTarget:
                    return "INVALID TARGET - CANNOT TARGET SELF";
                case DisplacementTargetFailure.SubjectKindNotAccepted:
                    return "INVALID TARGET - SUBJECT TYPE NOT ACCEPTED";
                case DisplacementTargetFailure.SubjectTooHeavy:
                    return "INVALID TARGET - TOO HEAVY ("
                        + subjectMass.ToString("0.#")
                        + " / "
                        + maximumSubjectMass.ToString("0.#")
                        + " KG)";
                case DisplacementTargetFailure.SubjectTooLarge:
                    return "INVALID TARGET - TOO LARGE";
                case DisplacementTargetFailure.SubjectOutOfReach:
                    return "INVALID TARGET - OUT OF REACH ("
                        + distance.ToString("0.#")
                        + " / "
                        + reach.ToString("0.#")
                        + " M)";
                case DisplacementTargetFailure.NotPinningActor:
                    return "INVALID TARGET - NOT THE PINNING PROP";
                case DisplacementTargetFailure.SubjectPinned:
                    return "INVALID TARGET - SUBJECT IS PINNED OR PINNING AN ACTOR";
                case DisplacementTargetFailure.None:
                    if (string.IsNullOrWhiteSpace(actionDisplayName))
                    {
                        throw new ArgumentException(
                            "Eligible target formatting requires an action name.",
                            nameof(actionDisplayName));
                    }

                    return actionDisplayName.ToUpperInvariant();
                default:
                    throw new ArgumentOutOfRangeException(nameof(failure));
            }
        }

        internal static string FormatDestinationFailure(
            DisplacementDestinationEvaluation evaluation) =>
            evaluation == null
                ? "INVALID DESTINATION"
                : evaluation.IsEligible
                    ? "VALID DESTINATION - CLICK TO CONFIRM"
                    : FormatDestinationFailure(evaluation.Failure);

        internal static string FormatDestinationFailure(
            DisplacementResolutionFailure failure)
        {
            switch (failure)
            {
                case DisplacementResolutionFailure.DestinationUnchanged:
                    return "INVALID DESTINATION - MOVE THE SUBJECT";
                case DisplacementResolutionFailure.DestinationTooFar:
                    return "INVALID DESTINATION - TOO FAR";
                case DisplacementResolutionFailure.DestinationBlocked:
                    return "INVALID DESTINATION - PATH BLOCKED";
                case DisplacementResolutionFailure.TurnModeRequired:
                    return "ACTION UNAVAILABLE - TURN MODE REENTRY LOCKED";
                case DisplacementResolutionFailure.InsufficientTurnBudget:
                    return "ACTION UNAVAILABLE - INSUFFICIENT AP";
                case DisplacementResolutionFailure.ActorNotActive:
                    return "ACTION UNAVAILABLE - NOT YOUR TURN";
                case DisplacementResolutionFailure.OperationInProgress:
                    return "ACTION UNAVAILABLE - OPERATION IN PROGRESS";
                case DisplacementResolutionFailure.SubjectOutOfReach:
                    return "INVALID SUBJECT - OUT OF REACH";
                case DisplacementResolutionFailure.SubjectTooHeavy:
                    return "INVALID SUBJECT - TOO HEAVY";
                case DisplacementResolutionFailure.SubjectTooLarge:
                    return "INVALID SUBJECT - TOO LARGE";
                case DisplacementResolutionFailure.SubjectKindNotAccepted:
                    return "INVALID SUBJECT - TYPE NOT ACCEPTED";
                case DisplacementResolutionFailure.ActionUnavailable:
                    return "ACTION UNAVAILABLE";
                case DisplacementResolutionFailure.HandsOccupied:
                    return "ACTION UNAVAILABLE - REQUIRED HANDS ARE OCCUPIED";
                case DisplacementResolutionFailure.SubjectUnavailable:
                    return "INVALID SUBJECT - UNAVAILABLE";
                case DisplacementResolutionFailure.ActorPinned:
                    return "ACTION UNAVAILABLE - PUSH OFF THE PINNING PROP";
                case DisplacementResolutionFailure.ActorNotPinned:
                    return "ACTION UNAVAILABLE - ACTOR IS NOT PINNED";
                case DisplacementResolutionFailure.NotPinningActor:
                    return "INVALID SUBJECT - NOT THE PINNING PROP";
                case DisplacementResolutionFailure.SubjectPinned:
                    return "INVALID SUBJECT - PIN STATE PREVENTS DISPLACEMENT";
                case DisplacementResolutionFailure.None:
                    return "VALID DESTINATION - CLICK TO CONFIRM";
                default:
                    throw new ArgumentOutOfRangeException(nameof(failure));
            }
        }

        private DisplacementActionDefinition RequireDisplacementAction(
            string actorId,
            string actionId)
        {
            DisplacementActionDefinition definition = scenario
                .GetActorDefinition(actorId)
                .GetDisplacementAction(actionId);
            if (definition == null)
            {
                throw new InvalidOperationException(
                    $"Actor '{actorId}' does not own displacement action "
                    + $"'{actionId}'.");
            }

            return definition;
        }

        private string GetSelectedActionDisplayName()
        {
            if (scenario == null)
            {
                return selectedActionId;
            }

            return RequireDisplacementAction(
                controlledActorId,
                selectedActionId).DisplayName;
        }

        private void Present(DisplacementRecord record)
        {
            if (!record.Succeeded)
            {
                return;
            }

            if (!subjectRoots.TryGetValue(record.Request.SubjectId, out var subject)
                || subject == null)
            {
                throw new InvalidOperationException(
                    $"Displacement subject '{record.Request.SubjectId}' has no presenter.");
            }

            CharacterController characterController =
                subject.GetComponent<CharacterController>();
            bool controllerWasEnabled =
                characterController != null && characterController.enabled;
            if (controllerWasEnabled)
            {
                characterController.enabled = false;
            }

            GameplayPosition position = record.ResultingPosition;
            subject.position = new Vector3(position.X, position.Y, position.Z);
            if (record.ResultingPropState != null)
            {
                GameplayPropPose pose = record.ResultingPropState.Pose;
                subject.rotation = Quaternion.Euler(
                    pose.PitchDegrees,
                    pose.YawDegrees,
                    pose.RollDegrees);
            }
            if (controllerWasEnabled)
            {
                characterController.enabled = true;
            }

            Physics.SyncTransforms();
            if (record.PinTransition != null
                && registry.TryGetActor(
                    record.PinTransition.ActorId,
                    out GameplayActorView pinnedActor))
            {
                pinnedActor.ReplayActions.PresentPinState(
                    record.PinTransition.ResultingState);
                ActorAnimationCoordinator animation =
                    pinnedActor.Root.GetComponent<ActorAnimationCoordinator>();
                animation?.TryRequestAction(
                    record.PinTransition.EstablishesPin
                        ? ActorAnimationAction.HitReaction
                        : ActorAnimationAction.Interact);
            }
        }

        private void SynchronizeAuthoritativeFacing()
        {
            if (Session == null || actorTransform == null || scenario == null)
            {
                return;
            }

            GameplayActorPose pose = gameplaySession.GetActor(
                controlledActorId).Pose;
            actorTransform.rotation = Quaternion.Euler(
                0f,
                pose.FacingDegrees,
                0f);
        }

        private static IReadOnlyDictionary<string, CloseQuartersControlProfile>
            CreateControlProfiles(GameplayScenarioAssembly assembly)
        {
            var profiles =
                new Dictionary<string, CloseQuartersControlProfile>(
                    StringComparer.Ordinal);
            foreach (ScenarioActorRuntimeDefinition actor in assembly.Actors)
            {
                profiles.Add(
                    actor.Id,
                    actor.ControlProfile);
            }

            return profiles;
        }

        private void RequireSession()
        {
            if (Session == null)
            {
                throw new InvalidOperationException(
                    "Displacement gameplay is not bound to a level.");
            }
        }

        private static GameplayPosition ToGameplayPosition(Vector3 position) =>
            new GameplayPosition(position.x, position.y, position.z);

        private static Vector3 ToVector3(GameplayPosition position) =>
            new Vector3(position.X, position.Y, position.Z);

        private static bool BelongsTo(Transform candidate, Transform root) =>
            candidate != null
            && root != null
            && (candidate == root || candidate.IsChildOf(root));

        private void OnDestroy()
        {
            Unbind();
        }
    }
}
