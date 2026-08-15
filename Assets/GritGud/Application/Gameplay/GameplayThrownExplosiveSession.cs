using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;

namespace GritGud.Application.Gameplay
{
    public enum ThrownExplosiveFailure
    {
        None,
        TurnModeRequired,
        ActorNotActive,
        ActorIncapacitated,
        OperationInProgress,
        Depleted,
        OutOfRange,
        InsufficientActionPoints,
        InsufficientMovementOpportunity,
    }

    public sealed class GameplayThrownExplosiveSession
    {
        private readonly GameplaySession gameplay;
        private readonly IThrownExplosiveLandingQuery landing;
        private readonly IBlastWorldQuery blast;
        private readonly GameplayBlastConsequenceResolver consequences;
        private readonly GameplaySmokeFieldSession smokeFields;
        private readonly IUncertaintySampler uncertainty;
        private readonly List<ThrownExplosiveRecord> throws = new List<ThrownExplosiveRecord>();
        private readonly IReadOnlyList<ThrownExplosiveRecord> readOnlyThrows;

        public GameplayThrownExplosiveSession(
            GameplaySession gameplaySession,
            IThrownExplosiveLandingQuery landingQuery,
            IBlastWorldQuery blastQuery,
            GameplayBlastConsequenceResolver consequenceResolver,
            IUncertaintySampler uncertaintySampler,
            GameplaySmokeFieldSession smokeFieldSession = null)
        {
            gameplay = gameplaySession ?? throw new ArgumentNullException(nameof(gameplaySession));
            landing = landingQuery ?? throw new ArgumentNullException(
                nameof(landingQuery));
            blast = blastQuery ?? throw new ArgumentNullException(
                nameof(blastQuery));
            consequences = consequenceResolver ??
                throw new ArgumentNullException(nameof(consequenceResolver));
            smokeFields = smokeFieldSession;
            uncertainty = uncertaintySampler ?? throw new ArgumentNullException(nameof(uncertaintySampler));
            readOnlyThrows = throws.AsReadOnly();
        }

        public IReadOnlyList<ThrownExplosiveRecord> Throws => readOnlyThrows;

        public bool TryPreview(
            string actorId,
            ThrownExplosiveDefinition definition,
            GameplayPosition intendedLanding,
            out float uncertaintyRadius,
            out ThrownExplosiveFailure failure)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            uncertaintyRadius = 0f;
            if (!TryValidatePreview(
                    actorId, definition, intendedLanding,
                    out GameplayActorSnapshot actor, out failure))
                return false;
            uncertaintyRadius = definition.GetUncertaintyRadius(
                actor.Pose.Position.DistanceTo(intendedLanding));
            return true;
        }

        public bool TryThrow(
            string actorId,
            ThrownExplosiveDefinition definition,
            GameplayPosition intendedLanding,
            out GameplayActionRecord action,
            out ThrownExplosiveFailure failure)
        {
            action = null;
            if (!TryPrepareThrow(
                    actorId,
                    definition,
                    intendedLanding,
                    out ThrownExplosiveRecord prepared,
                    out failure))
            {
                return false;
            }

            return TryCommitPreparedThrow(prepared, out action, out failure);
        }

        public bool TryPrepareThrow(
            string actorId,
            ThrownExplosiveDefinition definition,
            GameplayPosition intendedLanding,
            out ThrownExplosiveRecord prepared,
            out ThrownExplosiveFailure failure)
        {
            prepared = null;
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (!TryValidateAction(
                    actorId,
                    definition,
                    intendedLanding,
                    startsEncounter: false,
                    out GameplayActorSnapshot actor,
                    out failure))
            {
                return false;
            }

            float distance = actor.Pose.Position.DistanceTo(intendedLanding);
            float radius = definition.GetUncertaintyRadius(distance);
            GameplayPosition sampled = uncertainty.Sample(intendedLanding, radius);
            if (sampled.DistanceTo(intendedLanding) > radius + 0.0001f)
                throw new InvalidOperationException(
                    "Uncertainty samplers must return a point inside the previewed region.");
            GameplayPosition launchOrigin = definition.GetLaunchOrigin(actor.Pose);
            ThrownExplosiveLandingResult landingResult = landing.Resolve(
                launchOrigin,
                sampled);
            BlastWorldQueryResult blastResult = blast.Query(
                new BlastWorldQuery(
                    landingResult.LandingPosition,
                    definition.AreaRadius));
            if (landingResult.WorldStateRevision
                != blastResult.WorldStateRevision)
            {
                throw new InvalidOperationException(
                    "Landing and blast evidence must describe one world revision.");
            }

            long sequence = throws.Count + 1L;
            SmokeFieldRecord smokeField = definition.SmokeField == null
                ? null
                : new SmokeFieldRecord(
                    $"smoke.{actorId}.{sequence}",
                    actorId,
                    definition.Id,
                    landingResult.LandingPosition,
                    definition.SmokeField);
            prepared = new ThrownExplosiveRecord(
                sequence, actorId, definition, actor.Pose.Position,
                launchOrigin, intendedLanding, sampled,
                landingResult.LandingPosition, radius,
                blastResult.WorldStateRevision, blastResult.Effects,
                smokeField);
            failure = ThrownExplosiveFailure.None;
            return true;
        }

