using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;

namespace GritGud.Application.Gameplay
{
    public enum DisplacementResolutionFailure
    {
        None,
        TurnModeRequired,
        SubjectTooHeavy,
        SubjectTooLarge,
        DestinationUnchanged,
        DestinationTooFar,
        DestinationBlocked,
        InsufficientTurnBudget,
        ActorNotActive,
        HandsOccupied,
        OperationInProgress,
        ActionUnavailable,
        SubjectUnavailable,
        SubjectKindNotAccepted,
        SubjectOutOfReach,
        ActorPinned,
        ActorNotPinned,
        NotPinningActor,
        SubjectPinned,
        GetUpSpaceBlocked,
    }

    public readonly struct DisplacementPathValidation
    {
        public const string GetUpSpaceBlockedFailureCode =
            "displacement.get-up-space-blocked";

        private DisplacementPathValidation(
            bool accepted,
            string failureCode,
            IReadOnlyList<DisplacementContactEvidence> contacts)
        {
            Accepted = accepted;
            FailureCode = failureCode ?? string.Empty;
            Contacts = contacts ?? Array.Empty<DisplacementContactEvidence>();
        }

        public bool Accepted { get; }

        public string FailureCode { get; }

        public IReadOnlyList<DisplacementContactEvidence> Contacts { get; }

        public static DisplacementPathValidation Allowed() =>
            new DisplacementPathValidation(
                true,
                string.Empty,
                Array.Empty<DisplacementContactEvidence>());

        public static DisplacementPathValidation Allowed(
            IEnumerable<DisplacementContactEvidence> contacts)
        {
            var copy = new List<DisplacementContactEvidence>();
            foreach (DisplacementContactEvidence contact in
                contacts ?? Array.Empty<DisplacementContactEvidence>())
            {
                if (contact == null)
                {
                    throw new ArgumentException(
                        "Displacement contacts cannot contain null entries.",
                        nameof(contacts));
                }
                copy.Add(contact);
            }
            copy.Sort((left, right) => StringComparer.Ordinal.Compare(
                left.EntityId,
                right.EntityId));
            return new DisplacementPathValidation(
                true,
                string.Empty,
                copy.AsReadOnly());
        }

        public static DisplacementPathValidation Blocked(string failureCode)
        {
            if (string.IsNullOrWhiteSpace(failureCode))
            {
                throw new ArgumentException(
                    "Blocked paths require a failure code.",
                    nameof(failureCode));
            }

            return new DisplacementPathValidation(
                false,
                failureCode,
                Array.Empty<DisplacementContactEvidence>());
        }
    }

    public interface IDisplacementPathValidator
    {
        DisplacementPathValidation Validate(
            DisplacementRequest request,
            GameplayPosition origin,
            PropDisplacementState resultingPropState);
    }

    public interface ID20RollSource
    {
        int RollD20(
            GameplayTransitionIdentity transition,
            string purpose);
    }

    public sealed class AddressedD20RollSource : ID20RollSource
    {
        private readonly ScenarioRunIdentity run;

        public AddressedD20RollSource(ScenarioRunIdentity runIdentity)
        {
            run = runIdentity ?? throw new ArgumentNullException(
                nameof(runIdentity));
        }

        public int RollD20(
            GameplayTransitionIdentity transition,
            string purpose) => GameplayAddressedRandom.RollD20(
                run,
                transition,
                purpose);
    }

    public sealed class GameplayDisplacementSession
    {
        private readonly GameplaySession gameplay;
        private readonly DestructiblePropSession destructibles;
        private readonly DisplacementActionEvaluator actionEvaluator;
        private readonly DisplacementDestinationEvaluator destinationEvaluator;
        private readonly DisplacementPropResolver propResolver;
        private readonly DisplacementContestResolver contestResolver;
        private readonly DisplacementRecordCommitValidator commitValidator;
        private readonly List<DisplacementRecord> records =
            new List<DisplacementRecord>();
        private readonly IReadOnlyList<DisplacementRecord> readOnlyRecords;

