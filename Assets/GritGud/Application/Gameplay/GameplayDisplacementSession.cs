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
        private const int IntentDestinationSearchIterations = 10;
        private const float MinimumIntentDisplacement = 0.05f;
        private static readonly float[] PushOffCandidateOffsetsDegrees =
        {
            0f,
            45f,
            -45f,
            90f,
            -90f,
            135f,
            -135f,
            180f,
        };
        private readonly GameplaySession gameplay;
        private readonly DestructiblePropSession destructibles;
        private readonly Dictionary<string, DisplacementSubjectDefinition>
            subjects;
        private readonly Dictionary<string, CloseQuartersControlProfile>
            controlProfiles;
        private readonly IDisplacementPathValidator pathValidator;
        private readonly ID20RollSource rollSource;
        private readonly DisplacementActionEvaluator actionEvaluator;
        private readonly DisplacementPinTransitionResolver pinTransitionResolver;
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

            pathValidator = validator ??
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
            GameplayPosition destination)
        {
            RequireId(actorId, nameof(actorId));
            RequireId(actionId, nameof(actionId));
            RequireId(subjectId, nameof(subjectId));

            GameplayPosition origin = default(GameplayPosition);
            if (subjects.TryGetValue(
                    subjectId,
                    out DisplacementSubjectDefinition authoredSubject))
            {
                TryGetSubjectPosition(authoredSubject, out origin);
            }

            DisplacementActionAvailability availability =
                EvaluateActionAvailability(actorId, actionId);
            if (!availability.IsAvailable)
            {
                return CreateDestinationEvaluation(
                    actorId,
                    actionId,
                    subjectId,
                    origin,
                    destination,
                    ToResolutionFailure(availability.Failure),
                    availability.Action);
            }

            DisplacementTargetEvaluation target = EvaluateTarget(
                actorId,
                actionId,
                subjectId);
            if (!target.IsEligible
                || !TryGetSubjectPosition(target.Subject, out origin))
            {
                return CreateDestinationEvaluation(
                    actorId,
                    actionId,
                    subjectId,
                    origin,
                    destination,
                    target.IsEligible
                        ? DisplacementResolutionFailure.SubjectUnavailable
                        : ToResolutionFailure(target.Failure),
                    availability.Action);
            }

            var request = new DisplacementRequest(
                actorId,
                actionId,
                subjectId,
                target.Subject.Kind,
                target.Subject.Mass,
                target.Subject.Size,
                destination,
                availability.Action.Intent);
            PropDisplacementState resultingPropState = null;
            DestructiblePropSnapshot? prop = null;
            DisplacementResultPolicies appliedResults =
                DisplacementResultPolicies.None;
            if (target.Subject.Kind == DisplacementSubjectKind.Prop
                && destructibles.TryGetProp(
                    subjectId,
                    out DestructiblePropSnapshot resolvedProp))
            {
                prop = resolvedProp;
                resultingPropState = ResolvePropState(
                    target.Subject,
                    availability.Action,
                    resolvedProp,
                    destination,
                    out appliedResults);
                request = new DisplacementRequest(
                    actorId,
                    actionId,
                    subjectId,
                    target.Subject.Kind,
                    target.Subject.Mass,
                    target.Subject.Size,
                    resultingPropState.Pose.Position,
                    availability.Action.Intent);
            }
            bool valid = ValidateRequest(
                request,
                origin,
                destination,
                availability.Action,
                resultingPropState,
                out DisplacementPathValidation path,
                out DisplacementResolutionFailure failure);
            if (valid
                && prop.HasValue
                && !pinTransitionResolver.TryResolve(
                    actorId,
                    target.Subject,
                    availability.Action,
                    prop.Value,
                    path,
                    records.Count + 1L,
                    ref appliedResults,
                    out _,
                    out failure))
            {
                valid = false;
            }
            return CreateDestinationEvaluation(
                actorId,
                actionId,
                subjectId,
                origin,
                destination,
                valid ? DisplacementResolutionFailure.None : failure,
                availability.Action);
        }

        public DisplacementDestinationEvaluation EvaluateIntentDestination(
            string actorId,
            string actionId,
            string subjectId)
        {
            RequireId(actorId, nameof(actorId));
            RequireId(actionId, nameof(actionId));
            RequireId(subjectId, nameof(subjectId));

            DisplacementActionAvailability availability =
                EvaluateActionAvailability(actorId, actionId);
            DisplacementTargetEvaluation target = EvaluateTarget(
                actorId,
                actionId,
                subjectId);
            if (!availability.IsAvailable || !target.IsEligible)
            {
                GameplayPosition unavailableDestination = target.Subject != null
                    && TryGetSubjectPosition(
                        target.Subject,
                        out GameplayPosition unavailableOrigin)
                            ? unavailableOrigin
                            : default(GameplayPosition);
                return EvaluateDestination(
                    actorId,
                    actionId,
                    subjectId,
                    unavailableDestination);
            }

            if (!TryGetSubjectPosition(
                    target.Subject,
                    out GameplayPosition origin))
            {
                return EvaluateDestination(
                    actorId,
                    actionId,
                    subjectId,
                    default(GameplayPosition));
            }

            GameplayPosition actorPosition = gameplay.GetActor(
                actorId).Pose.Position;
            float directionX;
            float directionZ;
            switch (availability.Action.Intent)
            {
                case DisplacementActionKind.Push:
                case DisplacementActionKind.PushOff:
                    directionX = origin.X - actorPosition.X;
                    directionZ = origin.Z - actorPosition.Z;
                    break;
                default:
                    return EvaluateDestination(
                        actorId,
                        actionId,
                        subjectId,
                        origin);
            }

            float directionMagnitude = (float)Math.Sqrt(
                (directionX * directionX) + (directionZ * directionZ));
            if (directionMagnitude <= 0f
                && availability.Action.Intent ==
                    DisplacementActionKind.PushOff)
            {
                ActorPinState pin = gameplay.GetActor(actorId).PinState;
                directionX = pin?.Contact.Normal.X ?? 0f;
                directionZ = pin?.Contact.Normal.Z ?? 0f;
                directionMagnitude = (float)Math.Sqrt(
                    (directionX * directionX) + (directionZ * directionZ));
                if (directionMagnitude <= 0f)
                {
                    double radians = gameplay.GetActor(actorId)
                        .Pose.FacingDegrees
                        * (Math.PI / 180d);
                    directionX = (float)Math.Sin(radians);
                    directionZ = (float)Math.Cos(radians);
                    directionMagnitude = 1f;
                }
            }
            if (directionMagnitude <= 0f)
            {
                return EvaluateDestination(
                    actorId,
                    actionId,
                    subjectId,
                    origin);
            }

            directionX /= directionMagnitude;
            directionZ /= directionMagnitude;
            if (availability.Action.Intent == DisplacementActionKind.PushOff)
            {
                DisplacementDestinationEvaluation first = null;
                DisplacementDestinationEvaluation best = null;
                foreach (float offsetDegrees in PushOffCandidateOffsetsDegrees)
                {
                    double radians = offsetDegrees * (Math.PI / 180d);
                    float cosine = (float)Math.Cos(radians);
                    float sine = (float)Math.Sin(radians);
                    float candidateX = (directionX * cosine)
                        - (directionZ * sine);
                    float candidateZ = (directionX * sine)
                        + (directionZ * cosine);
                    DisplacementDestinationEvaluation candidate =
                        EvaluateIntentDestinationInDirection(
                            actorId,
                            actionId,
                            subjectId,
                            target.Subject,
                            origin,
                            candidateX,
                            candidateZ);
                    first = first ?? candidate;
                    if (candidate.IsEligible
                        && (best == null
                            || candidate.Distance > best.Distance))
                    {
                        best = candidate;
                    }
                }
                return best ?? first;
            }

            return EvaluateIntentDestinationInDirection(
                actorId,
                actionId,
                subjectId,
                target.Subject,
                origin,
                directionX,
                directionZ);
        }

        public DisplacementDestinationEvaluation
            EvaluateDirectionalPushOffDestination(
                string actorId,
                string actionId,
                string subjectId,
                GameplayPosition directionTarget)
        {
            RequireId(actorId, nameof(actorId));
            RequireId(actionId, nameof(actionId));
            RequireId(subjectId, nameof(subjectId));

            DisplacementActionAvailability availability =
                EvaluateActionAvailability(actorId, actionId);
            DisplacementTargetEvaluation target = EvaluateTarget(
                actorId,
                actionId,
                subjectId);
            if (!availability.IsAvailable || !target.IsEligible
                || !TryGetSubjectPosition(
                    target.Subject,
                    out GameplayPosition origin))
            {
                GameplayPosition unavailableDestination = target.Subject != null
                    && TryGetSubjectPosition(
                        target.Subject,
                        out GameplayPosition unavailableOrigin)
                            ? unavailableOrigin
                            : default(GameplayPosition);
                return EvaluateDestination(
                    actorId,
                    actionId,
                    subjectId,
                    unavailableDestination);
            }
            if (availability.Action.Intent != DisplacementActionKind.PushOff)
            {
                throw new InvalidOperationException(
                    "Directional intent is reserved for Push Off actions.");
            }

            float directionX = directionTarget.X - origin.X;
            float directionZ = directionTarget.Z - origin.Z;
            float magnitude = (float)Math.Sqrt(
                (directionX * directionX) + (directionZ * directionZ));
            if (magnitude <= 0f)
            {
                return EvaluateDestination(
                    actorId,
                    actionId,
                    subjectId,
                    origin);
            }

            return EvaluateIntentDestinationInDirection(
                actorId,
                actionId,
                subjectId,
                target.Subject,
                origin,
                directionX / magnitude,
                directionZ / magnitude);
        }

        private DisplacementDestinationEvaluation
            EvaluateIntentDestinationInDirection(
                string actorId,
                string actionId,
                string subjectId,
                DisplacementSubjectDefinition subject,
                GameplayPosition origin,
                float directionX,
                float directionZ)
        {
            DisplacementActionDefinition action = EvaluateActionAvailability(
                actorId,
                actionId).Action;
            float maximumDistance = action.GetMaximumDistance(subject);
            DisplacementDestinationEvaluation maximum =
                EvaluateDestinationAtDistance(
                    actorId,
                    actionId,
                    subjectId,
                    origin,
                    directionX,
                    directionZ,
                    maximumDistance);
            if (maximum.IsEligible
                || !IsSpatialDestinationFailure(maximum.Failure))
            {
                return maximum;
            }

            float acceptedDistance = 0f;
            float blockedDistance = maximumDistance;
            DisplacementDestinationEvaluation accepted = null;
            for (int iteration = 0;
                iteration < IntentDestinationSearchIterations;
                iteration++)
            {
                float candidateDistance =
                    (acceptedDistance + blockedDistance) * 0.5f;
                DisplacementDestinationEvaluation candidate =
                    EvaluateDestinationAtDistance(
                        actorId,
                        actionId,
                        subjectId,
                        origin,
                        directionX,
                        directionZ,
                        candidateDistance);
                if (candidate.IsEligible)
                {
                    accepted = candidate;
                    acceptedDistance = candidateDistance;
                }
                else if (IsSpatialDestinationFailure(candidate.Failure))
                {
                    blockedDistance = candidateDistance;
                }
                else
                {
                    return candidate;
                }
            }

            return accepted != null
                && accepted.Distance >= MinimumIntentDisplacement
                    ? accepted
                    : maximum;
        }

        private static bool IsSpatialDestinationFailure(
            DisplacementResolutionFailure failure) =>
            failure == DisplacementResolutionFailure.DestinationBlocked
            || failure == DisplacementResolutionFailure.GetUpSpaceBlocked;

        private DisplacementDestinationEvaluation
            EvaluateDestinationAtDistance(
                string actorId,
                string actionId,
                string subjectId,
                GameplayPosition origin,
                float directionX,
                float directionZ,
                float distance) =>
            EvaluateDestination(
                actorId,
                actionId,
                subjectId,
                new GameplayPosition(
                    origin.X + (directionX * distance),
                    origin.Y,
                    origin.Z + (directionZ * distance)));

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
                failure = ToResolutionFailure(availability.Failure);
                return false;
            }
            DisplacementActionDefinition definition = availability.Action;
            DisplacementTargetEvaluation target = EvaluateTarget(
                actorId,
                actionId,
                subjectId);
            if (!target.IsEligible)
            {
                failure = ToResolutionFailure(target.Failure);
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
                    failure = ToResolutionFailure(availability.Failure);
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

        private bool TryGetSubjectPosition(
            DisplacementSubjectDefinition subject,
            out GameplayPosition position) =>
            actionEvaluator.TryGetSubjectPosition(subject, out position);

        private static DisplacementDestinationEvaluation
            CreateDestinationEvaluation(
                string actorId,
                string actionId,
                string subjectId,
                GameplayPosition origin,
                GameplayPosition destination,
                DisplacementResolutionFailure failure,
                DisplacementActionDefinition action) =>
            new DisplacementDestinationEvaluation(
                actorId,
                actionId,
                subjectId,
                origin,
                destination,
                failure,
                action);

        private static DisplacementResolutionFailure ToResolutionFailure(
            DisplacementActionAvailabilityFailure failure)
        {
            switch (failure)
            {
                case DisplacementActionAvailabilityFailure.ActionUnavailable:
                    return DisplacementResolutionFailure.ActionUnavailable;
                case DisplacementActionAvailabilityFailure.OperationInProgress:
                    return DisplacementResolutionFailure.OperationInProgress;
                case DisplacementActionAvailabilityFailure.ActorNotActive:
                    return DisplacementResolutionFailure.ActorNotActive;
                case DisplacementActionAvailabilityFailure.HandsOccupied:
                    return DisplacementResolutionFailure.HandsOccupied;
                case DisplacementActionAvailabilityFailure.InsufficientTurnBudget:
                    return DisplacementResolutionFailure.InsufficientTurnBudget;
                case DisplacementActionAvailabilityFailure.ActorUnavailable:
                    return DisplacementResolutionFailure.SubjectUnavailable;
                case DisplacementActionAvailabilityFailure.ActorPinned:
                    return DisplacementResolutionFailure.ActorPinned;
                case DisplacementActionAvailabilityFailure.ActorNotPinned:
                    return DisplacementResolutionFailure.ActorNotPinned;
                case DisplacementActionAvailabilityFailure.None:
                    throw new ArgumentException(
                        "Available actions do not have resolution failures.",
                        nameof(failure));
                default:
                    throw new ArgumentOutOfRangeException(nameof(failure));
            }
        }

        private static DisplacementResolutionFailure ToResolutionFailure(
            DisplacementTargetFailure failure)
        {
            switch (failure)
            {
                case DisplacementTargetFailure.ActionUnavailable:
                    return DisplacementResolutionFailure.ActionUnavailable;
                case DisplacementTargetFailure.SubjectKindNotAccepted:
                    return DisplacementResolutionFailure.SubjectKindNotAccepted;
                case DisplacementTargetFailure.SubjectTooHeavy:
                    return DisplacementResolutionFailure.SubjectTooHeavy;
                case DisplacementTargetFailure.SubjectTooLarge:
                    return DisplacementResolutionFailure.SubjectTooLarge;
                case DisplacementTargetFailure.SubjectOutOfReach:
                    return DisplacementResolutionFailure.SubjectOutOfReach;
                case DisplacementTargetFailure.NotPinningActor:
                    return DisplacementResolutionFailure.NotPinningActor;
                case DisplacementTargetFailure.SubjectPinned:
                    return DisplacementResolutionFailure.SubjectPinned;
                case DisplacementTargetFailure.ActorUnavailable:
                case DisplacementTargetFailure.CandidateUnavailable:
                case DisplacementTargetFailure.SelfTarget:
                    return DisplacementResolutionFailure.SubjectUnavailable;
                case DisplacementTargetFailure.None:
                    throw new ArgumentException(
                        "Eligible targets do not have resolution failures.",
                        nameof(failure));
                default:
                    throw new ArgumentOutOfRangeException(nameof(failure));
            }
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
            PropDisplacementState resultingState = ResolvePropState(
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
            if (!ValidateRequest(
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

        private static PropDisplacementState ResolvePropState(
            DisplacementSubjectDefinition subject,
            DisplacementActionDefinition action,
            DestructiblePropSnapshot prop,
            GameplayPosition destination,
            out DisplacementResultPolicies appliedResults)
        {
            bool shouldTopple = prop.Posture == DestructiblePropPosture.Upright
                && subject.Toppling != null
                && action.AllowedResults.HasFlag(
                    DisplacementResultPolicies.Topple);
            if (shouldTopple)
            {
                appliedResults = DisplacementResultPolicies.Topple;
                return subject.Toppling.Resolve(prop.Pose, destination);
            }

            appliedResults = DisplacementResultPolicies.None;
            return new PropDisplacementState(
                prop.Pose.WithPosition(destination),
                prop.Posture);
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
            if (!ValidateRequest(
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

        private bool ValidateRequest(
            DisplacementRequest request,
            GameplayPosition origin,
            GameplayPosition intendedDestination,
            DisplacementActionDefinition definition,
            PropDisplacementState resultingPropState,
            out DisplacementPathValidation path,
            out DisplacementResolutionFailure failure)
        {
            path = DisplacementPathValidation.Allowed();
            if (definition == null
                || !string.Equals(
                    request.ActionId,
                    definition.Id,
                    StringComparison.Ordinal)
                || request.ActionKind != definition.Intent)
            {
                failure = DisplacementResolutionFailure.ActionUnavailable;
                return false;
            }

            if (!definition.Accepts(request.SubjectKind))
            {
                failure = DisplacementResolutionFailure.SubjectKindNotAccepted;
                return false;
            }

            if (request.SubjectMass > definition.MaximumSubjectMass)
            {
                failure = DisplacementResolutionFailure.SubjectTooHeavy;
                return false;
            }

            if (request.SubjectSize > definition.MaximumSubjectSize)
            {
                failure = DisplacementResolutionFailure.SubjectTooLarge;
                return false;
            }

            GameplayPosition actorPosition = gameplay.GetActor(
                request.ActorId).Pose.Position;
            if (actorPosition.DistanceTo(origin) > definition.Reach)
            {
                failure = DisplacementResolutionFailure.SubjectOutOfReach;
                return false;
            }

            float distance = origin.DistanceTo(intendedDestination);
            if (distance <= 0f)
            {
                failure = DisplacementResolutionFailure.DestinationUnchanged;
                return false;
            }

            float maximumDistance = definition.GetMaximumDistance(
                request.SubjectMass,
                request.SubjectSize);
            if (distance > maximumDistance)
            {
                failure = DisplacementResolutionFailure.DestinationTooFar;
                return false;
            }

            path = pathValidator.Validate(
                request,
                origin,
                resultingPropState);
            if (!path.Accepted)
            {
                failure = string.Equals(
                        path.FailureCode,
                        DisplacementPathValidation
                            .GetUpSpaceBlockedFailureCode,
                        StringComparison.Ordinal)
                    ? DisplacementResolutionFailure.GetUpSpaceBlocked
                    : DisplacementResolutionFailure.DestinationBlocked;
                return false;
            }

            failure = DisplacementResolutionFailure.None;
            return true;
        }

        private DisplacementSizeClass GetSubjectSize(string subjectId) =>
            actionEvaluator.GetSubjectSize(subjectId);
    }
}
