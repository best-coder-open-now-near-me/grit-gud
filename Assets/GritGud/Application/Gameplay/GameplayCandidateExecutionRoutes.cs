using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;

namespace GritGud.Application.Gameplay
{
    public readonly struct GameplayCandidateOutcomeFeature
    {
        public GameplayCandidateOutcomeFeature(string featureId, float value)
        {
            FeatureId = GameplayContentIdentity.RequireText(
                featureId,
                nameof(featureId));
            GameplayNumericPolicy.RequireFinite(value, nameof(value));
            Value = GameplayNumericPolicy.Normalize(value);
        }

        public string FeatureId { get; }
        public float Value { get; }
    }

    public sealed class GameplayCandidateOutcomeEstimate
    {
        public GameplayCandidateOutcomeEstimate(
            IEnumerable<GameplayCandidateOutcomeFeature> features)
        {
            var copy = new List<GameplayCandidateOutcomeFeature>(
                features ?? throw new ArgumentNullException(nameof(features)));
            copy.Sort((left, right) => StringComparer.Ordinal.Compare(
                left.FeatureId,
                right.FeatureId));
            for (int index = 1; index < copy.Count; index++)
                if (string.Equals(
                    copy[index - 1].FeatureId,
                    copy[index].FeatureId,
                    StringComparison.Ordinal))
                    throw new ArgumentException(
                        $"Outcome estimate feature '{copy[index].FeatureId}' is duplicated.",
                        nameof(features));
            Features = copy.AsReadOnly();
        }

        public IReadOnlyList<GameplayCandidateOutcomeFeature> Features { get; }

        public float GetValue(string featureId)
        {
            foreach (GameplayCandidateOutcomeFeature feature in Features)
                if (string.Equals(
                    feature.FeatureId,
                    featureId,
                    StringComparison.Ordinal))
                    return feature.Value;
            return 0f;
        }
    }

    public sealed class GameplayExecutableCandidateEvaluation
    {
        internal GameplayExecutableCandidateEvaluation(
            string routeId,
            GameplayCandidate candidate,
            string stateHash,
            bool isLegal,
            string failureCode,
            GameplayCandidateOutcomeEstimate expectedOutcome,
            IEnumerable<GameplayEvidenceRecord> evidence,
            object frozenPreparation)
        {
            RouteId = GameplayContentIdentity.RequireText(
                routeId,
                nameof(routeId));
            Candidate = candidate ?? throw new ArgumentNullException(
                nameof(candidate));
            StateHash = GameplayContentIdentity.RequireDigest(
                stateHash,
                nameof(stateHash));
            IsLegal = isLegal;
            FailureCode = isLegal
                ? string.Empty
                : GameplayContentIdentity.RequireText(
                    failureCode,
                    nameof(failureCode));
            ExpectedOutcome = expectedOutcome
                ?? new GameplayCandidateOutcomeEstimate(
                    Array.Empty<GameplayCandidateOutcomeFeature>());
            var evidenceCopy = new List<GameplayEvidenceRecord>(
                evidence ?? Array.Empty<GameplayEvidenceRecord>());
            foreach (GameplayEvidenceRecord item in evidenceCopy)
                if (item == null)
                    throw new ArgumentException(
                        "Candidate evidence cannot contain null entries.",
                        nameof(evidence));
            Evidence = evidenceCopy.AsReadOnly();
            if (isLegal && frozenPreparation == null)
                throw new ArgumentNullException(nameof(frozenPreparation));
            FrozenPreparation = frozenPreparation;
        }

        public string RouteId { get; }
        public GameplayCandidate Candidate { get; }
        public string StateHash { get; }
        public bool IsLegal { get; }
        public string FailureCode { get; }
        public GameplayCandidateOutcomeEstimate ExpectedOutcome { get; }
        public IReadOnlyList<GameplayEvidenceRecord> Evidence { get; }
        internal object FrozenPreparation { get; }
    }

    public interface IGameplayCandidateExecutionRoute
    {
        string RouteId { get; }

        bool Supports(GameplayCapabilityProfile profile);

        GameplayExecutableCandidateEvaluation Evaluate(
            GameplayDecisionContext context,
            GameplayCandidate candidate);

