using System;
using GritGud.Domain.Gameplay;

namespace GritGud.Application.Gameplay
{
    internal sealed class DisplacementPropResolver
    {
        private readonly GameplaySession gameplay;
        private readonly DestructiblePropSession destructibles;
        private readonly DisplacementDestinationEvaluator destinationEvaluator;
        private readonly DisplacementPinTransitionResolver pinTransitionResolver;

        public DisplacementPropResolver(
            GameplaySession gameplaySession,
            DestructiblePropSession destructibleSession,
            DisplacementDestinationEvaluator destinations,
            DisplacementPinTransitionResolver pinTransitions)
        {
            gameplay = gameplaySession ??
                throw new ArgumentNullException(nameof(gameplaySession));
            destructibles = destructibleSession ??
                throw new ArgumentNullException(nameof(destructibleSession));
            destinationEvaluator = destinations ??
                throw new ArgumentNullException(nameof(destinations));
            pinTransitionResolver = pinTransitions ??
                throw new ArgumentNullException(nameof(pinTransitions));
        }

        public bool TryResolve(
            string actorId,
            string propId,
            DisplacementSubjectDefinition subject,
            GameplayPosition destination,
            DisplacementActionDefinition definition,
            long displacementSequence,
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
                    displacementSequence,
                    ref appliedResults,
                    out ActorPinTransition pinTransition,
                    out failure))
            {
                record = null;
                return false;
            }

            record = new DisplacementRecord(
                displacementSequence,
                request,
                new PropDisplacementState(prop.Pose, prop.Posture),
                resultingState,
                appliedResults,
                pinTransition);
            failure = DisplacementResolutionFailure.None;
            return true;
        }
    }
}
