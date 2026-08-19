using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;

namespace GritGud.Application.Gameplay
{
    public sealed class GameplayExplosiveTransitionReducer :
        IGameplaySemanticTransitionReducer
    {
        public bool Supports(GameplayCapabilityProfile profile)
        {
            if (profile == null
                || profile.Capability
                    != GameplaySemanticCapability.ThrowExplosive)
                return false;
            try
            {
                string consequence = profile.GetTrait("consequence");
                return profile.GetTrait("delivery")
                        == "ballistic-landing-query"
                    && profile.GetTrait("targeting") == "world-area"
                    && profile.GetTrait("resource") == "inventory-quantity"
                    && (consequence == "smoke-field"
                        || consequence == "fire-field"
                        || consequence == "blast-actor-and-destructible");
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
                    "Explosive transition requires a supported resolved action.",
                    nameof(transition));
            GameplayActionRecord action = payload.Action;
            ValidateAction(state.Session, action);
            FindOutcomes(
                action,
                out ThrownExplosiveActionOutcome thrown,
                out InventoryQuantityChangedActionOutcome quantity);
            ThrownExplosiveRecord record = thrown.Record;
            if (!string.Equals(
                    record.ThrowerId,
                    action.Request.ActorId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    record.Definition.Id,
                    quantity.Change.ItemId,
                    StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Thrown explosive outcomes do not describe one item use.");

            var mutation = new GameplayCanonicalStateMutation(state);
            GameplayActorSnapshot acting = mutation.GetActor(record.ThrowerId);
            mutation.ReplaceActor(GameplayCanonicalStateMutation.CopyActor(
                acting,
                pose: FaceToward(acting.Pose, record.IntendedLanding),
                budget: action.ResultingBudget,
                inventory: ApplyQuantity(acting.Inventory, quantity.Change)));

            RequireBlastCoverage(state, record);
            GameplayBlastProjectionCounts counts =
                GameplayBlastStateProjector.Apply(
                    mutation,
                    record.BlastEffects,
                    record.Definition.BlastWoundMovementPenalty,
                    record.Definition.BlastIntegrityDamage);
            if (record.SmokeField != null)
            {
                mutation.AddSmokeField(new SmokeFieldSnapshot(
                    record.SmokeField,
                    remainingFraction: 1f));
            }
            if (record.FireField != null)
            {
                mutation.AddFireField(new FireFieldSnapshot(
                    record.FireField,
                    remainingFraction: 1f));
            }
            mutation.LastActionSequence = action.Sequence;
            mutation.JournalSequence = checked(
                mutation.JournalSequence
                + 1L
                + counts.DestructibleDamages);
            mutation.Revision = checked(
                mutation.Revision + 1L + counts.ActorInjuries);
            mutation.LastTransitionSequence = transition.Identity.Sequence;
            GameplayCombatStateSnapshot resulting = mutation.Build();
            return new GameplayReductionResult(
                state,
                resulting,
                new GameplayDomainEvent[]
                {
                    new GameplayTransitionReducedEvent(
                        transition.Identity,
                        record.Definition.Id,
                        action),
                });
        }

        private static void RequireBlastCoverage(
            GameplayCombatStateSnapshot state,
            ThrownExplosiveRecord record)
        {
            if (record.SmokeField != null)
                state.RequireCoverage(GameplayCombatStateCoverage.SmokeFields);
            if (record.FireField != null)
                state.RequireCoverage(GameplayCombatStateCoverage.FireFields);
            foreach (BlastEffectRecord effect in record.BlastEffects)
                if (effect.SubjectKind == BlastSubjectKind.DestructibleProp
                    && effect.Exposure > 0f
                    && record.Definition.BlastIntegrityDamage > 0f)
                {
                    state.RequireCoverage(
                        GameplayCombatStateCoverage.Destructibles);
                    return;
                }
        }

        private static void ValidateAction(
            GameplaySessionStateSnapshot session,
            GameplayActionRecord action)
        {
            if (action.Sequence != session.LastActionSequence + 1L)
                throw new InvalidOperationException(
                    "Explosive action is not the next action sequence.");
            if (session.Mode == GameplaySessionMode.TurnBased
                && !string.Equals(
                    session.ActiveActorId,
                    action.Request.ActorId,
                    StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Explosive action actor is not active.");
            GameplayActorSnapshot actor = session.GetActor(
                action.Request.ActorId);
            if (!BudgetsMatch(actor.TurnBudget, action.PreviousBudget)
                || !BudgetsMatch(
                    action.PreviousBudget.SpendAction(action.Cost),
                    action.ResultingBudget))
                throw new InvalidOperationException(
                    "Explosive action budget is not canonical.");
        }

        private static void FindOutcomes(
            GameplayActionRecord action,
            out ThrownExplosiveActionOutcome thrown,
            out InventoryQuantityChangedActionOutcome quantity)
        {
            thrown = null;
            quantity = null;
            foreach (GameplayActionOutcome outcome in action.Outcomes)
            {
                if (outcome is ThrownExplosiveActionOutcome foundThrow
                    && thrown == null)
                    thrown = foundThrow;
                else if (outcome
                    is InventoryQuantityChangedActionOutcome foundQuantity
                    && quantity == null)
                    quantity = foundQuantity;
                else
                    throw new ArgumentException(
                        "Explosive actions require one throw and one inventory outcome.",
                        nameof(action));
            }
            if (thrown == null || quantity == null)
                throw new ArgumentException(
                    "Explosive actions require one throw and one inventory outcome.",
                    nameof(action));
        }

        private static ActorInventorySnapshot ApplyQuantity(
            ActorInventorySnapshot inventory,
            InventoryQuantityChangeRecord change)
        {
            if (!string.Equals(
                inventory.ActorId,
                change.ActorId,
                StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Inventory change targets a different actor.");
            var quantities = new List<InventoryQuantitySnapshot>(
                inventory.Quantities.Count);
            bool replaced = false;
            foreach (InventoryQuantitySnapshot quantity in inventory.Quantities)
            {
                if (!string.Equals(
                    quantity.ItemId,
                    change.ItemId,
                    StringComparison.Ordinal))
                {
                    quantities.Add(quantity);
                    continue;
                }
                if (quantity.Quantity != change.PreviousQuantity)
                    throw new InvalidOperationException(
                        "Inventory change no longer starts at canonical quantity.");
                quantities.Add(new InventoryQuantitySnapshot(
                    change.ItemId,
                    change.ResultingQuantity));
                replaced = true;
            }
            if (!replaced)
                throw new KeyNotFoundException(
                    $"Inventory quantity '{change.ItemId}' is absent.");
            return new ActorInventorySnapshot(inventory.ActorId, quantities);
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
                (float)(Math.Atan2(x, z) * (180d / Math.PI)),
                pose.Stance);
        }

        private static bool BudgetsMatch(TurnBudget left, TurnBudget right) =>
            left.ActionPoints == right.ActionPoints
            && left.MovementOpportunity == right.MovementOpportunity;
    }
}