        public GameplayDisplacementSession(
            GameplaySession gameplaySession,
            DestructiblePropSession destructibleSession,
            IEnumerable<DisplacementSubjectDefinition> subjectDefinitions,
            IDisplacementPathValidator validator,
            ID20RollSource rolls,
            IReadOnlyDictionary<string, CloseQuartersControlProfile>
                authoredControlProfiles = null)
        {
            gameplay = gameplaySession ??
                throw new ArgumentNullException(nameof(gameplaySession));
            destructibles = destructibleSession ??
                throw new ArgumentNullException(nameof(destructibleSession));
            if (!ReferenceEquals(gameplay.Journal, destructibles.Journal))
            {
                throw new ArgumentException(
                    "Displacement participants must share one gameplay journal.",
                    nameof(destructibleSession));
            }

            if (subjectDefinitions == null)
            {
                throw new ArgumentNullException(nameof(subjectDefinitions));
            }

            var subjects =
                new Dictionary<string, DisplacementSubjectDefinition>(
                    StringComparer.Ordinal);
            foreach (DisplacementSubjectDefinition subject in
                subjectDefinitions)
            {
                if (subject == null)
                {
                    throw new ArgumentException(
                        "Displacement subjects cannot contain null entries.",
                        nameof(subjectDefinitions));
                }

                if (!subjects.TryAdd(subject.Id, subject))
                {
                    throw new ArgumentException(
                        $"Displacement subject '{subject.Id}' is defined more than once.",
                        nameof(subjectDefinitions));
                }
            }

            var controlProfiles =
                new Dictionary<string, CloseQuartersControlProfile>(
                    StringComparer.Ordinal);
            if (authoredControlProfiles != null)
            {
                foreach (KeyValuePair<string, CloseQuartersControlProfile>
                    profile in authoredControlProfiles)
                {
                    RequireId(profile.Key, nameof(authoredControlProfiles));
                    controlProfiles.Add(profile.Key, profile.Value);
                }
            }

            IDisplacementPathValidator resolvedPathValidator = validator ??
                throw new ArgumentNullException(nameof(validator));
            ID20RollSource resolvedRollSource = rolls ??
                throw new ArgumentNullException(nameof(rolls));
            actionEvaluator = new DisplacementActionEvaluator(
                gameplay,
                destructibles,
                subjects,
                controlProfiles);
            var pinTransitionResolver = new DisplacementPinTransitionResolver(
                gameplay,
                subjects);
            destinationEvaluator = new DisplacementDestinationEvaluator(
                gameplay,
                destructibles,
                actionEvaluator,
                pinTransitionResolver,
                resolvedPathValidator);
            propResolver = new DisplacementPropResolver(
                gameplay,
                destructibles,
                destinationEvaluator,
                pinTransitionResolver);
            contestResolver = new DisplacementContestResolver(
                gameplay,
                controlProfiles,
                resolvedRollSource,
                actionEvaluator,
                destinationEvaluator);
            commitValidator = new DisplacementRecordCommitValidator(
                gameplay,
                destructibles);
            readOnlyRecords = records.AsReadOnly();
        }

        public IReadOnlyList<DisplacementRecord> Records => readOnlyRecords;

        public GameplayJournal Journal => gameplay.Journal;

        public GameplaySessionMode Mode => gameplay.Mode;

        public CloseQuartersControlProfile GetControlProfile(string actorId) =>
            actionEvaluator.GetControlProfile(actorId);

        public DisplacementActionAvailability EvaluateActionAvailability(
            string actorId,
            string actionId) =>
            EvaluateActionAvailability(
                actorId,
                actionId,
                startsEncounter: false);

        private DisplacementActionAvailability EvaluateActionAvailability(
            string actorId,
            string actionId,
            bool startsEncounter) =>
            actionEvaluator.EvaluateAvailability(
                actorId,
                actionId,
                startsEncounter);

        public DisplacementTargetEvaluation EvaluateTarget(
            string actorId,
            string actionId,
            string candidateId) =>
            actionEvaluator.EvaluateTarget(actorId, actionId, candidateId);

        public DisplacementDestinationEvaluation EvaluateDestination(
            string actorId,
            string actionId,
            string subjectId,
            GameplayPosition destination) =>
            destinationEvaluator.Evaluate(
                actorId,
                actionId,
                subjectId,
                destination,
                gameplay.NextActionSequence);

        public DisplacementDestinationEvaluation EvaluateIntentDestination(
            string actorId,
            string actionId,
            string subjectId) =>
            destinationEvaluator.EvaluateIntent(
                actorId,
                actionId,
                subjectId,
                gameplay.NextActionSequence);

        public DisplacementDestinationEvaluation
            EvaluateDirectionalPushOffDestination(
                string actorId,
                string actionId,
                string subjectId,
                GameplayPosition directionTarget) =>
            destinationEvaluator.EvaluateDirectionalPushOff(
                actorId,
                actionId,
                subjectId,
                directionTarget,
                gameplay.NextActionSequence);

