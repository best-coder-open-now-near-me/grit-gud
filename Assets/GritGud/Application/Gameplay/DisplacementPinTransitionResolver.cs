using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;

namespace GritGud.Application.Gameplay
{
    internal sealed class DisplacementPinTransitionResolver
    {
        private readonly GameplaySession gameplay;
        private readonly IReadOnlyDictionary<string, DisplacementSubjectDefinition>
            subjects;

        public DisplacementPinTransitionResolver(
            GameplaySession gameplaySession,
            IReadOnlyDictionary<string, DisplacementSubjectDefinition>
                subjectDefinitions)
        {
            gameplay = gameplaySession ??
                throw new ArgumentNullException(nameof(gameplaySession));
            subjects = subjectDefinitions ??
                throw new ArgumentNullException(nameof(subjectDefinitions));
        }

        public bool TryResolve(
            string actorId,
            DisplacementSubjectDefinition propSubject,
            DisplacementActionDefinition action,
            DestructiblePropSnapshot prop,
            DisplacementPathValidation path,
            long displacementSequence,
            ref DisplacementResultPolicies appliedResults,
            out ActorPinTransition transition,
            out DisplacementResolutionFailure failure)
        {
            if (propSubject == null)
                throw new ArgumentNullException(nameof(propSubject));
            if (action == null)
                throw new ArgumentNullException(nameof(action));
            if (path.Contacts == null)
                throw new ArgumentException(
                    "Displacement paths require contact evidence.",
                    nameof(path));
            if (displacementSequence <= 0L)
                throw new ArgumentOutOfRangeException(
                    nameof(displacementSequence));

            transition = null;
            GameplayActorSnapshot actingActor = gameplay.GetActor(actorId);
            if (action.Intent == DisplacementActionKind.PushOff)
            {
                return TryResolveRelease(
                    actingActor,
                    action,
                    prop,
                    path,
                    ref appliedResults,
                    out transition,
                    out failure);
            }

            if (path.Contacts.Count == 0)
            {
                failure = DisplacementResolutionFailure.None;
                return true;
            }
            if (!appliedResults.HasFlag(DisplacementResultPolicies.Topple)
                || propSubject.Pinning == null
                || !action.AllowedResults.HasFlag(
                    DisplacementResultPolicies.Pin)
                || path.Contacts.Count != 1)
            {
                failure = DisplacementResolutionFailure.DestinationBlocked;
                return false;
            }

            DisplacementContactEvidence contact = path.Contacts[0];
            if (!subjects.TryGetValue(
                    contact.EntityId,
                    out DisplacementSubjectDefinition contactedSubject)
                || contactedSubject.Kind != DisplacementSubjectKind.Combatant
                || string.Equals(
                    contact.EntityId,
                    actorId,
                    StringComparison.Ordinal)
                || !gameplay.TryGetActor(
                    contact.EntityId,
                    out GameplayActorSnapshot contactedActor)
                || contactedActor.IsIncapacitated
                || contactedActor.IsPinned
                || !propSubject.Pinning.Accepts(
                    contactedSubject.Mass,
                    contact.OverlapDepth))
            {
                failure = DisplacementResolutionFailure.DestinationBlocked;
                return false;
            }

            var resultingPin = new ActorPinState(
                contactedActor.ActorId,
                prop.PropId,
                displacementSequence,
                contact);
            transition = new ActorPinTransition(
                contactedActor.ActorId,
                contactedActor.Pose,
                contactedActor.Pose,
                previousState: null,
                resultingPin);
            appliedResults |= DisplacementResultPolicies.Pin;
            failure = DisplacementResolutionFailure.None;
            return true;
        }

        private static bool TryResolveRelease(
            GameplayActorSnapshot actingActor,
            DisplacementActionDefinition action,
            DestructiblePropSnapshot prop,
            DisplacementPathValidation path,
            ref DisplacementResultPolicies appliedResults,
            out ActorPinTransition transition,
            out DisplacementResolutionFailure failure)
        {
            transition = null;
            if (actingActor.PinState == null)
            {
                failure = DisplacementResolutionFailure.ActorNotPinned;
                return false;
            }
            if (!string.Equals(
                    actingActor.PinState.PropId,
                    prop.PropId,
                    StringComparison.Ordinal))
            {
                failure = DisplacementResolutionFailure.NotPinningActor;
                return false;
            }
            if (!action.AllowedResults.HasFlag(
                    DisplacementResultPolicies.Release))
            {
                failure = DisplacementResolutionFailure.ActionUnavailable;
                return false;
            }
            if (path.Contacts.Count > 0)
            {
                failure = DisplacementResolutionFailure.DestinationBlocked;
                return false;
            }

            appliedResults |= DisplacementResultPolicies.Release;
            transition = new ActorPinTransition(
                actingActor.ActorId,
                actingActor.Pose,
                FaceToward(actingActor.Pose, prop.Position),
                actingActor.PinState,
                resultingState: null);
            failure = DisplacementResolutionFailure.None;
            return true;
        }

        private static GameplayActorPose FaceToward(
            GameplayActorPose pose,
            GameplayPosition target)
        {
            double deltaX = (double)target.X - pose.Position.X;
            double deltaZ = (double)target.Z - pose.Position.Z;
            if (Math.Abs(deltaX) <= 0.0001d
                && Math.Abs(deltaZ) <= 0.0001d)
            {
                return pose;
            }

            float facing = (float)(
                Math.Atan2(deltaX, deltaZ) * (180d / Math.PI));
            return new GameplayActorPose(
                pose.Position,
                facing,
                pose.Stance);
        }
    }
}
