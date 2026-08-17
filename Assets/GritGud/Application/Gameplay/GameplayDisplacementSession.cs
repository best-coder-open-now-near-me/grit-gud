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
        int RollD20();
    }

    public sealed class SeededD20RollSource : ID20RollSource
    {
        private uint state;

        public SeededD20RollSource(uint seed)
        {
            state = seed != 0u ? seed : 0x6D2B79F5u;
        }

        public int RollD20()
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return (int)(state % 20u) + 1;
        }
    }

    public sealed class GameplayDisplacementSession
    {
        private readonly GameplaySession gameplay;
        private readonly DestructiblePropSession destructibles;
        private readonly Dictionary<string, DisplacementSubjectDefinition>
            subjects;
        private readonly Dictionary<string, CloseQuartersControlProfile>
            controlProfiles;
        private readonly ID20RollSource rollSource;
        private readonly DisplacementActionEvaluator actionEvaluator;
        private readonly DisplacementPinTransitionResolver pinTransitionResolver;
        private readonly DisplacementDestinationEvaluator destinationEvaluator;
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

            subjects = new Dictionary<string, DisplacementSubjectDefinition>(
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

            controlProfiles =
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
            rollSource = rolls ??
                throw new ArgumentNullException(nameof(rolls));
            actionEvaluator = new DisplacementActionEvaluator(
                gameplay,
                destructibles,
                subjects,
                controlProfiles);
            pinTransitionResolver = new DisplacementPinTransitionResolver(
                gameplay,
                subjects);
            destinationEvaluator = new DisplacementDestinationEvaluator(
                gameplay,
                destructibles,
                actionEvaluator,
                pinTransitionResolver,
                resolvedPathValidator);
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
                records.Count + 1L);

        public DisplacementDestinationEvaluation EvaluateIntentDestination(
            string actorId,
            string actionId,
            string subjectId) =>
            destinationEvaluator.EvaluateIntent(
                actorId,
                actionId,
                subjectId,
                records.Count + 1L);

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
                records.Count + 1L);

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
                ? TryResolveProp(
                    actorId,
                    subjectId,
                    target.Subject,
                    destination,
                    definition,
                    out record,
                    out failure)
                : TryResolveCombatant(
                    actorId,
                    subjectId,
                    target.Subject.Mass,
                    destination,
                    definition,
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
                gameplay.LastResolvedAction == null
                    ? 1L
                    : gameplay.LastResolvedAction.Sequence + 1L,
                new GameplayActionRequest(
                    actorId,
                    definition.Id,
                    subjectId),
                cost,
                actor.TurnBudget,
                resultingBudget,
                outcomes);
            gameplay.ValidateActionCommit(action);
            ValidateCommit(record);
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

        private bool TryResolveProp(
            string actorId,
            string propId,
            DisplacementSubjectDefinition subject,
            GameplayPosition destination,
            DisplacementActionDefinition definition,
            out DisplacementRecord record,
            out DisplacementResolutionFailure failure)
        {
            gameplay.GetActor(actorId);
            DestructiblePropSnapshot prop = destructibles.GetProp(propId);
            PropDisplacementState resultingState =
                DisplacementDestinationEvaluator.ResolvePropState(
                subject,
                definition,
                prop,
                destination,
                out DisplacementResultPolicies appliedResults);
            var request = new DisplacementRequest(
                actorId,
                definition.Id,
                propId,
                DisplacementSubjectKind.Prop,
                subject.Mass,
                subject.Size,
                resultingState.Pose.Position,
                definition.Intent);
            if (!destinationEvaluator.TryValidateRequest(
                    request,
                    prop.Position,
                    destination,
                    definition,
                    resultingState,
                    out DisplacementPathValidation path,
                    out failure))
            {
                record = null;
                return false;
            }

            if (!pinTransitionResolver.TryResolve(
                    actorId,
                    subject,
                    definition,
                    prop,
                    path,
                    records.Count + 1L,
                    ref appliedResults,
                    out ActorPinTransition pinTransition,
                    out failure))
            {
                record = null;
                return false;
            }

            record = new DisplacementRecord(
                records.Count + 1L,
                request,
                new PropDisplacementState(prop.Pose, prop.Posture),
                resultingState,
                appliedResults,
                pinTransition);
            failure = DisplacementResolutionFailure.None;
            return true;
        }

        private bool TryResolveCombatant(
            string actorId,
            string targetActorId,
            float targetMass,
            GameplayPosition destination,
            DisplacementActionDefinition definition,
            out DisplacementRecord record,
            out DisplacementResolutionFailure failure)
        {
            if (!controlProfiles.TryGetValue(
                    actorId,
                    out CloseQuartersControlProfile attacker)
                || !controlProfiles.TryGetValue(
                    targetActorId,
                    out CloseQuartersControlProfile defender))
            {
                record = null;
                failure = DisplacementResolutionFailure.SubjectUnavailable;
                return false;
            }

            return TryResolveCombatant(
                actorId,
                targetActorId,
                targetMass,
                destination,
                definition,
                attacker,
                defender,
                out record,
                out failure);
        }

        private bool TryResolveCombatant(
            string actorId,
            string targetActorId,
            float targetMass,
            GameplayPosition destination,
            DisplacementActionDefinition definition,
            CloseQuartersControlProfile attacker,
            CloseQuartersControlProfile defender,
            out DisplacementRecord record,
            out DisplacementResolutionFailure failure)
        {
            gameplay.GetActor(actorId);
            GameplayActorSnapshot target = gameplay.GetActor(targetActorId);
            var request = new DisplacementRequest(
                actorId,
                definition.Id,
                targetActorId,
                DisplacementSubjectKind.Combatant,
                targetMass,
                GetSubjectSize(targetActorId),
                destination,
                definition.Intent);
            if (!destinationEvaluator.TryValidateRequest(
                    request,
                    target.Pose.Position,
                    destination,
                    definition,
                    resultingPropState: null,
                    out _,
                    out failure))
            {
                record = null;
                return false;
            }

            var contest = new CloseQuartersControlRecord(
                rollSource.RollD20(),
                attacker,
                rollSource.RollD20(),
                defender);
            GameplayPosition result = contest.AttackerSucceeded
                ? destination
                : target.Pose.Position;
            record = new DisplacementRecord(
                records.Count + 1L,
                request,
                target.Pose.Position,
                result,
                contest);
            failure = DisplacementResolutionFailure.None;
            return true;
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
                ValidateCommit(record);

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

        private void ValidateCommit(DisplacementRecord record)
        {
            if (record == null)
            {
                throw new ArgumentNullException(nameof(record));
            }

            long expectedSequence = records.Count + 1L;
            if (record.Sequence != expectedSequence)
            {
                throw new InvalidOperationException(
                    "The displacement record is not the next authoritative sequence.");
            }

            if (record.Request.SubjectKind == DisplacementSubjectKind.Prop)
            {
                DestructiblePropSnapshot current = destructibles.GetProp(
                    record.Request.SubjectId);
                if (record.PreviousPropState == null
                    || current.Posture != record.PreviousPropState.Posture
                    || !current.Pose.HasSameState(
                        record.PreviousPropState.Pose))
                {
                    throw new InvalidOperationException(
                        "The displacement record no longer starts from authoritative prop state.");
                }

                gameplay.ValidatePinTransition(record.PinTransition);
            }
            else if (gameplay.GetActor(record.Request.SubjectId).Pose.Position
                .DistanceTo(record.PreviousPosition) > 0f)
            {
                throw new InvalidOperationException(
                    "The displacement record no longer starts at authoritative position.");
            }
        }

        private DisplacementSizeClass GetSubjectSize(string subjectId) =>
            actionEvaluator.GetSubjectSize(subjectId);
    }
}
