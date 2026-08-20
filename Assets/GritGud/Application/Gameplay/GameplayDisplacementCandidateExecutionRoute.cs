using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;

namespace GritGud.Application.Gameplay
{
    public sealed class GameplayDisplacementIntent
    {
        public GameplayDisplacementIntent(
            GameplayReachableInput input,
            string stateHash,
            GameplayPosition origin,
            GameplayPosition destination)
        {
            Input = input ?? throw new ArgumentNullException(nameof(input));
            StateHash = GameplayContentIdentity.RequireDigest(
                stateHash,
                nameof(stateHash));
            if (origin.DistanceTo(destination) <= 0f)
                throw new ArgumentException(
                    "Displacement intent requires a changed destination.",
                    nameof(destination));
            Origin = origin;
            Destination = destination;
        }

        public GameplayReachableInput Input { get; }
        public string StateHash { get; }
        public GameplayPosition Origin { get; }
        public GameplayPosition Destination { get; }
    }

    /// <summary>
    /// Portable displacement collision evidence. Both simulation and the live
    /// adapter consume this contract; scene physics may visualize the result,
    /// but cannot author a second outcome.
    /// </summary>
    public sealed class GameplayHeadlessDisplacementPathValidator :
        IDisplacementPathValidator
    {
        private const float ActorRadius = 0.35f;
        private const float StandingHeight = 1.8f;

        private readonly GameplayCombatStateSnapshot state;
        private readonly GameplayScenarioAssembly assembly;
        private readonly GameplayHeadlessSpatialEvidence spatial;

        public GameplayHeadlessDisplacementPathValidator(
            GameplayCombatStateSnapshot canonicalState,
            GameplayScenarioAssembly scenarioAssembly,
            GameplayHeadlessSpatialEvidence spatialEvidence)
        {
            state = canonicalState ?? throw new ArgumentNullException(
                nameof(canonicalState));
            assembly = scenarioAssembly ?? throw new ArgumentNullException(
                nameof(scenarioAssembly));
            spatial = spatialEvidence ?? throw new ArgumentNullException(
                nameof(spatialEvidence));
        }

        public DisplacementPathValidation Validate(
            DisplacementRequest request,
            GameplayPosition origin,
            PropDisplacementState resultingPropState)
        {
            if (request.Destination.DistanceTo(origin) <= 0.0001f)
                return DisplacementPathValidation.Blocked(
                    "displacement.destination-unchanged");
            float radius = Radius(request.SubjectSize);
            bool blocked = request.SubjectKind == DisplacementSubjectKind.Prop
                ? spatial.BlocksPathIgnoringEntity(
                    state,
                    AddHeight(origin, radius),
                    AddHeight(request.Destination, radius),
                    radius,
                    request.SubjectId)
                : spatial.BlocksPath(
                    state,
                    AddHeight(origin, radius),
                    AddHeight(request.Destination, radius),
                    radius);
            if (blocked)
                return DisplacementPathValidation.Blocked(
                    "displacement.path-blocked");

            var contacts = new List<DisplacementContactEvidence>();
            foreach (GameplayActorSnapshot actor in state.Session.Actors)
            {
                if (actor.IsIncapacitated
                    || string.Equals(
                        actor.ActorId,
                        request.ActorId,
                        StringComparison.Ordinal)
                    || string.Equals(
                        actor.ActorId,
                        request.SubjectId,
                        StringComparison.Ordinal))
                    continue;
                float clearance = radius + ActorRadius;
                float pathDistance = DistanceToSegment(
                    actor.Pose.Position,
                    origin,
                    request.Destination);
                float destinationDistance = actor.Pose.Position.DistanceTo(
                    request.Destination);
                if (pathDistance >= clearance) continue;
                if (request.SubjectKind != DisplacementSubjectKind.Prop
                    || resultingPropState == null
                    || destinationDistance >= clearance)
                    return DisplacementPathValidation.Blocked(
                        "displacement.actor-blocked");

                GameplayPosition normal = Normalize(
                    request.Destination,
                    actor.Pose.Position);
                contacts.Add(new DisplacementContactEvidence(
                    actor.ActorId,
                    actor.Pose.Position,
                    normal,
                    clearance - destinationDistance));
            }

            if (request.ActionKind == DisplacementActionKind.PushOff)
            {
                GameplayActorSnapshot acting = state.Session.GetActor(
                    request.ActorId);
                GameplayPosition lower = AddHeight(
                    acting.Pose.Position,
                    ActorRadius);
                GameplayPosition upper = AddHeight(
                    acting.Pose.Position,
                    StandingHeight - ActorRadius);
                if (spatial.BlocksPathIgnoringEntity(
                        state,
                        lower,
                        upper,
                        ActorRadius,
                        request.SubjectId))
                    return DisplacementPathValidation.Blocked(
                        DisplacementPathValidation
                            .GetUpSpaceBlockedFailureCode);
                foreach (GameplayActorSnapshot other in state.Session.Actors)
                    if (!string.Equals(
                            other.ActorId,
                            acting.ActorId,
                            StringComparison.Ordinal)
                        && !other.IsIncapacitated
                        && other.Pose.Position.DistanceTo(acting.Pose.Position)
                            < ActorRadius * 2f)
                        return DisplacementPathValidation.Blocked(
                            DisplacementPathValidation
                                .GetUpSpaceBlockedFailureCode);
            }

            return contacts.Count == 0
                ? DisplacementPathValidation.Allowed()
                : DisplacementPathValidation.Allowed(contacts);
        }

        private static float Radius(DisplacementSizeClass size)
        {
            switch (size)
            {
                case DisplacementSizeClass.Tiny: return 0.15f;
                case DisplacementSizeClass.Small: return 0.25f;
                case DisplacementSizeClass.Medium: return 0.35f;
                case DisplacementSizeClass.Large: return 0.5f;
                case DisplacementSizeClass.Huge: return 0.75f;
                default: throw new ArgumentOutOfRangeException(nameof(size));
            }
        }

        private static GameplayPosition AddHeight(
            GameplayPosition value,
            float height) => new GameplayPosition(
                value.X,
                value.Y + height,
                value.Z);

        private static GameplayPosition Normalize(
            GameplayPosition from,
            GameplayPosition to)
        {
            float x = to.X - from.X;
            float y = to.Y - from.Y;
            float z = to.Z - from.Z;
            float length = (float)Math.Sqrt((x * x) + (y * y) + (z * z));
            return length <= 0.0001f
                ? new GameplayPosition(0f, 1f, 0f)
                : new GameplayPosition(x / length, y / length, z / length);
        }

        private static float DistanceToSegment(
            GameplayPosition point,
            GameplayPosition from,
            GameplayPosition to)
        {
            double x = to.X - from.X;
            double y = to.Y - from.Y;
            double z = to.Z - from.Z;
            double lengthSquared = (x * x) + (y * y) + (z * z);
            if (lengthSquared <= 0.00000001d) return point.DistanceTo(from);
            double projection = (
                ((point.X - from.X) * x)
                + ((point.Y - from.Y) * y)
                + ((point.Z - from.Z) * z)) / lengthSquared;
            projection = Math.Max(0d, Math.Min(1d, projection));
            return point.DistanceTo(new GameplayPosition(
                (float)(from.X + (x * projection)),
                (float)(from.Y + (y * projection)),
                (float)(from.Z + (z * projection))));
        }
    }

