using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;

namespace GritGud.Application.Gameplay
{
    public sealed class GameplayHeadlessExplosiveWorldQuery :
        IThrownExplosiveLandingQuery,
        IBlastWorldQuery
    {
        private readonly GameplayCombatStateSnapshot state;
        private readonly GameplayHeadlessSpatialEvidence spatial;

        public GameplayHeadlessExplosiveWorldQuery(
            GameplayCombatStateSnapshot canonicalState,
            GameplayHeadlessSpatialEvidence spatialEvidence)
        {
            state = canonicalState ?? throw new ArgumentNullException(
                nameof(canonicalState));
            spatial = spatialEvidence ?? throw new ArgumentNullException(
                nameof(spatialEvidence));
        }

        public ThrownExplosiveLandingResult Resolve(
            GameplayPosition launchOrigin,
            GameplayPosition sampledLanding) => spatial
                .ResolveThrownExplosiveLanding(
                    state,
                    launchOrigin,
                    sampledLanding);

        public BlastWorldQueryResult Query(BlastWorldQuery query) =>
            new BlastWorldQueryResult(
                query,
                state.Session.JournalSequence,
                spatial.CaptureBlastEffects(
                    state,
                    query.Origin,
                    query.Radius));
    }

    public static class GameplayThrownExplosivePreparation
    {
        public static bool TryPrepare(
            GameplayCombatStateSnapshot state,
            ScenarioDefinition scenario,
            string actorId,
            string semanticTargetId,
            ThrownExplosiveDefinition definition,
            GameplayPosition intendedLanding,
            IThrownExplosiveLandingQuery landing,
            IBlastWorldQuery blast,
            IUncertaintySampler uncertainty,
            bool canEnterTurnMode,
            out GameplayActionRecord action,
            out ThrownExplosiveFailure failure)
        {
            action = null;
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (scenario == null)
                throw new ArgumentNullException(nameof(scenario));
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));
            if (landing == null) throw new ArgumentNullException(nameof(landing));
            if (blast == null) throw new ArgumentNullException(nameof(blast));
            if (uncertainty == null)
                throw new ArgumentNullException(nameof(uncertainty));
            string targetId = GameplayContentIdentity.RequireText(
                semanticTargetId,
                nameof(semanticTargetId));
            if (!string.Equals(
                state.Session.ScenarioId,
                scenario.Id,
                StringComparison.Ordinal))
                throw new ArgumentException(
                    "Explosive rules and canonical state describe different scenarios.",
                    nameof(scenario));

            GameplaySessionStateSnapshot session = state.Session;
            if (session.Operation != GameplaySessionOperation.None)
                return Fail(
                    ThrownExplosiveFailure.OperationInProgress,
                    out failure);
            if (session.Mode == GameplaySessionMode.TurnBased
                && !string.Equals(
                    session.ActiveActorId,
                    actorId,
                    StringComparison.Ordinal))
                return Fail(
                    ThrownExplosiveFailure.ActorNotActive,
                    out failure);
            GameplayActorSnapshot actor;
            try
            {
                actor = session.GetActor(actorId);
            }
            catch (KeyNotFoundException)
            {
                return Fail(
                    ThrownExplosiveFailure.ActorNotActive,
                    out failure);
            }
            if (actor.IsIncapacitated)
                return Fail(
                    ThrownExplosiveFailure.ActorIncapacitated,
                    out failure);
            if (actor.IsPinned)
                return Fail(ThrownExplosiveFailure.ActorPinned, out failure);
            if (!GameplayInjuryCapabilityProjection.CanThrowExplosive(
                    actor.Capabilities))
                return Fail(
                    ThrownExplosiveFailure.InsufficientCapability,
                    out failure);
            if (!actor.Inventory.TryGetQuantity(
                    definition.Id,
                    out int previousQuantity)
                || previousQuantity <= 0)
                return Fail(ThrownExplosiveFailure.Depleted, out failure);

            long sequence = checked(session.LastActionSequence + 1L);
            ThrownExplosiveRangeProjection range =
                ThrownExplosiveRangeRules.Project(
                    actor.Pose.Position,
                    intendedLanding,
                    definition.MaximumRange);
            float distance = range.IntendedDistance;
            float uncertaintyRadius = definition.GetUncertaintyRadius(distance);
            var randomIdentity = new GameplayTransitionIdentity(
                sequence,
                GameplaySemanticCapability.ThrowExplosive.ToString(),
                actorId,
                definition.Id);
            GameplayPosition sampled = uncertainty.Sample(
                range.IntendedLanding,
                uncertaintyRadius,
                session.RunIdentity,
                randomIdentity,
                "landing-error");
            if (sampled.DistanceTo(range.IntendedLanding)
                > uncertaintyRadius + 0.0001f)
                throw new InvalidOperationException(
                    "Explosive uncertainty escaped its frozen preview radius.");
            GameplayPosition launchOrigin = definition.GetLaunchOrigin(
                actor.Pose);
            ThrownExplosiveLandingResult landingResult = landing.Resolve(
                launchOrigin,
                sampled);
            BlastWorldQueryResult blastResult = blast.Query(
                new BlastWorldQuery(
                    landingResult.LandingPosition,
                    definition.AreaRadius));
            if (landingResult.WorldStateRevision
                != blastResult.WorldStateRevision)
                throw new InvalidOperationException(
                    "Explosive landing and blast evidence describe different world revisions.");

