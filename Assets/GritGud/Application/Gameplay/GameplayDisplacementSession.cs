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
    }

    public readonly struct DisplacementPathValidation
    {
        private DisplacementPathValidation(bool accepted, string failureCode)
        {
            Accepted = accepted;
            FailureCode = failureCode ?? string.Empty;
        }

        public bool Accepted { get; }

        public string FailureCode { get; }

        public static DisplacementPathValidation Allowed() =>
            new DisplacementPathValidation(true, string.Empty);

        public static DisplacementPathValidation Blocked(string failureCode)
        {
            if (string.IsNullOrWhiteSpace(failureCode))
            {
                throw new ArgumentException(
                    "Blocked paths require a failure code.",
                    nameof(failureCode));
            }

            return new DisplacementPathValidation(false, failureCode);
        }
    }

    public interface IDisplacementPathValidator
    {
        DisplacementPathValidation Validate(
            DisplacementRequest request,
            GameplayPosition origin);
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
        private readonly GameplaySession gameplay;
        private readonly DestructiblePropSession destructibles;
        private readonly Dictionary<string, DisplacementSubjectDefinition>
            subjects;
        private readonly Dictionary<string, CloseQuartersControlProfile>
            controlProfiles;
        private readonly IDisplacementPathValidator pathValidator;
        private readonly ID20RollSource rollSource;
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
            readOnlyRecords = records.AsReadOnly();
        }

        public IReadOnlyList<DisplacementRecord> Records => readOnlyRecords;

        public GameplayJournal Journal => gameplay.Journal;

        public GameplaySessionMode Mode => gameplay.Mode;

        public CloseQuartersControlProfile GetControlProfile(string actorId)
        {
            RequireId(actorId, nameof(actorId));
            if (!controlProfiles.TryGetValue(
                    actorId,
                    out CloseQuartersControlProfile profile))
            {
                throw new KeyNotFoundException(
                    $"Actor '{actorId}' has no close-quarters control profile.");
            }

            return profile;
        }

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
            bool startsEncounter)
        {
            RequireId(actorId, nameof(actorId));
            RequireId(actionId, nameof(actionId));

            if (!gameplay.TryGetActor(actorId, out GameplayActorSnapshot actor))
            {
                return CreateActionAvailability(
                    actorId,
                    actionId,
                    DisplacementActionAvailabilityFailure.ActorUnavailable);
            }

            if (!gameplay.TryGetDisplacementAction(
                    actorId,
                    actionId,
                    out DisplacementActionDefinition action))
            {
                return CreateActionAvailability(
                    actorId,
                    actionId,
                    DisplacementActionAvailabilityFailure.ActionUnavailable);
            }

            InventoryItemDefinition equipped = gameplay.GetEquippedItem(actorId);
            ActionCost cost = ResolveActionCost(
                action,
                startsEncounter);
            string autoStowItemId = null;
            if (!HasRequiredFreeHands(action, equipped))
            {
                if (action.AutoStowPolicy == DisplacementAutoStowPolicy.Never)
                {
                    return CreateActionAvailability(
                        actorId,
                        actionId,
                        DisplacementActionAvailabilityFailure.HandsOccupied,
                        action,
                        cost);
                }

                autoStowItemId = equipped.Id;
                cost = ActionCost.Combine(
                    cost,
                    ResolveEquipmentCost(
                        equipped,
                        startsEncounter));
            }

            if (gameplay.Operation != GameplaySessionOperation.None)
            {
                return CreateActionAvailability(
                    actorId,
                    actionId,
                    DisplacementActionAvailabilityFailure.OperationInProgress,
                    action,
                    cost,
                    autoStowItemId);
            }

            if (gameplay.Mode == GameplaySessionMode.TurnBased
                && !string.Equals(
                    gameplay.ActiveActorId,
                    actorId,
                    StringComparison.Ordinal))
            {
                return CreateActionAvailability(
                    actorId,
                    actionId,
                    DisplacementActionAvailabilityFailure.ActorNotActive,
                    action,
                    cost,
                    autoStowItemId);
            }

            if (!actor.TurnBudget.CanAfford(cost))
            {
                return CreateActionAvailability(
                    actorId,
                    actionId,
                    DisplacementActionAvailabilityFailure.InsufficientTurnBudget,
                    action,
                    cost,
                    autoStowItemId);
            }

            return CreateActionAvailability(
                actorId,
                actionId,
                DisplacementActionAvailabilityFailure.None,
                action,
                cost,
                autoStowItemId);
        }

        public DisplacementTargetEvaluation EvaluateTarget(
            string actorId,
            string actionId,
            string candidateId)
        {
            RequireId(actorId, nameof(actorId));
            RequireId(actionId, nameof(actionId));
            RequireId(candidateId, nameof(candidateId));

            if (!gameplay.TryGetActor(actorId, out GameplayActorSnapshot actor))
            {
                return CreateTargetEvaluation(
                    actorId,
                    actionId,
                    candidateId,
                    DisplacementTargetFailure.ActorUnavailable);
            }

            if (!gameplay.TryGetDisplacementAction(
                    actorId,
                    actionId,
                    out DisplacementActionDefinition action))
            {
                return CreateTargetEvaluation(
                    actorId,
                    actionId,
                    candidateId,
                    DisplacementTargetFailure.ActionUnavailable);
            }

            if (!subjects.TryGetValue(
                    candidateId,
                    out DisplacementSubjectDefinition subject)
                || !TryGetSubjectPosition(
                    subject,
                    out GameplayPosition subjectPosition))
            {
                return CreateTargetEvaluation(
                    actorId,
                    actionId,
                    candidateId,
                    DisplacementTargetFailure.CandidateUnavailable,
                    action: action);
            }

            float distance = actor.Pose.Position.DistanceTo(subjectPosition);
            if (string.Equals(actorId, candidateId, StringComparison.Ordinal))
            {
                return CreateTargetEvaluation(
                    actorId,
                    actionId,
                    candidateId,
                    DisplacementTargetFailure.SelfTarget,
                    subject,
                    distance,
                    action);
            }

            if (!action.Accepts(subject.Kind))
            {
                return CreateTargetEvaluation(
                    actorId,
                    actionId,
                    candidateId,
                    DisplacementTargetFailure.SubjectKindNotAccepted,
                    subject,
                    distance,
                    action);
            }

            if (subject.Mass > action.MaximumSubjectMass)
            {
                return CreateTargetEvaluation(
                    actorId,
                    actionId,
                    candidateId,
                    DisplacementTargetFailure.SubjectTooHeavy,
                    subject,
                    distance,
                    action);
            }

            if (subject.Size > action.MaximumSubjectSize)
            {
                return CreateTargetEvaluation(
                    actorId,
                    actionId,
                    candidateId,
                    DisplacementTargetFailure.SubjectTooLarge,
                    subject,
                    distance,
                    action);
            }

            if (subject.Kind == DisplacementSubjectKind.Combatant
                && (!controlProfiles.ContainsKey(actorId)
                    || !controlProfiles.ContainsKey(candidateId)))
            {
                return CreateTargetEvaluation(
                    actorId,
                    actionId,
                    candidateId,
                    DisplacementTargetFailure.CandidateUnavailable,
                    subject,
                    distance,
                    action);
            }

            if (distance > action.Reach)
            {
                return CreateTargetEvaluation(
                    actorId,
                    actionId,
                    candidateId,
                    DisplacementTargetFailure.SubjectOutOfReach,
                    subject,
                    distance,
                    action);
            }

            return CreateTargetEvaluation(
                actorId,
                actionId,
                candidateId,
                DisplacementTargetFailure.None,
                subject,
                distance,
                action);
        }

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
            ValidateRequest(
                request,
                origin,
                availability.Action,
                out DisplacementResolutionFailure failure);
            return CreateDestinationEvaluation(
                actorId,
                actionId,
                subjectId,
                origin,
                destination,
                failure,
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
            DisplacementDestinationEvaluation maximum =
                EvaluateDestinationAtDistance(
                    actorId,
                    actionId,
                    subjectId,
                    origin,
                    directionX,
                    directionZ,
                    availability.Action.MaximumDistance);
            if (maximum.IsEligible
                || maximum.Failure !=
                    DisplacementResolutionFailure.DestinationBlocked)
            {
                return maximum;
            }

            float acceptedDistance = 0f;
            float blockedDistance = availability.Action.MaximumDistance;
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
                else if (candidate.Failure ==
                    DisplacementResolutionFailure.DestinationBlocked)
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
                    target.Subject.Mass,
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
            gameplay.CommitAction(action);
            Commit(record);
            failure = DisplacementResolutionFailure.None;
            return true;
        }

        private bool TryGetSubjectPosition(
            DisplacementSubjectDefinition subject,
            out GameplayPosition position)
        {
            if (subject.Kind == DisplacementSubjectKind.Prop)
            {
                if (destructibles.TryGetProp(
                        subject.Id,
                        out DestructiblePropSnapshot prop))
                {
                    position = prop.Position;
                    return true;
                }
            }
            else if (gameplay.TryGetActor(
                subject.Id,
                out GameplayActorSnapshot actor))
            {
                position = actor.Pose.Position;
                return true;
            }

            position = default(GameplayPosition);
            return false;
        }

        private static DisplacementTargetEvaluation CreateTargetEvaluation(
            string actorId,
            string actionId,
            string candidateId,
            DisplacementTargetFailure failure,
            DisplacementSubjectDefinition subject = null,
            float distance = 0f,
            DisplacementActionDefinition action = null) =>
            new DisplacementTargetEvaluation(
                actorId,
                actionId,
                candidateId,
                failure,
                subject,
                distance,
                action);

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

        private static DisplacementActionAvailability CreateActionAvailability(
            string actorId,
            string actionId,
            DisplacementActionAvailabilityFailure failure,
            DisplacementActionDefinition action = null,
            ActionCost resolvedCost = default(ActionCost),
            string autoStowItemId = null) =>
            new DisplacementActionAvailability(
                actorId,
                actionId,
                failure,
                action,
                resolvedCost,
                autoStowItemId);

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

        private ActionCost ResolveActionCost(
            DisplacementActionDefinition definition,
            bool startsEncounter) =>
            gameplay.Mode == GameplaySessionMode.TurnBased
                || startsEncounter
                ? definition.Cost
                : new ActionCost(
                    0,
                    0f,
                    definition.Cost.Mobility);

        private ActionCost ResolveEquipmentCost(
            InventoryItemDefinition item,
            bool startsEncounter) =>
            gameplay.Mode == GameplaySessionMode.TurnBased
                || startsEncounter
                ? item.EquipmentCost
                : new ActionCost(
                    0,
                    0f,
                    item.EquipmentCost.Mobility);

        private static bool HasRequiredFreeHands(
            DisplacementActionDefinition action,
            InventoryItemDefinition equipped) =>
            action.HasRequiredFreeHands(equipped?.OccupiedHands ?? 0);

        private bool TryResolveProp(
            string actorId,
            string propId,
            float propMass,
            GameplayPosition destination,
            DisplacementActionDefinition definition,
            out DisplacementRecord record,
            out DisplacementResolutionFailure failure)
        {
            gameplay.GetActor(actorId);
            DestructiblePropSnapshot prop = destructibles.GetProp(propId);
            var request = new DisplacementRequest(
                actorId,
                definition.Id,
                propId,
                DisplacementSubjectKind.Prop,
                propMass,
                GetSubjectSize(propId),
                destination,
                definition.Intent);
            if (!ValidateRequest(
                    request,
                    prop.Position,
                    definition,
                    out failure))
            {
                record = null;
                return false;
            }

            record = new DisplacementRecord(
                records.Count + 1L,
                request,
                new PropDisplacementState(prop.Pose, prop.Posture),
                new PropDisplacementState(
                    prop.Pose.WithPosition(destination),
                    prop.Posture));
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
            if (!ValidateRequest(
                    request,
                    target.Pose.Position,
                    definition,
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
            ValidateCommit(record);

            if (record.Succeeded)
            {
                if (record.Request.SubjectKind == DisplacementSubjectKind.Prop)
                {
                    destructibles.CommitDisplacement(record);
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
            DisplacementActionDefinition definition,
            out DisplacementResolutionFailure failure)
        {
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

            float distance = origin.DistanceTo(request.Destination);
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

            if (!pathValidator.Validate(request, origin).Accepted)
            {
                failure = DisplacementResolutionFailure.DestinationBlocked;
                return false;
            }

            failure = DisplacementResolutionFailure.None;
            return true;
        }

        private DisplacementSizeClass GetSubjectSize(string subjectId) =>
            subjects.TryGetValue(
                subjectId,
                out DisplacementSubjectDefinition subject)
                    ? subject.Size
                    : DisplacementSizeClass.Medium;
    }
}