    public static class GameplayDisplacementPreparation
    {
        public static bool TryPrepare(
            GameplayCombatStateSnapshot state,
            GameplayScenarioAssembly assembly,
            string actorId,
            string subjectId,
            DisplacementActionDefinition definition,
            GameplayPosition destination,
            IDisplacementPathValidator pathValidator,
            ID20RollSource rollSource,
            out GameplayActionRecord action,
            out DisplacementResolutionFailure failure)
        {
            action = null;
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (assembly == null)
                throw new ArgumentNullException(nameof(assembly));
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));
            if (pathValidator == null)
                throw new ArgumentNullException(nameof(pathValidator));
            if (rollSource == null)
                throw new ArgumentNullException(nameof(rollSource));
            if (!string.Equals(
                    state.Session.ScenarioId,
                    assembly.Scenario.Id,
                    StringComparison.Ordinal))
                throw new ArgumentException(
                    "Displacement rules and state describe different scenarios.",
                    nameof(assembly));
            if (state.Session.Mode != GameplaySessionMode.TurnBased)
                return Fail(
                    DisplacementResolutionFailure.TurnModeRequired,
                    out failure);
            if (state.Session.Operation != GameplaySessionOperation.None)
                return Fail(
                    DisplacementResolutionFailure.OperationInProgress,
                    out failure);