            bool startsEncounter = StartsEncounter(
                scenario,
                blastResult.Effects);
            if (startsEncounter
                && !session.EncounterActive
                && session.Mode == GameplaySessionMode.Exploration
                && !canEnterTurnMode)
                return Fail(
                    ThrownExplosiveFailure.TurnModeRequired,
                    out failure);
            ActionCost cost = session.Mode == GameplaySessionMode.TurnBased
                    || startsEncounter
                ? definition.TurnCost
                : new ActionCost(0, 0f, definition.TurnCost.Mobility);
            if (actor.TurnBudget.ActionPoints < cost.ActionPoints)
                return Fail(
                    ThrownExplosiveFailure.InsufficientActionPoints,
                    out failure);
            if (actor.TurnBudget.MovementOpportunity
                < cost.MovementOpportunity)
                return Fail(
                    ThrownExplosiveFailure.InsufficientMovementOpportunity,
                    out failure);
            TurnBudget resultingBudget = actor.TurnBudget.SpendAction(cost);

            SmokeFieldRecord smokeField = definition.SmokeField == null
                ? null
                : new SmokeFieldRecord(
                    $"smoke.{actorId}.{sequence}",
                    actorId,
                    definition.Id,
                    landingResult.LandingPosition,
                    definition.SmokeField);
            FireFieldRecord fireField = definition.FireField == null
                ? null
                : new FireFieldRecord(
                    $"fire.{actorId}.{sequence}",
                    actorId,
                    definition.Id,
                    landingResult.LandingPosition,
                    definition.FireField);
            IReadOnlyList<ConcussiveActionPointEffectRecord> concussive =
                ResolveConcussiveEffects(
                    state,
                    blastResult.Effects,
                    definition.BlastActionPointReduction,
                    actorId,
                    resultingBudget.ActionPoints);
            var record = new ThrownExplosiveRecord(
                sequence,
                actorId,
                definition,
                actor.Pose.Position,
                launchOrigin,
                range.IntendedLanding,
                sampled,
                landingResult.LandingPosition,
                uncertaintyRadius,
                blastResult.WorldStateRevision,
                blastResult.Effects,
                smokeField,
                fireField,
                concussive,
                requestedLanding: intendedLanding);
            var quantity = new InventoryQuantityChangeRecord(
                actorId,
                definition.Id,
                previousQuantity,
                consumedQuantity: 1,
                resultingQuantity: previousQuantity - 1);
            action = new GameplayActionRecord(
                sequence,
                new GameplayActionRequest(
                    actorId,
                    definition.Id,
                    targetId),
                cost,
                actor.TurnBudget,
                resultingBudget,
                new GameplayActionOutcome[]
                {
                    new ThrownExplosiveActionOutcome(record),
                    new InventoryQuantityChangedActionOutcome(quantity),
                });
            failure = ThrownExplosiveFailure.None;
            return true;
        }

        private static bool StartsEncounter(
            ScenarioDefinition scenario,
            IEnumerable<BlastEffectRecord> effects)
        {
            foreach (BlastEffectRecord effect in effects)
                if (effect.Exposure > 0f
                    && scenario.TryGetAttackResponse(
                        effect.EntityId,
                        out AttackResponseDefinition response)
                    && response.StartsEncounter)
                    return true;
            return false;
        }

        private static IReadOnlyList<ConcussiveActionPointEffectRecord>
            ResolveConcussiveEffects(
                GameplayCombatStateSnapshot state,
                IEnumerable<BlastEffectRecord> effects,
                int maximumReduction,
                string actingActorId,
                int actingResultingActionPoints)
        {
            var result = new List<ConcussiveActionPointEffectRecord>();
            if (maximumReduction == 0) return result.AsReadOnly();
            foreach (BlastEffectRecord effect in effects)
            {
                if (effect.SubjectKind != BlastSubjectKind.Actor
                    || effect.Exposure <= 0f)
                    continue;
                GameplayActorSnapshot actor = state.Session.GetActor(
                    effect.EntityId);
                int previous = string.Equals(
                        actor.ActorId,
                        actingActorId,
                        StringComparison.Ordinal)
                    ? actingResultingActionPoints
                    : actor.TurnBudget.ActionPoints;
                int requested = ConcussiveActionPointRules.RequestedReduction(
                    maximumReduction,
                    effect.Exposure);
                int removed = Math.Min(previous, requested);
                result.Add(new ConcussiveActionPointEffectRecord(
                    actor.ActorId,
                    previous,
                    requested,
                    removed,
                    previous - removed));
            }
            result.Sort((left, right) => StringComparer.Ordinal.Compare(
                left.ActorId,
                right.ActorId));
            return result.AsReadOnly();
        }

        private static bool Fail(
            ThrownExplosiveFailure value,
            out ThrownExplosiveFailure failure)
        {
            failure = value;
            return false;
        }
    }

    public sealed class GameplayThrownExplosiveCandidateExecutionRoute :
        IGameplayCandidateExecutionRoute
    {
        public const string Id = "thrown-explosive.v1";

        private readonly ScenarioDefinition scenario;
        private readonly GameplayHeadlessSpatialEvidence spatial;
        private readonly IUncertaintySampler uncertainty;

        public GameplayThrownExplosiveCandidateExecutionRoute(
            GameplayScenarioAssembly assembly,
            GameplayHeadlessSpatialEvidence spatialEvidence,
            IUncertaintySampler uncertaintySampler = null)
            : this(
                (assembly
                    ?? throw new ArgumentNullException(nameof(assembly)))
                    .Scenario,
                spatialEvidence,
                uncertaintySampler)
        {
        }

        public GameplayThrownExplosiveCandidateExecutionRoute(
            ScenarioDefinition scenarioDefinition,
            GameplayHeadlessSpatialEvidence spatialEvidence,
            IUncertaintySampler uncertaintySampler = null)
        {
            scenario = scenarioDefinition ?? throw new ArgumentNullException(
                nameof(scenarioDefinition));
            spatial = spatialEvidence ?? throw new ArgumentNullException(
                nameof(spatialEvidence));
            uncertainty = uncertaintySampler
                ?? new AddressedUncertaintySampler();
        }

        public string RouteId => Id;

        public bool Supports(GameplayCapabilityProfile profile)
        {
            if (profile == null
                || profile.Capability
                    != GameplaySemanticCapability.ThrowExplosive)
                return false;
            try
            {
                string consequence = profile.GetTrait("consequence");
                return GameplayCapabilityProfiles.GetSubjectKind(profile)
                        == GameplaySemanticSubjectKind.WorldPosition
                    && profile.GetTrait("delivery")
                        == "ballistic-landing-query"
                    && profile.GetTrait("targeting") == "world-area"
                    && profile.GetTrait("resource") == "inventory-quantity"
                    && (consequence == "smoke-field"
                        || consequence == "fire-field"
                        || consequence == "concussive-actor-ap"
                        || consequence == "blast-actor-and-destructible");
            }
            catch (KeyNotFoundException)
            {
                return false;
            }
        }

        public GameplayExecutableCandidateEvaluation Evaluate(
            GameplayDecisionContext context,
            GameplayCandidate candidate)
        {
            GameplayBasicCandidateRouteUtility.Require(
                context,
                candidate,
                Supports,
                Id);
            if (candidate.Intent is not GameplayWorldPositionIntent world)
                return Illegal(
                    context,
                    candidate,
                    "world-position-intent-required");
            ThrownExplosiveDefinition definition = FindDefinition(
                candidate.ActorId,
                candidate.Profile);
            if (definition == null)
                return Illegal(
                    context,
                    candidate,
                    "explosive-profile-not-owned");
            var worldQuery = new GameplayHeadlessExplosiveWorldQuery(
                context.State,
                spatial);
            if (!GameplayThrownExplosivePreparation.TryPrepare(
                    context.State,
                    scenario,
                    candidate.ActorId,
                    candidate.SubjectId,
                    definition,
                    world.Position,
                    worldQuery,
                    worldQuery,
                    uncertainty,
                    canEnterTurnMode: false,
                    out GameplayActionRecord action,
                    out ThrownExplosiveFailure failure))
                return Illegal(
                    context,
                    candidate,
                    "throw." + failure);
            ThrownExplosiveRecord record = FindRecord(action);
            int affectedActors = 0;
            int affectedProps = 0;
            int hostileActors = 0;
            int friendlyActors = 0;
            ScenarioActorDefinition thrower = scenario.GetActor(
                candidate.ActorId);
            foreach (BlastEffectRecord effect in record.BlastEffects)
            {
                if (effect.Exposure <= 0f) continue;
                if (effect.SubjectKind == BlastSubjectKind.Actor)
                {
                    affectedActors++;
                    if (record.SmokeField != null)
                        continue;
                    ScenarioActorDefinition affected = scenario.GetActor(
                        effect.EntityId);
                    if (thrower.Combat.IsHostileTo(
                            affected.Combat.AllegianceId))
                        hostileActors++;
                    else
                        friendlyActors++;
                }
                else if (effect.SubjectKind
                    == BlastSubjectKind.DestructibleProp)
                    affectedProps++;
            }
            int concussiveHostiles = 0;
            int concussiveFriendlies = 0;
            foreach (ConcussiveActionPointEffectRecord effect in
                record.ConcussiveEffects)
            {
                ScenarioActorDefinition affected = scenario.GetActor(
                    effect.ActorId);
                if (thrower.Combat.IsHostileTo(
                        affected.Combat.AllegianceId))
                    concussiveHostiles++;
                else
                    concussiveFriendlies++;
            }
            int fieldHostiles = 0;
            int fieldFriendlies = 0;
            if (record.FireField != null)
                foreach (GameplayActorSnapshot affected in
                    context.State.Session.Actors)
                {
                    if (affected.IsIncapacitated
                        || affected.Pose.Position.DistanceTo(
                            record.FireField.Origin)
                            > record.FireField.Definition.MaximumRadius)
                        continue;
                    ScenarioActorDefinition affectedDefinition =
                        scenario.GetActor(affected.ActorId);
                    if (thrower.Combat.IsHostileTo(
                            affectedDefinition.Combat.AllegianceId))
                        fieldHostiles++;
                    else
                        fieldFriendlies++;
                }
            return new GameplayExecutableCandidateEvaluation(
                Id,
                candidate,
                context.State.CanonicalHash,
                isLegal: true,
                failureCode: string.Empty,
                new GameplayCandidateOutcomeEstimate(new[]
                {
                    new GameplayCandidateOutcomeFeature(
                        "blast.affected-actors",
                        affectedActors),
                    new GameplayCandidateOutcomeFeature(
                        "blast.affected-destructibles",
                        affectedProps),
                    new GameplayCandidateOutcomeFeature(
                        "blast.hostile-actors",
                        hostileActors),
                    new GameplayCandidateOutcomeFeature(
                        "blast.friendly-actors",
                        friendlyActors),
                    new GameplayCandidateOutcomeFeature(
                        "field.smoke",
                        record.SmokeField == null ? 0f : 1f),
                    new GameplayCandidateOutcomeFeature(
                        "field.fire",
                        record.FireField == null ? 0f : 1f),
                    new GameplayCandidateOutcomeFeature(
                        "field.fire-hostile-actors",
                        fieldHostiles),
                    new GameplayCandidateOutcomeFeature(
                        "field.fire-friendly-actors",
                        fieldFriendlies),
                    new GameplayCandidateOutcomeFeature(
                        "concussive.affected-actors",
                        record.ConcussiveEffects.Count),
                    new GameplayCandidateOutcomeFeature(
                        "concussive.hostile-actors",
                        concussiveHostiles),
                    new GameplayCandidateOutcomeFeature(
                        "concussive.friendly-actors",
                        concussiveFriendlies),
                    new GameplayCandidateOutcomeFeature(
                        "cost.action-points",
                        action.Cost.ActionPoints),
                }),
                new[]
                {
                    spatial.CaptureEvidence(
                        "explosive-landing",
                        context.State,
                        record.LaunchOrigin,
                        record.ResolvedLanding),
                    spatial.CaptureEvidence(
                        "explosive-blast",
                        context.State,
                        record.ResolvedLanding,
                        record.IntendedLanding,
                        definition.AreaRadius),
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
                        "Thrown explosive preparation is missing.",
                        nameof(evaluation)));

        private ThrownExplosiveDefinition FindDefinition(
            string actorId,
            GameplayCapabilityProfile profile)
        {
            foreach (InventoryItemDefinition item in scenario.GetActor(actorId)
                .Inventory)
                if (item.ConsumablePower
                        is ThrownExplosiveDefinition explosive
                    && profile.Equals(
                        GameplayCapabilityProfiles.ThrowExplosive(explosive)))
                    return explosive;
            return null;
        }

        private static ThrownExplosiveRecord FindRecord(
            GameplayActionRecord action)
        {
            foreach (GameplayActionOutcome outcome in action.Outcomes)
                if (outcome is ThrownExplosiveActionOutcome thrown)
                    return thrown.Record;
            throw new InvalidOperationException(
                "Prepared explosive action has no throw outcome.");
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