        public bool TryPrepareThrowItem(
            string actorId,
            string itemId,
            GameplayPosition intendedLanding,
            out ThrownExplosiveRecord prepared,
            out ThrownExplosiveFailure failure)
        {
            InventoryItemDefinition item = gameplay.GetInventoryItem(actorId, itemId);
            if (!(item.ConsumablePower is ThrownExplosiveDefinition definition))
            {
                throw new InvalidOperationException(
                    $"Inventory item '{itemId}' is not a thrown explosive.");
            }

            return TryPrepareThrow(
                actorId,
                definition,
                intendedLanding,
                out prepared,
                out failure);
        }

        public bool TryCommitPreparedThrow(
            ThrownExplosiveRecord prepared,
            out GameplayActionRecord action,
            out ThrownExplosiveFailure failure)
        {
            action = null;
            if (prepared == null)
            {
                throw new ArgumentNullException(nameof(prepared));
            }

            if (prepared.Sequence != throws.Count + 1L)
            {
                throw new InvalidOperationException(
                    "The prepared explosive is not the next authoritative throw.");
            }

            bool startsEncounter = gameplay.ThrownExplosiveStartsEncounter(
                prepared);
            if (!TryValidateAction(
                    prepared.ThrowerId,
                    prepared.Definition,
                    prepared.IntendedLanding,
                    startsEncounter,
                    out GameplayActorSnapshot actor,
                    out failure))
            {
                return false;
            }

            ActionCost cost = GetActionCost(
                prepared.Definition,
                startsEncounter);
            TurnBudget resultingBudget = actor.TurnBudget.SpendAction(cost);
            int previousQuantity = gameplay.GetInventoryQuantity(
                prepared.ThrowerId,
                prepared.Definition.Id);
            var quantityChange = new InventoryQuantityChangeRecord(
                prepared.ThrowerId,
                prepared.Definition.Id,
                previousQuantity,
                consumedQuantity: 1,
                resultingQuantity: previousQuantity - 1);
            action = new GameplayActionRecord(
                gameplay.LastResolvedAction == null ? 1L : gameplay.LastResolvedAction.Sequence + 1L,
                new GameplayActionRequest(
                    prepared.ThrowerId,
                    prepared.Definition.Id,
                    prepared.Definition.Id),
                cost,
                actor.TurnBudget,
                resultingBudget,
                new GameplayActionOutcome[]
                {
                    new ThrownExplosiveActionOutcome(prepared),
                    new InventoryQuantityChangedActionOutcome(quantityChange),
                });
            CommitThrow(action);
            failure = ThrownExplosiveFailure.None;
            return true;
        }

        public bool TryThrowItem(
            string actorId,
            string itemId,
            GameplayPosition intendedLanding,
            out GameplayActionRecord action,
            out ThrownExplosiveFailure failure)
        {
            InventoryItemDefinition item = gameplay.GetInventoryItem(actorId, itemId);
            if (!(item.ConsumablePower is ThrownExplosiveDefinition definition))
                throw new InvalidOperationException(
                    $"Inventory item '{itemId}' is not a thrown explosive.");
            return TryThrow(
                actorId, definition, intendedLanding,
                out action, out failure);
        }

        public void CommitThrow(GameplayActionRecord action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            if (!TryGetOutcomes(
                    action,
                    out ThrownExplosiveActionOutcome outcome,
                    out InventoryQuantityChangedActionOutcome quantity))
                throw new ArgumentException(
                    "Thrown explosive actions require one throw and one inventory-consumption outcome.",
                    nameof(action));
            if (!string.Equals(
                    outcome.Record.Definition.Id,
                    quantity.Change.ItemId,
                    StringComparison.Ordinal)
                || quantity.Change.ConsumedQuantity != 1)
            {
                throw new InvalidOperationException(
                    "Thrown explosive actions must consume their matching inventory item.");
            }
            if (outcome.Record.Sequence != throws.Count + 1L)
                throw new InvalidOperationException(
                    "The thrown explosive is not the next authoritative throw.");
            consequences.Validate(
                outcome.Record.BlastEffects,
                outcome.Record.Definition.BlastWoundMovementPenalty,
                outcome.Record.Definition.BlastIntegrityDamage);
            if (outcome.Record.SmokeField != null && smokeFields == null)
                throw new InvalidOperationException(
                    "Thrown smoke requires an authoritative smoke-field session.");
            if (outcome.Record.SmokeField != null
                && smokeFields.TryGetField(
                    outcome.Record.SmokeField.Id,
                    out _))
            {
                throw new InvalidOperationException(
                    $"Smoke field '{outcome.Record.SmokeField.Id}' is already active.");
            }

            var notifications = new GameplayNotificationBatch();
            gameplay.CommitAction(action, notifications);
            throws.Add(outcome.Record);
            consequences.Apply(
                outcome.Record.BlastEffects,
                outcome.Record.Definition.BlastWoundMovementPenalty,
                outcome.Record.Definition.BlastIntegrityDamage,
                notifications);
            if (outcome.Record.SmokeField != null)
            {
                smokeFields.Deploy(
                    outcome.Record.SmokeField,
                    notifications);
            }

            notifications.Publish();
        }