            GameplayActorSnapshot actor;
            try
            {
                actor = state.Session.GetActor(actorId);
            }
            catch (KeyNotFoundException)
            {
                return Fail(
                    DisplacementResolutionFailure.ActorNotActive,
                    out failure);
            }
            if (!string.Equals(
                    state.Session.ActiveActorId,
                    actorId,
                    StringComparison.Ordinal)
                || actor.IsIncapacitated)
                return Fail(
                    DisplacementResolutionFailure.ActorNotActive,
                    out failure);
            if (actor.IsPinned
                && definition.Intent != DisplacementActionKind.PushOff)
                return Fail(
                    DisplacementResolutionFailure.ActorPinned,
                    out failure);
            if (!actor.IsPinned
                && definition.Intent == DisplacementActionKind.PushOff)
                return Fail(
                    DisplacementResolutionFailure.ActorNotPinned,
                    out failure);

            ScenarioActorDefinition actorDefinition = assembly.GetActor(
                actorId).GameplayDefinition;
            if (!ReferenceEquals(
                    actorDefinition.GetDisplacementAction(definition.Id),
                    definition))
                return Fail(
                    DisplacementResolutionFailure.ActionUnavailable,
                    out failure);
            InventoryItemDefinition equipped = actor.EquippedItemId == null
                ? null
                : actorDefinition.GetInventoryItem(actor.EquippedItemId);
            ActionCost cost = definition.Cost;
            string autoStowItemId = null;
            if (!definition.HasRequiredFreeHands(
                    equipped?.OccupiedHands ?? 0))
            {
                if (definition.AutoStowPolicy
                        != DisplacementAutoStowPolicy.Allowed
                    || equipped == null)
                    return Fail(
                        DisplacementResolutionFailure.HandsOccupied,
                        out failure);
                autoStowItemId = equipped.Id;
                cost = ActionCost.Combine(cost, equipped.EquipmentCost);
            }
            if (!actor.TurnBudget.CanAfford(cost))
                return Fail(
                    DisplacementResolutionFailure.InsufficientTurnBudget,
                    out failure);

            if (!assembly.TryGetDisplacementSubject(
                    subjectId,
                    out DisplacementSubjectDefinition subject))
                return Fail(
                    DisplacementResolutionFailure.SubjectUnavailable,
                    out failure);
            if (string.Equals(actorId, subjectId, StringComparison.Ordinal))
                return Fail(
                    DisplacementResolutionFailure.SubjectUnavailable,
                    out failure);
            if (!definition.Accepts(subject.Kind))
                return Fail(
                    DisplacementResolutionFailure.SubjectKindNotAccepted,
                    out failure);
            if (subject.Mass > definition.MaximumSubjectMass)
                return Fail(
                    DisplacementResolutionFailure.SubjectTooHeavy,
                    out failure);
            if (subject.Size > definition.MaximumSubjectSize)
                return Fail(
                    DisplacementResolutionFailure.SubjectTooLarge,
                    out failure);