        public bool TryDisplaceAction(
            string actorId,
            string actionId,
            string subjectId,
            GameplayPosition destination,
            out GameplayActionRecord action,
            out DisplacementRecord record,
            out DisplacementResolutionFailure failure)
        {
            action = null;
            record = null;
            DisplacementActionAvailability availability =
                EvaluateActionAvailability(actorId, actionId);
            if (!availability.IsAvailable)
            {
                failure = DisplacementActionEvaluator.ToResolutionFailure(
                    availability.Failure);
                return false;
            }
            DisplacementActionDefinition definition = availability.Action;
            DisplacementTargetEvaluation target = EvaluateTarget(
                actorId,
                actionId,
                subjectId);
            if (!target.IsEligible)
            {
                failure = DisplacementActionEvaluator.ToResolutionFailure(
                    target.Failure);
                return false;
            }
            bool startsEncounter = target.Subject.Kind
                    == DisplacementSubjectKind.Combatant
                && gameplay.AttackStartsEncounter(subjectId);
            if (startsEncounter
                && !gameplay.EncounterActive
                && gameplay.Mode == GameplaySessionMode.Exploration
                && !gameplay.CanEnterTurnMode)
            {
                failure = DisplacementResolutionFailure.TurnModeRequired;
                return false;
            }
            if (startsEncounter)
            {
                availability = EvaluateActionAvailability(
                    actorId,
                    actionId,
                    startsEncounter: true);
                if (!availability.IsAvailable)
                {
                    failure = DisplacementActionEvaluator.ToResolutionFailure(
                        availability.Failure);
                    return false;
                }
            }
            GameplayActorSnapshot actor = gameplay.GetActor(actorId);
            long actionSequence = gameplay.NextActionSequence;
            ActionCost cost = availability.ResolvedCost;
            TurnBudget resultingBudget;
            try
            {
                resultingBudget = actor.TurnBudget.SpendAction(cost);
            }
            catch (InvalidOperationException)
            {
                failure = DisplacementResolutionFailure.InsufficientTurnBudget;
                return false;
            }

            bool resolved = target.Subject.Kind == DisplacementSubjectKind.Prop
                ? propResolver.TryResolve(
                    actorId,
                    subjectId,
                    target.Subject,
                    destination,
                    definition,
                    actionSequence,
                    out record,
                    out failure)
                : contestResolver.TryResolve(
                    actorId,
                    subjectId,
                    target.Subject.Mass,
                    destination,
                    definition,
                    actionSequence,
                    out record,
                    out failure);
            if (!resolved)
            {
                return false;
            }

            var outcomes = new List<GameplayActionOutcome>();
            if (availability.RequiresAutoStow)
            {
                outcomes.Add(new EquipmentChangedActionOutcome(
                    new EquipmentChangeRecord(
                        actorId,
                        availability.AutoStowItemId,
                        EquipmentChangeKind.Unequip,
                        availability.AutoStowItemId,
                        resultingEquippedItemId: null)));
            }
            outcomes.Add(new DisplacementActionOutcome(record));
            action = new GameplayActionRecord(
                actionSequence,
                new GameplayActionRequest(
                    actorId,
                    definition.Id,
                    subjectId),
                cost,
                actor.TurnBudget,
                resultingBudget,
                outcomes);
            gameplay.ValidateActionCommit(action);
            commitValidator.Validate(record, actionSequence);
            var notifications = new GameplayNotificationBatch();
            gameplay.CommitAction(action, notifications);
            Commit(
                record,
                validate: false,
                notifications: notifications);
            notifications.Publish();
            failure = DisplacementResolutionFailure.None;
            return true;
        }

        private static void RequireId(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    "Displacement identifiers cannot be empty.",
                    parameterName);
            }
        }

        public void Commit(DisplacementRecord record)
        {
            var notifications = new GameplayNotificationBatch();
            Commit(
                record,
                validate: true,
                notifications: notifications);
            notifications.Publish();
        }

        private void Commit(
            DisplacementRecord record,
            bool validate,
            GameplayNotificationBatch notifications)
        {
            if (notifications == null)
                throw new ArgumentNullException(nameof(notifications));
            if (validate)
                commitValidator.Validate(record, records.Count + 1L);

            if (record.Succeeded)
            {
                if (record.Request.SubjectKind == DisplacementSubjectKind.Prop)
                {
                    destructibles.CommitDisplacement(record);
                    gameplay.CommitPinTransition(
                        record.PinTransition,
                        notifications,
                        validatePrevious: validate);
                }
                else
                {
                    gameplay.CommitForcedDisplacement(record);
                }
            }

            records.Add(record);
            gameplay.Journal.RecordDisplacementResolved(record);
        }

    }
}