        GameplayTransitionPayload PreparePayload(
            GameplayDecisionContext context,
            GameplayExecutableCandidateEvaluation evaluation);
    }

    /// <summary>
    /// Exact-profile registry for executable simulation routes. Metadata-only
    /// capability coverage is insufficient: a candidate cannot be evaluated or
    /// selected unless precisely one concrete route owns its preparation.
    /// </summary>
    public sealed class GameplayCandidateExecutionRouteRegistry
    {
        private readonly GameplayCapabilityRegistry capabilities;
        private readonly GameplaySemanticTransitionPreparer transitions;
        private readonly List<IGameplayCandidateExecutionRoute> routes =
            new List<IGameplayCandidateExecutionRoute>();

        public GameplayCandidateExecutionRouteRegistry(
            GameplayCapabilityRegistry capabilityRegistry)
        {
            capabilities = capabilityRegistry ?? throw new ArgumentNullException(
                nameof(capabilityRegistry));
            transitions = new GameplaySemanticTransitionPreparer(capabilities);
        }

        public void Register(IGameplayCandidateExecutionRoute route)
        {
            if (route == null) throw new ArgumentNullException(nameof(route));
            foreach (IGameplayCandidateExecutionRoute existing in routes)
                if (string.Equals(
                    existing.RouteId,
                    route.RouteId,
                    StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        $"Candidate execution route '{route.RouteId}' is already registered.");
            routes.Add(route);
        }

        public bool Supports(GameplayCapabilityProfile profile) =>
            Find(profile, throwIfMissing: false) != null;

        public GameplayExecutableCandidateEvaluation Evaluate(
            GameplayDecisionContext context,
            GameplayCandidate candidate)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (candidate == null)
                throw new ArgumentNullException(nameof(candidate));
            capabilities.RequireCandidateRoute(candidate);
            if (!string.Equals(
                    context.ActorId,
                    candidate.ActorId,
                    StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "A decision context cannot evaluate another actor's candidate.");
            return Find(candidate.Profile, throwIfMissing: true)
                .Evaluate(context, candidate);
        }

        public GameplaySemanticTransition Prepare(
            GameplayDecisionContext context,
            GameplayExecutableCandidateEvaluation evaluation)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (evaluation == null)
                throw new ArgumentNullException(nameof(evaluation));
            if (!evaluation.IsLegal)
                throw new InvalidOperationException(
                    $"Illegal candidate '{evaluation.Candidate.CandidateId}' cannot be prepared ({evaluation.FailureCode}).");
            if (!string.Equals(
                    context.State.CanonicalHash,
                    evaluation.StateHash,
                    StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Candidate evaluation is stale for the canonical state.");
            IGameplayCandidateExecutionRoute route = Find(
                evaluation.Candidate.Profile,
                throwIfMissing: true);
            if (!string.Equals(
                    route.RouteId,
                    evaluation.RouteId,
                    StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Candidate evaluation belongs to a different execution route.");
            GameplayTransitionPayload payload = route.PreparePayload(
                context,
                evaluation);
            return transitions.Prepare(
                context,
                evaluation.Candidate,
                payload,
                evaluation.Evidence);
        }

        private IGameplayCandidateExecutionRoute Find(
            GameplayCapabilityProfile profile,
            bool throwIfMissing)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            IGameplayCandidateExecutionRoute match = null;
            foreach (IGameplayCandidateExecutionRoute route in routes)
            {
                if (!route.Supports(profile)) continue;
                if (match != null)
                    throw new InvalidOperationException(
                        $"Capability '{profile.Signature}' has multiple concrete candidate routes.");
                match = route;
            }
            if (match == null && throwIfMissing)
                throw new NotSupportedException(
                    $"Capability '{profile.Signature}' has no concrete candidate execution route.");
            return match;
        }
    }

    public sealed class GameplayActorAttackCandidateExecutionRoute :
        IGameplayCandidateExecutionRoute
    {
        public const string Id = "actor-attack.v1";

        private readonly ScenarioDefinition scenario;
        private readonly GameplayHeadlessSpatialEvidence spatial;
        private readonly GameplayActorAttackTransitionPreparer attacks;

        public GameplayActorAttackCandidateExecutionRoute(
            GameplayScenarioAssembly assembly,
            GameplayHeadlessSpatialEvidence spatialEvidence)
            : this(
                (assembly ?? throw new ArgumentNullException(nameof(assembly)))
                    .Scenario,
                assembly.TacticalRules,
                spatialEvidence)
        {
        }

        public GameplayActorAttackCandidateExecutionRoute(
            ScenarioDefinition scenarioDefinition,
            IEnumerable<TacticalContextRuleDefinition> tacticalRules,
            GameplayHeadlessSpatialEvidence spatialEvidence)
        {
            scenario = scenarioDefinition ?? throw new ArgumentNullException(
                nameof(scenarioDefinition));
            spatial = spatialEvidence ?? throw new ArgumentNullException(
                nameof(spatialEvidence));
            attacks = new GameplayActorAttackTransitionPreparer(
                scenario,
                new GameplayHeadlessTacticalContextQuery(spatial),
                new GameplayTacticalContextEvaluator(
                    tacticalRules ?? throw new ArgumentNullException(
                        nameof(tacticalRules))));
        }

        public string RouteId => Id;

        public bool Supports(GameplayCapabilityProfile profile)
        {
            if (profile == null
                || profile.Capability != GameplaySemanticCapability.DirectAttack)
                return false;
            try
            {
                return GameplayCapabilityProfiles.GetSubjectKind(profile)
                        == GameplaySemanticSubjectKind.Actor
                    && profile.GetTrait("resource") == "equipped-weapon"
                    && profile.GetTrait("targeting") == "semantic-subject"
                    && profile.GetTrait("consequence") == "actor-wound"
                    && (profile.GetTrait("delivery") == "immediate-ranged"
                        || profile.GetTrait("delivery") == "contact");
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
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (candidate == null)
                throw new ArgumentNullException(nameof(candidate));
            if (!Supports(candidate.Profile))
                throw new NotSupportedException(
                    $"Route '{Id}' cannot evaluate '{candidate.Profile.Signature}'.");
            if (!context.Observation.ObservesActor(candidate.SubjectId))
                return Illegal(context, candidate, "target-unobserved");

            ScenarioActorDefinition attacker = scenario.GetActor(
                candidate.ActorId);
            ScenarioActorDefinition target = scenario.GetActor(
                candidate.SubjectId);
            if (!attacker.Combat.IsHostileTo(target.Combat.AllegianceId))
                return Illegal(context, candidate, "target-not-hostile");

            TargetExposureSnapshot exposure =
                GameplayHeadlessEncounterEvidence.CaptureSight(
                    context.State,
                    spatial,
                    candidate.ActorId,
                    candidate.SubjectId);
            if (exposure.VisibleSampleCount == 0)
                return Illegal(context, candidate, "target-not-exposed");
            if (!attacks.TryEvaluate(
                    context.State,
                    candidate.ActorId,
                    exposure,
                    out GameplayActorAttackEvaluation attack,
                    out AttackResolutionFailure failure))
                return Illegal(
                    context,
                    candidate,
                    "attack." + failure.ToString());
            if (!candidate.Profile.Equals(attack.Profile))
                return Illegal(context, candidate, "equipped-profile-mismatch");

            var estimate = new GameplayCandidateOutcomeEstimate(new[]
            {
                new GameplayCandidateOutcomeFeature(
                    "attack.hit-probability",
                    attack.FinalHitChancePercent / 100f),
                new GameplayCandidateOutcomeFeature(
                    "attack.injury-on-hit",
                    1f),
                new GameplayCandidateOutcomeFeature(
                    "cost.action-points",
                    attack.Cost.ActionPoints),
                new GameplayCandidateOutcomeFeature(
                    "cost.movement-opportunity",
                    attack.Cost.MovementOpportunity),
                new GameplayCandidateOutcomeFeature(
                    "target.functional-reserve",
                    (attack.Target.Capabilities.MovementCapacity
                        + attack.Target.Capabilities.AimStability
                        + attack.Target.Capabilities.GripCapacity)
                        / 100f),
            });
            return new GameplayExecutableCandidateEvaluation(
                Id,
                candidate,
                context.State.CanonicalHash,
                isLegal: true,
                failureCode: string.Empty,
                estimate,
                attack.Evidence,
                attack);
        }

        public GameplayTransitionPayload PreparePayload(
            GameplayDecisionContext context,
            GameplayExecutableCandidateEvaluation evaluation)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            GameplayActorAttackEvaluation attack = evaluation?.FrozenPreparation
                    as GameplayActorAttackEvaluation
                ?? throw new ArgumentException(
                    "Actor attack route requires its frozen attack evaluation.",
                    nameof(evaluation));
            GameplayPreparedTransition<GameplayActionRecord> prepared = attacks
                .Resolve(context.State, attack);
            return new GameplayWeaponTransitionPayload(
                evaluation.Candidate.Profile,
                prepared.Record);
        }

        private static GameplayExecutableCandidateEvaluation Illegal(
            GameplayDecisionContext context,
            GameplayCandidate candidate,
            string failureCode) => new GameplayExecutableCandidateEvaluation(
                Id,
                candidate,
                context.State.CanonicalHash,
                isLegal: false,
                failureCode,
                expectedOutcome: null,
                evidence: null,
                frozenPreparation: null);
    }

    public sealed class GameplayEndTurnCandidateExecutionRoute :
        IGameplayCandidateExecutionRoute
    {
        public const string Id = "end-turn.v1";

        private readonly float minimumVoluntaryTurnSeconds;

        public GameplayEndTurnCandidateExecutionRoute(
            ScenarioDefinition scenario)
        {
            minimumVoluntaryTurnSeconds = (scenario
                ?? throw new ArgumentNullException(nameof(scenario)))
                .Timing.MinimumVoluntaryTurnSeconds;
        }

        public string RouteId => Id;

        public bool Supports(GameplayCapabilityProfile profile) =>
            profile != null
            && (profile.Equals(GameplayCapabilityProfiles.EndTurn(
                    emergency: false))
                || profile.Equals(GameplayCapabilityProfiles.EndTurn(
                    emergency: true)));

        public GameplayExecutableCandidateEvaluation Evaluate(
            GameplayDecisionContext context,
            GameplayCandidate candidate)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            GameplaySessionStateSnapshot session = context.State.Session;
            bool emergency = candidate.Profile.Equals(
                GameplayCapabilityProfiles.EndTurn(emergency: true));
            string failure = session.Mode != GameplaySessionMode.TurnBased
                ? "turn-mode-required"
                : session.Operation != GameplaySessionOperation.None
                    ? "operation-in-progress"
                    : !string.Equals(
                        session.ActiveActorId,
                        candidate.ActorId,
                        StringComparison.Ordinal)
                        ? "actor-not-active"
                        : emergency
                            && session.TurnPhase
                                != GameplayTurnPhase.EmergencyReaction
                            ? "emergency-phase-required"
                            : !emergency
                                && session.TurnPhase
                                    == GameplayTurnPhase.EmergencyReaction
                                ? "normal-phase-required"
                                : string.Empty;
            bool legal = failure.Length == 0;
            return new GameplayExecutableCandidateEvaluation(
                Id,
                candidate,
                context.State.CanonicalHash,
                legal,
                failure,
                new GameplayCandidateOutcomeEstimate(new[]
                {
                    new GameplayCandidateOutcomeFeature("turn.end", 1f),
                    new GameplayCandidateOutcomeFeature(
                        "turn.saved-action-points",
                        session.GetActor(candidate.ActorId)
                            .TurnBudget.ActionPoints),
                }),
                evidence: null,
                frozenPreparation: legal ? candidate : null);
        }

        public GameplayTransitionPayload PreparePayload(
            GameplayDecisionContext context,
            GameplayExecutableCandidateEvaluation evaluation) =>
            new GameplayEndTurnTransitionPayload(
                evaluation.Candidate.ActorId,
                evaluation.Candidate.Profile.Equals(
                    GameplayCapabilityProfiles.EndTurn(emergency: true)),
                minimumVoluntaryTurnSeconds);
    }
}