        private static bool TryGetOutcomes(
            GameplayActionRecord action,
            out ThrownExplosiveActionOutcome thrown,
            out InventoryQuantityChangedActionOutcome quantity)
        {
            thrown = null;
            quantity = null;
            foreach (GameplayActionOutcome outcome in action.Outcomes)
            {
                if (outcome is ThrownExplosiveActionOutcome thrownOutcome)
                {
                    if (thrown != null) return false;
                    thrown = thrownOutcome;
                }
                else if (outcome
                    is InventoryQuantityChangedActionOutcome quantityOutcome)
                {
                    if (quantity != null) return false;
                    quantity = quantityOutcome;
                }
                else
                {
                    return false;
                }
            }

            return thrown != null && quantity != null;
        }

        private static bool Fail(ThrownExplosiveFailure value, out ThrownExplosiveFailure failure)
        {
            failure = value;
            return false;
        }

        private bool TryValidatePreview(
            string actorId,
            ThrownExplosiveDefinition definition,
            GameplayPosition intendedLanding,
            out GameplayActorSnapshot actor,
            out ThrownExplosiveFailure failure) =>
            TryValidateAction(
                actorId,
                definition,
                intendedLanding,
                startsEncounter: false,
                out actor,
                out failure);

        private bool TryValidateAction(
            string actorId,
            ThrownExplosiveDefinition definition,
            GameplayPosition intendedLanding,
            bool startsEncounter,
            out GameplayActorSnapshot actor,
            out ThrownExplosiveFailure failure)
        {
            actor = default;
            if (gameplay.Operation != GameplaySessionOperation.None)
                return Fail(ThrownExplosiveFailure.OperationInProgress, out failure);
            if (gameplay.Mode == GameplaySessionMode.TurnBased)
            {
                if (!TryGetActiveActor(actorId, out actor))
                {
                    return Fail(
                        ThrownExplosiveFailure.ActorNotActive,
                        out failure);
                }
            }
            else if (!gameplay.TryGetActor(actorId, out actor))
            {
                return Fail(ThrownExplosiveFailure.ActorNotActive, out failure);
            }

            if (gameplay.IsActorIncapacitated(actorId))
            {
                return Fail(
                    ThrownExplosiveFailure.ActorIncapacitated,
                    out failure);
            }

            if (startsEncounter
                && !gameplay.EncounterActive
                && gameplay.Mode == GameplaySessionMode.Exploration
                && !gameplay.CanEnterTurnMode)
            {
                return Fail(
                    ThrownExplosiveFailure.TurnModeRequired,
                    out failure);
            }

            if (gameplay.GetInventoryQuantity(actorId, definition.Id) <= 0)
            {
                return Fail(ThrownExplosiveFailure.Depleted, out failure);
            }

            return TryValidateRangeAndCost(
                definition,
                GetActionCost(definition, startsEncounter),
                intendedLanding,
                actor,
                out failure);
        }

        private bool TryGetActiveActor(
            string actorId,
            out GameplayActorSnapshot actor)
        {
            actor = default;
            return string.Equals(
                gameplay.ActiveActorId,
                actorId,
                StringComparison.Ordinal)
            && gameplay.TryGetActor(actorId, out actor);
        }

        private static bool TryValidateRangeAndCost(
            ThrownExplosiveDefinition definition,
            ActionCost cost,
            GameplayPosition intendedLanding,
            GameplayActorSnapshot actor,
            out ThrownExplosiveFailure failure)
        {
            float distance = actor.Pose.Position.DistanceTo(intendedLanding);
            if (distance > definition.MaximumRange)
                return Fail(ThrownExplosiveFailure.OutOfRange, out failure);
            if (actor.TurnBudget.ActionPoints < cost.ActionPoints)
                return Fail(ThrownExplosiveFailure.InsufficientActionPoints, out failure);
            if (actor.TurnBudget.MovementOpportunity < cost.MovementOpportunity)
                return Fail(ThrownExplosiveFailure.InsufficientMovementOpportunity, out failure);
            failure = ThrownExplosiveFailure.None;
            return true;
        }

        private ActionCost GetActionCost(
            ThrownExplosiveDefinition definition,
            bool startsEncounter) =>
            gameplay.Mode == GameplaySessionMode.TurnBased
                || startsEncounter
                ? definition.TurnCost
                : new ActionCost(
                    0,
                    0f,
                    definition.TurnCost.Mobility);
    }
}
