using System;
using GritGud.Domain.Gameplay;

namespace GritGud.Application.Gameplay
{
    internal sealed class DisplacementDestinationEvaluator
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
        private readonly DisplacementActionEvaluator actionEvaluator;
        private readonly DisplacementPinTransitionResolver pinTransitionResolver;
        private readonly IDisplacementPathValidator pathValidator;

        public DisplacementDestinationEvaluator(
            GameplaySession gameplaySession,
            DestructiblePropSession destructibleSession,
            DisplacementActionEvaluator actions,
            DisplacementPinTransitionResolver pinTransitions,
            IDisplacementPathValidator validator)
        {
            gameplay = gameplaySession ??
                throw new ArgumentNullException(nameof(gameplaySession));
            destructibles = destructibleSession ??
                throw new ArgumentNullException(nameof(destructibleSession));
            actionEvaluator = actions ??
                throw new ArgumentNullException(nameof(actions));
            pinTransitionResolver = pinTransitions ??
                throw new ArgumentNullException(nameof(pinTransitions));
            pathValidator = validator ??
                throw new ArgumentNullException(nameof(validator));
        }

        public DisplacementDestinationEvaluation Evaluate(
            string actorId,
            string actionId,
            string subjectId,
            GameplayPosition destination,
            long displacementSequence)
        {
            RequireId(actorId, nameof(actorId));
            RequireId(actionId, nameof(actionId));
            RequireId(subjectId, nameof(subjectId));

            GameplayPosition origin = default(GameplayPosition);
            actionEvaluator.TryGetSubjectPosition(subjectId, out origin);

            DisplacementActionAvailability availability =
                actionEvaluator.EvaluateAvailability(
                    actorId,
                    actionId,
                    startsEncounter: false);
            if (!availability.IsAvailable)
            {
                return CreateEvaluation(
                    actorId,
                    actionId,
                    subjectId,
                    origin,
                    destination,
                    DisplacementActionEvaluator.ToResolutionFailure(
                        availability.Failure),
                    availability.Action);
            }

            DisplacementTargetEvaluation target = actionEvaluator.EvaluateTarget(
                actorId,
                actionId,
                subjectId);
            if (!target.IsEligible
                || !actionEvaluator.TryGetSubjectPosition(
                    target.Subject,
                    out origin))
            {
                return CreateEvaluation(
                    actorId,
                    actionId,
                    subjectId,
                    origin,
                    destination,
                    target.IsEligible
                        ? DisplacementResolutionFailure.SubjectUnavailable
                        : DisplacementActionEvaluator.ToResolutionFailure(
                            target.Failure),
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

            bool valid = TryValidateRequest(
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
                    displacementSequence,
                    ref appliedResults,
                    out _,
                    out failure))
            {
                valid = false;
            }

            return CreateEvaluation(
                actorId,
                actionId,
                subjectId,
                origin,
                destination,
                valid ? DisplacementResolutionFailure.None : failure,
                availability.Action);
        }

        public DisplacementDestinationEvaluation EvaluateIntent(
            string actorId,
            string actionId,
            string subjectId,
            long displacementSequence)
        {
            RequireId(actorId, nameof(actorId));
            RequireId(actionId, nameof(actionId));
            RequireId(subjectId, nameof(subjectId));

            DisplacementActionAvailability availability =
                actionEvaluator.EvaluateAvailability(
                    actorId,
                    actionId,
                    startsEncounter: false);
            DisplacementTargetEvaluation target = actionEvaluator.EvaluateTarget(
                actorId,
                actionId,
                subjectId);
            if (!availability.IsAvailable || !target.IsEligible)
            {
                GameplayPosition unavailableDestination = target.Subject != null
                    && actionEvaluator.TryGetSubjectPosition(
                        target.Subject,
                        out GameplayPosition unavailableOrigin)
                            ? unavailableOrigin
                            : default(GameplayPosition);
                return Evaluate(
                    actorId,
                    actionId,
                    subjectId,
                    unavailableDestination,
                    displacementSequence);
            }

            if (!actionEvaluator.TryGetSubjectPosition(
                    target.Subject,
                    out GameplayPosition origin))
            {
                return Evaluate(
                    actorId,
                    actionId,
                    subjectId,
                    default(GameplayPosition),
                    displacementSequence);
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
                    return Evaluate(
                        actorId,
                        actionId,
                        subjectId,
                        origin,
                        displacementSequence);
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
                return Evaluate(
                    actorId,
                    actionId,
                    subjectId,
                    origin,
                    displacementSequence);
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
                        EvaluateIntentInDirection(
                            actorId,
                            actionId,
                            subjectId,
                            target.Subject,
                            origin,
                            candidateX,
                            candidateZ,
                            displacementSequence);
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

            return EvaluateIntentInDirection(
                actorId,
                actionId,
                subjectId,
                target.Subject,
                origin,
                directionX,
                directionZ,
                displacementSequence);
        }

        public DisplacementDestinationEvaluation EvaluateDirectionalPushOff(
            string actorId,
            string actionId,
            string subjectId,
            GameplayPosition directionTarget,
            long displacementSequence)
        {
            RequireId(actorId, nameof(actorId));
            RequireId(actionId, nameof(actionId));
            RequireId(subjectId, nameof(subjectId));

            DisplacementActionAvailability availability =
                actionEvaluator.EvaluateAvailability(
                    actorId,
                    actionId,
                    startsEncounter: false);
            DisplacementTargetEvaluation target = actionEvaluator.EvaluateTarget(
                actorId,
                actionId,
                subjectId);
            if (!availability.IsAvailable || !target.IsEligible
                || !actionEvaluator.TryGetSubjectPosition(
                    target.Subject,
                    out GameplayPosition origin))
            {
                GameplayPosition unavailableDestination = target.Subject != null
                    && actionEvaluator.TryGetSubjectPosition(
                        target.Subject,
                        out GameplayPosition unavailableOrigin)
                            ? unavailableOrigin
                            : default(GameplayPosition);
                return Evaluate(
                    actorId,
                    actionId,
                    subjectId,
                    unavailableDestination,
                    displacementSequence);
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
                return Evaluate(
                    actorId,
                    actionId,
                    subjectId,
                    origin,
                    displacementSequence);
            }

            return EvaluateIntentInDirection(
                actorId,
                actionId,
                subjectId,
                target.Subject,
                origin,
                directionX / magnitude,
                directionZ / magnitude,
                displacementSequence);
        }

        public bool TryValidateRequest(
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

        public static PropDisplacementState ResolvePropState(
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

        private DisplacementDestinationEvaluation EvaluateIntentInDirection(
            string actorId,
            string actionId,
            string subjectId,
            DisplacementSubjectDefinition subject,
            GameplayPosition origin,
            float directionX,
            float directionZ,
            long displacementSequence)
        {
            DisplacementActionDefinition action = actionEvaluator
                .EvaluateAvailability(
                    actorId,
                    actionId,
                    startsEncounter: false)
                .Action;
            float maximumDistance = action.GetMaximumDistance(subject);
            DisplacementDestinationEvaluation maximum = EvaluateAtDistance(
                actorId,
                actionId,
                subjectId,
                origin,
                directionX,
                directionZ,
                maximumDistance,
                displacementSequence);
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
                    EvaluateAtDistance(
                        actorId,
                        actionId,
                        subjectId,
                        origin,
                        directionX,
                        directionZ,
                        candidateDistance,
                        displacementSequence);
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

        private DisplacementDestinationEvaluation EvaluateAtDistance(
            string actorId,
            string actionId,
            string subjectId,
            GameplayPosition origin,
            float directionX,
            float directionZ,
            float distance,
            long displacementSequence) =>
            Evaluate(
                actorId,
                actionId,
                subjectId,
                new GameplayPosition(
                    origin.X + (directionX * distance),
                    origin.Y,
                    origin.Z + (directionZ * distance)),
                displacementSequence);

        private static bool IsSpatialDestinationFailure(
            DisplacementResolutionFailure failure) =>
            failure == DisplacementResolutionFailure.DestinationBlocked
            || failure == DisplacementResolutionFailure.GetUpSpaceBlocked;

        private static DisplacementDestinationEvaluation CreateEvaluation(
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

        private static void RequireId(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    "Displacement identifiers cannot be empty.",
                    parameterName);
            }
        }
    }
}