            GameplayPosition origin;
            DestructiblePropSnapshot prop = default;
            if (subject.Kind == DisplacementSubjectKind.Prop)
            {
                state.RequireCoverage(
                    GameplayCombatStateCoverage.Destructibles);
                if (!TryFindProp(state.Destructibles, subjectId, out prop)
                    || prop.State == DestructiblePropState.Destroyed)
                    return Fail(
                        DisplacementResolutionFailure.SubjectUnavailable,
                        out failure);
                origin = prop.Pose.Position;
                if (definition.Intent == DisplacementActionKind.PushOff
                    && (!actor.IsPinned
                        || !string.Equals(
                            actor.PinState.PropId,
                            subjectId,
                            StringComparison.Ordinal)))
                    return Fail(
                        DisplacementResolutionFailure.NotPinningActor,
                        out failure);
                if (definition.Intent != DisplacementActionKind.PushOff
                    && definition.Intent != DisplacementActionKind.Push
                    && TryFindPinnedBy(
                        state.Session.Actors,
                        subjectId,
                        out _))
                    return Fail(
                        DisplacementResolutionFailure.SubjectPinned,
                        out failure);
            }
            else
            {
                if (!TryFindActor(
                        state.Session.Actors,
                        subjectId,
                        out GameplayActorSnapshot target)
                    || target.IsIncapacitated)
                    return Fail(
                        DisplacementResolutionFailure.SubjectUnavailable,
                        out failure);
                if (target.IsPinned)
                    return Fail(
                        DisplacementResolutionFailure.SubjectPinned,
                        out failure);
                origin = target.Pose.Position;
            }
            if (actor.Pose.Position.DistanceTo(origin) > definition.Reach)
                return Fail(
                    DisplacementResolutionFailure.SubjectOutOfReach,
                    out failure);
            float distance = origin.DistanceTo(destination);
            if (distance <= 0f)
                return Fail(
                    DisplacementResolutionFailure.DestinationUnchanged,
                    out failure);
            if (distance > definition.GetMaximumDistance(subject) + 0.0001f)
                return Fail(
                    DisplacementResolutionFailure.DestinationTooFar,
                    out failure);

            long sequence = checked(state.Session.LastActionSequence + 1L);
            DisplacementRecord record;
            if (subject.Kind == DisplacementSubjectKind.Prop)
            {
                PropDisplacementState resulting =
                    DisplacementDestinationEvaluator.ResolvePropState(
                        subject,
                        definition,
                        prop,
                        destination,
                        out DisplacementResultPolicies applied);
                var request = new DisplacementRequest(
                    actorId,
                    definition.Id,
                    subjectId,
                    subject.Kind,
                    subject.Mass,
                    subject.Size,
                    resulting.Pose.Position,
                    definition.Intent);
                DisplacementPathValidation path = pathValidator.Validate(
                    request,
                    origin,
                    resulting);
                if (!path.Accepted)
                    return Fail(
                        string.Equals(
                            path.FailureCode,
                            DisplacementPathValidation
                                .GetUpSpaceBlockedFailureCode,
                            StringComparison.Ordinal)
                                ? DisplacementResolutionFailure
                                    .GetUpSpaceBlocked
                                : DisplacementResolutionFailure
                                    .DestinationBlocked,
                        out failure);
                if (!TryResolvePin(
                        state,
                        assembly,
                        actor,
                        subject,
                        definition,
                        prop,
                        path,
                        sequence,
                        ref applied,
                        out ActorPinTransition pin,
                        out failure))
                    return false;
                record = new DisplacementRecord(
                    sequence,
                    request,
                    new PropDisplacementState(prop.Pose, prop.Posture),
                    resulting,
                    applied,
                    pin);
            }
            else
            {
                var request = new DisplacementRequest(
                    actorId,
                    definition.Id,
                    subjectId,
                    subject.Kind,
                    subject.Mass,
                    subject.Size,
                    destination,
                    definition.Intent);
                DisplacementPathValidation path = pathValidator.Validate(
                    request,
                    origin,
                    resultingPropState: null);
                if (!path.Accepted)
                    return Fail(
                        DisplacementResolutionFailure.DestinationBlocked,
                        out failure);
                CloseQuartersControlProfile attacker = assembly.GetActor(
                    actorId).ControlProfile;
                CloseQuartersControlProfile defender = assembly.GetActor(
                    subjectId).ControlProfile;
                var transition = new GameplayTransitionIdentity(
                    sequence,
                    GameplaySemanticCapability.Displace.ToString(),
                    actorId,
                    subjectId);
                var contest = new CloseQuartersControlRecord(
                    rollSource.RollD20(transition, "attacker-roll"),
                    attacker,
                    rollSource.RollD20(transition, "defender-roll"),
                    defender);
                record = new DisplacementRecord(
                    sequence,
                    request,
                    origin,
                    contest.AttackerSucceeded ? destination : origin,
                    contest);
            }

