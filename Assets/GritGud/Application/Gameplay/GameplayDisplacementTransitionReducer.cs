using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;

namespace GritGud.Application.Gameplay
{
    public sealed class GameplayDisplacementTransitionReducer :
        IGameplaySemanticTransitionReducer
    {
        public bool Supports(GameplayCapabilityProfile profile)
        {
            if (profile == null
                || profile.Capability != GameplaySemanticCapability.Displace)
                return false;
            try
            {
                DisplacementSubjectKinds subjects =
                    DisplacementSubjectKinds.None;
                bool valid = Enum.TryParse(
                        profile.GetTrait("intent"),
                        out DisplacementActionKind _)
                    && Enum.TryParse(
                        profile.GetTrait("subjects"),
                        out subjects)
                    && Enum.TryParse(
                        profile.GetTrait("contest"),
                        out DisplacementContestPolicy _)
                    && Enum.TryParse(
                        profile.GetTrait("results"),
                        out DisplacementResultPolicies _)
                    && Enum.TryParse(
                        profile.GetTrait("hands"),
                        out DisplacementHandRequirement _)
                    && Enum.TryParse(
                        profile.GetTrait("auto-stow"),
                        out DisplacementAutoStowPolicy _)
                    && (profile.GetTrait("distance") == "fixed"
                        || profile.GetTrait("distance") == "mass-decay");
                if (!valid) return false;
                GameplaySemanticSubjectKind subject =
                    GameplayCapabilityProfiles.GetSubjectKind(profile);
                return subject == GameplaySemanticSubjectKind.Actor
                    ? (subjects & DisplacementSubjectKinds.Combatant) != 0
                    : subject == GameplaySemanticSubjectKind.DestructibleProp
                        && (subjects & DisplacementSubjectKinds.Prop) != 0;
            }
            catch (KeyNotFoundException)
            {
                return false;
            }
        }

        public GameplayReductionResult Reduce(
            GameplayCombatStateSnapshot state,
            GameplaySemanticTransition transition)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (transition == null)
                throw new ArgumentNullException(nameof(transition));
            if (!(transition.Payload
                is GameplayResolvedActionTransitionPayload payload)
                || !Supports(payload.Profile))
                throw new ArgumentException(
                    "Displacement transition requires a supported resolved action.",
                    nameof(transition));
            GameplayActionRecord action = payload.Action;
            ValidateAction(state.Session, action);
            FindOutcomes(
                action,
                out EquipmentChangedActionOutcome equipment,
                out DisplacementActionOutcome displaced);
            DisplacementRecord record = displaced.Displacement;

            var mutation = new GameplayCanonicalStateMutation(state);
            GameplayActorSnapshot acting = mutation.GetActor(
                action.Request.ActorId);
            bool pinTransitionOwnsActingPose = record.PinTransition != null
                && string.Equals(
                    record.PinTransition.ActorId,
                    acting.ActorId,
                    StringComparison.Ordinal);
            mutation.ReplaceActor(GameplayCanonicalStateMutation.CopyActor(
                acting,
                pose: pinTransitionOwnsActingPose
                    ? acting.Pose
                    : FaceToward(acting.Pose, record.PreviousPosition),
                budget: action.ResultingBudget));
            if (equipment != null)
                ApplyEquipment(mutation, equipment.Change);

            long revisionIncrement = 1L;
            if (record.Succeeded)
            {
                if (record.Request.SubjectKind
                    == DisplacementSubjectKind.Combatant)
                {
                    ApplyCombatant(mutation, record);
                    revisionIncrement++;
                }
                else
                {
                    state.RequireCoverage(
                        GameplayCombatStateCoverage.Destructibles);
                    ApplyProp(mutation, record);
                    if (record.PinTransition != null)
                    {
                        ApplyPin(mutation, record.PinTransition);
                        revisionIncrement++;
                    }
                }
            }

            mutation.LastActionSequence = action.Sequence;
            mutation.JournalSequence = checked(mutation.JournalSequence + 2L);
            mutation.Revision = checked(
                mutation.Revision + revisionIncrement);
            mutation.LastTransitionSequence = transition.Identity.Sequence;
            GameplayCombatStateSnapshot resulting = mutation.Build();
            return new GameplayReductionResult(
                state,
                resulting,
                new GameplayDomainEvent[]
                {
                    new GameplayTransitionReducedEvent(
                        transition.Identity,
                        record.Request.SubjectId,
                        action),
                });
        }

        private static void ApplyEquipment(
            GameplayCanonicalStateMutation mutation,
            EquipmentChangeRecord change)
        {
            GameplayActorSnapshot actor = mutation.GetActor(change.ActorId);
            if (!string.Equals(
                    actor.EquippedItemId,
                    change.PreviousEquippedItemId,
                    StringComparison.Ordinal)
                || change.ResultingEquippedItemId != null)
                throw new InvalidOperationException(
                    "Displacement auto-stow must unequip the canonical item.");
            mutation.ReplaceActor(GameplayCanonicalStateMutation.CopyActor(
                actor,
                equippedItemId: null,
                equipmentEffects: EquipmentEffectSet.None,
                replaceEquipment: true));
        }

        private static void ApplyCombatant(
            GameplayCanonicalStateMutation mutation,
            DisplacementRecord record)
        {
            GameplayActorSnapshot target = mutation.GetActor(
                record.Request.SubjectId);
            if (target.Pose.Position.DistanceTo(record.PreviousPosition) > 0f)
                throw new InvalidOperationException(
                    "Combatant displacement starts from stale state.");
            mutation.ReplaceActor(GameplayCanonicalStateMutation.CopyActor(
                target,
                pose: new GameplayActorPose(
                    record.ResultingPosition,
                    target.Pose.FacingDegrees,
                    target.Pose.Stance)));
        }