            TurnBudget resultingBudget = actor.TurnBudget.SpendAction(cost);
            var outcomes = new List<GameplayActionOutcome>();
            if (autoStowItemId != null)
                outcomes.Add(new EquipmentChangedActionOutcome(
                    new EquipmentChangeRecord(
                        actorId,
                        autoStowItemId,
                        EquipmentChangeKind.Unequip,
                        autoStowItemId,
                        resultingEquippedItemId: null)));
            outcomes.Add(new DisplacementActionOutcome(record));
            action = new GameplayActionRecord(
                sequence,
                new GameplayActionRequest(actorId, definition.Id, subjectId),
                cost,
                actor.TurnBudget,
                resultingBudget,
                outcomes);
            DisplacementActionCommitValidator.Validate(
                action,
                record,
                definition,
                equipped,
                chargesTurnCost: true);
            failure = DisplacementResolutionFailure.None;
            return true;
        }

        private static bool TryResolvePin(
            GameplayCombatStateSnapshot state,
            GameplayScenarioAssembly assembly,
            GameplayActorSnapshot acting,
            DisplacementSubjectDefinition propSubject,
            DisplacementActionDefinition definition,
            DestructiblePropSnapshot prop,
            DisplacementPathValidation path,
            long sequence,
            ref DisplacementResultPolicies applied,
            out ActorPinTransition transition,
            out DisplacementResolutionFailure failure)
        {
            transition = null;
            if (definition.Intent == DisplacementActionKind.PushOff)
            {
                if (acting.PinState == null)
                    return Fail(
                        DisplacementResolutionFailure.ActorNotPinned,
                        out failure);
                if (!string.Equals(
                        acting.PinState.PropId,
                        prop.PropId,
                        StringComparison.Ordinal))
                    return Fail(
                        DisplacementResolutionFailure.NotPinningActor,
                        out failure);
                if (!definition.AllowedResults.HasFlag(
                        DisplacementResultPolicies.Release))
                    return Fail(
                        DisplacementResolutionFailure.ActionUnavailable,
                        out failure);
                if (path.Contacts.Count > 0)
                    return Fail(
                        DisplacementResolutionFailure.DestinationBlocked,
                        out failure);
                applied |= DisplacementResultPolicies.Release;
                transition = new ActorPinTransition(
                    acting.ActorId,
                    acting.Pose,
                    FaceToward(acting.Pose, prop.Position),
                    acting.PinState,
                    resultingState: null);
                failure = DisplacementResolutionFailure.None;
                return true;
            }

            if (definition.Intent == DisplacementActionKind.Push
                && TryFindPinnedBy(
                    state.Session.Actors,
                    prop.PropId,
                    out GameplayActorSnapshot pinned))
            {
                if (path.Contacts.Count > 0)
                    return Fail(
                        DisplacementResolutionFailure.DestinationBlocked,
                        out failure);
                applied |= DisplacementResultPolicies.Release;
                transition = new ActorPinTransition(
                    pinned.ActorId,
                    pinned.Pose,
                    pinned.Pose,
                    pinned.PinState,
                    resultingState: null);
                failure = DisplacementResolutionFailure.None;
                return true;
            }
            if (path.Contacts.Count == 0)
            {
                failure = DisplacementResolutionFailure.None;
                return true;
            }
            if (!applied.HasFlag(DisplacementResultPolicies.Topple)
                || propSubject.Pinning == null
                || !definition.AllowedResults.HasFlag(
                    DisplacementResultPolicies.Pin)
                || path.Contacts.Count != 1)
                return Fail(
                    DisplacementResolutionFailure.DestinationBlocked,
                    out failure);

            DisplacementContactEvidence contact = path.Contacts[0];
            if (!assembly.TryGetDisplacementSubject(
                    contact.EntityId,
                    out DisplacementSubjectDefinition contacted)
                || contacted.Kind != DisplacementSubjectKind.Combatant
                || !TryFindActor(
                    state.Session.Actors,
                    contact.EntityId,
                    out GameplayActorSnapshot contactedActor)
                || contactedActor.IsIncapacitated
                || contactedActor.IsPinned
                || !propSubject.Pinning.Accepts(
                    contacted.Mass,
                    contact.OverlapDepth))
                return Fail(
                    DisplacementResolutionFailure.DestinationBlocked,
                    out failure);

            var pin = new ActorPinState(
                contactedActor.ActorId,
                prop.PropId,
                sequence,
                contact);
            transition = new ActorPinTransition(
                contactedActor.ActorId,
                contactedActor.Pose,
                contactedActor.Pose,
                previousState: null,
                pin);
            applied |= DisplacementResultPolicies.Pin;
            failure = DisplacementResolutionFailure.None;
            return true;
        }

        private static GameplayActorPose FaceToward(
            GameplayActorPose pose,
            GameplayPosition target)
        {
            double x = target.X - pose.Position.X;
            double z = target.Z - pose.Position.Z;
            if (Math.Abs(x) <= 0.0001d && Math.Abs(z) <= 0.0001d)
                return pose;
            return new GameplayActorPose(
                pose.Position,
                (float)(Math.Atan2(x, z) * 180d / Math.PI),
                pose.Stance);
        }

        private static bool TryFindActor(
            IEnumerable<GameplayActorSnapshot> actors,
            string actorId,
            out GameplayActorSnapshot actor)
        {
            foreach (GameplayActorSnapshot candidate in actors)
                if (string.Equals(
                    candidate.ActorId,
                    actorId,
                    StringComparison.Ordinal))
                {
                    actor = candidate;
                    return true;
                }
            actor = default;
            return false;
        }

        private static bool TryFindPinnedBy(
            IEnumerable<GameplayActorSnapshot> actors,
            string propId,
            out GameplayActorSnapshot actor)
        {
            foreach (GameplayActorSnapshot candidate in actors)
                if (candidate.PinState != null
                    && string.Equals(
                        candidate.PinState.PropId,
                        propId,
                        StringComparison.Ordinal))
                {
                    actor = candidate;
                    return true;
                }
            actor = default;
            return false;
        }

        private static bool TryFindProp(
            IEnumerable<DestructiblePropSnapshot> props,
            string propId,
            out DestructiblePropSnapshot prop)
        {
            foreach (DestructiblePropSnapshot candidate in props)
                if (string.Equals(
                    candidate.PropId,
                    propId,
                    StringComparison.Ordinal))
                {
                    prop = candidate;
                    return true;
                }
            prop = default;
            return false;
        }

        private static bool Fail(
            DisplacementResolutionFailure value,
            out DisplacementResolutionFailure failure)
        {
            failure = value;
            return false;
        }
    }

    public sealed class GameplayDisplacementCandidateExecutionRoute :
        IGameplayCandidateExecutionRoute
    {
        public const string Id = "displacement.v1";

        private readonly GameplayScenarioAssembly assembly;
        private readonly GameplayHeadlessSpatialEvidence spatial;

        public GameplayDisplacementCandidateExecutionRoute(
            GameplayScenarioAssembly scenarioAssembly,
            GameplayHeadlessSpatialEvidence spatialEvidence)
        {
            assembly = scenarioAssembly ?? throw new ArgumentNullException(
                nameof(scenarioAssembly));
            spatial = spatialEvidence ?? throw new ArgumentNullException(
                nameof(spatialEvidence));
        }

        public string RouteId => Id;

        public bool Supports(GameplayCapabilityProfile profile) =>
            new GameplayDisplacementTransitionReducer().Supports(profile);

        public GameplayExecutableCandidateEvaluation Evaluate(
            GameplayDecisionContext context,
            GameplayCandidate candidate)
        {
            GameplayBasicCandidateRouteUtility.Require(
                context,
                candidate,
                Supports,
                Id);
            if (candidate.Intent is not GameplayDisplacementIntent intent
                || !string.Equals(
                    intent.StateHash,
                    context.State.CanonicalHash,
                    StringComparison.Ordinal))
                return Illegal(
                    context,
                    candidate,
                    "displacement-intent-required");
            DisplacementActionDefinition definition = FindDefinition(
                candidate.ActorId,
                candidate.Profile);
            if (definition == null)
                return Illegal(
                    context,
                    candidate,
                    "displacement-profile-not-owned");
            var validator = new GameplayHeadlessDisplacementPathValidator(
                context.State,
                assembly,
                spatial);
            var rolls = new AddressedD20RollSource(
                context.State.Session.RunIdentity);
            if (!GameplayDisplacementPreparation.TryPrepare(
                    context.State,
                    assembly,
                    candidate.ActorId,
                    candidate.SubjectId,
                    definition,
                    intent.Destination,
                    validator,
                    rolls,
                    out GameplayActionRecord action,
                    out DisplacementResolutionFailure failure))
                return Illegal(
                    context,
                    candidate,
                    "displacement." + failure);
            DisplacementRecord record = FindRecord(action);
            return new GameplayExecutableCandidateEvaluation(
                Id,
                candidate,
                context.State.CanonicalHash,
                isLegal: true,
                failureCode: string.Empty,
                new GameplayCandidateOutcomeEstimate(new[]
                {
                    new GameplayCandidateOutcomeFeature(
                        "displacement.succeeded",
                        record.Succeeded ? 1f : 0f),
                    new GameplayCandidateOutcomeFeature(
                        "displacement.distance",
                        record.PreviousPosition.DistanceTo(
                            record.ResultingPosition)),
                    new GameplayCandidateOutcomeFeature(
                        "displacement.toppled",
                        record.AppliedResults.HasFlag(
                            DisplacementResultPolicies.Topple) ? 1f : 0f),
                    new GameplayCandidateOutcomeFeature(
                        "displacement.pinned",
                        record.AppliedResults.HasFlag(
                            DisplacementResultPolicies.Pin) ? 1f : 0f),
                    new GameplayCandidateOutcomeFeature(
                        "displacement.released",
                        record.AppliedResults.HasFlag(
                            DisplacementResultPolicies.Release) ? 1f : 0f),
                    new GameplayCandidateOutcomeFeature(
                        "cost.action-points",
                        action.Cost.ActionPoints),
                }),
                new[]
                {
                    spatial.CaptureEvidence(
                        "displacement-path",
                        context.State,
                        intent.Origin,
                        intent.Destination,
                        clearanceRadius: 0.35f),
                },
                action);
        }

        public GameplayTransitionPayload PreparePayload(
            GameplayDecisionContext context,
            GameplayExecutableCandidateEvaluation evaluation) =>
            new GameplayResolvedActionTransitionPayload(
                evaluation.Candidate.Profile,
                evaluation?.FrozenPreparation as GameplayActionRecord
                    ?? throw new ArgumentException(
                        "Displacement preparation is missing.",
                        nameof(evaluation)));

        private DisplacementActionDefinition FindDefinition(
            string actorId,
            GameplayCapabilityProfile profile)
        {
            GameplaySemanticSubjectKind subject =
                GameplayCapabilityProfiles.GetSubjectKind(profile);
            foreach (DisplacementActionDefinition action in assembly.GetActor(
                actorId).GameplayDefinition.DisplacementActions)
                if (profile.Equals(
                    GameplayCapabilityProfiles.Displace(action, subject)))
                    return action;
            return null;
        }

        private static DisplacementRecord FindRecord(
            GameplayActionRecord action)
        {
            foreach (GameplayActionOutcome outcome in action.Outcomes)
                if (outcome is DisplacementActionOutcome displaced)
                    return displaced.Displacement;
            throw new InvalidOperationException(
                "Prepared action has no displacement outcome.");
        }

        private static GameplayExecutableCandidateEvaluation Illegal(
            GameplayDecisionContext context,
            GameplayCandidate candidate,
            string failure) => GameplayBasicCandidateRouteUtility.Result(
                Id,
                context,
                candidate,
                legal: false,
                failure,
                outcome: null,
                preparation: null);
    }
}