        private static void ApplyProp(
            GameplayCanonicalStateMutation mutation,
            DisplacementRecord record)
        {
            DestructiblePropSnapshot prop = mutation.GetDestructible(
                record.Request.SubjectId);
            if (record.PreviousPropState == null
                || record.ResultingPropState == null
                || !PropMatches(prop, record.PreviousPropState))
                throw new InvalidOperationException(
                    "Prop displacement starts from stale state.");
            mutation.ReplaceDestructible(new DestructiblePropSnapshot(
                prop.PropId,
                prop.State,
                prop.MaximumIntegrity,
                prop.RemainingIntegrity,
                record.ResultingPropState.Pose,
                record.ResultingPropState.Posture,
                prop.FractureChunkCount,
                prop.DetachedFractureChunks));
        }

        private static void ApplyPin(
            GameplayCanonicalStateMutation mutation,
            ActorPinTransition pin)
        {
            GameplayActorSnapshot actor = mutation.GetActor(pin.ActorId);
            if (!PosesMatch(actor.Pose, pin.PreviousPose)
                || !PinMatches(actor.PinState, pin.PreviousState))
                throw new InvalidOperationException(
                    $"Pin transition for '{pin.ActorId}' starts from stale actor state: "
                    + $"canonical pose ({actor.Pose.Position.X}, {actor.Pose.Position.Y}, "
                    + $"{actor.Pose.Position.Z})/{actor.Pose.FacingDegrees}/"
                    + $"{actor.Pose.Stance}, recorded pose "
                    + $"({pin.PreviousPose.Position.X}, {pin.PreviousPose.Position.Y}, "
                    + $"{pin.PreviousPose.Position.Z})/{pin.PreviousPose.FacingDegrees}/"
                    + $"{pin.PreviousPose.Stance}.");
            mutation.ReplaceActor(GameplayCanonicalStateMutation.CopyActor(
                actor,
                pose: pin.ResultingPose,
                pinState: pin.ResultingState,
                replacePin: true));
        }

        private static void ValidateAction(
            GameplaySessionStateSnapshot session,
            GameplayActionRecord action)
        {
            if (action.Sequence != session.LastActionSequence + 1L
                || !string.Equals(
                    action.Request.ActorId,
                    session.ActiveActorId,
                    StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Displacement action sequence or actor is stale.");
            GameplayActorSnapshot actor = session.GetActor(
                action.Request.ActorId);
            if (!BudgetsMatch(actor.TurnBudget, action.PreviousBudget)
                || !BudgetsMatch(
                    action.PreviousBudget.SpendAction(action.Cost),
                    action.ResultingBudget))
                throw new InvalidOperationException(
                    "Displacement action budget is stale.");
        }

        private static void FindOutcomes(
            GameplayActionRecord action,
            out EquipmentChangedActionOutcome equipment,
            out DisplacementActionOutcome displaced)
        {
            equipment = null;
            displaced = null;
            foreach (GameplayActionOutcome outcome in action.Outcomes)
            {
                if (outcome is EquipmentChangedActionOutcome foundEquipment
                    && equipment == null)
                    equipment = foundEquipment;
                else if (outcome is DisplacementActionOutcome foundDisplacement
                    && displaced == null)
                    displaced = foundDisplacement;
                else
                    throw new ArgumentException(
                        "Displacement actions contain unsupported outcomes.",
                        nameof(action));
            }
            if (displaced == null)
                throw new ArgumentException(
                    "Displacement action has no displacement outcome.",
                    nameof(action));
        }

        private static GameplayActorPose FaceToward(
            GameplayActorPose pose,
            GameplayPosition target)
        {
            double x = target.X - pose.Position.X;
            double z = target.Z - pose.Position.Z;
            return Math.Abs(x) <= 0.0001d && Math.Abs(z) <= 0.0001d
                ? pose
                : new GameplayActorPose(
                    pose.Position,
                    (float)(Math.Atan2(x, z) * (180d / Math.PI)),
                    pose.Stance);
        }

        private static bool PropMatches(
            DestructiblePropSnapshot prop,
            PropDisplacementState state) =>
            prop.Pose.Position.DistanceTo(state.Pose.Position) == 0f
            && prop.Pose.YawDegrees == state.Pose.YawDegrees
            && prop.Pose.PitchDegrees == state.Pose.PitchDegrees
            && prop.Pose.RollDegrees == state.Pose.RollDegrees
            && prop.Posture == state.Posture;

        private static bool PosesMatch(
            GameplayActorPose left,
            GameplayActorPose right) =>
            left.Position.DistanceTo(right.Position) == 0f
            && left.FacingDegrees == right.FacingDegrees
            && left.Stance == right.Stance;

        private static bool PinMatches(ActorPinState left, ActorPinState right) =>
            ReferenceEquals(left, right)
            || (left != null && right != null
                && string.Equals(left.ActorId, right.ActorId, StringComparison.Ordinal)
                && string.Equals(left.PropId, right.PropId, StringComparison.Ordinal)
                && left.DisplacementSequence == right.DisplacementSequence);

        private static bool BudgetsMatch(TurnBudget left, TurnBudget right) =>
            left.ActionPoints == right.ActionPoints
            && left.MovementOpportunity == right.MovementOpportunity;
    }
}
